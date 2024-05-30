namespace NCronJob;

internal sealed record DynamicJobRegistration(JobDefinition JobDefinition, Func<IServiceProvider, DynamicJobFactory> DynamicJobFactoryResolver);
