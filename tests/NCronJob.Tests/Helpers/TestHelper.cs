using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace NCronJob.Tests;

public abstract class JobIntegrationBase : IDisposable
{
    private ServiceProvider? serviceProvider;
    private readonly OrchestrationHelper orchestrationHelper;

    protected CancellationToken CancellationToken { get; }
    protected ServiceCollection ServiceCollection { get; }
    protected FakeTimeProvider FakeTimer { get; }
    protected Storage Storage { get; }
    protected IList<ExecutionProgress> Events { get; private set; } = [];
    private IDisposable? subscription;

    protected JobIntegrationBase()
    {
        FakeTimeProvider fakeTimeProvider = new() { AutoAdvanceAmount = TimeSpan.FromMilliseconds(1) };
        FakeTimer = fakeTimeProvider;

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CancellationToken = cancellationToken;

        ServiceCollection = new();
        ServiceCollection.AddLogging();
        ServiceCollection.AddSingleton<IHostApplicationLifetime, MockHostApplicationLifetime>();
        ServiceCollection.AddSingleton<TimeProvider>(FakeTimer);

        Storage = new(FakeTimer);
        ServiceCollection.AddSingleton(Storage);

        orchestrationHelper = new(FakeTimer, StopMonitoringEvents, CancellationToken);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

#pragma warning disable IDISP023 // Don't use reference types in finalizer context
        // False positive (cf. https://github.com/DotNetAnalyzers/IDisposableAnalyzers/issues/176)

        StopMonitoringEvents();

        serviceProvider?.Dispose();

        TestFailureHelper.DumpContext(Storage, Events);
#pragma warning restore IDISP023 // Don't use reference types in finalizer context
    }

    protected ServiceProvider ServiceProvider => serviceProvider ??= ServiceCollection.BuildServiceProvider();

    protected async Task<IList<ExecutionProgress>> WaitForNthOrchestrationState(
        ExecutionState state,
        int howMany,
        Action? onAnyFoundButLast = null,
        bool stopMonitoringEvents = false)
    {
        AssertEventsAreBeingMonitored();

        return await orchestrationHelper.WaitForNthOrchestrationState(
            Events,
            state,
            howMany,
            onAnyFoundButLast,
            stopMonitoringEvents);
    }

    protected async Task WaitForOrchestrationCompletion(
        IList<ExecutionProgress> events,
        Guid orchestrationId)
    {
        await orchestrationHelper.WaitForOrchestrationState(
            events,
            orchestrationId,
            ExecutionState.OrchestrationCompleted,
            stopMonitoringEvents: false);
    }

    protected async Task WaitForOrchestrationCompletion(
        Guid orchestrationId,
        bool stopMonitoringEvents = false)
    {
        AssertEventsAreBeingMonitored();

        await orchestrationHelper.WaitForOrchestrationState(
            Events,
            orchestrationId,
            ExecutionState.OrchestrationCompleted,
            stopMonitoringEvents);
    }

    protected async Task WaitForOrchestrationState(
        Guid orchestrationId,
        ExecutionState state,
        bool stopMonitoringEvents = false)
    {
        AssertEventsAreBeingMonitored();

        await orchestrationHelper.WaitForOrchestrationState(Events, orchestrationId, state, stopMonitoringEvents);
    }

    protected async Task StartNCronJob(
        bool startMonitoringEvents = false)
    {
        if (startMonitoringEvents)
        {
            (subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(ServiceProvider);
            Events = events;
        }

        await ServiceProvider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);
    }

    protected (IDisposable subscription, IList<ExecutionProgress> events) RegisterAnExecutionProgressSubscriber(
        IServiceProvider serviceProvider)
    {
        return orchestrationHelper.RegisterAnExecutionProgressSubscriber(serviceProvider);
    }

    private void StopMonitoringEvents()
    {
        subscription?.Dispose();
    }

    private void AssertEventsAreBeingMonitored()
    {
        if (subscription is not null)
        {
            return;
        }

        throw new InvalidOperationException(
            $"""
            Events aren't monitored.
            Invoke '{nameof(StartNCronJob)}' and explicitly set the appropriate parameter to do so.
            """);
    }
}
