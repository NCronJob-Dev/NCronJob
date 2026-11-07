using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Shouldly;

namespace NCronJob.Tests;

public sealed class MetricsInstrumentTests
{
    public static TheoryData<string, string, string, bool> ExpectedInstruments => new()
    {
        { "ncronjob.job.runs", "{run}", "Number of job runs that started executing.", false },
        { "ncronjob.job.failures", "{run}", "Number of job runs that ended as faulted.", false },
        { "ncronjob.job.retries", "{retry}", "Number of retry attempts of job runs.", false },
        { "ncronjob.job.skipped", "{run}", "Number of job runs that were skipped.", false },
        { "ncronjob.job.expired", "{run}", "Number of job runs that expired before they could start.", false },
        { "ncronjob.job.duration", "s", "Duration of job runs, including retries.", true },
    };

    [Theory]
    [MemberData(nameof(ExpectedInstruments))]
    public void InstrumentsExposeStableNameUnitAndDescription(string name, string unit, string description, bool isHistogram)
    {
        RuntimeHelpers.RunClassConstructor(typeof(NCronJobMetrics).TypeHandle);
        var published = new List<Instrument>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, _) =>
        {
            if (instrument.Meter.Name == NCronJobDiagnostics.MeterName)
            {
                published.Add(instrument);
            }
        };
        listener.Start();

        var instrument = published.Single(i => i.Name == name);

        instrument.Unit.ShouldBe(unit);
        instrument.Description.ShouldBe(description);
        instrument.GetType().GetGenericTypeDefinition().ShouldBe(isHistogram ? typeof(Histogram<>) : typeof(Counter<>));
    }
}
