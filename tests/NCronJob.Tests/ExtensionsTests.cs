using LinkDotNet.NCronJob;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace NCronJob.Tests;

public class NCronJobTests
{
    [Fact]
    public void AddingWrongCronExpressionLeadsToException()
    {
        var collection = new ServiceCollection();

        Action act = () => collection.AddCronJob<FakeJob>(o => o.CronExpression = "not-valid");

        act.ShouldThrow<InvalidOperationException>();
    }

     [Fact]
    public void AddingCronJobWithSecondPrecisionExpressionNotThrowException()
    {
        var everySecond = "* * * * * *";
        var collection = new ServiceCollection();
        collection.AddNCronJob(options => options.EnableSecondPrecision  = true);

        Action act = () => collection.AddCronJob<FakeJob>(o => o.CronExpression = everySecond);

        act.ShouldNotThrow();
    }

    private sealed class FakeJob : IJob
    {
        public Task Run(JobExecutionContext context, CancellationToken token = default)
            => throw new NotImplementedException();
    }
}
