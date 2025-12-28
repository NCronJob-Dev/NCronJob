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
        var options = OptionsFrom((b) => b.WithCronExpression("0 0 12 * * ?"));
        options.ShouldContain(o => o.CronExpression == "0 0 12 * * ?");

        options = OptionsFrom((b) => b.WithCronExpression("0 1 * * *"));
        options.ShouldContain(o => o.CronExpression == "0 1 * * *");
    }

    [Fact]
    public void ShouldCreateJobOptionsWithCronExpression()
    {
        var options = OptionsFrom((b) => b.WithCronExpression(Cron.AtEveryMinute));

        options.Count.ShouldBe(1);
        options.Single().CronExpression.ShouldBe(Cron.AtEveryMinute);
        options.Single().Parameter.ShouldBeNull();
    }

    [Fact]
    public void ShouldCreateMultipleJobsWithParameters()
    {
        Action<JobOptionBuilder> actor = (b) => b.WithCronExpression(Cron.AtEveryMinute)
            .WithParameter("foo")
            .And
            .WithCronExpression(Cron.AtMinute0)
            .WithParameter("bar");

        var options = OptionsFrom(actor);

        options.Count.ShouldBe(2);
        options[0].CronExpression.ShouldBe(Cron.AtEveryMinute);
        options[0].Parameter.ShouldBe("foo");
        options[1].CronExpression.ShouldBe(Cron.AtMinute0);
        options[1].Parameter.ShouldBe("bar");
    }

    [Fact]
    public void ShouldAddMultipleCronJobsEvenWithoutParameters()
    {
        Action<JobOptionBuilder> actor = (b) => b
            .WithCronExpression(Cron.AtEveryMinute)
            .And
            .WithCronExpression(Cron.AtMinute0);

        var options = OptionsFrom(actor);

        options.Count.ShouldBe(2);
        options[0].CronExpression.ShouldBe(Cron.AtEveryMinute);
        options[0].Parameter.ShouldBeNull();
        options[1].CronExpression.ShouldBe(Cron.AtMinute0);
        options[1].Parameter.ShouldBeNull();
    }

    [Fact]
    public void ShouldCreateMultipleJobsWithoutAnd()
    {
        Action<JobOptionBuilder> actor = (builder) =>
        {
            builder.WithCronExpression(Cron.AtEveryMinute)
                .WithParameter("foo");

            builder.WithCronExpression(Cron.AtMinute0)
                .WithParameter("bar");
        };

        var options = OptionsFrom(actor);

        options.Count.ShouldBe(2);
        options[0].CronExpression.ShouldBe(Cron.AtEveryMinute);
        options[0].Parameter.ShouldBe("foo");
        options[1].CronExpression.ShouldBe(Cron.AtMinute0);
        options[1].Parameter.ShouldBe("bar");
    }

    [Fact]
    public void ShouldCreateMultipleNamedJobsWithAnd()
    {
        Action<JobOptionBuilder> actor = (b) => b
            .WithName("name1")
            .And
            .WithName("name2");

        var options = OptionsFrom(actor);

        options.Count.ShouldBe(2);
        options[0].Name.ShouldBe("name1");
        options[1].Name.ShouldBe("name2");
    }

    [Fact]
    public void ShouldCreateMultipleNamedJobsWithoutAnd()
    {
        Action<JobOptionBuilder> actor = (builder) =>
        {
            builder
                .WithName("name1");

            builder
                .WithName("name2");
        };

        var options = OptionsFrom(actor);

        options.Count.ShouldBe(2);
        options[0].Name.ShouldBe("name1");
        options[1].Name.ShouldBe("name2");
    }

    [Fact]
    public void ShouldCreateMultipleNamedAndScheduledJobsWithAnd()
    {
        Action<JobOptionBuilder> actor = (b) => b
            .WithName("name1")
            .WithCronExpression(Cron.AtEveryMinute)
            .And
            .WithName("name2")
            .WithCronExpression(Cron.AtEveryMinute);

        var options = OptionsFrom(actor);

        options.Count.ShouldBe(2);
        options[0].Name.ShouldBe("name1");
        options[0].CronExpression.ShouldBe(Cron.AtEveryMinute);
        options[1].Name.ShouldBe("name2");
        options[1].CronExpression.ShouldBe(Cron.AtEveryMinute);
    }

    [Fact]
    public void ShouldCreateMultipleNamedAndScheduledJobsWithoutAnd()
    {
        Action<JobOptionBuilder> actor = (builder) =>
        {
            builder
                .WithName("name1")
                .WithCronExpression(Cron.AtEveryMinute);

            builder
                .WithName("name2")
                .WithCronExpression(Cron.AtEveryMinute);
        };

        var options = OptionsFrom(actor);

        options.Count.ShouldBe(2);
        options[0].Name.ShouldBe("name1");
        options[0].CronExpression.ShouldBe(Cron.AtEveryMinute);
        options[1].Name.ShouldBe("name2");
        options[1].CronExpression.ShouldBe(Cron.AtEveryMinute);
    }

    [Fact]
    public void ShouldCreateMultipleNamedScheduledAndParameterizedJobsWithAnd()
    {
        Action<JobOptionBuilder> actor = (b) => b
            .WithName("name1")
            .WithCronExpression(Cron.AtEveryMinute)
            .WithParameter("foo")
            .And
            .WithName("name2")
            .WithCronExpression(Cron.AtEveryMinute)
            .WithParameter("bar");

        var options = OptionsFrom(actor);

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
        Action<JobOptionBuilder> actor = (builder) =>
        {
            builder
                .WithName("name1")
                .WithCronExpression(Cron.AtEveryMinute)
                .WithParameter("foo");

            builder
                .WithName("name2")
                .WithCronExpression(Cron.AtEveryMinute)
                .WithParameter("bar");
        };

        var options = OptionsFrom(actor);

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
        Action<JobOptionBuilder> actor = (b) => b
            .WithName("name1")
            .WithParameter("foo")
            .And
            .WithName("name2")
            .WithParameter("bar");

        var options = OptionsFrom(actor);

        options.Count.ShouldBe(2);
        options[0].Name.ShouldBe("name1");
        options[0].Parameter.ShouldBe("foo");
        options[1].Name.ShouldBe("name2");
        options[1].Parameter.ShouldBe("bar");
    }

    [Fact]
    public void ShouldCreateMultipleNamedAndParameterizedJobsWithoutAnd()
    {
        Action<JobOptionBuilder> actor = (builder) =>
        {
            builder
                .WithName("name1")
                .WithParameter("foo");

            builder
                .WithName("name2")
                .WithParameter("bar");
        };

        var options = OptionsFrom(actor);

        options.Count.ShouldBe(2);
        options[0].Name.ShouldBe("name1");
        options[0].Parameter.ShouldBe("foo");
        options[1].Name.ShouldBe("name2");
        options[1].Parameter.ShouldBe("bar");
    }

    [Fact]
    public void ShouldCreateMultipleParameterizedJobsWithAnd()
    {
        Action<JobOptionBuilder> actor = (b) => b
            .WithParameter("foo")
            .And
            .WithParameter("bar");

        var options = OptionsFrom(actor);

        options.Count.ShouldBe(2);
        options[0].Parameter.ShouldBe("foo");
        options[1].Parameter.ShouldBe("bar");
    }

    [Fact]
    public void ShouldCreateMultipleParameterizedJobsWithoutAnd()
    {
        Action<JobOptionBuilder> actor = (builder) =>
        {
            builder
               .WithParameter("foo");

            builder
                .WithParameter("bar");
        };

        var options = OptionsFrom(actor);

        options.Count.ShouldBe(2);
        options[0].Parameter.ShouldBe("foo");
        options[1].Parameter.ShouldBe("bar");
    }

    private static List<JobOption> OptionsFrom(Action<JobOptionBuilder> actor)
    {
        return JobOptionBuilder.Evaluate(actor).ToList();
    }
}
