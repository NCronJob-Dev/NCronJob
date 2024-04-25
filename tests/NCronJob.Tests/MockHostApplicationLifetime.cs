using Microsoft.Extensions.Hosting;

namespace NCronJob.Tests;

public class MockHostApplicationLifetime : IHostApplicationLifetime
{
    public CancellationToken ApplicationStarted { get; set; } = new CancellationToken(false);
    public CancellationToken ApplicationStopping { get; set; } = new CancellationToken(false);
    public CancellationToken ApplicationStopped { get; set; } = new CancellationToken(false);

    public void StopApplication()
    {
    }
}
