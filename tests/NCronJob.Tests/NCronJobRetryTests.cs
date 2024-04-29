using System.Threading.Channels;
using LinkDotNet.NCronJob;
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
        // 3 retries PolicyType.ExponentialBackoff
        ServiceCollection.AddSingleton<MaxFailuresWrapper>(new MaxFailuresWrapper(3));
        ServiceCollection.AddNCronJob(n => n.AddJob<FailingJob>(p => p.WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken.None);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));

        // Validate that the job was retried the correct number of times
        // Fail 3 times = 3 retries + 1 success
        var attempts = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        attempts.ShouldBe(4);
    }

    [Fact]
    public async Task JobWithCustomPolicyShouldRetryOnFailure()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        // 3 retries custom policy MyCustomPolicyCreator
        ServiceCollection.AddSingleton<MaxFailuresWrapper>(new MaxFailuresWrapper(3));
        ServiceCollection.AddNCronJob(n => n.AddJob<JobUsingCustomPolicy>(p => p.WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken.None);

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

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken.None);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));

        // 1 initial + 2 retries, all 3 failed; 20 seconds timeout because retries take time
        var jobFinished = await WaitForJobsOrTimeout(3, 20);
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

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken.None);
        fakeTimer.Advance(TimeSpan.FromMinutes(1));

        // First message expected: "Job retrying"
        var attempts = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        attempts.ShouldBe("Job retrying");

        // Trigger Job cancellation
        jobExecutor.CancelJobs();

        // Expect "Job was canceled" message next
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

        // get the specific registry entry for CancelRetryingJob2
        var cronRegistryEntries = provider.GetServices<RegistryEntry>();
        var cancelRetryingJobEntry = cronRegistryEntries.First(entry => entry.Type == typeof(CancelRetryingJob2));

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken.None);
        fakeTimer.Advance(TimeSpan.FromMinutes(1));

        // Trigger Job cancellation
        jobExecutor.CancelJobs();

        // Wait for the cancellation signal or timeout after 5 seconds
        var cancellationHandled = await Task.WhenAny(CancellationSignaled, Task.Delay(5000));
        cancellationHandled.ShouldBe(CancellationSignaled); // Assert that we did not hit the timeout

        cancelRetryingJobEntry.JobExecutionCount.ShouldBe(1);
    }

    [Fact]
    public async Task JobShouldHonorApplicationCancellationDuringRetry()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddSingleton<MaxFailuresWrapper>(new MaxFailuresWrapper(int.MaxValue)); // Always fail
        ServiceCollection.AddNCronJob(n => n.AddJob<CancelRetryingJob>(p => p.WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();
        var hostAppLifeTime = provider.GetRequiredService<IHostApplicationLifetime>();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken.None);
        fakeTimer.Advance(TimeSpan.FromMinutes(1));

        // First message expected: "Job retrying"
        var attempts = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        attempts.ShouldBe("Job retrying");

        // Trigger host cancellation
        hostAppLifeTime.StopApplication();
        await Task.Delay(100); // allow some time for cancellation to propagate

        // Expect "Job was canceled" message next
        var cancellationMessageTask = CommunicationChannel.Reader.ReadAsync(CancellationToken.None).AsTask();
        if (await Task.WhenAny(cancellationMessageTask, Task.Delay(5000)) == cancellationMessageTask)
        {
            var message = await cancellationMessageTask;
            message.ShouldBe("Job was canceled");
        }
        else
        {
            throw new TimeoutException("Timeout waiting for the job cancellation confirmation.");
        }

        CancelRetryingJob.Success.ShouldBeFalse();
        CancelRetryingJob.AttemptCount.ShouldBe(1);
    }

    private sealed class MaxFailuresWrapper(int maxFailuresBeforeSuccess = 3)
    {
        public int MaxFailuresBeforeSuccess { get; set; } = maxFailuresBeforeSuccess;
    }

    [RetryPolicy(retryCount: 4, PolicyType.ExponentialBackoff)]
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

    [RetryPolicy(retryCount: 4, PolicyType.FixedInterval)]
    private sealed class CancelRetryingJob2 : IJob
    {
        public Task RunAsync(JobExecutionContext context, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            while (!token.IsCancellationRequested)
            {
                throw new InvalidOperationException("Job Failed");
            }

            return Task.CompletedTask;
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
