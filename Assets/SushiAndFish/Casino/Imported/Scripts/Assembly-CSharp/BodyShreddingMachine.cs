namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::DG.Tweening;
using SushiAndFish.Casino.Original.Extensions;
using global::FMODUnity;
using global::JetBrains.Annotations;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::MoreMountains.Feedbacks;
using global::TMPro;
using global::UnityEngine;

public class BodyShreddingMachine : NetworkBehaviour
{
	[Header("Settings")]
	[SerializeField]
	[Range(0f, 1f)]
	private float spawnChance = 0.5f;

	[SerializeField]
	private float processDuration = 0.1f;

	[SerializeField]
	private Color buttonRemoveColor;

	[SerializeField]
	private Color buttonAddColor;

	[Header("References")]
	[SerializeField]
	private Collider checkCollider;

	[SerializeField]
	private TextMeshPro priceTag;

	[SerializeField]
	private TextMeshPro[] modeTexts;

	[SerializeField]
	private NetworkAnimator animator;

	[SerializeField]
	private MMWiggle wiggle;

	[SerializeField]
	private Transform lidTransform;

	[SerializeField]
	private MeshRenderer[] buttonMeshes;

	[SerializeField]
	private Transform[] leverTransforms;

	[Header("SFX")]
	[SerializeField]
	private EventReference bodyShredSfx;

	[SerializeField]
	private EventReference eyeShredSfx;

	[SerializeField]
	private EventReference mouthShredSfx;

	[SerializeField]
	private EventReference leverSwitchSfx;

	[SerializeField]
	private EventReference doorOpenSfx;

	[SerializeField]
	private EventReference doorCloseSfx;

	[SerializeField]
	private SFXComponent buyBackSfx;

	private LobbySettings _ls;

	private GameSettings _gs;

	private int _bodyPrice;

	private int _eyePrice;

	private int _mouthPrice;

	private bool _isLidOpen;

	private bool _isLeverBuy;

	private bool _isProcessing;

	private void Awake()
	{
		_ls = SushiAndFish.Casino.Original.CasinoResources.Load<LobbySettings>("LobbySettings");
		_gs = SushiAndFish.Casino.Original.CasinoResources.Load<GameSettings>("GameSettings");
	}

	private void Start()
	{
		if (base.isServer && GetSeededRandom().NextDouble() >= (double)spawnChance)
		{
			NetworkServer.Destroy(base.gameObject);
		}
	}

	public void OnQuotaChanged()
	{
		ServerSetPrices();
	}

	public override void OnStartClient()
	{
		base.OnStartClient();
		CmdSetPrices();
	}

	[Command(requiresAuthority = false)]
	private void CmdSetPrices()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendCommandInternal("System.Void BodyShreddingMachine::CmdSetPrices()", -19453666, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	private void ServerSetPrices()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void BodyShreddingMachine::ServerSetPrices()' called when server was not active");
			return;
		}
		GameSettings.CasinoFloorData currentFloorData = _gs.GetCurrentFloorData();
		_eyePrice = Mathf.RoundToInt(currentFloorData.shreddingEyePrice * GetPriceMultiplier());
		_mouthPrice = Mathf.RoundToInt(currentFloorData.shreddingMouthPrice * GetPriceMultiplier());
		_bodyPrice = Mathf.RoundToInt(currentFloorData.shreddingBodyPrice * GetPriceMultiplier());
		RpcSetPrices(_bodyPrice, _eyePrice, _mouthPrice);
	}

	[ClientRpc]
	private void RpcSetPrices(int bodyPrice, int eyePrice, int mouthPrice)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(bodyPrice);
		writer.WriteVarInt(eyePrice);
		writer.WriteVarInt(mouthPrice);
		SendRPCInternal("System.Void BodyShreddingMachine::RpcSetPrices(System.Int32,System.Int32,System.Int32)", 801687128, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public void ToggleLid()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void BodyShreddingMachine::ToggleLid()' called when server was not active");
			return;
		}
		_isLidOpen = !_isLidOpen;
		RpcToggleLid(_isLidOpen);
	}

	[ClientRpc]
	private void RpcToggleLid(bool isOpen)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isOpen);
		SendRPCInternal("System.Void BodyShreddingMachine::RpcToggleLid(System.Boolean)", -1431490903, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public void ServerToggleLever()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void BodyShreddingMachine::ServerToggleLever()' called when server was not active");
			return;
		}
		_isLeverBuy = !_isLeverBuy;
		RpcToggleLever(_isLeverBuy);
	}

	[ClientRpc]
	private void RpcToggleLever(bool isBuy)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isBuy);
		SendRPCInternal("System.Void BodyShreddingMachine::RpcToggleLever(System.Boolean)", 408643580, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public void OnEyeButtonClicked()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void BodyShreddingMachine::OnEyeButtonClicked()' called when server was not active");
		}
		else
		{
			if (_isLidOpen || _isProcessing)
			{
				return;
			}
			PlayerOrgans playersOrgans = GetPlayersOrgans();
			if (!playersOrgans)
			{
				return;
			}
			PlayerOrganData playerOrganData = NetworkSingleton<OrganManager>.Instance.OrganData[playersOrgans.connectionToClient.connectionId];
			if (playerOrganData == null)
			{
				return;
			}
			if (_isLeverBuy)
			{
				if (TryBuyEyeBack(playersOrgans, playerOrganData))
				{
					StartCoroutine(ProcessRoutine());
				}
			}
			else if (TryShredEye(playersOrgans, playerOrganData))
			{
				StartCoroutine(ProcessRoutine());
			}
		}
	}

	[Server]
	private bool TryShredEye(PlayerOrgans po, PlayerOrganData data)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Boolean BodyShreddingMachine::TryShredEye(PlayerOrgans,PlayerOrganData)' called when server was not active");
			return default;
		}
		if (!data.leftEye || !data.rightEye)
		{
			return false;
		}
		if (UnityEngine.Random.value > 0.5f)
		{
			NetworkSingleton<OrganManager>.Instance.ServerToggleOrgan(po, OrganType.RightEye, isEnabled: false);
		}
		else
		{
			NetworkSingleton<OrganManager>.Instance.ServerToggleOrgan(po, OrganType.LeftEye, isEnabled: false);
		}
		NetworkSingleton<MoneyManager>.Instance.TryChangeTicketBalance(_eyePrice);
		RpcOnEyeShredded();
		return true;
	}

	[Server]
	private bool TryBuyEyeBack(PlayerOrgans po, PlayerOrganData data)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Boolean BodyShreddingMachine::TryBuyEyeBack(PlayerOrgans,PlayerOrganData)' called when server was not active");
			return default;
		}
		if (data.leftEye && data.rightEye)
		{
			return false;
		}
		if (!NetworkSingleton<MoneyManager>.Instance.TryChangeTicketBalance(-Mathf.RoundToInt(_eyePrice)))
		{
			return false;
		}
		if (!data.leftEye)
		{
			NetworkSingleton<OrganManager>.Instance.ServerToggleOrgan(po, OrganType.LeftEye, isEnabled: true);
		}
		else if (!data.rightEye)
		{
			NetworkSingleton<OrganManager>.Instance.ServerToggleOrgan(po, OrganType.RightEye, isEnabled: true);
		}
		buyBackSfx.RpcPlayOneShotWith3DPos();
		return true;
	}

	[Server]
	public void OnMouthButtonClicked()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void BodyShreddingMachine::OnMouthButtonClicked()' called when server was not active");
		}
		else
		{
			if (_isLidOpen || _isProcessing)
			{
				return;
			}
			PlayerOrgans playersOrgans = GetPlayersOrgans();
			if (!playersOrgans)
			{
				return;
			}
			PlayerOrganData playerOrganData = NetworkSingleton<OrganManager>.Instance.OrganData[playersOrgans.connectionToClient.connectionId];
			if (playerOrganData == null)
			{
				return;
			}
			if (_isLeverBuy)
			{
				if (TryBuyMouthBack(playersOrgans, playerOrganData))
				{
					StartCoroutine(ProcessRoutine());
				}
			}
			else if (TryShredMouth(playersOrgans, playerOrganData))
			{
				StartCoroutine(ProcessRoutine());
			}
		}
	}

	[Server]
	private bool TryShredMouth(PlayerOrgans po, PlayerOrganData data)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Boolean BodyShreddingMachine::TryShredMouth(PlayerOrgans,PlayerOrganData)' called when server was not active");
			return default;
		}
		if (!data.mouth)
		{
			return false;
		}
		NetworkSingleton<OrganManager>.Instance.ServerToggleOrgan(po, OrganType.Mouth, isEnabled: false);
		NetworkSingleton<MoneyManager>.Instance.TryChangeTicketBalance(_mouthPrice);
		RpcOnMouthShredded();
		return true;
	}

	[Server]
	private bool TryBuyMouthBack(PlayerOrgans po, PlayerOrganData data)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Boolean BodyShreddingMachine::TryBuyMouthBack(PlayerOrgans,PlayerOrganData)' called when server was not active");
			return default;
		}
		if (data.mouth)
		{
			return false;
		}
		if (!NetworkSingleton<MoneyManager>.Instance.TryChangeTicketBalance(-Mathf.RoundToInt(_mouthPrice)))
		{
			return false;
		}
		NetworkSingleton<OrganManager>.Instance.ServerToggleOrgan(po, OrganType.Mouth, isEnabled: true);
		buyBackSfx.RpcPlayOneShotWith3DPos();
		return true;
	}

	[Server]
	public void OnBodyButtonClicked()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void BodyShreddingMachine::OnBodyButtonClicked()' called when server was not active");
		}
		else
		{
			if (_isLidOpen || _isProcessing)
			{
				return;
			}
			PlayerOrgans playersOrgans = GetPlayersOrgans();
			if (!playersOrgans)
			{
				return;
			}
			PlayerOrganData playerOrganData = NetworkSingleton<OrganManager>.Instance.OrganData[playersOrgans.connectionToClient.connectionId];
			if (playerOrganData == null)
			{
				return;
			}
			if (_isLeverBuy)
			{
				if (TryBuyBodyBack(playersOrgans, playerOrganData))
				{
					StartCoroutine(ProcessRoutine());
				}
			}
			else if (TryShredBody(playersOrgans, playerOrganData))
			{
				StartCoroutine(ProcessRoutine());
			}
		}
	}

	[Server]
	private bool TryShredBody(PlayerOrgans po, PlayerOrganData data)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Boolean BodyShreddingMachine::TryShredBody(PlayerOrgans,PlayerOrganData)' called when server was not active");
			return default;
		}
		if (!data.body)
		{
			return false;
		}
		NetworkSingleton<OrganManager>.Instance.ServerToggleOrgan(po, OrganType.Body, isEnabled: false);
		NetworkSingleton<MoneyManager>.Instance.TryChangeTicketBalance(_bodyPrice);
		RpcOnBodyShredded();
		return true;
	}

	[Server]
	private bool TryBuyBodyBack(PlayerOrgans po, PlayerOrganData data)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Boolean BodyShreddingMachine::TryBuyBodyBack(PlayerOrgans,PlayerOrganData)' called when server was not active");
			return default;
		}
		if (data.body)
		{
			return false;
		}
		if (!NetworkSingleton<MoneyManager>.Instance.TryChangeTicketBalance(-Mathf.RoundToInt(_bodyPrice)))
		{
			return false;
		}
		NetworkSingleton<OrganManager>.Instance.ServerToggleOrgan(po, OrganType.Body, isEnabled: true);
		buyBackSfx.RpcPlayOneShotWith3DPos();
		return true;
	}

	private IEnumerator ProcessRoutine()
	{
		_isProcessing = true;
		animator.SetTrigger("Shred");
		RpcOnProcess();
		yield return new WaitForSeconds(processDuration);
		_isProcessing = false;
	}

	[ClientRpc]
	private void RpcOnEyeShredded()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void BodyShreddingMachine::RpcOnEyeShredded()", 923359576, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcOnMouthShredded()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void BodyShreddingMachine::RpcOnMouthShredded()", 1991753518, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcOnBodyShredded()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void BodyShreddingMachine::RpcOnBodyShredded()", -125625849, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcOnProcess()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void BodyShreddingMachine::RpcOnProcess()", -1087308049, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[CanBeNull]
	private PlayerOrgans GetPlayersOrgans()
	{
		PlayerOrgans result = null;
		float num = float.MaxValue;
		Vector3 center = checkCollider.bounds.center;
		foreach (PlayerReferences player in MonoSingleton<LocalManager>.Instance.players)
		{
			Vector3 position = player.transform.position;
			if (checkCollider.bounds.Contains(position))
			{
				float sqrMagnitude = (position - center).sqrMagnitude;
				if (sqrMagnitude < num)
				{
					num = sqrMagnitude;
					result = player.organs;
				}
			}
		}
		return result;
	}

	private float GetPriceMultiplier()
	{
		_ = _ls.currentPlayerCount;
		return 1f;
	}

	private global::System.Random GetSeededRandom()
	{
		if (NetworkSingleton<SeededRandomManager>.Instance == null || NetworkSingleton<GameManager>.Instance == null)
		{
			return new global::System.Random(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
		}
		int currentSeed = NetworkSingleton<SeededRandomManager>.Instance.CurrentSeed;
		int daysPassed = NetworkSingleton<GameManager>.Instance.daysPassed;
		return new global::System.Random(currentSeed * 31 + daysPassed);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_CmdSetPrices()
	{
		ServerSetPrices();
	}

	protected static void InvokeUserCode_CmdSetPrices(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdSetPrices called on client.");
		}
		else
		{
			((BodyShreddingMachine)obj).UserCode_CmdSetPrices();
		}
	}

	protected void UserCode_RpcSetPrices__Int32__Int32__Int32(int bodyPrice, int eyePrice, int mouthPrice)
	{
		priceTag.text = "Eye\n" + $"<color=yellow>{eyePrice} Ticket</color>\n" + "Mouth\n" + $"<color=yellow>{mouthPrice} Ticket</color>\n" + "Body\n" + $"<color=yellow>{bodyPrice} Ticket</color>";
	}

	protected static void InvokeUserCode_RpcSetPrices__Int32__Int32__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetPrices called on server.");
		}
		else
		{
			((BodyShreddingMachine)obj).UserCode_RpcSetPrices__Int32__Int32__Int32(reader.ReadVarInt(), reader.ReadVarInt(), reader.ReadVarInt());
		}
	}

	protected void UserCode_RpcToggleLid__Boolean(bool isOpen)
	{
		lidTransform.DOLocalRotate(isOpen ? (Vector3.up * -120f) : Vector3.zero, 0.3f).SetEase(Ease.OutBounce);
		SFXManager.SFXOneShot(isOpen ? doorOpenSfx : doorCloseSfx, lidTransform.position);
	}

	protected static void InvokeUserCode_RpcToggleLid__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcToggleLid called on server.");
		}
		else
		{
			((BodyShreddingMachine)obj).UserCode_RpcToggleLid__Boolean(reader.ReadBool());
		}
	}

	protected void UserCode_RpcToggleLever__Boolean(bool isBuy)
	{
		Transform[] array = leverTransforms;
		foreach (Transform target in array)
		{
			target.DOKill();
			target.DOLocalRotate(isBuy ? (Vector3.forward * 75f) : Vector3.zero, 0.2f).SetEase(Ease.OutBounce);
		}
		MeshRenderer[] array2 = buttonMeshes;
		for (int i = 0; i < array2.Length; i++)
		{
			array2[i].material.SetColor("_Color", isBuy ? buttonAddColor : buttonRemoveColor);
		}
		SFXManager.SFXOneShot(leverSwitchSfx, leverTransforms[0].position);
		TextMeshPro[] array3 = modeTexts;
		foreach (TextMeshPro obj in array3)
		{
			obj.text = (isBuy ? "Buy" : "Sell");
			obj.color = (isBuy ? Color.forestGreen : Color.crimson);
		}
	}

	protected static void InvokeUserCode_RpcToggleLever__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcToggleLever called on server.");
		}
		else
		{
			((BodyShreddingMachine)obj).UserCode_RpcToggleLever__Boolean(reader.ReadBool());
		}
	}

	protected void UserCode_RpcOnEyeShredded()
	{
		SFXManager.SFXOneShot(eyeShredSfx, base.transform.position);
	}

	protected static void InvokeUserCode_RpcOnEyeShredded(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcOnEyeShredded called on server.");
		}
		else
		{
			((BodyShreddingMachine)obj).UserCode_RpcOnEyeShredded();
		}
	}

	protected void UserCode_RpcOnMouthShredded()
	{
		SFXManager.SFXOneShot(mouthShredSfx, base.transform.position);
	}

	protected static void InvokeUserCode_RpcOnMouthShredded(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcOnMouthShredded called on server.");
		}
		else
		{
			((BodyShreddingMachine)obj).UserCode_RpcOnMouthShredded();
		}
	}

	protected void UserCode_RpcOnBodyShredded()
	{
		SFXManager.SFXOneShot(bodyShredSfx, base.transform.position);
	}

	protected static void InvokeUserCode_RpcOnBodyShredded(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcOnBodyShredded called on server.");
		}
		else
		{
			((BodyShreddingMachine)obj).UserCode_RpcOnBodyShredded();
		}
	}

	protected void UserCode_RpcOnProcess()
	{
		wiggle.WiggleRotation(1f);
	}

	protected static void InvokeUserCode_RpcOnProcess(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcOnProcess called on server.");
		}
		else
		{
			((BodyShreddingMachine)obj).UserCode_RpcOnProcess();
		}
	}

	static BodyShreddingMachine()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(BodyShreddingMachine), "System.Void BodyShreddingMachine::CmdSetPrices()", InvokeUserCode_CmdSetPrices, requiresAuthority: false);
		RemoteProcedureCalls.RegisterRpc(typeof(BodyShreddingMachine), "System.Void BodyShreddingMachine::RpcSetPrices(System.Int32,System.Int32,System.Int32)", InvokeUserCode_RpcSetPrices__Int32__Int32__Int32);
		RemoteProcedureCalls.RegisterRpc(typeof(BodyShreddingMachine), "System.Void BodyShreddingMachine::RpcToggleLid(System.Boolean)", InvokeUserCode_RpcToggleLid__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(BodyShreddingMachine), "System.Void BodyShreddingMachine::RpcToggleLever(System.Boolean)", InvokeUserCode_RpcToggleLever__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(BodyShreddingMachine), "System.Void BodyShreddingMachine::RpcOnEyeShredded()", InvokeUserCode_RpcOnEyeShredded);
		RemoteProcedureCalls.RegisterRpc(typeof(BodyShreddingMachine), "System.Void BodyShreddingMachine::RpcOnMouthShredded()", InvokeUserCode_RpcOnMouthShredded);
		RemoteProcedureCalls.RegisterRpc(typeof(BodyShreddingMachine), "System.Void BodyShreddingMachine::RpcOnBodyShredded()", InvokeUserCode_RpcOnBodyShredded);
		RemoteProcedureCalls.RegisterRpc(typeof(BodyShreddingMachine), "System.Void BodyShreddingMachine::RpcOnProcess()", InvokeUserCode_RpcOnProcess);
	}
}

}
