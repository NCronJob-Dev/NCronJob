using System.Collections.ObjectModel;

namespace NCronJob.Tests;

public sealed class Storage(TimeProvider timeProvider)
{
#if NET9_0_OR_GREATER
    private readonly Lock locker = new();
#else
    private readonly object locker = new();
#endif
    private readonly List<(string, string)> timedEntries = [];

    public IList<string> Entries
    {
        get
        {
            lock (locker)
            {
                return new ReadOnlyCollection<string>(timedEntries.Select(e => e.Item2).ToList());
            }
        }
    }

    public IList<(string, string)> TimedEntries
    {
        get
        {
            lock (locker)
            {
                return new ReadOnlyCollection<(string, string)>([.. timedEntries]);
            }
        }
    }

    public void Add(string content)
    {
        lock (locker)
        {
            timedEntries.Add((timeProvider.GetUtcNow().ToString("o"), content));
        }
    }
}
