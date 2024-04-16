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

    // Execute the job every 2 minutes
    .AddJob<PrintHelloWorldJob>(p => p.WithCronExpression("*/2 * * * *").WithParameter("Hello from NCronJob"))

    // Execute many TestCancellationJob jobs to test cancellation support and parallel execution
    .AddJob<TestCancellationJob>(p => p.WithCronExpression("*/10 * * * * *", true).WithParameter("Instance 1"))
    .AddJob<TestCancellationJob>(p => p.WithCronExpression("*/9 * * * * *", true).WithParameter("Instance 2"))
    .AddJob<TestCancellationJob>(p => p.WithCronExpression("*/11 * * * * *", true).WithParameter("Instance 3"))
    .AddJob<TestCancellationJob>(p => p.WithCronExpression("*/8 * * * * *", true).WithParameter("Instance 4"))
    .AddJob<TestCancellationJob>(p => p.WithCronExpression("*/10 * * * * *", true).WithParameter("Instance 5"))

    // Register a handler that gets executed when the job is done
    .AddNotificationHandler<HelloWorldJobHandler, PrintHelloWorldJob>()
);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/trigger-instant", async (IInstantJobRegistry instantJobRegistry) =>
    {
        await instantJobRegistry.RunInstantJob<TestCancellationJob>("Hello from instant job!");
    })
    .WithName("TriggerInstantJob")
    .WithOpenApi();

app.Run();
