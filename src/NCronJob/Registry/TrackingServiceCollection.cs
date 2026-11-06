using Microsoft.Extensions.DependencyInjection;

namespace NCronJob;

/// <summary>
/// Forwards to the wrapped collection and remembers what was added so it can be taken out again.
/// </summary>
internal sealed class TrackingServiceCollection(IServiceCollection services) : IServiceCollection
{
    private readonly List<ServiceDescriptor> addedDescriptors = [];

    public ServiceDescriptor this[int index]
    {
        get => services[index];
        set => services[index] = value;
    }

    public int Count => services.Count;

    public bool IsReadOnly => services.IsReadOnly;

    public void Add(ServiceDescriptor item)
    {
        services.Add(item);
        addedDescriptors.Add(item);
    }

    public void Clear() => services.Clear();

    public bool Contains(ServiceDescriptor item) => services.Contains(item);

    public void CopyTo(ServiceDescriptor[] array, int arrayIndex) => services.CopyTo(array, arrayIndex);

    public IEnumerator<ServiceDescriptor> GetEnumerator() => services.GetEnumerator();

    public int IndexOf(ServiceDescriptor item) => services.IndexOf(item);

    public void Insert(int index, ServiceDescriptor item)
    {
        services.Insert(index, item);
        addedDescriptors.Add(item);
    }

    public bool Remove(ServiceDescriptor item) => services.Remove(item);

    public void RemoveAt(int index) => services.RemoveAt(index);

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public void Rollback()
    {
        foreach (var descriptor in addedDescriptors.AsEnumerable().Reverse())
        {
            for (var index = services.Count - 1; index >= 0; index--)
            {
                if (!ReferenceEquals(services[index], descriptor))
                {
                    continue;
                }

                services.RemoveAt(index);
                break;
            }
        }
    }
}
