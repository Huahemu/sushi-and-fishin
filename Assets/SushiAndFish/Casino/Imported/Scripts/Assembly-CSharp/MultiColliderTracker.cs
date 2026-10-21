namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using global::Mirror;
using global::UnityEngine;

public class MultiColliderTracker<T> : NetworkBehaviour where T : Component
{
	private readonly Dictionary<T, int> _insideCounts = new Dictionary<T, int>();

	public IReadOnlyCollection<T> InsideObjects => _insideCounts.Keys;

	protected virtual void OnTriggerEnter(Collider other)
	{
		if ((bool)other.attachedRigidbody && other.attachedRigidbody.TryGetComponent<T>(out var component))
		{
			if (!_insideCounts.ContainsKey(component))
			{
				_insideCounts[component] = 0;
				OnObjectEntered(component);
			}
			_insideCounts[component]++;
		}
	}

	protected virtual void OnTriggerExit(Collider other)
	{
		if ((bool)other.attachedRigidbody && other.attachedRigidbody.TryGetComponent<T>(out var component) && _insideCounts.ContainsKey(component))
		{
			_insideCounts[component]--;
			if (_insideCounts[component] <= 0)
			{
				_insideCounts.Remove(component);
				OnObjectExited(component);
			}
		}
	}

	protected virtual void OnObjectEntered(T other)
	{
	}

	protected virtual void OnObjectExited(T other)
	{
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
