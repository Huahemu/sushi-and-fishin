namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Runtime.InteropServices;
using global::DG.Tweening;
using SushiAndFish.Casino.Original.Extensions;
using global::JetBrains.Annotations;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::MoreMountains.Feedbacks;
using SushiAndFish.Casino.Original.Smooth;
using global::UnityEngine;

public class Item : InteractableBase
{
	[Header("References")]
	public SpawnableSO spawnableSo;

	public Transform modelTransform;

	public GameObject handRig;

	public SkinnedMeshRenderer handMesh;

	public MMF_Player onHandFb;

	public MMF_Player onDropFb;

	public ParticleSystem onThrowVfx;

	[SyncVar(hook = "OnHolderChanged")]
	[CanBeNull]
	protected PlayerInventory Holder;

	[HideInInspector]
	public bool isInPocket;

	[Header("Item Actions")]
	public List<ItemAction> itemActions;

	protected Rigidbody Rb;

	private List<Collider> _allColliders = new List<Collider>();

	private NetworkRigidbodyUnreliable _nrb;

	private SmoothSyncMirror _ssm;

	private SFXPhysicsObject _sfxPhysicsObject;

	private LobbySettings _ls;

	[Range(0f, 1f)]
	public float slowPercent;

	protected NetworkBehaviourSyncVar ___HolderNetId;

	public Action<PlayerInventory, PlayerInventory> _Mirror_SyncVarHookDelegate_Holder;

	[Header("Settings")]
	public virtual bool ShouldShowHoverDescription => true;

	public float Mass { get; private set; }

	public PlayerInventory NetworkHolder
	{
		get
		{
			return GetSyncVarNetworkBehaviour(___HolderNetId, ref Holder);
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter_NetworkBehaviour(value, ref Holder, 1uL, _Mirror_SyncVarHookDelegate_Holder, ref ___HolderNetId);
		}
	}

	protected override void OnAwake()
	{
		Rb = GetComponent<Rigidbody>();
		Mass = Rb.mass;
		_nrb = GetComponent<NetworkRigidbodyUnreliable>();
		_ssm = GetComponent<SmoothSyncMirror>();
		_sfxPhysicsObject = GetComponent<SFXPhysicsObject>();
		_ls = SushiAndFish.Casino.Original.CasinoResources.Load<LobbySettings>("LobbySettings");
		if (!modelTransform)
		{
			modelTransform = base.transform.Find("Model");
		}
		if ((bool)modelTransform && !handRig)
		{
			handRig = modelTransform.Find("HandRig").gameObject;
		}
		if ((bool)handRig && !handMesh)
		{
			handMesh = handRig.GetComponentInChildren<SkinnedMeshRenderer>();
		}
		if (!onHandFb)
		{
			onHandFb = base.transform.Find("Feedbacks")?.Find("OnHand")?.GetComponent<MMF_Player>();
		}
		if (!onDropFb)
		{
			onDropFb = base.transform.Find("Feedbacks")?.Find("OnDrop")?.GetComponent<MMF_Player>();
		}
		if (!onThrowVfx)
		{
			onThrowVfx = base.transform.Find("Feedbacks")?.Find("OnThrowVFX")?.GetComponent<ParticleSystem>();
		}
		Collider[] componentsInChildren = GetComponentsInChildren<Collider>(includeInactive: true);
		foreach (Collider collider in componentsInChildren)
		{
			if (!collider.isTrigger)
			{
				_allColliders.Add(collider);
			}
		}
	}

	protected void SetEnableColliders(bool isEnabled)
	{
		foreach (Collider allCollider in _allColliders)
		{
			allCollider.enabled = isEnabled;
		}
	}

	protected virtual void OnEnable()
	{
		SubscribeToEvents(isSubscribed: true);
		modelTransform.DOKill();
		if (!(this is PlayerCarry))
		{
			modelTransform.DOScale(Vector3.one, 1f).From(0f).SetEase(Ease.OutElastic, 0f, 0f)
				.SetUpdate(isIndependentUpdate: true);
		}
	}

	protected virtual void OnDisable()
	{
		SubscribeToEvents(isSubscribed: false);
	}

	protected virtual void SubscribeToEvents(bool isSubscribed)
	{
		if (isSubscribed)
		{
			InputEvents.OnUseItemEvent = (Action<bool>)Delegate.Combine(InputEvents.OnUseItemEvent, new Action<bool>(TryUseItem));
		}
		else
		{
			InputEvents.OnUseItemEvent = (Action<bool>)Delegate.Remove(InputEvents.OnUseItemEvent, new Action<bool>(TryUseItem));
		}
	}

	public override void OnStartServer()
	{
		base.OnStartServer();
		NetworkSingleton<PlayerSpawnManager>.Instance.OnPlayerLateJoined += ServerOnPlayerLateJoined;
	}

	public override void OnStopServer()
	{
		base.OnStopServer();
		NetworkSingleton<PlayerSpawnManager>.Instance.OnPlayerLateJoined -= ServerOnPlayerLateJoined;
	}

	[Server]
	private void ServerOnPlayerLateJoined()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Item::ServerOnPlayerLateJoined()' called when server was not active");
		}
		else if ((bool)NetworkHolder)
		{
			SetPickedUp(NetworkHolder);
			RpcPickup(NetworkHolder);
			OnPickedUp(NetworkHolder);
			ServerHandEnter(NetworkHolder);
		}
	}

	public override void OnStartClient()
	{
		base.OnStartClient();
		if (base.isOwned || (!base.isOwned && base.isServer && base.connectionToClient == null))
		{
			Rb.interpolation = RigidbodyInterpolation.Interpolate;
		}
		else
		{
			Rb.interpolation = RigidbodyInterpolation.None;
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		modelTransform.DOKill();
	}

	protected virtual void OnHolderChanged(PlayerInventory oldHolder, PlayerInventory newHolder)
	{
	}

	private void TryUseItem(bool isPressed)
	{
		if ((bool)NetworkHolder && NetworkHolder.isLocalPlayer && !isInPocket)
		{
			OnUseItem(isPressed);
			CmdUseItem(isPressed);
		}
	}

	public override void OnInteract(PlayerInteract playerInteract)
	{
		base.OnInteract(playerInteract);
		if (!NetworkHolder && playerInteract.TryGetComponent<PlayerInventory>(out var _))
		{
			OnLocalPickUp();
		}
	}

	[Server]
	public override void ServerOnInteract(PlayerInteract playerInteract)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Item::ServerOnInteract(PlayerInteract)' called when server was not active");
			return;
		}
		base.ServerOnInteract(playerInteract);
		if (!NetworkHolder && playerInteract.TryGetComponent<PlayerInventory>(out var component))
		{
			ServerPickup(component);
		}
	}

	[Server]
	public virtual void ServerThrow(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Item::ServerThrow(UnityEngine.Vector3,UnityEngine.Quaternion,UnityEngine.Vector3,UnityEngine.Vector3)' called when server was not active");
			return;
		}
		if ((bool)NetworkHolder)
		{
			ServerDrop();
		}
		Rb.Teleport(position);
		Rb.Rotate(rotation);
		Rb.linearVelocity = velocity;
		Rb.angularVelocity = angularVelocity;
		StartCoroutine(DelayFixedUpdate(velocity));
	}

	private IEnumerator DelayFixedUpdate(Vector3 velocity)
	{
		yield return new WaitForFixedUpdate();
		RpcOnThrow(velocity);
	}

	[ClientRpc]
	private void RpcOnThrow(Vector3 velocity)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVector3(velocity);
		SendRPCInternal("System.Void Item::RpcOnThrow(UnityEngine.Vector3)", -368834569, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public virtual void ServerTeleport(Vector3 position)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Item::ServerTeleport(UnityEngine.Vector3)' called when server was not active");
		}
		else if (!NetworkHolder)
		{
			Rb.Teleport(position, resetVelocity: true);
		}
	}

	[Server]
	public virtual void ServerRotate(Quaternion rotation)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Item::ServerRotate(UnityEngine.Quaternion)' called when server was not active");
		}
		else if (!NetworkHolder)
		{
			Rb.Rotate(rotation, resetVelocity: true);
		}
	}

	[Server]
	public void ServerSetEnabled(bool isEnabled)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Item::ServerSetEnabled(System.Boolean)' called when server was not active");
			return;
		}
		if (!isEnabled)
		{
			ServerDrop();
		}
		RpcSetEnabled(isEnabled);
	}

	[ClientRpc]
	private void RpcSetEnabled(bool isEnabled)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isEnabled);
		SendRPCInternal("System.Void Item::RpcSetEnabled(System.Boolean)", 1610715325, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public bool GetIsBeingHeld()
	{
		return NetworkHolder;
	}

	[Server]
	private void ServerPickup(PlayerInventory playerInventory)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Item::ServerPickup(PlayerInventory)' called when server was not active");
		}
		else if (!NetworkHolder)
		{
			NetworkHolder = playerInventory;
			playerInventory?.ServerAddItem(this);
			SetPickedUp(playerInventory);
			RpcPickup(playerInventory);
			OnPickedUp(playerInventory);
			ServerHandEnter(playerInventory);
		}
	}

	[ClientRpc]
	private void RpcPickup(PlayerInventory playerInventory)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(playerInventory);
		SendRPCInternal("System.Void Item::RpcPickup(PlayerInventory)", 1550294340, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void SetPickedUp(PlayerInventory playerInventory)
	{
		Rb.interpolation = RigidbodyInterpolation.None;
		Rb.isKinematic = true;
		if ((bool)_nrb)
		{
			_nrb.enabled = false;
		}
		if ((bool)_ssm)
		{
			_ssm.enabled = false;
		}
		SetEnableColliders(isEnabled: false);
		IsInteractable = false;
		base.transform.SetParent(playerInventory.handTransform);
		Color playerColor = _ls.GetPlayerBySteamId(playerInventory.GetComponent<PlayerProfile>().steamId).playerColor;
		handMesh.material.SetColor("_Color", playerColor);
	}

	private void OnLocalPickUp()
	{
		modelTransform.gameObject.SetActive(value: false);
	}

	[Server]
	public void ServerDrop()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Item::ServerDrop()' called when server was not active");
		}
		else if ((bool)NetworkHolder)
		{
			PlayerInventory networkHolder = NetworkHolder;
			NetworkHolder = null;
			networkHolder.ServerRemoveItem(this);
			SetDropped(networkHolder);
			RpcDrop(networkHolder);
			OnDropped(networkHolder);
		}
	}

	[ClientRpc]
	private void RpcDrop(PlayerInventory previousHolder)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(previousHolder);
		SendRPCInternal("System.Void Item::RpcDrop(PlayerInventory)", 251131031, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void SetDropped(PlayerInventory previousHolder)
	{
		isInPocket = false;
		base.transform.SetParent(null);
		Rb.isKinematic = false;
		if (base.isOwned || (!base.isOwned && base.isServer && base.connectionToClient == null))
		{
			Rb.interpolation = RigidbodyInterpolation.Interpolate;
		}
		StartCoroutine(MassTween());
		if ((bool)_nrb)
		{
			_nrb.enabled = true;
		}
		if ((bool)_ssm)
		{
			_ssm.enabled = true;
		}
		if (_sfxPhysicsObject != null)
		{
			_sfxPhysicsObject.SetPlayerThrowCooldown();
		}
		SetEnableColliders(isEnabled: true);
		IsInteractable = true;
		modelTransform.localPosition = Vector3.zero;
		modelTransform.localRotation = Quaternion.identity;
		modelTransform.gameObject.SetActive(value: true);
		handRig.SetActive(value: false);
		previousHolder.SetPlayerHandsVisible(visible: true);
		if ((bool)onDropFb)
		{
			onDropFb.PlayFeedbacks();
		}
	}

	private IEnumerator MassTween()
	{
		float duration = 0.1f;
		float t = 0f;
		Rb.mass = Mass / 100f;
		for (; t < duration; t += Time.fixedDeltaTime)
		{
			float mass = Mathf.Lerp(Mass / 100f, Mass, t / duration);
			Rb.mass = mass;
			yield return new WaitForFixedUpdate();
		}
		Rb.mass = Mass;
	}

	public void OnLocalDrop()
	{
		if ((bool)modelTransform)
		{
			modelTransform.gameObject.SetActive(value: false);
		}
	}

	[Server]
	public void ServerHandEnter(PlayerInventory playerInventory)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Item::ServerHandEnter(PlayerInventory)' called when server was not active");
		}
		else if ((bool)NetworkHolder && !(NetworkHolder != playerInventory))
		{
			SetHandEnter(playerInventory);
			RpcHandEnter(playerInventory);
			OnHandEnter(playerInventory);
		}
	}

	[ClientRpc]
	private void RpcHandEnter(PlayerInventory playerInventory)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(playerInventory);
		SendRPCInternal("System.Void Item::RpcHandEnter(PlayerInventory)", 275819659, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void SetHandEnter(PlayerInventory playerInventory)
	{
		isInPocket = false;
		base.transform.localPosition = Vector3.zero;
		base.transform.localRotation = Quaternion.identity;
		modelTransform.localPosition = Vector3.zero;
		modelTransform.localRotation = Quaternion.identity;
		modelTransform.gameObject.SetActive(value: true);
		playerInventory.SetPlayerHandsVisible(visible: false);
		handRig.SetActive(value: true);
		if ((bool)onHandFb)
		{
			onHandFb.PlayFeedbacks();
		}
	}

	[Server]
	public void ServerHandExit(PlayerInventory playerInventory)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Item::ServerHandExit(PlayerInventory)' called when server was not active");
		}
		else if ((bool)NetworkHolder && !(NetworkHolder != playerInventory))
		{
			SetHandExit(playerInventory);
			RpcHandExit(playerInventory);
			OnHandExit(playerInventory);
		}
	}

	[ClientRpc]
	private void RpcHandExit(PlayerInventory playerInventory)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(playerInventory);
		SendRPCInternal("System.Void Item::RpcHandExit(PlayerInventory)", -558030313, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void SetHandExit(PlayerInventory playerInventory)
	{
		isInPocket = true;
		modelTransform.gameObject.SetActive(value: false);
		base.transform.SetParent(playerInventory.pocketTransform);
		handRig.SetActive(value: false);
		playerInventory.SetPlayerHandsVisible(visible: true);
	}

	protected virtual void OnPickedUp(PlayerInventory playerInventory)
	{
	}

	[Server]
	protected virtual void OnDropped(PlayerInventory playerInventory)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Item::OnDropped(PlayerInventory)' called when server was not active");
		}
	}

	[Server]
	protected virtual void OnHandEnter(PlayerInventory holder)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Item::OnHandEnter(PlayerInventory)' called when server was not active");
		}
	}

	[Server]
	protected virtual void OnHandExit(PlayerInventory holder)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Item::OnHandExit(PlayerInventory)' called when server was not active");
		}
	}

	[Command(requiresAuthority = false)]
	private void CmdUseItem(bool isPressed)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isPressed);
		SendCommandInternal("System.Void Item::CmdUseItem(System.Boolean)", 738706521, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcUseItem(bool isPressed)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isPressed);
		SendRPCInternal("System.Void Item::RpcUseItem(System.Boolean)", -234203220, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	protected virtual void OnUseItem(bool isPressed)
	{
	}

	public Item()
	{
		_Mirror_SyncVarHookDelegate_Holder = OnHolderChanged;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcOnThrow__Vector3(Vector3 velocity)
	{
        if (!onThrowVfx) return;
		ParticleSystem.MainModule main = onThrowVfx.main;
		ParticleSystem.MinMaxCurve startSpeed = main.startSpeed;
		startSpeed.constantMax = Mathf.Max(velocity.magnitude, startSpeed.constantMin);
		main.startSpeed = startSpeed;
		onThrowVfx.Play();
	}

	protected static void InvokeUserCode_RpcOnThrow__Vector3(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcOnThrow called on server.");
		}
		else
		{
			((Item)obj).UserCode_RpcOnThrow__Vector3(reader.ReadVector3());
		}
	}

	protected void UserCode_RpcSetEnabled__Boolean(bool isEnabled)
	{
		if (base.gameObject.activeSelf != isEnabled)
		{
			base.gameObject.SetActive(isEnabled);
		}
	}

	protected static void InvokeUserCode_RpcSetEnabled__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetEnabled called on server.");
		}
		else
		{
			((Item)obj).UserCode_RpcSetEnabled__Boolean(reader.ReadBool());
		}
	}

	protected void UserCode_RpcPickup__PlayerInventory(PlayerInventory playerInventory)
	{
		if (!base.isServer)
		{
			SetPickedUp(playerInventory);
		}
	}

	protected static void InvokeUserCode_RpcPickup__PlayerInventory(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcPickup called on server.");
		}
		else
		{
			((Item)obj).UserCode_RpcPickup__PlayerInventory(reader.ReadNetworkBehaviour<PlayerInventory>());
		}
	}

	protected void UserCode_RpcDrop__PlayerInventory(PlayerInventory previousHolder)
	{
		if (!base.isServer)
		{
			SetDropped(previousHolder);
		}
	}

	protected static void InvokeUserCode_RpcDrop__PlayerInventory(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcDrop called on server.");
		}
		else
		{
			((Item)obj).UserCode_RpcDrop__PlayerInventory(reader.ReadNetworkBehaviour<PlayerInventory>());
		}
	}

	protected void UserCode_RpcHandEnter__PlayerInventory(PlayerInventory playerInventory)
	{
		if (!base.isServer)
		{
			SetHandEnter(playerInventory);
		}
	}

	protected static void InvokeUserCode_RpcHandEnter__PlayerInventory(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcHandEnter called on server.");
		}
		else
		{
			((Item)obj).UserCode_RpcHandEnter__PlayerInventory(reader.ReadNetworkBehaviour<PlayerInventory>());
		}
	}

	protected void UserCode_RpcHandExit__PlayerInventory(PlayerInventory playerInventory)
	{
		if (!base.isServer)
		{
			SetHandExit(playerInventory);
		}
	}

	protected static void InvokeUserCode_RpcHandExit__PlayerInventory(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcHandExit called on server.");
		}
		else
		{
			((Item)obj).UserCode_RpcHandExit__PlayerInventory(reader.ReadNetworkBehaviour<PlayerInventory>());
		}
	}

	protected void UserCode_CmdUseItem__Boolean(bool isPressed)
	{
		RpcUseItem(isPressed);
	}

	protected static void InvokeUserCode_CmdUseItem__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdUseItem called on client.");
		}
		else
		{
			((Item)obj).UserCode_CmdUseItem__Boolean(reader.ReadBool());
		}
	}

	protected void UserCode_RpcUseItem__Boolean(bool isPressed)
	{
		if ((bool)NetworkHolder && !NetworkHolder.isLocalPlayer)
		{
			OnUseItem(isPressed);
		}
	}

	protected static void InvokeUserCode_RpcUseItem__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcUseItem called on server.");
		}
		else
		{
			((Item)obj).UserCode_RpcUseItem__Boolean(reader.ReadBool());
		}
	}

	static Item()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(Item), "System.Void Item::CmdUseItem(System.Boolean)", InvokeUserCode_CmdUseItem__Boolean, requiresAuthority: false);
		RemoteProcedureCalls.RegisterRpc(typeof(Item), "System.Void Item::RpcOnThrow(UnityEngine.Vector3)", InvokeUserCode_RpcOnThrow__Vector3);
		RemoteProcedureCalls.RegisterRpc(typeof(Item), "System.Void Item::RpcSetEnabled(System.Boolean)", InvokeUserCode_RpcSetEnabled__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(Item), "System.Void Item::RpcPickup(PlayerInventory)", InvokeUserCode_RpcPickup__PlayerInventory);
		RemoteProcedureCalls.RegisterRpc(typeof(Item), "System.Void Item::RpcDrop(PlayerInventory)", InvokeUserCode_RpcDrop__PlayerInventory);
		RemoteProcedureCalls.RegisterRpc(typeof(Item), "System.Void Item::RpcHandEnter(PlayerInventory)", InvokeUserCode_RpcHandEnter__PlayerInventory);
		RemoteProcedureCalls.RegisterRpc(typeof(Item), "System.Void Item::RpcHandExit(PlayerInventory)", InvokeUserCode_RpcHandExit__PlayerInventory);
		RemoteProcedureCalls.RegisterRpc(typeof(Item), "System.Void Item::RpcUseItem(System.Boolean)", InvokeUserCode_RpcUseItem__Boolean);
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			writer.WriteNetworkBehaviour(NetworkHolder);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 1L) != 0L)
		{
			writer.WriteNetworkBehaviour(NetworkHolder);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize_NetworkBehaviour(ref Holder, _Mirror_SyncVarHookDelegate_Holder, reader, ref ___HolderNetId);
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 1L) != 0L)
		{
			GeneratedSyncVarDeserialize_NetworkBehaviour(ref Holder, _Mirror_SyncVarHookDelegate_Holder, reader, ref ___HolderNetId);
		}
	}
}

}
