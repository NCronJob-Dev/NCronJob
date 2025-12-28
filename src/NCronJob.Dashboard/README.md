# NCronJob.Dashboard

A Blazor-based real-time dashboard for monitoring and managing NCronJob scheduled jobs.

## Features

- **Real-time Monitoring**: Live updates of job execution progress using the observer pattern
- **Job Management**: Enable, disable, or remove scheduled jobs on the fly
- **CRON Schedule Visualization**: View CRON expressions and next execution times
- **Instant Job Triggering**: Trigger instant job executions with custom JSON parameters
- **Pure Blazor Server**: No JavaScript dependencies, fully server-side rendered with Interactive Server mode
- **Clean UI**: Modern, responsive design with custom CSS

## Installation

Add the NCronJob.Dashboard package to your ASP.NET Core application:

```bash
dotnet add package NCronJob.Dashboard
```

## Usage

### 1. Add Dashboard Services

In your `Program.cs` or `Startup.cs`, add the NCronJob Dashboard services:

```csharp
using NCronJob;
using NCronJob.Dashboard;

var builder = WebApplication.CreateBuilder(args);

// Add NCronJob with your jobs
builder.Services.AddNCronJob(options =>
{
    options.AddJob<MyJob>(j => j
        .WithCronExpression("0 */5 * * * *")
        .WithName("My Scheduled Job"));
});

// Add the NCronJob Dashboard
builder.Services.AddNCronJobDashboard();

var app = builder.Build();

// ... other middleware

// Map the dashboard endpoint
app.UseNCronJobDashboard();

await app.UseNCronJobAsync();
await app.RunAsync();
```

### 2. Access the Dashboard

Navigate to `/ncronjob-dashboard` in your browser to view the dashboard.

### 3. Customization (Optional)

You can customize the dashboard by providing options:

```csharp
builder.Services.AddNCronJobDashboard(options =>
{
    options.Title = "My Job Dashboard";
    options.ShowJobParameters = true;
    options.RefreshIntervalMs = 1000; // Refresh interval in milliseconds
});
```

You can also specify a custom URL pattern:

```csharp
app.UseNCronJobDashboard("/admin/jobs");
```

## Dashboard Features

### Job List

The dashboard displays all registered scheduled jobs with the following information:

- **Job Name**: The name of the job
- **Job Type**: The fully qualified type name
- **CRON Expression**: The schedule in CRON notation
- **Time Zone**: The timezone used for scheduling
- **Next Execution**: When the job will run next
- **Status**: Whether the job is enabled or disabled

### Job Actions

For each job, you can:

- **Enable/Disable**: Toggle the job's enabled state
- **Trigger**: Execute the job immediately with optional JSON parameters
- **Remove**: Remove the job from the scheduler

### Activity Log

The dashboard shows a real-time activity log of job executions, including:

- Job start times
- Job completion status
- Job states (Running, Completed, Failed, etc.)

## Security Considerations

The dashboard provides full control over your scheduled jobs. In production environments, you should:

1. **Secure the dashboard endpoint** using authentication and authorization
2. **Restrict access** to authorized users only
3. **Use HTTPS** to encrypt traffic
4. **Consider network-level restrictions** if the dashboard should only be accessible internally

Example with authorization:

```csharp
app.MapGroup("/ncronjob-dashboard")
    .RequireAuthorization("AdminOnly") // Add your authorization policy
    .UseNCronJobDashboard();
```

## Requirements

- .NET 8.0 or later
- ASP.NET Core application with support for Blazor Server

## Sample Application

Check out the [DashboardSample](../../sample/DashboardSample/) project for a complete working example.

## License

This project is licensed under the MIT License - see the LICENSE file for details.
