namespace LinkDotNet.NCronJob;

internal class JobQueueTupleComparer : IComparer<(DateTimeOffset NextRunTime, int Priority)>
{
    public int Compare((DateTimeOffset NextRunTime, int Priority) x, (DateTimeOffset NextRunTime, int Priority) y)
    {
        // First, compare by DateTime
        var timeComparison = DateTimeOffset.Compare(x.NextRunTime, y.NextRunTime);
        if (timeComparison != 0)
            return timeComparison;

        // If times are the same, use the priority where higher values indicate higher priority
        return y.Priority.CompareTo(x.Priority);
    }
}
