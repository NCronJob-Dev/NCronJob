using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace NCronJob.Tests;

public sealed class LoggingScopeTests : JobIntegrationBase
{
    private readonly ScopeCapturingLoggerProvider loggerProvider = new();

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            loggerProvider.Dispose();
        }
    }

    [Fact]
    public async Task LogsWrittenByAJobCarryTheRunScope()
    {
        ServiceCollection.AddLogging(b => b.AddProvider(loggerProvider));
        ServiceCollection.AddNCronJob(n => n.AddJob<LoggingJob>(p => p.WithCronExpression(Cron.AtEveryMinute).WithName("LoggingJob")));

        await StartNCronJob(startMonitoringEvents: true);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var orchestrationId = Events[0].CorrelationId;

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        var runId = Events.First(e => e.CorrelationId == orchestrationId && e.RunId is not null).RunId;

        var scopeProperties = loggerProvider.Entries
            .Single(e => e.Message == LoggingJob.Message)
            .ScopeProperties;

        scopeProperties["JobName"].ShouldBe($"LoggingJob ({typeof(LoggingJob).FullName})");
        scopeProperties["JobRunId"].ShouldBe(runId);
        scopeProperties["CorrelationId"].ShouldBe(orchestrationId);
        scopeProperties["TriggerType"].ShouldBe(TriggerType.Cron);
    }

    [Fact]
    public async Task DependentJobsLogWithTheirOwnRunScope()
    {
        ServiceCollection.AddLogging(b => b.AddProvider(loggerProvider));
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>()
            .ExecuteWhen(success: s => s.RunJob<LoggingJob>()));

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<DummyJob>(token: CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        var rootRunId = Events.First(e => e.CorrelationId == orchestrationId && e.Type == typeof(DummyJob) && e.RunId is not null).RunId;
        var dependentRunId = Events.First(e => e.CorrelationId == orchestrationId && e.Type == typeof(LoggingJob) && e.RunId is not null).RunId;

        var entry = loggerProvider.Entries.Single(e => e.Message == LoggingJob.Message);

        entry.JobRunScopeCount.ShouldBe(1);
        entry.ScopeProperties["JobName"].ShouldBe(typeof(LoggingJob).FullName);
        entry.ScopeProperties["JobRunId"].ShouldBe(dependentRunId);
        entry.ScopeProperties["JobRunId"].ShouldNotBe(rootRunId);
        entry.ScopeProperties["CorrelationId"].ShouldBe(orchestrationId);
        entry.ScopeProperties["TriggerType"].ShouldBe(TriggerType.Dependent);
    }

    private sealed class LoggingJob(ILogger<LoggingJob> logger) : IJob
    {
        public const string Message = "Hello from inside the job";

        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
#pragma warning disable CA1848 // The test asserts on the plain message
            logger.LogInformation(Message);
#pragma warning restore CA1848
            return Task.CompletedTask;
        }
    }

    private sealed class ScopeCapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private IExternalScopeProvider scopeProvider = new LoggerExternalScopeProvider();

        public ConcurrentQueue<(string Message, Dictionary<string, object?> ScopeProperties, int JobRunScopeCount)> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new ScopeCapturingLogger(this);

        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => this.scopeProvider = scopeProvider;

        public void Dispose()
        {
        }

        private sealed class ScopeCapturingLogger(ScopeCapturingLoggerProvider provider) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => provider.scopeProvider.Push(state);

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var properties = new Dictionary<string, object?>();
                var jobRunScopeCount = 0;
                provider.scopeProvider.ForEachScope((scope, props) =>
                {
                    if (scope is not IEnumerable<KeyValuePair<string, object?>> pairs)
                    {
                        return;
                    }

                    foreach (var (key, value) in pairs)
                    {
                        if (key == "JobRunId")
                        {
                            jobRunScopeCount++;
                        }

                        props[key] = value;
                    }
                }, properties);

                provider.Entries.Enqueue((formatter(state, exception), properties, jobRunScopeCount));
            }
        }
    }
}
