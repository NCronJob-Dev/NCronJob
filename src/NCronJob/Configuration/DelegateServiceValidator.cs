using Microsoft.Extensions.DependencyInjection;

namespace NCronJob;

internal sealed class DelegateServiceValidator(JobRegistry jobRegistry, IServiceProvider serviceProvider)
{
    public void EnsureDelegateServicesAreRegistered()
    {
        // Without IServiceProviderIsService (some third-party containers) registrations can't be inspected.
        if (serviceProvider.GetService<IServiceProviderIsService>() is not { } registeredServices)
        {
            return;
        }

        var missingServices = jobRegistry.GetAllJobDelegates()
            .SelectMany(d => d.Method.GetParameters())
            .Select(p => p.ParameterType)
            .Where(t => DelegateParameterResolver.IsService(t) && !registeredServices.IsService(t))
            .Distinct()
            .ToList();

        if (missingServices.Count > 0)
        {
            throw new InvalidOperationException(
                $"Job delegates depend on services that are not registered: {string.Join(", ", missingServices)}.");
        }
    }
}
