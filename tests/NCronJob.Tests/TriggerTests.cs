using Cronos;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace NCronJob.Tests;

public class TriggerTests
{
    [Fact]
    public void CronTrigger_ShouldHaveCorrectType()
    {
        // Arrange
        var cronExpression = CronExpression.Parse("0 0 * * *");
        var trigger = new CronTrigger(cronExpression, "0 0 * * *", TimeZoneInfo.Utc);

        // Act & Assert
        trigger.Type.ShouldBe(TriggerType.Cron);
    }

    [Fact]
    public void CronTrigger_ShouldCalculateNextOccurrence()
    {
        // Arrange
        var cronExpression = CronExpression.Parse("0 0 * * *"); // Daily at midnight
        var trigger = new CronTrigger(cronExpression, "0 0 * * *", TimeZoneInfo.Utc);
        var now = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);

        // Act
        var nextOccurrence = trigger.GetNextOccurrence(now);

        // Assert
        nextOccurrence.ShouldNotBeNull();
        nextOccurrence.Value.ShouldBe(new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void CronTrigger_ShouldPreserveUserDefinedExpression()
    {
        // Arrange
        var userExpression = "  0 0 1 * *  "; // With extra whitespace
        var cronExpression = CronExpression.Parse(userExpression.Trim());
        var trigger = new CronTrigger(cronExpression, userExpression, TimeZoneInfo.Utc);

        // Act & Assert
        trigger.UserDefinedCronExpression.ShouldBe(userExpression);
    }

    [Fact]
    public void InstantTrigger_ShouldHaveCorrectType()
    {
        // Arrange
        var runAt = DateTimeOffset.UtcNow;
        var trigger = new InstantTrigger(runAt);

        // Act & Assert
        trigger.Type.ShouldBe(TriggerType.Instant);
        trigger.RunAt.ShouldBe(runAt);
    }

    [Fact]
    public void StartupTrigger_ShouldHaveCorrectType()
    {
        // Arrange
        var trigger = new StartupTrigger(shouldCrashOnFailure: true);

        // Act & Assert
        trigger.Type.ShouldBe(TriggerType.Startup);
        trigger.ShouldCrashOnFailure.ShouldBeTrue();
    }

    [Fact]
    public void StartupTrigger_ShouldStoreCrashOnFailureSetting()
    {
        // Arrange
        var triggerWithCrash = new StartupTrigger(shouldCrashOnFailure: true);
        var triggerWithoutCrash = new StartupTrigger(shouldCrashOnFailure: false);

        // Act & Assert
        triggerWithCrash.ShouldCrashOnFailure.ShouldBeTrue();
        triggerWithoutCrash.ShouldCrashOnFailure.ShouldBeFalse();
    }

    [Fact]
    public void DependentTrigger_ShouldHaveCorrectType()
    {
        // Arrange
        var trigger = new DependentTrigger();

        // Act & Assert
        trigger.Type.ShouldBe(TriggerType.Dependent);
    }

    [Fact]
    public void JobRun_ShouldExposeCorrectTriggerType_ForCronJobs()
    {
        // Arrange
        var jd = JobDefinition.CreateTyped(typeof(DummyJob), null);
        jd.UpdateWith(new JobOption() { CronExpression = "0 0 * * *" });
        var timeProvider = new FakeTimeProvider();

        // Act
        var jobRun = JobRun.Create(timeProvider, _ => { }, jd, timeProvider.GetUtcNow());

        // Assert
        jobRun.TriggerType.ShouldBe(TriggerType.Cron);
        jobRun.Trigger.ShouldBeOfType<CronTrigger>();
    }

    [Fact]
    public void JobRun_ShouldExposeCorrectTriggerType_ForInstantJobs()
    {
        // Arrange
        var jd = JobDefinition.CreateTyped(typeof(DummyJob), null);
        var timeProvider = new FakeTimeProvider();
        var runAt = timeProvider.GetUtcNow().AddMinutes(5);

        // Act
        var jobRun = JobRun.CreateInstant(timeProvider, _ => { }, jd, runAt, null, CancellationToken.None);

        // Assert
        jobRun.TriggerType.ShouldBe(TriggerType.Instant);
        jobRun.Trigger.ShouldBeOfType<InstantTrigger>();
        if (jobRun.Trigger is InstantTrigger instantTrigger)
        {
            instantTrigger.RunAt.ShouldBe(runAt);
        }
    }

    [Fact]
    public void JobRun_ShouldExposeCorrectTriggerType_ForStartupJobs()
    {
        // Arrange
        var jd = JobDefinition.CreateTyped(typeof(DummyJob), null);
        jd.UpdateWith(new JobOption() { ShouldCrashOnStartupFailure = true });
        var timeProvider = new FakeTimeProvider();

        // Act
        var jobRun = JobRun.CreateStartupJob(timeProvider, _ => { }, jd);

        // Assert
        jobRun.TriggerType.ShouldBe(TriggerType.Startup);
        jobRun.Trigger.ShouldBeOfType<StartupTrigger>();
        if (jobRun.Trigger is StartupTrigger startupTrigger)
        {
            startupTrigger.ShouldCrashOnFailure.ShouldBeTrue();
        }
    }

    [Fact]
    public void JobRun_ShouldExposeCorrectTriggerType_ForDependentJobs()
    {
        // Arrange
        var parentJd = JobDefinition.CreateTyped(typeof(DummyJob), null);
        var childJd = JobDefinition.CreateTyped(typeof(DummyJob), null);
        var timeProvider = new FakeTimeProvider();
        var parentJobRun = JobRun.CreateStartupJob(timeProvider, _ => { }, parentJd);

        // Act
        var childJobRun = parentJobRun.CreateDependent(childJd, null, CancellationToken.None);

        // Assert
        childJobRun.TriggerType.ShouldBe(TriggerType.Dependent);
        childJobRun.Trigger.ShouldBeOfType<DependentTrigger>();
    }

    private sealed class DummyJob : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            return Task.CompletedTask;
        }
    }
}
