using Shouldly;

namespace NCronJob.Tests;

public sealed class DependentJobDefinitionTests
{
    [Fact]
    public async Task ConvertingTypedDescriptorPreservesDependentJobConfiguration()
    {
        var parameter = new object();
        var descriptor = DependentJobDefinition.CreateTyped(typeof(ConfiguredJob), parameter);
        descriptor.UpdateWith(new JobOption
        {
            Timeout = TimeSpan.FromSeconds(2),
            JobRunExpiry = TimeSpan.FromMinutes(3),
            Conditions = [(_, _) => ValueTask.FromResult(true)]
        });

        var definition = descriptor.ToJobDefinition();

        definition.Type.ShouldBe(typeof(ConfiguredJob));
        definition.Parameter.ShouldBeSameAs(parameter);
        definition.Timeout.ShouldBe(TimeSpan.FromSeconds(2));
        definition.JobRunExpiry.ShouldBe(TimeSpan.FromMinutes(3));
        definition.RetryPolicy.ShouldNotBeNull();
        definition.ConcurrencyPolicy.ShouldNotBeNull();
        definition.Condition.ShouldNotBeNull();
        (await definition.Condition(null!, CancellationToken.None)).ShouldBeTrue();
    }

    [Fact]
    public void DescriptorIdentityMatchesRootJobIdentityOnly()
    {
        var parameter = new object();
        var root = JobDefinition.CreateTyped("job", typeof(ConfiguredJob), parameter);
        root.UpdateWith(new JobOption { CronExpression = Cron.AtEveryMinute });

        var identity = DependentJobDefinition.FromRoot(root);
        var matching = DependentJobDefinition.CreateTyped(typeof(ConfiguredJob), parameter, "job");
        matching.UpdateWith(new JobOption { Timeout = TimeSpan.FromSeconds(1) });
        var differentParameter = DependentJobDefinition.CreateTyped(typeof(ConfiguredJob), new object(), "job");

        identity.ShouldBe(matching);
        identity.ShouldNotBe(differentParameter);
    }

    [RetryPolicy(retryCount: 2)]
    [SupportsConcurrency(2)]
    private sealed class ConfiguredJob : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token) => Task.CompletedTask;
    }
}
