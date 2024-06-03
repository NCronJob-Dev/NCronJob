using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace NCronJob.Tests;

public class RuntimeJobRegistryTests : JobIntegrationBase
{
    [Fact]
    public async Task DynamicallyAddedJobIsExecuted()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob();
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
        var registry = provider.GetRequiredService<IRuntimeJobRegistry>();

        registry.AddJob(s => s.AddJob(async (ChannelWriter<object> writer) => await writer.WriteAsync(true), "* * * * *"));

        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task CanRemoveJobByName()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(
            s => s.AddJob(async (ChannelWriter<object> writer) => await writer.WriteAsync(true), "* * * * *", jobName: "Job"));
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
        var registry = provider.GetRequiredService<IRuntimeJobRegistry>();

        registry.RemoveJob("Job");

        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1, TimeSpan.FromMilliseconds(200));
        jobFinished.ShouldBeFalse();
    }
}
