using Serilog;
using Serilog.Events;
using NCronJob.Dashboard.Components;
using NCronJob.Dashboard.Data;
using NCronJob.Dashboard.Hubs;


try
{
    // CreateSlimBuilder will not support https
    var builder = WebApplication.CreateSlimBuilder(args);
    
    // Configure Serilog
    builder.Host.UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration
        .ReadFrom.Configuration(hostingContext.Configuration)
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            "logs/log-.txt",
            rollingInterval: RollingInterval.Day,
            fileSizeLimitBytes: 10 * 1024 * 1024,
            retainedFileCountLimit: 15,
            rollOnFileSizeLimit: true,
            shared: true,
            flushToDiskInterval: TimeSpan.FromSeconds(1)));


    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddSingleton<AgentManager>();
    builder.Services.AddScoped<NotificationService>();
    builder.Services.AddSingleton<EventAggregator>();

    builder.Services.AddEndpointsApiExplorer();


    builder.Services.AddSignalR()
        .AddJsonProtocol(options =>
        {
            options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
            options.PayloadSerializerOptions.IncludeFields = true;
        }).AddHubOptions<AgentHub>(options =>
        {
            options.MaximumReceiveMessageSize = 256 * 1024;
#if DEBUG
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
            options.KeepAliveInterval = TimeSpan.FromSeconds(30);
            options.EnableDetailedErrors = true;
#else
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
        options.KeepAliveInterval = TimeSpan.FromSeconds(10);
#endif
            options.StatefulReconnectBufferSize = 200_000;
            options.MaximumParallelInvocationsPerClient = 2;
            options.StreamBufferCapacity = 15;
        });
    // .AddMessagePackProtocol();

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
        app.UseResponseCompression();
    }

    app.UseHttpsRedirection();

    app.UseStaticFiles();
    app.UseAntiforgery();

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    app.MapHub<AgentHub>("/agent", options =>
    {
        options.AllowStatefulReconnects = true;
    });


    await app.RunAsync();

    
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
