namespace SushiAndFish.Casino.Original
{
using global::System;
using global::DG.Tweening;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class EyeZoomEffect : NetworkBehaviour
{
	[SerializeField]
	private float initialEyeScale = 0.4f;

	[SerializeField]
	private float zoomedEyeScale = 0.6f;

	private PlayerEyes _playerEyes;

	private void Awake()
	{
		_playerEyes = GetComponent<PlayerEyes>();
	}

	public override void OnStartClient()
	{
		base.OnStartClient();
		if (!base.isLocalPlayer)
		{
			base.enabled = false;
		}
	}

	private void OnEnable()
	{
		InputEvents.OnZoomEvent = (Action<bool>)Delegate.Combine(InputEvents.OnZoomEvent, new Action<bool>(OnEyeZoom));
	}

	private void OnDisable()
	{
		InputEvents.OnZoomEvent = (Action<bool>)Delegate.Remove(InputEvents.OnZoomEvent, new Action<bool>(OnEyeZoom));
	}

	private void OnEyeZoom(bool isPressed)
	{
		LocalOnEyeZoom(isPressed);
		CmdOnEyeZoom(isPressed);
	}

	[Command]
	private void CmdOnEyeZoom(bool isPressed)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isPressed);
		SendCommandInternal("System.Void EyeZoomEffect::CmdOnEyeZoom(System.Boolean)", -2065909384, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcOnEyeZoom(bool isPressed)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isPressed);
		SendRPCInternal("System.Void EyeZoomEffect::RpcOnEyeZoom(System.Boolean)", -321266763, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void LocalOnEyeZoom(bool isPressed)
	{
		if (isPressed)
		{
			_playerEyes.EyeLeft.DOScaleZ(zoomedEyeScale, 0.2f).SetEase(Ease.OutQuad);
			_playerEyes.EyeRight.DOScaleZ(zoomedEyeScale, 0.2f).SetEase(Ease.OutQuad);
		}
		else
		{
			_playerEyes.EyeLeft.DOScaleZ(initialEyeScale, 0.2f).SetEase(Ease.OutQuad);
			_playerEyes.EyeRight.DOScaleZ(initialEyeScale, 0.2f).SetEase(Ease.OutQuad);
		}
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_CmdOnEyeZoom__Boolean(bool isPressed)
	{
		RpcOnEyeZoom(isPressed);
	}

	protected static void InvokeUserCode_CmdOnEyeZoom__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdOnEyeZoom called on client.");
		}
		else
		{
			((EyeZoomEffect)obj).UserCode_CmdOnEyeZoom__Boolean(reader.ReadBool());
		}
	}

	protected void UserCode_RpcOnEyeZoom__Boolean(bool isPressed)
	{
		if (!base.isLocalPlayer)
		{
			LocalOnEyeZoom(isPressed);
		}
	}

	protected static void InvokeUserCode_RpcOnEyeZoom__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcOnEyeZoom called on server.");
		}
		else
		{
			((EyeZoomEffect)obj).UserCode_RpcOnEyeZoom__Boolean(reader.ReadBool());
		}
	}

	static EyeZoomEffect()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(EyeZoomEffect), "System.Void EyeZoomEffect::CmdOnEyeZoom(System.Boolean)", InvokeUserCode_CmdOnEyeZoom__Boolean, requiresAuthority: true);
		RemoteProcedureCalls.RegisterRpc(typeof(EyeZoomEffect), "System.Void EyeZoomEffect::RpcOnEyeZoom(System.Boolean)", InvokeUserCode_RpcOnEyeZoom__Boolean);
	}
}

}
