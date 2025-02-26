using Shouldly;

namespace NCronJob.Tests;

public class JobOptionBuilderTests
{
    [Fact]
    public void ShouldCreateJobOptionsWithCronExpression()
    {
        var builder = new JobOptionBuilder();
        builder.WithCronExpression(Cron.AtEveryMinute);

        var options = builder.GetJobOptions();

        options.Count.ShouldBe(1);
        options.Single().CronExpression.ShouldBe(Cron.AtEveryMinute);
        options.Single().Parameter.ShouldBeNull();
    }

    [Fact]
    public void ShouldCreateMultipleJobsWithParameters()
    {
        var builder = new JobOptionBuilder();
        builder.WithCronExpression(Cron.AtEveryMinute)
            .WithParameter("foo")
            .And
            .WithCronExpression(Cron.AtMinute0)
            .WithParameter("bar");

        var options = builder.GetJobOptions();

        options.Count.ShouldBe(2);
        options[0].CronExpression.ShouldBe(Cron.AtEveryMinute);
        options[0].Parameter.ShouldBe("foo");
        options[1].CronExpression.ShouldBe(Cron.AtMinute0);
        options[1].Parameter.ShouldBe("bar");
    }

    [Fact]
    public void ShouldAddMultipleCronJobsEvenWithoutParameters()
    {
        var builder = new JobOptionBuilder();
        builder
            .WithCronExpression(Cron.AtEveryMinute)
            .And
            .WithCronExpression(Cron.AtMinute0);

        var options = builder.GetJobOptions();

        options.Count.ShouldBe(2);
        options[0].CronExpression.ShouldBe(Cron.AtEveryMinute);
        options[0].Parameter.ShouldBeNull();
        options[1].CronExpression.ShouldBe(Cron.AtMinute0);
        options[1].Parameter.ShouldBeNull();
    }

    [Fact]
    public void ShouldCreateMultipleJobsWithoutAnd()
    {
        var builder = new JobOptionBuilder();
        builder.WithCronExpression(Cron.AtEveryMinute)
            .WithParameter("foo");

        builder.WithCronExpression(Cron.AtMinute0)
            .WithParameter("bar");

        var options = builder.GetJobOptions();

        options.Count.ShouldBe(2);
        options[0].CronExpression.ShouldBe(Cron.AtEveryMinute);
        options[0].Parameter.ShouldBe("foo");
        options[1].CronExpression.ShouldBe(Cron.AtMinute0);
        options[1].Parameter.ShouldBe("bar");
    }

    [Fact]
    public void ShouldCreateMultipleNamedJobsWithAnd()
    {
        var builder = new JobOptionBuilder();
        builder
            .WithName("name1")
            .WithParameter("foo")
            .And
            .WithName("name2")
            .WithParameter("bar");

        var options = builder.GetJobOptions();

        options.Count.ShouldBe(2);
        options[0].Name.ShouldBe("name1");
        options[0].Parameter.ShouldBe("foo");
        options[1].Name.ShouldBe("name2");
        options[1].Parameter.ShouldBe("bar");
    }

    [Fact]
    public void ShouldCreateMultipleNamedJobsWithoutAnd()
    {
        var builder = new JobOptionBuilder();
        builder
            .WithName("name1")
            .WithParameter("foo");

        builder
            .WithName("name2")
            .WithParameter("bar");

        var options = builder.GetJobOptions();

        options.Count.ShouldBe(2);
        options[0].Name.ShouldBe("name1");
        options[0].Parameter.ShouldBe("foo");
        options[1].Name.ShouldBe("name2");
        options[1].Parameter.ShouldBe("bar");
    }
}
