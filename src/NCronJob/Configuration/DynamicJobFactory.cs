using System.Reflection;

namespace NCronJob;

internal class DynamicJobFactory : IJob
{
    private readonly IServiceProvider serviceProvider;
    private readonly Func<object[], Task> invoker;
    private readonly Func<IServiceProvider, object>?[] serviceResolvers;
    private readonly ParameterInfo[] parameters;

    public DynamicJobFactory(IServiceProvider serviceProvider, Delegate jobAction)
    {
        ArgumentNullException.ThrowIfNull(jobAction);

        this.serviceProvider = serviceProvider;
        parameters = jobAction.Method.GetParameters();
        serviceResolvers = ServiceResolverHelper.BuildServiceResolvers(parameters);
        invoker = BuildInvoker(jobAction);
    }

    private static Func<object[], Task> BuildInvoker(Delegate jobDelegate)
    {
        var returnType = jobDelegate.Method.ReturnType;

        if (returnType == typeof(Task))
        {
            return DelegateInvoker.Build<Task>(jobDelegate);
        }

        if (returnType == typeof(void))
        {
            var action = DelegateInvoker.BuildAction(jobDelegate);
            return objects => { action(objects); return Task.CompletedTask; };
        }

        throw new InvalidOperationException("The job action must return a Task or void type.");
    }

    public Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        var arguments = ServiceResolverHelper.ResolveArguments(
            serviceProvider,
            parameters,
            serviceResolvers,
            context,
            token);

        return invoker(arguments);
    }
}
