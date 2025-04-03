using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace NCronJob.Tests;

public class TriggerTypeTests : JobIntegrationBase
{
    [Fact]
    public async Task StartupJobsShouldHaveCorrectTriggerType()
    {
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddNCronJob(s => s.AddJob<TriggerTypeJob>().RunAtStartup());
            services.AddSingleton(Storage);
            services.Replace(new ServiceDescriptor(typeof(TimeProvider), FakeTimer));
        });

        using var app = builder.Build();

        await app.UseNCronJobAsync();

        Storage.Entries.Count.ShouldBe(1);
        Storage.Entries[0].ShouldBe($"TriggerType: {TriggerType.Startup}");
    }

    [Fact]
    public async Task CronJobsShouldHaveCorrectTriggerType()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<TriggerTypeJob>(p => p.WithCronExpression(Cron.AtEveryMinute)));

        await StartNCronJob(startMonitoringEvents: true);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));

        var orchestrationId = Events[0].CorrelationId;
        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries.Count.ShouldBe(1);
        Storage.Entries[0].ShouldBe($"TriggerType: {TriggerType.Cron}");
    }

    [Fact]
    public async Task InstantJobsShouldHaveCorrectTriggerType()
    {
        ServiceCollection.AddNCronJob(n => n.AddJob<TriggerTypeJob>());

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>()
            .RunInstantJob<TriggerTypeJob>(token: CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries.Count.ShouldBe(1);
        Storage.Entries[0].ShouldBe($"TriggerType: {TriggerType.Instant}");
    }

    [Fact]
    public async Task DependentJobsShouldHaveCorrectTriggerType()
    {
        ServiceCollection.AddNCronJob(n =>
        {
            n.AddJob<PrincipalJob>().ExecuteWhen(success: s => s.RunJob<TriggerTypeJob>());
        });

        await StartNCronJob(startMonitoringEvents: true);

        var orchestrationId = ServiceProvider.GetRequiredService<IInstantJobRegistry>()
            .RunInstantJob<PrincipalJob>(token: CancellationToken);

        await WaitForOrchestrationCompletion(orchestrationId, stopMonitoringEvents: true);

        Storage.Entries.Count.ShouldBe(2);
        Storage.Entries[0].ShouldBe("PrincipalJob: Success");
        Storage.Entries[1].ShouldBe($"TriggerType: {TriggerType.Dependent}");
    }

    private sealed class TriggerTypeJob : IJob
    {
        private readonly Storage storage;

        public TriggerTypeJob(Storage storage)
        {
            this.storage = storage;
        }

        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            storage.Add($"TriggerType: {context.TriggerType}");
            return Task.CompletedTask;
        }
    }

    private sealed class PrincipalJob : IJob
    {
        private readonly Storage storage;

        public PrincipalJob(Storage storage)
        {
            this.storage = storage;
        }

        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            storage.Add("PrincipalJob: Success");
            return Task.CompletedTask;
        }
    }
}
