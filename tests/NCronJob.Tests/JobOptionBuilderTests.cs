using Shouldly;

namespace NCronJob.Tests;

public class JobOptionBuilderTests
{
    [Fact]
    public void AddingNullCronExpressionThrowsArgumentNullException()
    {
        var builder = new JobOptionBuilder();
        Should.Throw<ArgumentNullException>(() => builder.WithCronExpression(null!));
    }

    [Fact]
    public void AddingValidCronExpressionWithMinutePrecisionDoesNotThrowException()
    {
        var builder = new JobOptionBuilder();
        Should.NotThrow(() => builder.WithCronExpression(Cron.AtMinute5));
    }

    [Fact]
    public void AddingValidCronExpressionWithSecondPrecisionDoesNotThrowException()
    {
        var builder = new JobOptionBuilder();
        Should.NotThrow(() => builder.WithCronExpression("30 5 * * * *"));
    }

    [Fact]
    public void AutoDetectSecondPrecisionWhenNotSpecified()
    {
        var builder = new JobOptionBuilder();
        builder.WithCronExpression("0 0 12 * * ?");
        var options = builder.GetJobOptions();
        options.ShouldContain(o => o.CronExpression == "0 0 12 * * ?");

        builder.WithCronExpression("0 1 * * *");
        options = builder.GetJobOptions();
        options.ShouldContain(o => o.CronExpression == "0 1 * * *");
    }

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
            .And
            .WithName("name2");

        var options = builder.GetJobOptions();

        options.Count.ShouldBe(2);
        options[0].Name.ShouldBe("name1");
        options[1].Name.ShouldBe("name2");
    }

    [Fact]
    public void ShouldCreateMultipleNamedJobsWithoutAnd()
    {
        var builder = new JobOptionBuilder();
        builder
            .WithName("name1");

        builder
            .WithName("name2");

        var options = builder.GetJobOptions();

        options.Count.ShouldBe(2);
        options[0].Name.ShouldBe("name1");
        options[1].Name.ShouldBe("name2");
    }

    [Fact]
    public void ShouldCreateMultipleNamedAndScheduledJobsWithAnd()
    {
        var builder = new JobOptionBuilder();
        builder
            .WithName("name1")
            .WithCronExpression(Cron.AtEveryMinute)
            .And
            .WithName("name2")
            .WithCronExpression(Cron.AtEveryMinute);

        var options = builder.GetJobOptions();

        options.Count.ShouldBe(2);
        options[0].Name.ShouldBe("name1");
        options[0].CronExpression.ShouldBe(Cron.AtEveryMinute);
        options[1].Name.ShouldBe("name2");
        options[1].CronExpression.ShouldBe(Cron.AtEveryMinute);
    }

    [Fact]
    public void ShouldCreateMultipleNamedAndScheduledJobsWithoutAnd()
    {
        var builder = new JobOptionBuilder();
        builder
            .WithName("name1")
            .WithCronExpression(Cron.AtEveryMinute);

        builder
            .WithName("name2")
            .WithCronExpression(Cron.AtEveryMinute);

        var options = builder.GetJobOptions();

        options.Count.ShouldBe(2);
        options[0].Name.ShouldBe("name1");
        options[0].CronExpression.ShouldBe(Cron.AtEveryMinute);
        options[1].Name.ShouldBe("name2");
        options[1].CronExpression.ShouldBe(Cron.AtEveryMinute);
    }

    [Fact]
    public void ShouldCreateMultipleNamedScheduledAndParameterizedJobsWithAnd()
    {
        var builder = new JobOptionBuilder();
        builder
            .WithName("name1")
            .WithCronExpression(Cron.AtEveryMinute)
            .WithParameter("foo")
            .And
            .WithName("name2")
            .WithCronExpression(Cron.AtEveryMinute)
            .WithParameter("bar");

        var options = builder.GetJobOptions();

        options.Count.ShouldBe(2);
        options[0].Name.ShouldBe("name1");
        options[0].CronExpression.ShouldBe(Cron.AtEveryMinute);
        options[0].Parameter.ShouldBe("foo");
        options[1].Name.ShouldBe("name2");
        options[1].CronExpression.ShouldBe(Cron.AtEveryMinute);
        options[1].Parameter.ShouldBe("bar");
    }

    [Fact]
    public void ShouldCreateMultipleNamedScheduledAndParameterizedJobsWithoutAnd()
    {
        var builder = new JobOptionBuilder();
        builder
            .WithName("name1")
            .WithCronExpression(Cron.AtEveryMinute)
            .WithParameter("foo");

        builder
            .WithName("name2")
            .WithCronExpression(Cron.AtEveryMinute)
            .WithParameter("bar");

        var options = builder.GetJobOptions();

        options.Count.ShouldBe(2);
        options[0].Name.ShouldBe("name1");
        options[0].CronExpression.ShouldBe(Cron.AtEveryMinute);
        options[0].Parameter.ShouldBe("foo");
        options[1].Name.ShouldBe("name2");
        options[1].CronExpression.ShouldBe(Cron.AtEveryMinute);
        options[1].Parameter.ShouldBe("bar");
    }

    [Fact]
    public void ShouldCreateMultipleNamedAndParameterizedJobsWithAnd()
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
    public void ShouldCreateMultipleNamedAndParameterizedJobsWithoutAnd()
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

    [Fact]
    public void ShouldCreateMultipleParameterizedJobsWithAnd()
    {
        var builder = new JobOptionBuilder();
        builder
            .WithParameter("foo")
            .And
            .WithParameter("bar");

        var options = builder.GetJobOptions();

        options.Count.ShouldBe(2);
        options[0].Parameter.ShouldBe("foo");
        options[1].Parameter.ShouldBe("bar");
    }

    [Fact]
    public void ShouldCreateMultipleParameterizedJobsWithoutAnd()
    {
        var builder = new JobOptionBuilder();
        builder
            .WithParameter("foo");

        builder
            .WithParameter("bar");

        var options = builder.GetJobOptions();

        options.Count.ShouldBe(2);
        options[0].Parameter.ShouldBe("foo");
        options[1].Parameter.ShouldBe("bar");
    }
}
