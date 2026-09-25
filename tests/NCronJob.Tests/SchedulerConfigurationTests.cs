using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace NCronJob.Tests;

public sealed class SchedulerConfigurationTests : JobIntegrationBase
{
    [Fact]
    public void SchedulerSettingsKeepTheirDefaults()
    {
        ServiceCollection.AddNCronJob();

        var settings = ServiceProvider.GetRequiredService<ConcurrencySettings>();

        settings.MaxDegreeOfParallelism.ShouldBe(Environment.ProcessorCount * 4);
        settings.DefaultJobRunExpiry.ShouldBe(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void RepeatedRegistrationsUseTheSameConcurrencySettingsForValidation()
    {
        ServiceCollection.AddNCronJob(options => options.WithMaxDegreeOfParallelism(1));

        var exception = Should.Throw<InvalidOperationException>(() =>
            ServiceCollection.AddNCronJob(options => options.AddJob<ConcurrentJob>()));

        exception.Message.ShouldContain("cannot exceed the global limit (1)");
    }

    [Fact]
    public void ConfiguredConcurrencyAllowsMatchingJobConcurrency()
    {
        Should.NotThrow(() => ServiceCollection.AddNCronJob(options =>
        {
            options.WithMaxDegreeOfParallelism(2);
            options.AddJob<ConcurrentJob>();
        }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MaxDegreeOfParallelismMustBePositive(int value)
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            ServiceCollection.AddNCronJob(options => options.WithMaxDegreeOfParallelism(value)));
    }

    [Theory]
    [MemberData(nameof(InvalidDurations))]
    public void TimeoutAndExpiryMustBePositive(TimeSpan value)
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            ServiceCollection.AddNCronJob(options =>
            {
                options.WithDefaultJobRunExpiry(value);
                options.AddJob<TimeoutJob>(job => job
                    .WithTimeout(value)
                    .WithJobRunExpiry(value));
            }));
    }

    [Fact]
    public void InfiniteTimeoutAndExpiryAreValidAcrossBuilderStages()
    {
        Should.NotThrow(() => ServiceCollection.AddNCronJob(options =>
        {
            options.WithDefaultJobRunExpiry(Timeout.InfiniteTimeSpan);
            options.AddJob<TimeoutJob>(job => job
                .WithName("unlimited")
                .WithTimeout(Timeout.InfiniteTimeSpan)
                .WithJobRunExpiry(Timeout.InfiniteTimeSpan)
                .WithCronExpression(Cron.Never)
                .WithParameter(null)
                .RunAtStartup(false));
        }));
    }

    [Fact]
    public async Task JobTimeoutCancelsWithoutTriggeringFaultDependency()
    {
        ServiceCollection.AddNCronJob(options =>
            options.AddJob<TimeoutJob>(job => job.WithTimeout(TimeSpan.FromSeconds(1)))
                .ExecuteWhen(faulted: dependency => dependency.RunJob<FaultDependencyJob>()));

        await StartNCronJob();

        var orchestrationId = ServiceProvider
            .GetRequiredService<IInstantJobRegistry>()
            .RunInstantJob<TimeoutJob>(token: CancellationToken);

        await WaitForOrchestrationState(orchestrationId, ExecutionState.Running);
        FakeTimer.Advance(TimeSpan.FromSeconds(2));
        await WaitForOrchestrationCompletion(orchestrationId);

        var states = Events.FilterByOrchestrationId(orchestrationId);
        states.ShouldContain(progress => progress.State == ExecutionState.Cancelled);
        states.ShouldNotContain(progress => progress.State == ExecutionState.Faulted);
        Storage.Entries.ShouldBeEmpty();
    }

    [Fact]
    public void TimeoutConfigurationPreservesRetryPolicy()
    {
        ServiceCollection.AddNCronJob(options =>
            options.AddJob<RetryingTimeoutJob>(job => job.WithTimeout(TimeSpan.FromMilliseconds(500))));

        var definition = ServiceProvider
            .GetRequiredService<JobRegistry>()
            .FindFirstRootJobDefinition(typeof(RetryingTimeoutJob));

        definition.ShouldNotBeNull();
        definition.Timeout.ShouldBe(TimeSpan.FromMilliseconds(500));
        definition.RetryPolicy.ShouldNotBeNull();
    }

    [Fact]
    public void JobRunExpiryUsesGlobalDefaultAndPerJobOverride()
    {
        var timeProvider = new FakeTimeProvider();
        var settings = new ConcurrencySettings { DefaultJobRunExpiry = TimeSpan.FromMinutes(3) };
        var globalDefinition = JobDefinition.CreateTyped(typeof(TimeoutJob), null);
        var overriddenDefinition = JobDefinition.CreateTyped(typeof(TimeoutJob), null);
        overriddenDefinition.UpdateWith(new JobOption { JobRunExpiry = TimeSpan.FromMinutes(5) });
        var runAt = timeProvider.GetUtcNow();

        var globalRun = JobRun.CreateCron(timeProvider, _ => { }, globalDefinition, runAt, settings);
        var overriddenRun = JobRun.CreateCron(timeProvider, _ => { }, overriddenDefinition, runAt, settings);

        timeProvider.Advance(TimeSpan.FromMinutes(4));

        globalRun.IsExpired.ShouldBeTrue();
        overriddenRun.IsExpired.ShouldBeFalse();
    }

    [Fact]
    public void InfiniteJobRunExpiryDisablesExpiry()
    {
        var timeProvider = new FakeTimeProvider();
        var settings = new ConcurrencySettings { DefaultJobRunExpiry = Timeout.InfiniteTimeSpan };
        var definition = JobDefinition.CreateTyped(typeof(TimeoutJob), null);
        var run = JobRun.CreateCron(timeProvider, _ => { }, definition, timeProvider.GetUtcNow(), settings);

        timeProvider.Advance(TimeSpan.FromDays(365));

        run.IsExpired.ShouldBeFalse();
    }

    public static TheoryData<TimeSpan> InvalidDurations => new()
    {
        TimeSpan.Zero,
        TimeSpan.FromTicks(-2),
    };

    [SupportsConcurrency(2)]
    private sealed class ConcurrentJob : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token) => Task.CompletedTask;
    }

    private sealed class TimeoutJob : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token) =>
            Task.Delay(Timeout.InfiniteTimeSpan, token);
    }

    private sealed class FaultDependencyJob(Storage storage) : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            storage.Add("fault dependency ran");
            return Task.CompletedTask;
        }
    }

    [RetryPolicy(retryCount: 3, PolicyType.FixedInterval)]
    private sealed class RetryingTimeoutJob : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            throw new InvalidOperationException("Retry");
        }
    }
}
