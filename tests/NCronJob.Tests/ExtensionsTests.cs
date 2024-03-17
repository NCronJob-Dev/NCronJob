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

    private sealed class FakeJob : IJob
    {
        public Task Run(JobExecutionContext context, CancellationToken token = default)
            => throw new NotImplementedException();
    }
}
