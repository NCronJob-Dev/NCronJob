using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace NCronJob.Tests;

public class RunDependentJobTests : JobIntegrationBase
{
    [Fact]
    public async Task WhenJobWasSuccessful_DependentJobShouldRun()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalJob>()
            .ExecuteWhen(success: s => s.RunJob<DummyJob>("Message")));

        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true, token: CancellationToken);

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        Storage.Entries[0].ShouldBe("PrincipalJob: Success");
        Storage.Entries[1].ShouldBe("DummyJob - Parameter: Message");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task WhenJobWasFailed_DependentJobShouldRun()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalJob>()
            .ExecuteWhen(faulted: s => s.RunJob<DummyJob>("Message")));

        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(false, token: CancellationToken);

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        Storage.Entries[0].ShouldBe("PrincipalJob: Failed");
        Storage.Entries[1].ShouldBe("DummyJob - Parameter: Message");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RemovingAJobShouldAlsoRemoveItsDependencies()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>()
            .ExecuteWhen(success: s => s.RunJob<AnotherDummyJob>()));

        await StartNCronJob();

        var instantJobRegistry = ServiceProvider.GetRequiredService<IInstantJobRegistry>();

        var orchestrationId = instantJobRegistry.ForceRunInstantJob<DummyJob>(token: CancellationToken);

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: ");
        Storage.Entries[1].ShouldBe("AnotherDummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(2);

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.RemoveJob<DummyJob>();
        registry.TryRegister(n => n.AddJob<DummyJob>());

        var secondRunOrchestrationId = instantJobRegistry.ForceRunInstantJob<DummyJob>(token: CancellationToken);

        await AdvanceTimeUntilOrchestrationCompletion(secondRunOrchestrationId);

        Storage.Entries[2].ShouldBe("DummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(3);
    }

    [Fact]
    public async Task CorrelationIdIsSharedByJobsAndTheirDependencies()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalCorrelationIdJob>()
            .ExecuteWhen(success: s => s.RunJob<DependentCorrelationIdJob>()));

        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalCorrelationIdJob>(token: CancellationToken);

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        Storage.Entries.Distinct().Count().ShouldBe(1);
        Storage.Entries[0].ShouldBe(orchestrationId.ToString());
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task SkipChildrenShouldPreventDependentJobsFromRunning()
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob<PrincipalCorrelationIdJob>()
                .ExecuteWhen(success: s => s.RunJob<DependentCorrelationIdJob>())
                .ExecuteWhen(success: s => s.RunJob((Storage storage) => storage.Add("1")));
        });

        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<PrincipalCorrelationIdJob>(parameter: true, token: CancellationToken);

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        Storage.Entries.Count.ShouldBe(1);

        var principalJobRun = Events[1];
        var dependentJobEvents = Events.Skip(6).Take(4).ToList();

        Events[0].State.ShouldBe(ExecutionState.OrchestrationStarted);
        Events[1].State.ShouldBe(ExecutionState.NotStarted);
        Events[2].State.ShouldBe(ExecutionState.Initializing);
        Events[3].State.ShouldBe(ExecutionState.Running);
        Events[4].State.ShouldBe(ExecutionState.Completing);
        Events[5].State.ShouldBe(ExecutionState.WaitingForDependency);

        dependentJobEvents[0].State.ShouldBe(ExecutionState.NotStarted);
        dependentJobEvents[1].State.ShouldBe(ExecutionState.Skipped);
        dependentJobEvents[2].State.ShouldBe(ExecutionState.NotStarted);
        dependentJobEvents[3].State.ShouldBe(ExecutionState.Skipped);

        principalJobRun.RunId.ShouldBe(dependentJobEvents[0].ParentRunId);
        principalJobRun.RunId.ShouldBe(dependentJobEvents[1].ParentRunId);
        principalJobRun.RunId.ShouldBe(dependentJobEvents[2].ParentRunId);
        principalJobRun.RunId.ShouldBe(dependentJobEvents[3].ParentRunId);
        dependentJobEvents[0].RunId.ShouldNotBe(dependentJobEvents[2].RunId);

        Events[10].State.ShouldBe(ExecutionState.Completed);
        Events[11].State.ShouldBe(ExecutionState.OrchestrationCompleted);
        Events.Count.ShouldBe(12);
        Events.ShouldAllBe(e => e.CorrelationId == orchestrationId);
    }

    [Fact]
    public async Task WhenJobWasSuccessful_DependentAnonymousJobShouldRun()
    {
        Func<Storage, IJobExecutionContext, Task> execution = (storage, context) =>
        {
            storage.Add($"Parent: {context.ParentOutput}");
            return Task.CompletedTask;
        };

        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalJob>()
            .ExecuteWhen(success: s => s.RunJob(execution)));

        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true, token: CancellationToken);

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        Storage.Entries[0].ShouldBe("PrincipalJob: Success");
        Storage.Entries[1].ShouldBe("Parent: Success");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task CanBuildAChainOfDependentJobsThatRunAfterOneJob()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalJob>()
            .ExecuteWhen(success: s => s.RunJob<DummyJob>("1").RunJob<DummyJob>("2"))
            .ExecuteWhen(success: s => s.RunJob<DummyJob>("3")));

        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true, token: CancellationToken);

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        Storage.Entries[0].ShouldBe("PrincipalJob: Success");
        Storage.Entries[1].ShouldBe("DummyJob - Parameter: 1");
        Storage.Entries[2].ShouldBe("DummyJob - Parameter: 2");
        Storage.Entries[3].ShouldBe("DummyJob - Parameter: 3");
        Storage.Entries.Count.ShouldBe(4);
    }

    [Fact]
    public async Task CanTriggerAChainOfDependentJobs()
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob<PrincipalJob>().ExecuteWhen(success: s => s.RunJob<DummyJob>());
            n.AddJob<DummyJob>().ExecuteWhen(success: s => s.RunJob<AnotherDummyJob>());
        });

        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true, token: CancellationToken);

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        Storage.Entries[0].ShouldBe("PrincipalJob: Success");
        Storage.Entries[1].ShouldBe("DummyJob - Parameter: ");
        Storage.Entries[2].ShouldBe("AnotherDummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(3);

        Events[0].State.ShouldBe(ExecutionState.OrchestrationStarted);
        Events[17].State.ShouldBe(ExecutionState.Completed);
        Events[18].State.ShouldBe(ExecutionState.OrchestrationCompleted);
        Events.Count.ShouldBe(19);
        Events.ShouldAllBe(e => e.CorrelationId == orchestrationId);
    }

    [Fact]
    public async Task CanBuildAScheduledChainOfDependentJobs()
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob<PrincipalJob>(o => o.WithCronExpression(Cron.AtEveryMinute).WithParameter(true))
                .ExecuteWhen(success: s => s.RunJob<DummyJob>());
            n.AddJob<DummyJob>(o => o.WithCronExpression(Cron.Never))
                .ExecuteWhen(success: s => s.RunJob<AnotherDummyJob>());
        });

        await StartNCronJob();

        await AdvanceTimeUntilOrchestrationCompletion(Events[0].CorrelationId);

        Storage.Entries[0].ShouldBe("PrincipalJob: Success");
        Storage.Entries[1].ShouldBe("DummyJob - Parameter: ");
        Storage.Entries[2].ShouldBe("AnotherDummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(3);
    }

    [Fact]
    public async Task ConfiguringDifferentDependentJobsForSchedulesShouldResultInIndependentRuns()
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob<PrincipalJob>(s => s.WithCronExpression("1 0 1 * *").WithParameter(true))
                .ExecuteWhen(s => s.RunJob((Storage storage) => storage.Add("1")));
            n.AddJob<PrincipalJob>(s => s.WithCronExpression("1 0 2 * *").WithParameter(true))
                .ExecuteWhen(s => s.RunJob((Storage storage) => storage.Add("2")));
        });

        await StartNCronJob();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var firstOrchestrationId = Events[0].CorrelationId;

        await AdvanceTimeUntilOrchestrationCompletion(firstOrchestrationId);

        Storage.Entries.ShouldContain("PrincipalJob: Success");
        Storage.Entries.ShouldContain("1");

        FakeTimer.Advance(TimeSpan.FromDays(1));

        await WaitForNthOrchestrationState(ExecutionState.OrchestrationCompleted, 2);

        Storage.Entries[2].ShouldBe("PrincipalJob: Success");
        Storage.Entries[3].ShouldBe("2");
        Storage.Entries.Count.ShouldBe(4);
    }

    [Fact]
    public async Task SameJobOnDifferentSchedulesWithoutParameterRunsOnlyItsOwnDependents()
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob<DummyJob>(s => s.WithCronExpression("1 0 1 * *"))
                .ExecuteWhen(s => s.RunJob((Storage storage) => storage.Add("1")));
            n.AddJob<DummyJob>(s => s.WithCronExpression("1 0 2 * *"))
                .ExecuteWhen(s => s.RunJob((Storage storage) => storage.Add("2")));
        });

        await StartNCronJob();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await AdvanceTimeUntilOrchestrationCompletion(Events[0].CorrelationId);

        Storage.Entries.ShouldBe(["DummyJob - Parameter: ", "1"], ignoreOrder: true);
    }

    [Fact]
    public async Task UpdatingTheParameterKeepsDependentJobs()
    {
        ServiceCollection.AddNCronJob(n => n
            .AddJob<DummyJob>(p => p.WithCronExpression(Cron.Never).WithName("Root"))
            .ExecuteWhen(success: s => s.RunJob<AnotherDummyJob>()));

        await StartNCronJob();

        ServiceProvider.GetRequiredService<IRuntimeJobRegistry>().UpdateParameter("Root", "updated");

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob("Root", token: CancellationToken);

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        Storage.Entries.ShouldBe(["DummyJob - Parameter: updated", "AnotherDummyJob - Parameter: "]);
    }

    [Fact]
    public async Task NamedJobInAChainRunsItsOwnDependentJobs()
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob<PrincipalJob>().ExecuteWhen(success: s => s.RunJob<DummyJob>());
            n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.Never).WithName("Dummy"))
                .ExecuteWhen(success: s => s.RunJob<AnotherDummyJob>());
        });

        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true, token: CancellationToken);

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        Storage.Entries.ShouldBe(["PrincipalJob: Success", "DummyJob - Parameter: ", "AnotherDummyJob - Parameter: "]);
    }

    [Fact]
    public void RegistrationsOfTheSameDependentJobWithAndWithoutDependentJobsAreRejected()
    {
        Action act = () => ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob<PrincipalJob>().ExecuteWhen(success: s => s.RunJob<DummyJob>());
            n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtMinute5))
                .ExecuteWhen(success: s => s.RunJob<AnotherDummyJob>());
            n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.Never));
        });

        act.ShouldThrow<InvalidOperationException>()
            .Message.ShouldContain("Ambiguous dependent job chain for type 'DummyJob' detected.");
    }

    [Fact]
    public void RemovedRootDoesNotInheritDependentJobsOfAnotherRegistration()
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtMinute5).WithName("Removed"));
            n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.Never).WithName("Kept"))
                .ExecuteWhen(success: s => s.RunJob<AnotherDummyJob>());
        });

        var jobRegistry = ServiceProvider.GetRequiredService<JobRegistry>();
        var removedRoot = jobRegistry.FindRootJobDefinitionOrThrow("Removed");

        ServiceProvider.GetRequiredService<IRuntimeJobRegistry>().RemoveJob("Removed");

        jobRegistry.GetDependentSuccessJobs(removedRoot).ShouldBeEmpty();
    }

    [Fact]
    public void RegistrationsOfTheSameDependentJobWithDifferentDependentJobsAreRejected()
    {
        Action act = () => ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob<PrincipalJob>().ExecuteWhen(success: s => s.RunJob<DummyJob>());
            n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtMinute5))
                .ExecuteWhen(success: s => s.RunJob<AnotherDummyJob>());
            n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.Never))
                .ExecuteWhen(success: s => s.RunJob<ExceptionJob>());
        });

        act.ShouldThrow<InvalidOperationException>()
            .Message.ShouldContain("Ambiguous dependent job chain for type 'DummyJob' detected.");
    }

    [Fact]
    public void RuntimeRegistrationCausingAnAmbiguousDependentChainIsRolledBack()
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob<PrincipalJob>().ExecuteWhen(success: s => s.RunJob<DummyJob>());
            n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtMinute5))
                .ExecuteWhen(success: s => s.RunJob<AnotherDummyJob>());
        });

        var jobRegistry = ServiceProvider.GetRequiredService<JobRegistry>();
        var rootJobs = jobRegistry.GetAllRootJobs();

        var successful = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>().TryRegister(
            builder => ((NCronJobOptionBuilder)builder)
                .AddJob<DummyJob>(p => p.WithCronExpression(Cron.Never))
                .ExecuteWhen(success: s => s.RunJob<ExceptionJob>()),
            out var exception);

        successful.ShouldBeFalse();
        exception.ShouldBeOfType<InvalidOperationException>()
            .Message.ShouldContain("Ambiguous dependent job chain for type 'DummyJob' detected.");
        jobRegistry.GetAllRootJobs().ShouldBe(rootJobs);
    }

    [Fact]
    public async Task DependentJobWithMultipleSchedulesSharingDependentJobsIsNotAmbiguous()
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob<PrincipalJob>().ExecuteWhen(success: s => s.RunJob<DummyJob>());
            n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtMinute5).And.WithCronExpression(Cron.Never))
                .ExecuteWhen(success: s => s.RunJob<AnotherDummyJob>());
        });

        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true, token: CancellationToken);

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        Storage.Entries.ShouldBe(["PrincipalJob: Success", "DummyJob - Parameter: ", "AnotherDummyJob - Parameter: "]);
    }

    [Fact]
    public async Task WhenJobIsNotCreated_DependentFailureJobShouldRun()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<JobThatThrowsInCtor>()
            .ExecuteWhen(faulted: s => s.RunJob<DummyJob>("After Exception")));

        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<JobThatThrowsInCtor>(false, token: CancellationToken);

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: After Exception");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task DependentJobShouldHandleParametersCorrectly()
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.Never))
                .ExecuteWhen(success: s => s.RunJob<AnotherDummyJob>("overridden"));
            n.AddJob<AnotherDummyJob>(p => p.WithCronExpression(Cron.Never).WithParameter("dependent"));
        });

        await StartNCronJob();

        var instantJobRegistry = ServiceProvider.GetRequiredService<IInstantJobRegistry>();

        var rootOrchestrationId = instantJobRegistry.ForceRunInstantJob<DummyJob>(null, token: CancellationToken);
        await AdvanceTimeUntilOrchestrationCompletion(rootOrchestrationId);

        var dependentOrchestrationId = instantJobRegistry.ForceRunInstantJob<AnotherDummyJob>(token: CancellationToken);
        await AdvanceTimeUntilOrchestrationCompletion(dependentOrchestrationId);

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: ");
        Storage.Entries[1].ShouldBe("AnotherDummyJob - Parameter: overridden");
        Storage.Entries[2].ShouldBe("AnotherDummyJob - Parameter: dependent");
        Storage.Entries.Count.ShouldBe(3);
    }

    private sealed class PrincipalJob(Storage storage) : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            var maybeThrow = () => { };

            if (context.Parameter is true)
            {
                context.Output = "Success";
            }
            else
            {
                context.Output = "Failed";
                maybeThrow = () => throw new InvalidOperationException("Failed");
            }

            storage.Add($"{nameof(PrincipalJob)}: {context.Output}");

            maybeThrow();

            return Task.CompletedTask;
        }
    }

    private sealed class PrincipalCorrelationIdJob(Storage storage) : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            storage.Add(context.CorrelationId.ToString());

            if (context.Parameter is true)
            {
                context.SkipChildren();
            }

            return Task.CompletedTask;
        }
    }

    private sealed class DependentCorrelationIdJob(Storage storage) : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            storage.Add(context.CorrelationId.ToString());
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task DependentJobWithOnlyIfConditionShouldExecuteWhenTrue()
    {
        var shouldRun = true;

        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalJob>()
            .ExecuteWhen(success: s => s.RunJob<DummyJob>("Message").OnlyIf(() => shouldRun)));

        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true, token: CancellationToken);

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        Storage.Entries[0].ShouldBe("PrincipalJob: Success");
        Storage.Entries[1].ShouldBe("DummyJob - Parameter: Message");
        Storage.Entries.Count.ShouldBe(2);

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldContain(e => e.State == ExecutionState.Completed);
    }

    [Fact]
    public async Task DependentJobWithOnlyIfConditionShouldSkipWhenFalse()
    {
        var shouldRun = false;

        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalJob>()
            .ExecuteWhen(success: s => s.RunJob<DummyJob>("Message").OnlyIf(() => shouldRun)));

        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true, token: CancellationToken);

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        Storage.Entries[0].ShouldBe("PrincipalJob: Success");
        Storage.Entries.Count.ShouldBe(1); // Dependent job never executed

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldContain(e => e.State == ExecutionState.Skipped);
    }

    [Fact]
    public async Task DependentJobWithDependencyInjectionConditionShouldWork()
    {
        ServiceCollection.AddSingleton<FeatureFlagService>();

        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalJob>()
            .ExecuteWhen(success: s => s.RunJob<DummyJob>("Message")
                .OnlyIf((FeatureFlagService flags) => flags.IsEnabled("dependent-feature"))));

        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true, token: CancellationToken);

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        Storage.Entries[0].ShouldBe("PrincipalJob: Success");
        Storage.Entries[1].ShouldBe("DummyJob - Parameter: Message");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task DependentJobWithAsyncConditionShouldWork()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalJob>()
            .ExecuteWhen(success: s => s.RunJob<DummyJob>("Message")
                .OnlyIf(async () =>
                {
                    await Task.Yield();
                    return true;
                })));

        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true, token: CancellationToken);

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        Storage.Entries[0].ShouldBe("PrincipalJob: Success");
        Storage.Entries[1].ShouldBe("DummyJob - Parameter: Message");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task MultipleDependentJobsWithOnlyIfConditionsShouldWorkIndependently()
    {
        var firstShouldRun = true;
        var secondShouldRun = false;

        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalJob>()
            .ExecuteWhen(success: s => s
                .RunJob<DummyJob>("First").OnlyIf(() => firstShouldRun)
                .RunJob<AnotherDummyJob>("Second").OnlyIf(() => secondShouldRun)));

        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true, token: CancellationToken);

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        Storage.Entries[0].ShouldBe("PrincipalJob: Success");
        Storage.Entries[1].ShouldBe("DummyJob - Parameter: First");
        Storage.Entries.Count.ShouldBe(2); // Second job never executed

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        // Should have one skipped event for the second job
        filteredEvents.Count(e => e.State == ExecutionState.Skipped).ShouldBe(1);
    }

    [Fact]
    public async Task DependentJobWithMultipleOnlyIfConditionsShouldBeCombinedWithAndLogic()
    {
        var condition1 = true;
        var condition2 = true;

        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalJob>()
            .ExecuteWhen(success: s => s.RunJob<DummyJob>("Message")
                .OnlyIf(() => condition1)
                .OnlyIf(() => condition2)));

        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true, token: CancellationToken);

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        Storage.Entries[0].ShouldBe("PrincipalJob: Success");
        Storage.Entries[1].ShouldBe("DummyJob - Parameter: Message");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task DependentJobWithMultipleOnlyIfConditionsShouldSkipWhenOneReturnsFalse()
    {
        var condition1 = true;
        var condition2 = false;

        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalJob>()
            .ExecuteWhen(success: s => s.RunJob<DummyJob>("Message")
                .OnlyIf(() => condition1)
                .OnlyIf(() => condition2)));

        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true, token: CancellationToken);

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        Storage.Entries[0].ShouldBe("PrincipalJob: Success");
        Storage.Entries.Count.ShouldBe(1); // Dependent job never executed

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldContain(e => e.State == ExecutionState.Skipped);
    }

    [Fact]
    public async Task DependentAnonymousJobWithOnlyIfConditionShouldWork()
    {
        var shouldRun = true;

        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalJob>()
            .ExecuteWhen(success: s => s.RunJob((Storage storage) => storage.Add("Anonymous executed"))
                .OnlyIf(() => shouldRun)));

        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true, token: CancellationToken);

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        Storage.Entries[0].ShouldBe("PrincipalJob: Success");
        Storage.Entries[1].ShouldBe("Anonymous executed");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task DependentJobOnlyIfCanAccessCancellationToken()
    {
        var cancellationTokenPassed = false;

        ServiceCollection.AddNCronJob(n => n.AddJob<PrincipalJob>()
            .ExecuteWhen(success: s => s.RunJob<DummyJob>("Message")
                .OnlyIf((CancellationToken ct) =>
                {
                    cancellationTokenPassed = !ct.IsCancellationRequested;
                    return true;
                })));

        await StartNCronJob();

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().ForceRunInstantJob<PrincipalJob>(true, token: CancellationToken);

        await AdvanceTimeUntilOrchestrationCompletion(orchestrationId);

        cancellationTokenPassed.ShouldBeTrue();
        Storage.Entries[0].ShouldBe("PrincipalJob: Success");
        Storage.Entries[1].ShouldBe("DummyJob - Parameter: Message");
    }

    [Theory]
    [MemberData(nameof(InstantJobRunners))]
    public async Task ShouldThrowRuntimeExceptionWhenTriggeringThroughTheInstantJobRegistryAnAmbiguousTypeReference(
        Func<IInstantJobRegistry, TimeProvider, object?, CancellationToken, Guid> instantJobRunner)
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob<DummyJob>(s => s.WithCronExpression(Cron.AtMinute5))
                .ExecuteWhen(success: s => s.RunJob<AnotherDummyJob>());
            n.AddJob<DummyJob>(s => s.WithCronExpression(Cron.Never))
                .ExecuteWhen(success: s => s.RunJob<ExceptionJob>());
        });

        await StartNCronJob();

        var instantJobRegistry = ServiceProvider.GetRequiredService<IInstantJobRegistry>();

        Action act = () => instantJobRunner(instantJobRegistry, FakeTimer, "Hello from InstantJob", CancellationToken);

        act.ShouldThrow<InvalidOperationException>()
            .Message.ShouldContain("Ambiguous job reference for type 'DummyJob' detected.");
    }

    private sealed class FeatureFlagService
    {
        public bool IsEnabled(string feature) => feature == "dependent-feature";
    }
}
