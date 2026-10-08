using HannasHabits.Application.Auth;
using HannasHabits.Application.Common.Exceptions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace HannasHabits.Integration.Tests.Support;

/// <summary>
/// The real application (Program.cs, all layers, real PostgreSQL) in an in-memory test server. Only Google's token
/// check is replaced - there is no way to get a real Google ID token in a test.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string JwtIssuer = "hannas-habits-tests";
    public const string JwtAudience = "hannas-habits-tests";
    public const string JwtKey = "integration-tests-signing-key-0123456789-abcdefghij";
    public const string AllowedOrigin = "http://localhost:5173";

    private readonly string _connectionString;

    static ApiFactory()
    {
        // The CORS origins are read while Program.cs registers the services - before a configuration source added by
        // a test factory takes effect - so this one comes in through the environment (same value for every factory).
        Environment.SetEnvironmentVariable("Cors__AllowedOrigins__0", AllowedOrigin);
    }

    public ApiFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not "Development": that would load the developer's user-secrets - and with them the real database.
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DbConnection"] = _connectionString,
            ["Jwt:Key"] = JwtKey,
            ["Jwt:Issuer"] = JwtIssuer,
            ["Jwt:Audience"] = JwtAudience,
            ["Google:ClientId"] = "tests.apps.googleusercontent.com"
        }));

        builder.ConfigureLogging(logging => logging.AddProvider(Logs));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IGoogleTokenVerifier>();
            services.AddScoped<IGoogleTokenVerifier, FakeGoogleTokenVerifier>();
        });
    }

    /// <summary>Everything the application logged as an error: an unexpected 500 shows its cause here.</summary>
    public LogCollector Logs { get; } = new();
}

/// <summary>Keeps the error-level log entries of the application under test, so a failing test can show them.</summary>
public sealed class LogCollector : ILoggerProvider
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _errors = new();

    public IReadOnlyCollection<string> Errors => _errors.ToArray();

    public ILogger CreateLogger(string categoryName) => new CollectingLogger(categoryName, _errors);

    public void Dispose()
    {
    }

    private sealed class CollectingLogger : ILogger
    {
        private readonly string _category;
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _errors;

        public CollectingLogger(string category, System.Collections.Concurrent.ConcurrentQueue<string> errors)
        {
            _category = category;
            _errors = errors;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
                _errors.Enqueue($"[{_category}] {formatter(state, exception)}{(exception is null ? "" : Environment.NewLine + exception)}");
        }
    }
}

/// <summary>
/// Accepts tokens of the form <c>google:{subject}:{email}[:{name}]</c> and rejects everything else, like Google would
/// reject a forged token.
/// </summary>
public sealed class FakeGoogleTokenVerifier : IGoogleTokenVerifier
{
    public static string TokenFor(string subject, string email, string? name = null)
        => $"google:{subject}:{email}" + (name is null ? "" : $":{name}");

    public Task<ExternalIdentity> VerifyAsync(string idToken, CancellationToken cancellationToken = default)
    {
        var parts = idToken.Split(':', 4);

        if (parts.Length < 3 || parts[0] != "google")
            throw new AuthenticationFailedException("The Google token is not valid.");

        return Task.FromResult(new ExternalIdentity("Google", parts[1], parts[2], parts.Length == 4 ? parts[3] : null));
    }
}
