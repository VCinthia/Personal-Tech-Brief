using System.Text.Json;
using System.Text.Json.Serialization;

namespace PersonalTechBrief.Infrastructure.Intelligence;

/// <summary>
/// Serialization options shared by the typed client and the cross-language contract tests. They
/// pin the wire format to the frozen Intelligence API contract: camelCase property names,
/// string enums (<c>high|medium|low</c>, <c>high|medium|low|none</c>), and strict casing so that
/// any drift in field names, casing, or enum values is observable rather than silently tolerated.
/// </summary>
public static class IntelligenceApiJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}
