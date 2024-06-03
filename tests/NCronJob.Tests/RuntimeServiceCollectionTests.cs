using Shouldly;

namespace NCronJob.Tests;

public class RuntimeServiceCollectionTests
{
    [Fact]
    public void RuntimeServiceCollectionShouldAddJob()
    {
        var collection = new RuntimeServiceCollection();

        collection.AddNCronJob(n => n.AddJob<SimpleJob>(p => p.WithCronExpression("0 0 * * *")));

        collection.GetJobDefinitions().ShouldHaveSingleItem();
        collection.GetJobDefinitions().First().Type.ShouldBe(typeof(SimpleJob));
        collection.GetJobDefinitions().First().CronExpression?.ToString().ShouldBe("0 0 * * *");
    }

    [Fact]
    public void RuntimeServiceCollectionShouldProcessAnonymousDelegateJobs()
    {
        var collection = new RuntimeServiceCollection();

        collection.AddNCronJob(() => Console.Write("Hello World"), "* * * * *");

        collection.GetJobDefinitions().ShouldHaveSingleItem();
    }

    [Fact]
    public void RuntimeServiceCollectionShouldProcessExecuteWhenJobs()
    {
        var collection = new RuntimeServiceCollection();

        collection.AddNCronJob(n =>
            n.AddJob<SimpleJob>().ExecuteWhen(success: builder => builder.RunJob(() => Console.WriteLine())));

        collection.GetJobDefinitions().Length.ShouldBe(2);
    }

    private sealed class SimpleJob : IJob
    {
        public Task RunAsync(JobExecutionContext context, CancellationToken token) => throw new NotImplementedException();
    }
}
