namespace LinkDotNet.NCronJob.Execution;
internal static class JobExtensions
{
    public static JobExecutionContext ToContext(this JobDefinition job) => new(job);
}
