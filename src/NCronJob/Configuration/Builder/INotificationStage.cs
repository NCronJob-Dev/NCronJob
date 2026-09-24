namespace NCronJob;

/// <summary>
/// Defines the contract for a stage in the job lifecycle where notifications are handled for the job.
/// </summary>
/// <typeparam name="TJob">The type of the job for which notifications are handled.</typeparam>
public interface INotificationStage<TJob> : IJobStage
    where TJob : class, IJob
{
    /// <summary>
    /// Adds a notification handler for a given <see cref="IJob"/>.
    /// </summary>
    /// <typeparam name="TJobNotificationHandler">The handler-type that is used to handle the job.</typeparam>
    /// <remarks>
    /// The given <see cref="IJobNotificationHandler{TJob}"/> instance is registered as a scoped service and resolved from its own scope, separate from the job's scope.
    /// Also, only one handler per job is allowed. If multiple handlers are registered, only the first one will be executed.
    /// </remarks>
    INotificationStage<TJob> AddNotificationHandler<TJobNotificationHandler>() where TJobNotificationHandler : class, IJobNotificationHandler<TJob>;

    /// <summary>
    /// Adds a condition handler for a given <see cref="IJob"/> that is invoked when the job's OnlyIf condition is not met.
    /// </summary>
    /// <typeparam name="TJobConditionHandler">The handler-type that is used to handle the condition failure.</typeparam>
    /// <remarks>
    /// The given <see cref="IJobConditionHandler{TJob}"/> instance is registered as a scoped service.
    /// This handler is invoked before job instantiation when an OnlyIf condition returns false.
    /// </remarks>
    INotificationStage<TJob> AddConditionHandler<TJobConditionHandler>() where TJobConditionHandler : class, IJobConditionHandler<TJob>;

    /// <summary>
    /// Adds a job that runs after the given job has finished.
    /// </summary>
    /// <param name="success">Configure a job that runs after the principal job has finished successfully.</param>
    /// <param name="faulted">Configure a job that runs after the principal job has faulted. Faulted means that the parent job did throw an exception.</param>
    /// <returns>The builder to add more jobs.</returns>
    INotificationStage<TJob> ExecuteWhen(
        Action<DependencyBuilder>? success = null,
        Action<DependencyBuilder>? faulted = null);
}
