using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NCronJob.Dashboard.Components;
using System.Threading.Channels;

namespace NCronJob.Dashboard;

internal sealed class DashboardWebHostService : IHostedService, IAsyncDisposable
{
    private readonly IServiceProvider parentServices;
    private readonly NCronJobDashboardOptions options;
    private WebApplication? application;

    public DashboardWebHostService(IServiceProvider parentServices, NCronJobDashboardOptions options)
    {
        this.parentServices = parentServices;
        this.options = options;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ValidateOptions();

        var webOptions = new WebApplicationOptions
        {
            ApplicationName = typeof(DashboardWebHostService).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory,
        };
        var builder = WebApplication.CreateSlimBuilder(webOptions);
        builder.WebHost.UseUrls($"http://{FormatAddress(options.BindAddress)}:{options.Port}");
        builder.Services.AddRazorComponents();
        BridgeParentServices(builder.Services);

        var app = builder.Build();
        var basePath = NormalizeBasePath(options.BasePath);
        if (basePath != "/")
        {
            app.UsePathBase(basePath);
            app.Use((context, next) => context.Request.PathBase.HasValue
                ? next(context)
                : Results.NotFound().ExecuteAsync(context));
        }

        if (options.Authorize is not null)
        {
            app.Use(async (context, next) =>
            {
                if (!options.Authorize(context))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                await next(context);
            });
        }

        app.UseAntiforgery();
        MapDashboardEndpoints(app);
        app.MapRazorComponents<App>();
        await app.StartAsync(cancellationToken);
        application = app;
    }

    private void MapDashboardEndpoints(WebApplication app)
    {
        app.MapGet("/dashboard.js", () =>
        {
            var assembly = typeof(DashboardWebHostService).Assembly;
            var resourceName = assembly.GetManifestResourceNames().Single(name => name.EndsWith("dashboard.js", StringComparison.Ordinal));
            return Results.Stream(assembly.GetManifestResourceStream(resourceName)!, "text/javascript; charset=utf-8");
        });

        app.MapGet("/events", StreamEvents);
        app.MapPost("/api/control", (DashboardControlRequest request) =>
        {
            try
            {
                var controls = parentServices.GetRequiredService<DashboardControls>();
                var result = request.Action switch
                {
                    "run-now" => RunNow(controls, request),
                    "enable" => Execute(() => controls.Enable(request.Name, request.TypeName)),
                    "disable" => Execute(() => controls.Disable(request.Name, request.TypeName)),
                    "schedule" => Execute(() => controls.UpdateSchedule(
                        request.Name ?? throw new InvalidOperationException("A job name is required."),
                        request.CronExpression ?? throw new InvalidOperationException("A cron expression is required."),
                        TimeZoneInfo.FindSystemTimeZoneById(request.TimeZoneId ?? TimeZoneInfo.Utc.Id))),
                    "parameter" => Execute(() => controls.UpdateParameter(
                        request.Name ?? throw new InvalidOperationException("A job name is required."),
                        request.ParameterJson ?? "null")),
                    _ => throw new InvalidOperationException("Unknown dashboard control action."),
                };
                return Results.Ok(result);
            }
            catch (Exception exception)
            {
                return Results.BadRequest(new { Error = exception.Message });
            }
        });
    }

    private async Task StreamEvents(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.ContentType = "text/event-stream";
        var channel = Channel.CreateUnbounded<bool>();
        var store = parentServices.GetRequiredService<DashboardDataStore>();
        void OnChanged() => channel.Writer.TryWrite(true);
        store.Changed += OnChanged;

        try
        {
            await foreach (var _ in channel.Reader.ReadAllAsync(context.RequestAborted))
            {
                await context.Response.WriteAsync("data: changed\n\n", context.RequestAborted);
                await context.Response.Body.FlushAsync(context.RequestAborted);
            }
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            channel.Writer.TryComplete();
        }
        finally
        {
            store.Changed -= OnChanged;
        }
    }

    private static object Execute(Action action)
    {
        action();
        return new { Success = true };
    }

    private static object RunNow(DashboardControls controls, DashboardControlRequest request)
        => new { CorrelationId = controls.RunNow(request.Name, request.TypeName) };

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (application is not null)
        {
            await application.StopAsync(cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (application is not null)
        {
            await application.DisposeAsync();
        }
    }

    private void BridgeParentServices(IServiceCollection services)
    {
        services.AddSingleton(parentServices.GetRequiredService<DashboardDataStore>());
        services.AddSingleton(parentServices.GetRequiredService<DashboardControls>());
        services.AddSingleton(parentServices.GetRequiredService<JobRegistry>());
        services.AddSingleton(parentServices.GetRequiredService<JobExecutionProgressObserver>());
        services.AddSingleton(parentServices.GetRequiredService<IRuntimeJobRegistry>());
        services.AddSingleton(parentServices.GetRequiredService<IInstantJobRegistry>());
        services.AddSingleton(parentServices.GetRequiredService<TimeProvider>());
        services.AddSingleton(options);
        services.AddSingleton<DependencyGraphBuilder>();
    }

    private void ValidateOptions()
    {
        ArgumentNullException.ThrowIfNull(options.BindAddress);
        if (options.Port is < 0 or > 65535)
        {
            throw new InvalidOperationException("Dashboard port must be between 0 and 65535.");
        }

        if (options.MaxHistoryEntries < 0)
        {
            throw new InvalidOperationException("MaxHistoryEntries cannot be negative.");
        }
    }

    private static string NormalizeBasePath(string basePath)
        => string.IsNullOrWhiteSpace(basePath) || basePath == "/" ? "/" : '/' + basePath.Trim('/');

    private static string FormatAddress(System.Net.IPAddress address)
        => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? $"[{address}]" : address.ToString();
}

internal sealed record DashboardControlRequest(
    string Action,
    string? Name,
    string? TypeName,
    string? CronExpression,
    string? TimeZoneId,
    string? ParameterJson);
