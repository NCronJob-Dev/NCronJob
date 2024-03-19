using LinkDotNet.NCronJob;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using TimeProviderExtensions;

namespace NCronJob.Tests;

public sealed class NCronJobIntegrationTests : IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();

    [Fact]
    public async Task CronJobThatIsScheduledEveryMinuteShouldBeExecuted()
    {
        var fakeTimer = GetOneSecondTickManualTimeProvider();
        var channel = new CommunicationChannel();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<TimeProvider>(fakeTimer);
        serviceCollection.AddNCronJob();
        serviceCollection.AddCronJob<SampleJob>(p => p.CronExpression = "* * * * *");
        serviceCollection.AddScoped<CommunicationChannel>(_ => channel);
        var provider = serviceCollection.BuildServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(cancellationTokenSource.Token);

        fakeTimer.Advance(TimeSpan.FromMinutes(2));
        var delay = Task.Delay(100, cancellationTokenSource.Token);
        var finishTask = await Task.WhenAny(channel.TaskCompletionSource.Task, delay);
        finishTask.ShouldBe(channel.TaskCompletionSource.Task);
    }

    private static ManualTimeProvider GetOneSecondTickManualTimeProvider()
    {
        var autoAdvanceBehavior = new AutoAdvanceBehavior
        {
            TimestampAdvanceAmount = TimeSpan.FromSeconds(1),
        };
        var fakeTimer = new ManualTimeProvider { AutoAdvanceBehavior = autoAdvanceBehavior };
        return fakeTimer;
    }

    private sealed class SampleJob(CommunicationChannel channel) : IJob
    {
        public Task Run(JobExecutionContext context, CancellationToken token = default)
        {
            channel.TaskCompletionSource.SetResult();
            return Task.CompletedTask;
        }
    }

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
    }
}
