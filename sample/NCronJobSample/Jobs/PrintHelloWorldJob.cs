using NCronJob;

namespace NCronJobSample;

[SupportsConcurrency(4)]
public partial class PrintHelloWorldJob : IJob
{
    private static int invocationCount;
    private readonly ILogger<PrintHelloWorldJob> logger;

    public PrintHelloWorldJob(ILogger<PrintHelloWorldJob> logger) => this.logger = logger;

    public async Task RunAsync(JobExecutionContext context, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(context);

        LogMessage($"{++invocationCount}  {context.Parameter}");

        await Task.Delay(200000, token);
    }

    [LoggerMessage(LogLevel.Information, "Scheduled email job done with count {Parameter}.")]
    private partial void LogMessage(object? parameter);
}

[SupportsConcurrency(2)]
public partial class DataProcessingJob : IJob
{
    private static int invocationCount;
    private readonly ILogger<DataProcessingJob> logger;

    public DataProcessingJob(ILogger<DataProcessingJob> logger) => this.logger = logger;

    public async Task RunAsync(JobExecutionContext context, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(context);

        LogMessage(++invocationCount);

        await Task.Delay(1000, token);
    }

    [LoggerMessage(LogLevel.Information, "Scheduled data processing job done with count {Parameter}.")]
    private partial void LogMessage(object? parameter);
}
