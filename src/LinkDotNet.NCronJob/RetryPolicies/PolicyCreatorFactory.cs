namespace LinkDotNet.NCronJob;

internal class PolicyCreatorFactory
{
    private readonly IServiceProvider serviceProvider;

    public PolicyCreatorFactory(IServiceProvider serviceProvider) => this.serviceProvider = serviceProvider;

    public TPolicyCreator Create<TPolicyCreator>() where TPolicyCreator : IPolicyCreator, new()
    {
        var creator = new TPolicyCreator();
        (creator as IInitializablePolicyCreator)?.Initialize(serviceProvider);
        return creator;
    }
}
