using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace NCronJob.Tests;

public sealed class JobParameterTests : JobIntegrationBase
{
    [Fact]
    public async Task EachJobRunHasItsOwnScope()
    {
        ServiceCollection.AddScoped<GuidGenerator>();
        ServiceCollection.AddNCronJob(n => n.AddJob<ScopedServiceJob>(
            p => p.WithCronExpression(Cron.AtEveryMinute).WithParameter("null")
                .And
                .WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForNthOrchestrationState(
            ExecutionState.Completed,
            2);

        Storage.Entries.Distinct().Count().ShouldBe(Storage.Entries.Count);
        Storage.Entries.Count.ShouldBe(2);
    }

    [Theory]
    [MemberData(nameof(InstantNamedJobRunners))]
    public async Task NamedInstantJobsCanExplicitlyOverrideConfiguredParameterWithNull(
        Func<IInstantJobRegistry, TimeProvider, string, object?, CancellationToken, Guid> instantJobRunner)
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob<DummyJob>(s => s.WithName("good").WithParameter("good_param"))
                .ExecuteWhen(success: s => s.RunJob<AnotherDummyJob>());
            n.AddJob<DummyJob>(s => s.WithCronExpression(Cron.Never).WithParameter("bad_param"))
                .ExecuteWhen(success: s => s.RunJob<ExceptionJob>());
        });

        await StartNCronJob();

        var instantJobRegistry = ServiceProvider.GetRequiredService<IInstantJobRegistry>();

        var orchestrationId = instantJobRunner(instantJobRegistry, FakeTimer, "good", null, CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId);

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: ");
        Storage.Entries[1].ShouldBe("AnotherDummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Theory]
    [MemberData(nameof(InstantNamedJobRunners))]
    public async Task CanDisambiguateSimarlyTypedJobsThroughNamesWhileOverridingParameters(
        Func<IInstantJobRegistry, TimeProvider, string, object?, CancellationToken, Guid> instantJobRunner)
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob<DummyJob>(s => s.WithName("good").WithParameter("good_param"))
                .ExecuteWhen(success: s => s.RunJob<AnotherDummyJob>());
            n.AddJob<DummyJob>(s => s.WithCronExpression(Cron.Never).WithParameter("bad_param"))
                .ExecuteWhen(success: s => s.RunJob<ExceptionJob>());
        });

        await StartNCronJob();

        var instantJobRegistry = ServiceProvider.GetRequiredService<IInstantJobRegistry>();

        var orchestrationId = instantJobRunner(instantJobRegistry, FakeTimer, "good", "overriden_param", CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId);

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: overriden_param");
        Storage.Entries[1].ShouldBe("AnotherDummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Theory]
    [MemberData(nameof(InstantNamedJobRunners))]
    public async Task CanTrigerDynamicJobsThroughNames(
        Func<IInstantJobRegistry, TimeProvider, string, object?, CancellationToken, Guid> instantJobRunner)
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob(
                UntypedJob,
                Cron.Never,
                jobName: "good");
        });

        await StartNCronJob();

        var instantJobRegistry = ServiceProvider.GetRequiredService<IInstantJobRegistry>();

        var orchestrationId = instantJobRunner(instantJobRegistry, FakeTimer, "good", null, CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId);

        Storage.Entries[0].ShouldBe("Done - Parameter : ");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Theory]
    [MemberData(nameof(InstantNamedJobRunners))]
    public async Task CanTrigerDynamicJobsThroughNamesWhilePassingParameters(
        Func<IInstantJobRegistry, TimeProvider, string, object?, CancellationToken, Guid> instantJobRunner)
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob(
                UntypedJob,
                Cron.Never,
                jobName: "good");
        });

        await StartNCronJob();

        var instantJobRegistry = ServiceProvider.GetRequiredService<IInstantJobRegistry>();

        var orchestrationId = instantJobRunner(instantJobRegistry, FakeTimer, "good", "param", CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId);

        Storage.Entries[0].ShouldBe("Done - Parameter : param");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Theory]
    [MemberData(nameof(InstantNamedJobRunners))]
    public async Task ShouldThrowRuntimeExceptionWhenTriggeringThroughTheInstantJobRegistryAnUnregisteredNamedJob(
        Func<IInstantJobRegistry, TimeProvider, string, object?, CancellationToken, Guid> instantJobRunner)
    {
        ServiceCollection.AddNCronJob(n =>
        {
        });

        await StartNCronJob();

        var instantJobRegistry = ServiceProvider.GetRequiredService<IInstantJobRegistry>();

        Action act = () => instantJobRunner(instantJobRegistry, FakeTimer, "good", "param", CancellationToken);

        act.ShouldThrow<InvalidOperationException>()
            .Message.ShouldContain("Job with name 'good' not found.");
    }

    public static TheoryData<Func<IInstantJobRegistry, TimeProvider, string, object?, CancellationToken, Guid>> InstantNamedJobRunners()
    {
        var t = new TheoryData<Func<IInstantJobRegistry, TimeProvider, string, object?, CancellationToken, Guid>>();
        t.Add((i, f, n, p, t) => i.RunInstantJob(n, p, t));
        t.Add((i, f, n, p, t) => i.RunScheduledJob(n, f.GetUtcNow(), p, t));
        t.Add((i, f, n, p, t) => i.ForceRunInstantJob(n, p, t));
        t.Add((i, f, n, p, t) => i.ForceRunScheduledJob(n, TimeSpan.Zero, p, t));
        return t;
    }

    private sealed class ScopedServiceJob(Storage storage, GuidGenerator guidGenerator) : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            storage.Add(guidGenerator.NewGuid.ToString());
            return Task.CompletedTask;
        }
    }
}
