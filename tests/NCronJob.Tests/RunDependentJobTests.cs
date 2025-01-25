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

        provider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true, token: CancellationToken);

        List<string?> results = [];
        results.Add(await CommunicationChannel.Reader.ReadAsync(CancellationToken) as string);
        results.Add(await CommunicationChannel.Reader.ReadAsync(CancellationToken) as string);
        results.ShouldContain("PrincipalJob: Success");
        results.ShouldContain("DependentJob: Message Parent: Success");
    }

    [Fact]
    public async Task WhenJobWasFailed_DependentJobShouldRun()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalJob>()
            .ExecuteWhen(faulted: s => s.RunJob<DependentJob>("Message")));

        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        provider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(false, token: CancellationToken);

        List<string?> results = [];
        results.Add(await CommunicationChannel.Reader.ReadAsync(CancellationToken) as string);
        results.Add(await CommunicationChannel.Reader.ReadAsync(CancellationToken) as string);
        results.ShouldContain("PrincipalJob: Failed");
        results.ShouldContain("DependentJob: Message Parent: Failed");
    }

    [Fact]
    public async Task RemovingAJobShouldAlsoRemoveItsDependencies()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<MainJob>()
            .ExecuteWhen(success: s => s.RunJob<SubMainJob>()));

        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var instantJobRegistry = provider.GetRequiredService<IInstantJobRegistry>();
        instantJobRegistry.ForceRunInstantJob<MainJob>(token: CancellationToken);

        var result = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        result.ShouldBe(nameof(MainJob));
        result = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        result.ShouldBe(nameof(SubMainJob));

        var registry = provider.GetRequiredService<IRuntimeJobRegistry>();

        registry.RemoveJob<MainJob>();
        registry.TryRegister(n => n.AddJob<MainJob>());

        instantJobRegistry.ForceRunInstantJob<MainJob>(token: CancellationToken);

        result = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        result.ShouldBe(nameof(MainJob));

        (await WaitForJobsOrTimeout(1, TimeSpan.FromMilliseconds(500))).ShouldBe(false);
    }

    [Fact]
    public async Task CorrelationIdIsSharedByJobsAndTheirDependencies()
    {
        ServiceCollection.AddSingleton(new Storage());
        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalCorrelationIdJob>()
            .ExecuteWhen(success: s => s.RunJob<DependentCorrelationIdJob>()));

        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var correlationId = provider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalCorrelationIdJob>(token: CancellationToken);

        await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        var storage = provider.GetRequiredService<Storage>();
        storage.Guids.Count.ShouldBe(2);
        storage.Guids.Distinct().Count().ShouldBe(1);
        storage.Guids.First().ShouldBe(correlationId);
    }

    [Fact]
    public async Task SkipChildrenShouldPreventDependentJobsFromRunning()
    {
        ServiceCollection.AddSingleton(new Storage());
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob<PrincipalCorrelationIdJob>()
                .ExecuteWhen(success: s => s.RunJob<DependentCorrelationIdJob>())
                .ExecuteWhen(success: s => s.RunJob((ChannelWriter<object> writer) => writer.WriteAsync("1", CancellationToken)));
        });

        var provider = CreateServiceProvider();

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(provider);

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        Guid orchestrationId = provider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<PrincipalCorrelationIdJob>(parameter: true, token: CancellationToken);

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        var storage = provider.GetRequiredService<Storage>();
        storage.Guids.Count.ShouldBe(1);

        Assert.All(events, e => Assert.Equal(orchestrationId, e.CorrelationId));
        Assert.Equal(ExecutionState.OrchestrationStarted, events[0].State);
        Assert.Equal(ExecutionState.NotStarted, events[1].State);
        Assert.Equal(ExecutionState.Initializing, events[2].State);
        Assert.Equal(ExecutionState.Running, events[3].State);
        Assert.Equal(ExecutionState.Completing, events[4].State);
        Assert.Equal(ExecutionState.WaitingForDependency, events[5].State);

        // Assess the dependent jobs
        Assert.Equal(events[1].RunId, events[6].ParentRunId);
        Assert.Equal(ExecutionState.NotStarted, events[6].State);
        Assert.Equal(events[1].RunId, events[7].ParentRunId);
        Assert.Equal(ExecutionState.Skipped, events[7].State);
        Assert.Equal(events[1].RunId, events[8].ParentRunId);
        Assert.Equal(ExecutionState.NotStarted, events[8].State);
        Assert.Equal(events[1].RunId, events[9].ParentRunId);
        Assert.Equal(ExecutionState.Skipped, events[9].State);

        Assert.NotEqual(events[6].RunId, events[8].RunId);

        Assert.Equal(ExecutionState.Completed, events[10].State);
        Assert.Equal(ExecutionState.OrchestrationCompleted, events[11].State);
        Assert.Equal(12, events.Count);
    }

    [Fact]
    public async Task WhenJobWasSuccessful_DependentAnonymousJobShouldRun()
    {
        Func<ChannelWriter<object>, JobExecutionContext, Task> execution = async (writer, context)
            => await writer.WriteAsync($"Parent: {context.ParentOutput}", CancellationToken);

        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalJob>()
            .ExecuteWhen(success: s => s.RunJob(execution)));

        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        provider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true, token: CancellationToken);

        List<string?> results = [];
        results.Add(await CommunicationChannel.Reader.ReadAsync(CancellationToken) as string);
        results.Add(await CommunicationChannel.Reader.ReadAsync(CancellationToken) as string);
        results.ShouldContain("PrincipalJob: Success");
        results.ShouldContain("Parent: Success");
    }

    [Fact]
    public async Task CanBuildAChainOfDependentJobsThatRunAfterOneJob()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalJob>()
            .ExecuteWhen(success: s => s.RunJob<DependentJob>("1").RunJob<DependentJob>("2"))
            .ExecuteWhen(success: s => s.RunJob<DependentJob>("3")));

        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        provider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true, token: CancellationToken);

        List<string?> results = [];
        using var timeoutToken = new CancellationTokenSource(2000);
        using var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(timeoutToken.Token, CancellationToken);
        results.Add(await CommunicationChannel.Reader.ReadAsync(linkedToken.Token) as string);
        results.Add(await CommunicationChannel.Reader.ReadAsync(linkedToken.Token) as string);
        results.Add(await CommunicationChannel.Reader.ReadAsync(linkedToken.Token) as string);
        results.Add(await CommunicationChannel.Reader.ReadAsync(linkedToken.Token) as string);

        results.ShouldContain("PrincipalJob: Success");
        results.ShouldContain("DependentJob: 1 Parent: Success");
        results.ShouldContain("DependentJob: 2 Parent: Success");
        results.ShouldContain("DependentJob: 3 Parent: Success");
    }

    [Fact]
    public async Task CanTriggerAChainOfDependentJobs()
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob<PrincipalJob>().ExecuteWhen(success: s => s.RunJob<DependentJob>());
            n.AddJob<DependentJob>().ExecuteWhen(success: s => s.RunJob<DependentDependentJob>());
        });

        var provider = CreateServiceProvider();

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(provider);

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        Guid orchestrationId = provider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true, token: CancellationToken);

        List<string?> results = [];
        results.Add(await CommunicationChannel.Reader.ReadAsync(CancellationToken) as string);
        results.Add(await CommunicationChannel.Reader.ReadAsync(CancellationToken) as string);
        results.Add(await CommunicationChannel.Reader.ReadAsync(CancellationToken) as string);

        results.ShouldContain("PrincipalJob: Success");
        results.ShouldContain("DependentJob:  Parent: Success");
        results.ShouldContain("Dependent job did run");

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        Assert.All(events, e => Assert.Equal(orchestrationId, e.CorrelationId));
        Assert.Equal(ExecutionState.OrchestrationStarted, events[0].State);
        Assert.Equal(ExecutionState.Completed, events[18].State);
        Assert.Equal(ExecutionState.OrchestrationCompleted, events[19].State);
        Assert.Equal(20, events.Count);
    }

    [Fact]
    public async Task CanBuildAScheduledChainOfDependentJobs()
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob<PrincipalJob>(o => o.WithCronExpression(Cron.AtEveryMinute).WithParameter(true))
                .ExecuteWhen(success: s => s.RunJob<DependentJob>());
            n.AddJob<DependentJob>(o => o.WithCronExpression(Cron.Never))
                .ExecuteWhen(success: s => s.RunJob<DependentDependentJob>());
        });

        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        List<string?> results = [];
        results.Add(await CommunicationChannel.Reader.ReadAsync(CancellationToken) as string);
        results.Add(await CommunicationChannel.Reader.ReadAsync(CancellationToken) as string);
        results.Add(await CommunicationChannel.Reader.ReadAsync(CancellationToken) as string);

        results.ShouldContain("PrincipalJob: Success");
        results.ShouldContain("DependentJob:  Parent: Success");
        results.ShouldContain("Dependent job did run");
    }

    [Fact]
    public async Task ConfiguringDifferentDependentJobsForSchedulesShouldResultInIndependentRuns()
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob<PrincipalJob>(s => s.WithCronExpression("1 0 1 * *").WithParameter(true))
                .ExecuteWhen(s => s.RunJob((ChannelWriter<object> writer) => writer.WriteAsync("1", CancellationToken).AsTask()));
            n.AddJob<PrincipalJob>(s => s.WithCronExpression("1 0 2 * *").WithParameter(true))
                .ExecuteWhen(s => s.RunJob((ChannelWriter<object> writer) => writer.WriteAsync("2", CancellationToken).AsTask()));
        });

        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        List<string?> results = [];
        results.Add(await CommunicationChannel.Reader.ReadAsync(CancellationToken) as string);
        results.Add(await CommunicationChannel.Reader.ReadAsync(CancellationToken) as string);
        results.ShouldContain("PrincipalJob: Success");
        results.ShouldContain("1");

        results = [];
        FakeTimer.Advance(TimeSpan.FromDays(1));

        results.Add(await CommunicationChannel.Reader.ReadAsync(CancellationToken) as string);
        results.Add(await CommunicationChannel.Reader.ReadAsync(CancellationToken) as string);
        results.ShouldContain("PrincipalJob: Success");
        results.ShouldContain("2");
    }

    [Fact]
    public async Task WhenJobIsNotCreated_DependentFailureJobShouldRun()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<JobThatThrowsInCtor>()
            .ExecuteWhen(faulted: s => s.RunJob<DependentJob>("After Exception")));
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        provider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<JobThatThrowsInCtor>(false, token: CancellationToken);

        var message = await CommunicationChannel.Reader.ReadAsync(CancellationToken) as string;
        message.ShouldNotBeNull();
        message.ShouldContain("DependentJob: After Exception Parent:");
    }

    private sealed class PrincipalJob(ChannelWriter<object> writer) : IJob
    {
        public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            Action maybeThrow = () => { };

            if (context.Parameter is true)
            {
                context.Output = "Success";
            }
            else
            {
                context.Output = "Failed";
                maybeThrow = () => throw new InvalidOperationException("Failed");
            }

            await writer.WriteAsync($"{nameof(PrincipalJob)}: {context.Output}", token);

            maybeThrow();
        }
    }

    private sealed class DependentJob(ChannelWriter<object> writer) : IJob
    {
        public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
            => await writer.WriteAsync($"{nameof(DependentJob)}: {context.Parameter} Parent: {context.ParentOutput}", token);
    }


    private sealed class DependentDependentJob(ChannelWriter<object> writer) : IJob
    {
        public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
            => await writer.WriteAsync("Dependent job did run", token);
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

    private sealed class MainJob(ChannelWriter<object> writer) : IJob
    {
        public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
            => await writer.WriteAsync(nameof(MainJob), token);
    }

    private sealed class SubMainJob(ChannelWriter<object> writer) : IJob
    {
        public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
            => await writer.WriteAsync(nameof(SubMainJob), token);
    }

    private sealed class Storage
    {
        public ConcurrentBag<Guid> Guids { get; } = [];
    }

    private sealed class JobThatThrowsInCtor : IJob
    {
        public JobThatThrowsInCtor()
            => throw new InvalidOperationException();

        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
            => Task.CompletedTask;
    }
}
