
namespace LinkDotNet.NCronJob.Messaging.States;
internal interface IMonitorStateCronJobs
{
    public event Func<JobStateMessage, Task>? JobStatusChanged;
}
