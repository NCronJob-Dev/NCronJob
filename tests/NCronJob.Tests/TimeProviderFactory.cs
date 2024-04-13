using TimeProviderExtensions;

namespace NCronJob.Tests;

internal static class TimeProviderFactory
{
    public static ManualTimeProvider GetTimeProvider(TimeSpan? advanceTime = null)
        => new()
        {
            AutoAdvanceBehavior = new AutoAdvanceBehavior
            {
                UtcNowAdvanceAmount = advanceTime ?? TimeSpan.Zero,
                TimerAutoTriggerCount = 1,
            },
        };
}
