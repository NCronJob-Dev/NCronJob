using System.Text.Json;

namespace NCronJob.Dashboard;

internal static class ParameterSerializer
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string Serialize(object? parameter)
    {
        if (parameter is null)
        {
            return "null";
        }

        try
        {
            return JsonSerializer.Serialize(parameter, parameter.GetType(), Options);
        }
        catch (NotSupportedException)
        {
            return parameter.ToString() ?? parameter.GetType().Name;
        }
        catch (JsonException)
        {
            return parameter.ToString() ?? parameter.GetType().Name;
        }
    }

    public static object? Deserialize(string json, object? currentValue)
    {
        ArgumentNullException.ThrowIfNull(json);
        var targetType = currentValue?.GetType() ?? typeof(JsonElement);
        return JsonSerializer.Deserialize(json, targetType);
    }
}
