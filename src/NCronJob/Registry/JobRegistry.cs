using System.Diagnostics;

namespace NCronJob;

internal sealed class JobRegistry
{
    private readonly HashSet<JobDefinition> allJobs = new(JobDefinitionEqualityComparer.Instance);
    public List<DynamicJobRegistration> DynamicJobRegistrations { get; } = [];
    private readonly Dictionary<Type, List<DependentJobRegistryEntry>> dependentJobsPerPrincipalJobType = [];

    public IReadOnlyCollection<JobDefinition> GetAllJobs() => [.. allJobs];

    public IReadOnlyCollection<JobDefinition> GetAllCronJobs() => allJobs.Where(c => c.CronExpression is not null).ToList();

    public IReadOnlyCollection<JobDefinition> GetAllOneTimeJobs() => allJobs.Where(c => c.IsStartupJob).ToList();

    public bool IsJobRegistered<T>() => allJobs.Any(j => j.Type == typeof(T));

    public JobDefinition GetJobDefinition<T>() => allJobs.First(j => j.Type == typeof(T));

    public JobDefinition? FindJobDefinition(Type type)
        => allJobs.FirstOrDefault(j => j.Type == type);

    public JobDefinition? FindJobDefinition(string jobName)
        => allJobs.FirstOrDefault(j => j.CustomName == jobName);

    public void Add(JobDefinition jobDefinition)
    {
        AssertNoDuplicateJobNames(jobDefinition.CustomName);

        var isTypeUpdate = allJobs.Any(j => j.JobFullName == jobDefinition.JobFullName);
        if (isTypeUpdate)
        {
            Remove(jobDefinition);
        }

        allJobs.Add(jobDefinition);
    }

    public int GetJobTypeConcurrencyLimit(string jobTypeName)
        => allJobs.FirstOrDefault(j => j.JobFullName == jobTypeName)
            ?.ConcurrencyPolicy
            ?.MaxDegreeOfParallelism ?? 1;

    public void RemoveByName(string jobName) => Remove(allJobs.FirstOrDefault(j => j.CustomName == jobName));

    public void RemoveByType(Type type) => Remove(allJobs.FirstOrDefault(j => j.Type == type));

    public JobDefinition AddDynamicJob(
        Delegate jobDelegate,
        string? jobName = null,
        JobOption? jobOption = null)
    {
        var jobPolicyMetadata = new JobExecutionAttributes(jobDelegate);

        var entry = new JobDefinition(typeof(DynamicJobFactory), null, null, null,
                JobName: DynamicJobNameGenerator.GenerateJobName(jobDelegate),
                JobPolicyMetadata: jobPolicyMetadata) { CustomName = jobName };

        if (jobOption is not null)
        {
            Debug.Assert(jobOption.CronExpression is not null);

            var cron = NCronJobOptionBuilder.GetCronExpression(jobOption.CronExpression);
            entry.CronExpression = cron;
            entry.TimeZone = jobOption.TimeZoneInfo;
            entry.UserDefinedCronExpression = jobOption.CronExpression;
        }

        Add(entry);
        AddDynamicJobRegistration(entry, jobDelegate);

        return entry;
    }

    public IJob GetDynamicJobInstance(IServiceProvider serviceProvider, JobDefinition jobDefinition)
        => DynamicJobRegistrations
            .Single(d => d.JobDefinition.JobFullName == jobDefinition.JobFullName)
            .DynamicJobFactoryResolver(serviceProvider);

    public void RegisterJobDependency(DependentJobRegistryEntry entry)
    {
        if (!dependentJobsPerPrincipalJobType.TryGetValue(entry.PrincipalType, out var entries))
        {
            entries = [];
            dependentJobsPerPrincipalJobType.Add(entry.PrincipalType, entries);
        }

        entries.Add(entry);
    }

    public IReadOnlyCollection<JobDefinition> GetDependentSuccessJobTypes(Type principalType)
        => FilterByAndProject(principalType, v => v.SelectMany(p => p.RunWhenSuccess));

    public IReadOnlyCollection<JobDefinition> GetDependentFaultedJobTypes(Type principalType)
        => FilterByAndProject(principalType, v => v.SelectMany(p => p.RunWhenFaulted));

    private void AddDynamicJobRegistration(JobDefinition jobDefinition, Delegate jobDelegate)
        => DynamicJobRegistrations.Add(new DynamicJobRegistration(jobDefinition, sp => new DynamicJobFactory(sp, jobDelegate)));

    public void UpdateJobDefinitionsToRunAtStartup<TJob>()
    {
        foreach (var jobDefinition in allJobs)
        {
            if (jobDefinition.Type != typeof(TJob))
            {
                continue;
            }

            jobDefinition.IsStartupJob = true;
        }
    }

    private JobDefinition[] FilterByAndProject(
    Type principalJobType,
    Func<IEnumerable<DependentJobRegistryEntry>, IEnumerable<JobDefinition>> transform)

    => !dependentJobsPerPrincipalJobType.TryGetValue(principalJobType, out var types)
        ? []
        : transform(types).ToArray();

    private void Remove(JobDefinition? jobDefinition)
    {
        if (jobDefinition is null)
        {
            return;
        }

        allJobs.Remove(jobDefinition);

        // TODO: Shouldn't we also remove related entries in DependentJobsPerPrincipalJobType?
        // cf. https://github.com/NCronJob-Dev/NCronJob/issues/107
    }

    private void AssertNoDuplicateJobNames(string? additionalJobName = null)
    {
        var duplicateJobName = allJobs
            .Select(c => c.CustomName)
            .Concat([additionalJobName])
            .Where(s => s is not null)
            .GroupBy(s => s)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicateJobName is not null)
        {
            throw new InvalidOperationException($"Duplicate job names found: {string.Join(", ", duplicateJobName)}");
        }
    }

    private sealed class JobDefinitionEqualityComparer : IEqualityComparer<JobDefinition>
    {
        public static readonly JobDefinitionEqualityComparer Instance = new();

        public bool Equals(JobDefinition? x, JobDefinition? y) =>
            (x is null && y is null) || (x is not null && y is not null
                                         && x.Type == y.Type && x.Type != typeof(DynamicJobFactory)
                                         && x.Parameter == y.Parameter
                                         && x.CronExpression == y.CronExpression
                                         && x.TimeZone == y.TimeZone
                                         && x.CustomName == y.CustomName
                                         && x.IsStartupJob == y.IsStartupJob);

        public int GetHashCode(JobDefinition obj) => HashCode.Combine(
            obj.Type,
            obj.Parameter,
            obj.CronExpression,
            obj.TimeZone,
            obj.CustomName,
            obj.IsStartupJob);
    }
}
