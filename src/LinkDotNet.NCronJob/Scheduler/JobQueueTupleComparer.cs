namespace LinkDotNet.NCronJob;

internal class JobQueueTupleComparer : IComparer<(DateTime NextRunTime, int Priority)>
{
    public int Compare((DateTime NextRunTime, int Priority) x, (DateTime NextRunTime, int Priority) y)
    {
        // First, compare by DateTime
        var timeComparison = DateTime.Compare(x.NextRunTime, y.NextRunTime);
        if (timeComparison != 0)
            return timeComparison;

        // If times are the same, use the priority where higher values indicate higher priority
        return y.Priority.CompareTo(x.Priority);
    }
}
