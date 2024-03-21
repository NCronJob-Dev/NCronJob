using System.Threading.Channels;

namespace NCronJob.Tests;

public abstract class JobIntegrationBase : IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();

    protected CancellationToken CancellationToken => cancellationTokenSource.Token;
    protected Channel<object> CommunicationChannel { get; } = Channel.CreateUnbounded<object>();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
    }

    protected async Task<bool> WaitForJobsOrTimeout(int jobRuns)
    {
        using var timeoutTcs = new CancellationTokenSource(100);
        try
        {
            await Task.WhenAll(GetCompletionJobs(jobRuns, timeoutTcs.Token));
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    protected IEnumerable<Task> GetCompletionJobs(int expectedJobCount, CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < expectedJobCount; i++)
        {
            yield return CommunicationChannel.Reader.ReadAsync(cancellationToken).AsTask();
        }
    }
}
