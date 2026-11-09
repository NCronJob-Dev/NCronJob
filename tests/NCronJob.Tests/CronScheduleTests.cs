using Shouldly;

namespace NCronJob.Tests;

public sealed class CronScheduleTests
{
    private static readonly DateTimeOffset From = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("* * * * *")]
    [InlineData("*/5 * * * * *")]
    [InlineData("  0 0 1 * *  ")]
    [InlineData("@daily")]
    public void ValidExpressionsAreAccepted(string expression)
    {
        CronSchedule.IsValid(expression, out var error).ShouldBeTrue();
        error.ShouldBeNull();
    }

    [Theory]
    [InlineData("* * * *")]
    [InlineData("* * * * * * *")]
    [InlineData("99 * * * *")]
    [InlineData("@sometimes")]
    public void InvalidExpressionsAreRejectedWithAReason(string expression)
    {
        CronSchedule.IsValid(expression, out var error).ShouldBeFalse();
        error.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ReturnsTheRequestedNumberOfOccurrencesAfterTheStart()
    {
        CronSchedule.GetNextOccurrences("0 * * * *", 3, From).ShouldBe(
        [
            From.AddHours(1),
            From.AddHours(2),
            From.AddHours(3),
        ]);
    }

    [Fact]
    public void SecondPrecisionExpressionsYieldSecondOccurrences()
    {
        CronSchedule.GetNextOccurrences("*/30 * * * * *", 2, From).ShouldBe([From.AddSeconds(30), From.AddMinutes(1)]);
    }

    [Fact]
    public void OccurrencesAreEvaluatedInTheGivenTimeZone()
    {
        var plusTwo = TimeZoneInfo.CreateCustomTimeZone("plus-two", TimeSpan.FromHours(2), "plus-two", "plus-two");

        var occurrence = CronSchedule.GetNextOccurrences("0 3 * * *", 1, From, plusTwo).ShouldHaveSingleItem();

        occurrence.ShouldBe(From.AddHours(1));
        occurrence.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void UnreachableExpressionYieldsNoOccurrences()
    {
        CronSchedule.GetNextOccurrences("0 0 30 2 *", 5, From).ShouldBeEmpty();
    }

    [Fact]
    public void InvalidExpressionThrows()
    {
        Should.Throw<ArgumentException>(() => CronSchedule.GetNextOccurrences("* * * *", 1, From));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveCountThrows(int count)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => CronSchedule.GetNextOccurrences("* * * * *", count, From));
    }
}
