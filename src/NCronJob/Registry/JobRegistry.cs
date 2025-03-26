using System.Diagnostics;

namespace NCronJob;

internal sealed class JobRegistry
{
    private readonly List<JobDefinition> allJobs = [];

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
                Job registration conflict for job '{jobDefinition.Name}' detected. Another job with the same type, parameters, or cron expression already exists.
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

        var entry = JobDefinition.CreateUntyped(jobName, jobDelegate);
        entry.UpdateWith(jobOption);

        Add(entry);

        return entry;
    }

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

    public static void UpdateJobDefinitionsToRunAtStartup(
        IReadOnlyCollection<JobDefinition> jobDefinitions,
        bool shouldCrashOnFailure = false)
    {
        foreach (var jobDefinition in jobDefinitions)
        {
            jobDefinition.UpdateWith(new JobOption() { ShouldCrashOnStartupFailure = shouldCrashOnFailure });
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

    private void AssertNoDuplicateJobNames(string? additionalJobName)
    {
        if (additionalJobName is null)
        {
            return;
        }

        if (!allJobs.Any(jd => jd.CustomName == additionalJobName))
        {
            return;
        }

        throw new InvalidOperationException(
            $"""
            Job registration conflict detected. A job has already been registered with the name '{additionalJobName}'.
            Please use a different name for each job.
            """);
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
