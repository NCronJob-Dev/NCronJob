using System.Text.Json;

namespace NCronJob.Dashboard;

internal sealed record ParameterText(string Full, string Preview);

internal static class JsonParameter
{
    internal const int PreviewLength = 80;

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static object? Parse(string json, Type? targetType)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (targetType is not null)
        {
            return root.Deserialize(targetType);
        }

        return root.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => root.GetString(),
            _ => root.GetRawText(),
        };
    }

    public static ParameterText? Describe(object? parameter)
    {
        if (parameter is null)
        {
            return null;
        }

        try
        {
            var type = parameter.GetType();
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
