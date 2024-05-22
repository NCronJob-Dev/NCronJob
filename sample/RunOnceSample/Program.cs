using NCronJob;
using RunOnceSample;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddLogging();


builder.Services.AddNCronJob(n => n

    .AddJob<PrintHelloWorldJob>(p =>
        p.WithCronExpression("*/20 * * * * *", timeZoneInfo: TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time")))

    .AddJob<RunAtStartJob>().RunAtStartup()
);

builder.Services.AddNCronJob(options =>
{
    options.AddJob<RunAtStartJob>()
        .AddNotificationHandler<RunAtStartJobHandler>()
        .RunAtStartup();
});


builder.Services.AddNCronJob(b => b.AddJob<RunAtStartJob>().RunAtStartup());


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

await app.RunAsync();
