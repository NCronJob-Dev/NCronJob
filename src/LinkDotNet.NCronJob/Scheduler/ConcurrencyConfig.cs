namespace LinkDotNet.NCronJob;

/// <summary>
/// Defines the configuration settings for managing concurrency within the application.
/// This record is used to configure how many operations can run in parallel.
/// </summary>
internal record ConcurrencyConfig(int MaxDegreeOfParallelism);

