namespace LinkDotNet.NCronJob;

/// <summary>
/// Provides a mechanism for initializing policy creators with necessary services.
/// </summary>
/// <remarks>
/// This interface is intended for use within the retry mechanism to allow policy creators to receive
/// and utilize services provided by the application's service provider, facilitating dependency injection.
/// </remarks>
internal interface IInitializablePolicyCreator
{
    /// <summary>
    /// Initializes the policy creator with the specified service provider.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve dependencies needed by the policy creator.</param>
    void Initialize(IServiceProvider serviceProvider);
}
