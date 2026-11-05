using System.Diagnostics;
using System.Diagnostics.Metrics;

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

    /// <summary>
    /// The name of the <see cref="System.Diagnostics.Metrics.Meter"/> that emits the job run metrics.
    /// </summary>
    /// <example>
    /// <code>builder.Services.AddOpenTelemetry().WithMetrics(m => m.AddMeter(NCronJobDiagnostics.MeterName));</code>
    /// </example>
    public const string MeterName = "NCronJob";

    internal static readonly ActivitySource ActivitySource = new(
        ActivitySourceName,
        typeof(NCronJobDiagnostics).Assembly.GetName().Version?.ToString());
}
