using Microsoft.Extensions.Time.Testing;

namespace NCronJob.Tests;

public sealed class TimerAwareFakeTimeProvider : FakeTimeProvider
{
    private int firedTimerCount;

    public int FiredTimerCount => Volatile.Read(ref firedTimerCount);

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        base.CreateTimer(
            timerState =>
            {
                Interlocked.Increment(ref firedTimerCount);
                callback(timerState);
            },
            state,
            dueTime,
            period);
}
