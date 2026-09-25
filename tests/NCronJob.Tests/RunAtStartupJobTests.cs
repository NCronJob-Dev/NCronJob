using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace NCronJob.Tests;

public class RunAtStartupJobTests : JobIntegrationBase
{
    [Fact]
    public async Task UseNCronJobIsMandatoryWhenStartupJobsAreDefined()
    {
        var builder = new HostBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddNCronJob(s => s.AddJob<DummyJob>().RunAtStartup());
        });

        using var app = BuildApp(builder);

        var act = async () => await RunApp(app);

        await act.ShouldThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UseNCronJobShouldTriggerStartupJobs()
    {
        var builder = new HostBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddNCronJob(s => s.AddJob<DummyJob>().RunAtStartup());
        });

        using var app = BuildApp(builder);

        await app.UseNCronJobAsync();

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: ");
        Storage.Entries.Count.ShouldBe(1);
    }

    [Theory]
    [MemberData(nameof(CronAndRunAtStartupBuilders))]
    public async Task StartupJobsShouldOnlyRunOnceWhenAlsoConfiguredAsCron(Action<NCronJobOptionBuilder> nBuilder)
    {
        var builder = new HostBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddNCronJob(nBuilder);
        });

        using var app = BuildApp(builder);

        using var monitor = CreateExecutionProgressMonitor(app.Services);

        await app.UseNCronJobAsync();
        await RunApp(app);

        monitor.Events.Count(e => e.State == ExecutionState.Running).ShouldBe(1);
    }

    public static TheoryData<Action<NCronJobOptionBuilder>> CronAndRunAtStartupBuilders = new()
    {
        {
            s =>
            {
                s.AddJob<DummyJob>(jo => jo.WithCronExpression(Cron.AtMinute5));
                s.AddJob<DummyJob>().RunAtStartup();
            }
        },
        {
            s =>
            {
                s.AddJob<DummyJob>().RunAtStartup();
                s.AddJob<DummyJob>(jo => jo.WithCronExpression(Cron.AtMinute5));
            }
        },
        {
            s => s.AddJob<DummyJob>(jo => jo.WithCronExpression(Cron.AtMinute5)).RunAtStartup()
        },
    };

    [Fact]
    public async Task ShouldStartStartupJobsBeforeApplicationIsSpunUp()
    {
        var builder = new HostBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddNCronJob(s => s.AddJob<DummyJob>().RunAtStartup());
            services.AddHostedService<StartingService>();
        });

        using var app = BuildApp(builder);

        using var monitor = CreateExecutionProgressMonitor(app.Services);

        await app.UseNCronJobAsync();
        await RunApp(app);

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: ");
        Storage.Entries[1].ShouldBe("StartingService");
        Storage.Entries.Count.ShouldBe(2);

        var orchestrationId = monitor.Events[0].CorrelationId;

        await monitor.WaitForStateAsync(orchestrationId, ExecutionState.OrchestrationCompleted);

        var filteredEvents = monitor.Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldBeInstantThenCompleted<DummyJob>();
    }

    [Theory]
    [MemberData(nameof(CrashingCronAndForgivingRunAtStartupBuilders))]
    public async Task StartupJobThatThrowsShouldNotPreventHostFromStarting(Action<NCronJobOptionBuilder> nBuilder)
    {
        var builder = new HostBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddNCronJob(nBuilder);
        });

        using var app = BuildApp(builder);

        using var monitor = CreateExecutionProgressMonitor(app.Services);

        await app.UseNCronJobAsync();
        await RunApp(app);

        Storage.Entries[0].ShouldBe("ExceptionHandler");
        Storage.Entries.Count.ShouldBe(1);

        var orchestrationId = monitor.Events[0].CorrelationId;

        await monitor.WaitForStateAsync(orchestrationId, ExecutionState.OrchestrationCompleted);

        var filteredEvents = monitor.Events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldBeInstantThenFaultedDuringRun<FailingJob>();
    }

    [Theory]
    [MemberData(nameof(CrashingCronAndNonForgivingRunAtStartupBuilders))]
    public async Task StartupJobCanBeConfiguredToPreventHostFromStartingOnFailure(Action<NCronJobOptionBuilder> nBuilder)
    {
        var builder = new HostBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddNCronJob(nBuilder);
        });

        using var app = BuildApp(builder);

        var exc = await Should.ThrowAsync<InvalidOperationException>(app.UseNCronJobAsync);

        exc.Message.ShouldStartWith(
            $"At least one of the startup jobs failed{Environment.NewLine}- System.InvalidOperationException: Failed",
            Case.Sensitive);

        Storage.Entries[0].ShouldBe("ExceptionHandler");
        Storage.Entries.Count.ShouldBe(1);
    }

    public static TheoryData<Action<NCronJobOptionBuilder>> CrashingCronAndNonForgivingRunAtStartupBuilders = new()
    {
        {
            s =>
            {
                s.AddJob<FailingJob>().RunAtStartup(shouldCrashOnFailure: true);
                s.AddExceptionHandler<ExceptionHandler>();
            }
        },
        {
            s =>
            {
                s.AddJob<FailingJob>(j => j.RunAtStartup());
                s.AddExceptionHandler<ExceptionHandler>();
            }
        },
    };

    public static TheoryData<Action<NCronJobOptionBuilder>> CrashingCronAndForgivingRunAtStartupBuilders = new()
    {
        {
            s =>
            {
                s.AddJob<FailingJob>().RunAtStartup();
                s.AddExceptionHandler<ExceptionHandler>();
            }
        },
        {
            s =>
            {
                s.AddJob<FailingJob>(j => j.RunAtStartup(shouldCrashOnFailure: false));
                s.AddExceptionHandler<ExceptionHandler>();
            }
        },
    };

    private IHost BuildApp(HostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton(Storage);
            services.Replace(new ServiceDescriptor(typeof(TimeProvider), FakeTimer));
        });

        return builder.Build();
    }

    private static async Task RunApp(IHost app)
    {
        await app.StartAsync(TestContext.Current.CancellationToken);
        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    private sealed class StartingService : IHostedService
    {
        private readonly Storage storage;

        public StartingService(Storage storage) => this.storage = storage;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            storage.Add("StartingService");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FailingJob : IJob
    {
        public Task RunAsync(IJobExecutionContext context, CancellationToken token) => throw new InvalidOperationException("Failed");
    }

    private sealed class ExceptionHandler : IExceptionHandler
    {
        private readonly Storage storage;

        public ExceptionHandler(Storage storage) => this.storage = storage;


        public Task<bool> TryHandleAsync(IJobExecutionContext jobExecutionContext, Exception exception, CancellationToken cancellationToken)
        {
            storage.Add("ExceptionHandler");
            return Task.FromResult(true);
        }
    }
}
