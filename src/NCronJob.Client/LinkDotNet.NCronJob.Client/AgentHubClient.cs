using System.Net.WebSockets;
using System.Reflection;
using LinkDotNet.NCronJob.Shared;
using LinkDotNet.NCronJob.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinkDotNet.NCronJob.Client;

internal class AgentHubClient : BackgroundService
{
    private readonly ILogger<AgentHubClient> logger;
    private readonly IJobAgentCoordinator agent;
    private readonly HubConnection connection;
    private readonly string agentId;
    private readonly ServiceAgent serviceAgent;
    private readonly AgentHubClientOptions options;
    private readonly SemaphoreSlim startSemaphore = new(1, 1);

    public AgentHubClient(
        ILogger<AgentHubClient> logger,
        HubConnection hubConnection,
        IJobAgentCoordinator agent,
        IConfiguration configuration,
        IHostEnvironment env,
        BufferedSignalRNotifier notifier,  // todo: this is temporary hack until the messaging is fixed, need to instantiate BufferedSignalRNotifier to register the event handlers
        IOptions<AgentHubClientOptions> options)
    {
        this.logger = logger;
        connection = hubConnection;
        this.agent = agent;
        this.options = options.Value;

        string serviceName = configuration.GetValue<string>("NCronJob:ServiceName")
                             ?? Assembly.GetEntryAssembly()?.GetName().Name
                             ?? Assembly.GetExecutingAssembly()?.GetName().Name
                             ?? "UnknownService";
        agentId = WebHostExtensions.GenerateAgentId(serviceName);
        serviceAgent = WebHostExtensions.CreateServiceAgent(configuration, env);

        RegisterHubEventHandlers();
    }

    private void RegisterHubEventHandlers()
    {
        connection.On(nameof(agent.GetJobState), async (Guid jobId) => await SafeExecuteAsync(() => agent.GetJobState(jobId)));
        connection.On(nameof(agent.GetAllJobs), async () => await SafeExecuteAsync(agent.GetAllJobs));
        connection.On<Guid, JobDetailsView>(nameof(agent.GetJobDetails),
            async (jobId) => await SafeExecuteAsync(() => agent.GetJobDetails(jobId)));

        connection.On<Guid, JobActionResult>(nameof(agent.TriggerJob),
            async (jobId) => await agent.TriggerJob(jobId));

        connection.On<Guid, JobActionResult>(nameof(agent.CancelJob),
            async (jobId) => await agent.CancelJob(jobId));

        connection.Reconnected += async (connectionId) =>
        {
            await SafeExecuteAsync(async () =>
            {
                await connection.InvokeAsync("RegisterServiceAgent", serviceAgent);
                logger.LogInformation($"Reconnected with connection ID: {connectionId}");
            });
        };

    }
        
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await startSemaphore.WaitAsync(stoppingToken).ConfigureAwait(false);
        try
        {
            for (int retryAttempt = 0;
                 retryAttempt < options.MaxRetryAttempts && !stoppingToken.IsCancellationRequested;
                 retryAttempt++)
            {
                try
                {
                    await connection.StartAsync(stoppingToken);
                    logger.LogInformation("Agent {AgentId} Connected to Jobs Control Center successfully.", agentId);

                    try
                    {
                        await connection.InvokeAsync("RegisterServiceAgent", serviceAgent, stoppingToken)
                            .ConfigureAwait(false);
                        logger.LogInformation("Service agent registered successfully.");
                        return; // Connection and registration succeeded, so we can exit the loop
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to register service agent.");
                        throw;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    if (IsUnauthorized(ex))
                    {
                        logger.LogError("Unauthorized access to Jobs Control Center. Stopping reconnection attempts.");
                        return;
                    }
                    HandleConnectionError(ex, retryAttempt);
                    await Task.Delay(CalculateBackoff(retryAttempt), stoppingToken);
                }
            }

            logger.LogError("The NCronJobs Jobs Control Center is down. " +
                            "Reached maximum retry attempts and giving up. ");
        }
        finally
        {
            startSemaphore.Release();
        }
    }

    private bool IsUnauthorized(Exception error) => error.Message.Contains("401") || error.Message.Contains("Unauthorized");

    private void HandleConnectionError(Exception ex, int retryAttempt)
    {
        switch (ex)
        {
            case WebSocketException _:
                logger.LogWarning( "WebSocket error on attempt {RetryAttempt}.", retryAttempt);
                break;
            case HttpRequestException _:
                logger.LogWarning( "HTTP request error on attempt {RetryAttempt}.", retryAttempt);
                break;
            default:
                logger.LogWarning( "Connection attempt {RetryAttempt} failed.", retryAttempt);
                break;
        }
    }

    private TimeSpan CalculateBackoff(int retryAttempt)
    {
        var backoff = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt) * options.InitialBackoffSeconds);
        return backoff <= options.MaxBackoff ? backoff : options.MaxBackoff;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await connection.DisposeAsync().ConfigureAwait(false);

        await base.StopAsync(cancellationToken);
    }
        
    private async Task SafeExecuteAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing SignalR hub action.");
        }
    }

    private async Task<T> SafeExecuteAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing SignalR hub action.");
            return default!;
        }
    }
}

public class AgentHubClientOptions
{
    public int MaxRetryAttempts { get; set; } = 10;
    public int InitialBackoffSeconds { get; set; } = 2;
    public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromMinutes(2);
}
