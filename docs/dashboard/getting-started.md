# Getting started

Install both lockstep packages:

```shell
dotnet add package NCronJob
dotnet add package NCronJob.Dashboard
```

Register the dashboard after NCronJob, then apply host-time options after `Build()` and before `Run()`:

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddNCronJob(options =>
    options.AddJob<MyJob>(job => job
        .WithName("my-job")
        .WithCronExpression("*/30 * * * * *")));
builder.Services.AddNCronJobDashboard();

using var host = builder.Build();
host.UseNCronJobDashboard(options => options.Port = 5050);
await host.UseNCronJobAsync();
await host.RunAsync();
```

Open `http://localhost:5050/`. The default bind address is `127.0.0.1`.

## Runtime requirement

The host machine must have the ASP.NET Core shared framework for the application's target framework installed. ASP.NET Core web applications already have this requirement. Worker and console deployments must also install the corresponding ASP.NET Core Runtime; the base .NET Runtime alone is insufficient.

## Options

Options can be supplied to `AddNCronJobDashboard` or `UseNCronJobDashboard`; values supplied to `UseNCronJobDashboard` win.

| Option | Default | Purpose |
| --- | --- | --- |
| `Port` | `8080` | Embedded server port; `0` selects an ephemeral port. |
| `BindAddress` | `IPAddress.Loopback` | Address accepted by Kestrel. |
| `BasePath` | `/` | Optional dashboard path base. |
| `MaxHistoryEntries` | `200` | Maximum completed runs retained in memory. |
| `EnableControls` | `true` | Enables mutations from the schedule page. |
| `Authorize` | `null` | Optional predicate evaluated for every request. |
