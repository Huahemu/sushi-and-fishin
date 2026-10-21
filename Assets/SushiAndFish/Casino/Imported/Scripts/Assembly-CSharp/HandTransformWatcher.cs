namespace SushiAndFish.Casino.Original
{
using global::System;
using global::Mirror;
using global::UnityEngine;

public class HandTransformWatcher : NetworkBehaviour
{
	[Header("Event Callbacks")]
	public Action OnChildrenChanged;

	private void OnTransformChildrenChanged()
	{
		OnChildrenChanged?.Invoke();
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
