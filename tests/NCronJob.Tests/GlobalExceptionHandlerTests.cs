using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace NCronJob.Tests;

public sealed class GlobalExceptionHandlerTests : JobIntegrationBase
{
    [Fact]
    public async Task ShouldInformGlobalExceptionHandlerInOrder()
    {
        ServiceCollection.AddNCronJob(o =>
        {
            o.AddExceptionHandler<FirstTestExceptionHandler>();
            o.AddExceptionHandler<SecondTestExceptionHandler>();
            o.AddJob(() =>
            {
                throw new InvalidOperationException();
            }, "* * * * *");
        });

        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var firstMessage = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        firstMessage.ShouldBe(1);
        var secondMessage = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        secondMessage.ShouldBe(2);
    }

    [Fact]
    public async Task ShouldStopProcessingWhenHandlerReturnsTrue()
    {
        ServiceCollection.AddNCronJob(o =>
        {
            o.AddExceptionHandler<FirstHandlerThatStops>();
            o.AddExceptionHandler<SecondTestExceptionHandler>();
            o.AddJob(() =>
            {
                throw new InvalidOperationException();
            }, "* * * * *");
        });

        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var runs = await WaitForJobsOrTimeout(2, TimeSpan.FromMilliseconds(100));
        runs.ShouldBeFalse();
    }

    [Fact]
    public async Task ExceptionHandlerThatThrowsShouldntStopProcessing()
    {
        ServiceCollection.AddNCronJob(o =>
        {
            o.AddExceptionHandler<ExceptionHandlerThatThrows>();
            o.AddExceptionHandler<SecondTestExceptionHandler>();
            o.AddJob(() =>
            {
                throw new InvalidOperationException();
            }, "* * * * *");
        });

        var provider = CreateServiceProvider();
        await provider.GetRequiredService<IHostedService>().StartAsync(CancellationToken);

        FakeTimer.Advance(TimeSpan.FromMinutes(1));
        var firstMessage = await CommunicationChannel.Reader.ReadAsync(CancellationToken);
        firstMessage.ShouldBe(2);
    }

    private sealed class FirstTestExceptionHandler : IExceptionHandler
    {
        private readonly ChannelWriter<object> writer;

        public FirstTestExceptionHandler(ChannelWriter<object> writer)
            => this.writer = writer;

        public async Task<bool> TryHandleAsync(IJobExecutionContext jobExecutionContext, Exception exception, CancellationToken cancellationToken)
        {
            await writer.WriteAsync(1, cancellationToken);
            return false;
        }
    }

    private sealed class SecondTestExceptionHandler : IExceptionHandler
    {
        private readonly ChannelWriter<object> writer;

        public SecondTestExceptionHandler(ChannelWriter<object> writer)
            => this.writer = writer;

        public async Task<bool> TryHandleAsync(IJobExecutionContext jobExecutionContext, Exception exception, CancellationToken cancellationToken)
        {
            await writer.WriteAsync(2, cancellationToken);
            return false;
        }
    }

    private sealed class FirstHandlerThatStops : IExceptionHandler
    {
        private readonly ChannelWriter<object> writer;

        public FirstHandlerThatStops(ChannelWriter<object> writer)
            => this.writer = writer;

        public async Task<bool> TryHandleAsync(IJobExecutionContext jobExecutionContext, Exception exception, CancellationToken cancellationToken)
        {
            await writer.WriteAsync(1, cancellationToken);
            return true;
        }
    }

    private sealed class ExceptionHandlerThatThrows : IExceptionHandler
    {
        public Task<bool> TryHandleAsync(IJobExecutionContext jobExecutionContext, Exception exception, CancellationToken cancellationToken)
            => throw new InvalidOperationException();
    }
}
