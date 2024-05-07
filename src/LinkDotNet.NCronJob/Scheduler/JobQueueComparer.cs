namespace LinkDotNet.NCronJob;

internal class JobQueueComparer : IComparer<RegistryEntry>
{
    public int Compare(RegistryEntry? x, RegistryEntry? y)
    {
        if (x == null && y == null)
            return 0;
        if (x == null)
            return -1;
        if (y == null)
            return 1;

        // Compare next scheduled run times
        var nowDateTime = DateTimeOffset.UtcNow;
        var xNextRunTime = x.CronExpression?.GetNextOccurrence(nowDateTime, x.TimeZone);
        var yNextRunTime = y.CronExpression?.GetNextOccurrence(nowDateTime, y.TimeZone);
        var timeComparison = DateTimeOffset.Compare(xNextRunTime.GetValueOrDefault(), yNextRunTime.GetValueOrDefault());

        if (timeComparison != 0)
            return timeComparison;

        // If times are the same, compare by priority (higher priority should come first)
        // Reverse comparison because higher enum values should be prioritized
        return y.Priority.CompareTo(x.Priority);
    }
}

