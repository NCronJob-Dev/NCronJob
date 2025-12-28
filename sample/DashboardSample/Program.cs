using DashboardSample.Jobs;
using NCronJob;
using NCronJob.Dashboard;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

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

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

// Map the NCronJob Dashboard
app.UseNCronJobDashboard();

await app.UseNCronJobAsync();
await app.RunAsync();

