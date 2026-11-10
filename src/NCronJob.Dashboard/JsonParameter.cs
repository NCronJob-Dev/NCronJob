using System.Text.Json;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace NCronJob.Dashboard;

internal sealed record ParameterText(string Full, string Preview);

internal static class JsonParameter
{
    internal const int PreviewLength = 80;

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    // Rejects JSON that doesn't follow the structure of the parameter type instead of silently dropping or defaulting members.
    private static readonly JsonSerializerOptions Strict = new()
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    public static object? Parse(string json, Type parameterType)
    {
        if (parameterType != typeof(object))
        {
            return JsonSerializer.Deserialize(json, parameterType, Strict);
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return root.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => root.GetString(),
            _ => root.GetRawText(),
        };
    }

    public static string? Schema(Type parameterType)
    {
        try
        {
            return JsonSchemaExporter.GetJsonSchemaAsNode(Strict, parameterType).ToJsonString(Indented);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // Only a hint: invalid JSON metadata surfaces as an error when the JSON is parsed, not while rendering.
            return null;
        }
    }

    public static ParameterText? Describe(object? parameter, Type? contractType = null)
    {
        if (parameter is null)
        {
            return null;
        }

        try
        {
            var type = contractType ?? parameter.GetType();
            return new ParameterText(
                JsonSerializer.Serialize(parameter, type, Indented),
                Truncate(JsonSerializer.Serialize(parameter, type)));
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // Best-effort display text: parameter getters and custom converters may throw anything.
            // ToString() is not safe here: a generated record ToString() recurses into the same cycle that broke serialization.
            var text = $"{parameter.GetType().Name} (not serializable to JSON)";
            return new ParameterText(text, Truncate(text));
        }
    }

    private static string Truncate(string text) =>
        text.Length <= PreviewLength ? text : string.Concat(text.AsSpan(0, PreviewLength - 1), "…");
}
