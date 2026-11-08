namespace NCronJob;

/// <summary>
/// A re-armable signal. <see cref="WaitAsync"/> is thread-safe; callers synchronize <see cref="Pulse"/> and <see cref="Complete"/>.
/// </summary>
internal sealed class AsyncSignal
{
    private TaskCompletionSource current = Create();

    public Task WaitAsync() => Volatile.Read(ref current).Task;

    public void Pulse() => Interlocked.Exchange(ref current, Create()).TrySetResult();

    public void Complete() => Volatile.Read(ref current).TrySetResult();

    private static TaskCompletionSource Create() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
