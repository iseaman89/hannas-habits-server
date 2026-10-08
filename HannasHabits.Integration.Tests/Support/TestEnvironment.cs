using HannasHabits.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Testcontainers.PostgreSql;

namespace HannasHabits.Integration.Tests.Support;

/// <summary>
/// One PostgreSQL container for the whole test run (starting one per test class would take minutes). The migrations are
/// applied once to a template database; every test class that needs a database of its own copies the template, which
/// takes a fraction of a second. The tests that go through the API share one database and keep apart by user:
/// everything the application stores is filtered by the owner anyway.
/// </summary>
public sealed class TestEnvironment : IAsyncLifetime
{
    // The same major version the project is developed against; pulling a different one would hide version differences.
    private const string Image = "postgres:17-alpine";
    private const string TemplateDatabase = "hh_template";

    static TestEnvironment()
    {
        UseTheApiVersionOfAnOldDockerEngine();
    }

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image).Build();
    private readonly Lazy<ApiFactory> _api;

    public TestEnvironment()
    {
        _api = new Lazy<ApiFactory>(() => new ApiFactory(SharedConnectionString));
    }

    /// <summary>The database the API tests run against (the application under test is wired to it).</summary>
    public string SharedConnectionString { get; private set; } = string.Empty;

    /// <summary>The application under test, started on first use.</summary>
    public ApiFactory Api => _api.Value;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await ExecuteAsAdminAsync($"CREATE DATABASE {TemplateDatabase}");
        await using (var context = CreateContext(ConnectionStringFor(TemplateDatabase)))
            await context.Database.MigrateAsync();

        // CREATE DATABASE ... TEMPLATE refuses to run while anybody is connected to the template.
        NpgsqlConnection.ClearAllPools();

        SharedConnectionString = await CreateDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        if (_api.IsValueCreated)
            await _api.Value.DisposeAsync();

        await _container.DisposeAsync();
    }

    /// <summary>A new, fully migrated, empty database; returns its connection string.</summary>
    public async Task<string> CreateDatabaseAsync()
    {
        var name = "hh_" + Guid.NewGuid().ToString("N");
        await ExecuteAsAdminAsync($"CREATE DATABASE {name} TEMPLATE {TemplateDatabase}");
        return ConnectionStringFor(name);
    }

    /// <summary>A new, EMPTY database without any table (for tests that run the migrations themselves).</summary>
    public async Task<string> CreateBlankDatabaseAsync()
    {
        var name = "hh_blank_" + Guid.NewGuid().ToString("N");
        await ExecuteAsAdminAsync($"CREATE DATABASE {name}");
        return ConnectionStringFor(name);
    }

    public ApplicationDbContext CreateContext(string? connectionString = null, params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString ?? SharedConnectionString);

        if (interceptors.Length > 0)
            options.AddInterceptors(interceptors);

        return new ApplicationDbContext(options.Options);
    }

    // Testcontainers talks to the Docker engine in API version 1.44 unless told otherwise, and an engine older than
    // Docker 25 (e.g. 23.0.5 = API 1.42) answers "client version 1.44 is too new". So: if the installed engine is older,
    // ask for its version instead. Anything the developer set explicitly (DOCKER_API_VERSION) wins.
    private static void UseTheApiVersionOfAnOldDockerEngine()
    {
        if (Environment.GetEnvironmentVariable("DOCKER_API_VERSION") is not null)
            return;

        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("docker", ["version", "--format", "{{.Server.APIVersion}}"])
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            if (process is null || !process.WaitForExit(15_000))
                return;

            var output = process.StandardOutput.ReadToEnd().Trim();
            if (Version.TryParse(output, out var server) && server < new Version(1, 44))
                Environment.SetEnvironmentVariable("DOCKER_API_VERSION", output);
        }
        catch (Exception)
        {
            // No docker CLI (or not on the PATH): Testcontainers finds the engine on its own, nothing to adjust.
        }
    }

    private string ConnectionStringFor(string database)
        => new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = database }.ConnectionString;

    private async Task ExecuteAsAdminAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<TestEnvironment>
{
    public const string Name = "Integration";
}
