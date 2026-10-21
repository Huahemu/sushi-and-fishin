namespace SushiAndFish.Casino.Original
{
using global::System;
using global::DG.Tweening;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class PlayerOrgans : NetworkBehaviour
{
	[Header("References")]
	[SerializeField]
	private GameObject leftEyeModel;

	[SerializeField]
	private GameObject rightEyeModel;

	[SerializeField]
	private GameObject bodyModel;

	[SerializeField]
	private GameObject mouthModel;

	private bool _localLeftEye = true;

	private bool _localRightEye = true;

	private bool _localBody = true;

	private bool _localMouth = true;

	private PlayerController _pc;

	private PlayerProfile _pp;

	private Rigidbody _rb;

	private CustomDrag _cd;

	private PlayerSettings _ps;

	private PlayerEyesUI _pe;

	public Transform LeftEye => leftEyeModel.transform.parent;

	public Transform RightEye => rightEyeModel.transform.parent;

	private void Awake()
	{
		_pc = GetComponent<PlayerController>();
		_pp = GetComponent<PlayerProfile>();
		_rb = GetComponent<Rigidbody>();
		_cd = GetComponent<CustomDrag>();
		_ps = SushiAndFish.Casino.Original.CasinoResources.Load<PlayerSettings>("PlayerSettings");
		_pe = MonoSingleton<LocalManager>.Instance.playerEyesUI;
	}

	private void OnEnable()
	{
		PlayerProfile pp = _pp;
		pp.OnPlayerProfileUpdated = (Action)Delegate.Combine(pp.OnPlayerProfileUpdated, new Action(OnProfileSync));
	}

	private void OnDisable()
	{
		PlayerProfile pp = _pp;
		pp.OnPlayerProfileUpdated = (Action)Delegate.Remove(pp.OnPlayerProfileUpdated, new Action(OnProfileSync));
	}

	private void OnProfileSync()
	{
		if (base.isServer)
		{
			NetworkSingleton<OrganManager>.Instance.ServerRegisterPlayer(this);
		}
	}

	[Server]
	public void ServerSetBodyParts(PlayerOrganData data)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void PlayerOrgans::ServerSetBodyParts(PlayerOrganData)' called when server was not active");
			return;
		}
		SetEyes(data);
		SetBody(data);
		SetMouth(data);
	}

	[Server]
	private void SetEyes(PlayerOrganData data)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void PlayerOrgans::SetEyes(PlayerOrganData)' called when server was not active");
		}
		else
		{
			RpcSetEyes(data.leftEye, data.rightEye);
		}
	}

	[ClientRpc]
	private void RpcSetEyes(bool leftEye, bool rightEye)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(leftEye);
		writer.WriteBool(rightEye);
		SendRPCInternal("System.Void PlayerOrgans::RpcSetEyes(System.Boolean,System.Boolean)", 1242183557, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	private void SetBody(PlayerOrganData data)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void PlayerOrgans::SetBody(PlayerOrganData)' called when server was not active");
		}
		else
		{
			RpcSetBody(data.body);
		}
	}

	[ClientRpc]
	private void RpcSetBody(bool isEnabled)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isEnabled);
		SendRPCInternal("System.Void PlayerOrgans::RpcSetBody(System.Boolean)", 876516184, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	private void SetMouth(PlayerOrganData data)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void PlayerOrgans::SetMouth(PlayerOrganData)' called when server was not active");
			return;
		}
		RpcSetMouth(data.mouth);
		if (TryGetComponent<PlayerVoiceFX>(out var component))
		{
			component.RpcSetNoMouthFX(!data.mouth);
		}
	}

	[ClientRpc]
	private void RpcSetMouth(bool isEnabled)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isEnabled);
		SendRPCInternal("System.Void PlayerOrgans::RpcSetMouth(System.Boolean)", 2028909947, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcSetEyes__Boolean__Boolean(bool leftEye, bool rightEye)
	{
		if (_localLeftEye != leftEye || _localRightEye != rightEye)
		{
			_localLeftEye = leftEye;
			_localRightEye = rightEye;
			leftEyeModel.SetActive(leftEye);
			rightEyeModel.SetActive(rightEye);
			if (base.isLocalPlayer)
			{
				_pe.ToggleEye(isRightEye: false, leftEye);
				_pe.ToggleEye(isRightEye: true, rightEye);
			}
		}
	}

	protected static void InvokeUserCode_RpcSetEyes__Boolean__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetEyes called on server.");
		}
		else
		{
			((PlayerOrgans)obj).UserCode_RpcSetEyes__Boolean__Boolean(reader.ReadBool(), reader.ReadBool());
		}
	}

	protected void UserCode_RpcSetBody__Boolean(bool isEnabled)
	{
		if (_localBody == isEnabled)
		{
			return;
		}
		_localBody = isEnabled;
		bodyModel.SetActive(isEnabled);
		if (base.isLocalPlayer)
		{
			_pc.NetworkhasBody = isEnabled;
			_pc.State = ((!isEnabled) ? PlayerController.PlayerState.Ragdoll : PlayerController.PlayerState.Free);
			if (isEnabled)
			{
				base.transform.DOMove(base.transform.position, 0.5f);
			}
			_pc.head.transform.DOLocalMove(isEnabled ? (Vector3.up * _ps.headHeight) : Vector3.zero, 0.5f);
		}
		_cd.angularDrag = (isEnabled ? new Vector3(0.5f, 20f, 0.5f) : new Vector3(5f, 20f, 5f));
		_rb.centerOfMass = (isEnabled ? (Vector3.up * 0.9f) : Vector3.zero);
	}

	protected static void InvokeUserCode_RpcSetBody__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetBody called on server.");
		}
		else
		{
			((PlayerOrgans)obj).UserCode_RpcSetBody__Boolean(reader.ReadBool());
		}
	}

	protected void UserCode_RpcSetMouth__Boolean(bool isEnabled)
	{
		if (_localMouth != isEnabled)
		{
			_localMouth = isEnabled;
			mouthModel.GetComponent<MeshRenderer>().enabled = isEnabled;
			if (base.isLocalPlayer)
			{
				GetComponent<PlayerMouth>().enabled = isEnabled;
			}
		}
	}

	protected static void InvokeUserCode_RpcSetMouth__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetMouth called on server.");
		}
		else
		{
			((PlayerOrgans)obj).UserCode_RpcSetMouth__Boolean(reader.ReadBool());
		}
	}

	static PlayerOrgans()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(PlayerOrgans), "System.Void PlayerOrgans::RpcSetEyes(System.Boolean,System.Boolean)", InvokeUserCode_RpcSetEyes__Boolean__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(PlayerOrgans), "System.Void PlayerOrgans::RpcSetBody(System.Boolean)", InvokeUserCode_RpcSetBody__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(PlayerOrgans), "System.Void PlayerOrgans::RpcSetMouth(System.Boolean)", InvokeUserCode_RpcSetMouth__Boolean);
	}
}

}
