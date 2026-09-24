namespace NCronJob;

internal static class ExecuteWhenHelper
{
    public static void AddRegistration(
        JobDefinitionCollector jobDefinitionCollector,
        IReadOnlyCollection<JobDefinition> parentJobDefinitions,
        Action<DependencyBuilder>? success,
        Action<DependencyBuilder>? faulted)
    {
        if (success is not null)
        {
            jobDefinitionCollector.Add(parentJobDefinitions, new DependentJobRegistryEntry { RunWhenSuccess = Build(success) });
        }

        if (faulted is not null)
        {
            jobDefinitionCollector.Add(parentJobDefinitions, new DependentJobRegistryEntry { RunWhenFaulted = Build(faulted) });
        }
    }

    private static List<DependentJobDefinition> Build(Action<DependencyBuilder> configure)
    {
        var dependencyBuilder = new DependencyBuilder();
        configure(dependencyBuilder);
        return dependencyBuilder.GetDependentJobOption();
    }
}
