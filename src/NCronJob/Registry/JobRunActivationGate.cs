namespace NCronJob;

internal sealed class JobRunActivationGate
{
    private readonly TaskCompletionSource<bool> completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<bool> WaitAsync() => completion.Task;

    public void Activate() => completion.TrySetResult(true);

    public void Reject() => completion.TrySetResult(false);
}
