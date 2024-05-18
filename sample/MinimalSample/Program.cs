using LinkDotNet.NCronJob;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddJob((ILogger<Program> logger, TimeProvider timeProvider) =>
{
    logger.LogInformation("Hello World - The current date and time is {Time}", timeProvider.GetLocalNow());
}, "*/40 * * * * *");

await builder.Build().RunAsync();
