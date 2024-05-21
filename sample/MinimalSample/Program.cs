using NCronJob;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddNCronJob((ILogger<Program> logger, TimeProvider timeProvider) =>
    logger.LogInformation("Hello World - The current date and time is {Time}", timeProvider.GetLocalNow())
    , "*/5 * * * * *");

await builder.Build().RunAsync();
