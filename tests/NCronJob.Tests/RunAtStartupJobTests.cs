using System.Diagnostics.CodeAnalysis;
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
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddNCronJob(s => s.AddJob<DummyJob>().RunAtStartup());
        });

        using var app = BuildApp(builder);

        Func<Task> act = async () => await RunApp(app);

        await act.ShouldThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UseNCronJobShouldTriggerStartupJobs()
    {
        var builder = Host.CreateDefaultBuilder();
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
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddNCronJob(nBuilder);
        });

        using var app = BuildApp(builder);

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(app.Services);

        await app.UseNCronJobAsync();
        await RunApp(app);

        subscription.Dispose();

        events.Count(e => e.State == ExecutionState.Running).ShouldBe(1);
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
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddNCronJob(s => s.AddJob<DummyJob>().RunAtStartup());
            services.AddHostedService<StartingService>();
        });

        using var app = BuildApp(builder);

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(app.Services);

        await app.UseNCronJobAsync();
        await RunApp(app);

        Storage.Entries[0].ShouldBe("DummyJob - Parameter: ");
        Storage.Entries[1].ShouldBe("StartingService");
        Storage.Entries.Count.ShouldBe(2);

        var orchestrationId = events[0].CorrelationId;

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        var filteredEvents = events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldBeInstantThenCompleted<DummyJob>();
    }

    [Theory]
    [MemberData(nameof(CrashingCronAndForgivingRunAtStartupBuilders))]
    public async Task StartupJobThatThrowsShouldNotPreventHostFromStarting(Action<NCronJobOptionBuilder> nBuilder)
    {
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddNCronJob(nBuilder);
        });

        using var app = BuildApp(builder);

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(app.Services);

        await app.UseNCronJobAsync();
        await RunApp(app);

        Storage.Entries[0].ShouldBe("ExceptionHandler");
        Storage.Entries.Count.ShouldBe(1);

        var orchestrationId = events[0].CorrelationId;

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        var filteredEvents = events.FilterByOrchestrationId(orchestrationId);
        filteredEvents.ShouldBeInstantThenFaultedDuringRun<FailingJob>();
    }

    [Theory]
    [MemberData(nameof(CrashingCronAndNonForgivingRunAtStartupBuilders))]
    public async Task StartupJobCanBeConfiguredToPreventHostFromStartingOnFailure(Action<NCronJobOptionBuilder> nBuilder)
    {
        var builder = Host.CreateDefaultBuilder();
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

    private IHost BuildApp(IHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton(Storage);
            services.Replace(new ServiceDescriptor(typeof(TimeProvider), FakeTimer));
        });

        return builder.Build();
    }

    [SuppressMessage("Major Code Smell", "S108:Nested blocks of code should not be left empty", Justification = "On purpose")]
    private static async Task RunApp(IHost app, TimeSpan? runtime = null)
    {
        using var cts = new CancellationTokenSource(runtime ?? TimeSpan.FromSeconds(1));
        try
        {
            await app.RunAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
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
