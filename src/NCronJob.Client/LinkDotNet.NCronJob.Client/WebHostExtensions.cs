using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using LinkDotNet.NCronJob.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace LinkDotNet.NCronJob.Client;

public static class WebHostExtensions
{

    public static ServiceAgent CreateServiceAgent(IConfiguration configuration, IHostEnvironment env)
    {
        var hostName = DnsTools.ResolveHostName();
        var iPAddress = DnsTools.ResolveHostAddress(hostName);
        ApplyConfigUrls(configuration.GetAspNetCoreUrls());
        var hostName2 = HostName;

        var configSection = configuration.GetSchedulerSection();
        var tags = configSection.GetSection("Tags").Get<List<string>>() ?? [];
        var environment = env.EnvironmentName;

        var serviceName = configSection.GetValue<string>("ServiceName")
                          ?? Assembly.GetEntryAssembly()?.GetName().Name
                          ?? Assembly.GetExecutingAssembly()?.GetName().Name
                          ?? "UnknownService";
        var agentId = GenerateAgentId(serviceName);
        var buildDate = GetBuildDate(typeof(WebHostExtensions).Assembly).ToLongDateString();
            
        var metadata = new Dictionary<string, string> {{"Library", "NCronJob"}};
        var configMetadata = configSection.GetSection("Metadata").GetChildren();
            
        foreach (var item in configMetadata)
        {
            var key = item["Key"]!;
            var value = item["Value"]!;

            if (!string.IsNullOrEmpty(key))
            {
                metadata[key] = value;
            }
        }
        var clientVersion = Assembly.GetExecutingAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;


        var serviceAgent = new ServiceAgent(
            serviceId: agentId,
            serviceName: serviceName,
            ipAddress: GetLocalIpAddress(),
            port: Port,//GetPort(null),
            version: clientVersion,
            environment: environment,
            platform: RuntimeInformation.FrameworkDescription,
            tags: tags,
            healthCheckEndpoint: "/health",
            deregisterCriticalServiceAfter: TimeSpan.FromMinutes(5),
            checkInterval: TimeSpan.FromSeconds(30),
            metadata!,
            host: hostName,
            buildDate
        );

        return serviceAgent;
    }

    public static string GenerateAgentId(string agentNamePrefix)
    {
        var machineName = Environment.MachineName;
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var combined = $"{agentNamePrefix}-{machineName}-{timestamp}";
        return combined;
    }

    public static string GetLocalIpAddress()
    {
        var hostName = Dns.GetHostName(); // Get hostname
        var ipAddresses = Dns.GetHostAddressesAsync(hostName).Result; // Get IP addresses associated with the hostname
        foreach (var ipAddress in ipAddresses)
        {
            if (ipAddress.AddressFamily == AddressFamily.InterNetwork) // Check for IPv4 addresses only
            {
                return ipAddress.ToString(); // Return the first IPv4 address found
            }
        }

        return "127.0.0.1"; // Default to localhost if no IPv4 address is found
    }

    public static void ApplyConfigUrls(List<Uri> addresses)
    {
        if (addresses.Count == 0 && Port == 0)
        {
            // prefer https
            var configAddress = addresses.Find(u => u.Scheme == "https");

            if (configAddress == null)
            {
                configAddress = addresses[0];
            }

            Port = configAddress.Port;

            // only set the host if it isn't a wildcard
            if (!WildcardHosts.Contains(configAddress.Host))
            {
                HostName = configAddress.Host;
            }
        }
    }

    public static string HostName { get; set; }

    public static int Port { get; set; }

    public static List<Uri> GetAspNetCoreUrls(this IConfiguration configuration)
    {
        var urlStringFromConfiguration = configuration["urls"];
        var foundUris = new List<Uri>();

        if (!string.IsNullOrEmpty(urlStringFromConfiguration))
        {
            var addresses = urlStringFromConfiguration.Split(new[]
            {
                ';'
            }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var address in addresses)
            {
                foundUris.AddNewUrisToList(address);
            }
        }

        return foundUris;
    }

    private const string IPV4WildcardHost = "0.0.0.0";
    private const string IPV6WildcardHost = "[::]";

    private static readonly string[] MicrosoftWildcardHosts =
        ["*", "+"];

    public static readonly string[] WildcardHosts =
        [IPV4WildcardHost, IPV6WildcardHost];

    private static void AddNewUrisToList(this List<Uri> addresses, string address)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out Uri uri) &&
            address.Any(a => MicrosoftWildcardHosts.Contains(a.ToString(CultureInfo.InvariantCulture))))
        {
            addresses.AddUniqueUri(address.WildcardUriParse(MicrosoftWildcardHosts, IPV4WildcardHost));
            addresses.AddUniqueUri(address.WildcardUriParse(MicrosoftWildcardHosts, IPV6WildcardHost));
        }
        else
        {
            addresses.AddUniqueUri(uri);
        }
    }

    private static void AddUniqueUri(this List<Uri> addresses, Uri newUri)
    {
        if (!addresses.Contains(newUri) && !string.IsNullOrEmpty(newUri?.ToString()))
        {
            addresses.Add(newUri);
        }
    }
    private static Uri WildcardUriParse(this string source, string[] stringsToReplace, string replaceWith)
    {
        foreach (var currentString in stringsToReplace)
        {
            source = source.Replace(currentString, replaceWith, StringComparison.Ordinal);
        }

        return new Uri(source, UriKind.Absolute);
    }

    private static DateTime GetBuildDate(Assembly? assembly)
    {
        var attribute = assembly?.GetCustomAttribute<BuildDateAttribute>();
        return attribute?.DateTime ?? default;
    }
}

[AttributeUsage(AttributeTargets.Assembly)]
public sealed class BuildDateAttribute : Attribute
{
    public BuildDateAttribute(string value)
    {
        DateTime = DateTime.ParseExact(value, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None);
    }

    public DateTime DateTime { get; }
}

public static class DnsTools
{
    /// <summary>
    /// Get the first listed <see cref="AddressFamily.InterNetwork" /> for the host name.
    /// </summary>
    /// <param name="hostName">
    /// The host name or address to use.
    /// </param>
    /// <returns>
    /// String representation of the IP Address or <see langword="null" />.
    /// </returns>
    public static string ResolveHostAddress(string hostName)
    {
        try
        {
            return Array.Find(Dns.GetHostAddresses(hostName), ip => ip.AddressFamily == AddressFamily.InterNetwork)?.ToString();
        }
        catch (Exception)
        {
            // Ignore
            return null;
        }
    }

    public static string ResolveHostName()
    {
        string result = null;

        try
        {
            result = Dns.GetHostName();

            if (!string.IsNullOrEmpty(result))
            {
                var response = Dns.GetHostEntry(result);
                return response.HostName;
            }
        }
        catch (Exception)
        {
            // Ignore
        }

        return result;
    }
}
