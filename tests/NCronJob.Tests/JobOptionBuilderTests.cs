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
        builder.WithCronExpression("* * * * *")
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
}
