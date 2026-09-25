using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace NCronJob.Tests;

public class RuntimeJobRegistrationTests : JobIntegrationBase
{
    [Fact]
    public async Task DynamicallyAddedJobIsExecuted()
    {
        ServiceCollection.AddNCronJob();

        await StartNCronJob();

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        Events.Count.ShouldBe(0);

        Delegate jobDelegate = (Storage storage) => storage.Add("true");
        registry.TryRegister(s => s.AddJob(jobDelegate, Cron.AtEveryMinute), out _).ShouldBe(true);

        var orchestrationId = Events[0].CorrelationId;

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        await WaitForOrchestrationCompletion(orchestrationId);

        var filteredEvents = Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldBeScheduledThenCompleted();

        Storage.Entries[0].ShouldBe("true");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task MultipleDynamicallyAddedJobsAreExecuted()
    {
        ServiceCollection.AddNCronJob();

        await StartNCronJob();

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        Events.Count.ShouldBe(0);

        Delegate jobDelegateOne = (Storage storage) => storage.Add("one");
        Delegate jobDelegateTwo = (Storage storage) => storage.Add("two");

        registry.TryRegister(s => s.AddJob(jobDelegateOne, Cron.AtEveryMinute), out _).ShouldBe(true);
        registry.TryRegister(s => s.AddJob(jobDelegateTwo, Cron.AtEveryMinute), out _).ShouldBe(true);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var completedOrchestrationEvents = await WaitForNthOrchestrationState(
            ExecutionState.OrchestrationCompleted,
            2);

        var firstOrchestrationEvents = Events.FilterByOrchestrationId(completedOrchestrationEvents[0].CorrelationId);
        firstOrchestrationEvents.ShouldBeScheduledThenCompleted();

        var secondOrchestrationEvents = Events.FilterByOrchestrationId(completedOrchestrationEvents[1].CorrelationId);
        secondOrchestrationEvents.ShouldBeScheduledThenCompleted();

        Storage.Entries.ShouldContain("one");
        Storage.Entries.ShouldContain("two");
        Storage.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task CanRegisterMultipleTimesTheSameDelegateWithDifferentNames()
    {
        ServiceCollection.AddNCronJob();

        await StartNCronJob();

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        Events.Count.ShouldBe(0);

        Delegate jobDelegate = () => { };

        // Trying to register twice the same unnamed delegates fails
        registry.TryRegister(s => s.AddJob(jobDelegate, Cron.AtEveryMinute), out _).ShouldBe(true);

        registry.TryRegister(s => s.AddJob(jobDelegate, Cron.AtEveryMinute), out var unNamedUntypedJobException).ShouldBe(false);
        unNamedUntypedJobException.ShouldNotBeNull();
        unNamedUntypedJobException.Message.ShouldStartWith("Job registration conflict for job 'Untyped job NCronJob.UntypedJob_");

        // Trying to register twice the same named delegates fails as well
        registry.TryRegister(s => s.AddJob(jobDelegate, Cron.AtEveryMinute, jobName: "one"), out _).ShouldBe(true);
        registry.TryRegister(s => s.AddJob(jobDelegate, Cron.AtEveryMinute, jobName: "one"), out var namedUntypedJobException).ShouldBe(false);
        namedUntypedJobException.ShouldNotBeNull();
        namedUntypedJobException.Message.ShouldStartWith("Job registration conflict detected. A job has already been registered with the name 'one'.");

        registry.TryRegister(s => s.AddJob(jobDelegate, Cron.AtEveryMinute, jobName: "another"), out _).ShouldBe(true);

        var jobRegistry = ServiceProvider.GetRequiredService<JobRegistry>();
        var jobNames = jobRegistry.GetAllRootJobs().Select(jd => jd.JobFullName).ToList();
        jobNames.Count.ShouldBe(3);
        jobNames[0].ShouldStartWith("Untyped job NCronJob.UntypedJob_");
        jobNames[1].ShouldBe("Untyped job one");
        jobNames[2].ShouldBe("Untyped job another");
    }

    [Fact]
    public void ShouldThrowRuntimeExceptionWithDuplicateJob()
    {
        ServiceCollection.AddNCronJob(s => s.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithName("JobName")));
        var runtimeJobRegistry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var successful = runtimeJobRegistry.TryRegister(s => s.AddJob(() =>
        {
        }, Cron.AtEveryMinute, jobName: "JobName"), out var exception);

        successful.ShouldBeFalse();
        exception.ShouldNotBeNull();
        exception.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public void LateRegistrationConflictDoesNotPartiallyRegisterOrScheduleEarlierJobs()
    {
        ServiceCollection.AddNCronJob();

        var runtimeJobRegistry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();
        var jobRegistry = ServiceProvider.GetRequiredService<JobRegistry>();
        var queueManager = ServiceProvider.GetRequiredService<JobQueueManager>();

        var successful = runtimeJobRegistry.TryRegister(s =>
        {
            s.AddJob<AnotherDummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithName("Duplicate"));
            s.AddJob(() => { }, Cron.AtEveryMinute, jobName: "Duplicate");
        }, out var exception);

        successful.ShouldBeFalse();
        exception.ShouldBeOfType<InvalidOperationException>();
        jobRegistry.GetAllRootJobs().ShouldBeEmpty();
        queueManager.GetAllJobQueueNames().ShouldBeEmpty();
        queueManager.TryGetQueue(typeof(AnotherDummyJob).FullName!, out _).ShouldBeFalse();

        runtimeJobRegistry.TryRegister(
            s => s.AddJob<AnotherDummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithName("Duplicate")),
            out _).ShouldBeTrue();

        var registeredJob = jobRegistry.FindRootJobDefinition("Duplicate");
        registeredJob.ShouldNotBeNull();
    }

    [Fact]
    public void SchedulingFailureRollsBackRegistryQueuesDependenciesAndServices()
    {
        ServiceCollection.AddNCronJob(s =>
            s.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtMinute2).WithName("Existing"))
                .ExecuteWhen(success: d => d.RunJob<ExceptionJob>()));

        var serviceDescriptors = ServiceCollection.ToArray();
        var runtimeJobRegistry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();
        var jobRegistry = ServiceProvider.GetRequiredService<JobRegistry>();
        var cronRunScheduler = ServiceProvider.GetRequiredService<CronRunScheduler>();
        var queueManager = ServiceProvider.GetRequiredService<JobQueueManager>();
        var settings = ServiceProvider.GetRequiredService<ConcurrencySettings>();
        var previousMaxDegreeOfParallelism = settings.MaxDegreeOfParallelism;
        var previousDefaultJobRunExpiry = settings.DefaultJobRunExpiry;
        var existingJob = jobRegistry.FindRootJobDefinition("Existing");
        existingJob.ShouldNotBeNull();

        cronRunScheduler.ScheduleNextRun(existingJob);
        queueManager.TryGetQueue(typeof(DummyJob).FullName!, out var sharedQueue).ShouldBeTrue();
        var existingRun = sharedQueue.Single();

        void ThrowOnNewQueue(string _) => throw new InvalidOperationException("Scheduling failed.");

        queueManager.QueueAdded += ThrowOnNewQueue;
        var successful = runtimeJobRegistry.TryRegister(builder =>
        {
            var fullBuilder = (NCronJobOptionBuilder)builder;
            fullBuilder
                .WithMaxDegreeOfParallelism(previousMaxDegreeOfParallelism + 1)
                .WithDefaultJobRunExpiry(TimeSpan.FromMinutes(3));
            fullBuilder.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtMinute5).WithName("Batch"))
                .AddNotificationHandler<RollbackNotificationHandler>()
                .ExecuteWhen(success: d => d.RunJob<AnotherDummyJob>());
            fullBuilder.AddJob<AnotherDummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithName("Failure"));
        }, out var exception);
        queueManager.QueueAdded -= ThrowOnNewQueue;

        successful.ShouldBeFalse();
        exception.ShouldBeOfType<InvalidOperationException>();
        jobRegistry.GetAllRootJobs().ShouldBe([existingJob]);
        jobRegistry.GetDependentSuccessJobs(existingJob).Single().Type.ShouldBe(typeof(ExceptionJob));

        queueManager.TryGetQueue(typeof(DummyJob).FullName!, out sharedQueue).ShouldBeTrue();
        sharedQueue.Count.ShouldBe(1);
        sharedQueue.Single().ShouldBeSameAs(existingRun);
        queueManager.TryGetQueue(typeof(AnotherDummyJob).FullName!, out _).ShouldBeFalse();

        ServiceCollection.Count.ShouldBe(serviceDescriptors.Length);
        ServiceCollection.Zip(serviceDescriptors).ShouldAllBe(pair => ReferenceEquals(pair.First, pair.Second));
        settings.MaxDegreeOfParallelism.ShouldBe(previousMaxDegreeOfParallelism);
        settings.DefaultJobRunExpiry.ShouldBe(previousDefaultJobRunExpiry);

        runtimeJobRegistry.TryRegister(
            builder => builder.AddJob(
                typeof(DummyJob),
                p => p.WithCronExpression(Cron.AtMinute5).WithName("Batch")),
            out _).ShouldBeTrue();

        var reRegisteredJob = jobRegistry.FindRootJobDefinition("Batch");
        reRegisteredJob.ShouldNotBeNull();
        jobRegistry.GetDependentSuccessJobs(reRegisteredJob).ShouldBeEmpty();
    }

    [Fact]
    public async Task FailedRegistrationCancelsADequeuedRunBeforeItsJobBodyExecutes()
    {
        ServiceCollection.AddNCronJob(builder =>
        {
            builder.WithMaxDegreeOfParallelism(1);
            builder.AddJob<DummyJob>(p => p.WithName("Registered"));
        });

        await StartNCronJob();

        var runtimeJobRegistry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();
        var jobRegistry = ServiceProvider.GetRequiredService<JobRegistry>();
        var queueManager = ServiceProvider.GetRequiredService<JobQueueManager>();
        var firstQueueAdded = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var continueRegistration = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queueAdditions = 0;

        void DequeueFirstRunThenFail(string queueName)
        {
            queueAdditions++;
            if (queueAdditions == 1)
            {
                firstQueueAdded.TrySetResult(queueName);
                continueRegistration.Task.GetAwaiter().GetResult();
                return;
            }

            throw new InvalidOperationException("Scheduling failed.");
        }

        queueManager.QueueAdded += DequeueFirstRunThenFail;
        var registrationTask = Task.Run(() =>
        {
            var successful = runtimeJobRegistry.TryRegister(builder =>
            {
                builder.AddJob(
                    typeof(DummyJob),
                    p => p.WithCronExpression(Cron.AtEveryMinute).WithName("Earlier"));
                builder.AddJob(
                    typeof(AnotherDummyJob),
                    p => p.WithCronExpression(Cron.AtEveryMinute).WithName("Failure"));
            }, out var exception);

            return (successful, exception);
        }, CancellationToken);

        try
        {
            var queueName = await firstQueueAdded.Task.WaitAsync(CancellationToken);
            FakeTimer.Advance(TimeSpan.FromMinutes(1));
            await queueManager.WaitUntilEmptyAsync(queueName, CancellationToken);
        }
        finally
        {
            continueRegistration.TrySetResult();
        }

        (bool successful, Exception? exception) result;
        try
        {
            result = await registrationTask.WaitAsync(CancellationToken);
        }
        finally
        {
            queueManager.QueueAdded -= DequeueFirstRunThenFail;
        }

        var (successful, exception) = result;

        successful.ShouldBeFalse();
        exception.ShouldBeOfType<InvalidOperationException>();

        await WaitForJobState(ExecutionState.Cancelled, name: "Earlier");

        Storage.Entries.ShouldBeEmpty();
        Events.ShouldNotContain(e =>
            e.Name == "Earlier"
            && (e.State == ExecutionState.Initializing || e.State == ExecutionState.Running));
        jobRegistry.GetAllRootJobs().Select(job => job.CustomName).ShouldBe(["Registered"]);
        queueManager.GetAllJobQueueNames().ShouldBeEmpty();

        runtimeJobRegistry.TryRegister(
            builder => builder.AddJob(
                typeof(DummyJob),
                p => p.WithCronExpression(Cron.AtEveryMinute).WithName("AfterRollback")),
            out _).ShouldBeTrue();

        var followUpRun = await WaitForJobState(ExecutionState.Scheduled, name: "AfterRollback");
        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        await WaitForOrchestrationCompletion(followUpRun.CorrelationId);

        Storage.Entries.ShouldBe(["DummyJob - Parameter: "]);
    }

    [Fact]
    public void RegisteringDuplicateDuringRuntimeLeadsToException()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        var runtimeRegistry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var successful = runtimeRegistry.TryRegister(n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)), out var exception);

        successful.ShouldBeFalse();
        exception.ShouldNotBeNull();
    }

    [Fact]
    public void TryRegisteringShouldIndicateFailureWithAGivenException()
    {
        ServiceCollection.AddNCronJob();
        var runtimeJobRegistry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();
        runtimeJobRegistry.TryRegister(s => s.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        var successful = runtimeJobRegistry.TryRegister(s => s.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)), out var exception);

        successful.ShouldBeFalse();
        exception.ShouldNotBeNull();
    }

    [Fact]
    public void TryRegisterShouldIndicateSuccess()
    {
        ServiceCollection.AddNCronJob();
        var runtimeJobRegistry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var successful = runtimeJobRegistry.TryRegister(s => s.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)), out var exception);

        successful.ShouldBeTrue();
        exception.ShouldBeNull();
    }

    private sealed class RollbackNotificationHandler : IJobNotificationHandler<DummyJob>
    {
        public Task HandleAsync(
            IJobExecutionContext context,
            Exception? exception,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
