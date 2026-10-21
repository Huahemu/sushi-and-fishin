namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;
using global::UnityEngine.AI;

[CreateAssetMenu(menuName = "NPC Behaviors/Curious Behavior")]
public class CuriousBehavior : NPCBehavior
{
	[Header("Curious Settings")]
	[SerializeField]
	[Range(0f, 1f)]
	private float payoutThreshold = 0.1f;

	[SerializeField]
	private float watchRadius = 25f;

	[SerializeField]
	private float watchDuration = 15f;

	[SerializeField]
	private float approachDistance = 3f;

	public override void UpdateBehavior(NPC npc, NPCBehaviorState state)
	{
		NavMeshAgent agent = npc.Agent;
		if (state.currentState == NPCBehaviorState.State.UsingSocket)
		{
			UpdateSocketUsage(npc, state);
		}
		else
		{
			if (agent == null || !agent.isOnNavMesh)
			{
				return;
			}
			switch (state.currentState)
			{
			case NPCBehaviorState.State.Idle:
				agent.isStopped = true;
				if (ShouldCheckSockets())
				{
					NPCSocket nPCSocket = FindAvailableSocket(npc.Transform.position);
					if (nPCSocket != null && !IsSocketOnCooldown(state, nPCSocket))
					{
						StartUsingSocket(npc, state, nPCSocket);
						break;
					}
				}
				if (Time.time >= state.stateUntil)
				{
					state.currentState = NPCBehaviorState.State.Roaming;
					state.stateUntil = Time.time + GetRandomRoamTime();
					StartRoaming(npc, state);
				}
				break;
			case NPCBehaviorState.State.Roaming:
				agent.isStopped = false;
				if (!agent.pathPending && (agent.remainingDistance <= agent.stoppingDistance + 0.1f || Time.time >= state.stateUntil))
				{
					state.currentState = NPCBehaviorState.State.Idle;
					state.stateUntil = Time.time + GetRandomIdleTime();
					agent.isStopped = true;
				}
				break;
			case NPCBehaviorState.State.Watching:
				agent.isStopped = false;
				if (agent.pathPending)
				{
					break;
				}
				if (agent.remainingDistance <= approachDistance)
				{
					agent.isStopped = true;
					Vector3 normalized = (state.watchPosition - npc.Transform.position).normalized;
					if (normalized != Vector3.zero)
					{
						Quaternion b = Quaternion.LookRotation(normalized);
						npc.Transform.rotation = Quaternion.Slerp(npc.Transform.rotation, b, Time.deltaTime * 2f);
					}
				}
				if (Time.time >= state.stateUntil)
				{
					state.currentState = NPCBehaviorState.State.Idle;
					state.stateUntil = Time.time + GetRandomIdleTime();
				}
				break;
			}
		}
	}

	private void StartUsingSocket(NPC npc, NPCBehaviorState state, NPCSocket socket)
	{
		state.currentState = NPCBehaviorState.State.UsingSocket;
		state.currentSocket = socket;
		state.hasEnteredSocket = false;
		state.socketStartTime = 0f;
		state.socketAttemptStartTime = Time.time;
		state.stateUntil = 0f;
		socket.Reserve();
		NavMeshAgent agent = npc.Agent;
		if (agent != null && agent.isOnNavMesh && NavMesh.SamplePosition(socket.Position, out var hit, 2f, -1))
		{
			npc.SetDestination(hit.position);
		}
	}

	private void UpdateSocketUsage(NPC npc, NPCBehaviorState state)
	{
		NavMeshAgent agent = npc.Agent;
		if (state.currentSocket == null)
		{
			state.currentState = NPCBehaviorState.State.Idle;
			state.stateUntil = Time.time + GetRandomIdleTime();
			return;
		}
		if (Vector3.Distance(npc.Transform.position, state.currentSocket.Position) <= state.currentSocket.UseRadius)
		{
			if (!state.hasEnteredSocket)
			{
				state.hasEnteredSocket = true;
				state.socketStartTime = Time.time;
				float num = ((state.currentSocket.Action != null) ? state.currentSocket.Action.GetRandomDuration() : 10f);
				state.stateUntil = Time.time + num;
				if (state.currentSocket.Action != null)
				{
					state.currentSocket.Action.OnEnter(npc, state.currentSocket);
				}
			}
			if (state.currentSocket.Action != null)
			{
				state.currentSocket.Action.OnUpdate(npc, state.currentSocket);
			}
		}
		else
		{
			if (agent != null && agent.enabled)
			{
				agent.isStopped = false;
			}
			if (!state.hasEnteredSocket && (Time.time - state.socketAttemptStartTime > 15f || (agent != null && agent.enabled && !agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathInvalid)))
			{
				state.currentSocket.Release();
				state.currentSocket = null;
				state.currentState = NPCBehaviorState.State.Idle;
				state.stateUntil = Time.time + GetRandomIdleTime();
				return;
			}
		}
		if (state.hasEnteredSocket && Time.time >= state.stateUntil)
		{
			if (state.currentSocket.Action != null)
			{
				state.currentSocket.Action.OnExit(npc, state.currentSocket);
			}
			NPCSocket currentSocket = state.currentSocket;
			state.currentSocket.Release();
			state.currentSocket = null;
			state.hasEnteredSocket = false;
			state.socketStartTime = 0f;
			if (currentSocket != null)
			{
				state.socketCooldowns[currentSocket] = Time.time + 30f;
			}
			state.currentState = NPCBehaviorState.State.Idle;
			state.stateUntil = Time.time + GetRandomIdleTime();
		}
	}

	public override void OnPayoutRecorded(NPC npc, PayoutRecord record, Vector3 npcPosition)
	{
		if (NetworkSingleton<GameManager>.Instance == null)
		{
			return;
		}
		long num = (long)((float)NetworkSingleton<GameManager>.Instance.currentQuota * payoutThreshold);
		if (record.payout < num || Vector3.Distance(npcPosition, record.gamePosition) > watchRadius)
		{
			return;
		}
		NPCBehaviorState nPCBehaviorState = NetworkSingleton<NPCController>.Instance?.GetNPCState(npc);
		if (nPCBehaviorState == null)
		{
			return;
		}
		if (nPCBehaviorState.currentState == NPCBehaviorState.State.UsingSocket && nPCBehaviorState.currentSocket != null)
		{
			float num2 = Time.time - nPCBehaviorState.socketStartTime;
			float num3 = ((nPCBehaviorState.currentSocket.Action != null) ? nPCBehaviorState.currentSocket.Action.MinDuration : 5f);
			if (num2 < num3)
			{
				return;
			}
		}
		nPCBehaviorState.currentState = NPCBehaviorState.State.Watching;
		nPCBehaviorState.watchPosition = record.gamePosition;
		nPCBehaviorState.stateUntil = Time.time + watchDuration;
		NavMeshAgent agent = npc.Agent;
		if (agent != null && agent.isOnNavMesh)
		{
			Vector3 vector = Random.insideUnitCircle * 2f;
			if (NavMesh.SamplePosition(record.gamePosition + new Vector3(vector.x, 0f, vector.y), out var hit, 5f, -1))
			{
				npc.SetDestination(hit.position);
			}
		}
	}

	public override Vector3 ChooseRoamTarget(Vector3 currentPosition, float radius)
	{
		Vector2 vector = Random.insideUnitCircle * radius;
		if (NavMesh.SamplePosition(currentPosition + new Vector3(vector.x, 0f, vector.y), out var hit, radius, -1))
		{
			return hit.position;
		}
		return currentPosition;
	}

	private void StartRoaming(NPC npc, NPCBehaviorState state)
	{
		NavMeshAgent agent = npc.Agent;
		if (agent != null && agent.isOnNavMesh && NavMesh.SamplePosition(state.targetPosition = ChooseRoamTarget(npc.Transform.position, roamRadius), out var hit, 2f, -1))
		{
			npc.SetDestination(hit.position);
		}
	}

	private bool IsSocketOnCooldown(NPCBehaviorState state, NPCSocket socket)
	{
		CleanExpiredCooldowns(state);
		if (state.socketCooldowns.ContainsKey(socket))
		{
			return Time.time < state.socketCooldowns[socket];
		}
		return false;
	}

	private void CleanExpiredCooldowns(NPCBehaviorState state)
	{
		List<NPCSocket> list = new List<NPCSocket>();
		foreach (KeyValuePair<NPCSocket, float> socketCooldown in state.socketCooldowns)
		{
			if (Time.time >= socketCooldown.Value)
			{
				list.Add(socketCooldown.Key);
			}
		}
		foreach (NPCSocket item in list)
		{
			state.socketCooldowns.Remove(item);
		}
	}
}

}
