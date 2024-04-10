using LinkDotNet.NCronJob;
using NCronJobSample;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddLogging();

// Add NCronJob to the container.
builder.Services.AddNCronJob();

// Execute the job every minute
builder.Services.AddCronJob<PrintHelloWorldJob>(p =>
{
    p.CronExpression = "* * * * *";
    p.Parameter = "Hello from NCronJob";
});

// Register a handler that gets executed when the job is done
builder.Services.AddNotificationHandler<HelloWorldJobHandler, PrintHelloWorldJob>();

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

app.Run();
