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

    [Theory]
    [MemberData(nameof(MultipleJobsWithParametersVariants))]
    public void ShouldCreateMultipleJobsWithParameters(Action<JobOptionBuilder> configureBuilder)
    {
        var builder = new JobOptionBuilder();
        configureBuilder(builder);

        var options = builder.GetJobOptions();

        options.Count.ShouldBe(2);
        options[0].CronExpression.ShouldBe(Cron.AtEveryMinute);
        options[0].Parameter.ShouldBe("foo");
        options[1].CronExpression.ShouldBe(Cron.AtMinute0);
        options[1].Parameter.ShouldBe("bar");
    }

    public static TheoryData<Action<JobOptionBuilder>> MultipleJobsWithParametersVariants()
    {
        var data = new TheoryData<Action<JobOptionBuilder>>();

        data.Add(b => b
            .WithCronExpression(Cron.AtEveryMinute)
            .WithParameter("foo")
            .And
            .WithCronExpression(Cron.AtMinute0)
            .WithParameter("bar"));

        data.Add(b =>
        {
            b.WithCronExpression(Cron.AtEveryMinute)
                .WithParameter("foo");

            b.WithCronExpression(Cron.AtMinute0)
                .WithParameter("bar");
        });

        return data;
    }

    [Theory]
    [MemberData(nameof(MultipleCronJobsVariants))]
    public void ShouldAddMultipleCronJobsEvenWithoutParameters(Action<JobOptionBuilder> configureBuilder)
    {
        var builder = new JobOptionBuilder();
        configureBuilder(builder);

        var options = builder.GetJobOptions();

        options.Count.ShouldBe(2);
        options[0].CronExpression.ShouldBe(Cron.AtEveryMinute);
        options[0].Parameter.ShouldBeNull();
        options[1].CronExpression.ShouldBe(Cron.AtMinute0);
        options[1].Parameter.ShouldBeNull();
    }

    public static TheoryData<Action<JobOptionBuilder>> MultipleCronJobsVariants()
    {
        var data = new TheoryData<Action<JobOptionBuilder>>();

        data.Add(b => b
            .WithCronExpression(Cron.AtEveryMinute)
            .And
            .WithCronExpression(Cron.AtMinute0));

        data.Add(b =>
        {
            b.WithCronExpression(Cron.AtEveryMinute);
            b.WithCronExpression(Cron.AtMinute0);
        });

        return data;
    }

    [Theory]
    [MemberData(nameof(MultipleNamedJobsVariants))]
    public void ShouldCreateMultipleNamedJobs(Action<JobOptionBuilder> configureBuilder)
    {
        var builder = new JobOptionBuilder();
        configureBuilder(builder);

        var options = builder.GetJobOptions();

        options.Count.ShouldBe(2);
        options[0].Name.ShouldBe("name1");
        options[1].Name.ShouldBe("name2");
    }

    public static TheoryData<Action<JobOptionBuilder>> MultipleNamedJobsVariants()
    {
        var data = new TheoryData<Action<JobOptionBuilder>>();

        data.Add(b => b
            .WithName("name1")
            .And
            .WithName("name2"));

        data.Add(b =>
        {
            b.WithName("name1");
            b.WithName("name2");
        });

        return data;
    }

    [Theory]
    [MemberData(nameof(MultipleNamedAndScheduledJobsVariants))]
    public void ShouldCreateMultipleNamedAndScheduledJobs(Action<JobOptionBuilder> configureBuilder)
    {
        var builder = new JobOptionBuilder();
        configureBuilder(builder);

        var options = builder.GetJobOptions();

        options.Count.ShouldBe(2);
        options[0].Name.ShouldBe("name1");
        options[0].CronExpression.ShouldBe(Cron.AtEveryMinute);
        options[1].Name.ShouldBe("name2");
        options[1].CronExpression.ShouldBe(Cron.AtEveryMinute);
    }

    public static TheoryData<Action<JobOptionBuilder>> MultipleNamedAndScheduledJobsVariants()
    {
        var data = new TheoryData<Action<JobOptionBuilder>>();

        data.Add(b => b
            .WithName("name1")
            .WithCronExpression(Cron.AtEveryMinute)
            .And
            .WithName("name2")
            .WithCronExpression(Cron.AtEveryMinute));

        data.Add(b =>
        {
            b.WithName("name1")
                .WithCronExpression(Cron.AtEveryMinute);

            b.WithName("name2")
                .WithCronExpression(Cron.AtEveryMinute);
        });

        return data;
    }

    [Theory]
    [MemberData(nameof(MultipleNamedScheduledAndParameterizedJobsVariants))]
    public void ShouldCreateMultipleNamedScheduledAndParameterizedJobs(Action<JobOptionBuilder> configureBuilder)
    {
        var builder = new JobOptionBuilder();
        configureBuilder(builder);

        var options = builder.GetJobOptions();

        options.Count.ShouldBe(2);
        options[0].Name.ShouldBe("name1");
        options[0].CronExpression.ShouldBe(Cron.AtEveryMinute);
        options[0].Parameter.ShouldBe("foo");
        options[1].Name.ShouldBe("name2");
        options[1].CronExpression.ShouldBe(Cron.AtEveryMinute);
        options[1].Parameter.ShouldBe("bar");
    }

    public static TheoryData<Action<JobOptionBuilder>> MultipleNamedScheduledAndParameterizedJobsVariants()
    {
        var data = new TheoryData<Action<JobOptionBuilder>>();

        data.Add(b => b
            .WithName("name1")
            .WithCronExpression(Cron.AtEveryMinute)
            .WithParameter("foo")
            .And
            .WithName("name2")
            .WithCronExpression(Cron.AtEveryMinute)
            .WithParameter("bar"));

        data.Add(b =>
        {
            b.WithName("name1")
                .WithCronExpression(Cron.AtEveryMinute)
                .WithParameter("foo");

            b.WithName("name2")
                .WithCronExpression(Cron.AtEveryMinute)
                .WithParameter("bar");
        });

        return data;
    }

    [Theory]
    [MemberData(nameof(MultipleNamedAndParameterizedJobsVariants))]
    public void ShouldCreateMultipleNamedAndParameterizedJobs(Action<JobOptionBuilder> configureBuilder)
    {
        var builder = new JobOptionBuilder();
        configureBuilder(builder);

        var options = builder.GetJobOptions();

        options.Count.ShouldBe(2);
        options[0].Name.ShouldBe("name1");
        options[0].Parameter.ShouldBe("foo");
        options[1].Name.ShouldBe("name2");
        options[1].Parameter.ShouldBe("bar");
    }

    public static TheoryData<Action<JobOptionBuilder>> MultipleNamedAndParameterizedJobsVariants()
    {
        var data = new TheoryData<Action<JobOptionBuilder>>();

        data.Add(b => b
            .WithName("name1")
            .WithParameter("foo")
            .And
            .WithName("name2")
            .WithParameter("bar"));

        data.Add(b =>
        {
            b.WithName("name1")
                .WithParameter("foo");

            b.WithName("name2")
                .WithParameter("bar");
        });

        return data;
    }

    [Theory]
    [MemberData(nameof(MultipleParameterizedJobsVariants))]
    public void ShouldCreateMultipleParameterizedJobs(Action<JobOptionBuilder> configureBuilder)
    {
        var builder = new JobOptionBuilder();
        configureBuilder(builder);

        var options = builder.GetJobOptions();

        options.Count.ShouldBe(2);
        options[0].Parameter.ShouldBe("foo");
        options[1].Parameter.ShouldBe("bar");
    }

    public static TheoryData<Action<JobOptionBuilder>> MultipleParameterizedJobsVariants()
    {
        var data = new TheoryData<Action<JobOptionBuilder>>();

        data.Add(b => b
            .WithParameter("foo")
            .And
            .WithParameter("bar"));

        data.Add(b =>
        {
            b.WithParameter("foo");
            b.WithParameter("bar");
        });

        return data;
    }
}
