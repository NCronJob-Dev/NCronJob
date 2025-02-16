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
        ServiceCollection.AddSingleton<MaxFailuresWrapper>(new MaxFailuresWrapper(2));
        ServiceCollection.AddNCronJob(n => n.AddJob<FailingJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob(startMonitoringEvents: true);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var orchestrationId = Events[0].CorrelationId;

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);

        filteredEvents[4].State.ShouldBe(ExecutionState.Running);
        filteredEvents[5].State.ShouldBe(ExecutionState.Retrying);
        filteredEvents[6].State.ShouldBe(ExecutionState.Retrying);
        filteredEvents[7].State.ShouldBe(ExecutionState.Completing);
        filteredEvents.Count.ShouldBe(10);

        // Validate that the job was retried the correct number of times
        Storage.Entries[0].ShouldBe("3"); // 2 retries + 1 success
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task JobWithCustomPolicyShouldRetryOnFailure()
    {
        ServiceCollection.AddSingleton<MaxFailuresWrapper>(new MaxFailuresWrapper(5));
        ServiceCollection.AddNCronJob(n => n.AddJob<JobUsingCustomPolicy>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob(startMonitoringEvents: true);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var orchestrationId = Events[0].CorrelationId;

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);

        filteredEvents[4].State.ShouldBe(ExecutionState.Running);
        filteredEvents[5].State.ShouldBe(ExecutionState.Retrying);
        filteredEvents[6].State.ShouldBe(ExecutionState.Retrying);
        filteredEvents[7].State.ShouldBe(ExecutionState.Retrying);
        filteredEvents[8].State.ShouldBe(ExecutionState.Retrying);
        filteredEvents[9].State.ShouldBe(ExecutionState.Retrying);
        filteredEvents[10].State.ShouldBe(ExecutionState.Completing);
        filteredEvents.Count.ShouldBe(13);

        // Validate that the job was retried the correct number of times
        Storage.Entries[0].ShouldBe("6"); // 5 retries + 1 success
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task JobShouldFailAfterAllRetries()
    {
        ServiceCollection.AddSingleton<MaxFailuresWrapper>(new MaxFailuresWrapper(int.MaxValue)); // Always fail
        ServiceCollection.AddNCronJob(n => n.AddJob<FailingJobRetryTwice>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob(startMonitoringEvents: true);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var orchestrationId = Events[0].CorrelationId;

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);

        filteredEvents[4].State.ShouldBe(ExecutionState.Running);
        filteredEvents[5].State.ShouldBe(ExecutionState.Retrying);
        filteredEvents[6].State.ShouldBe(ExecutionState.Retrying);
        filteredEvents[7].State.ShouldBe(ExecutionState.Faulted);
        filteredEvents.Count.ShouldBe(9);

        // 1 initial + 2 retries, all 3 failed
        Storage.Entries[0].ShouldBe($"{orchestrationId} Failed - 1");
        Storage.Entries[1].ShouldBe($"{orchestrationId} Failed - 2");
        Storage.Entries[2].ShouldBe($"{orchestrationId} Failed - 3");
    }

    [Theory]
    [ClassData(typeof(CancellingContextTestData))]
    internal async Task JobShouldHonorCancellation(
        (Type jobType, ExecutionState state) jobAndState,
        (Func<IServiceProvider, object> serviceRetriever, Action<object> serviceTriggerer) context)
    {
        ServiceCollection.AddSingleton(new MaxFailuresWrapper(int.MaxValue)); // Always fail
        ServiceCollection.AddNCronJob(n => n.AddJob(jobAndState.jobType, p => p.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob(startMonitoringEvents: true);

        var service = context.serviceRetriever(ServiceProvider);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var orchestrationId = Events[0].CorrelationId;

        await WaitForOrchestrationState(orchestrationId, jobAndState.state);

        context.serviceTriggerer(service);

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);

        filteredEvents[0].State.ShouldBe(ExecutionState.OrchestrationStarted);
        filteredEvents[1].State.ShouldBe(ExecutionState.NotStarted);
        filteredEvents[2].State.ShouldBe(ExecutionState.Scheduled);
        filteredEvents[3].State.ShouldBe(ExecutionState.Initializing);
        filteredEvents[4].State.ShouldBe(ExecutionState.Running);

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
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(delayFactor, retryAttempt)));
    }
}
