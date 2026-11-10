namespace NCronJob;

/// <summary>
/// Represents a job that can be executed.
/// </summary>
/// <remarks>
/// A job that needs a parameter implements <see cref="IJob{TParameter}"/> instead.
/// </remarks>
public interface IJob
{
    /// <summary>
    /// Entry point for the job. This method will be called when the job is executed.
    /// </summary>
    /// <param name="context">The context of the job execution.</param>
    /// <param name="token">A cancellation token that can be used to cancel the job.</param>
    public Task RunAsync(IJobExecutionContext context, CancellationToken token);
}

/// <summary>
/// Represents a job that is executed with a parameter of type <typeparamref name="TParameter"/>.
/// </summary>
/// <typeparam name="TParameter">
/// The type of the parameter. A parameter that is not assignable to it is rejected when the job is registered or triggered.
/// Without a parameter, <see cref="IJobExecutionContext{TParameter}.Parameter"/> is <c>default</c>, so declare a nullable type if the parameter is optional.
/// </typeparam>
public interface IJob<in TParameter> : IJob
{
    /// <summary>
    /// Entry point for the job. This method will be called when the job is executed.
    /// </summary>
    /// <param name="context">The context of the job execution, including the typed parameter.</param>
    /// <param name="token">A cancellation token that can be used to cancel the job.</param>
    public Task RunAsync(IJobExecutionContext<TParameter> context, CancellationToken token);

    Task IJob.RunAsync(IJobExecutionContext context, CancellationToken token)
        => context is IJobExecutionContext<TParameter> typedContext
            ? RunAsync(typedContext, token)
            : throw new ArgumentException(
                $"'{GetType().Name}' expects an {nameof(IJobExecutionContext)}<{typeof(TParameter).Name}>.",
                nameof(context));
}
