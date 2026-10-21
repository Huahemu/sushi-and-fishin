namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::Gilzoide.UpdateManager;
using global::Mirror;
using global::UnityEngine;

public class NPCController : NetworkSingleton<NPCController>, IUpdatable, IManagedObject
{
	[Header("NPC Settings")]
	[SerializeField]
	private float walkSpeed = 3.5f;

	[SerializeField]
	private float stoppingDistance = 0.5f;

	private List<NPC> allNPCs = new List<NPC>();

	private Dictionary<NPC, NPCBehaviorState> npcStates = new Dictionary<NPC, NPCBehaviorState>();

	protected override void OnAwake()
	{
		base.OnAwake();
	}

	private void OnEnable()
	{
		this.RegisterInManager();
		if (NetworkSingleton<PayoutTracker>.Instance != null)
		{
			NetworkSingleton<PayoutTracker>.Instance.OnPayoutRecorded += OnPayoutRecorded;
		}
	}

	private void OnDisable()
	{
		this.UnregisterInManager();
		if (NetworkSingleton<PayoutTracker>.Instance != null)
		{
			NetworkSingleton<PayoutTracker>.Instance.OnPayoutRecorded -= OnPayoutRecorded;
		}
	}

	public void ManagedUpdate()
	{
		if (base.isServer)
		{
			UpdateNPCs();
		}
	}

	[Server]
	public void RegisterNPC(NPC npc)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void NPCController::RegisterNPC(NPC)' called when server was not active");
		}
		else if (!(npc == null) && !allNPCs.Contains(npc))
		{
			allNPCs.Add(npc);
			npc.Initialize(walkSpeed, stoppingDistance);
			NPCBehaviorState nPCBehaviorState = new NPCBehaviorState();
			nPCBehaviorState.currentState = NPCBehaviorState.State.Idle;
			nPCBehaviorState.stateUntil = Time.time + 2f;
			npcStates[npc] = nPCBehaviorState;
		}
	}

	[Server]
	public void UnregisterNPC(NPC npc)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void NPCController::UnregisterNPC(NPC)' called when server was not active");
		}
		else if (!(npc == null))
		{
			allNPCs.Remove(npc);
			npcStates.Remove(npc);
		}
	}

	public NPCBehaviorState GetNPCState(NPC npc)
	{
		if (!npcStates.ContainsKey(npc))
		{
			return null;
		}
		return npcStates[npc];
	}

	private void UpdateNPCs()
	{
		foreach (NPC allNPC in allNPCs)
		{
			if (!(allNPC == null) && npcStates.ContainsKey(allNPC) && allNPC.State != NPC.NPCState.Ragdoll && allNPC.gameObject.activeSelf)
			{
				NPCBehaviorState nPCBehaviorState = npcStates[allNPC];
				if (allNPC.Behavior != null)
				{
					allNPC.Behavior.UpdateBehavior(allNPC, nPCBehaviorState);
				}
				allNPC.SetDebugState(nPCBehaviorState.currentState.ToString());
				if (allNPC.npcSfx != null)
				{
					allNPC.npcSfx.ManagedUpdate();
				}
			}
		}
	}

	[Server]
	private void OnPayoutRecorded(PayoutRecord record)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void NPCController::OnPayoutRecorded(PayoutRecord)' called when server was not active");
		}
		else
		{
			if (!base.isServer)
			{
				return;
			}
			foreach (NPC allNPC in allNPCs)
			{
				if (!(allNPC == null) && npcStates.ContainsKey(allNPC) && !(allNPC.Behavior == null))
				{
					allNPC.Behavior.OnPayoutRecorded(allNPC, record, allNPC.Transform.position);
				}
			}
		}
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
