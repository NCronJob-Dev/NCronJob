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

    [RetryPolicy(retryCount: 2)]
    [SupportsConcurrency(2)]
    private sealed class ConfiguredJob : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token) => Task.CompletedTask;
    }
}
