using NCronJob;

namespace NCronJobSample;

[SupportsConcurrency(10)]
public partial class PrintHelloWorldJob : IJob
{
    private static int invocationCount;
    private readonly ILogger<PrintHelloWorldJob> logger;

    public PrintHelloWorldJob(ILogger<PrintHelloWorldJob> logger) => this.logger = logger;

    public Task RunAsync(JobExecutionContext context, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(context);

        LogMessage(++invocationCount);

        context.Output = "Hey there!";

        return Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Information, "The input parameter is : {Parameter}")]
    private partial void LogMessage(object? parameter);
}
