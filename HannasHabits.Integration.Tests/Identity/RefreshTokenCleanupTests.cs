using System.ComponentModel.DataAnnotations;
using HannasHabits.Infrastructure.Identity;
using HannasHabits.Infrastructure.Persistence;
using HannasHabits.Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace HannasHabits.Integration.Tests.Identity;

// Expired refresh tokens are deleted so the table does not grow without bound. A database of its own per test: the
// cleanup is global (it looks at every user), so it must not see the rows of other tests.
[Collection(IntegrationCollection.Name)]
public class RefreshTokenCleanupTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Start = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Retention = TimeSpan.FromDays(7);

    private readonly TestEnvironment _environment;
    private readonly FakeTimeProvider _clock = new(Start);
    private string _connectionString = null!;
    private ApplicationDbContext _db = null!;
    private Guid _user;

    public RefreshTokenCleanupTests(TestEnvironment environment)
    {
        _environment = environment;
    }

    public async Task InitializeAsync()
    {
        _connectionString = await _environment.CreateDatabaseAsync();
        _db = _environment.CreateContext(_connectionString);
        _user = await Sql.InsertUserAsync(_db);
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private static DateTime Cutoff => Start.UtcDateTime - Retention;

    private RefreshTokenCleaner Cleaner(int retentionDays = 7)
        => new(_db, Options.Create(new RefreshTokenCleanupOptions { RetentionDays = retentionDays }), _clock);

    // A token of the usual 30 days that ends at the given moment.
    private async Task<Guid> AddToken(DateTime expiresAt, Guid? user = null, bool rotated = false, bool loggedOut = false)
    {
        var token = RefreshToken.Create(user ?? _user, Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"), expiresAt.AddDays(-30), TimeSpan.FromDays(30));

        if (rotated)
            token.Rotate(RefreshToken.Create(token.UserId, Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"), Start.UtcDateTime, TimeSpan.FromDays(30)), Start.UtcDateTime);

        _db.RefreshTokens.Add(token);
        await _db.SaveChangesAsync();

        if (loggedOut)
            await _db.RefreshTokens.Where(t => t.Id == token.Id).ExecuteUpdateAsync(set => set.SetProperty(t => t.RevokedAt, (DateTime?)Start.UtcDateTime));

        return token.Id;
    }

    private async Task<bool> Exists(Guid id)
    {
        await using var context = _environment.CreateContext(_connectionString);
        return await context.RefreshTokens.AnyAsync(t => t.Id == id);
    }

    private async Task<Guid[]> Remaining()
    {
        await using var context = _environment.CreateContext(_connectionString);
        return await context.RefreshTokens.Select(t => t.Id).ToArrayAsync();
    }

    // ---- which rows go ----

    [Fact]
    public async Task ARowThatExpiredLongerAgoThanTheRetention_IsDeleted()
    {
        var old = await AddToken(Cutoff.AddSeconds(-1));

        var deleted = await Cleaner().DeleteExpiredAsync();

        Assert.Equal(1, deleted);
        Assert.False(await Exists(old));
    }

    [Fact]
    public async Task ARowThatExpiredWithinTheRetention_IsKept_AlsoExactlyOnTheBoundary()
    {
        var onTheBoundary = await AddToken(Cutoff);
        var justAfter = await AddToken(Cutoff.AddSeconds(1));
        var expiredYesterday = await AddToken(Start.UtcDateTime.AddDays(-1));

        var deleted = await Cleaner().DeleteExpiredAsync();

        Assert.Equal(0, deleted);
        Assert.True(await Exists(onTheBoundary));
        Assert.True(await Exists(justAfter));
        Assert.True(await Exists(expiredYesterday));
    }

    [Fact]
    public async Task AnActiveRow_IsKept()
    {
        var active = await AddToken(Start.UtcDateTime.AddDays(30));

        await Cleaner().DeleteExpiredAsync();

        Assert.True(await Exists(active));
    }

    [Fact]
    public async Task OnlyExpiryDecides_ALoggedOutRowStaysUntilItExpired_ARotatedRowGoesOnceItsTimeIsUp()
    {
        var loggedOutButNotExpired = await AddToken(Start.UtcDateTime.AddDays(10), loggedOut: true);

        // A rotated token must outlive the moment it is replayed - it is used up, but replay detection needs the row
        // (the "rotated" mark) for as long as the stolen copy could still work, i.e. until it expires.
        var rotatedAndNotExpired = await AddToken(Start.UtcDateTime.AddDays(5), rotated: true);
        var rotatedAndLongExpired = await AddToken(Cutoff.AddDays(-1), rotated: true);

        await Cleaner().DeleteExpiredAsync();

        Assert.True(await Exists(loggedOutButNotExpired));
        Assert.True(await Exists(rotatedAndNotExpired));
        Assert.False(await Exists(rotatedAndLongExpired));
    }

    [Fact]
    public async Task RowsOfEveryUser_AreCleaned_AndOnlyTheExpiredOnes()
    {
        var otherUser = await Sql.InsertUserAsync(_db);
        var mine = await AddToken(Cutoff.AddDays(-3));
        var theirsExpired = await AddToken(Cutoff.AddDays(-2), otherUser);
        var theirsActive = await AddToken(Start.UtcDateTime.AddDays(20), otherUser);

        var deleted = await Cleaner().DeleteExpiredAsync();

        Assert.Equal(2, deleted);
        Assert.Equal(new[] { theirsActive }, await Remaining());
        Assert.False(await Exists(mine));
        Assert.False(await Exists(theirsExpired));
    }

    [Fact]
    public async Task TheRetentionIsConfigurable_ZeroDeletesEverythingExpired()
    {
        var justExpired = await AddToken(Start.UtcDateTime.AddSeconds(-1));
        var notYet = await AddToken(Start.UtcDateTime.AddSeconds(1));

        var deleted = await Cleaner(retentionDays: 0).DeleteExpiredAsync();

        Assert.Equal(1, deleted);
        Assert.False(await Exists(justExpired));
        Assert.True(await Exists(notYet));
    }

    [Fact]
    public async Task ItFollowsTheClock_ARowBecomesGarbageOnceTheRetentionPassed()
    {
        var token = await AddToken(Start.UtcDateTime.AddDays(1));
        var cleaner = Cleaner();

        _clock.Advance(TimeSpan.FromDays(1) + Retention); // exactly on the boundary
        Assert.Equal(0, await cleaner.DeleteExpiredAsync());
        Assert.True(await Exists(token));

        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(1, await cleaner.DeleteExpiredAsync());
        Assert.False(await Exists(token));
    }

    [Fact]
    public async Task WithNothingToDelete_ItDeletesNothing_AndRunningTwiceIsHarmless()
    {
        Assert.Equal(0, await Cleaner().DeleteExpiredAsync());

        await AddToken(Cutoff.AddDays(-1));
        Assert.Equal(1, await Cleaner().DeleteExpiredAsync());
        Assert.Equal(0, await Cleaner().DeleteExpiredAsync());
    }

    // ---- the schedule ----

    // The hosted service on the real database with a clock the test moves by hand. Every run gets a scope of its own.
    private sealed class CleanupHost : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly ILoggerFactory _loggerFactory;
        public RefreshTokenCleanupService Service { get; }
        public LogCollector Logs { get; } = new();

        public CleanupHost(string connectionString, FakeTimeProvider clock, Func<IServiceScopeFactory, IServiceScopeFactory>? wrapScopes = null, int intervalHours = 6)
        {
            _provider = TestServices.Build(connectionString, clock);
            var scopes = _provider.GetRequiredService<IServiceScopeFactory>();
            _loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(Logs));

            Service = new RefreshTokenCleanupService(
                wrapScopes?.Invoke(scopes) ?? scopes,
                Options.Create(new RefreshTokenCleanupOptions { IntervalHours = intervalHours }),
                clock,
                _loggerFactory.CreateLogger<RefreshTokenCleanupService>());
        }

        public async ValueTask DisposeAsync()
        {
            await Service.StopAsync(CancellationToken.None);
            Service.Dispose();
            await _provider.DisposeAsync();
            _loggerFactory.Dispose();
        }
    }

    private static async Task Eventually(Func<Task<bool>> condition, string what)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!await condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new Xunit.Sdk.XunitException($"Timed out waiting for: {what}");

            await Task.Delay(25);
        }
    }

    // Time for a run that must NOT happen to show up if it wrongly did.
    private static Task Settle() => Task.Delay(300);

    [Fact]
    public async Task TheServiceCleansRightAtStartup_ThenOncePerInterval_NotBefore()
    {
        var first = await AddToken(Cutoff.AddDays(-1));
        await using var host = new CleanupHost(_connectionString, _clock);

        await host.Service.StartAsync(CancellationToken.None);
        await Eventually(async () => !await Exists(first), "the run at startup");

        var second = await AddToken(Cutoff.AddDays(-1));
        _clock.Advance(TimeSpan.FromHours(5));
        await Settle();
        Assert.True(await Exists(second)); // 5 h < 6 h interval

        _clock.Advance(TimeSpan.FromHours(1));
        await Eventually(async () => !await Exists(second), "the run after one interval");
    }

    [Fact]
    public async Task AFailingRun_IsLogged_AndDoesNotEndTheLoop()
    {
        var garbage = await AddToken(Cutoff.AddDays(-1));
        var database = new DatabaseDown();
        await using var host = new CleanupHost(_connectionString, _clock, scopes => new FlakyScopeFactory(scopes, database));

        await host.Service.StartAsync(CancellationToken.None);
        await Eventually(() => Task.FromResult(database.Failures == 1), "the first (failing) run");

        Assert.True(await Exists(garbage));
        Assert.False(host.Service.ExecuteTask!.IsCompleted); // an exception escaping would also stop a real host
        var error = Assert.Single(host.Logs.Errors);
        Assert.Contains("Cleaning up expired refresh tokens failed", error);
        Assert.Contains("database is down", error);

        database.Up = true;
        _clock.Advance(TimeSpan.FromHours(6));
        await Eventually(async () => !await Exists(garbage), "the next run after the failure");
    }

    [Fact]
    public async Task Stopping_EndsTheLoopWithoutAnError()
    {
        await using var host = new CleanupHost(_connectionString, _clock);
        await host.Service.StartAsync(CancellationToken.None);

        await host.Service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(host.Service.ExecuteTask!.IsCompletedSuccessfully);
        Assert.Empty(host.Logs.Errors);
    }

    private sealed class DatabaseDown
    {
        private int _failures;
        public volatile bool Up;
        public int Failures => Volatile.Read(ref _failures);
        public void Failed() => Interlocked.Increment(ref _failures);
    }

    private sealed class FlakyScopeFactory : IServiceScopeFactory
    {
        private readonly IServiceScopeFactory _inner;
        private readonly DatabaseDown _database;

        public FlakyScopeFactory(IServiceScopeFactory inner, DatabaseDown database)
        {
            _inner = inner;
            _database = database;
        }

        public IServiceScope CreateScope()
        {
            if (_database.Up)
                return _inner.CreateScope();

            _database.Failed();
            throw new InvalidOperationException("the database is down");
        }
    }

    // ---- wiring and configuration ----

    [Fact]
    public async Task TheRealApplication_RunsTheCleanupAsAHostedService()
    {
        await using var provider = TestServices.Build(_connectionString);

        Assert.Single(provider.GetServices<IHostedService>().OfType<RefreshTokenCleanupService>());

        // ... and the full web host registers it too (the cleaner needs the scoped DbContext, so it is resolved in a scope).
        Assert.Single(_environment.Api.Services.GetServices<IHostedService>().OfType<RefreshTokenCleanupService>());
        await using var scope = _environment.Api.Services.CreateAsyncScope();
        Assert.NotNull(scope.ServiceProvider.GetService<RefreshTokenCleaner>());
    }

    [Fact]
    public void TheDefaults_AreEverySixHours_AndAWeekOfGrace()
    {
        var options = new RefreshTokenCleanupOptions();

        Assert.Equal(6, options.IntervalHours);
        Assert.Equal(7, options.RetentionDays);
        Assert.Empty(Validate(options));
    }

    [Theory]
    [InlineData(0, 7)]
    [InlineData(-1, 7)]
    [InlineData(169, 7)]
    [InlineData(6, -1)]
    [InlineData(6, 366)]
    public void ValuesOutsideTheRange_AreRejected(int intervalHours, int retentionDays)
    {
        Assert.NotEmpty(Validate(new RefreshTokenCleanupOptions { IntervalHours = intervalHours, RetentionDays = retentionDays }));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(168, 365)]
    public void TheBoundsThemselves_AreAllowed(int intervalHours, int retentionDays)
    {
        Assert.Empty(Validate(new RefreshTokenCleanupOptions { IntervalHours = intervalHours, RetentionDays = retentionDays }));
    }

    private static List<ValidationResult> Validate(RefreshTokenCleanupOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }
}
