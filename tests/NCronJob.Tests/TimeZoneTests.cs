using NCronJob;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using System.Threading.Channels;

namespace NCronJob.Tests;

public sealed class TimeZoneTests : JobIntegrationBase
{
    [Fact]
    public void ShouldAssignCorrectTimeZoneToJobOptions()
    {
        var builder = new JobOptionBuilder();
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        builder.WithCronExpression("* * * * *", timeZoneInfo: timeZone);

        var options = builder.GetJobOptions();

        options.Single().TimeZoneInfo.ShouldBe(timeZone);
    }

    [Fact]
    public void ShouldDefaultToUtcIfTimeZoneNotSpecified()
    {
        var builder = new JobOptionBuilder();
        builder.WithCronExpression("* * * * *");

        var options = builder.GetJobOptions();

        options.Single().TimeZoneInfo.ShouldBe(TimeZoneInfo.Utc);
    }

    [Fact]
    public void ShouldAssignCorrectTimeZoneAndExpressionToJobOptions()
    {
        var builder = new JobOptionBuilder();
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        var cronExpression = "* * * * *";
        builder.WithCronExpression(cronExpression, timeZoneInfo: timeZone);

        var options = builder.GetJobOptions();

        options.Single().TimeZoneInfo.ShouldBe(timeZone);
        options.Single().CronExpression.ShouldBe(cronExpression);
    }

    [Fact]
    public void ShouldHandleInvalidTimeZoneGracefully()
    {
        var builder = new JobOptionBuilder();
        Should.Throw<TimeZoneNotFoundException>(() =>
        {
            builder.WithCronExpression("* * * * *", timeZoneInfo: TimeZoneInfo.FindSystemTimeZoneById("Non-Existent Time Zone"));
        });
    }

    [Fact]
    public async Task ShouldCorrectlyHandleDaylightSavingTimeSpringForward()
    {
        var baseTime = new DateTimeOffset(2024, 3, 9, 23, 59, 59, TimeSpan.Zero); // Just before the spring forward
        SetupJobWithTimeZone<SimpleJob>("0 2 * * *", "Eastern Standard Time");
        var jobEntry = await InitializeServiceAndAdvanceTime<SimpleJob>(baseTime, TimeSpan.FromMinutes(2));

        var expectedRunTime = new DateTimeOffset(2024, 3, 10, 7, 0, 0, TimeSpan.Zero); // 3 AM EDT is 7 AM UTC
        var nextRunTime = GetNextRunTime(jobEntry, baseTime.AddMinutes(2), TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));

        nextRunTime!.Value.ShouldBe(expectedRunTime);
    }

    [Fact]
    public async Task ShouldCalculateNextRunTimeBasedOnTimeZone()
    {
        var baseTime = new DateTimeOffset(2024, 1, 1, 11, 0, 0, TimeSpan.Zero); // This is 6 AM EST on Jan 1, 2024
        SetupJobWithTimeZone<SimpleJob>("0 12 * * *", "Eastern Standard Time");
        var jobEntry = await InitializeServiceAndAdvanceTime<SimpleJob>(baseTime, TimeSpan.FromDays(1));

        var expectedRunTime = new DateTimeOffset(2024, 1, 1, 17, 0, 0, TimeSpan.Zero); // Noon EST is 5 PM UTC
        var nextRunTime = GetNextRunTime(jobEntry, baseTime, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));

        nextRunTime!.Value.ShouldBe(expectedRunTime);
    }

    [Fact]
    public async Task ShouldHandleCrossTimezoneSchedulingCorrectly()
    {
        var baseTime = new DateTimeOffset(2024, 1, 1, 11, 0, 0, TimeSpan.Zero); // This is 3 AM PST, 6 AM EST
        SetupJobWithTimeZone<SimpleJob>("0 8 * * *", "Pacific Standard Time");
        var jobEntry = await InitializeServiceAndAdvanceTime<SimpleJob>(baseTime, TimeSpan.FromDays(1));

        var expectedRunTime = new DateTimeOffset(2024, 1, 1, 16, 0, 0, TimeSpan.Zero); // 8 AM PST is 11 AM EST, which is 4 PM UTC
        var nextRunTime = GetNextRunTime(jobEntry, baseTime, TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));

        nextRunTime!.Value.ShouldBe(expectedRunTime);
    }

    private DateTimeOffset? GetNextRunTime(JobDefinition jobEntry, DateTimeOffset baseTime, TimeZoneInfo timeZone) =>
        jobEntry.CronExpression!.GetNextOccurrence(baseTime, timeZone);

    private void SetupJobWithTimeZone<T>(string cronExpression, string timeZoneId) where T : class, IJob
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        ServiceCollection.AddNCronJob(n => n.AddJob<T>(p => p.WithCronExpression(cronExpression, timeZoneInfo: timeZone)));
    }

    private async Task<JobDefinition> InitializeServiceAndAdvanceTime<T>(DateTimeOffset baseTime, TimeSpan? advanceTime = null) where T : IJob
    {
        var fakeTimer = new FakeTimeProvider(baseTime);
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        var provider = CreateServiceProvider();
        var cronRegistryEntries = provider.GetServices<JobDefinition>();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
        if (advanceTime.HasValue)
        {
            fakeTimer.Advance(advanceTime.Value);
        }
        return cronRegistryEntries.First(entry => entry.Type == typeof(T));
    }

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
}
