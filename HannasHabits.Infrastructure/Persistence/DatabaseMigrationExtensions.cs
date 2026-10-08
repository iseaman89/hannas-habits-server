using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HannasHabits.Infrastructure.Persistence;

public static class DatabaseMigrationExtensions
{
    /// <summary>
    /// Applies the EF Core migrations that the database has not seen yet (creates the database schema on an empty one).
    /// Lives here so the host can trigger it without knowing EF Core: only Infrastructure does.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync(cancellationToken);
    }
}
