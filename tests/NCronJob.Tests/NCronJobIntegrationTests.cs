using System.Collections.Concurrent;
using System.Threading.Channels;
using LinkDotNet.NCronJob;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace NCronJob.Tests;

public sealed class NCronJobIntegrationTests : IDisposable
{
    private static readonly Task TimeoutTask = Task.Delay(500);
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly Channel<object> channel = Channel.CreateUnbounded<object>();

    [Fact]
    public async Task CronJobThatIsScheduledEveryMinuteShouldBeExecuted()
    {
        var fakeTimer = TimeProviderFactory.GetAutoTickingTimeProvider();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<TimeProvider>(fakeTimer);
        serviceCollection.AddNCronJob();
        serviceCollection.AddCronJob<SimpleJob>(p => p.CronExpression = "* * * * *");
        serviceCollection.AddScoped<ChannelWriter<object>>(_ => channel.Writer);
        var provider = serviceCollection.BuildServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(cancellationTokenSource.Token);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        Task[] tasks = [..GetCompletionJobs(1), TimeoutTask];
        var winner = await Task.WhenAny(tasks);
        winner.ShouldNotBe(TimeoutTask);
    }

    [Fact]
    public async Task AdvancingTheWholeTimeShouldHaveTenEntries()
    {
        var fakeTimer = TimeProviderFactory.GetAutoTickingTimeProvider(TimeSpan.FromMinutes(1));
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<TimeProvider>(fakeTimer);
        serviceCollection.AddNCronJob();
        serviceCollection.AddCronJob<SimpleJob>(p => p.CronExpression = "* * * * *");
        serviceCollection.AddScoped<ChannelWriter<object>>(_ => channel.Writer);
        var provider = serviceCollection.BuildServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(cancellationTokenSource.Token);

        fakeTimer.Advance(TimeSpan.FromMinutes(10));
        Task[] tasks = [..GetCompletionJobs(10), TimeoutTask];
        var winner = await Task.WhenAny(tasks);
        winner.ShouldNotBe(TimeoutTask);
    }

    [Fact]
    public async Task EachJobRunHasItsOwnScope()
    {
        var fakeTimer = TimeProviderFactory.GetAutoTickingTimeProvider();
        var storage = new Storage();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<TimeProvider>(fakeTimer);
        serviceCollection.AddSingleton(storage);
        serviceCollection.AddScoped<GuidGenerator>();
        serviceCollection.AddNCronJob();
        serviceCollection.AddCronJob<ScopedServiceJob>(p => p.CronExpression = "* * * * *");
        serviceCollection.AddCronJob<ScopedServiceJob>(p => p.CronExpression = "* * * * *");
        serviceCollection.AddScoped<ChannelWriter<object>>(_ => channel.Writer);
        var provider = serviceCollection.BuildServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(cancellationTokenSource.Token);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        await Task.WhenAll(GetCompletionJobs(2));
        storage.Guids.Count.ShouldBe(2);
        storage.Guids.Distinct().Count().ShouldBe(storage.Guids.Count);
    }

    [Fact]
    public async Task ExecuteAnInstantJob()
    {
        var fakeTimer = TimeProviderFactory.GetAutoTickingTimeProvider();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<TimeProvider>(fakeTimer);
        serviceCollection.AddNCronJob();
        serviceCollection.AddCronJob<SimpleJob>();
        serviceCollection.AddScoped<ChannelWriter<object>>(_ => channel.Writer);
        var provider = serviceCollection.BuildServiceProvider();
        provider.GetRequiredService<IInstantJobRegistry>().AddInstantJob<SimpleJob>();

        await provider.GetRequiredService<IHostedService>().StartAsync(cancellationTokenSource.Token);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        Task[] tasks = [..GetCompletionJobs(2), TimeoutTask];
        var winner = await Task.WhenAny(tasks);
        winner.ShouldNotBe(TimeoutTask);
    }

    [Fact]
    public async Task CronJobShouldPassDownParameter()
    {
        var fakeTimer = TimeProviderFactory.GetAutoTickingTimeProvider();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<TimeProvider>(fakeTimer);
        serviceCollection.AddNCronJob();
        serviceCollection.AddCronJob<ParameterJob>(p =>
        {
            p.CronExpression = "* * * * *";
            p.Parameter = "Hello World";
        });
        serviceCollection.AddScoped<ChannelWriter<object>>(_ => channel.Writer);
        var provider = serviceCollection.BuildServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(cancellationTokenSource.Token);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        var content = await channel.Reader.ReadAsync(cancellationTokenSource.Token);
        content.ShouldBe("Hello World");
    }

    [Fact]
    public async Task InstantJobShouldGetParameter()
    {
        var fakeTimer = TimeProviderFactory.GetAutoTickingTimeProvider();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<TimeProvider>(fakeTimer);
        serviceCollection.AddNCronJob();
        serviceCollection.AddCronJob<ParameterJob>();
        serviceCollection.AddScoped<ChannelWriter<object>>(_ => channel.Writer);
        var provider = serviceCollection.BuildServiceProvider();
        provider.GetRequiredService<IInstantJobRegistry>().AddInstantJob<ParameterJob>("Hello World");

        await provider.GetRequiredService<IHostedService>().StartAsync(cancellationTokenSource.Token);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        var content = await channel.Reader.ReadAsync(cancellationTokenSource.Token);
        content.ShouldBe("Hello World");
    }

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
    }

    private sealed class GuidGenerator
    {
        public Guid NewGuid { get; set; } = Guid.NewGuid();
    }

    private sealed class Storage
    {
        public ConcurrentBag<Guid> Guids { get; } = new();
    }

    private sealed class SimpleJob(ChannelWriter<object> writer) : IJob
    {
        public async Task Run(JobExecutionContext context, CancellationToken token = default)
        {
            await writer.WriteAsync(true, token);
        }
    }

    private sealed class ScopedServiceJob(ChannelWriter<object> writer, Storage storage, GuidGenerator guidGenerator) : IJob
    {
        public async Task Run(JobExecutionContext context, CancellationToken token = default)
        {
            storage.Guids.Add(guidGenerator.NewGuid);
            await writer.WriteAsync(true, token);
        }
    }

    private sealed class ParameterJob(ChannelWriter<object> writer) : IJob
    {
        public async Task Run(JobExecutionContext context, CancellationToken token = default)
        {
            await writer.WriteAsync(context.Parameter!, token);
        }
    }

    private IEnumerable<Task> GetCompletionJobs(int expectedJobCount)
    {
        for (var i = 0; i < expectedJobCount; i++)
        {
            yield return channel.Reader.ReadAsync().AsTask();
        }
    }
}
