using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace NCronJob.Tests;

public sealed class StartupSummaryTests : JobIntegrationBase
{
    private readonly RecordingLoggerProvider loggerProvider = new();

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            loggerProvider.Dispose();
        }
    }

    [Fact]
    public async Task LogsOneSummaryOfAllScheduledJobsAtStartup()
    {
        var helsinki = TimeZoneInfo.FindSystemTimeZoneById("Europe/Helsinki");
        ServiceCollection.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug).AddProvider(loggerProvider));
        ServiceCollection.AddNCronJob(n => n
            .AddJob<DummyJob>(p => p.WithName("nightly-import").WithCronExpression("0 2 * * *", helsinki))
            .AddJob<AnotherDummyJob>(p => p.WithName("cache-warmup").WithCronExpression("*/5 * * * *")));

        await StartNCronJob();

        var summary = loggerProvider.Entries.Single(e => e.Message.StartsWith("NCronJob started with", StringComparison.Ordinal));
        summary.Level.ShouldBe(LogLevel.Information);

        var lines = summary.Message.ReplaceLineEndings("\n").Split('\n');
        lines[0].ShouldBe("NCronJob started with 2 jobs:");
        lines[1].ShouldContain("nightly-import");
        lines[1].ShouldContain("0 2 * * *");
        lines[1].ShouldContain("Europe/Helsinki");
        lines[1].ShouldContain("next ");
        lines[2].ShouldContain("cache-warmup");
        lines[2].ShouldContain("*/5 * * * *");
        lines[2].ShouldContain("UTC");
    }

    [Fact]
    public async Task SummaryKeepsSecondsForSecondPrecisionSchedules()
    {
        ServiceCollection.AddLogging(b => b.AddProvider(loggerProvider));
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEverySecond)));

        await StartNCronJob();

        var summary = loggerProvider.Entries.Single(e => e.Message.StartsWith("NCronJob started with", StringComparison.Ordinal));

        summary.Message.ShouldMatch(@"next \d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$");
    }

    [Fact]
    public async Task QueueChangesAreNotLoggedAboveDebug()
    {
        ServiceCollection.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug).AddProvider(loggerProvider));
        ServiceCollection.AddNCronJob(n => n.AddJob<DummyJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        // The test host only starts the QueueWorker; the logger subscribes on construction.
        _ = ServiceProvider.GetServices<IHostedService>().OfType<QueueChangeLogger>().Single();
        await StartNCronJob();

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        await WaitForOrchestrationCompletion(Events[0].CorrelationId);

        var queueChanges = loggerProvider.Entries
            .Where(e => e.Message.StartsWith("Job added to queue", StringComparison.Ordinal)
                || e.Message.StartsWith("Job removed from queue", StringComparison.Ordinal))
            .ToList();

        queueChanges.ShouldNotBeEmpty();
        queueChanges.ShouldAllBe(e => e.Level == LogLevel.Debug);
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<(LogLevel Level, string Message)> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(this);

        public void Dispose()
        {
        }

        private sealed class RecordingLogger(RecordingLoggerProvider provider) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                provider.Entries.Enqueue((logLevel, formatter(state, exception)));
        }
    }
}
