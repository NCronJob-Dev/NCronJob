using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace NCronJob.Tests;

public class NCronJobNotificationHandlerTests : JobIntegrationBase
{
    [Fact]
    public async Task ShouldCallNotificationHandlerWhenJobIsDone()
    {
        ServiceCollection.AddNCronJob(n => n
                .AddJob<SimpleJob>(p => p.WithCronExpression("* * * * *"))
                .AddNotificationHandler<SimpleJobHandler>()
        );
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var message = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        message.ShouldBe("Foo");
    }

    [Fact]
    public async Task ShouldPassDownExceptionToNotificationHandler()
    {
        ServiceCollection.AddNCronJob(n => n
                .AddJob<ExceptionJob>(p => p.WithCronExpression("* * * * *"))
                .AddNotificationHandler<ExceptionHandler>()
        );
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var message = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        message.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task HandlerThatThrowsExceptionShouldNotInfluenceOtherHandlers()
    {
        ServiceCollection.AddNCronJob(n => n
                .AddJob<SimpleJob>(p => p.WithCronExpression("* * * * *"))
                .AddNotificationHandler<SimpleJobHandler>()
                .AddJob<ExceptionJob>(p => p.WithCronExpression("* * * * *"))
                .AddNotificationHandler<HandlerThatThrowsException>()

        );
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var message = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        message.ShouldBe("Foo");
    }

    [Fact]
    public async Task HandlerThatThrowsExceptionInAsyncPartShouldNotInfluenceOtherHandlers()
    {
        ServiceCollection.AddNCronJob(n => n
                .AddJob<SimpleJob>(p => p.WithCronExpression("* * * * *"))
                .AddNotificationHandler<SimpleJobHandler>()
                .AddJob<ExceptionJob>(p => p.WithCronExpression("* * * * *"))
                .AddNotificationHandler<HandlerThatThrowsInAsyncPartException>()
        );
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
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
            => throw new InvalidOperationException();
    }

    private sealed class SimpleJobHandler(ChannelWriter<object> writer) : IJobNotificationHandler<SimpleJob>
    {
        public Task HandleAsync(JobExecutionContext context, Exception? exception, CancellationToken cancellationToken)
            => writer.WriteAsync(context.Output!, cancellationToken).AsTask();
    }

    private sealed class ExceptionHandler(ChannelWriter<object> writer) : IJobNotificationHandler<ExceptionJob>
    {
        public Task HandleAsync(JobExecutionContext context, Exception? exception, CancellationToken cancellationToken)
            => writer.WriteAsync(exception!, cancellationToken).AsTask();
    }

    private sealed class HandlerThatThrowsException : IJobNotificationHandler<ExceptionJob>
    {
        public Task HandleAsync(JobExecutionContext context, Exception? exception, CancellationToken cancellationToken)
            => throw new InvalidOperationException();
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
