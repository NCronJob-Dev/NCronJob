namespace NCronJob;

internal sealed class JobRunResources : IDisposable
{
    public CancellationTokenSource CombinedTokenSource { get; private set; }
    public CancellationToken PauseToken => PauseTokenSource.Token;
    private CancellationTokenSource PauseTokenSource { get; set; }

    public JobRunResources(CancellationToken jobCancellationToken, CancellationToken globalCancellationToken)
    {
        PauseTokenSource = new CancellationTokenSource();
        CombinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(jobCancellationToken, globalCancellationToken, PauseTokenSource.Token);
    }

    public void Pause() => PauseTokenSource.Cancel();
    public void Resume() => PauseTokenSource = new CancellationTokenSource();


    public void Dispose()
    {
        CombinedTokenSource.Dispose();
        PauseTokenSource.Dispose();
    }
}
