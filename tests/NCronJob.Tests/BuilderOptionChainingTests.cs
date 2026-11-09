using Shouldly;

namespace NCronJob.Tests;

public sealed class BuilderOptionChainingTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Expiry = TimeSpan.FromSeconds(7);

    [Fact]
    public void ConditionTimeoutAndExpiryAreKeptAfterNamingAJob()
    {
        var builder = new JobOptionBuilder();

        builder.WithName("job")
            .OnlyIf(() => true)
            .OnlyIf((Delegate)(() => true))
            .OnlyIf(() => Task.FromResult(true))
            .WithTimeout(Timeout)
            .WithJobRunExpiry(Expiry);

        AssertFullyConfigured(builder.GetJobOptions().Single());
    }

    [Fact]
    public void ConditionTimeoutAndExpiryAreKeptAfterConfiguringACronExpression()
    {
        var builder = new JobOptionBuilder();

        builder.WithCronExpression(Cron.AtEveryMinute)
            .OnlyIf(() => true)
            .OnlyIf((Delegate)(() => true))
            .OnlyIf(() => Task.FromResult(true))
            .WithTimeout(Timeout)
            .WithJobRunExpiry(Expiry);

        AssertFullyConfigured(builder.GetJobOptions().Single());
    }

    [Fact]
    public void ConditionTimeoutAndExpiryAreKeptAfterAddingACronExpressionToAnExistingStage()
    {
        var builder = new JobOptionBuilder();

        builder.WithName("job")
            .WithCronExpression(Cron.AtEveryMinute)
            .OnlyIf(() => true)
            .OnlyIf((Delegate)(() => true))
            .OnlyIf(() => Task.FromResult(true))
            .WithTimeout(Timeout)
            .WithJobRunExpiry(Expiry);

        AssertFullyConfigured(builder.GetJobOptions().Single());
    }

    [Fact]
    public void ConditionTimeoutAndExpiryAreKeptAfterConfiguringAParameter()
    {
        var builder = new JobOptionBuilder();

        builder.WithParameter("parameter")
            .OnlyIf(() => true)
            .OnlyIf((Delegate)(() => true))
            .OnlyIf(() => Task.FromResult(true))
            .WithTimeout(Timeout)
            .WithJobRunExpiry(Expiry);

        AssertFullyConfigured(builder.GetJobOptions().Single());
    }

    [Fact]
    public void ConditionTimeoutAndExpiryAreKeptOnTheParameterStageOfACronJob()
    {
        var builder = new JobOptionBuilder();

        builder.WithName("job")
            .WithCronExpression(Cron.AtEveryMinute)
            .WithParameter("parameter")
            .OnlyIf(() => true)
            .OnlyIf((Delegate)(() => true))
            .OnlyIf(() => Task.FromResult(true))
            .WithTimeout(Timeout)
            .WithJobRunExpiry(Expiry);

        AssertFullyConfigured(builder.GetJobOptions().Single());
    }

    [Fact]
    public void ConditionTimeoutAndExpiryAreKeptWhenConfiguredBeforeTheCronExpression()
    {
        var builder = new JobOptionBuilder();

        builder.WithName("job")
            .WithTimeout(Timeout)
            .WithJobRunExpiry(Expiry)
            .OnlyIf(() => true)
            .OnlyIf((Delegate)(() => true))
            .OnlyIf(() => Task.FromResult(true))
            .WithCronExpression(Cron.AtEveryMinute);

        var option = builder.GetJobOptions().Single();
        AssertFullyConfigured(option);
        option.CronExpression.ShouldBe(Cron.AtEveryMinute);
    }

    [Fact]
    public void SkipIfStillRunningIsKeptOnEveryStage()
    {
        var builder = new JobOptionBuilder();

        builder.SkipIfStillRunning()
            .And.WithName("a").SkipIfStillRunning()
            .And.WithCronExpression(Cron.AtEveryMinute).SkipIfStillRunning()
            .And.WithName("b").WithCronExpression(Cron.AtEveryMinute).SkipIfStillRunning()
            .WithParameter("p").SkipIfStillRunning();

        builder.GetJobOptions().ShouldAllBe(o => o.SkipIfStillRunning);
    }

    [Fact]
    public async Task DependentJobKeepsAllOptionsOfOneChain()
    {
        var dependencyBuilder = new DependencyBuilder();

        dependencyBuilder.RunJob<DummyJob>()
            .OnlyIf(() => true)
            .OnlyIf((Delegate)(() => true))
            .OnlyIf(() => Task.FromResult(false))
            .WithTimeout(Timeout)
            .WithJobRunExpiry(Expiry);

        var definition = dependencyBuilder.GetDependentJobOption().Single().ToJobDefinition();

        definition.Timeout.ShouldBe(Timeout);
        definition.JobRunExpiry.ShouldBe(Expiry);
        definition.Condition.ShouldNotBeNull();
        (await definition.Condition(null!, CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task DependentJobConditionsAreAllRequired()
    {
        var dependencyBuilder = new DependencyBuilder();

        dependencyBuilder.RunJob<DummyJob>()
            .OnlyIf(() => true)
            .OnlyIf((Delegate)(() => true));

        var definition = dependencyBuilder.GetDependentJobOption().Single().ToJobDefinition();

        (await definition.Condition!(null!, CancellationToken.None)).ShouldBeTrue();
    }

    [Fact]
    public void DependentJobOptionsAreCommittedWhenNextDependentJobIsAddedOnTheDependencyBuilder()
    {
        var dependencyBuilder = new DependencyBuilder();

        dependencyBuilder.RunJob<DummyJob>().WithTimeout(Timeout);
        dependencyBuilder.RunJob<AnotherDummyJob>();
        dependencyBuilder.RunJob(() => { }, "delegate");

        var definitions = dependencyBuilder.GetDependentJobOption();

        definitions.Count.ShouldBe(3);
        definitions[0].ToJobDefinition().Timeout.ShouldBe(Timeout);
        definitions[1].ToJobDefinition().Timeout.ShouldBe(System.Threading.Timeout.InfiniteTimeSpan);
    }

    [Fact]
    public void DependentJobOptionsAreCommittedWhenADelegateDependentIsAddedOnTheDependencyBuilder()
    {
        var dependencyBuilder = new DependencyBuilder();

        dependencyBuilder.RunJob<DummyJob>().WithJobRunExpiry(Expiry);
        dependencyBuilder.RunJob(() => { }, "delegate");

        dependencyBuilder.GetDependentJobOption()[0].ToJobDefinition().JobRunExpiry.ShouldBe(Expiry);
    }

    [Fact]
    public void ChainedDependentJobsEachKeepTheirOwnOptions()
    {
        var dependencyBuilder = new DependencyBuilder();

        dependencyBuilder.RunJob<DummyJob>()
            .WithTimeout(Timeout)
            .RunJob<AnotherDummyJob>()
            .WithJobRunExpiry(Expiry)
            .RunJob(() => { }, "delegate")
            .WithTimeout(TimeSpan.FromSeconds(9));

        var definitions = dependencyBuilder.GetDependentJobOption().Select(d => d.ToJobDefinition()).ToArray();

        definitions[0].Timeout.ShouldBe(Timeout);
        definitions[0].JobRunExpiry.ShouldBeNull();
        definitions[1].Timeout.ShouldBe(System.Threading.Timeout.InfiniteTimeSpan);
        definitions[1].JobRunExpiry.ShouldBe(Expiry);
        definitions[2].Timeout.ShouldBe(TimeSpan.FromSeconds(9));
    }

    [Fact]
    public void DependencyBuilderRejectsNullDelegate()
    {
        Should.Throw<ArgumentNullException>(() => new DependencyBuilder().RunJob(null!));
    }

    [Fact]
    public void CronExpressionMustNotBeNullOnTheFollowUpStage()
    {
        var builder = new JobOptionBuilder();

        Should.Throw<ArgumentNullException>(() => builder.WithName("job").WithCronExpression(null!));
    }

    [Fact]
    public void ConditionPredicatesMustNotBeNull()
    {
        var builder = new JobOptionBuilder();

        Should.Throw<ArgumentNullException>(() => builder.OnlyIf((Func<bool>)null!));
        Should.Throw<ArgumentNullException>(() => builder.OnlyIf((Delegate)null!));
        Should.Throw<ArgumentNullException>(() => builder.OnlyIf((Func<Task<bool>>)null!));
    }

    [Fact]
    public void ConditionPredicateMustReturnABooleanLikeResult()
    {
        var builder = new JobOptionBuilder();

        var exception = Should.Throw<InvalidOperationException>(() => builder.OnlyIf((Delegate)(() => "nope")));

        exception.Message.ShouldBe("The condition predicate must return bool, Task<bool>, or ValueTask<bool>. Found: String");
    }

    [Fact]
    public async Task ValueTaskPredicatesAreSupported()
    {
        var builder = new JobOptionBuilder();
        builder.OnlyIf((Delegate)(() => new ValueTask<bool>(false)));

        var condition = builder.GetJobOptions().Single().Conditions.ShouldHaveSingleItem();

        (await condition(null!, CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task TaskPredicatesAreAwaitedWhenEvaluated()
    {
        var builder = new JobOptionBuilder();
        var evaluated = false;
        builder.OnlyIf(async () =>
        {
            await Task.Yield();
            evaluated = true;
            return false;
        });

        var condition = builder.GetJobOptions().Single().Conditions.ShouldHaveSingleItem();

        (await condition(null!, CancellationToken.None)).ShouldBeFalse();
        evaluated.ShouldBeTrue();
    }

    [Fact]
    public void TimeoutAndExpiryMustBePositiveOnEveryStage()
    {
        var invalid = TimeSpan.Zero;

        Should.Throw<ArgumentOutOfRangeException>(() => new JobOptionBuilder().WithName("a").WithTimeout(invalid));
        Should.Throw<ArgumentOutOfRangeException>(() => new JobOptionBuilder().WithName("a").WithJobRunExpiry(invalid));
        Should.Throw<ArgumentOutOfRangeException>(() => new JobOptionBuilder().WithCronExpression(Cron.AtEveryMinute).WithTimeout(invalid));
        Should.Throw<ArgumentOutOfRangeException>(() => new JobOptionBuilder().WithCronExpression(Cron.AtEveryMinute).WithJobRunExpiry(invalid));
        Should.Throw<ArgumentOutOfRangeException>(() => new JobOptionBuilder().WithName("a").WithCronExpression(Cron.AtEveryMinute).WithTimeout(invalid));
        Should.Throw<ArgumentOutOfRangeException>(() => new JobOptionBuilder().WithName("a").WithCronExpression(Cron.AtEveryMinute).WithJobRunExpiry(invalid));
        Should.Throw<ArgumentOutOfRangeException>(() => new JobOptionBuilder().WithParameter(1).WithTimeout(invalid));
        Should.Throw<ArgumentOutOfRangeException>(() => new JobOptionBuilder().WithParameter(1).WithJobRunExpiry(invalid));
        Should.Throw<ArgumentOutOfRangeException>(() => new DependencyBuilder().RunJob<DummyJob>().WithTimeout(invalid));
        Should.Throw<ArgumentOutOfRangeException>(() => new DependencyBuilder().RunJob<DummyJob>().WithJobRunExpiry(invalid));
    }

    [Fact]
    public void TimeoutErrorMessageExplainsTheAllowedValues()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new JobOptionBuilder().WithTimeout(TimeSpan.Zero));

        exception.Message.ShouldContain("The value must be positive or Timeout.InfiniteTimeSpan.");
        exception.ParamName.ShouldBe("timeout");
    }

    private static void AssertFullyConfigured(JobOption option)
    {
        option.Conditions.ShouldNotBeNull().Count.ShouldBe(3);
        option.Timeout.ShouldBe(Timeout);
        option.JobRunExpiry.ShouldBe(Expiry);
    }
}
