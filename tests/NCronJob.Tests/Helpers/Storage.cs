using System.Collections.ObjectModel;

namespace NCronJob.Tests;

public sealed class Storage(TimeProvider timeProvider)
{
#if NET9_0_OR_GREATER
    private readonly Lock locker = new();
#else
    private readonly object locker = new();
#endif

    public IList<string> Entries => new ReadOnlyCollection<string>(TimedEntries.Select(e => e.Item2).ToList());
    public IList<(string, string)> TimedEntries { get; } = [];

    public void Add(string content)
    {
        lock (locker)
        {
            TimedEntries.Add((timeProvider.GetUtcNow().ToString("o"), content));
        }
    }
}
