using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NCronJob.Tests;

public class MockHostApplicationLifetime : IHostApplicationLifetime, IDisposable
{
    private readonly CancellationTokenSource startedCts = new();
    private readonly CancellationTokenSource stoppingCts = new();
    private readonly CancellationTokenSource stoppedCts = new();
    private bool disposed;

    public CancellationToken ApplicationStarted => startedCts.Token;
    public CancellationToken ApplicationStopping => stoppingCts.Token;
    public CancellationToken ApplicationStopped => stoppedCts.Token;

    public void StopApplication()
    {
        if (!disposed)
        {
            stoppingCts.Cancel();
            stoppedCts.Cancel();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                startedCts.Dispose();
                stoppingCts.Dispose();
                stoppedCts.Dispose();
            }
            disposed = true;
        }
    }
}
