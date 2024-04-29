using LinkDotNet.NCronJob;
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

        // Check that the timezone is correctly assigned
        options.Single().TimeZoneInfo.ShouldBe(timeZone);

        // Check that the cron expression is correctly assigned
        options.Single().CronExpression.ShouldBe(cronExpression);
    }

    [Fact]
    public async Task ShouldCorrectlyHandleDaylightSavingTimeSpringForward()
    {
        var fakeTimer = new FakeTimeProvider(new DateTimeOffset(2024, 3, 10, 0, 0, 0, TimeSpan.Zero)); // Start of the day on DST change
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(p =>
            p.WithCronExpression("0 2 * * *", timeZoneInfo: timeZone)));// run at 2 AM every day

        var provider = CreateServiceProvider();
        var cronRegistryEntries = provider.GetServices<RegistryEntry>();
        var simpleJobEntry = cronRegistryEntries.First(entry => entry.Type == typeof(SimpleJob));

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken.None);

        // The test date is one minute before the spring forward, which skips from 1:59 AM to 3:00 AM
        var testDate = new DateTime(2024, 3, 10, 1, 59, 59, DateTimeKind.Unspecified); // One minute before the spring forward
        var baseTime = TimeZoneInfo.ConvertTimeToUtc(testDate, timeZone);

        var nextRunTime = GetNextRunTime(simpleJobEntry, baseTime, timeZone);

        // Check if nextRunTime correctly jumps over the missing hour
        var expectedRunTime = new DateTime(2024, 3, 10, 7, 0, 0, DateTimeKind.Utc); // 3 AM EDT is 7 AM UTC
        nextRunTime!.Value.ShouldBe(expectedRunTime);
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
    public void ShouldCalculateNextRunTimeBasedOnTimeZone()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(p =>
            p.WithCronExpression("0 12 * * *", timeZoneInfo: timeZone)));// run at 2 AM every day

        var provider = CreateServiceProvider();
        var cronRegistryEntries = provider.GetServices<RegistryEntry>();
        var simpleJobEntry = cronRegistryEntries.First(entry => entry.Type == typeof(SimpleJob));

        var baseTime = new DateTime(2024, 1, 1, 11, 0, 0, DateTimeKind.Utc); // This is 6 AM EST on Jan 1, 2024
        var expectedRunTime = new DateTime(2024, 1, 1, 17, 0, 0, DateTimeKind.Utc); // Noon EST is 5 PM UTC on Jan 1, 2024

        var nextRunTime = GetNextRunTime(simpleJobEntry, baseTime, timeZone);

        nextRunTime!.Value.ShouldBe(expectedRunTime);
    }

    [Fact]
    public void ShouldHandleCrossTimezoneSchedulingCorrectly()
    {
        var timeZoneLA = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(p =>
            p.WithCronExpression("0 8 * * *", timeZoneInfo: timeZoneLA))); // 8 AM PST

        var provider = CreateServiceProvider();
        var cronRegistryEntries = provider.GetServices<RegistryEntry>();
        var simpleJobEntry = cronRegistryEntries.First(entry => entry.Type == typeof(SimpleJob));

        var baseTime = new DateTime(2024, 1, 1, 11, 0, 0, DateTimeKind.Utc); // This is 3 AM PST, 6 AM EST
        var expectedRunTime = new DateTime(2024, 1, 1, 16, 0, 0, DateTimeKind.Utc); // 8 AM PST is 11 AM EST, which is 4 PM UTC

        var nextRunTime = GetNextRunTime(simpleJobEntry, baseTime, timeZoneLA);

        nextRunTime!.Value.ShouldBe(expectedRunTime);
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

    private DateTime? GetNextRunTime(RegistryEntry jobEntry, DateTime baseTime, TimeZoneInfo timeZone) =>
        jobEntry.CronExpression!.GetNextOccurrence(baseTime, timeZone);
}
