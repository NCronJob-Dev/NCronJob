using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;

namespace NCronJob;

internal class ObservablePriorityQueue<TElement> : ObservablePriorityQueue<TElement, (DateTimeOffset NextRunTime, int Priority)> where TElement : JobDefinition
{
    public ObservablePriorityQueue(IComparer<(DateTimeOffset NextRunTime, int Priority)> comparer) : base(comparer)
    { }

    public void Enqueue([DisallowNull] TElement jobWrapper) =>
        Enqueue(jobWrapper, (jobWrapper.RunAt ?? DateTimeOffset.UtcNow, (int)jobWrapper.Priority));
}
internal class ObservablePriorityQueue<TElement, TPriority> : IEnumerable<TElement>, INotifyCollectionChanged where TPriority : IComparable<TPriority>
{
    protected readonly PriorityQueue<TElement, TPriority> PriorityQueue;
    protected readonly object Lock = new();

    public ObservablePriorityQueue(IComparer<TPriority> comparer)
    {
        ArgumentNullException.ThrowIfNull(comparer);
        PriorityQueue = new PriorityQueue<TElement, TPriority>(comparer);
    }

    public ObservablePriorityQueue(IComparer<TPriority> comparer, int initialCapacity)
    {
        ArgumentNullException.ThrowIfNull(comparer);
        PriorityQueue = new PriorityQueue<TElement, TPriority>(initialCapacity, comparer);
    }

    public ObservablePriorityQueue(IComparer<TPriority> comparer, IEnumerable<(TElement, TPriority)> items)
    {
        ArgumentNullException.ThrowIfNull(comparer);
        PriorityQueue = new PriorityQueue<TElement, TPriority>(comparer);

        foreach (var (element, priority) in items)
        {
            Enqueue(element!, priority);
        }
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public void Enqueue([DisallowNull] TElement element, [DisallowNull] TPriority priority)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(priority);

        lock (Lock)
        {
            PriorityQueue.Enqueue(element, priority);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, element));
        }
    }

    public TElement Dequeue()
    {
        lock (Lock)
        {
            if (PriorityQueue.Count == 0) throw new InvalidOperationException("Queue is empty");

            var element = PriorityQueue.Dequeue();
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, element));

            return element;
        }
    }

    public bool TryDequeue([MaybeNullWhen(false)] out TElement element, [MaybeNullWhen(false)] out TPriority priority)
    {
        lock (Lock)
        {
            if (PriorityQueue.Count == 0)
            {
                element = default;
                priority = default;
                return false;
            }

            if(PriorityQueue.TryDequeue(out element,out priority))
            {
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, element));
                return true;
            }

            return false;
        }
    }

    public bool TryPeek([MaybeNullWhen(false)] out TElement element, [MaybeNullWhen(false)] out TPriority priority)
    {
        lock (Lock)
        {
            if (PriorityQueue.Count == 0)
            {
                element = default;
                priority = default;
                return false;
            }

            return PriorityQueue.TryPeek(out element, out priority);
        }
    }

    public void Clear()
    {
        lock (Lock)
        {
            PriorityQueue.Clear();
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
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

    private void OnCollectionChanged(NotifyCollectionChangedEventArgs args)
    {
        var handler = Interlocked.CompareExchange(ref CollectionChanged, null, null);
        handler?.Invoke(this, args);
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
}
