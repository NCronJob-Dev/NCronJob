using LinkDotNet.NCronJob;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddJob((ILogger<Program> logger, TimeProvider timeProvider) =>
{
    logger.LogInformation("Hello World - The current date and time is {Time}", timeProvider.GetLocalNow());
}, "*/5 * * * * *");

builder.Services.AddJob(
    [RetryPolicy(retryCount: 4)]
    [SupportsConcurrency(4)]
    (JobExecutionContext context, ILogger<Program> logger) =>
{
    var attemptCount = context.Attempts;

    if (attemptCount <= 4)
    {
        logger.LogWarning("TestRetryJob simulating failure.");
        throw new InvalidOperationException("Simulated operation failure in TestRetryJob.");
    }

    logger.LogInformation($"Job ran after {attemptCount} attempts");
}, "*/7 * * * * *");

await builder.Build().RunAsync();
