using System.Reflection;
using System.Text;

namespace NCronJob;

internal static class DynamicJobNameGenerator
{
    /// <summary>
    /// Construct a consistent job name from the delegate's method signature and a hash of its target
    /// </summary>
    /// <param name="jobDelegate">The delegate to generate a name for</param>
    /// <returns></returns>
    public static string GenerateJobName(Delegate jobDelegate)
    {
        var methodInfo = jobDelegate.GetMethodInfo();
        var jobNameBuilder = new StringBuilder(methodInfo.DeclaringType!.FullName);
        jobNameBuilder.Append(methodInfo.Name);

        foreach (var param in methodInfo.GetParameters())
        {
            jobNameBuilder.Append(param.ParameterType.Name);
        }
        var jobHash = jobNameBuilder.ToString().GenerateConsistentShortHash();
        return $"UntypedJob_{jobHash}";
    }
}
