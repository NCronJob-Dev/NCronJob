namespace NCronJob;

/// <summary>
/// Represents the configuration settings for managing concurrency within the application.
/// </summary>
/// <remarks>
/// This configuration is utilized to specify the maximum number of concurrent operations
/// that the system can execute simultaneously.
/// </remarks>
internal class ConcurrencySettings
{
    /// <summary>
    /// The total number of concurrent jobs that can be executed
    /// by the scheduler at any one time, irrespective of the job type.
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; }

    /// <summary>
    /// Uses a custom task scheduler designed to execute tasks in a deterministic order.
    /// </summary>
    /// <remarks>
    /// This configuration is currently used for testing purposes only. Although more accurate, the Deterministic Task Scheduler
    /// currently uses the ThreadPool.UnsafeQueueUserWorkItem method to execute tasks, which may have security implications,
    /// and is not currently recommended for production use.
    /// </remarks>
    public static bool UseDeterministicTaskScheduler => true;
}

