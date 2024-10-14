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
        // Arrange
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
        secondMessage.ShouldBe(1);
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
            await writer.WriteAsync(1, cancellationToken);
            return false;
        }
    }
}
