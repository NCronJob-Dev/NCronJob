namespace NCronJob.Tests;

public static class TestFailureHelper
{
    public static void DumpContext(Storage storage, IList<ExecutionProgress> events)
    {
        var current = TestContext.Current;
        var output = current.TestOutputHelper!;

        if (current.Warnings is not null)
        {
            output.WriteLine("** Warnings:");

            foreach (var warning in current.Warnings)
            {
                output.WriteLine(warning);
            }
        }

        if (current.TestState is not null && current.TestState.Result == TestResult.Failed)
        {
            output.WriteLine("** Events:");

            foreach (var @event in events)
            {
                output.WriteLine($"{@event.Timestamp:o} {@event.CorrelationId} {@event.State}");
            }

            output.WriteLine("");
            output.WriteLine("** Storage:");

            foreach ((var timestamp, var content) in storage.TimedEntries)
            {
                output.WriteLine($"{timestamp} {content}");
            }
        }
    }

}
