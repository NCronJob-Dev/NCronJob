using LinkDotNet.NCronJob;
using Shouldly;

namespace NCronJob.Tests;

public class CronRegistryTests
{
    [Fact]
    public void ConsecutiveCallsToGetAllInstantJobsAndClear_ShouldReturnEmptyList()
    {
        var registry = new CronRegistry([]);
        registry.AddInstantJob<SimpleJob>();
        registry.GetAllInstantJobsAndClear().ShouldHaveSingleItem();
        registry.GetAllInstantJobsAndClear().ShouldBeEmpty();
    }

    private sealed class SimpleJob : IJob
    {
        public Task RunAsync(JobExecutionContext context, CancellationToken token)
        {
            return Task.CompletedTask;
        }
    }
}
