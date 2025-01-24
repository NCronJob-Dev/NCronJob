using System.Collections.Specialized;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polly;
using Shouldly;

namespace NCronJob.Tests;

public sealed class NCronJobRetryTests : JobIntegrationBase
{
    [Fact]
    public async Task JobShouldRetryOnFailure()
    {
        ServiceCollection.AddSingleton<MaxFailuresWrapper>(new MaxFailuresWrapper(2));
        ServiceCollection.AddNCronJob(n => n.AddJob<FailingJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        // Validate that the job was retried the correct number of times
        // Total = 2 retries + 1 success
        var attempts = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        attempts.ShouldBe(3);
    }

    [Fact]
    public async Task JobWithCustomPolicyShouldRetryOnFailure()
    {
        ServiceCollection.AddSingleton<MaxFailuresWrapper>(new MaxFailuresWrapper(3));
        ServiceCollection.AddNCronJob(n => n.AddJob<JobUsingCustomPolicy>(p => p.WithCronExpression(Cron.AtEveryMinute)));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        // Validate that the job was retried the correct number of times
        // Fail 3 times = 3 retries + 1 success
        var attempts = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        attempts.ShouldBe(4);
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
        var jobQueueManager = provider.GetRequiredService<JobQueueManager>();
        var jobQueue = jobQueueManager.GetOrAddQueue(typeof(CancelRetryingJob2).FullName!);

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(provider);

        JobRun? nextJob = null;
        var tcs = new TaskCompletionSource<JobStateType>();

        jobQueue.CollectionChanged += (sender, args) =>
        {
            if (args.Action == NotifyCollectionChangedAction.Add && nextJob == null)
            {
                nextJob = args.NewItems?.OfType<JobRun>().FirstOrDefault();
                nextJob!.CurrentState.Type.ShouldBe(JobStateType.NotStarted);
                nextJob!.JobExecutionCount.ShouldBe(0);
                nextJob!.OnStateChanged += (jr) =>
                {
                    if (jr.CurrentState == JobStateType.Cancelled)
                    {
                        tcs.SetResult(jr.CurrentState);
                    }
                };
            }
        };

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        while (nextJob!.CurrentState != JobStateType.Retrying)
        {
            await Task.Delay(1, CancellationToken);
        }
        // wait until we're in retrying state before cancelling
        jobExecutor.CancelJobs();

        var cancellationHandled = await Task.WhenAny(tcs.Task, Task.Delay(1000, CancellationToken));
        cancellationHandled.ShouldBe(tcs.Task);
        await Task.Delay(10, CancellationToken);
        nextJob!.CurrentState.Type.ShouldBe(JobStateType.Cancelled);
        nextJob!.JobExecutionCount.ShouldBe(1);

        Guid orchestrationId = events.First().CorrelationId;

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        var filteredEvents = events.Where((e) => e.CorrelationId == orchestrationId).ToList();

        Assert.Equal(ExecutionState.OrchestrationStarted, filteredEvents[0].State);
        Assert.Equal(ExecutionState.NotStarted, filteredEvents[1].State);
        Assert.Equal(ExecutionState.Scheduled, filteredEvents[2].State);
        Assert.Equal(ExecutionState.Initializing, filteredEvents[3].State);
        Assert.Equal(ExecutionState.Running, filteredEvents[4].State);
        Assert.Equal(ExecutionState.Retrying, filteredEvents[5].State);
        Assert.Equal(ExecutionState.Cancelled, filteredEvents[6].State);
        Assert.Equal(ExecutionState.OrchestrationCompleted, filteredEvents[7].State);
        Assert.Equal(8, filteredEvents.Count);
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
    private sealed class FailingJob(ChannelWriter<object> writer, MaxFailuresWrapper maxFailuresWrapper)
        : IJob
    {
        public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);

            var attemptCount = context.Attempts;

            if (attemptCount <= maxFailuresWrapper.MaxFailuresBeforeSuccess)
            {
                throw new InvalidOperationException("Job Failed");
            }

            await writer.WriteAsync(attemptCount, token);
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

    [RetryPolicy<MyCustomPolicyCreator>(3, 1)]
    private sealed class JobUsingCustomPolicy(ChannelWriter<object> writer, MaxFailuresWrapper maxFailuresWrapper)
        : IJob
    {
        public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);

            var attemptCount = context.Attempts;

            if (attemptCount <= maxFailuresWrapper.MaxFailuresBeforeSuccess)
            {
                throw new InvalidOperationException("Job Failed");
            }

            await writer.WriteAsync(attemptCount, token);
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
