using System.Diagnostics.CodeAnalysis;

namespace NCronJob;

/// <summary>
/// Provides a way to be notified of job execution lifecycle.
/// </summary>
[Experimental("NCRONJOB_OBSERVER")]
public interface IJobExecutionProgressReporter
{
    /// <summary>
    /// Enlist a new callback hook that will be triggered on each job state change.
    /// </summary>
    /// <param name="callback">The action that will be invoked.</param>
    /// <returns>An <see cref="IDisposable"/> object representing the subscription. Once disposed, the callback hook won't be invoked anymore.</returns>
    IDisposable Register(Action<ExecutionProgress> callback);
}
