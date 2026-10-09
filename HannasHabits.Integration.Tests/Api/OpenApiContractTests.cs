using System.Net;
using System.Text.Json;
using HannasHabits.Integration.Tests.Support;

namespace HannasHabits.Integration.Tests.Api;

/// <summary>
/// The OpenAPI document is the frontend's source of types (<c>npm run api:types</c>), so it has to describe what the
/// API really does: every route has a typed success answer, the answers' properties are required, and real answers fit
/// the schemas that promise them.
/// </summary>
[Collection(IntegrationCollection.Name)]
public class OpenApiContractTests
{
    private readonly ApiFactory _api;

    public OpenApiContractTests(TestEnvironment environment)
    {
        _api = environment.Api;
    }

    [Fact]
    public async Task EveryRoute_HasATypedSuccessResponse()
    {
        var contract = await OpenApiContract.LoadAsync(_api);

        Assert.Empty(contract.OperationsWithoutTypedSuccess());
    }

    [Fact]
    public async Task EveryPropertyOfASuccessResponse_IsRequired()
    {
        var contract = await OpenApiContract.LoadAsync(_api);

        Assert.Empty(contract.OptionalPropertiesInSuccessResponses());
    }

    [Fact]
    public async Task EveryBadRequest_DescribesTheFieldErrors()
    {
        var contract = await OpenApiContract.LoadAsync(_api);

        Assert.Empty(contract.OperationsWhoseBadRequestHasNoFieldErrors());
    }

    [Fact]
    public async Task Nullability_FollowsTheCSharpTypes_EvenForAnEnumBehindAReference()
    {
        var document = await (await _api.CreateClient().GetAsync("/swagger/v1/swagger.json")).ReadJsonAsync();
        var schemas = document.GetProperty("components").GetProperty("schemas");

        bool IsNullable(string schema, string property)
            => schemas.GetProperty(schema).GetProperty("properties").GetProperty(property).TryGetProperty("nullable", out var nullable)
               && nullable.GetBoolean();

        foreach (var schema in new[] { "DailyDiaryDto", "DailyDiaryDayDto", "SaveDailyDiaryRequest" })
            Assert.True(IsNullable(schema, "mood"), $"{schema}.mood"); // Mood? - lost behind a $ref in OpenAPI 3.0 without allOf
        Assert.True(IsNullable("HabitDetailsDto", "description")); // string?
        Assert.False(IsNullable("HabitOverviewDto", "title")); // string
    }

    // ---- real answers against the schemas ----

    private static async Task AssertFitsAsync(OpenApiContract contract, string method, string pathTemplate, HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Empty(await contract.ProblemsOfAsync(method, pathTemplate, response));
    }

    [Fact]
    public async Task TheAuthAnswers_FitTheirSchema()
    {
        var contract = await OpenApiContract.LoadAsync(_api);
        var anonymous = TestUser.Anonymous(_api);
        var email = TestUser.NewEmail();

        var register = await anonymous.PostAsync("/api/auth/register", new { email, password = TestUser.Password, displayName = "Hanna" });
        await AssertFitsAsync(contract, "POST", "/api/auth/register", register, HttpStatusCode.OK);

        var login = await anonymous.PostAsync("/api/auth/login", new { email, password = TestUser.Password });
        await AssertFitsAsync(contract, "POST", "/api/auth/login", login, HttpStatusCode.OK);

        var refreshToken = (await login.ReadJsonAsync()).GetProperty("tokens").GetProperty("refreshToken").GetString();
        var refresh = await anonymous.PostAsync("/api/auth/refresh", new { refreshToken });
        await AssertFitsAsync(contract, "POST", "/api/auth/refresh", refresh, HttpStatusCode.OK);
    }

    [Fact]
    public async Task TheHabitAnswers_FitTheirSchema()
    {
        var contract = await OpenApiContract.LoadAsync(_api);
        var user = await TestUser.RegisterAsync(_api);

        // One habit with every optional value, one without (description = null in the answers).
        var create = await user.Client.PostAsync("/api/habits",
            new { title = "Read", description = "Twenty pages", schedule = new[] { 1, 3, 5 }, startDate = "2026-10-01" });
        await AssertFitsAsync(contract, "POST", "/api/habits", create, HttpStatusCode.Created);
        var habitId = (await create.ReadJsonAsync()).GetProperty("id").GetGuid();
        await user.CreateHabitAsync("Stretch");

        await AssertFitsAsync(contract, "GET", "/api/habits", await user.Client.GetAsync("/api/habits"), HttpStatusCode.OK);
        await AssertFitsAsync(contract, "GET", "/api/habits/{id}", await user.Client.GetAsync($"/api/habits/{habitId}"), HttpStatusCode.OK);

        var mark = await user.Client.PutAsync($"/api/habits/{habitId}/records/2026-10-05");
        await AssertFitsAsync(contract, "PUT", "/api/habits/{habitId}/records/{date}", mark, HttpStatusCode.OK);
        await AssertFitsAsync(contract, "GET", "/api/habits/{habitId}/records", await user.Client.GetAsync($"/api/habits/{habitId}/records"), HttpStatusCode.OK);

        var overview = await user.Client.GetAsync("/api/habits/overview?from=2026-10-01&to=2026-10-31&asOf=2026-10-08");
        await AssertFitsAsync(contract, "GET", "/api/habits/overview", overview, HttpStatusCode.OK);
        Assert.Equal(2, (await overview.ReadJsonAsync()).GetArrayLength()); // the check above was not run on an empty list
    }

    [Fact]
    public async Task TheDiaryAnswers_FitTheirSchema_AlsoForAnEntryWithoutMoodAndNumbers()
    {
        var contract = await OpenApiContract.LoadAsync(_api);
        var user = await TestUser.RegisterAsync(_api);

        // Only a highlight: mood, body and mind come back as null, the lists as empty arrays.
        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.PutAsync("/api/daily-diaries/2026-10-07", new { highlight = "A quiet day" })).StatusCode);
        var minimal = await user.Client.GetAsync("/api/daily-diaries/2026-10-07");
        await AssertFitsAsync(contract, "GET", "/api/daily-diaries/{date}", minimal, HttpStatusCode.OK);
        Assert.Equal(JsonValueKind.Null, (await minimal.ReadJsonAsync()).GetProperty("mood").ValueKind);

        var full = await user.Client.PutAsync("/api/daily-diaries/2026-10-08", new
        {
            mood = 5, body = 70, mind = 40, highlight = "Finished B11",
            grateful = new[] { "coffee" }, learned = new[] { "allOf" },
            tasks = new[] { new { title = "Write the test", done = true } }
        });
        Assert.Equal(HttpStatusCode.NoContent, full.StatusCode);
        await AssertFitsAsync(contract, "GET", "/api/daily-diaries/{date}", await user.Client.GetAsync("/api/daily-diaries/2026-10-08"), HttpStatusCode.OK);

        var days = await user.Client.GetAsync("/api/daily-diaries?from=2026-10-01&to=2026-10-31");
        await AssertFitsAsync(contract, "GET", "/api/daily-diaries", days, HttpStatusCode.OK);
        Assert.Equal(2, (await days.ReadJsonAsync()).GetArrayLength());
    }

    [Fact]
    public async Task TheResolutionAnswers_FitTheirSchema_WithAndWithoutAHabit()
    {
        var contract = await OpenApiContract.LoadAsync(_api);
        var user = await TestUser.RegisterAsync(_api);
        var habitId = await user.CreateHabitAsync("Run");

        var plain = await user.Client.PostAsync("/api/resolutions/2026/items", new { title = "Learn Spanish" });
        await AssertFitsAsync(contract, "POST", "/api/resolutions/{year}/items", plain, HttpStatusCode.Created);
        var linked = await user.Client.PostAsync("/api/resolutions/2026/items", new { title = "Run a 10k", habitId });
        await AssertFitsAsync(contract, "POST", "/api/resolutions/{year}/items", linked, HttpStatusCode.Created);

        var list = await user.Client.GetAsync("/api/resolutions/2026");
        await AssertFitsAsync(contract, "GET", "/api/resolutions/{year}", list, HttpStatusCode.OK);
        Assert.Equal(2, (await list.ReadJsonAsync()).GetArrayLength());
    }

    [Fact]
    public async Task TheErrorAnswers_FitTheirProblemDetailsSchemas_IncludingTheFieldErrorMap()
    {
        var contract = await OpenApiContract.LoadAsync(_api);
        var user = await TestUser.RegisterAsync(_api);

        var missing = await user.Client.GetAsync($"/api/habits/{Guid.NewGuid()}");
        await AssertFitsAsync(contract, "GET", "/api/habits/{id}", missing, HttpStatusCode.NotFound);

        // errors.<field> is what the forms map onto their fields: field name -> messages.
        var invalid = await user.Client.PostAsync("/api/habits", new { title = "", schedule = new[] { 1 } });
        await AssertFitsAsync(contract, "POST", "/api/habits", invalid, HttpStatusCode.BadRequest);
        Assert.Contains("title", await invalid.ErrorKeysAsync());

        var wrongPassword = await TestUser.Anonymous(_api).PostAsync("/api/auth/login", new { email = user.Email, password = "wrong" });
        await AssertFitsAsync(contract, "POST", "/api/auth/login", wrongPassword, HttpStatusCode.Unauthorized);
    }
}

/// <summary>
/// The rules of <see cref="OpenApiContract"/> must be able to fail, or the tests above prove nothing: each case feeds
/// them a small document or answer that breaks exactly one rule.
/// </summary>
public class OpenApiContractRuleTests
{
    private static OpenApiContract Contract(string json) => new(JsonDocument.Parse(json).RootElement.Clone());

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private const string Pet = """
        { "type": "object", "additionalProperties": false, "required": ["id", "name", "mood"],
          "properties": {
            "id": { "type": "string", "format": "uuid" },
            "name": { "type": "string" },
            "mood": { "allOf": [{ "$ref": "#/components/schemas/Mood" }], "nullable": true } } }
        """;

    private static OpenApiContract PetContract() => Contract($$"""
        { "paths": {}, "components": { "schemas": { "Mood": { "enum": [1, 2, 3], "type": "integer" }, "Pet": {{Pet}} } } }
        """);

    private static JsonElement PetSchema() => Json("""{ "$ref": "#/components/schemas/Pet" }""");

    [Fact]
    public void AnOperationWithoutATypedSuccessAnswer_IsReported()
    {
        var contract = Contract("""
            { "paths": {
                "/untyped": { "get": { "responses": { "401": { "description": "no" } } } },
                "/no-body": { "get": { "responses": { "200": { "description": "OK" }, "404": { "description": "no" } } } },
                "/typed":   { "get": { "responses": { "200": { "content": { "application/json": { "schema": { "type": "string" } } } } } } },
                "/gone":    { "delete": { "responses": { "204": { "description": "No Content" } } } },
                "/shared":  { "parameters": [], "put": { "responses": { "201": { "content": { "application/json": { "schema": { "type": "string" } } } } } } } },
              "components": { "schemas": {} } }
            """);

        Assert.Equal(["GET /untyped", "GET /no-body"], contract.OperationsWithoutTypedSuccess());
    }

    [Fact]
    public void ABadRequestWithoutFieldErrors_IsReported()
    {
        var contract = Contract("""
            { "paths": {
                "/plain": { "post": { "responses": { "400": { "content": { "application/json": { "schema": { "$ref": "#/components/schemas/ProblemDetails" } } } } } } },
                "/fields": { "post": { "responses": { "400": { "content": { "application/json": { "schema": { "$ref": "#/components/schemas/ValidationProblemDetails" } } } } } } },
                "/none": { "get": { "responses": { "200": { "description": "OK" } } } } },
              "components": { "schemas": {} } }
            """);

        Assert.Equal(["POST /plain"], contract.OperationsWhoseBadRequestHasNoFieldErrors());
    }

    [Fact]
    public void AnOptionalPropertyOfAResponseSchema_IsReported_AlsoWhenNested_ButNotOneOfARequest()
    {
        var contract = Contract("""
            { "paths": {
                "/pets": { "get": { "responses": { "200": { "content": { "application/json": { "schema":
                    { "type": "array", "items": { "$ref": "#/components/schemas/Owner" } } } } } } },
                           "post": { "requestBody": { "content": { "application/json": { "schema": { "$ref": "#/components/schemas/Request" } } } },
                                     "responses": { "204": { "description": "No Content" } } } } },
              "components": { "schemas": {
                "Owner": { "type": "object", "required": ["name"], "properties": { "name": { "type": "string" }, "pet": { "$ref": "#/components/schemas/Pet" } } },
                "Pet": { "type": "object", "required": ["id"], "properties": { "id": { "type": "string" }, "nickname": { "type": "string" } } },
                "Request": { "type": "object", "properties": { "anything": { "type": "string" } } } } } }
            """);

        Assert.Equal(["Owner.pet", "Pet.nickname"], contract.OptionalPropertiesInSuccessResponses());
    }

    [Fact]
    public void TheValuesOfAMap_AreCheckedAgainstTheirSchema()
    {
        var contract = PetContract();
        var map = Json("""{ "type": "object", "additionalProperties": { "type": "array", "items": { "type": "string" } } }""");

        Assert.Empty(contract.ProblemsOf(map, Json("""{ "title": ["is required"], "year": [] }""")));
        Assert.Contains(contract.ProblemsOf(map, Json("""{ "title": "is required" }""")), problem => problem.StartsWith("$.title should be an array"));
    }

    [Fact]
    public void AnAnswerThatFitsTheSchema_HasNoProblems()
    {
        var contract = PetContract();

        Assert.Empty(contract.ProblemsOf(PetSchema(), Json("""{ "id": "6c6bba06-3f1c-4a8e-9d2a-0a8f3d6c1b11", "name": "Rex", "mood": 2 }""")));
        Assert.Empty(contract.ProblemsOf(PetSchema(), Json("""{ "id": "6c6bba06-3f1c-4a8e-9d2a-0a8f3d6c1b11", "name": "Rex", "mood": null }""")));
    }

    [Theory]
    [InlineData("""{ "id": "6c6bba06-3f1c-4a8e-9d2a-0a8f3d6c1b11", "mood": 1 }""", "$.name is required but missing")]
    [InlineData("""{ "id": "6c6bba06-3f1c-4a8e-9d2a-0a8f3d6c1b11", "name": null, "mood": 1 }""", "$.name is null")]
    [InlineData("""{ "id": "6c6bba06-3f1c-4a8e-9d2a-0a8f3d6c1b11", "name": "Rex", "mood": 1, "age": 3 }""", "$.age is not in the schema")]
    [InlineData("""{ "id": "6c6bba06-3f1c-4a8e-9d2a-0a8f3d6c1b11", "name": 7, "mood": 1 }""", "$.name should be a string")]
    [InlineData("""{ "id": "not-a-guid", "name": "Rex", "mood": 1 }""", "$.id should be a string (uuid)")]
    [InlineData("""{ "id": "6c6bba06-3f1c-4a8e-9d2a-0a8f3d6c1b11", "name": "Rex", "mood": 9 }""", "$.mood is 9, which is not one of")]
    public void AnAnswerThatBreaksTheSchema_IsReported(string answer, string expectedProblem)
    {
        var problems = PetContract().ProblemsOf(PetSchema(), Json(answer));

        Assert.Contains(problems, problem => problem.StartsWith(expectedProblem, StringComparison.Ordinal));
    }
}
