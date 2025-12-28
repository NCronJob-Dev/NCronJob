using System.Text.Json;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace NCronJob.Dashboard.Services;

/// <summary>
/// Service providing dashboard functionality for job management and monitoring.
/// </summary>
[SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "Dashboard service - performance not critical")]
[SuppressMessage("CodeQuality", "S2139:Either log this exception and handle it, or rethrow it with some contextual information", Justification = "Dashboard service - exception already logged")]
public class DashboardService
{
    private readonly IRuntimeJobRegistry runtimeJobRegistry;
    private readonly IInstantJobRegistry instantJobRegistry;
    private readonly IJobExecutionProgressReporter progressReporter;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<DashboardService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardService"/> class.
    /// </summary>
    public DashboardService(
        IRuntimeJobRegistry runtimeJobRegistry,
        IInstantJobRegistry instantJobRegistry,
        IJobExecutionProgressReporter progressReporter,
        TimeProvider timeProvider,
        ILogger<DashboardService> logger)
    {
        this.runtimeJobRegistry = runtimeJobRegistry;
        this.instantJobRegistry = instantJobRegistry;
        this.progressReporter = progressReporter;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    /// <summary>
    /// Gets all recurring jobs with their schedules.
    /// </summary>
    public IReadOnlyCollection<JobInfo> GetAllJobs()
    {
        var recurringJobs = runtimeJobRegistry.GetAllRecurringJobs();
        var utcNow = timeProvider.GetUtcNow();

        return recurringJobs.Select(job =>
        {
            DateTimeOffset? nextExecution = null;
            if (!string.IsNullOrEmpty(job.CronExpression))
            {
                try
                {
                    var cronExpression = Cronos.CronExpression.Parse(job.CronExpression);
                    nextExecution = cronExpression.GetNextOccurrence(utcNow, job.TimeZone);
                }
                catch
                {
                    // If parsing fails, nextExecution remains null
                }
            }

            return new JobInfo
            {
                Name = job.JobName ?? job.Type?.Name ?? "Unknown",
                TypeName = job.Type?.FullName,
                CronExpression = job.CronExpression,
                IsEnabled = job.IsEnabled,
                TimeZone = job.TimeZone,
                NextExecution = nextExecution,
                IsTypedJob = job.IsTypedJob
            };
        }).ToList();
    }

    /// <summary>
    /// Enables a job by name.
    /// </summary>
    public void EnableJob(string jobName)
    {
        try
        {
            runtimeJobRegistry.EnableJob(jobName);
            logger.LogInformation("Job {JobName} enabled via dashboard", jobName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enable job {JobName}", jobName);
            throw;
        }
    }

    /// <summary>
    /// Disables a job by name.
    /// </summary>
    public void DisableJob(string jobName)
    {
        try
        {
            runtimeJobRegistry.DisableJob(jobName);
            logger.LogInformation("Job {JobName} disabled via dashboard", jobName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to disable job {JobName}", jobName);
            throw;
        }
    }

    /// <summary>
    /// Removes a job by name.
    /// </summary>
    public void RemoveJob(string jobName)
    {
        try
        {
            runtimeJobRegistry.RemoveJob(jobName);
            logger.LogInformation("Job {JobName} removed via dashboard", jobName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove job {JobName}", jobName);
            throw;
        }
    }

    /// <summary>
    /// Triggers an instant job execution.
    /// </summary>
    public Guid TriggerInstantJob(string jobName, string? parameterJson)
    {
        try
        {
            object? parameter = null;
            if (!string.IsNullOrWhiteSpace(parameterJson))
            {
                // Try to deserialize as a generic object
                parameter = JsonSerializer.Deserialize<JsonElement>(parameterJson);
            }

            var correlationId = instantJobRegistry.RunScheduledJob(jobName, TimeSpan.Zero, parameter);
            logger.LogInformation("Instant job {JobName} triggered via dashboard with correlation ID {CorrelationId}", 
                jobName, correlationId);
            
            return correlationId;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to trigger instant job {JobName}", jobName);
            throw;
        }
    }

    /// <summary>
    /// Triggers an instant job execution by type.
    /// </summary>
    public Guid TriggerInstantJobByType(Type jobType, string? parameterJson)
    {
        ArgumentNullException.ThrowIfNull(jobType);
        
        try
        {
            object? parameter = null;
            if (!string.IsNullOrWhiteSpace(parameterJson))
            {
                parameter = JsonSerializer.Deserialize<JsonElement>(parameterJson);
            }

            var correlationId = instantJobRegistry.RunScheduledJob(jobType, TimeSpan.Zero, parameter);
            logger.LogInformation("Instant job {JobType} triggered via dashboard with correlation ID {CorrelationId}", 
                jobType.Name, correlationId);
            
            return correlationId;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to trigger instant job {JobType}", jobType.Name);
            throw;
        }
    }

    /// <summary>
    /// Subscribes to job execution progress updates.
    /// </summary>
    public IDisposable SubscribeToJobProgress(Action<ExecutionProgress> callback)
    {
        return progressReporter.Register(callback);
    }
}

/// <summary>
/// Information about a scheduled job.
/// </summary>
public class JobInfo
{
    /// <summary>
    /// Gets or sets the job name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full type name of the job.
    /// </summary>
    public string? TypeName { get; set; }

    /// <summary>
    /// Gets or sets the CRON expression.
    /// </summary>
    public string CronExpression { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the job is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the time zone for the job.
    /// </summary>
    public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Utc;

    /// <summary>
    /// Gets or sets the next scheduled execution time.
    /// </summary>
    public DateTimeOffset? NextExecution { get; set; }

    /// <summary>
    /// Gets or sets whether the job is a typed job (implements IJob).
    /// </summary>
    public bool IsTypedJob { get; set; }
}
