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

    [Fact]
    public async Task TypedJobReceivesItsParameterAndHandlersSeeTheTypedContext()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<ReportJob>(p => p.WithName("report").WithParameter(new ReportRequest("EU")))
            .AddNotificationHandler<ReportHandler>());

        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob("report", CancellationToken);
        await WaitForOrchestrationCompletion(orchestrationId);

        Storage.Entries.ShouldBe(["Report EU", "Handled EU"]);
    }

    [Fact]
    public async Task DelegateJobReceivesTypedParameter()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob(
            (IJobExecutionContext<ReportRequest> context, Storage storage) => storage.Add($"Report {context.Parameter.Region}"),
            Cron.Never,
            jobName: "report"));

        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob("report", new ReportRequest("CH"), CancellationToken);
        await WaitForOrchestrationCompletion(orchestrationId);

        Storage.Entries.ShouldBe(["Report CH"]);
    }

    [Fact]
    public void RegisteringParameterForJobWithoutParameterThrows()
    {
        Action act = () => ServiceCollection.AddNCronJob(n => n.AddJob<ExceptionJob>(p => p.WithParameter("value")));

        act.ShouldThrow<ArgumentException>().Message.ShouldContain("does not accept a parameter");
    }

    [Fact]
    public void RegisteringParameterOfWrongTypeThrows()
    {
        Action act = () => ServiceCollection.AddNCronJob(n => n.AddJob<ReportJob>(p => p.WithParameter("EU")));

        act.ShouldThrow<ArgumentException>().Message.ShouldContain($"expects a parameter of type '{typeof(ReportRequest)}'");
    }

    [Fact]
    public void DependentJobWithParameterOfWrongTypeThrowsAtRegistration()
    {
        Action act = () => ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>()
            .ExecuteWhen(success: s => s.RunJob<ReportJob>(42)));

        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public async Task TriggeringWithParameterOfWrongTypeThrowsImmediately()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<ReportJob>());

        await StartNCronJob();

        Action act = () => ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<ReportJob>("EU", CancellationToken);

        act.ShouldThrow<ArgumentException>();
        Storage.Entries.ShouldBeEmpty();
    }

    [Fact]
    public void UpdatingParameterWithWrongTypeThrows()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<ReportJob>(p => p.WithCronExpression(Cron.Never).WithName("report")));
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        Action act = () => registry.UpdateParameter("report", "EU");

        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void JobRegisteredByItsTypedInterfaceKeepsItsParameterType()
    {
        JobDefinition.CreateTyped(typeof(IJob<ReportRequest>), new ReportRequest("EU")).ParameterType.ShouldBe(typeof(ReportRequest));
    }

    [Fact]
    public void DelegateWithConflictingParameterTypesIsRejected()
    {
        Action act = () => JobDefinition.CreateUntyped(null, (IJobExecutionContext<object> first, IJobExecutionContext<string> second) => { });

        act.ShouldThrow<InvalidOperationException>().Message.ShouldContain("more than one parameter type");
    }

    [Fact]
    public async Task DelegateArgumentOfCustomContextTypeIsResolvedFromServices()
    {
        ServiceCollection.AddSingleton<ICustomContext, CustomContext>();
        ServiceCollection.AddNCronJob(n => n.AddJob(
            (ICustomContext custom, Storage storage) => storage.Add(custom.GetType().Name),
            Cron.Never,
            jobName: "custom"));

        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob("custom", CancellationToken);
        await WaitForOrchestrationCompletion(orchestrationId);

        Storage.Entries.ShouldBe([nameof(CustomContext)]);
    }

    private interface ICustomContext : IJobExecutionContext;

    private sealed class CustomContext : ICustomContext
    {
        public Guid Id => Guid.Empty;
        public object? Output { get; set; }
        public int Attempts => 0;
        public Guid CorrelationId => Guid.Empty;
        public object? ParentOutput => null;
        public string? JobName => null;
        public Type? JobType => null;
        public TriggerType TriggerType => TriggerType.Instant;
        public void SkipChildren() { }
    }

    private sealed record ReportRequest(string Region);

    private sealed class ReportJob(Storage storage) : IJob<ReportRequest>
    {
        public Task RunAsync(IJobExecutionContext<ReportRequest> context, CancellationToken token)
        {
            storage.Add($"Report {context.Parameter.Region}");
            return Task.CompletedTask;
        }
    }

    private sealed class ReportHandler(Storage storage) : IJobNotificationHandler<ReportJob>
    {
        public Task HandleAsync(IJobExecutionContext context, Exception? exception, CancellationToken cancellationToken)
        {
            if (context is IJobExecutionContext<ReportRequest> typedContext)
            {
                storage.Add($"Handled {typedContext.Parameter.Region}");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class ScopedServiceJob(Storage storage, GuidGenerator guidGenerator) : IJob<string?>
    {
        public Task RunAsync(IJobExecutionContext<string?> context, CancellationToken token)
        {
            storage.Add(guidGenerator.NewGuid.ToString());
            return Task.CompletedTask;
        }
    }
}
