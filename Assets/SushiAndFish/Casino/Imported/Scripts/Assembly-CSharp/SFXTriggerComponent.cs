namespace SushiAndFish.Casino.Original
{
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class SFXTriggerComponent : NetworkBehaviour
{
	[SerializeField]
	private Transform customPosition;

	[Header("Trigger Enter")]
	[SerializeField]
	private EventReference eventReference;

	[SerializeField]
	private LayerMask allowedLayers;

	[SerializeField]
	private float cooldownTime = 1f;

	private float _saved_cooldown;

	[SerializeField]
	private bool clientOnly = true;

	[SerializeField]
	private bool disableColliderAfterTrigger;

	private int playerLayer = 6;

	[Header("Trigger Exit")]
	[SerializeField]
	private bool useTriggerExit;

	[SerializeField]
	private EventReference exitEventReference;

	private Collider _collider;

	private void Awake()
	{
		_collider = GetComponent<Collider>();
	}

	private void OnTriggerEnter(Collider other)
	{
		if (!base.isActiveAndEnabled || Time.time < _saved_cooldown || !IsLayerAllowed(other.gameObject.layer) || eventReference.IsNull)
		{
			return;
		}
		if (!clientOnly && other.gameObject.layer == playerLayer)
		{
			if (base.isServer)
			{
				CmdTriggerServerEvent();
				if (disableColliderAfterTrigger)
				{
					_collider.enabled = false;
				}
			}
		}
		else if (other.gameObject.layer == playerLayer && IsLocalPlayerCollider(other))
		{
			TriggerEnterEvent();
			if (disableColliderAfterTrigger)
			{
				_collider.enabled = false;
			}
		}
	}

    private static bool IsLocalPlayerCollider(Collider other)
    {
        if(CasinoRuntimeContext.IsHostIntegration)
        {
            var host=other.GetComponentInParent<PaperPixelGames.Core.Player.PPG_Player>();
            if(host)return host.IsOwner;
        }
        var identity=other.attachedRigidbody?other.attachedRigidbody.GetComponent<NetworkIdentity>():other.GetComponentInParent<NetworkIdentity>();
        return identity&&identity.isLocalPlayer;
    }

	private void OnTriggerExit(Collider other)
	{
		if (!base.isActiveAndEnabled || !useTriggerExit || Time.time < _saved_cooldown || !IsLayerAllowed(other.gameObject.layer) || exitEventReference.IsNull)
		{
			return;
		}
		if (!clientOnly && other.gameObject.layer == playerLayer)
		{
			if (base.isServer)
			{
				CmdTriggerServerExitEvent();
				if (disableColliderAfterTrigger)
				{
					_collider.enabled = false;
				}
			}
		}
		else if (other.gameObject.layer == playerLayer && IsLocalPlayerCollider(other))
		{
			TriggerExitEvent();
			if (disableColliderAfterTrigger)
			{
				_collider.enabled = false;
			}
		}
	}

	private bool IsLayerAllowed(int layer)
	{
		return ((int)allowedLayers & (1 << layer)) != 0;
	}

	[Command(requiresAuthority = false)]
	private void CmdTriggerServerEvent()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendCommandInternal("System.Void SFXTriggerComponent::CmdTriggerServerEvent()", 657598474, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcTriggerServerEvent()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void SFXTriggerComponent::RpcTriggerServerEvent()", -668821287, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void TriggerEnterEvent()
	{
		Vector3 pos = ((customPosition == null) ? base.transform.position : customPosition.position);
		SFXManager.SFXOneShot(eventReference, pos);
		_saved_cooldown = Time.time + cooldownTime;
	}

	[Command(requiresAuthority = false)]
	private void CmdTriggerServerExitEvent()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendCommandInternal("System.Void SFXTriggerComponent::CmdTriggerServerExitEvent()", -1927226966, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcTriggerServerExitEvent()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void SFXTriggerComponent::RpcTriggerServerExitEvent()", -731581903, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void TriggerExitEvent()
	{
		Vector3 pos = ((customPosition == null) ? base.transform.position : customPosition.position);
		SFXManager.SFXOneShot(exitEventReference, pos);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_CmdTriggerServerEvent()
	{
		RpcTriggerServerEvent();
	}

	protected static void InvokeUserCode_CmdTriggerServerEvent(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdTriggerServerEvent called on client.");
		}
		else
		{
			((SFXTriggerComponent)obj).UserCode_CmdTriggerServerEvent();
		}
	}

	protected void UserCode_RpcTriggerServerEvent()
	{
		TriggerEnterEvent();
	}

	protected static void InvokeUserCode_RpcTriggerServerEvent(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcTriggerServerEvent called on server.");
		}
		else
		{
			((SFXTriggerComponent)obj).UserCode_RpcTriggerServerEvent();
		}
	}

	protected void UserCode_CmdTriggerServerExitEvent()
	{
		RpcTriggerServerExitEvent();
	}

	protected static void InvokeUserCode_CmdTriggerServerExitEvent(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdTriggerServerExitEvent called on client.");
		}
		else
		{
			((SFXTriggerComponent)obj).UserCode_CmdTriggerServerExitEvent();
		}
	}

	protected void UserCode_RpcTriggerServerExitEvent()
	{
		TriggerExitEvent();
	}

	protected static void InvokeUserCode_RpcTriggerServerExitEvent(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcTriggerServerExitEvent called on server.");
		}
		else
		{
			((SFXTriggerComponent)obj).UserCode_RpcTriggerServerExitEvent();
		}
	}

	static SFXTriggerComponent()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(SFXTriggerComponent), "System.Void SFXTriggerComponent::CmdTriggerServerEvent()", InvokeUserCode_CmdTriggerServerEvent, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(SFXTriggerComponent), "System.Void SFXTriggerComponent::CmdTriggerServerExitEvent()", InvokeUserCode_CmdTriggerServerExitEvent, requiresAuthority: false);
		RemoteProcedureCalls.RegisterRpc(typeof(SFXTriggerComponent), "System.Void SFXTriggerComponent::RpcTriggerServerEvent()", InvokeUserCode_RpcTriggerServerEvent);
		RemoteProcedureCalls.RegisterRpc(typeof(SFXTriggerComponent), "System.Void SFXTriggerComponent::RpcTriggerServerExitEvent()", InvokeUserCode_RpcTriggerServerExitEvent);
	}
}

}
