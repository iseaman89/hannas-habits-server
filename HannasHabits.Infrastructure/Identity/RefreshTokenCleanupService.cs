using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HannasHabits.Infrastructure.Identity;

/// <summary>
/// Runs <see cref="RefreshTokenCleaner"/> once at startup and then every <see cref="RefreshTokenCleanupOptions.IntervalHours"/>.
/// It only schedules: a hosted service is a singleton and the DbContext behind the cleaner is scoped, so every run
/// gets a scope of its own, like one HTTP request would.
/// </summary>
public class RefreshTokenCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RefreshTokenCleanupOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RefreshTokenCleanupService> _logger;

    public RefreshTokenCleanupService(
        IServiceScopeFactory scopeFactory,
        IOptions<RefreshTokenCleanupOptions> options,
        TimeProvider timeProvider,
        ILogger<RefreshTokenCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(_options.IntervalHours), _timeProvider);

        try
        {
            do
            {
                await RunOnceAsync(stoppingToken);
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is shutting down.
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var deleted = await scope.ServiceProvider.GetRequiredService<RefreshTokenCleaner>().DeleteExpiredAsync(cancellationToken);

            _logger.Log(deleted > 0 ? LogLevel.Information : LogLevel.Debug, "Deleted {Count} expired refresh tokens", deleted);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // A database that is down for a moment must not end the loop - and an unhandled exception in a
            // BackgroundService stops the whole host. The next tick tries again.
            _logger.LogError(exception, "Cleaning up expired refresh tokens failed; trying again at the next run");
        }
    }
}
