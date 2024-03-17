namespace LinkDotNet.NCronJob;

/// <summary>
/// Represents the context of a job execution.
/// </summary>
/// <param name="Parameter">The passed in parameters to a job</param>
public sealed record JobExecutionContext(object? Parameter);
