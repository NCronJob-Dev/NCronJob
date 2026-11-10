using NCronJob;
using NCronJob.Dashboard;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddNCronJob(o => o.AddJob<NoOpJob>(j => j.WithCronExpression("* * * * *")));
builder.Services.AddNCronJobDashboard();

var app = builder.Build();
app.MapNCronJobDashboard("/ncronjob");
await app.RunAsync();

internal sealed class NoOpJob : IJob
{
    public Task RunAsync(IJobExecutionContext context, CancellationToken token) => Task.CompletedTask;
}
