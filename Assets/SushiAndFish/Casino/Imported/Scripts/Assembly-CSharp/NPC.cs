namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Runtime.InteropServices;
using global::DG.Tweening;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;
using global::UnityEngine.AI;

public class NPC : NetworkBehaviour
{
	public enum NPCState
	{
		Free = 0,
		Ragdoll = 1
	}

	[SyncVar(hook = "OnStateChanged")]
	private NPCState _state;

	[Header("References")]
	[SerializeField]
	private NavMeshAgent agent;

	[SerializeField]
	private Transform npcTransform;

	[SerializeField]
	private NPCEyes npcEyes;

	[Header("Behavior")]
	[SerializeField]
	private NPCBehavior behavior;

	[Header("Debug")]
	[SerializeField]
	[ReadOnly]
	private string _debugState;

	public RandomNPCSFX npcSfx;

	private Rigidbody _rb;

	private PlayerSettings _ps;

	private Coroutine _ragdollRoutine;

	public Action<NPCState, NPCState> _Mirror_SyncVarHookDelegate__state;

	public NavMeshAgent Agent => agent;

	public Transform Transform => npcTransform;

	public NPCBehavior Behavior => behavior;

	public NPCState State
	{
		get
		{
			return _state;
		}
		set
		{
			if (_ragdollRoutine != null)
			{
				StopCoroutine(_ragdollRoutine);
			}
			if (value == NPCState.Ragdoll)
			{
				_ragdollRoutine = StartCoroutine(DelayedDisableRagdoll());
			}
			if (value != _state)
			{
				Network_state = value;
				SetNPCState(value);
			}
		}
	}

	public NPCState Network_state
	{
		get
		{
			return _state;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref _state, 1uL, _Mirror_SyncVarHookDelegate__state);
		}
	}

	public void SetDebugState(string stateLabel)
	{
		_debugState = stateLabel;
	}

	[Server]
	public void SetDestination(Vector3 position)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void NPC::SetDestination(UnityEngine.Vector3)' called when server was not active");
		}
		else if (!(agent == null) && agent.isOnNavMesh)
		{
			agent.SetDestination(position);
			SetDestinationRpc(position);
		}
	}

	[ClientRpc]
	private void SetDestinationRpc(Vector3 position)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVector3(position);
		SendRPCInternal("System.Void NPC::SetDestinationRpc(UnityEngine.Vector3)", 375149186, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public void Warp(Vector3 position)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void NPC::Warp(UnityEngine.Vector3)' called when server was not active");
		}
		else if (!(agent == null))
		{
			agent.Warp(position);
			WarpRpc(position);
		}
	}

	[ClientRpc]
	private void WarpRpc(Vector3 position)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVector3(position);
		SendRPCInternal("System.Void NPC::WarpRpc(UnityEngine.Vector3)", -1260865492, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void OnStateChanged(NPCState oldState, NPCState newState)
	{
		SetNPCState(newState);
	}

	private void Awake()
	{
		if (agent == null)
		{
			agent = GetComponent<NavMeshAgent>();
		}
		if (npcTransform == null)
		{
			npcTransform = base.transform;
		}
		_rb = GetComponent<Rigidbody>();
		_ps = SushiAndFish.Casino.Original.CasinoResources.Load<PlayerSettings>("PlayerSettings");
		if (_rb != null)
		{
			_rb.freezeRotation = true;
			_rb.isKinematic = false;
		}
	}

	public override void OnStartServer()
	{
		base.OnStartServer();
		StartCoroutine(RegisterWithController());
	}

	private IEnumerator RegisterWithController()
	{
		while (NetworkSingleton<NPCController>.Instance == null)
		{
			yield return null;
		}
		NetworkSingleton<NPCController>.Instance.RegisterNPC(this);
	}

	private void OnDisable()
	{
		if (!base.isServer)
		{
			return;
		}
		NPCBehaviorState nPCBehaviorState = NetworkSingleton<NPCController>.Instance?.GetNPCState(this);
		if (nPCBehaviorState != null && nPCBehaviorState.currentState == NPCBehaviorState.State.UsingSocket && nPCBehaviorState.currentSocket != null)
		{
			if (nPCBehaviorState.currentSocket.Action != null && nPCBehaviorState.hasEnteredSocket)
			{
				nPCBehaviorState.currentSocket.Action.OnExit(this, nPCBehaviorState.currentSocket);
			}
			nPCBehaviorState.currentSocket.Release();
			nPCBehaviorState.currentSocket = null;
			nPCBehaviorState.hasEnteredSocket = false;
			nPCBehaviorState.currentState = NPCBehaviorState.State.Idle;
		}
	}

	private void OnEnable()
	{
		if (base.isServer)
		{
			StartCoroutine(ResetAnimator());
		}
	}

	private IEnumerator ResetAnimator()
	{
		if (!base.gameObject.activeSelf)
		{
			yield break;
		}
		yield return new WaitForSeconds(1f);
		Animator component = GetComponent<Animator>();
		if (component != null && component.isInitialized)
		{
			component.Play("Default", 0, 0f);
			component.Update(0f);
			NetworkAnimator component2 = GetComponent<NetworkAnimator>();
			if (component2 != null && base.isServer)
			{
				component2.SetTrigger("npc_done");
			}
		}
	}

	private void OnDestroy()
	{
		if (Application.isPlaying && NetworkSingleton<NPCController>.Instance != null)
		{
			NetworkSingleton<NPCController>.Instance.UnregisterNPC(this);
		}
	}

	public void Initialize(float walkSpeed, float stoppingDistance)
	{
		if (agent != null)
		{
			agent.speed = walkSpeed;
			agent.stoppingDistance = stoppingDistance;
			agent.acceleration = 8f;
			agent.angularSpeed = 120f;
			agent.updatePosition = true;
			agent.updateRotation = true;
			agent.updateUpAxis = true;
		}
	}

	private void SetNPCState(NPCState newState)
	{
		if (_rb == null)
		{
			return;
		}
		if (!_rb.isKinematic)
		{
			_rb.linearVelocity = Vector3.zero;
		}
		_rb.constraints = ((newState != NPCState.Ragdoll) ? RigidbodyConstraints.FreezeRotation : RigidbodyConstraints.None);
		if (!(agent != null))
		{
			return;
		}
		if (newState == NPCState.Free)
		{
			agent.enabled = false;
			_rb.DORotate(Vector3.zero, 0.5f).SetEase(Ease.OutCubic).OnComplete(() =>
			{
				if (agent != null)
				{
					agent.enabled = true;
					if (base.isServer)
					{
						Warp(base.transform.position);
					}
				}
			});
		}
		else
		{
			agent.enabled = false;
		}
	}

	private IEnumerator DelayedDisableRagdoll()
	{
		yield return new WaitForSeconds(_ps.ragdollDuration);
		if (_state == NPCState.Ragdoll)
		{
			State = NPCState.Free;
		}
	}

	[Server]
	public void ServerKnockback(Vector3 force, Vector3 torque)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void NPC::ServerKnockback(UnityEngine.Vector3,UnityEngine.Vector3)' called when server was not active");
		}
		else if (!(_rb == null))
		{
			State = NPCState.Ragdoll;
			ApplyKnockbackRpc(force, torque);
		}
	}

	[ClientRpc]
	private void ApplyKnockbackRpc(Vector3 force, Vector3 torque)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVector3(force);
		writer.WriteVector3(torque);
		SendRPCInternal("System.Void NPC::ApplyKnockbackRpc(UnityEngine.Vector3,UnityEngine.Vector3)", 1947053242, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public NPC()
	{
		_Mirror_SyncVarHookDelegate__state = OnStateChanged;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_SetDestinationRpc__Vector3(Vector3 position)
	{
		if (!base.isServer && agent != null && agent.isOnNavMesh)
		{
			agent.SetDestination(position);
		}
	}

	protected static void InvokeUserCode_SetDestinationRpc__Vector3(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC SetDestinationRpc called on server.");
		}
		else
		{
			((NPC)obj).UserCode_SetDestinationRpc__Vector3(reader.ReadVector3());
		}
	}

	protected void UserCode_WarpRpc__Vector3(Vector3 position)
	{
		if (!base.isServer && agent != null)
		{
			agent.Warp(position);
		}
	}

	protected static void InvokeUserCode_WarpRpc__Vector3(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC WarpRpc called on server.");
		}
		else
		{
			((NPC)obj).UserCode_WarpRpc__Vector3(reader.ReadVector3());
		}
	}

	protected void UserCode_ApplyKnockbackRpc__Vector3__Vector3(Vector3 force, Vector3 torque)
	{
		if (_rb != null)
		{
			_rb.AddForce(force, ForceMode.VelocityChange);
			_rb.AddTorque(torque, ForceMode.VelocityChange);
		}
	}

	protected static void InvokeUserCode_ApplyKnockbackRpc__Vector3__Vector3(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC ApplyKnockbackRpc called on server.");
		}
		else
		{
			((NPC)obj).UserCode_ApplyKnockbackRpc__Vector3__Vector3(reader.ReadVector3(), reader.ReadVector3());
		}
	}

	static NPC()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(NPC), "System.Void NPC::SetDestinationRpc(UnityEngine.Vector3)", InvokeUserCode_SetDestinationRpc__Vector3);
		RemoteProcedureCalls.RegisterRpc(typeof(NPC), "System.Void NPC::WarpRpc(UnityEngine.Vector3)", InvokeUserCode_WarpRpc__Vector3);
		RemoteProcedureCalls.RegisterRpc(typeof(NPC), "System.Void NPC::ApplyKnockbackRpc(UnityEngine.Vector3,UnityEngine.Vector3)", InvokeUserCode_ApplyKnockbackRpc__Vector3__Vector3);
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			GeneratedNetworkCode._Write_NPC_002FNPCState(writer, _state);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 1L) != 0L)
		{
			GeneratedNetworkCode._Write_NPC_002FNPCState(writer, _state);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref _state, _Mirror_SyncVarHookDelegate__state, GeneratedNetworkCode._Read_NPC_002FNPCState(reader));
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 1L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref _state, _Mirror_SyncVarHookDelegate__state, GeneratedNetworkCode._Read_NPC_002FNPCState(reader));
		}
	}
}

}
