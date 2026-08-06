using Shouldly;

namespace NCronJob.Dashboard.Tests;

public class DependencyGraphBuilderTests
{
    [Fact]
    public void BuildsSuccessAndFaultedEdgesWithLayeredNodes()
    {
        var root = JobDefinition.CreateTyped("root", typeof(RootJob), null);
        var success = JobDefinition.CreateTyped(typeof(SuccessJob), null);
        var faulted = JobDefinition.CreateTyped(typeof(FaultedJob), null);
        var registry = new JobRegistry();
        registry.Add(root);
        registry.RegisterJobDependency([root], new DependentJobRegistryEntry
        {
            RunWhenSuccess = [success],
            RunWhenFaulted = [faulted],
        });

        var graph = new DependencyGraphBuilder(registry).Build();

        graph.Nodes.Count.ShouldBe(3);
        graph.Edges.Count(edge => edge.Kind == GraphEdgeKind.Success).ShouldBe(1);
        graph.Edges.Count(edge => edge.Kind == GraphEdgeKind.Faulted).ShouldBe(1);
        graph.Nodes.Single(node => node.Label == "root").Level.ShouldBe(0);
        graph.Nodes.Where(node => node.Label != "root").ShouldAllBe(node => node.Level == 1);
    }

    private sealed class RootJob : IJob { public Task RunAsync(IJobExecutionContext context, CancellationToken token) => Task.CompletedTask; }
    private sealed class SuccessJob : IJob { public Task RunAsync(IJobExecutionContext context, CancellationToken token) => Task.CompletedTask; }
    private sealed class FaultedJob : IJob { public Task RunAsync(IJobExecutionContext context, CancellationToken token) => Task.CompletedTask; }
}
