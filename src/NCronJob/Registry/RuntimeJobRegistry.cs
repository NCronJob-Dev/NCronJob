using System.Diagnostics.CodeAnalysis;

namespace NCronJob;

/// <inheritdoc />
internal sealed class RuntimeJobRegistry : IRuntimeJobRegistry
{
    private readonly RuntimeJobRegistrar registrar;
    private readonly JobRegistry jobRegistry;
    private readonly CronRunScheduler cronRunScheduler;
    private readonly JobQueueManager jobQueueManager;
    private readonly TimeProvider timeProvider;

    public RuntimeJobRegistry(
        RuntimeJobRegistrar registrar,
        JobRegistry jobRegistry,
        CronRunScheduler cronRunScheduler,
        JobQueueManager jobQueueManager,
        TimeProvider timeProvider)
    {
        this.registrar = registrar;
        this.jobRegistry = jobRegistry;
        this.cronRunScheduler = cronRunScheduler;
        this.jobQueueManager = jobQueueManager;
        this.timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public bool TryRegister(Action<IRuntimeJobBuilder> jobBuilder, [NotNullWhen(false)] out Exception? exception) =>
        registrar.TryRegister(jobBuilder, out exception);

    /// <inheritdoc />
    public void RemoveJob(string jobName) => RemoveJob(() => jobRegistry.RemoveByName(jobName));

    /// <inheritdoc />
    public void RemoveJob(Type type) => RemoveJob(() => jobRegistry.RemoveByType(type));

    /// <inheritdoc />
    public void UpdateSchedule(string jobName, string cronExpression, TimeZoneInfo? timeZoneInfo = null)
    {
        ArgumentNullException.ThrowIfNull(jobName);
        ArgumentNullException.ThrowIfNull(cronExpression);

        UpdateAndReschedule(jobName, new JobOption { CronExpression = cronExpression, TimeZoneInfo = timeZoneInfo });
    }

    /// <inheritdoc />
    public void UpdateParameter(string jobName, object? parameter)
    {
        ArgumentNullException.ThrowIfNull(jobName);

        UpdateAndReschedule(jobName, new JobOption { Parameter = parameter });
    }

    /// <inheritdoc />
    public bool TryGetSchedule(string jobName, out string? cronExpression, out TimeZoneInfo? timeZoneInfo)
    {
        cronExpression = null;
        timeZoneInfo = null;

        var job = jobRegistry.FindRootJobDefinition(jobName);
        if (job is null)
        {
            return false;
        }

        (cronExpression, timeZoneInfo) = job.GetSchedule();

        return true;
    }

    /// <inheritdoc />
    public bool TryGetNextOccurrence(string jobName, out DateTimeOffset? nextRun)
    {
        ArgumentNullException.ThrowIfNull(jobName);

        nextRun = null;

        var job = jobRegistry.FindRootJobDefinition(jobName);
        if (job is null || !job.IsEnabled || job.UserDefinedCronExpression is null)
        {
            return false;
        }

        nextRun = job.GetNextCronOccurrence(timeProvider.GetUtcNow());
        return true;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<RecurringJobSchedule> GetAllRecurringJobs()
        => jobRegistry
            .GetAllCronJobs()
            .Select(jd => jd.ToRecurringJobSchedule())
            .ToArray();

    /// <inheritdoc />
    public void EnableJob(string jobName)
    {
        var job = jobRegistry.FindRootJobDefinition(jobName)
                  ?? throw new InvalidOperationException($"Root job with name '{jobName}' not found.");

        EnableJob(job);
    }

    /// <inheritdoc />
    public void EnableJob(Type type)
    {
        ProcessAllJobDefinitionsOfType(type, EnableJob);
    }

    /// <inheritdoc />
    public void DisableJob(string jobName)
    {
        var job = jobRegistry.FindRootJobDefinition(jobName)
                  ?? throw new InvalidOperationException($"Root job with name '{jobName}' not found.");

        DisableJob(job);
    }

    /// <inheritdoc />
    public void DisableJob(Type type)
    {
        ProcessAllJobDefinitionsOfType(type, DisableJob);
    }

    private void ProcessAllJobDefinitionsOfType(Type type, Action<JobDefinition> processor)
    {
        ArgumentNullException.ThrowIfNull(type);

        var jobDefinitions = jobRegistry.FindAllRootJobDefinition(type);
        if (jobDefinitions.Count == 0)
        {
            throw new InvalidOperationException($"Root job with type '{type}' not found.");
        }

        foreach (var jobDefinition in jobDefinitions)
        {
            processor(jobDefinition);
        }
    }

    private void EnableJob(JobDefinition job)
    {
        job.Enable();

        RescheduleJob(job);
    }

    private void DisableJob(JobDefinition job)
    {
        job.Disable();

        RescheduleJob(job);
    }

    private void RemoveJob(Func<string?> unregister)
    {
        var jobFullName = unregister();

        if (jobFullName is not null)
        {
            jobQueueManager.RemoveQueue(jobFullName);
        }
    }

    private void UpdateAndReschedule(string jobName, JobOption option)
    {
        var job = jobRegistry.FindRootJobDefinitionOrThrow(jobName);
        job.UpdateWith(option);

        RescheduleJob(job);
    }

    private void RescheduleJob(JobDefinition job)
    {
        jobQueueManager.RemoveQueue(job.JobFullName);
        cronRunScheduler.ScheduleNextRun(job);
    }
}
