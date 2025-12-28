using DashboardSample.Jobs;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NCronJob;
using NCronJob.Dashboard;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on a specific port
builder.WebHost.UseUrls("http://localhost:5000");

// Add NCronJob with sample jobs
builder.Services.AddNCronJob(options =>
{
    options.AddJob<HelloWorldJob>(j => j
        .WithCronExpression("*/10 * * * * *") // Every 10 seconds
        .WithName("Hello World Job"));

    options.AddJob<DataProcessingJob>(j => j
        .WithCronExpression("0 */2 * * * *") // Every 2 minutes
        .WithParameter("Sample Data")
        .WithName("Data Processing"));

    options.AddJob<ReportGenerationJob>(j => j
        .WithCronExpression("0 0 */1 * * *") // Every hour
        .WithName("Report Generation"));
});

// Add NCronJob Dashboard
builder.Services.AddNCronJobDashboard();

var app = builder.Build();

// Use antiforgery middleware
app.UseAntiforgery();

// Map the NCronJob Dashboard
app.UseNCronJobDashboard();

Console.WriteLine("NCronJob Dashboard is running at http://localhost:5000");
Console.WriteLine("Press Ctrl+C to shut down.");

await app.UseNCronJobAsync();
await app.RunAsync();
