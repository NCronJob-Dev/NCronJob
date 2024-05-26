using System.Collections.Concurrent;
using System.Threading.Channels;
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
            .ExecuteWhen(success: s => s.RunJob<DependentJob>("Message")));
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

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
            .ExecuteWhen(faulted: s => s.RunJob<DependentJob>("Message")));
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        provider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<PrincipalJob>(false);

        using var tcs = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var result = await CommunicationChannel.Reader.ReadAsync(tcs.Token) as string;
        result.ShouldBe("Me: Message Parent: Failed");
    }

    [Fact]
    public async Task CorrelationIdIsSharedByJobsAndTheirDependencies()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddSingleton(new Storage());
        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalCorrelationIdJob>()
            .ExecuteWhen(success: s => s.RunJob<DependentCorrelationIdJob>()));
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        provider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<PrincipalCorrelationIdJob>();

        using var tcs = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await CommunicationChannel.Reader.ReadAsync(tcs.Token);
        var storage = provider.GetRequiredService<Storage>();
        storage.Guids.Count.ShouldBe(2);
        storage.Guids.Distinct().Count().ShouldBe(1);
    }

    [Fact]
    public async Task SkipChildrenShouldPreventDependentJobsFromRunning()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddSingleton(new Storage());
        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalCorrelationIdJob>()
            .ExecuteWhen(success: s => s.RunJob<DependentCorrelationIdJob>()));
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        provider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<PrincipalCorrelationIdJob>(parameter: true);

        using var tcs = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await CommunicationChannel.Reader.ReadAsync(tcs.Token);
        await Task.Delay(150, tcs.Token);
        var storage = provider.GetRequiredService<Storage>();
        storage.Guids.Count.ShouldBe(1);
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

    private sealed class DependentJob(ChannelWriter<object> writer) : IJob
    {
        public async Task RunAsync(JobExecutionContext context, CancellationToken token)
            => await writer.WriteAsync($"Me: {context.Parameter} Parent: {context.ParentOutput}", token);
    }

    private sealed class PrincipalCorrelationIdJob(Storage storage, ChannelWriter<object> writer) : IJob
    {
        public async Task RunAsync(JobExecutionContext context, CancellationToken token)
        {
            storage.Guids.Add(context.CorrelationId);

            if (context.Parameter is true)
            {
                context.SkipChildren();
                await writer.WriteAsync("done", token);
            }
        }
    }

    private sealed class DependentCorrelationIdJob(Storage storage, ChannelWriter<object> writer) : IJob
    {
        public async Task RunAsync(JobExecutionContext context, CancellationToken token)
        {
            storage.Guids.Add(context.CorrelationId);
            await writer.WriteAsync("done", token);
        }
    }

    private sealed class Storage
    {
        public ConcurrentBag<Guid> Guids { get; } = [];
    }
}
