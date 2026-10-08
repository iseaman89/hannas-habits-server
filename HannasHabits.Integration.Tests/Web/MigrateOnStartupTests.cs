using System.Net.Http.Json;
using HannasHabits.Integration.Tests.Support;
using HannasHabits.WebApi.Extensions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HannasHabits.Integration.Tests.Web;

// docker-compose starts the API on an empty PostgreSQL and expects it to create the schema itself. Off by default: a
// developer pointing the app at a database does not want it changed behind their back.
[Collection(IntegrationCollection.Name)]
public class MigrateOnStartupTests
{
    private readonly TestEnvironment _environment;

    public MigrateOnStartupTests(TestEnvironment environment)
    {
        _environment = environment;
    }

    // The factory itself keeps the log; the one with the extra setting is the one that serves requests.
    private static (ApiFactory Original, WebApplicationFactory<Program> Host) StartFactory(string connectionString, bool? migrateOnStartup)
    {
        var original = new ApiFactory(connectionString);

        return (original, migrateOnStartup is null
            ? original
            : original.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [WebApplicationExtensions.MigrateOnStartupKey] = migrateOnStartup.Value.ToString()
                }))));
    }

    private async Task<long> TablesNamed(string connectionString, string name)
    {
        await using var db = _environment.CreateContext(connectionString);
        return await Sql.ScalarAsync(db, "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = @p0", name);
    }

    [Fact]
    public async Task ByDefault_TheStartLeavesTheDatabaseAlone()
    {
        var connectionString = await _environment.CreateBlankDatabaseAsync();

        var (original, host) = StartFactory(connectionString, migrateOnStartup: null);
        await using var disposeLater = original;
        _ = host.Services; // starts the host

        Assert.Equal(0, await TablesNamed(connectionString, "Habits"));
        Assert.Equal(0, await TablesNamed(connectionString, "__EFMigrationsHistory"));
    }

    [Fact]
    public async Task ExplicitlyOff_TheStartLeavesTheDatabaseAlone()
    {
        var connectionString = await _environment.CreateBlankDatabaseAsync();

        var (original, host) = StartFactory(connectionString, migrateOnStartup: false);
        await using var disposeLater = original;
        _ = host.Services;

        Assert.Equal(0, await TablesNamed(connectionString, "Habits"));
    }

    [Fact]
    public async Task WhenOn_AnEmptyDatabaseGetsEveryMigration_BeforeTheAppServes()
    {
        var connectionString = await _environment.CreateBlankDatabaseAsync();

        var (original, host) = StartFactory(connectionString, migrateOnStartup: true);
        await using var disposeLater = original;
        var response = await host.CreateClient().PostAsJsonAsync("/api/auth/register", new { email = TestUser.NewEmail(), password = TestUser.Password }, Json.Options);

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        await using var db = _environment.CreateContext(connectionString);
        Assert.Equal(db.Database.GetMigrations().Count(), await Sql.ScalarAsync(db, "SELECT COUNT(*) FROM \"__EFMigrationsHistory\""));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Empty(original.Logs.Errors); // e.g. the cleanup service must not run into a missing table
    }

    [Fact]
    public async Task WhenOn_AStartOnAnUpToDateDatabase_ChangesNothing_AndKeepsTheData()
    {
        var connectionString = await _environment.CreateBlankDatabaseAsync();
        var email = TestUser.NewEmail();

        var (firstOriginal, first) = StartFactory(connectionString, migrateOnStartup: true);
        await using (firstOriginal)
        {
            var registered = await first.CreateClient().PostAsJsonAsync("/api/auth/register", new { email, password = TestUser.Password }, Json.Options);
            Assert.True(registered.IsSuccessStatusCode);
        }

        var (secondOriginal, second) = StartFactory(connectionString, migrateOnStartup: true);
        await using var disposeSecondLater = secondOriginal;
        var login = await second.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = TestUser.Password }, Json.Options);

        Assert.True(login.IsSuccessStatusCode, await login.Content.ReadAsStringAsync());
        Assert.Empty(secondOriginal.Logs.Errors);
    }
}
