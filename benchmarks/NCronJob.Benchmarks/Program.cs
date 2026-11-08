using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NCronJob;

// Usage: dotnet run -c Release -- <scenario> <count>
// Prints exactly one JSON line with wall time, CPU time, allocations, GC counts and memory figures.
var scenario = args.Length > 0 ? args[0] : "instant";
var count = args.Length > 1 ? int.Parse(args[1]) : 20_000;

var result = scenario switch
{
    "instant" => await Scenarios.InstantJobs(count, dependents: false),
    "dependent" => await Scenarios.InstantJobs(count, dependents: true),
    "startup-cron" => await Scenarios.StartupCronJobs(count),
    "runtime-churn" => await Scenarios.RuntimeChurn(count),
    "cron-burst" => await Scenarios.CronBurst(count),
    _ => throw new ArgumentException($"Unknown scenario '{scenario}'."),
};

Console.WriteLine(JsonSerializer.Serialize(result with { Scenario = scenario, Count = count }));

internal sealed record Measurement(
    string Scenario,
    int Count,
    double WallMs,
    double CpuMs,
    double AllocatedMb,
    int Gen0,
    int Gen1,
    int Gen2,
    double PeakManagedMb,
    double PeakWorkingSetMb,
    double RetainedMb);

internal static class Scenarios
{
    private static int completed;
    private static int expected;
    private static TaskCompletionSource allDone = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static void JobCompleted()
    {
        if (Interlocked.Increment(ref completed) == Volatile.Read(ref expected))
        {
            allDone.TrySetResult();
        }
    }

    private static void Arm(int expectedRuns)
    {
        completed = 0;
        expected = expectedRuns;
        allDone = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static IHost BuildHost(Action<NCronJobOptionBuilder> configure)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddNCronJob(configure);
        return builder.Build();
    }

    private static IHost BuildInstantHost(bool dependents) => BuildHost(o =>
    {
        o.WithMaxDegreeOfParallelism(64);
        o.AddJob<InstantJobA>();
        o.AddJob<InstantJobB>();
        o.AddJob<InstantJobC>();
        o.AddJob<InstantJobD>();
        if (dependents)
        {
            o.AddJob<RootJob>().ExecuteWhen(success: s => s.RunJob<ChildJobA>().RunJob<ChildJobB>());
        }
    });

    private static void EnqueueBatch(IInstantJobRegistry registry, int runs, bool dependents)
    {
        for (var i = 0; i < runs; i++)
        {
            if (dependents)
            {
                registry.RunScheduledJob<RootJob>(TimeSpan.Zero);
                continue;
            }

            switch (i % 4)
            {
                case 0: registry.RunScheduledJob<InstantJobA>(TimeSpan.Zero); break;
                case 1: registry.RunScheduledJob<InstantJobB>(TimeSpan.Zero); break;
                case 2: registry.RunScheduledJob<InstantJobC>(TimeSpan.Zero); break;
                default: registry.RunScheduledJob<InstantJobD>(TimeSpan.Zero); break;
            }
        }
    }

    public static async Task<Measurement> InstantJobs(int count, bool dependents)
    {
        var runsPerEnqueue = dependents ? 3 : 1;

        // The warm-up runs on its own host that is stopped completely, so JIT and DI caches are warm
        // but none of its cleanup can overlap the measured batch.
        using (var warmupHost = BuildInstantHost(dependents))
        {
            await warmupHost.StartAsync();
            Arm(2_000 * runsPerEnqueue);
            EnqueueBatch(warmupHost.Services.GetRequiredService<IInstantJobRegistry>(), 2_000, dependents);
            await allDone.Task;
            await warmupHost.StopAsync();
        }

        using var host = BuildInstantHost(dependents);
        await host.StartAsync();
        var registry = host.Services.GetRequiredService<IInstantJobRegistry>();

        Arm(count * runsPerEnqueue);

        // StopAsync waits for all running jobs, so completion notifications, scope disposal and
        // concurrency slot release are part of the measurement and not left over.
        return await Measure(async () =>
        {
            EnqueueBatch(registry, count, dependents);
            await allDone.Task;
            await host.StopAsync();
        });
    }

    public static async Task<Measurement> StartupCronJobs(int count)
    {
        using var host = BuildHost(o =>
        {
            for (var i = 0; i < count; i++)
            {
                o.AddJob(() => { }, "0 0 1 1 *", null, $"job-{i}");
            }
        });

        return await Measure(async () =>
        {
            await host.StartAsync();
            await Task.Delay(500);
            await host.StopAsync();
        });
    }

    // Every job fires on the same second, so all of them compete for the global parallelism limit at once.
    // Wall time is bound by the clock; CPU time and allocations are what this scenario measures.
    public static async Task<Measurement> CronBurst(int count)
    {
        const int ticks = 5;
        using var host = BuildHost(o =>
        {
            for (var i = 0; i < count; i++)
            {
                o.AddJob(JobCompleted, "* * * * * *", null, $"job-{i}");
            }
        });

        Arm(count * ticks);

        return await Measure(async () =>
        {
            await host.StartAsync();
            await allDone.Task;
            await host.StopAsync();
        });
    }

    public static async Task<Measurement> RuntimeChurn(int count)
    {
        using var host = BuildHost(_ => { });
        await host.StartAsync();
        var registry = host.Services.GetRequiredService<IRuntimeJobRegistry>();

        // StopAsync waits for the workers of the removed queues, so their clean-up is part of the measurement.
        return await Measure(async () =>
        {
            for (var i = 0; i < count; i++)
            {
                var name = $"job-{i}";
                registry.TryRegister(b => b.AddJob(() => { }, "0 0 1 1 *", null, name), out var exception);
                if (exception is not null)
                {
                    throw exception;
                }
            }

            for (var i = 0; i < count; i++)
            {
                var name = $"job-{i}";
                registry.UpdateSchedule(name, "0 0 2 1 *");
                registry.DisableJob(name);
                registry.EnableJob(name);
            }

            for (var i = 0; i < count; i++)
            {
                registry.RemoveJob($"job-{i}");
            }

            await host.StopAsync();
        });
    }

    private static async Task<Measurement> Measure(Func<Task> work)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var baselineHeap = GC.GetTotalMemory(true);
        var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        var gen0 = GC.CollectionCount(0);
        var gen1 = GC.CollectionCount(1);
        var gen2 = GC.CollectionCount(2);
        var cpuBefore = Process.GetCurrentProcess().TotalProcessorTime;

        var peakHeap = baselineHeap;
        using var samplerStop = new CancellationTokenSource();
        var sampler = Task.Run(async () =>
        {
            while (!samplerStop.IsCancellationRequested)
            {
                peakHeap = Math.Max(peakHeap, GC.GetTotalMemory(false));
                await Task.Delay(2);
            }
        });

        var stopwatch = Stopwatch.StartNew();
        await work();
        stopwatch.Stop();

        var cpuAfter = Process.GetCurrentProcess().TotalProcessorTime;
        var allocatedAfter = GC.GetTotalAllocatedBytes(precise: true);
        var gen0After = GC.CollectionCount(0);
        var gen1After = GC.CollectionCount(1);
        var gen2After = GC.CollectionCount(2);

        await samplerStop.CancelAsync();
        await sampler;

        var retained = GC.GetTotalMemory(true) - baselineHeap;
        const double mb = 1024.0 * 1024.0;

        return new Measurement(
            string.Empty,
            0,
            stopwatch.Elapsed.TotalMilliseconds,
            (cpuAfter - cpuBefore).TotalMilliseconds,
            (allocatedAfter - allocatedBefore) / mb,
            gen0After - gen0,
            gen1After - gen1,
            gen2After - gen2,
            (peakHeap - baselineHeap) / mb,
            Process.GetCurrentProcess().PeakWorkingSet64 / mb,
            retained / mb);
    }
}

[SupportsConcurrency(16)]
internal sealed class InstantJobA : IJob
{
    public Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        Scenarios.JobCompleted();
        return Task.CompletedTask;
    }
}

[SupportsConcurrency(16)]
internal sealed class InstantJobB : IJob
{
    public Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        Scenarios.JobCompleted();
        return Task.CompletedTask;
    }
}

[SupportsConcurrency(16)]
internal sealed class InstantJobC : IJob
{
    public Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        Scenarios.JobCompleted();
        return Task.CompletedTask;
    }
}

[SupportsConcurrency(16)]
internal sealed class InstantJobD : IJob
{
    public Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        Scenarios.JobCompleted();
        return Task.CompletedTask;
    }
}

[SupportsConcurrency(16)]
internal sealed class RootJob : IJob
{
    public Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        Scenarios.JobCompleted();
        return Task.CompletedTask;
    }
}

[SupportsConcurrency(16)]
internal sealed class ChildJobA : IJob
{
    public Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        Scenarios.JobCompleted();
        return Task.CompletedTask;
    }
}

[SupportsConcurrency(16)]
internal sealed class ChildJobB : IJob
{
    public Task RunAsync(IJobExecutionContext context, CancellationToken token)
    {
        Scenarios.JobCompleted();
        return Task.CompletedTask;
    }
}
