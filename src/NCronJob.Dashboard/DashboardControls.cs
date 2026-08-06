namespace NCronJob.Dashboard;

internal sealed class DashboardControls
{
    private readonly IInstantJobRegistry instantJobs;
    private readonly IRuntimeJobRegistry runtimeJobs;
    private readonly JobRegistry registry;
    private readonly NCronJobDashboardOptions options;

    public DashboardControls(
        IInstantJobRegistry instantJobs,
        IRuntimeJobRegistry runtimeJobs,
        JobRegistry registry,
        NCronJobDashboardOptions options)
    {
        this.instantJobs = instantJobs;
        this.runtimeJobs = runtimeJobs;
        this.registry = registry;
        this.options = options;
    }

    public Guid RunNow(string? name, Type? type)
    {
        EnsureEnabled();
        var definition = Find(name, type);
        return definition.CustomName is not null
            ? instantJobs.ForceRunScheduledJob(definition.CustomName, TimeSpan.Zero, definition.Parameter)
            : instantJobs.ForceRunScheduledJob(definition.Type!, TimeSpan.Zero, definition.Parameter);
    }

    public Guid RunNow(string? name, string? typeName) => RunNow(name, ResolveType(typeName));

    public void Enable(string? name, Type? type)
    {
        EnsureEnabled();
        if (name is not null)
        {
            var definition = registry.FindRootJobDefinition(name)
                ?? throw new InvalidOperationException($"Job with name '{name}' not found.");
            runtimeJobs.UpdateSchedule(
                name,
                definition.UserDefinedCronExpression ?? throw new InvalidOperationException("The job has no saved schedule."),
                definition.TimeZone);
        }
        else
        {
            runtimeJobs.EnableJob(type!);
        }
    }

    public void Enable(string? name, string? typeName) => Enable(name, ResolveType(typeName));

    public void Disable(string? name, Type? type)
    {
        EnsureEnabled();
        if (name is not null)
        {
            runtimeJobs.DisableJob(name);
        }
        else
        {
            runtimeJobs.DisableJob(type!);
        }
    }

    public void Disable(string? name, string? typeName) => Disable(name, ResolveType(typeName));

    public void UpdateSchedule(string name, string cronExpression, TimeZoneInfo timeZone)
    {
        EnsureEnabled();
        runtimeJobs.UpdateSchedule(name, cronExpression, timeZone);
    }

    public void UpdateParameter(string name, string parameterJson)
    {
        EnsureEnabled();
        var definition = registry.FindRootJobDefinition(name)
            ?? throw new InvalidOperationException($"Job with name '{name}' not found.");
        runtimeJobs.UpdateParameter(name, ParameterSerializer.Deserialize(parameterJson, definition.Parameter));
    }

    public IReadOnlyList<ScheduleEntry> GetSchedule(DateTimeOffset now)
        => registry.GetAllCronJobs()
            .Select(definition => new ScheduleEntry(
                definition.CustomName,
                definition.Type,
                definition.CustomName ?? definition.Type?.Name ?? "Anonymous job",
                definition.UserDefinedCronExpression ?? string.Empty,
                definition.TimeZone ?? TimeZoneInfo.Utc,
                definition.IsEnabled ? definition.GetNextCronOccurrence(now) : null,
                definition.IsEnabled,
                ParameterSerializer.Serialize(definition.Parameter)))
            .OrderBy(entry => entry.DisplayName, StringComparer.Ordinal)
            .ToArray();

    private JobDefinition Find(string? name, Type? type)
        => name is not null
            ? registry.FindRootJobDefinition(name) ?? throw new InvalidOperationException($"Job with name '{name}' not found.")
            : registry.FindFirstRootJobDefinition(type!) ?? throw new InvalidOperationException($"Job with type '{type}' not found.");

    private Type? ResolveType(string? typeName)
        => typeName is null
            ? null
            : registry.GetAllRootJobs().Select(definition => definition.Type).FirstOrDefault(type => type?.FullName == typeName)
              ?? throw new InvalidOperationException($"Job with type '{typeName}' not found.");

    private void EnsureEnabled()
    {
        if (!options.EnableControls)
        {
            throw new InvalidOperationException("Dashboard controls are disabled.");
        }
    }
}
