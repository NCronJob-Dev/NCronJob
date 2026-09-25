using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polly;
using Shouldly;

namespace NCronJob.Tests;

public sealed class RetryTests : JobIntegrationBase
{
    [Fact]
    public async Task JobShouldRetryOnFailure()
    {
        const int failuresBeforeSuccess = 2;
        ServiceCollection.AddSingleton<MaxFailuresWrapper>(new MaxFailuresWrapper(failuresBeforeSuccess));
        ServiceCollection.AddNCronJob(n => n.AddJob<FailingJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var orchestrationId = Events[0].CorrelationId;

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        Events.FilterByOrchestrationId(orchestrationId).Select(e => e.State).ShouldBe([
            ExecutionState.OrchestrationStarted,
            ExecutionState.NotStarted,
            ExecutionState.Scheduled,
            ExecutionState.Initializing,
            ExecutionState.Running,
            ExecutionState.Retrying,
            ExecutionState.Retrying,
            ExecutionState.Completing,
            ExecutionState.Completed,
            ExecutionState.OrchestrationCompleted]);

        const int attemptsIncludingSuccess = failuresBeforeSuccess + 1;
        Storage.Entries.ShouldBe([attemptsIncludingSuccess.ToString(CultureInfo.InvariantCulture)]);
    }

    [Fact]
    public async Task JobWithCustomPolicyShouldRetryOnFailure()
    {
        const int failuresBeforeSuccess = 5;
        ServiceCollection.AddSingleton<MaxFailuresWrapper>(new MaxFailuresWrapper(failuresBeforeSuccess));
        ServiceCollection.AddNCronJob(n => n.AddJob<JobUsingCustomPolicy>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var orchestrationId = Events[0].CorrelationId;

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        Events.FilterByOrchestrationId(orchestrationId).Select(e => e.State).ShouldBe([
            ExecutionState.OrchestrationStarted,
            ExecutionState.NotStarted,
            ExecutionState.Scheduled,
            ExecutionState.Initializing,
            ExecutionState.Running,
            ExecutionState.Retrying,
            ExecutionState.Retrying,
            ExecutionState.Retrying,
            ExecutionState.Retrying,
            ExecutionState.Retrying,
            ExecutionState.Completing,
            ExecutionState.Completed,
            ExecutionState.OrchestrationCompleted]);

        const int attemptsIncludingSuccess = failuresBeforeSuccess + 1;
        Storage.Entries.ShouldBe([attemptsIncludingSuccess.ToString(CultureInfo.InvariantCulture)]);
    }

    [Fact]
    public async Task JobShouldFailAfterAllRetries()
    {
        ServiceCollection.AddSingleton<MaxFailuresWrapper>(new MaxFailuresWrapper(int.MaxValue)); // Always fail
        ServiceCollection.AddNCronJob(n => n.AddJob<FailingJobRetryTwice>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var orchestrationId = Events[0].CorrelationId;

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        Events.FilterByOrchestrationId(orchestrationId).Select(e => e.State).ShouldBe([
            ExecutionState.OrchestrationStarted,
            ExecutionState.NotStarted,
            ExecutionState.Scheduled,
            ExecutionState.Initializing,
            ExecutionState.Running,
            ExecutionState.Retrying,
            ExecutionState.Retrying,
            ExecutionState.Faulted,
            ExecutionState.OrchestrationCompleted]);

        Storage.Entries.ShouldBe([
            $"{orchestrationId} Failed - 1",
            $"{orchestrationId} Failed - 2",
            $"{orchestrationId} Failed - 3"]);
    }

    [Fact]
    public async Task ExponentialBackoffDelaysHonorTimeProvider()
    {
        ServiceCollection.AddSingleton(new MaxFailuresWrapper(2));
        ServiceCollection.AddNCronJob(n => n.AddJob<ExponentialBackoffJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var orchestrationId = Events[0].CorrelationId;

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        var attemptTimes = Storage.TimedEntries
            .Select(e => DateTimeOffset.Parse(e.Item1, CultureInfo.InvariantCulture))
            .ToList();
        attemptTimes.Count.ShouldBe(3);

        var firstDelay = attemptTimes[1] - attemptTimes[0];
        var secondDelay = attemptTimes[2] - attemptTimes[1];

        // The fake clock is advanced in 1s steps while waiting, so only lower bounds are exact.
        // The upper bound still fails when delays elapse in real time, as every real second maps to ~50 fake seconds.
        firstDelay.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromSeconds(2));
        secondDelay.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromSeconds(4));
        (firstDelay + secondDelay).ShouldBeLessThan(TimeSpan.FromSeconds(30));
    }

    [Theory]
    [ClassData(typeof(CancellingContextTestData))]
    internal async Task JobShouldHonorCancellation(
        (Type jobType, ExecutionState state) jobAndState,
        (Func<IServiceProvider, object> serviceRetriever, Action<object> serviceTriggerer) context)
    {
        ServiceCollection.AddSingleton(new MaxFailuresWrapper(int.MaxValue)); // Always fail
        ServiceCollection.AddNCronJob(n => n.AddJob(jobAndState.jobType, p => p.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob();

        var service = context.serviceRetriever(ServiceProvider);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var orchestrationId = Events[0].CorrelationId;

        await AdvanceTimeUntilOrchestrationState(orchestrationId, jobAndState.state);

        context.serviceTriggerer(service);

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);

        filteredEvents.Take(5).Select(e => e.State).ShouldBe([
            ExecutionState.OrchestrationStarted,
            ExecutionState.NotStarted,
            ExecutionState.Scheduled,
            ExecutionState.Initializing,
            ExecutionState.Running]);

        filteredEvents[filteredEvents.Count - 3].State.ShouldBe(jobAndState.state);
        filteredEvents[filteredEvents.Count - 2].State.ShouldBe(ExecutionState.Cancelled);
        filteredEvents[filteredEvents.Count - 1].State.ShouldBe(ExecutionState.OrchestrationCompleted);
    }

    internal sealed class CancellingContextTestData
        : MatrixTheoryData<(Type jobType, ExecutionState state), (Func<IServiceProvider, object> serviceRetriever, Action<object> serviceTriggerer)>
    {
        private static readonly (Type jobType, ExecutionState state)[] JobAndStateTypes =
        [
            (typeof(FailingJob), ExecutionState.Retrying),
            (typeof(RetryingLongRunningJob), ExecutionState.Running),
        ];

        private static readonly (Func<IServiceProvider, object> serviceRetriever, Action<object> serviceTriggerer)[] Actions =
        [
            ((sp) => sp.GetRequiredService<JobExecutor>(), (s) => ((JobExecutor)s).CancelJobs()),
            ((sp) => sp.GetRequiredService<IHostApplicationLifetime>(), (s) => ((IHostApplicationLifetime)s).StopApplication())
        ];

        public CancellingContextTestData() : base(JobAndStateTypes, Actions)
        {
        }
    }

    private sealed record MaxFailuresWrapper(int MaxFailuresBeforeSuccess = 3);

    [RetryPolicy(retryCount: 3, PolicyType.FixedInterval)]
    private sealed class FailingJob(Storage storage, MaxFailuresWrapper maxFailuresWrapper)
        : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);

            var attemptCount = context.Attempts;

            if (attemptCount <= maxFailuresWrapper.MaxFailuresBeforeSuccess)
            {
                throw new InvalidOperationException("Job Failed");
            }

            storage.Add(attemptCount.ToString(CultureInfo.InvariantCulture));

            return Task.CompletedTask;
        }
    }

    [RetryPolicy(retryCount: 2)]
    private sealed class ExponentialBackoffJob(Storage storage, MaxFailuresWrapper maxFailuresWrapper)
        : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);

            storage.Add(context.Attempts.ToString(CultureInfo.InvariantCulture));

            return context.Attempts <= maxFailuresWrapper.MaxFailuresBeforeSuccess
                ? throw new InvalidOperationException("Job Failed")
                : Task.CompletedTask;
        }
    }

    [RetryPolicy(retryCount: 2, PolicyType.FixedInterval)]
    private sealed class FailingJobRetryTwice(Storage storage, MaxFailuresWrapper maxFailuresWrapper)
        : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.Attempts <= maxFailuresWrapper.MaxFailuresBeforeSuccess)
            {
                storage.Add($"{context.CorrelationId} Failed - {context.Attempts}");
                throw new InvalidOperationException("Job Failed");
            }

            storage.Add($"{context.CorrelationId} Succeeded - {context.Attempts}");

            return Task.CompletedTask;
        }
    }

    [RetryPolicy(retryCount: 2, PolicyType.FixedInterval)]
    private sealed class RetryingLongRunningJob : LongRunningJob
    {
        public RetryingLongRunningJob(Storage storage, TimeProvider timeProvider)
            : base(storage, timeProvider)
        {
        }
    }

    [RetryPolicy<MyCustomPolicyCreator>(retryCount: 5, delayFactor: 1)]
    private sealed class JobUsingCustomPolicy(Storage storage, MaxFailuresWrapper maxFailuresWrapper)
        : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);

            var attemptCount = context.Attempts;

            if (attemptCount <= maxFailuresWrapper.MaxFailuresBeforeSuccess)
            {
                throw new InvalidOperationException("Job Failed");
            }

            storage.Add(attemptCount.ToString(CultureInfo.InvariantCulture));

            return Task.CompletedTask;
        }
    }

    private sealed class MyCustomPolicyCreator : IPolicyCreator
    {
        public IAsyncPolicy CreatePolicy(int maxRetryAttempts = 3, double delayFactor = 2) =>
            Policy.Handle<Exception>()
                .WaitAndRetryAsync(maxRetryAttempts,
                    _ => TimeSpan.Zero);
    }
}
