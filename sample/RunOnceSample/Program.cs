using Microsoft.AspNetCore.Builder;
using NCronJob;
using RunOnceSample;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNCronJob(options =>
    options
        .AddJob<RunAtStartJob>(j => j.RunAtStartup())
        .AddNotificationHandler<RunAtStartJobHandler>());

var app = builder.Build();

await app.UseNCronJobAsync();

await app.RunAsync();
