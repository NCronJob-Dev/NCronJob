using LinkDotNet.NCronJob;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using TimeProviderExtensions;

namespace NCronJob.Tests;

public class NCronJobIntegrationTests
{
    [Fact]
    public async Task CronJobThatIsScheduledEveryMinuteShouldBeExecuted()
    {
        using var tokenSource = new CancellationTokenSource();
        var autoAdvanceBehavior = new AutoAdvanceBehavior
        {
            TimestampAdvanceAmount = TimeSpan.FromSeconds(1),
        };
        var fakeTimer = new ManualTimeProvider { AutoAdvanceBehavior = autoAdvanceBehavior };
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<TimeProvider>(fakeTimer);
        serviceCollection.AddNCronJob();
        serviceCollection.AddCronJob<SampleJob>(p => p.CronExpression = "* * * * *");
        var provider = serviceCollection.BuildServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(tokenSource.Token);

        fakeTimer.Advance(TimeSpan.FromMinutes(2));
        var delay = Task.Delay(100, tokenSource.Token);
        var finishTask = await Task.WhenAny(SampleJob.WasCalledTask, delay);
        finishTask.ShouldNotBe(delay);
        await tokenSource.CancelAsync();
    }

    private sealed class SampleJob : IJob
    {
        private static readonly TaskCompletionSource WasCalledTcs = new();
        public static Task WasCalledTask => WasCalledTcs.Task;

        public Task Run(JobExecutionContext context, CancellationToken token = default)
        {
            WasCalledTcs.SetResult();
            return Task.CompletedTask;
        }
    }
}
