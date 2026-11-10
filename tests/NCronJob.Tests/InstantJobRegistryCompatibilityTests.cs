using Shouldly;

namespace NCronJob.Tests;

public sealed class InstantJobRegistryCompatibilityTests
{
    [Fact]
    public void ParameterlessOverloadsPreserveLegacyNullFallbackForCustomImplementations()
    {
        var implementation = new LegacyInstantJobRegistry();
        IInstantJobRegistry registry = implementation;

        registry.RunScheduledJob(typeof(DummyJob), TimeSpan.Zero, CancellationToken.None);
        registry.RunScheduledJob("Job", TimeSpan.Zero, CancellationToken.None);
        registry.ForceRunScheduledJob(typeof(DummyJob), TimeSpan.Zero, CancellationToken.None);
        registry.ForceRunScheduledJob("Job", TimeSpan.Zero, CancellationToken.None);

        implementation.Parameters.ShouldBe([null, null, null, null]);
        implementation.ForcedFlags.ShouldBe([false, false, true, true]);
    }

    private sealed class LegacyInstantJobRegistry : IInstantJobRegistry
    {
        public List<object?> Parameters { get; } = [];
        public List<bool> ForcedFlags { get; } = [];

        public Guid RunScheduledJob(
            Type jobType,
            TimeSpan delay,
            object? parameter,
            CancellationToken token = default) =>
            Record(parameter, forced: false);

        public Guid RunScheduledJob(
            string jobName,
            TimeSpan delay,
            object? parameter,
            CancellationToken token = default) =>
            Record(parameter, forced: false);

        public Guid RunScheduledJob(Delegate jobDelegate, TimeSpan delay, CancellationToken token = default) =>
            Guid.NewGuid();

        public Guid ForceRunScheduledJob(Delegate jobDelegate, TimeSpan delay, CancellationToken token = default) =>
            Guid.NewGuid();

        public Guid ForceRunScheduledJob(
            Type jobType,
            TimeSpan delay,
            object? parameter,
            CancellationToken token = default) =>
            Record(parameter, forced: true);

        public Guid ForceRunScheduledJob(
            string jobName,
            TimeSpan delay,
            object? parameter,
            CancellationToken token = default) =>
            Record(parameter, forced: true);

        private Guid Record(object? parameter, bool forced)
        {
            Parameters.Add(parameter);
            ForcedFlags.Add(forced);
            return Guid.NewGuid();
        }
    }
}
