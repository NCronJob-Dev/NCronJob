using DashboardSample;
using NCronJob;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddNCronJob(options => options
    .AddJob<PipelineJob>(job => job
        .WithName("pipeline")
        .WithCronExpression("*/8 * * * * *")
        .WithParameter(new PipelineOptions("eu-central", 3)))
    .ExecuteWhen(
        success => success.RunJob<ArchiveJob>(new { Bucket = "daily" }),
        faulted => faulted.RunJob<AlertJob>(new { Channel = "operations" })));
builder.Services.AddNCronJobDashboard();

using var host = builder.Build();
host.UseNCronJobDashboard(options => options.Port = 5050);
await host.UseNCronJobAsync();
await host.RunAsync();
