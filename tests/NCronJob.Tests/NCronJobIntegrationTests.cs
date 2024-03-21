using System.Collections.Concurrent;
using System.Threading.Channels;
using LinkDotNet.NCronJob;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace NCronJob.Tests;

public sealed class NCronJobIntegrationTests : IDisposable
{
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
        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
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
        var jobFinished = await WaitForJobsOrTimeout(10);
        jobFinished.ShouldBeTrue();
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
        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
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

    [Fact]
    public async Task CronJobThatIsScheduledEverySecondShouldBeExecuted()
    {
        var fakeTimer = TimeProviderFactory.GetAutoTickingTimeProvider();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<TimeProvider>(fakeTimer);
        serviceCollection.AddNCronJob(p => p.EnableSecondPrecision = true);
        serviceCollection.AddCronJob<SimpleJob>(p => p.CronExpression = "* * * * * *");
        serviceCollection.AddScoped<ChannelWriter<object>>(_ => channel.Writer);
        var provider = serviceCollection.BuildServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(cancellationTokenSource.Token);

        fakeTimer.Advance(TimeSpan.FromSeconds(3));
        var jobFinished = await WaitForJobsOrTimeout(2);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task LongRunningJobShouldNotBlockSchedulerWithIsolationLevelTask()
    {
        var fakeTimer = TimeProviderFactory.GetAutoTickingTimeProvider();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<TimeProvider>(fakeTimer);
        serviceCollection.AddNCronJob();
        serviceCollection.AddCronJob<LongRunningJob>(p =>
        {
            p.CronExpression = "* * * * *";
            p.IsolationLevel = IsolationLevel.NewTask;
        });
        serviceCollection.AddCronJob<SimpleJob>(p => p.CronExpression = "* * * * *");

        serviceCollection.AddScoped<ChannelWriter<object>>(_ => channel.Writer);
        var provider = serviceCollection.BuildServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(cancellationTokenSource.Token);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task LongRunningJobBlocksSchedulerWithoutIsolationLevelTask()
    {
        var fakeTimer = TimeProviderFactory.GetAutoTickingTimeProvider();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<TimeProvider>(fakeTimer);
        serviceCollection.AddNCronJob();
        serviceCollection.AddCronJob<LongRunningJob>(p => p.CronExpression = "* * * * *");
        serviceCollection.AddCronJob<SimpleJob>(p => p.CronExpression = "* * * * *");
        serviceCollection.AddScoped<ChannelWriter<object>>(_ => channel.Writer);
        var provider = serviceCollection.BuildServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(cancellationTokenSource.Token);

        // Advancing the timer will lead to a synchronous blocking of the scheduler
        _ = Task.Run(() => fakeTimer.Advance(TimeSpan.FromMinutes(1)));
        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeFalse();
    }

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
    }

    private async Task<bool> WaitForJobsOrTimeout(int jobRuns)
    {
        using var timeoutTcs = new CancellationTokenSource(100);
        try
        {
            await Task.WhenAll(GetCompletionJobs(jobRuns, timeoutTcs.Token));
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private IEnumerable<Task> GetCompletionJobs(int expectedJobCount, CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < expectedJobCount; i++)
        {
            yield return channel.Reader.ReadAsync(cancellationToken).AsTask();
        }
    }

    private sealed class GuidGenerator
    {
        public Guid NewGuid { get; } = Guid.NewGuid();
    }

    private sealed class Storage
    {
        public ConcurrentBag<Guid> Guids { get; } = [];
    }

    private sealed class SimpleJob(ChannelWriter<object> writer) : IJob
    {
        public async Task Run(JobExecutionContext context, CancellationToken token = default)
        {
            await writer.WriteAsync(true, token);
        }
    }

    private sealed class LongRunningJob : IJob
    {
        public Task Run(JobExecutionContext context, CancellationToken token = default)
        {
            Task.Delay(10000, token).GetAwaiter().GetResult();
            return Task.CompletedTask;
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
}
