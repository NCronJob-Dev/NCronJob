using System.Threading.Channels;
using LinkDotNet.NCronJob;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace NCronJob.Tests;

public class RunDependentJobTests : JobIntegrationBase
{
    [Fact]
    public async Task WhenJobWasSuccessful_DependentJobShouldRun()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalJob>()
            .When(success: s => s.RunJob<DependentJob>("Message")));
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(default);

        provider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<PrincipalJob>(true);

        using var tcs = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var result = await CommunicationChannel.Reader.ReadAsync(tcs.Token) as string;
        result.ShouldBe("Me: Message Parent: Success");
    }

    [Fact]
    public async Task WhenJobWasFailed_DependentJobShouldRun()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalJob>()
            .When(faulted: s => s.RunJob<DependentJob>("Message")));
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(default);

        provider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<PrincipalJob>(false);

        using var tcs = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var result = await CommunicationChannel.Reader.ReadAsync(tcs.Token) as string;
        result.ShouldBe("Me: Message Parent: Failed");
    }

    private sealed class PrincipalJob : IJob
    {
        public Task RunAsync(JobExecutionContext context, CancellationToken token)
        {
            if (context.Parameter is true)
            {
                context.Output = "Success";
            }
            else
            {
                context.Output = "Failed";
                throw new InvalidOperationException("Failed");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class DependentJob : IJob
    {
        private readonly ChannelWriter<object> writer;

        public DependentJob(ChannelWriter<object> writer) => this.writer = writer;

        public async Task RunAsync(JobExecutionContext context, CancellationToken token)
            => await writer.WriteAsync($"Me: {context.Parameter} Parent: {context.ParentOutput}", token);
    }
}
