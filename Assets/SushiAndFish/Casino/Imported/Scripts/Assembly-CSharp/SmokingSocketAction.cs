namespace SushiAndFish.Casino.Original
{
using global::Mirror;
using global::UnityEngine;
using global::UnityEngine.AI;

[CreateAssetMenu(menuName = "NPC Socket Actions/Smoking Action")]
public class SmokingSocketAction : NPCSocketAction
{
	public override void OnEnter(NPC npc, NPCSocket socket)
	{
		NavMeshAgent agent = npc.Agent;
		if (agent != null)
		{
			agent.enabled = false;
		}
		Rigidbody component = npc.GetComponent<Rigidbody>();
		if (component != null)
		{
			component.isKinematic = true;
		}
		Quaternion rotation = Quaternion.LookRotation(socket.Forward);
		npc.Transform.rotation = rotation;
		if (string.IsNullOrEmpty(animationTrigger) || !npc.isServer)
		{
			return;
		}
		NetworkAnimator component2 = npc.GetComponent<NetworkAnimator>();
		if (component2 != null)
		{
			component2.SetTrigger(animationTrigger);
			return;
		}
		Animator component3 = npc.GetComponent<Animator>();
		if (component3 != null)
		{
			component3.SetTrigger(animationTrigger);
		}
	}

	public override void OnUpdate(NPC npc, NPCSocket socket)
	{
		Quaternion b = Quaternion.LookRotation(socket.Forward);
		npc.Transform.rotation = Quaternion.Slerp(npc.Transform.rotation, b, Time.deltaTime * 0.5f);
	}

	public override void OnExit(NPC npc, NPCSocket socket)
	{
		Rigidbody component = npc.GetComponent<Rigidbody>();
		if (component != null)
		{
			component.isKinematic = false;
		}
		NavMeshAgent agent = npc.Agent;
		if (agent != null)
		{
			agent.enabled = true;
			if (npc.isServer)
			{
				npc.Warp(npc.Transform.position);
			}
		}
		if (string.IsNullOrEmpty(actionDoneTrigger) || !npc.isServer)
		{
			return;
		}
		NetworkAnimator component2 = npc.GetComponent<NetworkAnimator>();
		if (component2 != null)
		{
			component2.SetTrigger(actionDoneTrigger);
			return;
		}
		Animator component3 = npc.GetComponent<Animator>();
		if (component3 != null)
		{
			component3.SetTrigger(actionDoneTrigger);
		}
	}
}

}
