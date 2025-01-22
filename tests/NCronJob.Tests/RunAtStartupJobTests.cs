using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace NCronJob.Tests;

public class RunAtStartupJobTests : JobIntegrationBase
{
    private const string AtMinute5 = "5 * * * *";

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

#pragma warning disable IDISP013 // Await in using
        Func<Task> act = () => RunApp(app);
#pragma warning restore IDISP013 // Await in using

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

        storage.Content.Count.ShouldBe(1);
        storage.Content[0].ShouldBe("SimpleJob");
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

        Assert.Equal(1, events.Count(e => e.State == ExecutionState.Running));
    }

    public static TheoryData<Action<NCronJobOptionBuilder>> CronAndRunAtStartupBuilders = new()
    {
        {
            s =>
            {
                s.AddJob<SimpleJob>(jo => jo.WithCronExpression(AtMinute5));
                s.AddJob<SimpleJob>().RunAtStartup();
            }
        },
        {
            s =>
            {
                s.AddJob<SimpleJob>().RunAtStartup();
                s.AddJob<SimpleJob>(jo => jo.WithCronExpression(AtMinute5));
            }
        },
        {
            s => s.AddJob<SimpleJob>(jo => jo.WithCronExpression(AtMinute5)).RunAtStartup()
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

        storage.Content.Count.ShouldBe(2);
        storage.Content[0].ShouldBe("SimpleJob");
        storage.Content[1].ShouldBe("StartingService");

        Guid orchestrationId = events.First().CorrelationId;

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        Assert.All(events, e => Assert.Equal(orchestrationId, e.CorrelationId));
        Assert.Equal(ExecutionState.OrchestrationStarted, events[0].State);
        Assert.Equal(ExecutionState.NotStarted, events[1].State);
        Assert.Equal(ExecutionState.Initializing, events[2].State);
        Assert.Equal(ExecutionState.Running, events[3].State);
        Assert.Equal(ExecutionState.Completing, events[4].State);
        Assert.Equal(ExecutionState.Completed, events[5].State);
        Assert.Equal(ExecutionState.OrchestrationCompleted, events[6].State);
        Assert.Equal(7, events.Count);
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

        storage.Content.Count.ShouldBe(1);
        storage.Content[0].ShouldBe("ExceptionHandler");

        Guid orchestrationId = events.First().CorrelationId;

        await WaitForOrchestrationCompletion(events, orchestrationId);

        subscription.Dispose();

        Assert.All(events, e => Assert.Equal(orchestrationId, e.CorrelationId));
        Assert.Equal(ExecutionState.OrchestrationStarted, events[0].State);
        Assert.Equal(ExecutionState.NotStarted, events[1].State);
        Assert.Equal(ExecutionState.Initializing, events[2].State);
        Assert.Equal(ExecutionState.Running, events[3].State);
        Assert.Equal(ExecutionState.Faulted, events[4].State);
        Assert.Equal(ExecutionState.OrchestrationCompleted, events[5].State);
        Assert.Equal(6, events.Count);
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

        var exc = await Assert.ThrowsAsync<InvalidOperationException>(app.UseNCronJobAsync);

        Assert.StartsWith(
            $"At least one of the startup jobs failed{Environment.NewLine}- System.InvalidOperationException: Failed",
            exc.Message,
            StringComparison.Ordinal);

        storage.Content.Count.ShouldBe(1);
        storage.Content[0].ShouldBe("ExceptionHandler");
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

    private sealed class Storage
    {
#if NET9_0_OR_GREATER
        private readonly Lock locker = new();
#else
        private readonly object locker = new();
#endif
        public List<string> Content { get; private set; } = [];

        public void Add(string content)
        {
            lock (locker)
            {
                Content.Add(content);
            }
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
