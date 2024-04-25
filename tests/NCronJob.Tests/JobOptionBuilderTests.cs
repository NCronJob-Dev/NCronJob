using Cronos;
using LinkDotNet.NCronJob;
using Shouldly;

namespace NCronJob.Tests;

public class JobOptionBuilderTests
{
    [Fact]
    public void ShouldCreateJobOptionsWithCronExpression()
    {
        var builder = new JobOptionBuilder();
        builder.WithCronExpression("* * * * *");

        var options = builder.GetJobOptions();

        options.Count.ShouldBe(1);
        options.Single().CronExpression.ShouldBe("* * * * *");
        options.Single().EnableSecondPrecision.ShouldBeFalse();
        options.Single().Parameter.ShouldBeNull();
    }

    [Fact]
    public void ShouldCreateMultipleJobsWithParameters()
    {
        var builder = new JobOptionBuilder();
        builder.WithCronExpression("* * * * *")
            .WithParameter("foo")
            .And
            .WithCronExpression("0 * * * *")
            .WithParameter("bar");

        var options = builder.GetJobOptions();

        options.Count.ShouldBe(2);
        options[0].CronExpression.ShouldBe("* * * * *");
        options[0].EnableSecondPrecision.ShouldBeFalse();
        options[0].Parameter.ShouldBe("foo");
        options[1].CronExpression.ShouldBe("0 * * * *");
        options[1].EnableSecondPrecision.ShouldBeFalse();
        options[1].Parameter.ShouldBe("bar");
    }

    [Fact]
    public void ShouldAddMultipleCronJobsEvenWithoutParameters()
    {
        var builder = new JobOptionBuilder();
        builder
            .WithCronExpression("* * * * *")
            .And
            .WithCronExpression("0 * * * *");

        var options = builder.GetJobOptions();

        options.Count.ShouldBe(2);
        options[0].CronExpression.ShouldBe("* * * * *");
        options[0].EnableSecondPrecision.ShouldBeFalse();
        options[0].Parameter.ShouldBeNull();
        options[1].CronExpression.ShouldBe("0 * * * *");
        options[1].EnableSecondPrecision.ShouldBeFalse();
        options[1].Parameter.ShouldBeNull();
    }

    [Fact]
    public void ShouldCreateMultipleJobsWithoutAnd()
    {
        var builder = new JobOptionBuilder();
        builder.WithCronExpression("* * * * *")
            .WithParameter("foo");

        builder.WithCronExpression("0 * * * *")
            .WithParameter("bar");

        var options = builder.GetJobOptions();

        options.Count.ShouldBe(2);
        options[0].CronExpression.ShouldBe("* * * * *");
        options[0].EnableSecondPrecision.ShouldBeFalse();
        options[0].Parameter.ShouldBe("foo");
        options[1].CronExpression.ShouldBe("0 * * * *");
        options[1].EnableSecondPrecision.ShouldBeFalse();
        options[1].Parameter.ShouldBe("bar");
    }

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
    public void ShouldCalculateNextRunTimeBasedOnTimeZone()
    {
        var builder = new JobOptionBuilder();
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        builder.WithCronExpression("0 12 * * *", timeZoneInfo: timeZone);  // Noon every day

        var options = builder.GetJobOptions();

        var baseTime = new DateTime(2024, 1, 1, 11, 0, 0, DateTimeKind.Utc); // This is 6 AM EST on Jan 1, 2024
        var expectedRunTime = new DateTime(2024, 1, 1, 17, 0, 0, DateTimeKind.Utc); // Noon EST is 5 PM UTC on Jan 1, 2024

        var nextRunTime = CronExpression.Parse(options.Single().CronExpression)
            .GetNextOccurrence(baseTime, timeZone);

        nextRunTime!.Value.ShouldBe(expectedRunTime);
    }

    [Fact]
    public void ShouldHandleCrossTimezoneSchedulingCorrectly()
    {
        var builder = new JobOptionBuilder();
        var timeZoneLA = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        builder.WithCronExpression("0 8 * * *", timeZoneInfo: timeZoneLA);  // 8 AM PST

        var options = builder.GetJobOptions();
        var baseTime = new DateTime(2024, 1, 1, 11, 0, 0, DateTimeKind.Utc); // This is 3 AM PST, 6 AM EST
        var expectedRunTime = new DateTime(2024, 1, 1, 16, 0, 0, DateTimeKind.Utc); // 8 AM PST is 11 AM EST, which is 4 PM UTC

        var nextRunTime = CronExpression.Parse(options.Single().CronExpression)
            .GetNextOccurrence(baseTime, timeZoneLA);

        nextRunTime!.Value.ShouldBe(expectedRunTime);
    }

    [Fact]
    public void ShouldHandleDaylightSavingTimeSpringForwardCorrectly()
    {
        var builder = new JobOptionBuilder();
        var timeZoneNY = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        builder.WithCronExpression("0 2 10 3 *", timeZoneInfo: timeZoneNY);  // 2 AM on March 10th, the day DST starts in 2024

        var options = builder.GetJobOptions();
        var baseTime = new DateTime(2024, 3, 9, 7, 0, 0, DateTimeKind.Utc); // The day before DST starts, 2 AM EST is 7 AM UTC
        var expectedRunTime = new DateTime(2024, 3, 10, 7, 0, 0, DateTimeKind.Utc); // The next valid run time should be 3 AM EDT (7 AM UTC), as 2 AM does not exist on this day

        var nextRunTime = CronExpression.Parse(options.Single().CronExpression)
            .GetNextOccurrence(baseTime, timeZoneNY);

        nextRunTime!.Value.ShouldBe(expectedRunTime);
    }

}
