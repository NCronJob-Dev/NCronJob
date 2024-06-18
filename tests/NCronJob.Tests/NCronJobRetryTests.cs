using System.Collections.Specialized;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Polly;
using Shouldly;

namespace NCronJob.Tests;

public sealed class NCronJobRetryTests : JobIntegrationBase
{
    [Fact]
    public async Task JobShouldRetryOnFailure()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddSingleton<MaxFailuresWrapper>(new MaxFailuresWrapper(2));
        ServiceCollection.AddNCronJob(n => n.AddJob<FailingJob>(p => p.WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));

        // Validate that the job was retried the correct number of times
        // Total = 2 retries + 1 success
        var attempts = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        attempts.ShouldBe(3);
    }

    [Fact]
    public async Task JobWithCustomPolicyShouldRetryOnFailure()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddSingleton<MaxFailuresWrapper>(new MaxFailuresWrapper(3));
        ServiceCollection.AddNCronJob(n => n.AddJob<JobUsingCustomPolicy>(p => p.WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));

        // Validate that the job was retried the correct number of times
        // Fail 3 times = 3 retries + 1 success
        var attempts = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        attempts.ShouldBe(4);
    }

    [Fact]
    public async Task JobShouldFailAfterAllRetries()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddSingleton<MaxFailuresWrapper>(new MaxFailuresWrapper(int.MaxValue)); // Always fail
        ServiceCollection.AddNCronJob(n => n.AddJob<FailingJobRetryTwice>(p => p.WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));

        // 1 initial + 2 retries, all 3 failed; 20 seconds timeout because retries take time
        var jobFinished = await WaitForJobsOrTimeout(3, TimeSpan.FromSeconds(20));
        jobFinished.ShouldBeTrue();

        FailingJobRetryTwice.Success.ShouldBeFalse();
        FailingJobRetryTwice.AttemptCount.ShouldBe(3);
    }

    [Fact]
    public async Task JobShouldHonorJobCancellationDuringRetry()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddSingleton<MaxFailuresWrapper>(new MaxFailuresWrapper(int.MaxValue)); // Always fail
        ServiceCollection.AddNCronJob(n => n.AddJob<CancelRetryingJob>(p => p.WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();
        var jobExecutor = provider.GetRequiredService<JobExecutor>();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
        fakeTimer.Advance(TimeSpan.FromMinutes(1));

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
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(n => n.AddJob<CancelRetryingJob2>(p => p.WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();
        var jobExecutor = provider.GetRequiredService<JobExecutor>();
        var jobQueueManager = provider.GetRequiredService<JobQueueManager>();
        var jobQueue = jobQueueManager.GetOrAddQueue(typeof(CancelRetryingJob2).FullName!);

        JobRun? nextJob = null;
        var tcs = new TaskCompletionSource<JobStateType>();

        jobQueue.CollectionChanged += (sender, args) =>
        {
            if (args.Action == NotifyCollectionChangedAction.Add && nextJob == null)
            {
                nextJob = args.NewItems?.OfType<JobRun>().FirstOrDefault();
                nextJob!.CurrentState.Type.ShouldBe(JobStateType.NotStarted);
                nextJob!.JobExecutionCount.ShouldBe(0);
                nextJob!.OnStateChanged += (s, e) =>
                {
                    if (e == JobStateType.Cancelled)
                    {
                        tcs.SetResult(e);
                    }
                };
            }
        };

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        
        while (nextJob!.CurrentState != JobStateType.Retrying)
        {
            await Task.Delay(1);
        }
        // wait until we're in retrying state before cancelling
        jobExecutor.CancelJobs();

        var cancellationHandled = await Task.WhenAny(tcs.Task, Task.Delay(1000));
        cancellationHandled.ShouldBe(tcs.Task);
        await Task.Delay(10);
        nextJob!.CurrentState.Type.ShouldBe(JobStateType.Cancelled);
        nextJob!.JobExecutionCount.ShouldBe(1);
    }

    [Fact]
    public async Task JobShouldHonorApplicationCancellationDuringRetry()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddSingleton(new MaxFailuresWrapper(int.MaxValue)); // Always fail
        ServiceCollection.AddNCronJob(n => n.AddJob<CancelRetryingJob>(p => p.WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();
        var hostAppLifeTime = provider.GetRequiredService<IHostApplicationLifetime>();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
        fakeTimer.Advance(TimeSpan.FromMinutes(1));

        var attempts = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        attempts.ShouldBe("Job retrying");

        hostAppLifeTime.StopApplication();
        await Task.Delay(100); // allow some time for cancellation to propagate

        var cancellationMessageTask = CommunicationChannel.Reader.ReadAsync(CancellationToken.None).AsTask();
        var winnerTask = await Task.WhenAny(cancellationMessageTask, Task.Delay(5000));
        winnerTask.ShouldBe(cancellationMessageTask);
        CancelRetryingJob.Success.ShouldBeFalse();
        CancelRetryingJob.AttemptCount.ShouldBe(1);
    }

    private sealed record MaxFailuresWrapper(int MaxFailuresBeforeSuccess = 3);

    [RetryPolicy(retryCount: 3, PolicyType.FixedInterval)]
    private sealed class FailingJob(ChannelWriter<object> writer, MaxFailuresWrapper maxFailuresWrapper)
        : IJob
    {
        public async Task RunAsync(JobExecutionContext context, CancellationToken token)
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

        public async Task RunAsync(JobExecutionContext context, CancellationToken token)
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

        public async Task RunAsync(JobExecutionContext context, CancellationToken token)
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
        public Task RunAsync(JobExecutionContext context, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Job Failed");
        }
    }

    [RetryPolicy<MyCustomPolicyCreator>(3, 1)]
    private sealed class JobUsingCustomPolicy(ChannelWriter<object> writer, MaxFailuresWrapper maxFailuresWrapper)
        : IJob
    {
        public async Task RunAsync(JobExecutionContext context, CancellationToken token)
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
