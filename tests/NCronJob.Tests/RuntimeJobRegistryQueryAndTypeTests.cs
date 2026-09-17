using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace NCronJob.Tests;

public class RuntimeJobRegistryQueryAndTypeTests : JobIntegrationBase
{
    [Fact]
    public void TryGetNextOccurrenceUsesCurrentUtcTimeAndConfiguredTimeZone()
    {
        FakeTimer.SetUtcNow(new DateTimeOffset(2024, 1, 1, 11, 0, 0, TimeSpan.Zero));
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        ServiceCollection.AddNCronJob(options => options.AddJob<DummyJob>(job => job
            .WithName("Job")
            .WithCronExpression("0 8 * * *", timeZone)));

        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.TryGetNextOccurrence("Job", out var nextRun).ShouldBeTrue();
        nextRun.ShouldBe(new DateTimeOffset(2024, 1, 1, 16, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void TryGetNextOccurrenceReturnsFalseForUnknownJob()
    {
        ServiceCollection.AddNCronJob();
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.TryGetNextOccurrence("Unknown", out var nextRun).ShouldBeFalse();
        nextRun.ShouldBeNull();
    }

    [Fact]
    public void TryGetNextOccurrenceReturnsFalseForUnscheduledJob()
    {
        ServiceCollection.AddNCronJob(options => options.AddJob<DummyJob>(job => job.WithName("Job")));
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.TryGetNextOccurrence("Job", out var nextRun).ShouldBeFalse();
        nextRun.ShouldBeNull();
    }

    [Fact]
    public void TryGetNextOccurrenceReturnsFalseForDisabledJob()
    {
        ServiceCollection.AddNCronJob(options => options.AddJob<DummyJob>(job => job
            .WithName("Job")
            .WithCronExpression(Cron.AtEveryMinute)));
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.DisableJob("Job");

        registry.TryGetNextOccurrence("Job", out var nextRun).ShouldBeFalse();
        nextRun.ShouldBeNull();
    }

    [Fact]
    public void TryGetNextOccurrenceReturnsTrueWithNullWhenScheduleHasNoFutureOccurrence()
    {
        FakeTimer.SetUtcNow(new DateTimeOffset(9999, 12, 31, 23, 59, 59, TimeSpan.Zero));
        ServiceCollection.AddNCronJob(options => options.AddJob<DummyJob>(job => job
            .WithName("Job")
            .WithCronExpression(Cron.AtEveryMinute)));
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        registry.TryGetNextOccurrence("Job", out var nextRun).ShouldBeTrue();
        nextRun.ShouldBeNull();
    }

    [Fact]
    public void EnableJobByTypeThrowsWhenNoRootJobMatches()
    {
        ServiceCollection.AddNCronJob();
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var exception = Should.Throw<InvalidOperationException>(() => registry.EnableJob(typeof(DummyJob)));

        exception.Message.ShouldBe($"Root job with type '{typeof(DummyJob)}' not found.");
    }

    [Fact]
    public void DisableJobByTypeThrowsWhenNoRootJobMatches()
    {
        ServiceCollection.AddNCronJob();
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        var exception = Should.Throw<InvalidOperationException>(() => registry.DisableJob(typeof(DummyJob)));

        exception.Message.ShouldBe($"Root job with type '{typeof(DummyJob)}' not found.");
    }

    [Fact]
    public void EnableAndDisableJobByTypeValidateNull()
    {
        ServiceCollection.AddNCronJob();
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();

        Should.Throw<ArgumentNullException>(() => registry.EnableJob((Type)null!));
        Should.Throw<ArgumentNullException>(() => registry.DisableJob((Type)null!));
    }

    [Fact]
    public void EnableAndDisableJobByTypeRemainIdempotentForMultipleRegistrations()
    {
        ServiceCollection.AddNCronJob(options =>
        {
            options.AddJob<DummyJob>(job => job.WithName("First").WithCronExpression(Cron.AtEveryMinute));
            options.AddJob<DummyJob>(job => job.WithName("Second").WithCronExpression(Cron.AtMinute2));
        });
        var registry = ServiceProvider.GetRequiredService<IRuntimeJobRegistry>();
        var jobs = ServiceProvider.GetRequiredService<JobRegistry>().FindAllRootJobDefinition(typeof(DummyJob));

        registry.DisableJob(typeof(DummyJob));
        registry.DisableJob(typeof(DummyJob));
        jobs.ShouldAllBe(job => !job.IsEnabled);

        registry.EnableJob(typeof(DummyJob));
        registry.EnableJob(typeof(DummyJob));
        jobs.ShouldAllBe(job => job.IsEnabled);
    }
}
