namespace SushiAndFish.Casino.Original
{
using global::System;
using global::FMOD.Studio;
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::Unity.Mathematics;
using global::UnityEngine;

public class PlayerSFX : NetworkBehaviour
{
	[Header("SFX References")]
	[SerializeField]
	private EventReference throwItem;

	[SerializeField]
	private EventReference pickupItem;

	[SerializeField]
	private EventReference pickupChip;

	[SerializeField]
	private EventReference jump;

	[SerializeField]
	private EventReference landing;

	[SerializeField]
	private EventReference footstep;

	[SerializeField]
	private EventReference zoom;

	private PlayerSettings _ps;

	private PlayerInventory _pi;

	private PlayerController _pc;

	private EventInstance _walkInstance;

	private bool _isWalking;

	private bool _isGrounded;

	private void Awake()
	{
		_ps = SushiAndFish.Casino.Original.CasinoResources.Load<PlayerSettings>("PlayerSettings");
		_pc = GetComponent<PlayerController>();
		_pi = GetComponent<PlayerInventory>();
	}

	private void OnEnable()
	{
		_pc.OnClientJumped += OnJump;
		_pc.OnClientLanded += OnLand;
		_pi.OnClientItemThrown += OnThrowItem;
		_pi.OnClientItemPickup += OnPickupItem;
		InputEvents.OnZoomEvent = (Action<bool>)Delegate.Combine(InputEvents.OnZoomEvent, new Action<bool>(OnZoom));
	}

	private void OnDisable()
	{
		_pc.OnClientJumped -= OnJump;
		_pc.OnClientLanded -= OnLand;
		_pi.OnClientItemThrown -= OnThrowItem;
		_pi.OnClientItemPickup -= OnPickupItem;
		InputEvents.OnZoomEvent = (Action<bool>)Delegate.Remove(InputEvents.OnZoomEvent, new Action<bool>(OnZoom));
		if (_walkInstance.isValid())
		{
			_walkInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
			_walkInstance.release();
		}
	}

	private void Update()
	{
		SetIsGrounded();
		Walking();
	}

	private void SetIsGrounded()
	{
		bool isGrounded = _pc.isGrounded;
		if (_isGrounded != isGrounded)
		{
			_isGrounded = isGrounded;
			if (_walkInstance.isValid())
			{
				_walkInstance.setParameterByName("IsGrounded", isGrounded ? 1 : 0);
			}
		}
	}

	private void Walking()
	{
		bool flag = _pc.serverVelocity.sqrMagnitude > 0.1f;
		if (_isWalking != flag)
		{
			_isWalking = flag;
			if (flag)
			{
				_walkInstance = RuntimeManager.CreateInstance(footstep);
				_walkInstance.setParameterByName("IsGrounded", _isGrounded ? 1 : 0);
				RuntimeManager.AttachInstanceToGameObject(_walkInstance, base.gameObject, nonRigidbodyVelocity: true);
				_walkInstance.start();
			}
			else
			{
				_walkInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
				_walkInstance.release();
			}
		}
	}

	private void OnJump(bool isGrounded)
	{
		SFXParams[] sFXParams = new SFXParams[1]
		{
			new SFXParams("IsLocalPlayer", base.isLocalPlayer ? 1 : 0)
		};
		SFXManager.SFXOneShot3DAttachedWithParameters(jump, sFXParams, base.gameObject);
	}

	private void OnLand(float fallImpact)
	{
		float v = math.remap(0.05f, 20f, 0f, 1f, fallImpact);
		SFXParams[] sFXParams = new SFXParams[2]
		{
			new SFXParams("IsLocalPlayer", base.isLocalPlayer ? 1 : 0),
			new SFXParams("Force", v)
		};
		SFXManager.SFXOneShot3DAttachedWithParameters(landing, sFXParams, base.gameObject);
	}

	private void OnThrowItem(float force, Item item)
	{
		float v = math.remap(_ps.minItemThrowForce, _ps.maxItemThrowForce, 0f, 1f, force);
		SFXParams[] sFXParams = new SFXParams[1]
		{
			new SFXParams("Force", v)
		};
		SFXManager.SFXOneShot3DAttachedWithParameters(throwItem, sFXParams, base.gameObject);
	}

	private void OnPickupItem(Item item)
	{
		SFXManager.SFXOneShot3DAttached(pickupItem, base.gameObject);
	}

	private void OnZoom(bool isPressed)
	{
		if (base.isLocalPlayer)
		{
			ClientOnZoom(isPressed);
			CmdOnZoom(isPressed);
		}
	}

	[Command]
	private void CmdOnZoom(bool isPressed)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isPressed);
		SendCommandInternal("System.Void PlayerSFX::CmdOnZoom(System.Boolean)", 661333140, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcOnZoom(bool isPressed)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isPressed);
		SendRPCInternal("System.Void PlayerSFX::RpcOnZoom(System.Boolean)", -1627116593, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void ClientOnZoom(bool isPressed)
	{
		SFXManager.SFXOneShot3DAttached(zoom, base.gameObject, isPressed);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_CmdOnZoom__Boolean(bool isPressed)
	{
		RpcOnZoom(isPressed);
	}

	protected static void InvokeUserCode_CmdOnZoom__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdOnZoom called on client.");
		}
		else
		{
			((PlayerSFX)obj).UserCode_CmdOnZoom__Boolean(reader.ReadBool());
		}
	}

	protected void UserCode_RpcOnZoom__Boolean(bool isPressed)
	{
		if (!base.isLocalPlayer)
		{
			ClientOnZoom(isPressed);
		}
	}

	protected static void InvokeUserCode_RpcOnZoom__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcOnZoom called on server.");
		}
		else
		{
			((PlayerSFX)obj).UserCode_RpcOnZoom__Boolean(reader.ReadBool());
		}
	}

	static PlayerSFX()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(PlayerSFX), "System.Void PlayerSFX::CmdOnZoom(System.Boolean)", InvokeUserCode_CmdOnZoom__Boolean, requiresAuthority: true);
		RemoteProcedureCalls.RegisterRpc(typeof(PlayerSFX), "System.Void PlayerSFX::RpcOnZoom(System.Boolean)", InvokeUserCode_RpcOnZoom__Boolean);
	}
}

}
