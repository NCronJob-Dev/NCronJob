using LinkDotNet.NCronJob;
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
        p.WithCronExpression("*/20 * * * * *", timeZoneInfo: TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time")))

    // Execute the job every 2 minutes
    .AddJob<PrintHelloWorldJob>(p =>
        p.WithCronExpression("*/2 * * * *").WithParameter("Hello from NCronJob"))
    // Register a handler that gets executed when the job is done
    .AddNotificationHandler<HelloWorldJobHandler, PrintHelloWorldJob>()

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
    instantJobRegistry.RunInstantJob<PrintHelloWorldJob>("Hello from instant job!");
})
    .WithName("TriggerInstantJob")
    .WithOpenApi();

app.MapPost("/trigger-instant-concurrent", (IInstantJobRegistry instantJobRegistry) =>
{
    instantJobRegistry.RunInstantJob<ConcurrentTaskExecutorJob>();
})
    .WithSummary("Triggers a job that can run concurrently with other instances.")
    .WithDescription(
        """
        This endpoint triggers an instance of 'TestCancellationJob' that is designed 
        to run concurrently with other instances of the same job. Each instance operates 
        independently, allowing parallel processing without mutual interference.
        """)
    .WithName("TriggerConcurrentJob")
    .WithOpenApi();

app.Run();
