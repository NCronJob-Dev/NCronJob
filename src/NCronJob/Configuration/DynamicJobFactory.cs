using System.Reflection;
using System.Runtime.CompilerServices;

namespace NCronJob;

internal sealed class DynamicJobFactory : IJob
{
    // Compiling the invoker is expensive and the delegate is fixed per job definition, so compile once per delegate.
    private static readonly ConditionalWeakTable<Delegate, CompiledJob> CompiledJobs = new();

    private readonly IServiceProvider serviceProvider;
    private readonly CompiledJob compiledJob;

    public DynamicJobFactory(IServiceProvider serviceProvider, Delegate jobAction)
    {
        ArgumentNullException.ThrowIfNull(jobAction);

        this.serviceProvider = serviceProvider;
        compiledJob = CompiledJobs.GetValue(jobAction, static action => new CompiledJob(action));
    }

    public Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        var arguments = ServiceResolverHelper.ResolveArguments(
            serviceProvider,
            compiledJob.Parameters,
            compiledJob.ServiceResolvers,
            context,
            token);

        return compiledJob.Invoker(arguments);
    }

    private sealed class CompiledJob
    {
        public CompiledJob(Delegate jobAction)
        {
            Parameters = jobAction.Method.GetParameters();
            ServiceResolvers = ServiceResolverHelper.BuildServiceResolvers(Parameters);
            Invoker = BuildInvoker(jobAction);
        }

        public ParameterInfo[] Parameters { get; }

        public Func<IServiceProvider, object>?[] ServiceResolvers { get; }

        public Func<object[], Task> Invoker { get; }

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
    }
}
