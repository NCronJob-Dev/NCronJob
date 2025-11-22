namespace NCronJob;

/// <summary>
/// Options for configuring NCronJob behavior.
/// </summary>
public sealed class NCronJobOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to validate that all jobs and their dependencies
    /// are properly registered when the application starts.
    /// When enabled, this will throw an exception during application startup if any job or its dependencies
    /// cannot be resolved from the service container.
    /// </summary>
    /// <remarks>
    /// This is similar to the ASP.NET Core ValidateOnBuild option and helps catch configuration errors early.
    /// It is recommended to enable this during development.
    /// </remarks>
    public bool ValidateOnBuild { get; set; }
}
