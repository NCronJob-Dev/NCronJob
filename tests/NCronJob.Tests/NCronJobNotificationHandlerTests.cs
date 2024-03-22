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
        var fakeTimer = TimeProviderFactory.GetTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob();
        ServiceCollection.AddCronJob<SimpleJob>(p => p.CronExpression = "* * * * *");
        ServiceCollection.AddNotificationHandler<SimpleJobHandler, SimpleJob>();
        await using var provider = ServiceCollection.BuildServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        var message = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        message.ShouldBe("Foo");
    }

    [Fact]
    public async Task ShouldPassDownExceptionToNotificationHandler()
    {
        var fakeTimer = TimeProviderFactory.GetTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob();
        ServiceCollection.AddCronJob<ExceptionJob>(p => p.CronExpression = "* * * * *");
        ServiceCollection.AddNotificationHandler<ExceptionHandler, ExceptionJob>();
        await using var provider = ServiceCollection.BuildServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        var message = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        message.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task HandlerThatThrowsExceptionShouldNotInfluenceOtherHandlers()
    {
        var fakeTimer = TimeProviderFactory.GetTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob();
        ServiceCollection.AddCronJob<SimpleJob>(p => p.CronExpression = "* * * * *");
        ServiceCollection.AddNotificationHandler<HandlerThatThrowsException, ExceptionJob>();
        ServiceCollection.AddNotificationHandler<SimpleJobHandler, SimpleJob>();
        await using var provider = ServiceCollection.BuildServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        var message = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        message.ShouldBe("Foo");
    }

    [Fact]
    public async Task HandlerThatThrowsExceptionInAsyncPartShouldNotInfluenceOtherHandlers()
    {
        var fakeTimer = TimeProviderFactory.GetTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob();
        ServiceCollection.AddCronJob<SimpleJob>(p => p.CronExpression = "* * * * *");
        ServiceCollection.AddNotificationHandler<HandlerThatThrowsInAsyncPartException, ExceptionJob>();
        ServiceCollection.AddNotificationHandler<SimpleJobHandler, SimpleJob>();
        await using var provider = ServiceCollection.BuildServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        var message = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        message.ShouldBe("Foo");
    }

    private sealed class SimpleJob : IJob
    {
        public Task RunAsync(JobExecutionContext context, CancellationToken token)
        {
            context.Output = "Foo";
            return Task.CompletedTask;
        }
    }

    private sealed class ExceptionJob : IJob
    {
        public Task RunAsync(JobExecutionContext context, CancellationToken token)
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

    private sealed class HandlerThatThrowsInAsyncPartException : IJobNotificationHandler<ExceptionJob>
    {
        public async Task HandleAsync(JobExecutionContext context, Exception? exception, CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
            throw new InvalidOperationException();
        }
    }
}
