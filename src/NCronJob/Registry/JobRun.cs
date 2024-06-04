namespace NCronJob;

internal class JobRun
{
    private int jobExecutionCount;

    internal JobPriority Priority { get; set; } = JobPriority.Normal;

    public required Guid JobRunId { get; init; }

    public required JobDefinition JobDefinition { get; init; }

    public Guid CorrelationId { get; set; } = Guid.NewGuid();

    public CancellationToken CancellationToken { get; set; }

    public DateTimeOffset? RunAt { get; set; }
    public TimeSpan Expiry { get; set; } = TimeSpan.FromMinutes(10);
    public bool IsExpired => RunAt.HasValue && DateTimeOffset.UtcNow - RunAt.Value > Expiry;

    public bool IsOneTimeJob { get; set; }

    public object? Parameter { get; init; }

    public object? ParentOutput { get; set; }

    public int JobExecutionCount => Interlocked.CompareExchange(ref jobExecutionCount, 0, 0);

    public void IncrementJobExecutionCount() => Interlocked.Increment(ref jobExecutionCount);

    public static JobRun Create(JobDefinition jobDefinition) =>
        new()
        {
            JobRunId = Guid.NewGuid(),
            JobDefinition = jobDefinition,
            Parameter = jobDefinition.Parameter
        };

    public static JobRun Create(JobDefinition jobDefinition, object? parameter, CancellationToken token) =>
        new()
        {
            JobRunId = Guid.NewGuid(),
            JobDefinition = jobDefinition,
            Parameter = parameter,
            CancellationToken = token
        };
}
