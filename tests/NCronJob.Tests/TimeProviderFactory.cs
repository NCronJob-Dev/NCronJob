using TimeProviderExtensions;

namespace NCronJob.Tests;

internal static class TimeProviderFactory
{
    public static ManualTimeProvider GetTimeProvider()
    {
        return new ManualTimeProvider();
    }
}
