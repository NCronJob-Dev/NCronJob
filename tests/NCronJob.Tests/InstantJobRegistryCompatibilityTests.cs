using Shouldly;

namespace NCronJob.Tests;

public sealed class InstantJobRegistryCompatibilityTests
{
    [Fact]
    public void ParameterlessOverloadsPreserveLegacyNullFallbackForCustomImplementations()
    {
        var implementation = new LegacyInstantJobRegistry();
        IInstantJobRegistry registry = implementation;

#pragma warning disable CS0618
        registry.RunScheduledJob(typeof(DummyJob), TimeSpan.Zero, CancellationToken.None);
        registry.RunScheduledJob("Job", TimeSpan.Zero, CancellationToken.None);
        registry.RunScheduledJob<DummyJob>(DateTimeOffset.UtcNow, CancellationToken.None);
        registry.RunScheduledJob("Job", DateTimeOffset.UtcNow, CancellationToken.None);
#pragma warning restore CS0618
        registry.ForceRunScheduledJob(typeof(DummyJob), TimeSpan.Zero, CancellationToken.None);
        registry.ForceRunScheduledJob("Job", TimeSpan.Zero, CancellationToken.None);

        implementation.Parameters.Count.ShouldBe(6);
        implementation.Parameters.ShouldBe([null, null, null, null, null, null]);
    }

    private sealed class LegacyInstantJobRegistry : IInstantJobRegistry
    {
        public List<object?> Parameters { get; } = [];

        public Guid RunScheduledJob(
            Type jobType,
            TimeSpan delay,
            object? parameter,
            CancellationToken token = default) =>
            Record(parameter);

        public Guid RunScheduledJob(
            string jobName,
            TimeSpan delay,
            object? parameter,
            CancellationToken token = default) =>
            Record(parameter);

#pragma warning disable CS0618
        public Guid RunScheduledJob<TJob>(
            DateTimeOffset startDate,
            object? parameter,
            CancellationToken token = default)
            where TJob : IJob =>
            Record(parameter);

        public Guid RunScheduledJob(
            string jobName,
            DateTimeOffset startDate,
            object? parameter,
            CancellationToken token = default) =>
            Record(parameter);
#pragma warning restore CS0618

        public Guid RunScheduledJob(Delegate jobDelegate, TimeSpan delay, CancellationToken token = default) =>
            Guid.NewGuid();

#pragma warning disable CS0618
        public Guid RunScheduledJob(Delegate jobDelegate, DateTimeOffset startDate, CancellationToken token = default) =>
            Guid.NewGuid();
#pragma warning restore CS0618

        public Guid ForceRunScheduledJob(Delegate jobDelegate, TimeSpan delay, CancellationToken token = default) =>
            Guid.NewGuid();

#pragma warning disable CS0618
        public Guid ForceRunScheduledJob(Delegate jobDelegate, DateTimeOffset startDate, CancellationToken token = default) =>
            Guid.NewGuid();
#pragma warning restore CS0618

        public Guid ForceRunScheduledJob(
            Type jobType,
            TimeSpan delay,
            object? parameter,
            CancellationToken token = default) =>
            Record(parameter);

        public Guid ForceRunScheduledJob(
            string jobName,
            TimeSpan delay,
            object? parameter,
            CancellationToken token = default) =>
            Record(parameter);

        private Guid Record(object? parameter)
        {
            Parameters.Add(parameter);
            return Guid.NewGuid();
        }
    }
}
