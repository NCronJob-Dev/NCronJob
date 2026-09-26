using System.Diagnostics;

namespace NCronJob;

/// <summary>
/// Diagnostics identifiers emitted by NCronJob.
/// </summary>
public static class NCronJobDiagnostics
{
    /// <summary>
    /// The name of the <see cref="System.Diagnostics.ActivitySource"/> that emits one activity per job run.
    /// </summary>
    /// <example>
    /// <code>builder.Services.AddOpenTelemetry().WithTracing(t => t.AddSource(NCronJobDiagnostics.ActivitySourceName));</code>
    /// </example>
    public const string ActivitySourceName = "NCronJob";

    internal static readonly ActivitySource ActivitySource = new(
        ActivitySourceName,
        typeof(NCronJobDiagnostics).Assembly.GetName().Version?.ToString());
}
