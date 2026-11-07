using Shouldly;

namespace NCronJob.Tests;

public sealed class JobConditionChainTests
{
    [Fact]
    public async Task AllConditionsSatisfiedYieldsTrue()
    {
        var chain = JobConditionChain.Append(Always(true), [Always(true), Always(true)]);

        (await chain(null!, CancellationToken.None)).ShouldBeTrue();
    }

    [Fact]
    public async Task WithoutPreviousConditionOnlyTheAddedConditionsDecide()
    {
        var chain = JobConditionChain.Append(null, [Always(true)]);

        (await chain(null!, CancellationToken.None)).ShouldBeTrue();
    }

    [Fact]
    public async Task FailingPreviousConditionYieldsFalseAndSkipsTheAddedConditions()
    {
        var addedEvaluations = 0;
        var chain = JobConditionChain.Append(Always(false), [Counting(true, () => addedEvaluations++)]);

        (await chain(null!, CancellationToken.None)).ShouldBeFalse();
        addedEvaluations.ShouldBe(0);
    }

    [Fact]
    public async Task FailingAddedConditionYieldsFalseAndSkipsLaterOnes()
    {
        var laterEvaluations = 0;
        var chain = JobConditionChain.Append(Always(true), [Always(false), Counting(true, () => laterEvaluations++)]);

        (await chain(null!, CancellationToken.None)).ShouldBeFalse();
        laterEvaluations.ShouldBe(0);
    }

    [Fact]
    public async Task PreviousConditionIsEvaluatedBeforeTheAddedOnes()
    {
        List<string> order = [];
        var chain = JobConditionChain.Append(
            Counting(true, () => order.Add("previous")),
            [Counting(true, () => order.Add("added"))]);

        await chain(null!, CancellationToken.None);

        order.ShouldBe(["previous", "added"]);
    }

    [Fact]
    public async Task ConditionsReceiveTheServiceProviderAndToken()
    {
        IServiceProvider? receivedProvider = null;
        CancellationToken receivedToken = default;
        using var cts = new CancellationTokenSource();
        var provider = new ServiceProviderStub();
        var chain = JobConditionChain.Append(null, [(sp, ct) =>
        {
            receivedProvider = sp;
            receivedToken = ct;
            return ValueTask.FromResult(true);
        }]);

        await chain(provider, cts.Token);

        receivedProvider.ShouldBeSameAs(provider);
        receivedToken.ShouldBe(cts.Token);
    }

    private static Func<IServiceProvider, CancellationToken, ValueTask<bool>> Always(bool result) =>
        (_, _) => ValueTask.FromResult(result);

    private static Func<IServiceProvider, CancellationToken, ValueTask<bool>> Counting(bool result, Action onEvaluated) =>
        (_, _) =>
        {
            onEvaluated();
            return ValueTask.FromResult(result);
        };

    private sealed class ServiceProviderStub : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}

public sealed class StartupSummaryFormatterTests
{
    [Fact]
    public void NoJobsProducesTheHeaderOnly() =>
        StartupSummaryFormatter.Format([]).ShouldBe("NCronJob started with 0 jobs");

    [Fact]
    public void SingleJobUsesTheSingularForm() =>
        StartupSummaryFormatter.Format([(CronJob("0 2 * * *"), null)])
            .ShouldStartWith("NCronJob started with 1 job:");

    [Fact]
    public void EnabledJobWithoutUpcomingRunIsReportedAsSuch()
    {
        var summary = StartupSummaryFormatter.Format([(CronJob("0 2 * * *"), null)]);

        summary.ShouldEndWith("no upcoming run");
        summary.ShouldNotContain("disabled");
    }

    [Fact]
    public void DisabledJobIsReportedAsDisabled()
    {
        var job = CronJob("0 2 * * *");
        job.Disable();

        var summary = StartupSummaryFormatter.Format([(job, null)]);

        summary.ShouldEndWith("disabled");
        summary.ShouldNotContain("no upcoming run");
    }

    [Fact]
    public void ScheduledJobShowsCronTimeZoneAndNextRunInThatTimeZone()
    {
        var job = CronJob("0 2 * * *", "Europe/Helsinki");
        var nextRun = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var summary = StartupSummaryFormatter.Format([(job, CreateRun(job, nextRun))]);

        summary.ShouldContain("0 2 * * *");
        summary.ShouldContain("Europe/Helsinki");
        summary.ShouldEndWith("next 2030-01-01 02:00");
    }

    [Fact]
    public void JobWithoutScheduleShowsNoCronAndUtc()
    {
        var job = JobDefinition.CreateTyped("manual", typeof(DummyJob), parameter: null);

        var rows = StartupSummaryFormatter.Format([(job, null)]).ReplaceLineEndings("\n").Split('\n');

        rows[1].ShouldStartWith("  manual");
        rows[1].ShouldContain(TimeZoneInfo.Utc.Id);
    }

    [Fact]
    public void ColumnsAreAlignedAcrossJobs()
    {
        var shortName = CronJob("0 2 * * *", name: "a");
        var longName = CronJob("*/5 * * * *", name: "much-longer-name");

        var rows = StartupSummaryFormatter.Format([(shortName, null), (longName, null)])
            .ReplaceLineEndings("\n").Split('\n');

        rows[1].IndexOf("0 2 * * *", StringComparison.Ordinal)
            .ShouldBe(rows[2].IndexOf("*/5 * * * *", StringComparison.Ordinal));
    }

    private static JobDefinition CronJob(string cron, string? timeZoneId = null, string name = "job")
    {
        var job = JobDefinition.CreateTyped(name, typeof(DummyJob), parameter: null);
        job.UpdateWith(new JobOption
        {
            CronExpression = cron,
            TimeZoneInfo = timeZoneId is null ? null : TimeZoneInfo.FindSystemTimeZoneById(timeZoneId),
        });
        return job;
    }

    private static JobRun CreateRun(JobDefinition job, DateTimeOffset runAt) =>
        JobRun.CreateInstant(
            TimeProvider.System,
            _ => { },
            job,
            runAt,
            OptionalParameter.Unspecified,
            CancellationToken.None);
}

public sealed class JobDefinitionValidationTests
{
    [Fact]
    public void TypedJobDefinitionRejectsTypesThatAreNotJobs()
    {
        var exception = Should.Throw<InvalidOperationException>(
            () => JobDefinition.CreateTyped(typeof(string), parameter: null));

        exception.Message.ShouldBe($"Type '{typeof(string)}' doesn't implement '{nameof(IJob)}'.");
    }

    [Fact]
    public void DependentTypedJobDefinitionRejectsTypesThatAreNotJobs()
    {
        var exception = Should.Throw<InvalidOperationException>(
            () => DependentJobDefinition.CreateTyped(typeof(string), parameter: null));

        exception.Message.ShouldBe($"Type '{typeof(string)}' doesn't implement '{nameof(IJob)}'.");
    }

    [Fact]
    public void DependentTypedJobDefinitionAcceptsJobTypes() =>
        DependentJobDefinition.CreateTyped(typeof(DummyJob), parameter: null, customName: "dependent")
            .IsTypedJob.ShouldBeTrue();

    [Fact]
    public void DependentJobDefinitionGuardsItsArguments()
    {
        Should.Throw<ArgumentNullException>(() => DependentJobDefinition.CreateTyped(null!, parameter: null))
            .ParamName.ShouldBe("type");
        Should.Throw<ArgumentNullException>(() => DependentJobDefinition.CreateUntyped("name", null!))
            .ParamName.ShouldBe("jobDelegate");
        Should.Throw<ArgumentNullException>(() => DependentJobDefinition.CreateTyped(typeof(DummyJob), parameter: null).UpdateWith(null!))
            .ParamName.ShouldBe("jobOption");
    }

    [Fact]
    public void MarkingAStartupJobTwiceIsRejected()
    {
        var job = JobDefinition.CreateTyped("startup", typeof(DummyJob), parameter: null);
        job.MarkAsStartupJob(shouldCrashOnFailure: false);

        var exception = Should.Throw<InvalidOperationException>(() => job.MarkAsStartupJob(shouldCrashOnFailure: false));

        exception.Message.ShouldBe($"Job '{job.Name}' is already defined as a startup job.");
    }

    [Fact]
    public void EmptyConditionListDoesNotInstallACondition()
    {
        var job = JobDefinition.CreateTyped("job", typeof(DummyJob), parameter: null);

        job.UpdateWith(new JobOption { Conditions = [] });

        job.Condition.ShouldBeNull();
    }

    [Fact]
    public async Task ConditionsAddedAcrossOptionsMustAllBeSatisfied()
    {
        var job = JobDefinition.CreateTyped("job", typeof(DummyJob), parameter: null);
        job.UpdateWith(new JobOption { Conditions = [(_, _) => ValueTask.FromResult(true)] });
        job.UpdateWith(new JobOption { Conditions = [(_, _) => ValueTask.FromResult(false)] });

        (await job.Condition!(null!, CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public void InvalidCronExpressionIsRejected()
    {
        var exception = Should.Throw<InvalidOperationException>(() => JobSchedule.Parse("60 * * * *", null));

        exception.Message.ShouldBe("Invalid cron expression");
    }

    [Fact]
    public void CronExpressionWithWrongPartCountMentionsTheExpectedPrecision()
    {
        var exception = Should.Throw<ArgumentException>(() => JobSchedule.Parse("* * * *", null));

        exception.Message.ShouldStartWith("Invalid cron expression format for minute precision.");
        exception.ParamName.ShouldBe("cronExpression");
    }
}
