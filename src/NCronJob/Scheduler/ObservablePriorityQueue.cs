using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;

namespace NCronJob;

internal class ObservablePriorityQueue<TElement> : ObservablePriorityQueue<TElement, (DateTimeOffset NextRunTime, int Priority)> where TElement : JobRun
{
    public ObservablePriorityQueue(IComparer<(DateTimeOffset NextRunTime, int Priority)> comparer) : base(comparer)
    { }

    protected void RemoveByPredicate(Func<TElement, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        lock (Lock)
        {
            // A unique job name can only lead to one entry in the queue
            var elementToRemove = this.FirstOrDefault(predicate);
            if (elementToRemove is null)
                return;

#if NET9_0_OR_GREATER
            PriorityQueue.Remove(elementToRemove, out _, out _);
#else
            var allElementsExceptDeleted = PriorityQueue.UnorderedItems.Where(e => !ReferenceEquals(e.Element, elementToRemove)).ToList();
            PriorityQueue.Clear();
            PriorityQueue.EnqueueRange(allElementsExceptDeleted);
#endif

            InformCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, elementToRemove));
        }
    }
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

    public TElement Dequeue()
    {
        TElement element;

        lock (Lock)
        {
            if (PriorityQueue.Count == 0)
                throw new InvalidOperationException("Queue is empty");

            element = PriorityQueue.Dequeue();
        }

        InformCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, element));

        return element;
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

    protected void InformCollectionChanged(NotifyCollectionChangedEventArgs args)
    {
        var handler = Interlocked.CompareExchange(ref CollectionChanged, null, null);
        handler?.Invoke(this, args);
    }
}
