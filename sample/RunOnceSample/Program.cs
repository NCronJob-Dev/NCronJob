using Microsoft.AspNetCore.Builder;
using NCronJob;
using RunOnceSample;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNCronJob(options =>
    options.AddJob<RunAtStartJob>()
    .RunAtStartup()
    .AddNotificationHandler<RunAtStartJobHandler>());

var app = builder.Build();

await app.RunAsync();
