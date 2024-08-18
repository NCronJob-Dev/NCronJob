using NCronJob;
using NCronJob.TestProgram;

var webApp = WebApplication.CreateBuilder();
webApp.Services.AddNCronJob(
    s => s.AddJob<Sample>(p => p.WithCronExpression("* * * * * *"))
        .ExecuteWhen(success: sc => sc.RunJob(() => { })));

await using var run = webApp.Build();
run.MapGet("/", () => string.Empty);
await run.RunAsync();

public partial class Program;
