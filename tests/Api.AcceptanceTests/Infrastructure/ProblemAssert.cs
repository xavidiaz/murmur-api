using System.Text.Json;

namespace Api.AcceptanceTests.Infrastructure;

/// <summary>Assertions for RFC 9457 <c>application/problem+json</c> responses.</summary>
public static class ProblemAssert
{
    /// <summary>
    /// Asserts status, problem+json content type, the body's <c>status</c>, and (when given) the <c>code</c> extension.
    /// Returns the parsed body for further checks.
    /// </summary>
    public static async Task<JsonElement> AssertProblemAsync(this HttpResponseMessage response, HttpStatusCode status, string? code = null)
    {
        await ApiClient.AssertStatusAsync(response, status);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json.Options, TestContext.Current.CancellationToken);
        Assert.Equal(JsonValueKind.Object, body.ValueKind);
        Assert.True(body.TryGetProperty("status", out var s), "problem body has no 'status'");
        Assert.Equal((int)status, s.GetInt32());

        if (code is not null)
        {
            Assert.True(body.TryGetProperty("code", out var c), $"problem body has no 'code' (expected '{code}'): {body}");
            Assert.Equal(code, c.GetString());
        }
        return body;
    }

    /// <summary>
    /// Asserts a 400 <c>ValidationProblemDetails</c> whose <c>errors</c> map has a non-empty entry for each field
    /// (camelCase, e.g. <c>"userName"</c>). Returns the parsed body.
    /// </summary>
    public static async Task<JsonElement> AssertValidationProblemAsync(this HttpResponseMessage response, params string[] fields)
    {
        var body = await response.AssertProblemAsync(HttpStatusCode.BadRequest);
        Assert.True(body.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object,
            $"validation problem has no 'errors' object: {body}");
        foreach (var field in fields)
        {
            Assert.True(errors.TryGetProperty(field, out var messages), $"errors has no entry '{field}': {errors}");
            Assert.True(messages.ValueKind == JsonValueKind.Array && messages.GetArrayLength() > 0,
                $"errors.{field} is empty: {errors}");
        }
        return body;
    }

    /// <summary>Problem members that legitimately differ per request; ignored by <see cref="AssertSameProblem"/>.</summary>
    public static readonly IReadOnlyList<string> PerRequestFields = ["traceId", "instance"];

    /// <summary>
    /// Asserts two problem bodies are indistinguishable once per-request members (<see cref="PerRequestFields"/>)
    /// are removed — e.g. so a response can't reveal whether a resource exists.
    /// </summary>
    public static void AssertSameProblem(JsonElement expected, JsonElement actual)
    {
        var a = WithoutPerRequestFields(expected);
        var b = WithoutPerRequestFields(actual);
        Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(a, b),
            $"problem bodies differ (ignoring {string.Join(", ", PerRequestFields)}):\n first:  {a}\n second: {b}");
    }

    private static System.Text.Json.Nodes.JsonObject WithoutPerRequestFields(JsonElement problem)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(problem.GetRawText())!.AsObject();
        foreach (var field in PerRequestFields)
            node.Remove(field);
        return node;
    }

    /// <summary>The problem's <c>code</c> extension, or null.</summary>
    public static string? Code(this JsonElement problem) =>
        problem.TryGetProperty("code", out var c) ? c.GetString() : null;
}
