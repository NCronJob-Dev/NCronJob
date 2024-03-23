using System.Collections.Concurrent;
using System.Threading.Channels;
using LinkDotNet.NCronJob;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace NCronJob.Tests;

public sealed class NCronJobIntegrationTests : JobIntegrationBase
{
    [Fact]
    public async Task CronJobThatIsScheduledEveryMinuteShouldBeExecuted()
    {
        var fakeTimer = TimeProviderFactory.GetTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob();
        ServiceCollection.AddCronJob<SimpleJob>(p => p.CronExpression = "* * * * *");
        await using var provider = ServiceCollection.BuildServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task AdvancingTheWholeTimeShouldHaveTenEntries()
    {
        var fakeTimer = TimeProviderFactory.GetTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob();
        ServiceCollection.AddCronJob<SimpleJob>(p => p.CronExpression = "* * * * *");
        await using var provider = ServiceCollection.BuildServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        fakeTimer.Advance(TimeSpan.FromMinutes(10));
        var jobFinished = await WaitForJobsOrTimeout(10);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task EachJobRunHasItsOwnScope()
    {
        var fakeTimer = TimeProviderFactory.GetTimeProvider();
        var storage = new Storage();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddSingleton(storage);
        ServiceCollection.AddScoped<GuidGenerator>();
        ServiceCollection.AddNCronJob();
        ServiceCollection.AddCronJob<ScopedServiceJob>(p => p.CronExpression = "* * * * *");
        ServiceCollection.AddCronJob<ScopedServiceJob>(p => p.CronExpression = "* * * * *");
        await using var provider = ServiceCollection.BuildServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        await Task.WhenAll(GetCompletionJobs(2));
        storage.Guids.Count.ShouldBe(2);
        storage.Guids.Distinct().Count().ShouldBe(storage.Guids.Count);
    }

    [Fact]
    public async Task ExecuteAnInstantJob()
    {
        var fakeTimer = TimeProviderFactory.GetTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob();
        ServiceCollection.AddCronJob<SimpleJob>();
        await using var provider = ServiceCollection.BuildServiceProvider();
        provider.GetRequiredService<IInstantJobRegistry>().AddInstantJob<SimpleJob>();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task CronJobShouldPassDownParameter()
    {
        var fakeTimer = TimeProviderFactory.GetTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob();
        ServiceCollection.AddCronJob<ParameterJob>(p =>
        {
            p.CronExpression = "* * * * *";
            p.Parameter = "Hello World";
        });
        await using var provider = ServiceCollection.BuildServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        var content = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        content.ShouldBe("Hello World");
    }

    [Fact]
    public async Task InstantJobShouldGetParameter()
    {
        var fakeTimer = TimeProviderFactory.GetTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob();
        ServiceCollection.AddCronJob<ParameterJob>();
        await using var provider = ServiceCollection.BuildServiceProvider();
        provider.GetRequiredService<IInstantJobRegistry>().AddInstantJob<ParameterJob>("Hello World");

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        var content = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        content.ShouldBe("Hello World");
    }

    [Fact]
    public async Task CronJobThatIsScheduledEverySecondShouldBeExecuted()
    {
        var fakeTimer = TimeProviderFactory.GetTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob();
        ServiceCollection.AddCronJob<SimpleJob>(p =>
        {
            p.EnableSecondPrecision = true;
            p.CronExpression = "* * * * * *";
        });
        await using var provider = ServiceCollection.BuildServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        fakeTimer.Advance(TimeSpan.FromSeconds(3));
        var jobFinished = await WaitForJobsOrTimeout(2);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task CanRunSecondPrecisionAndMinutePrecisionJobs()
    {
        var fakeTimer = TimeProviderFactory.GetTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob();
        ServiceCollection.AddCronJob<SimpleJob>(p =>
        {
            p.EnableSecondPrecision = true;
            p.CronExpression = "* * * * * *";
        });
        ServiceCollection.AddCronJob<SimpleJob>(p =>
        {
            p.CronExpression = "* * * * *";
        });
        await using var provider = ServiceCollection.BuildServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        fakeTimer.Advance(TimeSpan.FromSeconds(61));
        var jobFinished = await WaitForJobsOrTimeout(61);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task LongRunningJobShouldNotBlockSchedulerWithIsolationLevelTask()
    {
        var fakeTimer = TimeProviderFactory.GetTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob();
        ServiceCollection.AddCronJob<LongRunningJob>(p =>
        {
            p.CronExpression = "* * * * *";
            p.IsolationLevel = IsolationLevel.NewTask;
        });
        ServiceCollection.AddCronJob<SimpleJob>(p => p.CronExpression = "* * * * *");
        await using var provider = ServiceCollection.BuildServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task LongRunningJobBlocksSchedulerWithoutIsolationLevelTask()
    {
        var fakeTimer = TimeProviderFactory.GetTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob();
        ServiceCollection.AddCronJob<LongRunningJob>(p => p.CronExpression = "* * * * *");
        ServiceCollection.AddCronJob<SimpleJob>(p => p.CronExpression = "* * * * *");
        await using var provider = ServiceCollection.BuildServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        // Advancing the timer will lead to a synchronous blocking of the scheduler
        _ = Task.Run(() => fakeTimer.Advance(TimeSpan.FromMinutes(1)));
        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeFalse();
    }

    [Fact]
    public async Task NotRegisteredJobShouldNotAbortOtherRuns()
    {
        var fakeTimer = TimeProviderFactory.GetTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob();
        ServiceCollection.AddCronJob<SimpleJob>(p => p.CronExpression = "* * * * *");
        await using var provider = ServiceCollection.BuildServiceProvider();
        provider.GetRequiredService<IInstantJobRegistry>().AddInstantJob<ParameterJob>();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
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
        public async Task RunAsync(JobExecutionContext context, CancellationToken token)
        {
            await writer.WriteAsync(true, token);
        }
    }

    private sealed class LongRunningJob : IJob
    {
        public Task RunAsync(JobExecutionContext context, CancellationToken token)
        {
            Task.Delay(10000, token).GetAwaiter().GetResult();
            return Task.CompletedTask;
        }
    }

    private sealed class ScopedServiceJob(ChannelWriter<object> writer, Storage storage, GuidGenerator guidGenerator) : IJob
    {
        public async Task RunAsync(JobExecutionContext context, CancellationToken token)
        {
            storage.Guids.Add(guidGenerator.NewGuid);
            await writer.WriteAsync(true, token);
        }
    }

    private sealed class ParameterJob(ChannelWriter<object> writer) : IJob
    {
        public async Task RunAsync(JobExecutionContext context, CancellationToken token)
        {
            await writer.WriteAsync(context.Parameter!, token);
        }
    }
}
