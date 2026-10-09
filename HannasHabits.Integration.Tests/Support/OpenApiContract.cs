using System.Globalization;
using System.Text.Json;

namespace HannasHabits.Integration.Tests.Support;

/// <summary>
/// The generated OpenAPI document read the way a client generator reads it. The frontend derives its types from it,
/// so these are the rules it has to keep, and a check of a real answer against the schema that promises it.
/// </summary>
public sealed class OpenApiContract
{
    private static readonly string[] Methods = ["get", "put", "post", "delete", "patch"];

    private readonly JsonElement _document;

    public OpenApiContract(JsonElement document)
    {
        _document = document;
    }

    public static async Task<OpenApiContract> LoadAsync(ApiFactory api)
    {
        var response = await api.CreateClient().GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        return new OpenApiContract(await response.ReadJsonAsync());
    }

    // ---- rules over the whole document ----

    /// <summary>
    /// Operations that promise no usable success answer: neither a 204 nor a 2xx with a JSON schema. A generated
    /// client cannot type their result (for example because an explicit error <c>ProducesResponseType</c> switched
    /// off ASP.NET's inference of the 200 from <c>ActionResult&lt;T&gt;</c>).
    /// </summary>
    public IReadOnlyList<string> OperationsWithoutTypedSuccess() => Operations()
        .Where(operation => !SuccessResponses(operation.Value).Any(response => response.Name == "204" || JsonSchemaOf(response.Value) is not null))
        .Select(operation => operation.Key)
        .ToList();

    /// <summary>
    /// Properties of the schemas a success answer uses (directly or nested) that are not <c>required</c>. The server
    /// always writes all of them, so a generated client would wrongly make them optional.
    /// </summary>
    public IReadOnlyList<string> OptionalPropertiesInSuccessResponses()
    {
        var schemaNames = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var operation in Operations())
        foreach (var response in SuccessResponses(operation.Value))
            if (JsonSchemaOf(response.Value) is { } schema)
                CollectSchemaNames(schema, schemaNames);

        var optional = new List<string>();
        foreach (var name in schemaNames)
        {
            var schema = Component(name);
            if (!schema.TryGetProperty("properties", out var properties))
                continue;

            var required = schema.TryGetProperty("required", out var list)
                ? list.EnumerateArray().Select(item => item.GetString()).ToHashSet()
                : [];
            optional.AddRange(properties.EnumerateObject().Where(p => !required.Contains(p.Name)).Select(p => $"{name}.{p.Name}"));
        }

        return optional;
    }

    /// <summary>
    /// Operations whose 400 is not described as <c>ValidationProblemDetails</c>. The server answers a rejected request
    /// with an <c>errors</c> map (field -> messages) and the forms map it onto their fields; under plain
    /// <c>ProblemDetails</c> (an open schema) that map would be untyped.
    /// </summary>
    public IReadOnlyList<string> OperationsWhoseBadRequestHasNoFieldErrors() => Operations()
        .Where(operation => operation.Value.GetProperty("responses").TryGetProperty("400", out var response)
                            && !(JsonSchemaOf(response) is { } schema
                                 && schema.TryGetProperty("$ref", out var reference)
                                 && reference.GetString()!.EndsWith("/ValidationProblemDetails", StringComparison.Ordinal)))
        .Select(operation => operation.Key)
        .ToList();

    // ---- a real answer against the document ----

    /// <summary>
    /// Compares the answer of a real call with what the document promises for that route and status: the status is
    /// documented, and the body has exactly the properties, types and nullability of the schema.
    /// </summary>
    public async Task<IReadOnlyList<string>> ProblemsOfAsync(string method, string pathTemplate, HttpResponseMessage response)
    {
        var status = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
        if (!_document.GetProperty("paths").TryGetProperty(pathTemplate, out var path)
            || !path.TryGetProperty(method.ToLowerInvariant(), out var operation))
            return [$"{method} {pathTemplate} is not in the document"];

        if (!operation.GetProperty("responses").TryGetProperty(status, out var documented))
            return [$"{method} {pathTemplate} answered {status}, which the document does not list"];

        var schema = JsonSchemaOf(documented);
        if (schema is null)
            return [];

        // As a string, not through ReadJsonAsync: that closes the content stream and the test reads the body again.
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return ProblemsOf(schema.Value, body.RootElement);
    }

    public IReadOnlyList<string> ProblemsOf(JsonElement schema, JsonElement value)
    {
        var problems = new List<string>();
        Check(schema, value, "$", problems);
        return problems;
    }

    private void Check(JsonElement schema, JsonElement value, string at, List<string> problems)
    {
        // `nullable` sits next to `allOf`/`type`; once a `$ref` is followed it is gone, so ask before.
        if (value.ValueKind == JsonValueKind.Null)
        {
            if (!(schema.TryGetProperty("nullable", out var nullable) && nullable.GetBoolean()))
                problems.Add($"{at} is null, but the schema does not allow it");
            return;
        }

        if (schema.TryGetProperty("$ref", out var reference))
        {
            Check(Component(reference.GetString()!.Split('/')[^1]), value, at, problems);
            return;
        }

        if (schema.TryGetProperty("allOf", out var parts))
            foreach (var part in parts.EnumerateArray())
                Check(part, value, at, problems);

        if (schema.TryGetProperty("enum", out var allowed)
            && !allowed.EnumerateArray().Any(item => item.GetRawText() == value.GetRawText()))
            problems.Add($"{at} is {value.GetRawText()}, which is not one of {allowed.GetRawText()}");

        switch (schema.TryGetProperty("type", out var type) ? type.GetString() : null)
        {
            case "object":
                CheckObject(schema, value, at, problems);
                break;
            case "array":
                if (value.ValueKind != JsonValueKind.Array)
                    problems.Add($"{at} should be an array");
                else if (schema.TryGetProperty("items", out var items))
                    foreach (var (item, index) in value.EnumerateArray().Select((item, index) => (item, index)))
                        Check(items, item, $"{at}[{index}]", problems);
                break;
            case "string":
                if (value.ValueKind != JsonValueKind.String || !FitsFormat(schema, value.GetString()!))
                    problems.Add($"{at} should be a string{FormatSuffix(schema)}, was {value.GetRawText()}");
                break;
            case "integer":
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out _))
                    problems.Add($"{at} should be an integer, was {value.GetRawText()}");
                break;
            case "boolean":
                if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    problems.Add($"{at} should be a boolean, was {value.GetRawText()}");
                break;
        }
    }

    private void CheckObject(JsonElement schema, JsonElement value, string at, List<string> problems)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            problems.Add($"{at} should be an object");
            return;
        }

        var properties = schema.TryGetProperty("properties", out var declared) ? declared : default;
        var hasAdditional = schema.TryGetProperty("additionalProperties", out var additional);
        var closed = hasAdditional && additional.ValueKind == JsonValueKind.False;

        foreach (var property in value.EnumerateObject())
        {
            if (properties.ValueKind == JsonValueKind.Object && properties.TryGetProperty(property.Name, out var propertySchema))
                Check(propertySchema, property.Value, $"{at}.{property.Name}", problems);
            else if (closed)
                problems.Add($"{at}.{property.Name} is not in the schema");
            else if (hasAdditional && additional.ValueKind == JsonValueKind.Object) // a map: every value has the same schema
                Check(additional, property.Value, $"{at}.{property.Name}", problems);
        }

        if (schema.TryGetProperty("required", out var required))
            foreach (var name in required.EnumerateArray().Select(item => item.GetString()!))
                if (!value.TryGetProperty(name, out _))
                    problems.Add($"{at}.{name} is required but missing");
    }

    private static bool FitsFormat(JsonElement schema, string text) => schema.TryGetProperty("format", out var format)
        ? format.GetString() switch
        {
            "uuid" => Guid.TryParse(text, out _),
            "date" => DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            "date-time" => DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            _ => true
        }
        : true;

    private static string FormatSuffix(JsonElement schema)
        => schema.TryGetProperty("format", out var format) ? $" ({format.GetString()})" : "";

    // ---- reading the document ----

    private IEnumerable<KeyValuePair<string, JsonElement>> Operations() => _document.GetProperty("paths").EnumerateObject()
        .SelectMany(path => path.Value.EnumerateObject()
            .Where(member => Methods.Contains(member.Name))
            .Select(member => KeyValuePair.Create($"{member.Name.ToUpperInvariant()} {path.Name}", member.Value)));

    private static IEnumerable<JsonProperty> SuccessResponses(JsonElement operation)
        => operation.TryGetProperty("responses", out var responses)
            ? responses.EnumerateObject().Where(response => response.Name.StartsWith('2'))
            : [];

    private static JsonElement? JsonSchemaOf(JsonElement response)
        => response.TryGetProperty("content", out var content)
           && content.TryGetProperty("application/json", out var json)
           && json.TryGetProperty("schema", out var schema)
            ? schema
            : null;

    private JsonElement Component(string name) => _document.GetProperty("components").GetProperty("schemas").GetProperty(name);

    private void CollectSchemaNames(JsonElement schema, ISet<string> names)
    {
        if (schema.TryGetProperty("$ref", out var reference))
        {
            var name = reference.GetString()!.Split('/')[^1];
            if (names.Add(name))
                CollectSchemaNames(Component(name), names);
            return;
        }

        if (schema.TryGetProperty("allOf", out var parts))
            foreach (var part in parts.EnumerateArray())
                CollectSchemaNames(part, names);

        if (schema.TryGetProperty("items", out var items))
            CollectSchemaNames(items, names);

        if (schema.TryGetProperty("properties", out var properties))
            foreach (var property in properties.EnumerateObject())
                CollectSchemaNames(property.Value, names);
    }
}
