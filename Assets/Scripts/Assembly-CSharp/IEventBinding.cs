using System;

public interface IEventBinding<T>
{
	Action<T> OnEvent { get; set; }

	Action OnEventNoArgs { get; set; }
}
