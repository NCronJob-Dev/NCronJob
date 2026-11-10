# Dashboard

The `NCronJob.Dashboard` package adds a Blazor Server dashboard to your ASP.NET Core application. It shows:

- **Jobs**: every registered job, its schedule, its next run and its configured parameter. [Startup jobs](startup-jobs.md) are marked *At startup* in the schedule, together with whether a failure stops the application, and their status shows the state and duration of the startup run. Cron jobs can be enabled or disabled, and any job can be triggered as an instant job with a JSON parameter.
- **Dependencies**: the jobs that run after another job succeeds or fails (see [*"Model Dependencies"*](model-dependencies.md)).
- **Runs**: live and recent runs, with what triggered them (cron, instant, startup or dependent), their state history and the parameter each run was started with.

## Setup

```bash
dotnet add package NCronJob.Dashboard
```

```csharp
builder.Services.AddNCronJob(/* your jobs */);
builder.Services.AddNCronJobDashboard();

var app = builder.Build();

app.MapNCronJobDashboard("/ncronjob");
```

The dashboard is now served at `/ncronjob`. It runs in its own branch of the request pipeline, so it doesn't clash with a Blazor app you already host.

## Path, host and port

The first argument of `MapNCronJobDashboard` sets the path the dashboard is served from:

```csharp
app.MapNCronJobDashboard("/admin/jobs");
```

The dashboard runs on your application's own server, so it listens on the same URLs as the rest of the app. To serve it only on a separate port, for example an internal admin port, have the app listen on both ports and map the dashboard only for connections that arrived on the admin port:

```csharp
builder.WebHost.UseUrls("http://*:8080", "http://*:9090");

app.MapWhen(
    context => context.Connection.LocalPort == 9090,
    admin => admin.MapNCronJobDashboard("/ncronjob"));
```

`Connection.LocalPort` is the port of the listener that accepted the connection, so a client on port 8080 can't reach the dashboard. The rest of the app keeps answering on both ports. Behind a reverse proxy or in a container, check the port the app itself listens on, and make sure the admin port isn't exposed publicly.

!!! warning

    Don't use `RequireHost("*:9090")` to isolate a port. It only checks the `Host` header of the request, which the client controls. A client connected to port 8080 can send `Host: example.com:9090` and reach the dashboard. `RequireHost` is fine for routing by host name, for example `endpoints => endpoints.RequireHost("admin.example.com")`, but it is not an access control.

Port isolation is not a replacement for authorization; combine both:

```csharp
app.MapWhen(
    context => context.Connection.LocalPort == 9090,
    admin => admin.MapNCronJobDashboard("/ncronjob", endpoints => endpoints.RequireAuthorization("Admin")));
```

## Securing the dashboard

The dashboard can enable, disable and trigger jobs, so protect it outside of development. `MapNCronJobDashboard` lets you configure its endpoints. The configuration applies to the pages and to the Blazor hub:

```csharp
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Admin", policy => policy.RequireRole("Admin"));

app.UseAuthentication();
app.MapNCronJobDashboard("/ncronjob", endpoints => endpoints.RequireAuthorization("Admin"));
```

## Triggering a job with a JSON parameter

Click **Run** on a job and enter its parameter as JSON:

- The field is prefilled with the configured parameter. If you leave it unchanged or empty, the configured parameter object itself is passed, not a copy deserialized from JSON.
- If the job has a configured parameter, the JSON is deserialized into the type of that parameter, including `string`: `"hello"` becomes `hello`, `null` becomes `null`, and JSON that doesn't fit the type is rejected with an error.
- Otherwise `null` is passed as `null`, a JSON string (`"hello"`) as a `string`, and any other JSON as its raw text, so the job can deserialize it itself.

!!! warning

    Job parameters are of type `object?` and are not type-checked (see [*"Parameters are not type-checked"*](parameters.md#parameters-are-not-type-checked)). If a job has no configured parameter, the dashboard passes a `string`, even when the job expects a complex type. The job then only notices at run time, either through an `InvalidCastException` or through a `null` from a soft cast.

Unnamed jobs are triggered by their type. If a type is registered more than once, give each registration a name with `WithName(...)` to trigger it from the dashboard.

## Parameters

Parameters are shown as JSON. Tables show a shortened one-line preview, and hovering it shows the full JSON. Expanding a run shows its full, indented parameter. A parameter that can't be serialized to JSON, for example an object graph with a cycle or `double.NaN`, is shown by its type name, such as `Node (not serializable to JSON)`. `ToString()` isn't used, because a record's generated `ToString()` follows the same cycle.

## Run history

The run history is kept in memory. It keeps every active run, the outcome of every startup job, and the last 500 finished runs. It is lost when the application restarts. Recording starts when NCronJob starts, so startup jobs run by `UseNCronJobAsync()` are captured even when it is called before `MapNCronJobDashboard`.
