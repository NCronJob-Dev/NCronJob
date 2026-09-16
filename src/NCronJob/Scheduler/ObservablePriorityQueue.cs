using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;

namespace NCronJob;

internal class ObservablePriorityQueue<TElement> : ObservablePriorityQueue<TElement, (DateTimeOffset NextRunTime, int Priority)> where TElement : JobRun
{
    public ObservablePriorityQueue(IComparer<(DateTimeOffset NextRunTime, int Priority)> comparer) : base(comparer)
    { }
}

internal class ObservablePriorityQueue<TElement, TPriority> : IEnumerable<TElement>, INotifyCollectionChanged
    where TPriority : IComparable<TPriority>
{
    protected readonly PriorityQueue<TElement, TPriority> PriorityQueue;
#if NET9_0_OR_GREATER
    protected readonly Lock Lock = new();
#else
    protected readonly object Lock = new();
#endif

    public ObservablePriorityQueue(IComparer<TPriority> comparer)
    {
        ArgumentNullException.ThrowIfNull(comparer);
        PriorityQueue = new PriorityQueue<TElement, TPriority>(comparer);
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public void Enqueue([DisallowNull] TElement element, [DisallowNull] TPriority priority)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(priority);

        lock (Lock)
        {
            PriorityQueue.Enqueue(element, priority);
        }

        InformCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, element));
    }

    public bool TryDequeueIf(TElement expected)
    {
        lock (Lock)
        {
            if (!PriorityQueue.TryPeek(out var head, out _) || !EqualityComparer<TElement>.Default.Equals(head, expected))
            {
                return false;
            }

            PriorityQueue.Dequeue();
        }

        InformCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, expected));

        return true;
    }

    public bool TryPeek([MaybeNullWhen(false)] out TElement element, [MaybeNullWhen(false)] out TPriority priority)
    {
        lock (Lock)
        {
            return PriorityQueue.TryPeek(out element, out priority);
        }
    }

    public void Clear()
    {
        lock (Lock)
        {
            PriorityQueue.Clear();
        }
    }

    public int Count
    {
        get
        {
            lock (Lock)
            {
                return PriorityQueue.Count;
            }
        }
    }

    public IEnumerator<TElement> GetEnumerator()
    {
        List<TElement> snapshot;

        lock (Lock)
        {
            snapshot = PriorityQueue.UnorderedItems.Select(item => item.Element).ToList();
        }

        return snapshot.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    protected void InformCollectionChanged(NotifyCollectionChangedEventArgs args) =>
        CollectionChanged?.Invoke(this, args);
}
