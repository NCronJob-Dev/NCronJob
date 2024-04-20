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

        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();

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

        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();

        // Validate that the job was retried the correct number of times
        // Fail 3 times = 3 retries + 1 success
        var attempts = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        attempts.ShouldBe(4);
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


    [RetryPolicy<MyCustomPolicyCreator>(3, 2)]
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
