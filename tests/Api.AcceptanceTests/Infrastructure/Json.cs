using System.Text.Json;

namespace Api.AcceptanceTests.Infrastructure;

/// <summary>Serializer options matching the API's wire format (ASP.NET Core web defaults).</summary>
public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
