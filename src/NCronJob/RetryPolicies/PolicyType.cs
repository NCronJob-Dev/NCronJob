namespace NCronJob;

/// <summary>
/// Defines the types of retry policies that can be applied to operations to handle transient faults by retrying failed operations.
/// </summary>
public enum PolicyType
{
    /// <summary>
    /// Specifies a retry policy that implements an exponential backoff strategy.
    /// This policy increases the delay between retry attempts exponentially, which helps to reduce the load on the system and increases the probability of a successful retry under high contention scenarios.
    /// </summary>
    ExponentialBackoff,

    /// <summary>
    /// Specifies a retry policy that implements a fixed interval strategy.
    /// Each retry attempt will wait for a consistent delay period between attempts regardless of the number of retries. This is useful for scenarios where the expected time for resolving a transient fault is consistent.
    /// </summary>
    FixedInterval
}

