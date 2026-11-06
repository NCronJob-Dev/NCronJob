using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace NCronJob;

/// <summary>
/// Offers a failed run to the registered <see cref="IExceptionHandler"/>s until one of them handles it.
/// </summary>
internal sealed partial class ExceptionHandlerPipeline
{
    private readonly ImmutableArray<IExceptionHandler> exceptionHandlers;
    private readonly ILogger<ExceptionHandlerPipeline> logger;

    public ExceptionHandlerPipeline(
        IEnumerable<IExceptionHandler> exceptionHandlers,
        ILogger<ExceptionHandlerPipeline> logger)
    {
        this.exceptionHandlers = [.. exceptionHandlers];
        this.logger = logger;
    }

    public async Task HandleAsync(JobExecutionContext runContext, Exception exc, CancellationToken stoppingToken)
    {
        foreach (var exceptionHandler in exceptionHandlers)
        {
            try
            {
                if (await exceptionHandler.TryHandleAsync(runContext, exc, stoppingToken))
                {
                    break;
                }
            }
            catch (Exception handlerException)
            {
                LogExceptionHandlerError(handlerException, exceptionHandler.GetType());
            }
        }
    }

    [LoggerMessage(LogLevel.Error, "The exception handler '{Type}' threw an exception.")]
    private partial void LogExceptionHandlerError(Exception exception, Type type);
}
