namespace NCronJob.Tests;

public static class TestFailureHelper
{
    public static void DumpContext(Storage storage, IList<ExecutionProgress> events)
    {
        var current = TestContext.Current;

        if (current.Warnings is not null)
        {
            current.TestOutputHelper!.WriteLine("** Warnings:");

            foreach (string warning in current.Warnings)
            {
                current.TestOutputHelper!.WriteLine(warning);
            }
        }

        if (current.TestState is not null && current.TestState.Result == TestResult.Failed)
        {
            current.TestOutputHelper!.WriteLine("** Events:");

            foreach (ExecutionProgress @event in events)
            {
                current.TestOutputHelper!.WriteLine($"{@event.Timestamp:o} {@event.CorrelationId} {@event.State}");
            }

            current.TestOutputHelper!.WriteLine("");
            current.TestOutputHelper!.WriteLine("** Storage:");

            foreach ((string timestamp, string content) in storage.TimedEntries)
            {
                current.TestOutputHelper!.WriteLine($"{timestamp} {content}");
            }
        }
    }

}
