using System.Diagnostics;

namespace NCronJob;

internal sealed class JobRegistry
{
    private readonly List<JobDefinition> allJobs = [];
    public List<DynamicJobRegistration> DynamicJobRegistrations { get; } = [];
    private readonly Dictionary<JobDefinition, List<DependentJobRegistryEntry>> dependentJobsPerJobDefinition
        = new(DependentJobDefinitionEqualityComparer.Instance);

    public IReadOnlyCollection<JobDefinition> GetAllJobs() => [.. allJobs];

    public IReadOnlyCollection<JobDefinition> GetAllCronJobs() => allJobs.Where(c => c.CronExpression is not null).ToList();

    public IReadOnlyCollection<JobDefinition> GetAllOneTimeJobs() => allJobs.Where(c => c.IsStartupJob).ToList();

    public IReadOnlyCollection<JobDefinition> FindAllJobDefinition(Type type)
        => allJobs.Where(j => j.Type == type).ToList();

    public JobDefinition? FindFirstJobDefinition(Type type)
        => allJobs.FirstOrDefault(j => j.Type == type);

    public JobDefinition? FindJobDefinition(string jobName)
        => allJobs.FirstOrDefault(j => j.CustomName == jobName);

    public void Add(JobDefinition jobDefinition)
    {
        AssertNoDuplicateJobNames(jobDefinition.CustomName);

        if (allJobs.Contains(jobDefinition, JobDefinitionEqualityComparer.Instance))
        {
            throw new InvalidOperationException(
                $"""
                Job registration conflict for type '{jobDefinition.Type.Name}' detected. Another job with the same type, parameters, or cron expression already exists.
                Please either remove the duplicate job, change its parameters, or assign a unique name to it if duplication is intended.
                """);
        }

        allJobs.Add(jobDefinition);
    }

    public int GetJobTypeConcurrencyLimit(string jobTypeName)
        => allJobs.FirstOrDefault(j => j.JobFullName == jobTypeName)
            ?.ConcurrencyPolicy
            ?.MaxDegreeOfParallelism ?? 1;

    public void RemoveByName(string jobName) => Remove(allJobs.FirstOrDefault(j => j.CustomName == jobName));

    public void RemoveByType(Type type)
    {
        var jobDefinitions = FindAllJobDefinition(type);

        foreach (var jobDefinition in jobDefinitions)
        {
            Remove(jobDefinition);
        }
    }

    public JobDefinition AddDynamicJob(
        Delegate jobDelegate,
        string? jobName = null,
        JobOption? jobOption = null)
    {
        var jobPolicyMetadata = new JobExecutionAttributes(jobDelegate);

        var entry = JobDefinition.CreateUntyped(DynamicJobNameGenerator.GenerateJobName(jobDelegate), jobPolicyMetadata) with
        {
            CustomName = jobName
        };

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

    public void RegisterJobDependency(IReadOnlyCollection<JobDefinition> parentJobdefinitions, DependentJobRegistryEntry entry)
    {
        foreach (var jobDefinition in parentJobdefinitions)
        {
            if (!dependentJobsPerJobDefinition.TryGetValue(jobDefinition, out var entries))
            {
                entries = [];
                dependentJobsPerJobDefinition.Add(jobDefinition, entries);
            }

            entries.Add(entry);
        }
    }

    public IReadOnlyCollection<JobDefinition> GetDependentSuccessJobTypes(JobDefinition parentJobDefinition)
        => FilterByAndProject(parentJobDefinition, v => v.SelectMany(p => p.RunWhenSuccess));

    public IReadOnlyCollection<JobDefinition> GetDependentFaultedJobTypes(JobDefinition parentJobDefinition)
        => FilterByAndProject(parentJobDefinition, v => v.SelectMany(p => p.RunWhenFaulted));

    private void AddDynamicJobRegistration(JobDefinition jobDefinition, Delegate jobDelegate)
        => DynamicJobRegistrations.Add(new DynamicJobRegistration(jobDefinition, sp => new DynamicJobFactory(sp, jobDelegate)));

    public static void UpdateJobDefinitionsToRunAtStartup(
        IReadOnlyCollection<JobDefinition> jobDefinitions,
        bool shouldCrashOnFailure = false)
    {
        foreach (var jobDefinition in jobDefinitions)
        {
            jobDefinition.ShouldCrashOnStartupFailure = shouldCrashOnFailure;
        }
    }

    private JobDefinition[] FilterByAndProject(
        JobDefinition parentJobDefinition,
        Func<IEnumerable<DependentJobRegistryEntry>, IEnumerable<JobDefinition>> transform)
    => !dependentJobsPerJobDefinition.TryGetValue(parentJobDefinition, out var types)
        ? []
        : transform(types).ToArray();

    private void Remove(JobDefinition? jobDefinition)
    {
        if (jobDefinition is null)
        {
            return;
        }

        allJobs.Remove(jobDefinition);

        dependentJobsPerJobDefinition.Remove(jobDefinition);
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
            throw new InvalidOperationException(
                $"""
                Job registration conflict detected. Duplicate job names found: {duplicateJobName}.
                Please use a different name for each job.
                """);
        }
    }

    private sealed class JobDefinitionEqualityComparer : IEqualityComparer<JobDefinition>
    {
        public static readonly JobDefinitionEqualityComparer Instance = new();

        public bool Equals(JobDefinition? x, JobDefinition? y) =>
            (x is null && y is null) || (x is not null && y is not null
                                         && x.Type == y.Type && x.IsTypedJob
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

    private sealed class DependentJobDefinitionEqualityComparer : IEqualityComparer<JobDefinition>
    {
        // TODO: Maybe is the code conflating two different concepts.
        // Dependent jobs may have a name, a type and a parameter, but that's the most of it.
        // And the code currently uses the same type to hold the configuration of "lead" jobs
        // and dependent jobs.
        //
        // Which brings this dependent job only comparer.
        //
        // Maybe should a DependentJobDefinition type spawn?

        public static readonly DependentJobDefinitionEqualityComparer Instance = new();

        public bool Equals(JobDefinition? x, JobDefinition? y) =>
            (x is null && y is null) || (x is not null && y is not null
                                         && x.Type == y.Type && x.IsTypedJob
                                         && x.Parameter == y.Parameter
                                         && x.CustomName == y.CustomName);

        public int GetHashCode(JobDefinition obj) => HashCode.Combine(
            obj.Type,
            obj.Parameter,
            obj.CustomName
            );
    }
}
