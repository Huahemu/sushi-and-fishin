namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::DG.Tweening;
using SushiAndFish.Casino.Original.Extensions;
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::TMPro;
using global::UnityEngine;
using global::UnityEngine.UI;

public class Coordinator : ConsumableItem
{
	[Header("References")]
	[SerializeField]
	private Camera coordinatorCamera;

	[SerializeField]
	private RawImage coordinatorRenderTarget;

	[SerializeField]
	private TextMeshPro bankTotalText;

	[SerializeField]
	private TextMeshPro multiplierText;

	[SerializeField]
	private TextMeshPro potentialWinningText;

	[SerializeField]
	private Transform beamTransform;

	[SerializeField]
	private MeshRenderer flashLight;

	[SerializeField]
	private MeshRenderer renderingIndicator;

	[SerializeField]
	private List<MeshRenderer> chargeRenderers;

	[SerializeField]
	private NetworkAnimator animator;

	[Header("Settings")]
	[SerializeField]
	private float range;

	[SerializeField]
	private double directBankMultiplier;

	[SerializeField]
	private double[] multipliers;

	[SerializeField]
	private int cameraFpsLimit = 30;

	[SerializeField]
	private float throwThreshold = 5f;

	[SerializeField]
	private float shatterThreshold = 0.5f;

	[Header("SFX")]
	[SerializeField]
	private SFXLoopComponent sFXLoopComponent;

	[SerializeField]
	private EventReference[] oneShotSFX;

	private long _bankTotal;

	private int _currentCharge;

	private PlayerProfile _lastHolder;

	private bool _isActive;

	private bool _isCameraRendering;

	private float _lastCameraRenderTime;

	private bool _isBreakable;

	private Coroutine _setUnbreakableRoutine;

	private RenderTexture _renderTexture;

	private void Start()
	{
		_renderTexture = new RenderTexture(600, 500, 24);
		coordinatorCamera.targetTexture = _renderTexture;
		coordinatorRenderTarget.texture = _renderTexture;
	}

	protected override void OnPickedUp(PlayerInventory playerInventory)
	{
		base.OnPickedUp(playerInventory);
		_lastHolder = playerInventory.GetComponent<PlayerProfile>();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (coordinatorCamera != null)
		{
			coordinatorCamera.targetTexture = null;
		}
		if (_renderTexture != null)
		{
			_renderTexture.Release();
		}
	}

	[Server]
	public override void ServerTeleport(Vector3 position)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Coordinator::ServerTeleport(UnityEngine.Vector3)' called when server was not active");
		}
		else if (!base.NetworkHolder)
		{
			_isBreakable = false;
			Rb.Teleport(position, resetVelocity: true);
		}
	}

	private IEnumerator SetUnbreakableRoutine()
	{
		yield return new WaitForSeconds(3f);
		_isBreakable = true;
	}

	private void OnCollisionEnter(Collision collision)
	{
		if (!base.isServer || (bool)base.NetworkHolder || !_isBreakable || collision.impulse.magnitude < shatterThreshold)
		{
			return;
		}
		if (NetworkSingleton<GameManager>.Instance.state == GameState.Game)
		{
			if (_bankTotal <= 0)
			{
				return;
			}
			Payout();
		}
		sFXLoopComponent.RpcLoopSFX(play: false);
		RpcPlaySFX(2);
		DestroyItem();
	}

	public override void ServerThrow(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity)
	{
		base.ServerThrow(position, rotation, velocity, angularVelocity);
		if (velocity.magnitude < throwThreshold)
		{
			_isBreakable = false;
			return;
		}
		_isBreakable = true;
		if (_setUnbreakableRoutine != null)
		{
			StopCoroutine(_setUnbreakableRoutine);
		}
		_setUnbreakableRoutine = StartCoroutine(SetUnbreakableRoutine());
	}

	private void Update()
	{
		if (_isCameraRendering)
		{
			float num = 1f / (float)cameraFpsLimit;
			if (Time.time >= _lastCameraRenderTime + num)
			{
				_lastCameraRenderTime += num;
				if(CasinoRuntimeContext.Active) SushiAndFish.Casino.CasinoHostRepair.RepairOffscreenCamera(coordinatorCamera);
				coordinatorCamera.Render();
			}
		}
	}

	protected override void SubscribeToEvents(bool isSubscribed)
	{
		base.SubscribeToEvents(isSubscribed);
		if (isSubscribed)
		{
			NetworkSingleton<GameResultsManager>.Instance.OnResultRegistered += OnResultRegistered;
			InputEvents.OnZoomEvent = (Action<bool>)Delegate.Combine(InputEvents.OnZoomEvent, new Action<bool>(OnZoomEvent));
		}
		else
		{
			NetworkSingleton<GameResultsManager>.Instance.OnResultRegistered -= OnResultRegistered;
			InputEvents.OnZoomEvent = (Action<bool>)Delegate.Remove(InputEvents.OnZoomEvent, new Action<bool>(OnZoomEvent));
		}
	}

	private void OnZoomEvent(bool isPressed)
	{
		if ((bool)base.NetworkHolder && base.NetworkHolder.isLocalPlayer && !isInPocket)
		{
			ZoomToCamera(isPressed);
			CmdOnZoomEvent(isPressed);
		}
	}

	[Command(requiresAuthority = false)]
	private void CmdOnZoomEvent(bool isPressed)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isPressed);
		SendCommandInternal("System.Void Coordinator::CmdOnZoomEvent(System.Boolean)", 1044819190, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcOnZoomEvent(bool isPressed)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isPressed);
		SendRPCInternal("System.Void Coordinator::RpcOnZoomEvent(System.Boolean)", 2034253727, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void ZoomToCamera(bool isPressed)
	{
		onHandFb.StopFeedbacks();
		modelTransform.DOKill();
		if (!isPressed)
		{
			modelTransform.DOLocalMove(Vector3.zero, 0.2f).SetEase(Ease.OutQuad);
		}
		else if (base.NetworkHolder.isLocalPlayer)
		{
			modelTransform.DOLocalMove(new Vector3(0f, 0.4f, -0.35f), 0.2f).SetEase(Ease.OutQuad);
		}
		else
		{
			modelTransform.DOLocalMove(new Vector3(0f, 0.4f, -0.15f), 0.2f).SetEase(Ease.OutQuad);
		}
	}

	private void OnResultRegistered(long bet, long payout, PlayerProfile playerProfile, CasinoGameType gameType, Vector3 position, bool hadTipsyFortune, bool hadInspiringMelody, bool hadImmunity, Dictionary<string, object> gameSpecificData)
	{
		if (!base.isServer || !base.NetworkHolder || !_isActive || _lastHolder == playerProfile)
		{
			return;
		}
		Vector3 position2 = playerProfile.GetComponent<PlayerController>().head.transform.position;
		if (!((position2 - coordinatorCamera.transform.position).sqrMagnitude > range * range) && IsInCameraFrustum(position2))
		{
			if (payout > bet && _currentCharge < chargeRenderers.Count)
			{
				IncreaseWinning(payout - bet);
			}
			else if (payout < bet)
			{
				ResetWinning();
			}
			RpcSetCharges(_currentCharge);
			RpcSetText(_bankTotal, multipliers[_currentCharge]);
			RpcResetCamera();
		}
	}

	private void IncreaseWinning(long profit)
	{
		_bankTotal += (long)Math.Round((double)profit * directBankMultiplier * (double)NetworkSingleton<UpgradeManager>.Instance.GetUpgradeData(_lastHolder.steamId, PlayerUpgradeType.Stakeholder));
		_currentCharge++;
		animator.SetTrigger("Shoot");
		RpcPlaySFX(0);
	}

	private void ResetWinning()
	{
		_bankTotal = 0L;
		_currentCharge = 0;
		animator.SetTrigger("Shoot");
		RpcPlaySFX(1);
	}

	protected override void OnUseItem(bool isPressed)
	{
		_isActive = isPressed;
		sFXLoopComponent.LoopSFX(isPressed);
		renderingIndicator.material.color = (isPressed ? Color.green : Color.red);
		flashLight.material.SetColor("_EmissionColor", isPressed ? (Color.white * 5f) : Color.black);
		beamTransform.DOScale(isPressed ? (Vector3.one * range) : Vector3.zero, 0.2f).SetEase(Ease.Linear);
		_isCameraRendering = isPressed;
	}

	private void Payout()
	{
		long num = (long)Math.Round((double)_bankTotal * multipliers[_currentCharge]);
		if (num > 0 && (bool)_lastHolder)
		{
			NetworkSingleton<MoneyManager>.Instance.TryChangeBalance(num, _lastHolder, ChangeType.Item);
		}
	}

	protected override void OnDropped(PlayerInventory playerInventory)
	{
		base.OnDropped(playerInventory);
		_isActive = false;
		RpcResetCamera();
	}

	[ClientRpc]
	private void RpcSetText(long bankTotal, double mult)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarLong(bankTotal);
		writer.WriteDouble(mult);
		SendRPCInternal("System.Void Coordinator::RpcSetText(System.Int64,System.Double)", -641863677, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcSetCharges(int charge)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(charge);
		SendRPCInternal("System.Void Coordinator::RpcSetCharges(System.Int32)", 321818366, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcResetCamera()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void Coordinator::RpcResetCamera()", 1066267376, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcPlaySFX(int i)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(i);
		SendRPCInternal("System.Void Coordinator::RpcPlaySFX(System.Int32)", -685521556, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private bool IsInCameraFrustum(Vector3 worldPos)
	{
		Vector3 vector = coordinatorCamera.WorldToViewportPoint(worldPos);
		float x = vector.x;
		if (x >= 0f && x <= 1f)
		{
			x = vector.y;
			if (x >= 0f && x <= 1f)
			{
				return vector.z > 0f;
			}
		}
		return false;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_CmdOnZoomEvent__Boolean(bool isPressed)
	{
		RpcOnZoomEvent(isPressed);
	}

	protected static void InvokeUserCode_CmdOnZoomEvent__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdOnZoomEvent called on client.");
		}
		else
		{
			((Coordinator)obj).UserCode_CmdOnZoomEvent__Boolean(reader.ReadBool());
		}
	}

	protected void UserCode_RpcOnZoomEvent__Boolean(bool isPressed)
	{
		if (!base.NetworkHolder || !base.NetworkHolder.isLocalPlayer)
		{
			ZoomToCamera(isPressed);
		}
	}

	protected static void InvokeUserCode_RpcOnZoomEvent__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcOnZoomEvent called on server.");
		}
		else
		{
			((Coordinator)obj).UserCode_RpcOnZoomEvent__Boolean(reader.ReadBool());
		}
	}

	protected void UserCode_RpcSetText__Int64__Double(long bankTotal, double mult)
	{
		bankTotalText.text = MoneyFormatter.FormatWithDollar(bankTotal);
		multiplierText.text = mult.ToString("0.#") + "x";
		potentialWinningText.text = MoneyFormatter.FormatWithDollar((long)Math.Round((double)bankTotal * mult));
	}

	protected static void InvokeUserCode_RpcSetText__Int64__Double(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetText called on server.");
		}
		else
		{
			((Coordinator)obj).UserCode_RpcSetText__Int64__Double(reader.ReadVarLong(), reader.ReadDouble());
		}
	}

	protected void UserCode_RpcSetCharges__Int32(int charge)
	{
		for (int i = 0; i < chargeRenderers.Count; i++)
		{
			if (i < charge)
			{
				chargeRenderers[i].material.SetColor("_EmissionColor", Color.gold * 2f);
			}
			else
			{
				chargeRenderers[i].material.SetColor("_EmissionColor", Color.black);
			}
		}
	}

	protected static void InvokeUserCode_RpcSetCharges__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetCharges called on server.");
		}
		else
		{
			((Coordinator)obj).UserCode_RpcSetCharges__Int32(reader.ReadVarInt());
		}
	}

	protected void UserCode_RpcResetCamera()
	{
		sFXLoopComponent.LoopSFX(play: false);
		_isCameraRendering = false;
		_isActive = false;
		renderingIndicator.material.color = Color.red;
		flashLight.material.SetColor("_EmissionColor", Color.black);
		modelTransform.DOKill();
		modelTransform.DOLocalMove(Vector3.zero, 0.2f).SetEase(Ease.OutQuad);
		beamTransform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.Linear);
	}

	protected static void InvokeUserCode_RpcResetCamera(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcResetCamera called on server.");
		}
		else
		{
			((Coordinator)obj).UserCode_RpcResetCamera();
		}
	}

	protected void UserCode_RpcPlaySFX__Int32(int i)
	{
		SFXManager.SFXOneShot3DAttached(oneShotSFX[i], base.gameObject, non_rigidbody_velocity: true);
	}

	protected static void InvokeUserCode_RpcPlaySFX__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcPlaySFX called on server.");
		}
		else
		{
			((Coordinator)obj).UserCode_RpcPlaySFX__Int32(reader.ReadVarInt());
		}
	}

	static Coordinator()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(Coordinator), "System.Void Coordinator::CmdOnZoomEvent(System.Boolean)", InvokeUserCode_CmdOnZoomEvent__Boolean, requiresAuthority: false);
		RemoteProcedureCalls.RegisterRpc(typeof(Coordinator), "System.Void Coordinator::RpcOnZoomEvent(System.Boolean)", InvokeUserCode_RpcOnZoomEvent__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(Coordinator), "System.Void Coordinator::RpcSetText(System.Int64,System.Double)", InvokeUserCode_RpcSetText__Int64__Double);
		RemoteProcedureCalls.RegisterRpc(typeof(Coordinator), "System.Void Coordinator::RpcSetCharges(System.Int32)", InvokeUserCode_RpcSetCharges__Int32);
		RemoteProcedureCalls.RegisterRpc(typeof(Coordinator), "System.Void Coordinator::RpcResetCamera()", InvokeUserCode_RpcResetCamera);
		RemoteProcedureCalls.RegisterRpc(typeof(Coordinator), "System.Void Coordinator::RpcPlaySFX(System.Int32)", InvokeUserCode_RpcPlaySFX__Int32);
	}
}

}
