using TimeProviderExtensions;

namespace NCronJob.Tests;

internal static class TimeProviderFactory
{
    public static ManualTimeProvider GetAutoTickingTimeProvider(TimeSpan? advanceAmount = null)
    {
        var autoAdvanceBehavior = new AutoAdvanceBehavior
        {
            TimestampAdvanceAmount = advanceAmount ?? TimeSpan.FromSeconds(1),
        };
        var fakeTimer = new ManualTimeProvider { AutoAdvanceBehavior = autoAdvanceBehavior };
        return fakeTimer;
    }
}
