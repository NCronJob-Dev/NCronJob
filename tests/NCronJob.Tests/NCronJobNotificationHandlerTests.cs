using System.Threading.Channels;
using LinkDotNet.NCronJob;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace NCronJob.Tests;

public class NCronJobNotificationHandlerTests : JobIntegrationBase
{
    [Fact]
    public async Task ShouldCallNotificationHandlerWhenJobIsDone()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(n => n
                .AddJob<SimpleJob>(p => p.WithCronExpression("* * * * *"))
                .AddNotificationHandler<SimpleJobHandler, SimpleJob>()
        );
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        var message = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        message.ShouldBe("Foo");
    }

    [Fact]
    public async Task ShouldPassDownExceptionToNotificationHandler()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(n => n
                .AddJob<ExceptionJob>(p => p.WithCronExpression("* * * * *"))
                .AddNotificationHandler<ExceptionHandler, ExceptionJob>()
        );
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        var message = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        message.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task HandlerThatThrowsExceptionShouldNotInfluenceOtherHandlers()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(n => n
                .AddJob<SimpleJob>(p => p.WithCronExpression("* * * * *"))
                .AddNotificationHandler<HandlerThatThrowsException, ExceptionJob>()
                .AddNotificationHandler<SimpleJobHandler, SimpleJob>()
        );
        var provider = CreateServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        fakeTimer.Advance(TimeSpan.FromMinutes(1));
        var message = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        message.ShouldBe("Foo");
    }

    [Fact]
    public async Task HandlerThatThrowsExceptionInAsyncPartShouldNotInfluenceOtherHandlers()
    {
        var fakeTimer = new FakeTimeProvider();
        ServiceCollection.AddSingleton<TimeProvider>(fakeTimer);
        ServiceCollection.AddNCronJob(n => n
                .AddJob<SimpleJob>(p => p.WithCronExpression("* * * * *"))
                .AddNotificationHandler<HandlerThatThrowsInAsyncPartException, ExceptionJob>()
                .AddNotificationHandler<SimpleJobHandler, SimpleJob>()
        );
        var provider = CreateServiceProvider();

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
