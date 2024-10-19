using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace NCronJob.Tests;

public class RunDependentJobTests : JobIntegrationBase
{
    [Fact]
    public async Task WhenJobWasSuccessful_DependentJobShouldRun()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalJob>()
            .ExecuteWhen(success: s => s.RunJob<DependentJob>("Message")));
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        provider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true);

        var result = await CommunicationChannel.Reader.ReadAsync(CancellationToken) as string;
        result.ShouldBe("Me: Message Parent: Success");
    }

    [Fact]
    public async Task WhenJobWasFailed_DependentJobShouldRun()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalJob>()
            .ExecuteWhen(faulted: s => s.RunJob<DependentJob>("Message")));
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        provider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(false);

        var result = await CommunicationChannel.Reader.ReadAsync(CancellationToken) as string;
        result.ShouldBe("Me: Message Parent: Failed");
    }

    [Fact]
    public async Task CorrelationIdIsSharedByJobsAndTheirDependencies()
    {
        ServiceCollection.AddSingleton(new Storage());
        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalCorrelationIdJob>()
            .ExecuteWhen(success: s => s.RunJob<DependentCorrelationIdJob>()));
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        provider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalCorrelationIdJob>();

        await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        var storage = provider.GetRequiredService<Storage>();
        storage.Guids.Count.ShouldBe(2);
        storage.Guids.Distinct().Count().ShouldBe(1);
    }

    [Fact]
    public async Task SkipChildrenShouldPreventDependentJobsFromRunning()
    {
        ServiceCollection.AddSingleton(new Storage());
        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalCorrelationIdJob>()
            .ExecuteWhen(success: s => s.RunJob<DependentCorrelationIdJob>()));
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        provider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<PrincipalCorrelationIdJob>(parameter: true);

        await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        var storage = provider.GetRequiredService<Storage>();
        storage.Guids.Count.ShouldBe(1);
    }

    [Fact]
    public async Task WhenJobWasSuccessful_DependentAnonymousJobShouldRun()
    {
        Func<ChannelWriter<object>, JobExecutionContext, Task> execution = async (writer, context) => await writer.WriteAsync($"Parent: {context.ParentOutput}");
        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalJob>()
            .ExecuteWhen(success: s => s.RunJob(execution)));
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        provider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true);

        var result = await CommunicationChannel.Reader.ReadAsync(CancellationToken) as string;
        result.ShouldBe("Parent: Success");
    }

    [Fact]
    public async Task CanBuildAChainOfDependentJobs()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalJob>()
            .ExecuteWhen(success: s => s.RunJob<DependentJob>("1"))
            .ExecuteWhen(success: s => s.RunJob<DependentJob>("2"))
            .ExecuteWhen(success: s => s.RunJob<DependentJob>("3")));
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        provider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true);

        List<string?> results = [];
        using var timeoutToken = new CancellationTokenSource(2000);
        using var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(timeoutToken.Token, CancellationToken);
        results.Add(await CommunicationChannel.Reader.ReadAsync(linkedToken.Token) as string);
        results.Add(await CommunicationChannel.Reader.ReadAsync(linkedToken.Token) as string);
        results.Add(await CommunicationChannel.Reader.ReadAsync(linkedToken.Token) as string);
        results.ShouldContain("Me: 1 Parent: Success");
        results.ShouldContain("Me: 2 Parent: Success");
        results.ShouldContain("Me: 3 Parent: Success");
    }

    private sealed class PrincipalJob : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
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
        public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
            => await writer.WriteAsync($"Me: {context.Parameter} Parent: {context.ParentOutput}", token);
    }

    private sealed class PrincipalCorrelationIdJob(Storage storage, ChannelWriter<object> writer) : IJob
    {
        public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
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
        public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
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
