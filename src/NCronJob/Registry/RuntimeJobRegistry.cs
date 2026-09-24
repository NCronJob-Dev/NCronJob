using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace NCronJob;

/// <summary>
/// Gives the ability to add, delete or adjust jobs at runtime.
/// </summary>
public interface IRuntimeJobRegistry
{
    /// <summary>
    /// Tries to register a job with the given configuration.
    /// </summary>/param>
    /// <param name="jobBuilder">The job builder that configures the job.</param>
    /// <param name="exception">The exception that occurred during the registration process. Or <c>null</c> if the registration was successful.</param>
    /// <returns>Returns <c>true</c> if the registration was successful, otherwise <c>false</c>.</returns>
    bool TryRegister(Action<IRuntimeJobBuilder> jobBuilder, [NotNullWhen(false)] out Exception? exception);

    /// <summary>
    /// Removes the job with the given name.
    /// </summary>
    /// <param name="jobName">The name of the job to remove.</param>
    /// <remarks>If the given job is not found, no exception is thrown.</remarks>
    void RemoveJob(string jobName);

    /// <summary>
    /// Removes all jobs of the given type.
    /// </summary>
    /// <remarks>If the given job is not found, no exception is thrown.</remarks>
    void RemoveJob(Type type);

    /// <summary>
    /// Updates the schedule of a given job by its name.
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    /// <param name="cronExpression">The new cron expression for the job.</param>
    /// <param name="timeZoneInfo">An optional timezone to use for the cron expression.If not provided, UTC is used.</param>
    /// <remarks>
    /// If the given job is not found, an exception is thrown.
    /// Furthermore, all current planned executions of that job are canceled and rescheduled with the new cron expression.
    /// </remarks>
    void UpdateSchedule(string jobName, string cronExpression, TimeZoneInfo? timeZoneInfo = null);

    /// <summary>
    /// Updates the parameter of a given job by its name.
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    /// <param name="parameter">The new parameter that will be passed into the job.</param>
    /// <remarks>
    /// If the given job is not found, an exception is thrown.
    /// Furthermore, all current planned executions of that job are canceled and rescheduled with the new parameter.
    /// </remarks>
    void UpdateParameter(string jobName, object? parameter);

    /// <summary>
    /// Retrieves the schedule of a given job by its name. If the job is not found, the out parameters are set to null.
    /// </summary>
    /// <param name="jobName">The given job name.</param>
    /// <param name="cronExpression">The associated cron expression. If the job has none, or couldn't be found this will be <c>null</c>.</param>
    /// <param name="timeZoneInfo">The associated time zone. If the job has no schedule, or couldn't be found this will be <c>null</c>.</param>
    /// <returns>Returns <c>true</c> if the job was found, otherwise <c>false</c>.</returns>
    bool TryGetSchedule(string jobName, out string? cronExpression, out TimeZoneInfo? timeZoneInfo);

    /// <summary>
    /// Tries to retrieve the next scheduled occurrence of an enabled recurring job by its name.
    /// </summary>
    /// <param name="jobName">The given job name.</param>
    /// <param name="nextRun">
    /// The next occurrence in UTC, or <c>null</c> when a valid recurring schedule has no future occurrence.
    /// This is also <c>null</c> when the method returns <c>false</c>.
    /// </param>
    /// <returns>
    /// <c>true</c> when an enabled recurring job was found; otherwise <c>false</c> for unknown, disabled,
    /// or unscheduled jobs.
    /// </returns>
    bool TryGetNextOccurrence(string jobName, out DateTimeOffset? nextRun);

    /// <summary>
    /// Returns a list of all recurring jobs.
    /// </summary>
    /// <returns></returns>
    IReadOnlyCollection<RecurringJobSchedule> GetAllRecurringJobs();

    /// <summary>
    /// Enables a job that was previously disabled.
    /// </summary>
    /// <param name="jobName">The unique job name that identifies this job.</param>
    /// <remarks>
    /// If the job is already enabled, this method does nothing.
    /// If the job is not found, an exception is thrown.
    /// </remarks>
    void EnableJob(string jobName);

    /// <summary>
    /// Enables all jobs of the given type that were previously disabled.
    /// </summary>
    /// <remarks>
    /// If the job is already enabled, this method does nothing.
    /// If the job is not found, an exception is thrown.
    /// </remarks>
    void EnableJob(Type type);

    /// <summary>
    /// Disables a job that was previously enabled.
    /// </summary>
    /// <param name="jobName">The unique job name that identifies this job.</param>
    /// <remarks>
    /// If the job is already disabled, this method does nothing.
    /// If the job is not found, an exception is thrown.
    /// </remarks>
    void DisableJob(string jobName);

    /// <summary>
    /// Disables all jobs of the given type.
    /// </summary>
    /// <remarks>
    /// If the job is already disabled, this method does nothing.
    /// If the job is not found, an exception is thrown.
    /// </remarks>
    void DisableJob(Type type);
}

/// <summary>
/// Represents a recurring job schedule.
/// </summary>
/// <param name="JobName">The optional custom name given to the job. Will be <c>null</c> when no name was specified.</param>
/// <param name="Type">The type of the job; Will be <c>null</c> if the job is an anonymous function based job.</param>
/// <param name="IsTypedJob">Whether the job is a class based job (implementing <see cref="IJob"/>) or not.</param>
/// <param name="CronExpression">The cron expression that defines when the job should be executed.</param>
/// <param name="IsEnabled">Whether the job is enabled or not.</param>
/// <param name="TimeZone">The timezone that is used to evaluate the cron expression.</param>
public sealed record RecurringJobSchedule(string? JobName, string CronExpression, bool IsEnabled, TimeZoneInfo TimeZone, Type? Type = null, bool IsTypedJob = true);

/// <inheritdoc />
internal sealed class RuntimeJobRegistry : IRuntimeJobRegistry
{
    private readonly SyncLock registrationLock = new();

    private readonly IServiceCollection services;
    private readonly JobRegistry jobRegistry;
    private readonly JobWorker jobWorker;
    private readonly JobQueueManager jobQueueManager;
    private readonly ConcurrencySettings concurrencySettings;
    private readonly TimeProvider timeProvider;

    public RuntimeJobRegistry(
        IServiceCollection services,
        JobRegistry jobRegistry,
        JobWorker jobWorker,
        JobQueueManager jobQueueManager,
        ConcurrencySettings concurrencySettings,
        TimeProvider timeProvider)
    {
        this.services = services;
        this.jobRegistry = jobRegistry;
        this.jobWorker = jobWorker;
        this.jobQueueManager = jobQueueManager;
        this.concurrencySettings = concurrencySettings;
        this.timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public bool TryRegister(Action<IRuntimeJobBuilder> jobBuilder, [NotNullWhen(false)] out Exception? exception)
    {
        lock (registrationLock)
        {
            var trackedServices = new TrackingServiceCollection(services);
            var previousSettings = concurrencySettings.Snapshot();
            JobRegistryRegistration? registration = null;
            List<JobRun> scheduledRuns = [];
            List<string> createdQueueNames = [];
            var activationGate = new JobRunActivationGate();

            try
            {
                var jdc = new JobDefinitionCollector();
                var builder = new NCronJobOptionBuilder(trackedServices, concurrencySettings, jdc);
                jobBuilder(builder);
                builder.ValidateConcurrencySettings(jobRegistry.GetAllRootJobs());

                registration = jobRegistry.FeedFrom(jdc);

                foreach (var jobDefinition in jdc.Entries.Keys)
                {
                    jobWorker.ScheduleJob(
                        jobDefinition,
                        onRunCreated: scheduledRuns.Add,
                        onQueueCreated: createdQueueNames.Add,
                        activationGate: activationGate);
                }

                activationGate.Activate();
                exception = null;
                return true;
            }
            catch (Exception ex)
            {
                List<Exception> rollbackExceptions = [];
                activationGate.Reject();

                TryRollback(
                    () => jobQueueManager.RemoveRuns(scheduledRuns, createdQueueNames),
                    rollbackExceptions);

                if (registration is not null)
                {
                    TryRollback(() => jobRegistry.Rollback(registration), rollbackExceptions);
                }

                TryRollback(trackedServices.Rollback, rollbackExceptions);
                TryRollback(() => concurrencySettings.Restore(previousSettings), rollbackExceptions);

                exception = rollbackExceptions.Count == 0
                    ? ex
                    : new AggregateException(
                        "Runtime job registration failed and rollback encountered additional errors.",
                        new[] { ex }.Concat(rollbackExceptions));
                return false;
            }
        }
    }

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
        jobWorker.ScheduleJob(job);
    }

    private static void TryRollback(Action rollback, List<Exception> exceptions)
    {
        try
        {
            rollback();
        }
        catch (Exception exception)
        {
            exceptions.Add(exception);
        }
    }

    private sealed class TrackingServiceCollection(IServiceCollection services) : IServiceCollection
    {
        private readonly List<ServiceDescriptor> addedDescriptors = [];

        public ServiceDescriptor this[int index]
        {
            get => services[index];
            set => services[index] = value;
        }

        public int Count => services.Count;

        public bool IsReadOnly => services.IsReadOnly;

        public void Add(ServiceDescriptor item)
        {
            services.Add(item);
            addedDescriptors.Add(item);
        }

        public void Clear() => services.Clear();

        public bool Contains(ServiceDescriptor item) => services.Contains(item);

        public void CopyTo(ServiceDescriptor[] array, int arrayIndex) => services.CopyTo(array, arrayIndex);

        public IEnumerator<ServiceDescriptor> GetEnumerator() => services.GetEnumerator();

        public int IndexOf(ServiceDescriptor item) => services.IndexOf(item);

        public void Insert(int index, ServiceDescriptor item)
        {
            services.Insert(index, item);
            addedDescriptors.Add(item);
        }

        public bool Remove(ServiceDescriptor item) => services.Remove(item);

        public void RemoveAt(int index) => services.RemoveAt(index);

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public void Rollback()
        {
            foreach (var descriptor in addedDescriptors.AsEnumerable().Reverse())
            {
                for (var index = services.Count - 1; index >= 0; index--)
                {
                    if (!ReferenceEquals(services[index], descriptor))
                    {
                        continue;
                    }

                    services.RemoveAt(index);
                    break;
                }
            }
        }
    }
}
