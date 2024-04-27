using Microsoft.Extensions.Time.Testing;
using Shouldly;

using TaskExtensions = LinkDotNet.NCronJob.TaskExtensions;

namespace NCronJob.Tests;

public class TaskExtensionsTests
{
    [Fact]
    public async Task LongDelaySafe_WhenCalledWith100Days_ShouldDelay()
    {
        // Arrange
        SynchronizationContext.SetSynchronizationContext(null); // Otherwise, the test will deadlock
        var maxDelayTimeSpan = TimeSpan.FromMilliseconds(uint.MaxValue - 1);
        var timeProvider = new FakeTimeProvider { AutoAdvanceAmount = maxDelayTimeSpan };
        var targetTimeSpan = TimeSpan.FromDays(100);
        var token = new CancellationToken();

        // Act
        var act = () =>
        {
            var delayTask = TaskExtensions.LongDelaySafe(targetTimeSpan, timeProvider, token);
            timeProvider.Advance(maxDelayTimeSpan);
            timeProvider.Advance(maxDelayTimeSpan);
            timeProvider.Advance(targetTimeSpan - (maxDelayTimeSpan + maxDelayTimeSpan));
            return delayTask;
        };

        // Assert
        // No exception should be thrown
        await act.ShouldNotThrowAsync();
    }

    [Fact]
    public void LongDelaySafeThrowsWhenTimeSpanZeroIsPassedIn()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var targetTimeSpan = TimeSpan.Zero;
        var token = new CancellationToken();

        // Act
        var act = () => TaskExtensions.LongDelaySafe(targetTimeSpan, timeProvider, token);

        // Assert
        act.ShouldThrow<ArgumentOutOfRangeException>();
    }
}
