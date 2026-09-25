using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace NCronJob.Tests;

public sealed class JobRegistrationValidationTests : JobIntegrationBase
{
    private static readonly Delegate JobDelegate = () => { };

    [Fact]
    public void CanAddUntypedJobsWithTheSameDelegateWithDifferentNames()
    {
        Action act = () => ServiceCollection.AddNCronJob(
            n => n.AddJob(UntypedJob, Cron.AtEveryMinute)
                .AddJob(UntypedJob, Cron.AtEveryMinute, jobName: "one")
                .AddJob(UntypedJob, Cron.AtEveryMinute, jobName: "another"));

        act.ShouldNotThrow();
    }

    [Fact]
    public void AddJobsDynamicallyWhenNameIsDuplicatedLeadsToException()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob(() => { }, Cron.AtEveryMinute, jobName: "Job1"));

        var runtimeRegistry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var successful = runtimeRegistry.TryRegister(n => n.AddJob(() => { }, Cron.AtEveryMinute, jobName: "Job1"), out var exception);

        successful.ShouldBeFalse();
        exception.ShouldNotBeNull();
    }

    [Fact]
    public async Task TwoJobsWithDifferentDefinitionLeadToTwoExecutions()
    {
        ServiceCollection.AddNCronJob(n => n
            .AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithParameter("1"))
            .AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithParameter("2")));

        await StartNCronJob();

        var countJobs = ServiceProvider.GetRequiredService<JobRegistry>().GetAllCronJobs().Count;
        countJobs.ShouldBe(2);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForNthOrchestrationState(
            ExecutionState.OrchestrationCompleted,
            2);

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: 1");
        Storage.Entries[1].ShouldBe("DummyJob - Parameter: 2");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    [SuppressMessage("Usage", "CA2263: Prefer generic overload", Justification = "Needed for the test")]
    public async Task AddJobWithTypeAsParameterAddsJobs()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob(typeof(DummyJob), p => p.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob();

        var orchestrationId = Events[0].CorrelationId;

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForOrchestrationCompletion(orchestrationId);

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(typeof(IList<>))]
    [InlineData(typeof(string[]))]
    [InlineData(typeof(GuidGenerator))]
    [SuppressMessage("Usage", "CA2263: Prefer generic overload", Justification = "Needed for the test")]
    public void AddJobWithTypeDoesNotSupportAnyRandomTypes(Type type)
    {
        Action act = () => ServiceCollection.AddNCronJob(n => n.AddJob(type, p => p.WithCronExpression(Cron.AtEveryMinute)));

        act.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public async Task AddingRuntimeJobsWillNotCauseDuplicatedExecution()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>().AddJob<AnotherDummyJob>());

        await StartNCronJob();

        Events.Count.ShouldBe(0);

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.TryRegister(n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));
        registry.TryRegister(n => n.AddJob<AnotherDummyJob>(p => p.WithCronExpression("0 0 10 * *")));

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForNthOrchestrationState(
            ExecutionState.OrchestrationCompleted,
            1);

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CallingAddNCronJobMultipleTimesWillRegisterAllJobs()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));
        ServiceCollection.AddNCronJob(n => n.AddJob<AnotherDummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForNthOrchestrationState(
            ExecutionState.OrchestrationCompleted,
            2);

        Storage.Entries.ShouldContain("DummyJob - Parameter: ");
        Storage.Entries.ShouldContain("AnotherDummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task PassedInExpressionShouldBePassedOn()
    {
        const string expression = "     0 0 1 * *";
        ServiceCollection.AddNCronJob(n => n
            .AddJob(() => { }, expression, jobName: "job1")
            .AddJob<DummyJob>(p => p.WithName("job2").WithCronExpression(expression)));
        await StartNCronJob();

        var runtimeJobRegistry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var jobs = runtimeJobRegistry.GetAllRecurringJobs();

        jobs.First().CronExpression.ShouldBe(expression);
        jobs.Last().CronExpression.ShouldBe(expression);

        runtimeJobRegistry.TryGetSchedule("job1", out var cron1, out _).ShouldBeTrue();
        cron1.ShouldBe(expression);

        runtimeJobRegistry.TryGetSchedule("job2", out var cron2, out _).ShouldBeTrue();
        cron2.ShouldBe(expression);
    }

    [Theory]
    [MemberData(nameof(InvalidRegistrations))]
    public void CanDetectInvalidRegistrations(Action<NCronJobOptionBuilder> register)
    {
        var act = () => ServiceCollection.AddNCronJob(register);

        act.ShouldThrow<InvalidOperationException>();
    }

    [Theory]
    [MemberData(nameof(ValidRegistrations))]
    public void SupportsValidRegistrations(Action<NCronJobOptionBuilder> register)
    {
        var act = () => ServiceCollection.AddNCronJob(register);

        act.ShouldNotThrow();
    }

    public static TheoryData<Action<NCronJobOptionBuilder>> InvalidRegistrations = new()
    {
        {
            // Names should be unique
            s => s.AddJob<DummyJob>(p => p
                    .WithName("JobName")
                    .And
                    .WithName("JobName"))
        },
        {
            // Names should be unique
            n => n.AddJob(() => { }, Cron.AtEveryMinute, jobName: "JobName")
                  .AddJob(() => { }, Cron.AtMinute0, jobName: "JobName")
        },
        {
            // Pure duplicate registration
            s =>
            {
                s.AddJob<DummyJob>();
                s.AddJob<DummyJob>();
            }
        },
        {
            // Pure duplicate registration
            s =>
            {
                s.AddJob(JobDelegate, Cron.AtEveryMinute);
                s.AddJob(JobDelegate, Cron.AtEveryMinute);
            }
        },
        {
            // Pure duplicate registration
            s => s.AddJob<DummyJob>(p => p
                    .WithCronExpression(Cron.AtEveryMinute)
                    .And
                    .WithCronExpression(Cron.AtEveryMinute))
        },
        {
            // No way to invoke DummyJob inambiguously
            s => s.AddJob<DummyJob>(p => p
                    .WithParameter("one")
                    .And
                    .WithParameter("two"))
        },
        {
            // Pure duplicate registration
            s => s.AddJob<DummyJob>(p => p
                    .WithParameter("one")
                    .And
                    .WithParameter("one")).RunAtStartup()
        },
        {
            // Duplicate registration as a startup job
            s => s.AddJob<DummyJob>(p => p
                    .WithName("JobName").RunAtStartup()).RunAtStartup()
        },
        {
            // Duplicate registration as a startup job
            s => s.AddJob<DummyJob>(p => p
                    .WithName("JobName")
                    .WithCronExpression(Cron.AtEveryMinute)
                    .RunAtStartup()).RunAtStartup()
        },
        {
            // Duplicate registration as a startup job
            s => s.AddJob<DummyJob>(p => p
                    .WithParameter("one").RunAtStartup()).RunAtStartup()
        },
        {
            // Duplicate registration as a startup job
            s => s.AddJob<DummyJob>(p => p.RunAtStartup()).RunAtStartup()
        },
    };

    public static TheoryData<Action<NCronJobOptionBuilder>> ValidRegistrations = new()
    {
        {
            s => s.AddJob<DummyJob>(p => p
                    .WithCronExpression(Cron.AtEveryJanuaryTheFirst, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"))
                    .And
                    .WithCronExpression(Cron.AtEveryJanuaryTheFirst, TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time")))
        },
        {
            s => s.AddJob<DummyJob>(p => p
                    .WithCronExpression(Cron.AtEveryMinute).WithParameter("one")
                    .And
                    .WithCronExpression(Cron.AtEveryMinute).WithParameter("two"))
        },
        {
            s => s.AddJob<DummyJob>(p => p
                    .WithParameter("one"))
        },
        {
            s => s.AddJob<DummyJob>(p => p
                    .WithParameter("one")
                    .And
                    .WithParameter("two")).RunAtStartup()
        },
        {
            s => s.AddJob<DummyJob>(p => p
                    .WithParameter("one").RunAtStartup()
                    .And
                    .WithParameter("two").RunAtStartup())
        },
        {
            s => {
                s.AddJob<DummyJob>(p => p.WithName("Job1").WithCronExpression(Cron.AtEveryMinute))
                    .ExecuteWhen(success: s => s.RunJob<AnotherDummyJob>());
                s.AddJob<DummyJob>(p => p.WithName("Job2").WithCronExpression(Cron.AtEveryMinute))
                    .ExecuteWhen(success: s => s.RunJob<LongRunningJob>());
            }
        },
    };
}
