namespace LinkDotNet.NCronJob;

/// <summary>
/// When competing for resources the Higher the priority the more
/// likely the job will be executed over others of lower priority.
/// </summary>
internal enum JobPriority
{
    Low = 0,
    Normal = 1,
    High = 2
}
