namespace LinkDotNet.NCronJob.Shared;

public class ServiceAgent(
    string serviceId,
    string serviceName,
    string ipAddress,
    int port,
    string version,
    string environment,
    string platform,
    List<string> tags,
    string healthCheckEndpoint,
    TimeSpan deregisterCriticalServiceAfter,
    TimeSpan checkInterval,
    Dictionary<string, string> metadata,
    string host,
    string buildDate)
{
    public string ServiceId { get; private set; } = serviceId; // Unique identifier for the service
    public string ServiceName { get; private set; } = serviceName; // Human-readable name of the service
    public string IpAddress { get; private set; } = ipAddress; // IP address where the service is running
    public int Port { get; private set; } = port; // Port on which the service is available
    public string Host { get; private set; } = host; // Hostname where the service is running
    public string BuildDate { get; set; } = buildDate;
    public string Version { get; private set; } = version; // Version of the service
    public string Environment { get; private set; } = environment; // Deployment environment (e.g., development, staging, production)
    public string Platform { get; private set; } = platform; // Platform information (e.g., .NET Core, .NET 7, .NET 8)
    public List<string> Tags { get; private set; } = tags ?? new List<string>(); // Arbitrary tags for categorizing or filtering services
    public string HealthCheckEndpoint { get; private set; } = healthCheckEndpoint; // Endpoint for health checking the service
    public ServiceStatus Status { get; set; } = ServiceStatus.Starting; // Default status at creation
    // Current status of the service
    public DateTimeOffset StartTime { get; private set; } = DateTimeOffset.UtcNow; // The time when the service instance was started
    public TimeSpan DeregisterCriticalServiceAfter { get; private set; } = deregisterCriticalServiceAfter; // Auto-deregistration timeout
    public TimeSpan CheckInterval { get; private set; } = checkInterval; // Health check interval
    public Dictionary<string, string> Metadata { get; private set; } = metadata ?? new Dictionary<string, string>(); // Additional metadata about the service

}

public enum ServiceStatus
{
    Starting,
    Healthy,
    Unhealthy,
    Stopping
}
