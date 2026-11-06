namespace NCronJob;

/// <summary>
/// Creates <see cref="JobRun"/>s wired to the shared clock, progress reporting and concurrency settings.
/// </summary>
internal sealed class JobRunFactory
{
    private readonly TimeProvider timeProvider;
    private readonly JobExecutionProgressObserver observer;
    private readonly ConcurrencySettings concurrencySettings;

    public JobRunFactory(
        TimeProvider timeProvider,
        JobExecutionProgressObserver observer,
        ConcurrencySettings concurrencySettings)
    {
        this.timeProvider = timeProvider;
        this.observer = observer;
        this.concurrencySettings = concurrencySettings;
    }

    public JobRun CreateStartup(JobDefinition jobDefinition) =>
        JobRun.CreateStartupJob(timeProvider, observer.Report, jobDefinition, concurrencySettings);

    public JobRun CreateCron(JobDefinition jobDefinition, DateTimeOffset runAt, JobRunActivationGate? activationGate = null) =>
        JobRun.CreateCron(timeProvider, observer.Report, jobDefinition, runAt, concurrencySettings, activationGate);

    public JobRun CreateInstant(
        JobDefinition jobDefinition,
        DateTimeOffset runAt,
        OptionalParameter parameter,
        CancellationToken token) =>
        JobRun.CreateInstant(timeProvider, observer.Report, jobDefinition, runAt, parameter, token, concurrencySettings);
}
