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

    private static NCronJobOptionBuilder BuildSut()
    {
        var collection = new ServiceCollection();
        var settings = new ConcurrencySettings { MaxDegreeOfParallelism = Environment.ProcessorCount * 4 };
        var sut = new NCronJobOptionBuilder(collection, settings, new());

        return sut;
    }
}
