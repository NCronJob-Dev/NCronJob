namespace NCronJob;

internal readonly record struct OptionalParameter(bool IsSpecified, object? Value)
{
    public static OptionalParameter Unspecified => default;

    public static OptionalParameter FromValue(object? value) => new(true, value);
}
