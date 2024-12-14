using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
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
        using var app = builder.Build();

        await app.UseNCronJobAsync();

        storage.Content.Count.ShouldBe(1);
        storage.Content[0].ShouldBe("SimpleJob");
    }

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
        using var app = builder.Build();

        await app.UseNCronJobAsync();
        await RunApp(app);

        storage.Content.Count.ShouldBe(2);
        storage.Content[0].ShouldBe("SimpleJob");
        storage.Content[1].ShouldBe("StartingService");
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
        using var app = builder.Build();

        await app.UseNCronJobAsync();
        await RunApp(app);

        storage.Content.Count.ShouldBe(1);
        storage.Content[0].ShouldBe("ExceptionHandler");
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
#if NET8_0
        private readonly object locker = new();
#else
        private readonly Lock locker = new();
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

    private sealed class SimpleJob: IJob
    {
        private readonly Storage storage;

        public SimpleJob(Storage storage) => this.storage = storage;

        public Task RunAsync(IJobExecutionContext context, CancellationToken token)
        {
            storage.Add("SimpleJob");
            return Task.CompletedTask;
        }
    }

    private sealed class FailingJob: IJob
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
