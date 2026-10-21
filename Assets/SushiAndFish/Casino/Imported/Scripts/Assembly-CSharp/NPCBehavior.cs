namespace SushiAndFish.Casino.Original
{
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;

public abstract class NPCBehavior : ScriptableObject
{
	[Header("General Settings")]
	[SerializeField]
	protected float minIdleTime = 2f;

	[SerializeField]
	protected float maxIdleTime = 6f;

	[SerializeField]
	protected float minRoamTime = 5f;

	[SerializeField]
	protected float maxRoamTime = 12f;

	[SerializeField]
	protected float roamRadius = 15f;

	[Header("Socket Settings")]
	[SerializeField]
	protected float socketInterestChance = 0.2f;

	[SerializeField]
	protected float socketSearchRadius = 30f;

	public abstract void UpdateBehavior(NPC npc, NPCBehaviorState state);

	public abstract void OnPayoutRecorded(NPC npc, PayoutRecord record, Vector3 npcPosition);

	public abstract Vector3 ChooseRoamTarget(Vector3 currentPosition, float radius);

	protected float GetRandomIdleTime()
	{
		return Random.Range(minIdleTime, maxIdleTime);
	}

	protected float GetRandomRoamTime()
	{
		return Random.Range(minRoamTime, maxRoamTime);
	}

	protected bool ShouldCheckSockets()
	{
		return Random.value < socketInterestChance;
	}

	protected NPCSocket FindAvailableSocket(Vector3 position, NPCSocketAction preferredAction = null)
	{
		if (MonoSingleton<NPCSocketManager>.Instance == null)
		{
			return null;
		}
		return MonoSingleton<NPCSocketManager>.Instance.FindAvailableSocket(position, socketSearchRadius, preferredAction);
	}
}

}
