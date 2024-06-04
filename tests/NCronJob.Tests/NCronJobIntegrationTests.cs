using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace NCronJob.Tests;

public sealed class NCronJobIntegrationTests : JobIntegrationBase
{
    [Fact]
    public async Task CronJobThatIsScheduledEveryMinuteShouldBeExecuted()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(p => p.WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task AdvancingTheWholeTimeShouldHaveTenEntries()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(p => p.WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        void AdvanceTime() => fakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(10, AdvanceTime);

        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task JobsShouldCancelOnCancellation()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(p => p.WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var jobFinished = await DoNotWaitJustCancel(10);
        jobFinished.ShouldBeFalse();
    }

    [Fact]
    public async Task EachJobRunHasItsOwnScope()
    {
        var fakeTimer = new FakeTimeProvider();
        var storage = new Storage();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddSingleton(storage);
        ServiceCollection.AddScoped<GuidGenerator>();
        ServiceCollection.AddNCronJob(n => n.AddJob<ScopedServiceJob>(
            p => p.WithCronExpression("* * * * *").And.WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));

        await Task.WhenAll(GetCompletionJobs(2));
        storage.Guids.Count.ShouldBe(2);
        storage.Guids.Distinct().Count().ShouldBe(storage.Guids.Count);
    }

    [Fact]
    public async Task ExecuteAnInstantJob()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>());
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        provider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<SimpleJob>();

        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task CronJobShouldPassDownParameter()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(n => n.AddJob<ParameterJob>(p => p.WithCronExpression("* * * * *").WithParameter("Hello World")));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));

        var content = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        content.ShouldBe("Hello World");
    }

    [Fact]
    public async Task InstantJobShouldGetParameter()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(n => n.AddJob<ParameterJob>());
        var provider = CreateServiceProvider();
        provider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<ParameterJob>("Hello World");

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var content = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        content.ShouldBe("Hello World");
    }

    [Fact]
    public async Task CronJobThatIsScheduledEverySecondShouldBeExecuted()
    {
        var fakeTimer = new FakeTimeProvider();
        fakeTimer.Advance(TimeSpan.FromSeconds(1));
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(p => p.WithCronExpression("* * * * * *", true)));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        void AdvanceTime() => fakeTimer.Advance(TimeSpan.FromSeconds(1));
        var jobFinished = await WaitForJobsOrTimeout(10, AdvanceTime);

        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task CanRunSecondPrecisionAndMinutePrecisionJobs()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(
            p => p.WithCronExpression("* * * * * *", true).And.WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        void AdvanceTime() => fakeTimer.Advance(TimeSpan.FromSeconds(1));
        var jobFinished = await WaitForJobsOrTimeout(61, AdvanceTime);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task LongRunningJobShouldNotBlockScheduler()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(n => n
                .AddJob<LongRunningJob>(p => p.WithCronExpression("* * * * *"))
                .AddJob<SimpleJob>(p => p.WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task ThrowIfJobWithDependenciesIsNotRegistered()
    {
        ServiceCollection
            .AddNCronJob(n => n.AddJob<JobWithDependency>(p => p.WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            using var serviceScope = provider.CreateScope();
            using var executor = serviceScope.ServiceProvider.GetRequiredService<JobExecutor>();
            var jobDefinition = new JobDefinition(typeof(JobWithDependency), null, null, null);
            await executor.RunJob(JobRun.Create(jobDefinition), CancellationToken.None);
        });
    }

    [Fact]
    public async Task ExecuteAScheduledJob()
    {
        var fakeTimer = new FakeTimeProvider { AutoAdvanceAmount = TimeSpan.FromMinutes(1) };
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>());
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        provider.GetRequiredService<IInstantJobRegistry>().RunScheduledJob<SimpleJob>(TimeSpan.FromMinutes(1));

        await Task.Delay(10);
        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAScheduledJobWithDateTimeOffset()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>());
        var provider = CreateServiceProvider();
        var runDate = fakeTimer.GetUtcNow().AddMinutes(1);
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        provider.GetRequiredService<IInstantJobRegistry>().RunScheduledJob<SimpleJob>(runDate);

        await Task.Delay(10);
        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task WhileAwaitingJobTriggeringInstantJobShouldAnywayTriggerCronJob()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(p => p.WithCronExpression("0 * * * *")));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        provider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<SimpleJob>();
        fakeTimer.Advance(TimeSpan.FromMilliseconds(1));
        (await WaitForJobsOrTimeout(1)).ShouldBeTrue();
        fakeTimer.Advance(TimeSpan.FromHours(1));
        (await WaitForJobsOrTimeout(1)).ShouldBeTrue();
    }

    [Fact]
    public async Task MinimalJobApiCanBeUsedForTriggeringCronJobs()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(async (ChannelWriter<object?> writer) =>
        {
            await writer.WriteAsync(null);
        }, "* * * * *");
        ServiceCollection.AddNCronJob(async (ChannelWriter<object?> writer) =>
        {
            await writer.WriteAsync(null);
        }, "* * * * *");
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(2);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task ConcurrentJobConfigurationShouldBeRespected()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(n => n.AddJob<ShortRunningJob>(p => p
            .WithCronExpression("* * * * *")
            .And.WithCronExpression("* * * * *")
            .And.WithCronExpression("* * * * *")
            .And.WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        // Wait 2 instances at the same time
        (await WaitForJobsOrTimeout(2, TimeSpan.FromMilliseconds(150))).ShouldBeTrue();
        // But not another instance
        (await WaitForJobsOrTimeout(1, TimeSpan.FromMilliseconds(50))).ShouldBeFalse();
    }

    [Fact]
    public async Task InstantJobHasHigherPriorityThanCronJob()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(n => n.AddJob<ParameterJob>(p => p.WithCronExpression("* * * * *").WithParameter("CRON")));
        ServiceCollection.AddSingleton(_ => new ConcurrencySettings { MaxDegreeOfParallelism = 1 });
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        provider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<ParameterJob>("INSTANT");
        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        var answer = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        answer.ShouldBe("INSTANT");
    }

    [Fact]
    public async Task TriggeringInstantJobWithoutRegisteringThrowsException()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob();
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        Action act = () => provider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<SimpleJob>();

        act.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public async Task ExecuteAnInstantJobDelegate()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>());
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        provider.GetRequiredService<IInstantJobRegistry>().RunInstantJob(async (ChannelWriter<object> writer) =>
        {
            await writer.WriteAsync("Done");
        });

        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task AnonymousJobsCanBeExecutedMultipleTimes()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(n => n.AddJob(async (ChannelWriter<object> writer) => await writer.WriteAsync(true), "* * * * *"));
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        await WaitForJobsOrTimeout(1);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));

        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public void AddingJobsWithTheSameCustomNameLeadsToException()
    {
        ServiceCollection.AddNCronJob(
            n => n.AddJob(() => { }, "* * * * *", jobName: "Job1")
                .AddJob(() => { }, "* * * * *", jobName: "Job1"));
        var provider = CreateServiceProvider();

        Action act = () => provider.GetRequiredService<JobRegistry>();

        act.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void AddJobsDynamicallyWhenNameIsDuplicatedLeadsToException()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob(() => { }, "* * * * *", jobName: "Job1"));
        var provider = CreateServiceProvider();
        var runtimeRegistry = provider.GetRequiredService<IRuntimeJobRegistry>();

        var act = () => runtimeRegistry.AddJob(n => n.AddJob(() => { }, "* * * * *", jobName: "Job1"));

        act.ShouldThrow<InvalidOperationException>();
    }

    private sealed class GuidGenerator
    {
        public Guid NewGuid { get; } = Guid.NewGuid();
    }

    private sealed class Storage
    {
        public ConcurrentBag<Guid> Guids { get; } = [];
    }

    [SupportsConcurrency(2)]
    private sealed class SimpleJob(ChannelWriter<object> writer) : IJob
    {
        public async Task RunAsync(JobExecutionContext context, CancellationToken token)
        {
            try
            {
                context.Output = "Job Completed";
                await writer.WriteAsync(context.Output, token);
            }
            catch (Exception ex)
            {
                await writer.WriteAsync(ex, token);
            }
        }
    }

    [SupportsConcurrency(2)]
    private sealed class ShortRunningJob(ChannelWriter<object> writer) : IJob
    {
        public async Task RunAsync(JobExecutionContext context, CancellationToken token)
        {
            try
            {
                context.Output = "Job Completed";
                await writer.WriteAsync(context.Output, token);
                await Task.Delay(200, token);
            }
            catch (Exception ex)
            {
                await writer.WriteAsync(ex, token);
            }
        }
    }

    private sealed class LongRunningJob(TimeProvider timeProvider) : IJob
    {
        public async Task RunAsync(JobExecutionContext context, CancellationToken token) =>
            await Task.Delay(TimeSpan.FromSeconds(10), timeProvider, token);
    }

    private sealed class ScopedServiceJob(ChannelWriter<object> writer, Storage storage, GuidGenerator guidGenerator) : IJob
    {
        public async Task RunAsync(JobExecutionContext context, CancellationToken token)
        {
            storage.Guids.Add(guidGenerator.NewGuid);
            await writer.WriteAsync(true, token);
        }
    }

    private sealed class ParameterJob(ChannelWriter<object> writer) : IJob
    {
        public async Task RunAsync(JobExecutionContext context, CancellationToken token)
            => await writer.WriteAsync(context.Parameter!, token);
    }

    private sealed class JobWithDependency(ChannelWriter<object> writer, GuidGenerator guidGenerator) : IJob
    {
        public async Task RunAsync(JobExecutionContext context, CancellationToken token)
            => await writer.WriteAsync(guidGenerator.NewGuid, token);
    }
}
