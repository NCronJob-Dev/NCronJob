using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace NCronJob.Tests;

public class NCronJobTests
{
    [Fact]
    public void AddingWrongCronExpressionLeadsToException()
    {
        var builder = BuildSut();

        Action act = () => builder.AddJob<DummyJob>(o => o.WithCronExpression("not-valid"));

        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void AddingCronJobWithSecondPrecisionExpressionNotThrowException()
    {
        var builder = BuildSut();

        Action act = () => builder.AddJob<DummyJob>(o =>
        {
            o.WithCronExpression(Cron.AtEverySecond);
        });

        act.ShouldNotThrow();
    }

    [Fact]
    public void AddingCronExpressionWithIncorrectSegmentCountThrowsArgumentException()
    {
        var builder = BuildSut();

        Should.Throw<ArgumentException>(() => builder.AddJob<DummyJob>(p => p.WithCronExpression("* * *")));
    }

    [Theory]
    [InlineData("@every_second")]
    [InlineData("@every_minute")]
    [InlineData("@hourly")]
    [InlineData("@daily")]
    [InlineData("@midnight")]
    [InlineData("@weekly")]
    [InlineData("@monthly")]
    [InlineData("@yearly")]
    [InlineData("@annually")]
    public void AddingCronJobWithMacroDoesNotThrow(string macro)
    {
        var builder = BuildSut();

        Should.NotThrow(() => builder.AddJob<DummyJob>(p => p.WithCronExpression(macro)));
    }

    [Theory]
    [InlineData("@every_second", "* * * * * *")]
    [InlineData("@every_minute", "* * * * *")]
    [InlineData("@hourly", "0 * * * *")]
    [InlineData("@daily", "0 0 * * *")]
    [InlineData("@midnight", "0 0 * * *")]
    [InlineData("@weekly", "0 0 * * 0")]
    [InlineData("@monthly", "0 0 1 * *")]
    [InlineData("@yearly", "0 0 1 1 *")]
    [InlineData("@annually", "0 0 1 1 *")]
    internal void CronMacroMatchesItsDocumentedExpression(string macro, string expression)
    {
        var macroJob = JobDefinition.CreateTyped(typeof(DummyJob), null);
        macroJob.UpdateWith(new JobOption { CronExpression = macro });

        var expressionJob = JobDefinition.CreateTyped(typeof(DummyJob), null);
        expressionJob.UpdateWith(new JobOption { CronExpression = expression });

        var start = new DateTimeOffset(2026, 9, 11, 10, 30, 15, TimeSpan.Zero);
        macroJob.GetNextCronOccurrence(start).ShouldBe(expressionJob.GetNextCronOccurrence(start));
    }

    [Fact]
    public void AddingCronJobWithUnknownMacroThrowsArgumentException()
    {
        var builder = BuildSut();

        Should.Throw<ArgumentException>(() => builder.AddJob<DummyJob>(p => p.WithCronExpression("@fortnightly")))
            .Message.ShouldContain("@fortnightly");
    }

    [Fact]
    public void NamedDelegateConvenienceOverloadRegistersRuntimeManageableJob()
    {
        var services = new ServiceCollection();
        Action job = () => { };

        services.AddNCronJob(job, Cron.AtEveryMinute, timeZoneInfo: null, jobName: "Named job");

        using var serviceProvider = services.BuildServiceProvider();
        var jobDefinition = serviceProvider.GetRequiredService<JobRegistry>().FindRootJobDefinition("Named job");

        jobDefinition.ShouldNotBeNull();
        jobDefinition.IsTypedJob.ShouldBeFalse();
    }

    private static NCronJobOptionBuilder BuildSut()
    {
        var collection = new ServiceCollection();
        var settings = new ConcurrencySettings { MaxDegreeOfParallelism = Environment.ProcessorCount * 4 };
        var sut = new NCronJobOptionBuilder(collection, settings, new());

        return sut;
    }
}
