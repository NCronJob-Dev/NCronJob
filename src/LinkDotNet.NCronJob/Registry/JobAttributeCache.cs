using System.Collections.Concurrent;
using System.Reflection;

namespace LinkDotNet.NCronJob.Registry;
internal static class JobAttributeCache
{
    private static readonly ConcurrentDictionary<object, JobExecutionAttributes> Cache = new();

    public static JobExecutionAttributes GetJobExecutionAttributes(Type jobType) =>
        Cache.GetOrAdd(jobType, (type) => JobExecutionAttributes.CreateFromType(type as Type));

    public static JobExecutionAttributes GetJobExecutionAttributes(MethodInfo methodInfo) =>
        Cache.GetOrAdd(methodInfo, (info) => JobExecutionAttributes.CreateFromMethodInfo(info as MethodInfo));
}
