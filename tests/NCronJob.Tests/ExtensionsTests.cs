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
        var builder = new NCronJobOptionBuilder(collection);

        Action act = () => builder.AddJob<FakeJob>(o => o.WithCronExpression("not-valid"));

        act.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void AddingCronJobWithSecondPrecisionExpressionNotThrowException()
    {
        var collection = new ServiceCollection();
        var builder = new NCronJobOptionBuilder(collection);

        Action act = () => builder.AddJob<FakeJob>(o =>
        {
            o.WithCronExpression("* * * * * *", true);
        });

        act.ShouldNotThrow();
    }

    private sealed class FakeJob : IJob
    {
        public Task RunAsync(JobExecutionContext context, CancellationToken token)
            => throw new NotImplementedException();
    }
}
