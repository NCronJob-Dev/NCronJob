using LinkDotNet.NCronJob.Shared;
using Microsoft.Extensions.Logging;

namespace LinkDotNet.NCronJob.Client;

public interface IJobService
{
    List<JobDetailsView> GetAllJobs();
    JobDetailsView GetJobDetails(Guid jobId);
    Task<(bool Success, string Message)> TriggerJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<(bool Success, string Message)> CancelRunningJobAsync(Guid jobId);
}

internal class JobService : IJobService
{
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger logger;
    private readonly JobRegistry cronRegistry;

    public JobService(IServiceProvider sp,
        ILogger<JobService> logger,
        JobRegistry cronRegistry)
    {
        serviceProvider = sp;
        this.logger = logger;
        this.cronRegistry = cronRegistry;
    }

    public List<JobDetailsView> GetAllJobs()
    {
        var jobs = cronRegistry.GetAllCronJobs()
            .Select(p => new JobDetailsView(new JobExecutionContext(p)));  //todo: this cannot be create a new context here

        return jobs.ToList();
    }

    public JobDetailsView GetJobDetails(Guid jobId)
    {
        var jobs = cronRegistry.GetAllCronJobs();
        var jobDetails = jobs.First(j => jobId == j.JobId);
        if (jobDetails == null)
        {
            throw new ArgumentException($"Job {jobId} not found");
        }
        var runContext = new JobExecutionContext(jobDetails); //todo: this cannot be create a new context here
        var info = new JobDetailsView(runContext);
        return info;
    }

    public async Task<(bool Success, string Message)> TriggerJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var jobInstance = GetJobDetails(jobId);

        //try
        //{
        //    _ = Task.Run(async () =>
        //    {
        //        try
        //        {
        //            using (_logger.BeginScope(new Dictionary<string, object>
        //                   {
        //                       { "JobName", jobId },
        //                       { "JobTypeFullName", jobInstance.JobTypeFullName }
        //                   }))
        //            {
        //                await jobInstance.RunJobAsync(cancellationToken);
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.LogError(ex, "Error occurred while executing the triggered job {JobName}", jobId);
        //            // Consider additional error handling here, e.g., updating job state or notifying control panel.
        //        }
        //    });

        //    await Task.Delay(100); // Optional: Add a small delay to allow the job to start before returning
        //    _logger.LogInformation("Job has been successfully triggered: {JobName}", jobId);
        //    return (true, $"The Job '{jobId}' has been triggered successfully");
        //}
        //catch (Exception ex)
        //{
        //    _logger.LogError(ex, "Error triggering job {JobName}", jobId);
        //    return (false, $"Error triggering '{jobId}' job: {ex.Message}");
        //}

        logger.LogInformation("Job has been successfully triggered: {JobName}", jobId);
        return (true, "Job triggered successfully");
    }

    public async Task<(bool Success, string Message)> CancelRunningJobAsync(Guid jobId)
    {
        //var jobInstance = GetJobDetails(jobName);
        //try
        //{
        //    jobInstance.CancelJob();
        //    _logger.LogInformation("Job has been successfully cancelled: {JobName}", jobName);
        //    return (true, $"The Job '{jobName}' has been cancelled successfully");
        //}
        //catch (Exception ex)
        //{
        //    _logger.LogError(ex, "Error cancelling job {JobName}", jobName);
        //    return (false, $"Error cancelling '{jobName}' job: {ex.Message}");
        //}

        logger.LogInformation("Job has been successfully cancelled: {JobId}", jobId);
        return (true, "Job cancelled successfully");
    }
}
