using System.Globalization;
using System.Threading.Channels;
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

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        Guid orchestrationId = events[0].CorrelationId;

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        var filteredEvents = events.Where(e => e.CorrelationId == orchestrationId).ToList();

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

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        Guid orchestrationId = events[0].CorrelationId;

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        var filteredEvents = events.Where(e => e.CorrelationId == orchestrationId).ToList();

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
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        // 1 initial + 2 retries, all 3 failed; 20 seconds timeout because retries take time
        var jobFinished = await WaitForJobsOrTimeout(3, TimeSpan.FromSeconds(20));
        jobFinished.ShouldBeTrue();

        FailingJobRetryTwice.Success.ShouldBeFalse();
        FailingJobRetryTwice.AttemptCount.ShouldBe(3);
    }

    [Fact]
    public async Task JobShouldHonorJobCancellationDuringRetry()
    {
        ServiceCollection.AddSingleton<MaxFailuresWrapper>(new MaxFailuresWrapper(int.MaxValue)); // Always fail
        ServiceCollection.AddNCronJob(n => n.AddJob<CancelRetryingJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));
        var provider = CreateServiceProvider();
        var jobExecutor = provider.GetRequiredService<JobExecutor>();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var attempts = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        attempts.ShouldBe("Job retrying");

        jobExecutor.CancelJobs();

        var cancellationMessageTask = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        cancellationMessageTask.ShouldBe("Job was canceled");

        CancelRetryingJob.Success.ShouldBeFalse();
        CancelRetryingJob.AttemptCount.ShouldBe(1);
    }

    [Fact]
    public async Task CancelledJobIsStillAValidExecution()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<CancelRetryingJob2>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        var provider = CreateServiceProvider();
        var jobExecutor = provider.GetRequiredService<JobExecutor>();

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(provider);

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        Guid orchestrationId = events.First().CorrelationId;

        await WaitForOrchestrationState(events, orchestrationId, ExecutionState.Retrying);

        jobExecutor.CancelJobs();

        await WaitForOrchestrationState(events, orchestrationId, ExecutionState.Cancelled);

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        var filteredEvents = events.Where((e) => e.CorrelationId == orchestrationId).ToList();

        filteredEvents[0].State.ShouldBe(ExecutionState.OrchestrationStarted);
        filteredEvents[1].State.ShouldBe(ExecutionState.NotStarted);
        filteredEvents[2].State.ShouldBe(ExecutionState.Scheduled);
        filteredEvents[3].State.ShouldBe(ExecutionState.Initializing);
        filteredEvents[4].State.ShouldBe(ExecutionState.Running);
        filteredEvents[5].State.ShouldBe(ExecutionState.Retrying);
        filteredEvents[6].State.ShouldBe(ExecutionState.Cancelled);
        filteredEvents[7].State.ShouldBe(ExecutionState.OrchestrationCompleted);
        filteredEvents.Count.ShouldBe(8);
    }

    [Fact]
    public async Task JobShouldHonorApplicationCancellationDuringRetry()
    {
        ServiceCollection.AddSingleton(new MaxFailuresWrapper(int.MaxValue)); // Always fail
        ServiceCollection.AddNCronJob(n => n.AddJob<CancelRetryingJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));
        var provider = CreateServiceProvider();
        var hostAppLifeTime = provider.GetRequiredService<IHostApplicationLifetime>();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var attempts = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        attempts.ShouldBe("Job retrying");

        hostAppLifeTime.StopApplication();
        await Task.Delay(100, CancellationToken); // allow some time for cancellation to propagate

        var cancellationMessageTask = CommunicationChannel.Reader.ReadAsync(CancellationToken.None).AsTask();
        var winnerTask = await Task.WhenAny(cancellationMessageTask, Task.Delay(5000, CancellationToken));
        winnerTask.ShouldBe(cancellationMessageTask);
        CancelRetryingJob.Success.ShouldBeFalse();
        CancelRetryingJob.AttemptCount.ShouldBe(1);
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
    private sealed class FailingJobRetryTwice(ChannelWriter<object> writer, MaxFailuresWrapper maxFailuresWrapper)
        : IJob
    {
        public static int AttemptCount { get; private set; }
        public static bool Success { get; private set; }

        public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);

            AttemptCount = context.Attempts;

            try
            {
                Success = true;
                if (AttemptCount <= maxFailuresWrapper.MaxFailuresBeforeSuccess)
                {
                    Success = false;
                    throw new InvalidOperationException("Job Failed");
                }
            }
            finally
            {
                await writer.WriteAsync(AttemptCount, token);
            }
        }
    }

    [RetryPolicy(retryCount: 4, PolicyType.FixedInterval)]
    private sealed class CancelRetryingJob(ChannelWriter<object> writer, MaxFailuresWrapper maxFailuresWrapper)
        : IJob
    {
        public static int AttemptCount { get; private set; }
        public static bool Success { get; private set; }

        public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);

            AttemptCount = context.Attempts;

            try
            {
                token.ThrowIfCancellationRequested();

                if (AttemptCount <= maxFailuresWrapper.MaxFailuresBeforeSuccess)
                {
                    Success = false;
                    throw new InvalidOperationException("Job Failed");
                }

                Success = true;
                await writer.WriteAsync("Job completed successfully", CancellationToken.None);
            }
            catch (Exception)
            {
                Success = false;
                if (!token.IsCancellationRequested)
                {
                    await writer.WriteAsync("Job retrying", CancellationToken.None);
                }
                throw;
            }
        }
    }

    [RetryPolicy(retryCount: 2, PolicyType.FixedInterval)]
    private sealed class CancelRetryingJob2 : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Job Failed");
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
