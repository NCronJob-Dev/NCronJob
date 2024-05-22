using System.Collections.Concurrent;
using System.Reflection;

namespace NCronJob;
internal static class JobAttributeCache
{
    private static readonly ConcurrentDictionary<object, JobExecutionAttributes> Cache = new();

    public static JobExecutionAttributes GetJobExecutionAttributes(Type jobType) =>
        Cache.GetOrAdd(jobType, (type) => JobExecutionAttributes.CreateFromType((Type)type));

    public static JobExecutionAttributes GetJobExecutionAttributes(MethodInfo methodInfo) =>
        Cache.GetOrAdd(methodInfo, (info) => JobExecutionAttributes.CreateFromMethodInfo((MethodInfo)info));
}
