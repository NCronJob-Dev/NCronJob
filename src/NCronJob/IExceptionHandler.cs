namespace NCronJob;

/// <summary>
/// Represents an interface for handling exceptions in NCronJob applications.
/// </summary>
public interface IExceptionHandler
{
    /// <summary>
    /// Tries to handle an exception asynchronously within the NCronJob pipeline.
    /// </summary>
    /// <param name="jobExecutionContext">The context of the job that throws the exception.</param>
    /// <param name="exception">The unhandled exception.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>If the result is <c>true</c> no other exception handlers will be called.</returns>
    Task<bool> TryHandleAsync(
        IJobExecutionContext jobExecutionContext,
        Exception exception,
        CancellationToken cancellationToken);
}
