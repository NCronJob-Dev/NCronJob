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
        var storage = new Storage();
        builder.ConfigureServices(services =>
        {
            services.AddNCronJob(s => s.AddJob<SimpleJob>().RunAtStartup());
            services.AddSingleton(_ => storage);
        });

        using var app = builder.Build();

        Func<Task> act = async () => await RunApp(app);

        await act.ShouldThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UseNCronJobShouldTriggerStartupJobs()
    {
        var builder = Host.CreateDefaultBuilder();
        var storage = new Storage();
        builder.ConfigureServices(services =>
        {
            services.AddNCronJob(s => s.AddJob<SimpleJob>().RunAtStartup());
            services.AddSingleton(_ => storage);
        });

        using var app = BuildApp(builder);

        await app.UseNCronJobAsync();

        storage.Entries[0].ShouldBe("SimpleJob");
        storage.Entries.Count.ShouldBe(1);
    }

    [Theory]
    [MemberData(nameof(CronAndRunAtStartupBuilders))]
    public async Task StartupJobsShouldOnlyRunOnceWhenAlsoConfiguredAsCron(Action<NCronJobOptionBuilder> nBuilder)
    {
        var builder = Host.CreateDefaultBuilder();
        var storage = new Storage();
        builder.ConfigureServices(services =>
        {
            services.AddSingleton(_ => storage);
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
                s.AddJob<SimpleJob>(jo => jo.WithCronExpression(Cron.AtMinute5));
                s.AddJob<SimpleJob>().RunAtStartup();
            }
        },
        {
            s =>
            {
                s.AddJob<SimpleJob>().RunAtStartup();
                s.AddJob<SimpleJob>(jo => jo.WithCronExpression(Cron.AtMinute5));
            }
        },
        {
            s => s.AddJob<SimpleJob>(jo => jo.WithCronExpression(Cron.AtMinute5)).RunAtStartup()
        },
    };

    [Fact]
    public async Task ShouldStartStartupJobsBeforeApplicationIsSpunUp()
    {
        var builder = Host.CreateDefaultBuilder();
        var storage = new Storage();
        builder.ConfigureServices(services =>
        {
            services.AddNCronJob(s => s.AddJob<SimpleJob>().RunAtStartup());
            services.AddSingleton(_ => storage);
            services.AddHostedService<StartingService>();
        });

        using var app = BuildApp(builder);

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(app.Services);

        await app.UseNCronJobAsync();
        await RunApp(app);

        storage.Entries[0].ShouldBe("SimpleJob");
        storage.Entries[1].ShouldBe("StartingService");
        storage.Entries.Count.ShouldBe(2);

        Guid orchestrationId = events.First().CorrelationId;

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        events[0].State.ShouldBe(ExecutionState.OrchestrationStarted);
        events[1].State.ShouldBe(ExecutionState.NotStarted);
        events[2].State.ShouldBe(ExecutionState.Initializing);
        events[3].State.ShouldBe(ExecutionState.Running);
        events[4].State.ShouldBe(ExecutionState.Completing);
        events[5].State.ShouldBe(ExecutionState.Completed);
        events[6].State.ShouldBe(ExecutionState.OrchestrationCompleted);
        events.Count.ShouldBe(7);
        events.ShouldAllBe(e => e.CorrelationId == orchestrationId);
    }

    [Fact]
    public async Task StartupJobThatThrowsShouldNotPreventHostFromStarting()
    {
        var builder = Host.CreateDefaultBuilder();
        var storage = new Storage();
        builder.ConfigureServices(services =>
        {
            services.AddNCronJob(s =>
            {
                s.AddJob<FailingJob>().RunAtStartup();
                s.AddExceptionHandler<ExceptionHandler>();
            });
            services.AddSingleton(_ => storage);
        });

        using var app = BuildApp(builder);

        (IDisposable subscription, IList<ExecutionProgress> events) = RegisterAnExecutionProgressSubscriber(app.Services);

        await app.UseNCronJobAsync();
        await RunApp(app);

        storage.Entries[0].ShouldBe("ExceptionHandler");
        storage.Entries.Count.ShouldBe(1);

        Guid orchestrationId = events.First().CorrelationId;

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        events[0].State.ShouldBe(ExecutionState.OrchestrationStarted);
        events[1].State.ShouldBe(ExecutionState.NotStarted);
        events[2].State.ShouldBe(ExecutionState.Initializing);
        events[3].State.ShouldBe(ExecutionState.Running );
        events[4].State.ShouldBe(ExecutionState.Faulted);
        events[5].State.ShouldBe(ExecutionState.OrchestrationCompleted);
        events.Count.ShouldBe(6);
        events.ShouldAllBe(e => e.CorrelationId == orchestrationId);
    }

    [Fact]
    public async Task StartupJobCanBeConfiguredToPreventHostFromStartingOnFailure()
    {
        var builder = Host.CreateDefaultBuilder();
        var storage = new Storage();
        builder.ConfigureServices(services =>
        {
            services.AddNCronJob(s =>
            {
                s.AddJob<FailingJob>().RunAtStartup(shouldCrashOnFailure: true);
                s.AddExceptionHandler<ExceptionHandler>();
            });
            services.AddSingleton(_ => storage);
        });

        using var app = BuildApp(builder);

        var exc = await Should.ThrowAsync<InvalidOperationException>(app.UseNCronJobAsync);

        exc.Message.ShouldStartWith(
            $"At least one of the startup jobs failed{Environment.NewLine}- System.InvalidOperationException: Failed",
            Case.Sensitive);

        storage.Entries[0].ShouldBe("ExceptionHandler");
        storage.Entries.Count.ShouldBe(1);
    }

    private IHost BuildApp(IHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
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

    private sealed class SimpleJob : IJob
    {
        private readonly Storage storage;

        public SimpleJob(Storage storage) => this.storage = storage;

        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            storage.Add("SimpleJob");
            return Task.CompletedTask;
        }
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
