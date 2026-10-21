namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;
using global::UnityEngine.Serialization;

public class InteractableBase : NetworkBehaviour, IInteractable
{
	[FormerlySerializedAs("<IsInteractable>k__BackingField")]
	[SerializeField]
	private bool isInteractable = true;

	[FormerlySerializedAs("<MeetRequirements>k__BackingField")]
	[SerializeField]
	private bool meetRequirements = true;

	[FormerlySerializedAs("<TooltipMessage>k__BackingField")]
	[SerializeField]
	private string tooltipMessage = "Press [E] to Interact";

	[FormerlySerializedAs("<Name>k__BackingField")]
	[SerializeField]
	private string interactableName = "Item";

	[FormerlySerializedAs("<CursorType>k__BackingField")]
	[SerializeField]
	private CursorManager.CursorType cursorType = CursorManager.CursorType.Interact;

	private Outline _outline;

	[field: SerializeField]
	public virtual bool HoldInteract { get; set; }

	[field: SerializeField]
	public virtual float HoldDuration { get; set; } = 0.25f;

	public virtual bool IsInteractable
	{
		get
		{
			return isInteractable;
		}
		set
		{
			SetField(ref isInteractable, value);
		}
	}

	public virtual bool MeetRequirements
	{
		get
		{
			return meetRequirements;
		}
		set
		{
			SetField(ref meetRequirements, value);
		}
	}

	[field: SerializeField]
	public virtual bool IsBeingHovered { get; set; }

	[field: SerializeField]
	public virtual bool IsBeingHold { get; set; }

	[field: SerializeField]
	public float HoldProgress { get; set; }

	public virtual string TooltipMessage
	{
		get
		{
			return tooltipMessage;
		}
		set
		{
			SetField(ref tooltipMessage, value);
		}
	}

	public virtual string InteractableName
	{
		get
		{
			return interactableName;
		}
		set
		{
			SetField(ref interactableName, value);
		}
	}

	public virtual CursorManager.CursorType CursorType
	{
		get
		{
			return cursorType;
		}
		set
		{
			SetField(ref cursorType, value);
		}
	}

	public event Action<IInteractable> OnInteractableChanged;

	public override void OnStartClient()
	{
		base.OnStartClient();
		if (!base.isLocalPlayer)
		{
			_outline = base.gameObject.AddComponent<Outline>();
			_outline.OutlineMode = Outline.Mode.OutlineAll;
			_outline.OutlineColor = Color.yellow;
			_outline.OutlineWidth = 5f;
			_outline.enabled = false;
		}
	}

	private void Awake()
	{
		OnAwake();
	}

	protected virtual void OnAwake()
	{
	}

	public virtual void OnHover(PlayerInteract playerInteract)
	{
		if (!IsBeingHovered && IsInteractable && MeetRequirements)
		{
			IsBeingHovered = true;
			if ((bool)_outline)
			{
				_outline.enabled = true;
			}
			IsBeingHold = false;
			HoldProgress = 0f;
			CmdOnHover(playerInteract);
		}
	}

	[Command(requiresAuthority = false)]
	private void CmdOnHover(PlayerInteract playerInteract)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(playerInteract);
		SendCommandInternal("System.Void InteractableBase::CmdOnHover(PlayerInteract)", 1096060196, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public virtual void ServerOnHover(PlayerInteract playerInteract)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void InteractableBase::ServerOnHover(PlayerInteract)' called when server was not active");
		}
	}

	public virtual void OnHoverExit(PlayerInteract playerInteract)
	{
		if (IsBeingHovered)
		{
			IsBeingHovered = false;
			if ((bool)_outline)
			{
				_outline.enabled = false;
			}
			IsBeingHold = false;
			HoldProgress = 0f;
			CmdOnHoverExit(playerInteract);
		}
	}

	[Command(requiresAuthority = false)]
	private void CmdOnHoverExit(PlayerInteract playerInteract)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(playerInteract);
		SendCommandInternal("System.Void InteractableBase::CmdOnHoverExit(PlayerInteract)", 58381884, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public virtual void ServerOnHoverExit(PlayerInteract playerInteract)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void InteractableBase::ServerOnHoverExit(PlayerInteract)' called when server was not active");
		}
	}

	public virtual void OnHold(PlayerInteract playerInteract)
	{
		if (!IsBeingHold)
		{
			IsBeingHold = true;
			HoldProgress = 0f;
			CmdOnHold(playerInteract);
		}
	}

	[Command(requiresAuthority = false)]
	private void CmdOnHold(PlayerInteract playerInteract)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(playerInteract);
		SendCommandInternal("System.Void InteractableBase::CmdOnHold(PlayerInteract)", 1136293411, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public virtual void ServerOnHold(PlayerInteract playerInteract)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void InteractableBase::ServerOnHold(PlayerInteract)' called when server was not active");
		}
	}

	public virtual void OnHoldExit(PlayerInteract playerInteract)
	{
		if (IsBeingHold)
		{
			IsBeingHold = false;
			HoldProgress = 0f;
			CmdOnHoldExit(playerInteract);
		}
	}

	[Command(requiresAuthority = false)]
	private void CmdOnHoldExit(PlayerInteract playerInteract)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(playerInteract);
		SendCommandInternal("System.Void InteractableBase::CmdOnHoldExit(PlayerInteract)", 1949110787, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public virtual void ServerOnHoldExit(PlayerInteract playerInteract)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void InteractableBase::ServerOnHoldExit(PlayerInteract)' called when server was not active");
		}
	}

	public virtual void OnInteract(PlayerInteract playerInteract)
	{
		CmdOnInteract(playerInteract);
	}

	[Command(requiresAuthority = false)]
	private void CmdOnInteract(PlayerInteract playerInteract)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(playerInteract);
		SendCommandInternal("System.Void InteractableBase::CmdOnInteract(PlayerInteract)", -1367533928, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public virtual void ServerOnInteract(PlayerInteract playerInteract)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void InteractableBase::ServerOnInteract(PlayerInteract)' called when server was not active");
		}
		else
		{
			ClientRpcOnInteract(playerInteract);
		}
	}

	[ClientRpc]
	private void ClientRpcOnInteract(PlayerInteract playerInteract)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(playerInteract);
		SendRPCInternal("System.Void InteractableBase::ClientRpcOnInteract(PlayerInteract)", 1920928442, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public virtual void RpcOnInteract(PlayerInteract playerInteract)
	{
	}

	protected virtual void OnDestroy()
	{
		OnInteractableChanged = null;
	}

	private bool SetField<T>(ref T field, T value)
	{
		if (EqualityComparer<T>.Default.Equals(field, value))
		{
			return false;
		}
		field = value;
		OnInteractableChanged?.Invoke(this);
		return true;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_CmdOnHover__PlayerInteract(PlayerInteract playerInteract)
	{
		ServerOnHover(playerInteract);
	}

	protected static void InvokeUserCode_CmdOnHover__PlayerInteract(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdOnHover called on client.");
		}
		else
		{
			((InteractableBase)obj).UserCode_CmdOnHover__PlayerInteract(reader.ReadNetworkBehaviour<PlayerInteract>());
		}
	}

	protected void UserCode_CmdOnHoverExit__PlayerInteract(PlayerInteract playerInteract)
	{
		ServerOnHoverExit(playerInteract);
	}

	protected static void InvokeUserCode_CmdOnHoverExit__PlayerInteract(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdOnHoverExit called on client.");
		}
		else
		{
			((InteractableBase)obj).UserCode_CmdOnHoverExit__PlayerInteract(reader.ReadNetworkBehaviour<PlayerInteract>());
		}
	}

	protected void UserCode_CmdOnHold__PlayerInteract(PlayerInteract playerInteract)
	{
		ServerOnHold(playerInteract);
	}

	protected static void InvokeUserCode_CmdOnHold__PlayerInteract(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdOnHold called on client.");
		}
		else
		{
			((InteractableBase)obj).UserCode_CmdOnHold__PlayerInteract(reader.ReadNetworkBehaviour<PlayerInteract>());
		}
	}

	protected void UserCode_CmdOnHoldExit__PlayerInteract(PlayerInteract playerInteract)
	{
		ServerOnHoldExit(playerInteract);
	}

	protected static void InvokeUserCode_CmdOnHoldExit__PlayerInteract(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdOnHoldExit called on client.");
		}
		else
		{
			((InteractableBase)obj).UserCode_CmdOnHoldExit__PlayerInteract(reader.ReadNetworkBehaviour<PlayerInteract>());
		}
	}

	protected void UserCode_CmdOnInteract__PlayerInteract(PlayerInteract playerInteract)
	{
		ServerOnInteract(playerInteract);
	}

	protected static void InvokeUserCode_CmdOnInteract__PlayerInteract(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdOnInteract called on client.");
		}
		else
		{
			((InteractableBase)obj).UserCode_CmdOnInteract__PlayerInteract(reader.ReadNetworkBehaviour<PlayerInteract>());
		}
	}

	protected void UserCode_ClientRpcOnInteract__PlayerInteract(PlayerInteract playerInteract)
	{
		RpcOnInteract(playerInteract);
	}

	protected static void InvokeUserCode_ClientRpcOnInteract__PlayerInteract(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC ClientRpcOnInteract called on server.");
		}
		else
		{
			((InteractableBase)obj).UserCode_ClientRpcOnInteract__PlayerInteract(reader.ReadNetworkBehaviour<PlayerInteract>());
		}
	}

	static InteractableBase()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(InteractableBase), "System.Void InteractableBase::CmdOnHover(PlayerInteract)", InvokeUserCode_CmdOnHover__PlayerInteract, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(InteractableBase), "System.Void InteractableBase::CmdOnHoverExit(PlayerInteract)", InvokeUserCode_CmdOnHoverExit__PlayerInteract, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(InteractableBase), "System.Void InteractableBase::CmdOnHold(PlayerInteract)", InvokeUserCode_CmdOnHold__PlayerInteract, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(InteractableBase), "System.Void InteractableBase::CmdOnHoldExit(PlayerInteract)", InvokeUserCode_CmdOnHoldExit__PlayerInteract, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(InteractableBase), "System.Void InteractableBase::CmdOnInteract(PlayerInteract)", InvokeUserCode_CmdOnInteract__PlayerInteract, requiresAuthority: false);
		RemoteProcedureCalls.RegisterRpc(typeof(InteractableBase), "System.Void InteractableBase::ClientRpcOnInteract(PlayerInteract)", InvokeUserCode_ClientRpcOnInteract__PlayerInteract);
	}
}

}
