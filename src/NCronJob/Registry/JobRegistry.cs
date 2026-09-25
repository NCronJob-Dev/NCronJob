namespace NCronJob;

internal sealed class JobRegistry
{
    private readonly SyncLock syncLock = new();

    private readonly List<JobDefinition> allRootJobs = [];

    private IEnumerable<DependentJobDefinition> AllDependentJobDefinitions => dependentJobsPerJobDefinition.Values
        .SelectMany(v => v)
        .SelectMany(v => v.RunWhenSuccess.Concat(v.RunWhenFaulted));

    private readonly Dictionary<DependentJobDefinition, List<DependentJobRegistryEntry>> dependentJobsPerJobDefinition = [];

    public IReadOnlyCollection<JobDefinition> GetAllRootJobs()
    {
        lock (syncLock)
        {
            return [.. allRootJobs];
        }
    }

    public IReadOnlyCollection<JobDefinition> GetAllCronJobs()
    {
        lock (syncLock)
        {
            return allRootJobs.Where(c => c.CronExpression is not null).ToList();
        }
    }

    public IReadOnlyCollection<JobDefinition> GetAllStartupJobs()
    {
        lock (syncLock)
        {
            return allRootJobs.Where(c => c.IsStartupJob).ToList();
        }
    }

    public IReadOnlyCollection<JobDefinition> FindAllRootJobDefinition(Type type)
    {
        lock (syncLock)
        {
            return allRootJobs.Where(j => j.Type == type).ToList();
        }
    }

    public bool IsRootJob(JobDefinition jobDefinition)
    {
        lock (syncLock)
        {
            return allRootJobs.Contains(jobDefinition);
        }
    }

    public JobDefinition? FindFirstRootJobDefinition(Type type)
    {
        lock (syncLock)
        {
            return allRootJobs.FirstOrDefault(j => j.Type == type);
        }
    }

    public JobDefinition? FindRootJobDefinition(string jobName)
    {
        lock (syncLock)
        {
            return allRootJobs.FirstOrDefault(j => j.CustomName == jobName);
        }
    }

    public JobDefinition FindRootJobDefinitionOrThrow(string jobName) =>
        FindRootJobDefinition(jobName) ?? throw new InvalidOperationException($"Job with name '{jobName}' not found.");

    public void Add(JobDefinition jobDefinition)
    {
        lock (syncLock)
        {
            AddUnsafe(allRootJobs, jobDefinition);
        }
    }

    private static void AddUnsafe(List<JobDefinition> rootJobs, JobDefinition jobDefinition)
    {
        AssertNoDuplicateJobNames(rootJobs, jobDefinition.CustomName);
        AssertOnlyOneUnnamedUnscheduledParameterizedTypedJob(rootJobs, jobDefinition);

        if (rootJobs.Contains(jobDefinition, JobDefinitionEqualityComparer.Instance))
        {
            throw new InvalidOperationException(
                $"""
                Job registration conflict for job '{jobDefinition.Name}' detected. Another job with the same type, parameters, or cron expression already exists.
                Please either remove the duplicate job, change its parameters, or assign a unique name to it if duplication is intended.
                """);
        }

        rootJobs.Add(jobDefinition);
    }

    public string? RemoveByName(string jobName)
    {
        lock (syncLock)
        {
            EnsureCanBeRemoved(j => j.CustomName == jobName);

            var jobDefinition = allRootJobs.FirstOrDefault(j => j.CustomName == jobName);

            if (jobDefinition is null)
            {
                return null;
            }

            Remove(jobDefinition);

            return jobDefinition.JobFullName;
        }
    }

    public string? RemoveByType(Type type)
    {
        lock (syncLock)
        {
            EnsureCanBeRemoved(j => j.Type == type);

            var allJobDefinitions = allRootJobs.Where(j => j.Type == type).ToList();

            if (allJobDefinitions.Count == 0)
            {
                return null;
            }

            foreach (var oneJobDefinition in allJobDefinitions)
            {
                Remove(oneJobDefinition);
            }

            return allJobDefinitions[0].JobFullName;
        }
    }

    private void RegisterJobDependencyUnsafe(IReadOnlyCollection<JobDefinition> parentJobDefinitions, DependentJobRegistryEntry entry)
    {
        foreach (var jobDefinition in parentJobDefinitions)
        {
            var entries = dependentJobsPerJobDefinition.GetOrCreateList(DependentJobDefinition.FromRoot(jobDefinition));
            entries.Add(entry);
        }
    }

    public IReadOnlyCollection<JobDefinition> GetDependentSuccessJobs(JobDefinition parentJobDefinition)
        => FilterByAndProject(parentJobDefinition, v => v.SelectMany(p => p.RunWhenSuccess));

    public IReadOnlyCollection<JobDefinition> GetDependentFaultedJobs(JobDefinition parentJobDefinition)
        => FilterByAndProject(parentJobDefinition, v => v.SelectMany(p => p.RunWhenFaulted));

    private JobDefinition[] FilterByAndProject(
        JobDefinition parentJobDefinition,
        Func<IEnumerable<DependentJobRegistryEntry>, IEnumerable<DependentJobDefinition>> transform)
    {
        lock (syncLock)
        {
            if (!parentJobDefinition.IsTypedJob)
            {
                return [];
            }

            var dependentJobIdentity = DependentJobDefinition.FromRoot(parentJobDefinition);

            return !dependentJobsPerJobDefinition.TryGetValue(dependentJobIdentity, out var types)
                ? []
                : transform(types).Select(definition => definition.ToJobDefinition()).ToArray();
        }
    }

    private void EnsureCanBeRemoved(Func<DependentJobDefinition, bool> jobDefinitionFinder)
    {
        var any = AllDependentJobDefinitions.Any(jobDefinitionFinder);

        if (!any)
        {
            return;
        }

        throw new InvalidOperationException("Cannot remove a job that is a dependency of another job.");
    }

    private void Remove(JobDefinition jobDefinition)
    {
        allRootJobs.Remove(jobDefinition);

        if (jobDefinition.IsTypedJob)
        {
            dependentJobsPerJobDefinition.Remove(DependentJobDefinition.FromRoot(jobDefinition));
        }
    }

    private static void AssertNoDuplicateJobNames(
        IReadOnlyCollection<JobDefinition> rootJobs,
        string? additionalJobName)
    {
        if (additionalJobName is null)
        {
            return;
        }

        if (!rootJobs.Any(jd => jd.CustomName == additionalJobName))
        {
            return;
        }

        throw new InvalidOperationException(
            $"""
            Job registration conflict detected. A job has already been registered with the name '{additionalJobName}'.
            Please use a different name for each job.
            """);
    }

    private static void AssertOnlyOneUnnamedUnscheduledParameterizedTypedJob(
        IReadOnlyCollection<JobDefinition> rootJobs,
        JobDefinition jobDefinition)
    {
        if (jobDefinition.IsExemptFromUniqueParameterizedTypedJobCheck)
        {
            return;
        }

        if (!rootJobs.Any(jd => jd.Type == jobDefinition.Type))
        {
            return;
        }

        throw new InvalidOperationException(
            $"""
            Job registration conflict detected. An unscheduled typed job '{jobDefinition.Name}' has already been registered with a parameter.
            Please use a different name for each job.
            """);
    }

    public JobRegistryRegistration FeedFrom(PendingJobDefinitions pendingJobDefinitions)
    {
        lock (syncLock)
        {
            var validatedRootJobs = new List<JobDefinition>(allRootJobs);
            var registeredDependencies = new List<RegisteredJobDependency>();

            foreach (var jobDefinition in pendingJobDefinitions.Entries.Keys)
            {
                AddUnsafe(validatedRootJobs, jobDefinition);
            }

            var registration = new JobRegistryRegistration([.. pendingJobDefinitions.Entries.Keys], registeredDependencies);

            try
            {
                allRootJobs.AddRange(pendingJobDefinitions.Entries.Keys);

                foreach (var (jobDefinition, dependentJobs) in pendingJobDefinitions.Entries)
                {
                    List<JobDefinition> value = [jobDefinition];

                    foreach (var entry in dependentJobs)
                    {
                        RegisterJobDependencyUnsafe(value, entry);
                        registeredDependencies.Add(new RegisteredJobDependency(
                            DependentJobDefinition.FromRoot(jobDefinition),
                            entry));
                    }
                }

                return registration;
            }
            catch
            {
                RollbackUnsafe(registration);
                throw;
            }
        }
    }

    public void Rollback(JobRegistryRegistration registration)
    {
        lock (syncLock)
        {
            RollbackUnsafe(registration);
        }
    }

    private void RollbackUnsafe(JobRegistryRegistration registration)
    {
        foreach (var dependency in registration.Dependencies)
        {
            if (!dependentJobsPerJobDefinition.TryGetValue(dependency.Parent, out var entries))
            {
                continue;
            }

            entries.Remove(dependency.Entry);
            if (entries.Count == 0)
            {
                dependentJobsPerJobDefinition.Remove(dependency.Parent);
            }
        }

        foreach (var jobDefinition in registration.RootJobs)
        {
            allRootJobs.RemoveAll(candidate => ReferenceEquals(candidate, jobDefinition));
        }
    }

    private sealed class JobDefinitionEqualityComparer : IEqualityComparer<JobDefinition>
    {
        public static readonly JobDefinitionEqualityComparer Instance = new();

        public bool Equals(JobDefinition? x, JobDefinition? y)
        {
            if (x is null && y is null)
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            var expressionEquals = x.CronExpression?.Equals(y.CronExpression) ?? (y.CronExpression is null);

            return x.JobFullName == y.JobFullName
                         && x.Parameter == y.Parameter
                         && expressionEquals
                         && x.TimeZone == y.TimeZone
                         && x.CustomName == y.CustomName
                         && x.IsStartupJob == y.IsStartupJob;
        }

        public int GetHashCode(JobDefinition obj) => HashCode.Combine(
            obj.JobFullName,
            obj.Parameter,
            obj.CronExpression,
            obj.TimeZone,
            obj.CustomName,
            obj.IsStartupJob);
    }
}

internal sealed record JobRegistryRegistration(
    IReadOnlyCollection<JobDefinition> RootJobs,
    IReadOnlyCollection<RegisteredJobDependency> Dependencies);

internal sealed record RegisteredJobDependency(
    DependentJobDefinition Parent,
    DependentJobRegistryEntry Entry);
