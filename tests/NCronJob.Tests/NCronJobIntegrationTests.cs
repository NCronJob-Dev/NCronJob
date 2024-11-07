using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace NCronJob.Tests;

public sealed class NCronJobIntegrationTests : JobIntegrationBase
{
    [Fact]
    public async Task CronJobThatIsScheduledEveryMinuteShouldBeExecuted()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(p => p.WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task AdvancingTheWholeTimeShouldHaveTenEntries()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(p => p.WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        void AdvanceTime() => FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(10, AdvanceTime);

        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task JobsShouldCancelOnCancellation()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(p => p.WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var jobFinished = await DoNotWaitJustCancel(10);
        jobFinished.ShouldBeFalse();
    }

    [Fact]
    public async Task EachJobRunHasItsOwnScope()
    {
        var storage = new Storage();
        ServiceCollection.AddSingleton(storage);
        ServiceCollection.AddScoped<GuidGenerator>();
        ServiceCollection.AddNCronJob(n => n.AddJob<ScopedServiceJob>(
            p => p.WithCronExpression("* * * * *").WithParameter("null")
                .And
                .WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await Task.WhenAll(GetCompletionJobs(2));
        storage.Guids.Count.ShouldBe(2);
        storage.Guids.Distinct().Count().ShouldBe(storage.Guids.Count);
    }

    [Fact]
    public async Task ExecuteAnInstantJob()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>());
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        provider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<SimpleJob>();

        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task InstantJobShouldInheritInitiallyDefinedParameter()
    {
        ServiceCollection.AddNCronJob(
            n => n.AddJob<ParameterJob>(o => o.WithCronExpression("* * 31 2 *").WithParameter("Hello from AddNCronJob")));

        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        provider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<ParameterJob>();

        var content = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        content.ShouldBe("Hello from AddNCronJob");
    }

    [Fact]
    public async Task InstantJobCanOverrideInitiallyDefinedParameter()
    {
        ServiceCollection.AddNCronJob(
            n => n.AddJob<ParameterJob>(o => o.WithCronExpression("* * 31 2 *").WithParameter("Hello from AddNCronJob")));

        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        provider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<ParameterJob>("Hello from InstantJob");

        var content = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        content.ShouldBe("Hello from InstantJob");
    }

    [Fact]
    public async Task InstantJobShouldPassDownParameter()
    {
        ServiceCollection.AddNCronJob(
            n => n.AddJob<ParameterJob>());

        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        provider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<ParameterJob>("Hello from InstantJob");

        var content = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        content.ShouldBe("Hello from InstantJob");
    }

    [Fact]
    public async Task ShouldThrowRuntimeExceptionWhenForceRunningAnAmbiguousTypeReference()
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob<ParameterJob>(s => s.WithCronExpression("* * 30 2 *"));
            n.AddJob<ParameterJob>(s => s.WithCronExpression("* * 31 2 *"));
        });

        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var instantJobRegistry = provider.GetRequiredService<IInstantJobRegistry>();

        Action act = () => instantJobRegistry.ForceRunInstantJob<ParameterJob>("something", CancellationToken);

        act.ShouldThrow<InvalidOperationException>()
            .Message.ShouldContain($"Ambiguous job reference for type 'ParameterJob' detected.");
    }

    [Fact]
    public async Task CronJobShouldInheritInitiallyDefinedParameter()
    {
        ServiceCollection.AddNCronJob(
            n => n.AddJob<ParameterJob>(p => p.WithCronExpression("* * * * *").WithParameter("Hello from AddNCronJob")));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var content = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        content.ShouldBe("Hello from AddNCronJob");
    }

    [Fact]
    public async Task CronJobThatIsScheduledEverySecondShouldBeExecuted()
    {
        FakeTimer.Advance(TimeSpan.FromSeconds(1));
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(p => p.WithCronExpression("* * * * * *")));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        void AdvanceTime() => FakeTimer.Advance(TimeSpan.FromSeconds(1));
        var jobFinished = await WaitForJobsOrTimeout(10, AdvanceTime);

        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task CanRunSecondPrecisionAndMinutePrecisionJobs()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(
            p => p.WithCronExpression("* * * * * *").And.WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        void AdvanceTime() => FakeTimer.Advance(TimeSpan.FromSeconds(1));
        var jobFinished = await WaitForJobsOrTimeout(61, AdvanceTime);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task LongRunningJobShouldNotBlockScheduler()
    {
        ServiceCollection.AddNCronJob(n => n
                .AddJob<LongRunningJob>(p => p.WithCronExpression("* * * * *"))
                .AddJob<SimpleJob>(p => p.WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
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
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>());
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        provider.GetRequiredService<IInstantJobRegistry>().RunScheduledJob<SimpleJob>(TimeSpan.FromMinutes(1));

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAScheduledJobWithDateTimeOffset()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>());
        var provider = CreateServiceProvider();
        var runDate = FakeTimer.GetUtcNow().AddMinutes(1);
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        provider.GetRequiredService<IInstantJobRegistry>().RunScheduledJob<SimpleJob>(runDate);

        await Task.Delay(10);
        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task WhileAwaitingJobTriggeringInstantJobShouldAnywayTriggerCronJob()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(p => p.WithCronExpression("0 * * * *")));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        provider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<SimpleJob>();
        FakeTimer.Advance(TimeSpan.FromMilliseconds(1));
        (await WaitForJobsOrTimeout(1)).ShouldBeTrue();
        FakeTimer.Advance(TimeSpan.FromHours(1));
        (await WaitForJobsOrTimeout(1)).ShouldBeTrue();
    }

    [Fact]
    public async Task MinimalJobApiCanBeUsedForTriggeringCronJobs()
    {
        ServiceCollection.AddNCronJob(async (ChannelWriter<object?> writer) =>
        {
            await writer.WriteAsync(null);
        }, "* * * * *");
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task ConcurrentJobConfigurationShouldBeRespected()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<ShortRunningJob>(p => p
            .WithCronExpression("* * * * *").WithName("Job 1")
            .And.WithCronExpression("* * * * *").WithName("Job 2")
            .And.WithCronExpression("* * * * *").WithName("Job 3")
            .And.WithCronExpression("* * * * *").WithName("Job 4")));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        // Wait 2 instances at the same time
        (await WaitForJobsOrTimeout(2, TimeSpan.FromMilliseconds(150))).ShouldBeTrue();
        // But not another instance
        (await WaitForJobsOrTimeout(1, TimeSpan.FromMilliseconds(50))).ShouldBeFalse();
    }

    [Fact]
    public async Task InstantJobHasHigherPriorityThanCronJob()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<ParameterJob>(p => p.WithCronExpression("* * * * *").WithParameter("CRON")));
        ServiceCollection.AddSingleton(_ => new ConcurrencySettings { MaxDegreeOfParallelism = 1 });
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        provider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<ParameterJob>("INSTANT");
        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var answer = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        answer.ShouldBe("INSTANT");
    }

    [Fact]
    public async Task TriggeringInstantJobWithoutRegisteringContinuesToWork()
    {
        ServiceCollection.AddNCronJob();
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        Action act = () => provider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<SimpleJob>(CancellationToken);

        act.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public async Task ExecuteAnInstantJobDelegate()
    {
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
        ServiceCollection.AddNCronJob(n => n.AddJob(async (ChannelWriter<object> writer, CancellationToken ct) =>
        {
            await Task.Delay(10, ct);
            await writer.WriteAsync(true, ct);
        }, "* * * * *"));
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        await WaitForJobsOrTimeout(1);

        void AdvanceTime() => FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1, AdvanceTime);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task StaticAnonymousJobsCanBeExecutedMultipleTimes()
    {

        ServiceCollection.AddNCronJob(n => n.AddJob(JobMethods.WriteTrueStaticAsync, "* * * * *"));

        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        await WaitForJobsOrTimeout(1);

        void AdvanceTime() => FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1, AdvanceTime);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public void AddingJobsWithTheSameCustomNameLeadsToException()
    {
        Action act = () => ServiceCollection.AddNCronJob(
            n => n.AddJob(() => { }, "* * * * *", jobName: "Job1")
                .AddJob(() => { }, "0 * * * *", jobName: "Job1"));

        act.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void AddJobsDynamicallyWhenNameIsDuplicatedLeadsToException()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob(() => { }, "* * * * *", jobName: "Job1"));
        var provider = CreateServiceProvider();
        var runtimeRegistry = provider.GetRequiredService<IRuntimeJobRegistry>();

        var successful = runtimeRegistry.TryRegister(n => n.AddJob(() => { }, "* * * * *", jobName: "Job1"), out var exception);

        successful.ShouldBeFalse();
        exception.ShouldNotBeNull();
    }

    [Fact]
    public async Task TwoJobsWithDifferentDefinitionLeadToTwoExecutions()
    {
        ServiceCollection.AddNCronJob(n => n
            .AddJob<SimpleJob>(p => p.WithCronExpression("* * * * *").WithParameter("1"))
            .AddJob<SimpleJob>(p => p.WithCronExpression("* * * * *").WithParameter("2")));
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var countJobs = provider.GetRequiredService<JobRegistry>().GetAllCronJobs().Count;
        countJobs.ShouldBe(2);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(2, TimeSpan.FromMilliseconds(250));
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    [SuppressMessage("Usage", "CA2263: Prefer generic overload", Justification = "Needed for the test")]
    public async Task AddJobWithTypeAsParameterAddsJobs()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob(typeof(SimpleJob), p => p.WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task AddingJobsAndDuringStartupAndRuntimeNotInOnlyOneCallOnlyOneExecution()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>().AddJob<ShortRunningJob>());
        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
        var registry = provider.GetRequiredService<IRuntimeJobRegistry>();

        registry.TryRegister(n => n.AddJob<SimpleJob>(p => p.WithCronExpression("* * * * *")));
        registry.TryRegister(n => n.AddJob<ShortRunningJob>(p => p.WithCronExpression("0 0 10 * *")));

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
        var anotherJobFinished = await WaitForJobsOrTimeout(1, TimeSpan.FromMilliseconds(500));
        anotherJobFinished.ShouldBeFalse();
    }

    [Fact]
    public async Task CallingAddNCronJobMultipleTimesWillRegisterAllJobs()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<ShortRunningJob>(p => p.WithCronExpression("* * * * *")));
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(p => p.WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(2);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public void RegisteringDuplicatedJobsLeadToAnExceptionWhenRegistration()
    {
        Action act = () =>
            ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(p => p
                .WithCronExpression("* * * * *")
                .And
                .WithCronExpression("* * * * *")));

        act.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void RegisteringDuplicateDuringRuntimeLeadsToException()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(p => p.WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();
        var runtimeRegistry = provider.GetRequiredService<IRuntimeJobRegistry>();

        var successful = runtimeRegistry.TryRegister(n => n.AddJob<SimpleJob>(p => p.WithCronExpression("* * * * *")), out var exception);

        successful.ShouldBeFalse();
        exception.ShouldNotBeNull();
    }

    private static class JobMethods
    {
        public static async Task WriteTrueStaticAsync(ChannelWriter<object> writer, CancellationToken ct)
        {
            await Task.Delay(10, ct);
            await writer.WriteAsync(true, ct);
        }
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
        public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            try
            {
                context.Output = "Job Completed";
                await Task.Delay(10, token);
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
        public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
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
        public async Task RunAsync(IJobExecutionContext context, CancellationToken token) =>
            await Task.Delay(TimeSpan.FromSeconds(10), timeProvider, token);
    }

    private sealed class ScopedServiceJob(ChannelWriter<object> writer, Storage storage, GuidGenerator guidGenerator) : IJob
    {
        public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            storage.Guids.Add(guidGenerator.NewGuid);
            await writer.WriteAsync(true, token);
        }
    }

    private sealed class ParameterJob(ChannelWriter<object> writer) : IJob
    {
        public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
            => await writer.WriteAsync(context.Parameter!, token);
    }

    private sealed class JobWithDependency(ChannelWriter<object> writer, GuidGenerator guidGenerator) : IJob
    {
        public async Task RunAsync(IJobExecutionContext context, CancellationToken token)
            => await writer.WriteAsync(guidGenerator.NewGuid, token);
    }
}
