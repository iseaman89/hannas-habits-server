using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace HannasHabits.Integration.Tests.Support;

/// <summary>JSON the way the API speaks it (camelCase, enums as numbers, dates as yyyy-MM-dd).</summary>
public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<T>(Options))!;

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
    {
        using var stream = await response.Content.ReadAsStreamAsync();
        return (await JsonDocument.ParseAsync(stream)).RootElement.Clone();
    }

    /// <summary>The names of the fields a validation problem (400) complains about, e.g. <c>title</c>, <c>grateful[1]</c>.</summary>
    public static async Task<string[]> ErrorKeysAsync(this HttpResponseMessage response)
    {
        var body = await response.ReadJsonAsync();
        return body.TryGetProperty("errors", out var errors)
            ? errors.EnumerateObject().Select(property => property.Name).ToArray()
            : [];
    }
}

/// <summary>An <see cref="HttpClient"/> with the JSON helpers the tests keep needing.</summary>
public sealed class ApiClient
{
    public ApiClient(HttpClient http)
    {
        Http = http;
    }

    public HttpClient Http { get; }

    public ApiClient WithBearer(string accessToken)
    {
        Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return this;
    }

    public Task<HttpResponseMessage> GetAsync(string url) => Http.GetAsync(url);

    public Task<HttpResponseMessage> PostAsync(string url, object? body = null)
        => Http.PostAsJsonAsync(url, body, Json.Options);

    public Task<HttpResponseMessage> PutAsync(string url, object? body = null)
        => Http.PutAsJsonAsync(url, body, Json.Options);

    public Task<HttpResponseMessage> DeleteAsync(string url) => Http.DeleteAsync(url);

    /// <summary>Sends a body that is not built from an object (broken JSON, wrong types, no body at all).</summary>
    public Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string url, string? json)
    {
        var request = new HttpRequestMessage(method, url);
        if (json is not null)
            request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        return Http.SendAsync(request);
    }
}
