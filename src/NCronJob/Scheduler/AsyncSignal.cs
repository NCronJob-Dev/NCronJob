namespace NCronJob;

/// <summary>
/// A re-armable signal. Not thread-safe; callers synchronize access.
/// </summary>
internal sealed class AsyncSignal
{
    private TaskCompletionSource current = Create();

    public Task WaitAsync() => current.Task;

    public void Pulse()
    {
        var previous = current;
        current = Create();
        previous.TrySetResult();
    }

    public void Complete() => current.TrySetResult();

    private static TaskCompletionSource Create() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
