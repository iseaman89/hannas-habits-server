using HannasHabits.Infrastructure.Persistence;

namespace HannasHabits.WebApi.Extensions;

public static class WebApplicationExtensions
{
    public const string MigrateOnStartupKey = "Database:MigrateOnStartup";

    /// <summary>
    /// Brings the database up to date before the first request, if <c>Database:MigrateOnStartup</c> is <c>true</c>
    /// (off by default; docker-compose switches it on). Convenient for one instance with its own database.
    /// <para>
    /// The catch: EF Core 8 takes no lock, so several instances starting at the same time would race each other.
    /// With more than one instance, migrate as a separate step instead (<c>dotnet ef database update</c> or a
    /// migration bundle in the deployment pipeline) and leave this off.
    /// </para>
    /// </summary>
    public static async Task ApplyMigrationsIfConfiguredAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>(MigrateOnStartupKey))
            return;

        app.Logger.LogInformation("Applying pending database migrations");
        await app.Services.MigrateDatabaseAsync();
    }
}
