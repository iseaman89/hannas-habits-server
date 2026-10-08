using HannasHabits.Application;
using HannasHabits.Application.Common.Interfaces;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.Time.Testing;

namespace HannasHabits.Application.Tests.Fakes;

/// <summary>The signed-in user of a test; a fresh random user unless a test says otherwise.</summary>
internal sealed class FakeCurrentUser : ICurrentUser
{
    public Guid UserId { get; set; } = Guid.NewGuid();
}

/// <summary>
/// Counts the saves and can interfere with them: <see cref="BeforeSave"/> runs before each save (1-based call number)
/// and may throw to simulate what the database does (a unique violation, a concurrent delete, ...).
/// </summary>
internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Action<int>? BeforeSave { get; set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        BeforeSave?.Invoke(SaveCount);
        return Task.FromResult(1);
    }
}

internal static class TestClock
{
    /// <summary>Thursday, 8 October 2026, noon UTC - the calendar the tests are written against.</summary>
    public static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    public static readonly DateOnly Today = new(2026, 10, 8);

    public static FakeTimeProvider Create() => new(Now);
}

internal static class TestMapper
{
    /// <summary>The real Mapster configuration of the application (all <c>IRegister</c>s), so mapping bugs show up here.</summary>
    public static IMapper Instance { get; } = Create();

    private static IMapper Create()
    {
        var config = new TypeAdapterConfig();
        config.Scan(typeof(DependencyInjection).Assembly);
        return new Mapper(config);
    }
}
