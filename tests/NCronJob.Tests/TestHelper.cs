using System.Runtime.CompilerServices;
using System.Threading.Channels;
using LinkDotNet.NCronJob;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace NCronJob.Tests;

public abstract class JobIntegrationBase : IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private ServiceProvider? serviceProvider;

    private readonly TaskCompletionSource<bool> cancellationSignaled = new();
    protected Task CancellationSignaled => cancellationSignaled.Task;
    protected CancellationToken CancellationToken => cancellationTokenSource.Token;
    protected Channel<object> CommunicationChannel { get; } = Channel.CreateUnbounded<object>();
    protected ServiceCollection ServiceCollection { get; }

    protected JobIntegrationBase()
    {
        ServiceCollection = new();
        ServiceCollection.AddLogging();
        ServiceCollection.AddScoped<ChannelWriter<object>>(_ => CommunicationChannel.Writer);
        ServiceCollection.AddSingleton<IHostApplicationLifetime, MockHostApplicationLifetime>();
        ServiceCollection.AddSingleton<IRetryHandler, TestRetryHandler>();
        ServiceCollection.AddSingleton<IRetryHandler>(sp =>
            new TestRetryHandler(sp, sp.GetRequiredService<ChannelWriter<object>>(), cancellationSignaled));
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
        cancellationSignaled.TrySetCanceled();
        serviceProvider?.Dispose();
    }

    protected ServiceProvider CreateServiceProvider() => serviceProvider ??= ServiceCollection.BuildServiceProvider();

    protected async Task<bool> WaitForJobsOrTimeout(int jobRuns, TimeSpan? timeOut = null)
    {
        using var timeoutTcs = new CancellationTokenSource(timeOut ?? TimeSpan.FromSeconds(5));
        try
        {
            await Task.WhenAll(GetCompletionJobs(jobRuns, timeoutTcs.Token));
            return true;
        }
        catch
        {
            return false;
        }
    }

    protected async Task<bool> WaitForJobsOrTimeout(int jobRuns, Action timeAdvancer)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await foreach (var jobSuccessful in GetCompletionJobsAsync(jobRuns, timeAdvancer, timeoutCts.Token))
            {
                jobSuccessful.ShouldBe("Job Completed");
            }
            return true;
        }
        catch
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

    private async IAsyncEnumerable<object> GetCompletionJobsAsync(int expectedJobCount, Action timeAdvancer, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < expectedJobCount; i++)
        {
            timeAdvancer();
            var jobResult = await CommunicationChannel.Reader.ReadAsync(cancellationToken);
            yield return jobResult;
        }
    }
}
