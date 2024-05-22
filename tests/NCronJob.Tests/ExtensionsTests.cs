using NCronJob;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Microsoft.Extensions.Time.Testing;

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
        var fakeTimer = new FakeTimeProvider();
        var builder = new JobOptionBuilder(fakeTimer);
        Should.Throw<ArgumentNullException>(() => builder.WithCronExpression(null!));
    }

    [Fact]
    public void AddingCronExpressionWithIncorrectSegmentCountThrowsArgumentException()
    {
        var fakeTimer = new FakeTimeProvider();
        var builder = new JobOptionBuilder(fakeTimer);
        Should.Throw<ArgumentException>(() => builder.WithCronExpression("* * *"));
    }

    [Fact]
    public void AddingValidCronExpressionWithMinutePrecisionDoesNotThrowException()
    {
        var fakeTimer = new FakeTimeProvider();
        var builder = new JobOptionBuilder(fakeTimer);
        Should.NotThrow(() => builder.WithCronExpression("5 * * * *"));
    }

    [Fact]
    public void AddingValidCronExpressionWithSecondPrecisionDoesNotThrowException()
    {
        var fakeTimer = new FakeTimeProvider();
        var builder = new JobOptionBuilder(fakeTimer);
        Should.NotThrow(() => builder.WithCronExpression("30 5 * * * *", true));
    }

    [Fact]
    public void AddingCronExpressionWithInvalidSecondPrecisionThrowsArgumentException()
    {
        var fakeTimer = new FakeTimeProvider();
        var builder = new JobOptionBuilder(fakeTimer);
        Should.Throw<ArgumentException>(() => builder.WithCronExpression("5 * * * *", true));
    }


    [Fact]
    public void AutoDetectSecondPrecisionWhenNotSpecified()
    {
        var fakeTimer = new FakeTimeProvider();
        var builder = new JobOptionBuilder(fakeTimer);
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
