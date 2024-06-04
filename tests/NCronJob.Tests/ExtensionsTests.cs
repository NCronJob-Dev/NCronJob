using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace NCronJob.Tests;

public class NCronJobTests
{
    [Fact]
    public void AddingWrongCronExpressionLeadsToException()
    {
        var collection = new ServiceCollection();
        var settings = new ConcurrencySettings { MaxDegreeOfParallelism = Environment.ProcessorCount * 4 };
        var builder = new NCronJobOptionBuilder(collection, settings);

        Action act = () => builder.AddJob<FakeJob>(o => o.WithCronExpression("not-valid"));

        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void AddingCronJobWithSecondPrecisionExpressionNotThrowException()
    {
        var collection = new ServiceCollection();
        var settings = new ConcurrencySettings { MaxDegreeOfParallelism = Environment.ProcessorCount * 4 };
        var builder = new NCronJobOptionBuilder(collection, settings);

        Action act = () => builder.AddJob<FakeJob>(o =>
        {
            o.WithCronExpression("* * * * * *", true);
        });

        act.ShouldNotThrow();
    }

    [Fact]
    public void AddingNullCronExpressionThrowsArgumentNullException()
    {
        var builder = new JobOptionBuilder();
        Should.Throw<ArgumentNullException>(() => builder.WithCronExpression(null!));
    }

    [Fact]
    public void AddingCronExpressionWithIncorrectSegmentCountThrowsArgumentException()
    {
        var builder = new JobOptionBuilder();
        Should.Throw<ArgumentException>(() => builder.WithCronExpression("* * *"));
    }

    [Fact]
    public void AddingValidCronExpressionWithMinutePrecisionDoesNotThrowException()
    {
        var builder = new JobOptionBuilder();
        Should.NotThrow(() => builder.WithCronExpression("5 * * * *"));
    }

    [Fact]
    public void AddingValidCronExpressionWithSecondPrecisionDoesNotThrowException()
    {
        var builder = new JobOptionBuilder();
        Should.NotThrow(() => builder.WithCronExpression("30 5 * * * *", true));
    }

    [Fact]
    public void AddingCronExpressionWithInvalidSecondPrecisionThrowsArgumentException()
    {
        var builder = new JobOptionBuilder();
        Should.Throw<ArgumentException>(() => builder.WithCronExpression("5 * * * *", true));
    }


    [Fact]
    public void AutoDetectSecondPrecisionWhenNotSpecified()
    {
        var builder = new JobOptionBuilder();
        builder.WithCronExpression("0 0 12 * * ?");
        var options = builder.GetJobOptions();
        options.ShouldContain(o => o.CronExpression == "0 0 12 * * ?" && o.EnableSecondPrecision);

        builder.WithCronExpression("0 1 * * *");
        options = builder.GetJobOptions();
        options.ShouldContain(o => o.CronExpression == "0 1 * * *" && !o.EnableSecondPrecision);
    }

    private sealed class FakeJob : IJob
    {
        public Task RunAsync(JobExecutionContext context, CancellationToken token)
            => throw new NotImplementedException();
    }
}
