namespace NCronJob;

internal static class DependentJobRegistration
{
    public static void Register(
        PendingJobDefinitions pendingJobDefinitions,
        IReadOnlyCollection<JobDefinition> parentJobDefinitions,
        Action<DependencyBuilder>? success,
        Action<DependencyBuilder>? faulted)
    {
        if (success is not null)
        {
            pendingJobDefinitions.Add(parentJobDefinitions, new DependentJobRegistryEntry { RunWhenSuccess = Build(success) });
        }

        if (faulted is not null)
        {
            pendingJobDefinitions.Add(parentJobDefinitions, new DependentJobRegistryEntry { RunWhenFaulted = Build(faulted) });
        }
    }

    private static List<DependentJobDefinition> Build(Action<DependencyBuilder> configure)
    {
        var dependencyBuilder = new DependencyBuilder();
        configure(dependencyBuilder);
        return dependencyBuilder.GetDependentJobOption();
    }
}
