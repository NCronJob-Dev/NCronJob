using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NCronJob.Tests;

public abstract class JobIntegrationBase : IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private ServiceProvider? serviceProvider;

    protected CancellationToken CancellationToken => cancellationTokenSource.Token;
    protected Channel<object> CommunicationChannel { get; } = Channel.CreateUnbounded<object>();
    protected ServiceCollection ServiceCollection { get; }

    protected JobIntegrationBase()
    {
        ServiceCollection = new();
        ServiceCollection.AddLogging()
            .AddScoped<ChannelWriter<object>>(_ => CommunicationChannel.Writer);

        var mockLifetime = new MockHostApplicationLifetime();
        ServiceCollection.AddSingleton<IHostApplicationLifetime>(mockLifetime);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
        serviceProvider?.Dispose();
    }

    protected ServiceProvider CreateServiceProvider() => serviceProvider ??= ServiceCollection.BuildServiceProvider();

    protected async Task<bool> WaitForJobsOrTimeout(int jobRuns)
    {
        using var timeoutTcs = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            await Task.WhenAll(GetCompletionJobs(jobRuns, timeoutTcs.Token));
            return true;
        }
        catch (OperationCanceledException)
        {
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    protected async Task<bool> DoNotWaitJustCancel(int jobRuns)
    {
        using var timeoutTcs = new CancellationTokenSource(10);
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
