using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestPlatform.TestHost;

namespace UserService.Tests;

public class AuthIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AuthIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_ValidUser_ReturnsAccepted()
    {
        var user = new { Email = "test@test.com", Password = "Qwerty123!" };

        var response = await _client.PostAsJsonAsync("/api/auth/register", user);

        Assert.Equal(System.Net.HttpStatusCode.Accepted, response.StatusCode);
    }
}