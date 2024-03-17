namespace LinkDotNet.NCronJob;

/// <summary>
/// Represents the context of a job execution.
/// </summary>
/// <param name="parameter">The passed in parameters to a job</param>
public sealed record JobExecutionContext(object? Parameter);
