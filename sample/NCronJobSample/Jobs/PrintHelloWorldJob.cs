using LinkDotNet.NCronJob;

namespace NCronJobSample;

public partial class PrintHelloWorldJob : IJob
{
    private readonly ILogger<PrintHelloWorldJob> logger;

    public PrintHelloWorldJob(ILogger<PrintHelloWorldJob> logger) => this.logger = logger;

    public Task RunAsync(JobExecutionContext context, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!string.IsNullOrEmpty(context.Parameter as string))
            LogMessage(context.Parameter);

        context.Output = "Hey there!";

        return Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Information, "The input parameter is : {Parameter}")]
    private partial void LogMessage(object? parameter);
}
