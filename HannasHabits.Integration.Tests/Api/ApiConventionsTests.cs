using System.Net;
using HannasHabits.Integration.Tests.Support;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace HannasHabits.Integration.Tests.Api;

[Collection(IntegrationCollection.Name)]
public class ApiConventionsTests
{
    private readonly ApiFactory _api;

    public ApiConventionsTests(TestEnvironment environment)
    {
        _api = environment.Api;
    }

    [Fact]
    public async Task Swagger_DescribesTheApi()
    {
        var response = await _api.CreateClient().GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // would be a 500 if two actions claimed the same route
        var document = await response.ReadJsonAsync();
        var paths = document.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Contains("/api/auth/login", paths);
        Assert.Contains("/api/habits/overview", paths);
        Assert.Contains("/api/habits/{habitId}/records/{date}", paths);
        Assert.Contains("/api/daily-diaries/{date}", paths);
        Assert.Contains("/api/resolutions/{year}/items/{id}", paths);
        Assert.True(document.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _));
    }

    [Fact]
    public async Task AnUnknownRoute_Is404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _api.CreateClient().GetAsync("/api/nothing-here")).StatusCode);
    }

    [Fact]
    public async Task AWrongMethod_Is405()
    {
        var user = await TestUser.RegisterAsync(_api);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await user.Client.DeleteAsync("/api/habits")).StatusCode);
    }

    // ---- CORS ----

    private static HttpRequestMessage Preflight(string origin, string method = "PUT")
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/habits/overview");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        return request;
    }

    [Fact]
    public async Task CorsPreflight_FromTheConfiguredFrontend_IsAnsweredWithoutAToken()
    {
        var response = await _api.CreateClient().SendAsync(Preflight(ApiFactory.AllowedOrigin));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(ApiFactory.AllowedOrigin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains("PUT", string.Join(",", response.Headers.GetValues("Access-Control-Allow-Methods")));
    }

    [Fact]
    public async Task CorsPreflight_FromAnyOtherOrigin_GetsNoPermission()
    {
        var response = await _api.CreateClient().SendAsync(Preflight("https://evil.example.com"));

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task AnActualRequest_FromTheFrontend_CarriesTheAllowOriginHeader()
    {
        var user = await TestUser.RegisterAsync(_api);
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/habits");
        request.Headers.Add("Origin", ApiFactory.AllowedOrigin);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", user.AccessToken);

        var response = await _api.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ApiFactory.AllowedOrigin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    // ---- the composition ----

    [Fact]
    public void EveryHandlerCanBeCreatedByTheRealContainer()
    {
        // The Application layer only declares what it needs (interfaces); this fails if Infrastructure or the host
        // forgets to register one of them.
        using var scope = _api.Services.CreateScope();
        var requestTypes = typeof(HannasHabits.Application.DependencyInjection).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false } && type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>)))
            .ToList();
        Assert.NotEmpty(requestTypes);

        foreach (var request in requestTypes)
        {
            var response = request.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>)).GetGenericArguments()[0];
            var handlerType = typeof(IRequestHandler<,>).MakeGenericType(request, response);

            Assert.NotNull(scope.ServiceProvider.GetRequiredService(handlerType));
        }
    }

    [Fact]
    public void TheApplicationStartsWithTheConfigurationItNeeds_AndNothingElse()
    {
        // Options are validated on start; reaching this line means Jwt and Google are bound and valid.
        Assert.NotNull(_api.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<HannasHabits.Infrastructure.Identity.JwtOptions>>().Value);
        Assert.NotNull(_api.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<HannasHabits.Infrastructure.Identity.GoogleOptions>>().Value);
    }

    [Fact]
    public async Task ExpectedClientErrors_AreNotLoggedAsServerErrors()
    {
        // Only an unhandled exception is an error in the log; "not found" and "invalid" are the normal answer.
        var user = await TestUser.RegisterAsync(_api);
        var before = _api.Logs.Errors.Count;

        await user.Client.GetAsync($"/api/habits/{Guid.NewGuid()}");     // 404
        await user.Client.PostAsync("/api/habits", new { title = "" });  // 400

        Assert.Equal(before, _api.Logs.Errors.Count);
    }
}
