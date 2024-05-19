namespace NCronJob.Dashboard.Data
{
    public class EventAggregator
    {
        private readonly Dictionary<Type, List<object>> subscribers = new();

        public void Subscribe<TEventData>(Action<TEventData> handler)
        {
            if (!subscribers.ContainsKey(typeof(TEventData)))
            {
                subscribers[typeof(TEventData)] = new List<object>();
            }

            subscribers[typeof(TEventData)].Add(handler);
        }

        public void Unsubscribe<TEventData>(Action<TEventData> handler)
        {
            if (subscribers.ContainsKey(typeof(TEventData)))
            {
                subscribers[typeof(TEventData)].Remove(handler);
            }
        }

        public void Publish<TEventData>(TEventData eventData)
        {
            if (subscribers.ContainsKey(typeof(TEventData)))
            {
                foreach (var subscriber in subscribers[typeof(TEventData)].OfType<Action<TEventData>>())
                {
                    subscriber.Invoke(eventData);
                }
            }
        }
    }

}
