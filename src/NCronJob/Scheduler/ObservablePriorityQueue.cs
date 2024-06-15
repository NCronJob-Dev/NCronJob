using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;

namespace NCronJob;

internal class ObservablePriorityQueue<TElement> : ObservablePriorityQueue<TElement, (DateTimeOffset NextRunTime, int Priority)> where TElement : JobRun
{
    public ObservablePriorityQueue(IComparer<(DateTimeOffset NextRunTime, int Priority)> comparer) : base(comparer)
    { }
}

internal class ObservablePriorityQueue<TElement, TPriority> : IEnumerable<TElement>, INotifyCollectionChanged where TPriority : IComparable<TPriority>
{
    protected readonly PriorityQueue<TElement, TPriority> PriorityQueue;
#if NET8_0
    private readonly object @lock = new();
#else
    private readonly Lock @lock = new();
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

        lock (@lock)
        {
            PriorityQueue.Enqueue(element, priority);
        }

        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, element));
    }

    public TElement Dequeue()
    {
        TElement element;

        lock (@lock)
        {
            if (PriorityQueue.Count == 0)
                throw new InvalidOperationException("Queue is empty");

            element = PriorityQueue.Dequeue();
        }

        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, element));

        return element;
    }

    public bool TryDequeue([MaybeNullWhen(false)] out TElement element, [MaybeNullWhen(false)] out TPriority priority)
    {
        bool result;

        lock (@lock)
        {
            result = PriorityQueue.TryDequeue(out element, out priority);
        }

        if (result)
        {
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, element));
        }

        return result;
    }

    public bool TryPeek([MaybeNullWhen(false)] out TElement element, [MaybeNullWhen(false)] out TPriority priority)
    {
        lock (@lock)
        {
            return PriorityQueue.TryPeek(out element, out priority);
        }
    }

    public void Clear()
    {
        lock (@lock)
        {
            PriorityQueue.Clear();
        }

        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public int Count
    {
        get
        {
            lock (@lock)
            {
                return PriorityQueue.Count;
            }
        }
    }

    private void OnCollectionChanged(NotifyCollectionChangedEventArgs args)
    {
        var handler = Interlocked.CompareExchange(ref CollectionChanged, null, null);
        handler?.Invoke(this, args);
    }

    public IEnumerator<TElement> GetEnumerator()
    {
        List<TElement> snapshot;

        lock (@lock)
        {
            snapshot = PriorityQueue.UnorderedItems.Select(item => item.Element).ToList();
        }

        return snapshot.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
