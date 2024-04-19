using System.Collections.Concurrent;
using System.Threading.Channels;
using LinkDotNet.NCronJob;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using TimeProviderExtensions;

namespace NCronJob.Tests;

public sealed class NCronJobIntegrationTests : JobIntegrationBase
{
    [Fact]
    public async Task CronJobThatIsScheduledEveryMinuteShouldBeExecuted()
    {
        var fakeTimer = TimeProviderFactory.GetTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(p => p.WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task AdvancingTheWholeTimeShouldHaveTenEntries()
    {
        var fakeTimer = TimeProviderFactory.GetTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(p => p.WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var jobFinished = await WaitForJobsOrTimeout(10);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task EachJobRunHasItsOwnScope()
    {
        var fakeTimer = new ManualTimeProvider();
        var storage = new Storage();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddSingleton(storage);
        ServiceCollection.AddScoped<GuidGenerator>();
        ServiceCollection.AddNCronJob(n => n.AddJob<ScopedServiceJob>(
            p => p.WithCronExpression("* * * * *").And.WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();

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
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>());
        var provider = CreateServiceProvider();
        provider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<SimpleJob>();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task CronJobShouldPassDownParameter()
    {
        var fakeTimer = TimeProviderFactory.GetTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(n => n.AddJob<ParameterJob>(p => p.WithCronExpression("* * * * *").WithParameter("Hello World")));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var content = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        content.ShouldBe("Hello World");
    }

    [Fact]
    public async Task InstantJobShouldGetParameter()
    {
        var fakeTimer = TimeProviderFactory.GetTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(n => n.AddJob<ParameterJob>());
        var provider = CreateServiceProvider();
        provider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<ParameterJob>("Hello World");

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var content = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        content.ShouldBe("Hello World");
    }

    [Fact]
    public async Task CronJobThatIsScheduledEverySecondShouldBeExecuted()
    {
        var fakeTimer = TimeProviderFactory.GetTimeProvider(TimeSpan.FromSeconds(1));
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(p => p.WithCronExpression("* * * * * *", true)));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var jobFinished = await WaitForJobsOrTimeout(2);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task CanRunSecondPrecisionAndMinutePrecisionJobs()
    {
        var fakeTimer = TimeProviderFactory.GetTimeProvider(TimeSpan.FromSeconds(1));
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(
            p => p.WithCronExpression("* * * * * *", true).And.WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var jobFinished = await WaitForJobsOrTimeout(61);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task LongRunningJobShouldNotBlockScheduler()
    {
        var fakeTimer = TimeProviderFactory.GetTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(n => n
                .AddJob<LongRunningJob>(p => p.WithCronExpression("* * * * *"))
                .AddJob<SimpleJob>(p => p.WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task NotRegisteredJobShouldNotAbortOtherRuns()
    {
        var fakeTimer = TimeProviderFactory.GetTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(n => n.AddJob<SimpleJob>(p => p.WithCronExpression("* * * * *")));
        ServiceCollection.AddTransient<ParameterJob>();
        var provider = CreateServiceProvider();
        provider.GetRequiredService<IInstantJobRegistry>().RunInstantJob<ParameterJob>();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        var jobFinished = await WaitForJobsOrTimeout(1);
        jobFinished.ShouldBeTrue();
    }

    [Fact]
    public void ThrowIfJobWithDependenciesIsNotRegistered()
    {
        ServiceCollection
            .AddNCronJob(n => n.AddJob<JobWithDependency>(p => p.WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();

        Assert.Throws<InvalidOperationException>(() =>
        {
            using var executor = new JobExecutor(provider, NullLogger<JobExecutor>.Instance);
            executor.RunJob(new RegistryEntry(typeof(JobWithDependency), new JobExecutionContext(null), null), CancellationToken.None);

        });
    }

    [Fact]
    public async Task NotThrowIfJobWithDependenciesRegistered()
    {
        ServiceCollection
            .AddSingleton<GuidGenerator>()
            .AddNCronJob(n => n.AddJob<JobWithDependency>(p => p.WithCronExpression("* * * * *")));
        var provider = CreateServiceProvider();

        using var executor = new JobExecutor(provider, NullLogger<JobExecutor>.Instance);
        executor.RunJob(new RegistryEntry(typeof(JobWithDependency), new JobExecutionContext(null), null), CancellationToken.None);

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
            => await writer.WriteAsync(true, token);
    }

    private sealed class LongRunningJob : IJob
    {
        public Task RunAsync(JobExecutionContext context, CancellationToken token)
        {
            Task.Delay(1000, token).GetAwaiter().GetResult();
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
            => await writer.WriteAsync(context.Parameter!, token);
    }

    private sealed class JobWithDependency(ChannelWriter<object> writer, GuidGenerator guidGenerator) : IJob
    {
        public async Task RunAsync(JobExecutionContext context, CancellationToken token)
            => await writer.WriteAsync(guidGenerator.NewGuid, token);
    }
}
