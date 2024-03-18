using LinkDotNet.NCronJob;
using NCronJobSample;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddLogging();

// Add NCronJob to the container.
builder.Services.AddNCronJob(o =>
{
    o.TimerInterval = TimeSpan.FromSeconds(1);
});

// Execute the job every minute
builder.Services.AddCronJob<PrintHelloWorld>(p =>
{
    p.CronExpression = "* * * * *";
    p.Parameter = "Hello from NCronJob";
});

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
        instantJobRegistry.AddInstantJob<PrintHelloWorld>("Hello from instant job!");
    })
    .WithName("TriggerInstantJob")
    .WithOpenApi();

app.Run();
