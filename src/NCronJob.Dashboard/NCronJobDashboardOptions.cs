using System.Net;
using Microsoft.AspNetCore.Http;

namespace NCronJob;

/// <summary>Configures the embedded NCronJob dashboard host.</summary>
public sealed class NCronJobDashboardOptions
{
    /// <summary>Gets or sets the TCP port used by the dashboard.</summary>
    public int Port { get; set; } = 8080;

    /// <summary>Gets or sets the address used by the dashboard. The safe default is loopback only.</summary>
    public IPAddress BindAddress { get; set; } = IPAddress.Loopback;

    /// <summary>Gets or sets the path under which the dashboard is served.</summary>
    public string BasePath { get; set; } = "/";

    /// <summary>Gets or sets the maximum number of completed runs retained in memory.</summary>
    public int MaxHistoryEntries { get; set; } = 200;

    /// <summary>Gets or sets whether job control actions are available.</summary>
    public bool EnableControls { get; set; } = true;

    /// <summary>Gets or sets an optional authorization predicate evaluated for every dashboard request.</summary>
    public Func<HttpContext, bool>? Authorize { get; set; }
}
