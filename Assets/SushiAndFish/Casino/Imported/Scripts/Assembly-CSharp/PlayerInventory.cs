namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Runtime.InteropServices;
using SushiAndFish.Casino.Original.Extensions;
using global::JetBrains.Annotations;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::SkyBrave_Toolkit.Scripts.Scriptable_Game_Events;
using global::UnityEngine;

public class PlayerInventory : NetworkBehaviour
{
	[Header("References")]
	public Transform handTransform;

	public Transform pocketTransform;

	public Transform throwPosition;

	[Tooltip("Optional. Player hand rig to hide when holding an item that has its own itemHandRig.")]
	[SerializeField]
	private GameObject playerHands;

	[SyncVar(hook = "OnHoldingItemChanged")]
	[CanBeNull]
	public Item holdingItem;

	[ItemCanBeNull]
	public readonly SyncList<Item> Pockets = new SyncList<Item>();

	[Header("Settings")]
	public uint inventorySlotCount;

	private PlayerSettings _ps;

	private PlayerController _pc;

	private Rigidbody _rigidbody;

	private Coroutine _throwRoutine;

	private float _currentThrowPercentage;

	private bool _isThrowCancelled;

	private bool _localAlreadyThrown;

	public GameEvent localOnInventoryUpdate;

	[SerializeField]
	private SFXLoopComponent throwSfxLoopComponent;

	protected NetworkBehaviourSyncVar ___holdingItemNetId;

	public Action<Item, Item> _Mirror_SyncVarHookDelegate_holdingItem;

	public Item NetworkholdingItem
	{
		get
		{
			return GetSyncVarNetworkBehaviour(___holdingItemNetId, ref holdingItem);
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter_NetworkBehaviour(value, ref holdingItem, 1uL, _Mirror_SyncVarHookDelegate_holdingItem, ref ___holdingItemNetId);
		}
	}

	public event Action<Item> OnClientItemPickup;

	public event Action<float, Item> OnClientItemThrown;

	public event Action ServerOnItemStash;

	public event Action<float> OnThrowChargeChanged;

	public event Action OnLocalInventoryUpdated;

	public override void OnStartServer()
	{
		base.OnStartServer();
		for (int i = 0; i < inventorySlotCount; i++)
		{
			Pockets.Add(null);
		}
	}

	public override void OnStartClient()
	{
		base.OnStartClient();
		SyncList<Item> pockets = Pockets;
		pockets.Callback = (Action<SyncList<Item>.Operation, int, Item, Item>)Delegate.Combine(pockets.Callback, new Action<SyncList<Item>.Operation, int, Item, Item>(OnPocketsChanged));
		if (!base.isLocalPlayer)
		{
			base.enabled = false;
			return;
		}
		MonoSingleton<LocalManager>.Instance.interactionUIPanel.SetPlayerInventory(this);
		MonoSingleton<LocalManager>.Instance.heldItemActionPanel.SetPlayerInventory(this);
	}

	private void Awake()
	{
		_ps = SushiAndFish.Casino.Original.CasinoResources.Load<PlayerSettings>("PlayerSettings");
		_rigidbody = GetComponent<Rigidbody>();
		_pc = GetComponent<PlayerController>();
	}

	private void OnEnable()
	{
		InputEvents.OnThrowItemEvent = (Action<bool>)Delegate.Combine(InputEvents.OnThrowItemEvent, new Action<bool>(OnThrowItemEvent));
		InputEvents.OnItemSelectEvent = (Action<int>)Delegate.Combine(InputEvents.OnItemSelectEvent, new Action<int>(OnItemSelectEvent));
		InputEvents.OnZoomEvent = (Action<bool>)Delegate.Combine(InputEvents.OnZoomEvent, new Action<bool>(OnZoomEvent));
	}

	private void OnDisable()
	{
		InputEvents.OnThrowItemEvent = (Action<bool>)Delegate.Remove(InputEvents.OnThrowItemEvent, new Action<bool>(OnThrowItemEvent));
		InputEvents.OnItemSelectEvent = (Action<int>)Delegate.Remove(InputEvents.OnItemSelectEvent, new Action<int>(OnItemSelectEvent));
		InputEvents.OnZoomEvent = (Action<bool>)Delegate.Remove(InputEvents.OnZoomEvent, new Action<bool>(OnZoomEvent));
	}

	public void SetPlayerHandsVisible(bool visible)
	{
		if (playerHands != null)
		{
			playerHands.SetActive(visible);
		}
	}

	[Tooltip("Only call this from Item script!!")]
	[Server]
	public void ServerAddItem(Item item)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void PlayerInventory::ServerAddItem(Item)' called when server was not active");
		}
		else
		{
			if (NetworkholdingItem == item)
			{
				return;
			}
			if ((bool)NetworkholdingItem)
			{
				bool flag = false;
				for (int i = 0; i < Pockets.Count; i++)
				{
					if (Pockets[i] == null)
					{
						Pockets[i] = NetworkholdingItem;
						NetworkholdingItem = null;
						Pockets[i].ServerHandExit(this);
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					NetworkholdingItem.ServerDrop();
				}
			}
			NetworkholdingItem = item;
		}
	}

	[Tooltip("Only call this from Item script!!")]
	[Server]
	public void ServerRemoveItem(Item item)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void PlayerInventory::ServerRemoveItem(Item)' called when server was not active");
		}
		else if ((bool)NetworkholdingItem && !(NetworkholdingItem != item))
		{
			NetworkholdingItem = null;
		}
	}

	[Server]
	[CanBeNull]
	public Item ServerDropHoldingItem()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'Item PlayerInventory::ServerDropHoldingItem()' called when server was not active");
			return null;
		}
		if ((bool)NetworkholdingItem)
		{
			NetworkholdingItem.ServerDrop();
			return NetworkholdingItem;
		}
		return null;
	}

	[Server]
	public void ServerThrowItemRandomly()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void PlayerInventory::ServerThrowItemRandomly()' called when server was not active");
		}
		else
		{
			ServerThrowItem(_pc.serverVelocity, (UnityEngine.Random.insideUnitSphere + Vector3.up).normalized, _ps.maxItemThrowForce, _ps.maxItemThrowTorque);
		}
	}

	[Server]
	public void ServerRemoveItemFromPocket(Item item)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void PlayerInventory::ServerRemoveItemFromPocket(Item)' called when server was not active");
			return;
		}
		for (int i = 0; i < Pockets.Count; i++)
		{
			if (Pockets[i] == item)
			{
				item.ServerDrop();
				Pockets[i] = null;
			}
		}
	}

	private void OnHoldingItemChanged([CanBeNull] Item oldItem, [CanBeNull] Item newItem)
	{
		if ((bool)newItem)
		{
			OnClientItemPickup?.Invoke(newItem);
		}
		OnInventoryUpdate(oldItem, newItem);
	}

	private void OnPocketsChanged(SyncList<Item>.Operation op, int index, Item oldItem, Item newItem)
	{
		OnInventoryUpdate(oldItem, newItem);
	}

	private void OnInventoryUpdate([CanBeNull] Item oldItem, [CanBeNull] Item newItem)
	{
		if (base.isLocalPlayer)
		{
			if ((bool)newItem)
			{
				_localAlreadyThrown = false;
			}
			localOnInventoryUpdate?.Raise();
			OnLocalInventoryUpdated?.Invoke();
			StopThrowRoutine();
			if ((bool)newItem && InputEvents.IsThrowItemPressed)
			{
				_throwRoutine = StartCoroutine(ThrowRoutine());
			}
		}
	}

	private void OnItemSelectEvent(int index)
	{
		if (index <= Pockets.Count && index > 0)
		{
			int slot = index - 1;
			CmdSelectSlot(slot);
		}
	}

	[Command]
	private void CmdSelectSlot(int slot)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(slot);
		SendCommandInternal("System.Void PlayerInventory::CmdSelectSlot(System.Int32)", -1745405221, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcSetItemParent(Item item, bool active)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(item);
		writer.WriteBool(active);
		SendRPCInternal("System.Void PlayerInventory::RpcSetItemParent(Item,System.Boolean)", -1603755222, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void OnZoomEvent(bool isPressed)
	{
		if (isPressed)
		{
			if (_throwRoutine != null)
			{
				_isThrowCancelled = true;
			}
			StopThrowRoutine();
		}
	}

	private void OnThrowItemEvent(bool isPressed)
	{
		if (!NetworkholdingItem || _localAlreadyThrown)
		{
			return;
		}
		if (!isPressed)
		{
			if (_isThrowCancelled)
			{
				_isThrowCancelled = false;
				return;
			}
			_localAlreadyThrown = true;
			float force = Mathf.Lerp(_ps.minItemThrowForce, _ps.maxItemThrowForce, _currentThrowPercentage);
			float torque = Mathf.Lerp(_ps.minItemThrowTorque, _ps.maxItemThrowTorque, _currentThrowPercentage);
			StopThrowRoutine();
			NetworkholdingItem.OnLocalDrop();
			CmdThrowItem(_rigidbody.linearVelocity, throwPosition.forward, force, torque);
			OnItemThrown(force, NetworkholdingItem);
			CmdOnItemThrown(force, NetworkholdingItem);
		}
		else
		{
			if (_throwRoutine != null)
			{
				StopCoroutine(_throwRoutine);
			}
			_throwRoutine = StartCoroutine(ThrowRoutine());
		}
	}

	private void StopThrowRoutine()
	{
		if (_throwRoutine != null)
		{
			StopCoroutine(_throwRoutine);
		}
		_throwRoutine = null;
		_currentThrowPercentage = 0f;
		throwSfxLoopComponent.LoopSFX(play: false);
		OnThrowChargeChanged?.Invoke(0f);
	}

	[Command]
	private void CmdOnItemThrown(float force, Item item)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteFloat(force);
		writer.WriteNetworkBehaviour(item);
		SendCommandInternal("System.Void PlayerInventory::CmdOnItemThrown(System.Single,Item)", -862694014, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcOnItemThrown(float force, Item item)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteFloat(force);
		writer.WriteNetworkBehaviour(item);
		SendRPCInternal("System.Void PlayerInventory::RpcOnItemThrown(System.Single,Item)", -781555935, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void OnItemThrown(float force, Item item)
	{
		OnClientItemThrown?.Invoke(force, item);
	}

	private IEnumerator ThrowRoutine()
	{
		throwSfxLoopComponent.LoopSFX(play: true);
		_currentThrowPercentage = 0f;
		OnThrowChargeChanged?.Invoke(0f);
		float percentage = 0f;
		while (percentage < 1f)
		{
			percentage = Mathf.Clamp01(percentage + Time.deltaTime / _ps.itemThrowDuration);
			_currentThrowPercentage = Mathf.InverseLerp(_ps.throwThreshold / _ps.itemThrowDuration, 1f, percentage);
			if (_currentThrowPercentage > 0f)
			{
				OnThrowChargeChanged?.Invoke(_currentThrowPercentage);
			}
			yield return new WaitForEndOfFrame();
		}
	}

	[Command]
	private void CmdThrowItem(Vector3 velocity, Vector3 direction, float force, float torque)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVector3(velocity);
		writer.WriteVector3(direction);
		writer.WriteFloat(force);
		writer.WriteFloat(torque);
		SendCommandInternal("System.Void PlayerInventory::CmdThrowItem(UnityEngine.Vector3,UnityEngine.Vector3,System.Single,System.Single)", 1818901107, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	private void ServerThrowItem(Vector3 velocity, Vector3 direction, float force, float torque)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void PlayerInventory::ServerThrowItem(UnityEngine.Vector3,UnityEngine.Vector3,System.Single,System.Single)' called when server was not active");
		}
		else if ((bool)NetworkholdingItem)
		{
			Item networkholdingItem = NetworkholdingItem;
			Vector3 velocity2 = velocity + direction * force / (networkholdingItem.Mass + _ps.constantMass);
			Vector3 angularVelocity = UnityEngine.Random.insideUnitSphere * torque / (networkholdingItem.Mass + _ps.constantMass);
			networkholdingItem.ServerThrow(throwPosition.position, throwPosition.rotation, velocity2, angularVelocity);
		}
	}

	public PlayerInventory()
	{
		InitSyncObject(Pockets);
		_Mirror_SyncVarHookDelegate_holdingItem = OnHoldingItemChanged;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_CmdSelectSlot__Int32(int slot)
	{
		if (NetworkholdingItem != null)
		{
			ServerOnItemStash?.Invoke();
			if (Pockets[slot] == null)
			{
				Pockets[slot] = NetworkholdingItem;
				NetworkholdingItem = null;
				Pockets[slot].ServerHandExit(this);
				return;
			}
			SyncList<Item> pockets = Pockets;
			Item networkholdingItem = NetworkholdingItem;
			Item networkholdingItem2 = Pockets[slot];
			Item item = (pockets[slot] = networkholdingItem);
			NetworkholdingItem = networkholdingItem2;
			Pockets[slot].ServerHandExit(this);
			NetworkholdingItem.ServerHandEnter(this);
		}
		else if (Pockets[slot] != null)
		{
			Item networkholdingItem3 = Pockets[slot];
			Pockets[slot] = null;
			NetworkholdingItem = networkholdingItem3;
			NetworkholdingItem.ServerHandEnter(this);
		}
	}

	protected static void InvokeUserCode_CmdSelectSlot__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdSelectSlot called on client.");
		}
		else
		{
			((PlayerInventory)obj).UserCode_CmdSelectSlot__Int32(reader.ReadVarInt());
		}
	}

	protected void UserCode_RpcSetItemParent__Item__Boolean(Item item, bool active)
	{
		if (active)
		{
			item.transform.SetParent(handTransform);
			item.gameObject.SetActive(value: true);
		}
		else
		{
			item.gameObject.SetActive(value: false);
			item.transform.SetParent(pocketTransform);
		}
	}

	protected static void InvokeUserCode_RpcSetItemParent__Item__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetItemParent called on server.");
		}
		else
		{
			((PlayerInventory)obj).UserCode_RpcSetItemParent__Item__Boolean(reader.ReadNetworkBehaviour<Item>(), reader.ReadBool());
		}
	}

	protected void UserCode_CmdOnItemThrown__Single__Item(float force, Item item)
	{
		RpcOnItemThrown(force, item);
	}

	protected static void InvokeUserCode_CmdOnItemThrown__Single__Item(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdOnItemThrown called on client.");
		}
		else
		{
			((PlayerInventory)obj).UserCode_CmdOnItemThrown__Single__Item(reader.ReadFloat(), reader.ReadNetworkBehaviour<Item>());
		}
	}

	protected void UserCode_RpcOnItemThrown__Single__Item(float force, Item item)
	{
		if (!base.isLocalPlayer)
		{
			OnItemThrown(force, item);
		}
	}

	protected static void InvokeUserCode_RpcOnItemThrown__Single__Item(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcOnItemThrown called on server.");
		}
		else
		{
			((PlayerInventory)obj).UserCode_RpcOnItemThrown__Single__Item(reader.ReadFloat(), reader.ReadNetworkBehaviour<Item>());
		}
	}

	protected void UserCode_CmdThrowItem__Vector3__Vector3__Single__Single(Vector3 velocity, Vector3 direction, float force, float torque)
	{
		ServerThrowItem(velocity, direction, force, torque);
	}

	protected static void InvokeUserCode_CmdThrowItem__Vector3__Vector3__Single__Single(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdThrowItem called on client.");
		}
		else
		{
			((PlayerInventory)obj).UserCode_CmdThrowItem__Vector3__Vector3__Single__Single(reader.ReadVector3(), reader.ReadVector3(), reader.ReadFloat(), reader.ReadFloat());
		}
	}

	static PlayerInventory()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(PlayerInventory), "System.Void PlayerInventory::CmdSelectSlot(System.Int32)", InvokeUserCode_CmdSelectSlot__Int32, requiresAuthority: true);
		RemoteProcedureCalls.RegisterCommand(typeof(PlayerInventory), "System.Void PlayerInventory::CmdOnItemThrown(System.Single,Item)", InvokeUserCode_CmdOnItemThrown__Single__Item, requiresAuthority: true);
		RemoteProcedureCalls.RegisterCommand(typeof(PlayerInventory), "System.Void PlayerInventory::CmdThrowItem(UnityEngine.Vector3,UnityEngine.Vector3,System.Single,System.Single)", InvokeUserCode_CmdThrowItem__Vector3__Vector3__Single__Single, requiresAuthority: true);
		RemoteProcedureCalls.RegisterRpc(typeof(PlayerInventory), "System.Void PlayerInventory::RpcSetItemParent(Item,System.Boolean)", InvokeUserCode_RpcSetItemParent__Item__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(PlayerInventory), "System.Void PlayerInventory::RpcOnItemThrown(System.Single,Item)", InvokeUserCode_RpcOnItemThrown__Single__Item);
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			writer.WriteNetworkBehaviour(NetworkholdingItem);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 1L) != 0L)
		{
			writer.WriteNetworkBehaviour(NetworkholdingItem);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize_NetworkBehaviour(ref holdingItem, _Mirror_SyncVarHookDelegate_holdingItem, reader, ref ___holdingItemNetId);
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 1L) != 0L)
		{
			GeneratedSyncVarDeserialize_NetworkBehaviour(ref holdingItem, _Mirror_SyncVarHookDelegate_holdingItem, reader, ref ___holdingItemNetId);
		}
	}
}

}
