using HannasHabits.Application;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Infrastructure;
using HannasHabits.Infrastructure.Persistence;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HannasHabits.Integration.Tests.Support;

/// <summary>
/// The Infrastructure layer wired up like the real host does it (<c>AddInfrastructure</c>), without the web server:
/// real DbContext, Identity, repositories, token service. A test can swap the clock and add EF interceptors.
/// </summary>
public static class TestServices
{
    public static ServiceProvider Build(string connectionString, TimeProvider? clock = null, params IInterceptor[] interceptors)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DbConnection"] = connectionString,
            ["Jwt:Key"] = ApiFactory.JwtKey,
            ["Jwt:Issuer"] = ApiFactory.JwtIssuer,
            ["Jwt:Audience"] = ApiFactory.JwtAudience,
            ["Google:ClientId"] = "tests.apps.googleusercontent.com"
        }).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        // The web host registers these for Identity's token providers and sign-in manager; a bare container does not.
        services.AddDataProtection();
        services.AddAuthentication();
        services.AddInfrastructure(configuration);

        if (clock is not null)
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton(clock);
        }

        if (interceptors.Length > 0)
        {
            // AddDbContext registers its options with TryAdd, so the first registration (from AddInfrastructure) would win.
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString).AddInterceptors(interceptors));
        }

        return services.BuildServiceProvider(validateScopes: true);
    }
}

/// <summary>The signed-in user of a test.</summary>
public sealed class FakeCurrentUser : ICurrentUser
{
    public FakeCurrentUser(Guid userId)
    {
        UserId = userId;
    }

    public Guid UserId { get; }
}

public static class TestMapper
{
    /// <summary>The application's real Mapster configuration.</summary>
    public static IMapper Instance { get; } = Create();

    private static IMapper Create()
    {
        var config = new TypeAdapterConfig();
        config.Scan(typeof(HannasHabits.Application.DependencyInjection).Assembly);
        return new Mapper(config);
    }
}

/// <summary>
/// Wraps the real unit of work and, right before the FIRST save of this instance, waits for the other participants of a
/// <see cref="Barrier"/> and/or runs a hook. That forces "both requests have loaded, now both save" - the interleaving a
/// race test needs and that a plain parallel test only hits by luck.
/// </summary>
public sealed class RaceUnitOfWork : IUnitOfWork
{
    private readonly IUnitOfWork _inner;
    private readonly Barrier? _barrier;
    private readonly Func<Task>? _beforeFirstSave;
    private bool _armed = true;

    /// <summary>What the database answered to the save, if it refused - to show the race really took place.</summary>
    public Exception? SaveFailure { get; private set; }

    public RaceUnitOfWork(IUnitOfWork inner, Barrier? barrier = null, Func<Task>? beforeFirstSave = null)
    {
        _inner = inner;
        _barrier = barrier;
        _beforeFirstSave = beforeFirstSave;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (_armed)
        {
            _armed = false;

            if (_barrier is not null && !_barrier.SignalAndWait(TimeSpan.FromSeconds(20)))
                throw new TimeoutException("The other participant of the race never arrived.");

            if (_beforeFirstSave is not null)
                await _beforeFirstSave();
        }

        try
        {
            return await _inner.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            SaveFailure ??= exception;
            throw;
        }
    }
}

/// <summary>Lets the first save of every context wait for the other participants of a <see cref="Barrier"/> (once armed).</summary>
public sealed class BarrierSaveInterceptor : SaveChangesInterceptor
{
    private readonly Barrier _barrier;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<DbContext, bool> _passed = new();

    public BarrierSaveInterceptor(Barrier barrier)
    {
        _barrier = barrier;
    }

    /// <summary>Off while a test sets the stage, on for the saves under test.</summary>
    public volatile bool Armed;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Armed && eventData.Context is { } context && _passed.TryAdd(context, true))
        {
            if (!_barrier.SignalAndWait(TimeSpan.FromSeconds(20)))
                throw new TimeoutException("The other participant of the race never arrived.");
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
