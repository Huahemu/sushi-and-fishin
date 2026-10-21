namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

public abstract class NPCSocketAction : ScriptableObject
{
	[Header("Action Settings")]
	[SerializeField]
	protected string animationTrigger;

	[SerializeField]
	protected string actionDoneTrigger;

	[SerializeField]
	protected float minDuration = 5f;

	[SerializeField]
	protected float maxDuration = 15f;

	[SerializeField]
	protected float interestChance = 0.3f;

	public float MinDuration => minDuration;

	public abstract void OnEnter(NPC npc, NPCSocket socket);

	public abstract void OnUpdate(NPC npc, NPCSocket socket);

	public abstract void OnExit(NPC npc, NPCSocket socket);

	public float GetRandomDuration()
	{
		return Random.Range(minDuration, maxDuration);
	}

	protected bool ShouldInterested()
	{
		return Random.value < interestChance;
	}
}

}
