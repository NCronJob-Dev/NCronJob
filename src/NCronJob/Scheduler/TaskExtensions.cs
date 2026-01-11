namespace NCronJob;

/// <summary>
/// Represents extensions for the <see cref="Task"/> object.
/// </summary>
internal static class TaskExtensions
{
    extension(Task)
    {
        /// <summary>
        /// Has the same semantics as <see cref="Task.Delay(TimeSpan, TimeProvider, CancellationToken)"/> but is safe to use for long delays.
        /// </summary>
        /// <param name="targetTimeSpan">The <see cref="TimeSpan" /> to wait before completing the returned task, or <see cref="Timeout.InfiniteTimeSpan" /> to wait indefinitely.</param>
        /// <param name="timeProvider">The <see cref="TimeProvider" /> with which to interpret <paramref name="targetTimeSpan" />.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        public static async Task LongDelaySafe(
            TimeSpan targetTimeSpan,
            TimeProvider timeProvider,
            CancellationToken token)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(targetTimeSpan, TimeSpan.Zero);

            var remainingTime = targetTimeSpan;
            while (remainingTime > TimeSpan.Zero)
            {
                var delay = TimeSpan.FromMilliseconds(Math.Min(remainingTime.TotalMilliseconds, uint.MaxValue - 1));
                await Task.Delay(delay, timeProvider, token).ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
                remainingTime -= delay;
            }
        }
    }
}
