namespace LinkDotNet.NCronJob;

/// <summary>
/// This is in pub-internal interface that is used to avoid reflection.
/// Please derive directly from <see cref="IJobNotificationHandler{TJob}"/>.
/// </summary>
public interface IJobNotificationHandler
{
    /// <summary>
    /// This method is invoked when a <see cref="IJob"/> is finished (either successfully or with an exception).
    /// </summary>
    /// <param name="context">The <see cref="JobExecutionContext"/> that was used for this run.</param>
    /// <param name="exception">The exception that was thrown during the execution of the job. If the job was successful, this will be <c>null</c>.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to cancel the notification.</param>
    /// <remarks>
    /// The method will be invoked with the same scope as the job itself.
    /// </remarks>
    Task HandleAsync(JobExecutionContext context, Exception? exception, CancellationToken cancellationToken);
}

/// <summary>
/// Classes implementing this interface can handle the notification of a job execution.
/// </summary>
public interface IJobNotificationHandler<TJob> : IJobNotificationHandler
    where TJob : IJob;
