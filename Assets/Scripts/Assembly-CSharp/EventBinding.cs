using System;

public class EventBinding<T> : IEventBinding<T> where T : IEvent
{
	private Action<T> onEvent = (T _) =>
	{
	};

	private Action onEventNoArgs = () =>
	{
	};

	Action<T> IEventBinding<T>.OnEvent
	{
		get
		{
			return onEvent;
		}
		set
		{
			onEvent = value;
		}
	}

	Action IEventBinding<T>.OnEventNoArgs
	{
		get
		{
			return onEventNoArgs;
		}
		set
		{
			onEventNoArgs = value;
		}
	}

	public EventBinding(Action<T> onEvent)
	{
		this.onEvent = onEvent;
	}

	public EventBinding(Action onEventNoArgs)
	{
		this.onEventNoArgs = onEventNoArgs;
	}

	public void Add(Action onEvent)
	{
		onEventNoArgs = (Action)Delegate.Combine(onEventNoArgs, onEvent);
	}

	public void Remove(Action onEvent)
	{
		onEventNoArgs = (Action)Delegate.Remove(onEventNoArgs, onEvent);
	}

	public void Add(Action<T> onEvent)
	{
		this.onEvent = (Action<T>)Delegate.Combine(this.onEvent, onEvent);
	}

	public void Remove(Action<T> onEvent)
	{
		this.onEvent = (Action<T>)Delegate.Remove(this.onEvent, onEvent);
	}
}
