using System.Collections.Generic;

public static class EventBus<T> where T : IEvent
{
	private static readonly HashSet<IEventBinding<T>> bindings = new HashSet<IEventBinding<T>>();

	public static void Register(EventBinding<T> binding)
	{
		bindings.Add(binding);
	}

	public static void Deregister(EventBinding<T> binding)
	{
		bindings.Remove(binding);
	}

	public static void Raise(T @event)
	{
		foreach (IEventBinding<T> item in new HashSet<IEventBinding<T>>(bindings))
		{
			if (bindings.Contains(item))
			{
				item.OnEvent(@event);
				item.OnEventNoArgs();
			}
		}
	}

	private static void Clear()
	{
		bindings.Clear();
	}
}
