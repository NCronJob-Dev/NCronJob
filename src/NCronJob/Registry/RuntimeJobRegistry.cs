using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace NCronJob;

/// <inheritdoc />
internal sealed class RuntimeJobRegistry : IRuntimeJobRegistry
{
    private readonly SyncLock registrationLock = new();

    private readonly IServiceCollection services;
    private readonly JobRegistry jobRegistry;
    private readonly CronRunScheduler cronRunScheduler;
    private readonly JobQueueManager jobQueueManager;
    private readonly ConcurrencySettings concurrencySettings;
    private readonly TimeProvider timeProvider;

    public RuntimeJobRegistry(
        IServiceCollection services,
        JobRegistry jobRegistry,
        CronRunScheduler cronRunScheduler,
        JobQueueManager jobQueueManager,
        ConcurrencySettings concurrencySettings,
        TimeProvider timeProvider)
    {
        this.services = services;
        this.jobRegistry = jobRegistry;
        this.cronRunScheduler = cronRunScheduler;
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
                    cronRunScheduler.ScheduleNextRun(
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
        cronRunScheduler.ScheduleNextRun(job);
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
