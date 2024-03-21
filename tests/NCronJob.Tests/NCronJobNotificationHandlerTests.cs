using System.Threading.Channels;
using LinkDotNet.NCronJob;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace NCronJob.Tests;

public class NCronJobNotificationHandlerTests : JobIntegrationBase
{
    [Fact]
    public async Task ShouldCallNotificationHandlerWhenJobIsDone()
    {
        var fakeTimer = TimeProviderFactory.GetAutoTickingTimeProvider();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<TimeProvider>(fakeTimer);
        serviceCollection.AddNCronJob();
        serviceCollection.AddCronJob<SimpleJob>(p => p.CronExpression = "* * * * *");
        serviceCollection.AddNotificationHandler<SimpleJobHandler, SimpleJob>();
        serviceCollection.AddScoped<ChannelWriter<object>>(_ => CommunicationChannel.Writer);
        await using var provider = serviceCollection.BuildServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        var message = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        message.ShouldBe("Foo");
    }

    [Fact]
    public async Task ShouldPassDownExceptionToNotificationHandler()
    {
        var fakeTimer = TimeProviderFactory.GetAutoTickingTimeProvider();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<TimeProvider>(fakeTimer);
        serviceCollection.AddNCronJob();
        serviceCollection.AddCronJob<ExceptionJob>(p => p.CronExpression = "* * * * *");
        serviceCollection.AddNotificationHandler<ExceptionHandler, ExceptionJob>();
        serviceCollection.AddScoped<ChannelWriter<object>>(_ => CommunicationChannel.Writer);
        await using var provider = serviceCollection.BuildServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        var message = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        message.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task HandlerThatThrowsExceptionShouldNotInfluenceOtherHandlers()
    {
        var fakeTimer = TimeProviderFactory.GetAutoTickingTimeProvider();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<TimeProvider>(fakeTimer);
        serviceCollection.AddNCronJob();
        serviceCollection.AddCronJob<SimpleJob>(p => p.CronExpression = "* * * * *");
        serviceCollection.AddNotificationHandler<HandlerThatThrowsException, ExceptionJob>();
        serviceCollection.AddNotificationHandler<SimpleJobHandler, SimpleJob>();
        serviceCollection.AddScoped<ChannelWriter<object>>(_ => CommunicationChannel.Writer);
        await using var provider = serviceCollection.BuildServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        var message = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        message.ShouldBe("Foo");
    }

    private sealed class SimpleJob : IJob
    {
        public Task Run(JobExecutionContext context, CancellationToken token = default)
        {
            context.Output = "Foo";
            return Task.CompletedTask;
        }
    }

    private sealed class ExceptionJob : IJob
    {
        public Task Run(JobExecutionContext context, CancellationToken token = default)
        {
            throw new InvalidOperationException();
        }
    }

    private sealed class SimpleJobHandler(ChannelWriter<object> writer) : IJobNotificationHandler<SimpleJob>
    {
        public Task HandleAsync(JobExecutionContext context, Exception? exception, CancellationToken cancellationToken)
        {
            return writer.WriteAsync(context.Output!, cancellationToken).AsTask();
        }
    }

    private sealed class ExceptionHandler(ChannelWriter<object> writer) : IJobNotificationHandler<ExceptionJob>
    {
        public Task HandleAsync(JobExecutionContext context, Exception? exception, CancellationToken cancellationToken)
        {
            return writer.WriteAsync(exception!, cancellationToken).AsTask();
        }
    }

    private sealed class HandlerThatThrowsException : IJobNotificationHandler<ExceptionJob>
    {
        public Task HandleAsync(JobExecutionContext context, Exception? exception, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException();
        }
    }
}
