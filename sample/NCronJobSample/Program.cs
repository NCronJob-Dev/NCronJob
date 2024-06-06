using NCronJob;
using NCronJobSample;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddLogging();

// Add NCronJob to the container.
builder.Services.AddNCronJob(n => n

    .AddJob<PrintHelloWorldJob>(p =>
        p.WithCronExpression("*/5 * * * *", timeZoneInfo: TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time")))

    .AddJob<PrintHelloWorldJob>(p =>
        p.WithCronExpression("*/4 * * * * *"))

    // Execute the job every 2 minutes
    .AddJob<DataProcessingJob>(p =>
        p.WithCronExpression("*/5 * * * * *"))

    .AddJob<PrintHelloWorldJob>(p =>
        p.WithCronExpression("*/1 * * * * *").WithParameter("Hello from NCronJob"))

    .AddJob<PrintHelloWorldJob>(p =>
        p.WithCronExpression("*/1 * * * * *").WithParameter("Hello from NCronJob"))
    // Register a handler that gets executed when the job is done
    //.AddNotificationHandler<HelloWorldJobHandler, PrintHelloWorldJob>()

    // Multiple instances of the same job with different cron expressions can be supported
    // by marking the job with [SupportsConcurrency] attribute
    .AddJob<ConcurrentTaskExecutorJob>(p =>
        p.WithCronExpression("*/25 * * * * *"))

    // A job can support retries by marking it with [RetryPolicy(retryCount: 4)] attribute
    .AddJob<TestRetryJob>(p =>
        p.WithCronExpression("*/5 * * * * *"))
);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/trigger-instant", (IInstantJobRegistry instantJobRegistry) =>
{
    instantJobRegistry.RunInstantJob<PrintHelloWorldJob>("Hello from instant job! ###################### Queued, not forced ######################");
})
    .WithName("TriggerInstantJob")
    .WithOpenApi();

app.MapPost("/trigger-instant-forced", (IInstantJobRegistry instantJobRegistry) =>
{
    instantJobRegistry.RunInstantJob<PrintHelloWorldJob>("Hello from instant job! ######################## May the Force be with you ######################",
        forceExecution: true);
})
    .WithSummary("Triggers a job regardless of concurrency setting for that Job Type")
    .WithName("ForceTriggerInstantJob")
    .WithOpenApi();

await app.RunAsync();
