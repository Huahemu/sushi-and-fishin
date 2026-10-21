namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using global::System.Runtime.InteropServices;
using global::DG.Tweening;
using SushiAndFish.Casino.Original.Extensions;
using global::FMOD.Studio;
using global::FMODUnity;
using global::JetBrains.Annotations;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class Taser : ConsumableItem
{
	[Header("References")]
	[SerializeField]
	private Transform chargeBar;

	[SerializeField]
	private ParticleSystem lightningParticles;

	[SerializeField]
	private Animator anim;

	[Header("Settings")]
	[SerializeField]
	private float chargeTick = 0.5f;

	[SerializeField]
	private float chargeUsePerTick = 0.05f;

	[SerializeField]
	private float totalMultiplierIncrease = 4f;

	[SerializeField]
	private float raycastDistance = 2f;

	[SerializeField]
	private LayerMask rayMask;

	[Header("SFX")]
	[SerializeField]
	private EventReference taserShootSfx;

	[SerializeField]
	private EventReference taserInvalidSfx;

	private EventInstance _taserLoopInstance;

	[SerializeField]
	private EventReference destroySfx;

	private PlayerProfile _holderProfile;

	[SyncVar]
	private GameBase _targetGame;

	private Coroutine _setTargetGameRoutine;

	private readonly RaycastHit[] _raycastHits = new RaycastHit[8];

	private float _currentCharge = 1f;

	private float _lastChargeTime;

	protected NetworkBehaviourSyncVar ____targetGameNetId;

	public GameBase Network_targetGame
	{
		get
		{
			return GetSyncVarNetworkBehaviour(____targetGameNetId, ref _targetGame);
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter_NetworkBehaviour(value, ref _targetGame, 2uL, null, ref ____targetGameNetId);
		}
	}

	protected override void OnPickedUp(PlayerInventory playerInventory)
	{
		base.OnPickedUp(playerInventory);
		_holderProfile = playerInventory.GetComponent<PlayerProfile>();
	}

	protected override void OnDropped(PlayerInventory playerInventory)
	{
		base.OnDropped(playerInventory);
		RpcOnDropped();
	}

	[ClientRpc]
	private void RpcOnDropped()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void Taser::RpcOnDropped()", 519732880, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	protected override void OnUseItem(bool isPressed)
	{
		if ((bool)base.NetworkHolder && base.NetworkHolder.isLocalPlayer && !(_currentCharge <= 0f))
		{
			if (_setTargetGameRoutine != null)
			{
				StopCoroutine(_setTargetGameRoutine);
			}
			if (isPressed)
			{
				_setTargetGameRoutine = StartCoroutine(SetTargetGameRoutine());
			}
			else
			{
				SetTargetGame(null);
			}
			TaserFeedbacks(isPressed);
			CmdTaserFeedbacks(isPressed);
		}
	}

	private IEnumerator SetTargetGameRoutine()
	{
		while ((bool)base.NetworkHolder && !Network_targetGame)
		{
			TryFindTargetGame();
			yield return new WaitForSeconds(0.1f);
		}
	}

	private void TryFindTargetGame()
	{
		Camera mainCamera = MonoSingleton<LocalManager>.Instance.mainCamera;
		Vector3 position = mainCamera.transform.position;
		Vector3 forward = mainCamera.transform.forward;
		int num = Physics.RaycastNonAlloc(new Ray(position, forward), _raycastHits, raycastDistance, rayMask, QueryTriggerInteraction.Ignore);
		if (num <= 0)
		{
			return;
		}
		Keypad keypad = null;
		for (int i = 0; i < num; i++)
		{
			RaycastHit raycastHit = _raycastHits[i];
			if (raycastHit.transform.TryGetComponent<Keypad>(out var component) && !((base.transform.position - component.transform.position).sqrMagnitude > raycastDistance * raycastDistance))
			{
				keypad = component;
				break;
			}
		}
		if ((bool)keypad)
		{
			SetTargetGame(keypad.NetworkcasinoGame);
		}
	}

	private void SetTargetGame([CanBeNull] GameBase targetGame)
	{
		if (!(targetGame == Network_targetGame))
		{
			Network_targetGame = targetGame;
			bool flag = targetGame;
			UseTaser(flag);
			TaserLoopSfxParams(_currentCharge, flag);
			CmdSetTargetGame(Network_targetGame);
		}
	}

	[Command(requiresAuthority = false)]
	private void CmdSetTargetGame([CanBeNull] GameBase targetGame)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(targetGame);
		SendCommandInternal("System.Void Taser::CmdSetTargetGame(GameBase)", 1565143834, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcSetTargetGame([CanBeNull] GameBase targetGame)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(targetGame);
		SendRPCInternal("System.Void Taser::RpcSetTargetGame(GameBase)", -370932413, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcUseTaser(bool isApplied)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isApplied);
		SendRPCInternal("System.Void Taser::RpcUseTaser(System.Boolean)", -758977358, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void UseTaser(bool isApplied)
	{
		modelTransform.DOKill();
		onHandFb.StopFeedbacks();
		if (isApplied)
		{
			Vector3 targetPosition = Network_targetGame.keypadSpawnPoint.transform.position + Network_targetGame.keypadSpawnPoint.transform.forward * 0.4f + Network_targetGame.keypadSpawnPoint.transform.up * -0.2f + Network_targetGame.keypadSpawnPoint.transform.right * -0.2f;
			Quaternion targetRotation = Quaternion.LookRotation(-Network_targetGame.keypadSpawnPoint.transform.up, -Network_targetGame.keypadSpawnPoint.transform.forward);
			modelTransform.DOMove(targetPosition, 0.3f).SetEase(Ease.OutCubic).OnComplete(() =>
			{
				modelTransform.DOMove(targetPosition, 10f);
			});
			modelTransform.DORotateQuaternion(targetRotation, 0.3f).SetEase(Ease.OutCubic).OnComplete(() =>
			{
				modelTransform.DORotateQuaternion(targetRotation, 10f);
			});
		}
		else
		{
			modelTransform.DOLocalMove(Vector3.zero, 0.1f).SetEase(Ease.OutCubic);
			modelTransform.DOLocalRotate(Vector3.zero, 0.1f).SetEase(Ease.OutCubic);
		}
	}

	private void Update()
	{
		if (!base.NetworkHolder || !Network_targetGame)
		{
			return;
		}
		if (base.NetworkHolder.isLocalPlayer && (base.transform.position - Network_targetGame.keypadSpawnPoint.transform.position).sqrMagnitude > raycastDistance * raycastDistance)
		{
			SetTargetGame(null);
			if (_setTargetGameRoutine != null)
			{
				StopCoroutine(_setTargetGameRoutine);
			}
			_setTargetGameRoutine = StartCoroutine(SetTargetGameRoutine());
		}
		else if (base.isServer)
		{
			UseCharge();
		}
	}

	private void UseCharge()
	{
		if (!(Time.time - _lastChargeTime < chargeTick))
		{
			_lastChargeTime = Time.time;
			SetCurrentCharge(_currentCharge - chargeUsePerTick);
			RpcSetCurrentCharge(_currentCharge);
			Network_targetGame.MaxBetOverrideMultiplier += totalMultiplierIncrease * chargeUsePerTick * NetworkSingleton<UpgradeManager>.Instance.GetUpgradeData(_holderProfile.steamId, PlayerUpgradeType.Stakeholder);
			RpcTaserLoopSfxParams(_currentCharge, targetGame: true, callOnHolder: true);
			if (_currentCharge <= 0f)
			{
				Network_targetGame = null;
				RpcDestroySfx();
				DestroyItem();
			}
		}
	}

	[ClientRpc]
	private void RpcSetCurrentCharge(float charge)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteFloat(charge);
		SendRPCInternal("System.Void Taser::RpcSetCurrentCharge(System.Single)", -678428923, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void SetCurrentCharge(float charge)
	{
		_currentCharge = charge;
		chargeBar.transform.DOScaleY(charge, 0.2f).SetEase(Ease.OutCubic);
	}

	[Command(requiresAuthority = false)]
	private void CmdTaserFeedbacks(bool isPressed)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isPressed);
		SendCommandInternal("System.Void Taser::CmdTaserFeedbacks(System.Boolean)", -1693549802, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcTaserFeedbacks(bool isPressed)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isPressed);
		SendRPCInternal("System.Void Taser::RpcTaserFeedbacks(System.Boolean)", 1518651113, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void TaserFeedbacks(bool isPressed)
	{
		float endValue = (isPressed ? 1f : 0f);
		DOTween.To(() => anim.GetFloat("Blend"), (float x) =>
		{
			anim.SetFloat("Blend", x);
		}, endValue, 0.25f).SetEase(Ease.OutCubic);
		if (isPressed)
		{
			lightningParticles.Play();
		}
		else
		{
			lightningParticles.Stop();
		}
		if (isPressed)
		{
			SFXManager.SFXOneShot(taserInvalidSfx, base.transform.position);
		}
		TaserLoopSfx(isPressed);
	}

	private void TaserLoopSfx(bool play)
	{
		if (play)
		{
			if (_taserLoopInstance.isValid())
			{
				_taserLoopInstance.getPlaybackState(out var state);
				if (state == PLAYBACK_STATE.PLAYING)
				{
					return;
				}
			}
			_taserLoopInstance = RuntimeManager.CreateInstance(taserShootSfx);
			_taserLoopInstance.set3DAttributes(base.transform.position.To3DAttributes());
			RuntimeManager.AttachInstanceToGameObject(_taserLoopInstance, base.gameObject, nonRigidbodyVelocity: true);
			_taserLoopInstance.start();
		}
		else if (_taserLoopInstance.isValid())
		{
			_taserLoopInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
			_taserLoopInstance.release();
		}
	}

	[ClientRpc]
	private void RpcDestroySfx()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void Taser::RpcDestroySfx()", -1855344908, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcTaserLoopSfxParams(float charge, bool targetGame, bool callOnHolder)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteFloat(charge);
		writer.WriteBool(targetGame);
		writer.WriteBool(callOnHolder);
		SendRPCInternal("System.Void Taser::RpcTaserLoopSfxParams(System.Single,System.Boolean,System.Boolean)", -365983546, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void TaserLoopSfxParams(float charge, bool targetGame)
	{
		if (_taserLoopInstance.isValid())
		{
			_taserLoopInstance.getPlaybackState(out var state);
			if (state == PLAYBACK_STATE.PLAYING)
			{
				_taserLoopInstance.setPitch(1f + charge * 0.5f);
				_taserLoopInstance.setParameterByName("bool", targetGame ? 1f : 0f);
			}
		}
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcOnDropped()
	{
		Network_targetGame = null;
		modelTransform.DOKill();
		modelTransform.localPosition = Vector3.zero;
		modelTransform.localRotation = Quaternion.identity;
		TaserLoopSfx(play: false);
		anim.SetFloat("Blend", 0f);
		anim.Update(0f);
	}

	protected static void InvokeUserCode_RpcOnDropped(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcOnDropped called on server.");
		}
		else
		{
			((Taser)obj).UserCode_RpcOnDropped();
		}
	}

	protected void UserCode_CmdSetTargetGame__GameBase(GameBase targetGame)
	{
		Network_targetGame = targetGame;
		RpcSetTargetGame(targetGame);
		bool flag = targetGame;
		RpcUseTaser(flag);
		RpcTaserLoopSfxParams(_currentCharge, flag, callOnHolder: false);
	}

	protected static void InvokeUserCode_CmdSetTargetGame__GameBase(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdSetTargetGame called on client.");
		}
		else
		{
			((Taser)obj).UserCode_CmdSetTargetGame__GameBase(reader.ReadNetworkBehaviour<GameBase>());
		}
	}

	protected void UserCode_RpcSetTargetGame__GameBase(GameBase targetGame)
	{
		if ((bool)base.NetworkHolder && !base.NetworkHolder.isLocalPlayer)
		{
			Network_targetGame = targetGame;
		}
	}

	protected static void InvokeUserCode_RpcSetTargetGame__GameBase(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetTargetGame called on server.");
		}
		else
		{
			((Taser)obj).UserCode_RpcSetTargetGame__GameBase(reader.ReadNetworkBehaviour<GameBase>());
		}
	}

	protected void UserCode_RpcUseTaser__Boolean(bool isApplied)
	{
		if ((bool)base.NetworkHolder && !base.NetworkHolder.isLocalPlayer)
		{
			UseTaser(isApplied);
		}
	}

	protected static void InvokeUserCode_RpcUseTaser__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcUseTaser called on server.");
		}
		else
		{
			((Taser)obj).UserCode_RpcUseTaser__Boolean(reader.ReadBool());
		}
	}

	protected void UserCode_RpcSetCurrentCharge__Single(float charge)
	{
		if (!base.isServer)
		{
			SetCurrentCharge(charge);
		}
	}

	protected static void InvokeUserCode_RpcSetCurrentCharge__Single(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetCurrentCharge called on server.");
		}
		else
		{
			((Taser)obj).UserCode_RpcSetCurrentCharge__Single(reader.ReadFloat());
		}
	}

	protected void UserCode_CmdTaserFeedbacks__Boolean(bool isPressed)
	{
		RpcTaserFeedbacks(isPressed);
	}

	protected static void InvokeUserCode_CmdTaserFeedbacks__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdTaserFeedbacks called on client.");
		}
		else
		{
			((Taser)obj).UserCode_CmdTaserFeedbacks__Boolean(reader.ReadBool());
		}
	}

	protected void UserCode_RpcTaserFeedbacks__Boolean(bool isPressed)
	{
		if ((bool)base.NetworkHolder && !base.NetworkHolder.isLocalPlayer)
		{
			TaserFeedbacks(isPressed);
		}
	}

	protected static void InvokeUserCode_RpcTaserFeedbacks__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcTaserFeedbacks called on server.");
		}
		else
		{
			((Taser)obj).UserCode_RpcTaserFeedbacks__Boolean(reader.ReadBool());
		}
	}

	protected void UserCode_RpcDestroySfx()
	{
		SFXManager.SFXOneShot(destroySfx, base.transform.position);
	}

	protected static void InvokeUserCode_RpcDestroySfx(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcDestroySfx called on server.");
		}
		else
		{
			((Taser)obj).UserCode_RpcDestroySfx();
		}
	}

	protected void UserCode_RpcTaserLoopSfxParams__Single__Boolean__Boolean(float charge, bool targetGame, bool callOnHolder)
	{
		if ((bool)base.NetworkHolder && (callOnHolder || !base.NetworkHolder.isLocalPlayer))
		{
			TaserLoopSfxParams(charge, targetGame);
		}
	}

	protected static void InvokeUserCode_RpcTaserLoopSfxParams__Single__Boolean__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcTaserLoopSfxParams called on server.");
		}
		else
		{
			((Taser)obj).UserCode_RpcTaserLoopSfxParams__Single__Boolean__Boolean(reader.ReadFloat(), reader.ReadBool(), reader.ReadBool());
		}
	}

	static Taser()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(Taser), "System.Void Taser::CmdSetTargetGame(GameBase)", InvokeUserCode_CmdSetTargetGame__GameBase, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(Taser), "System.Void Taser::CmdTaserFeedbacks(System.Boolean)", InvokeUserCode_CmdTaserFeedbacks__Boolean, requiresAuthority: false);
		RemoteProcedureCalls.RegisterRpc(typeof(Taser), "System.Void Taser::RpcOnDropped()", InvokeUserCode_RpcOnDropped);
		RemoteProcedureCalls.RegisterRpc(typeof(Taser), "System.Void Taser::RpcSetTargetGame(GameBase)", InvokeUserCode_RpcSetTargetGame__GameBase);
		RemoteProcedureCalls.RegisterRpc(typeof(Taser), "System.Void Taser::RpcUseTaser(System.Boolean)", InvokeUserCode_RpcUseTaser__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(Taser), "System.Void Taser::RpcSetCurrentCharge(System.Single)", InvokeUserCode_RpcSetCurrentCharge__Single);
		RemoteProcedureCalls.RegisterRpc(typeof(Taser), "System.Void Taser::RpcTaserFeedbacks(System.Boolean)", InvokeUserCode_RpcTaserFeedbacks__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(Taser), "System.Void Taser::RpcDestroySfx()", InvokeUserCode_RpcDestroySfx);
		RemoteProcedureCalls.RegisterRpc(typeof(Taser), "System.Void Taser::RpcTaserLoopSfxParams(System.Single,System.Boolean,System.Boolean)", InvokeUserCode_RpcTaserLoopSfxParams__Single__Boolean__Boolean);
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			writer.WriteNetworkBehaviour(Network_targetGame);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 2L) != 0L)
		{
			writer.WriteNetworkBehaviour(Network_targetGame);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize_NetworkBehaviour(ref _targetGame, null, reader, ref ____targetGameNetId);
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 2L) != 0L)
		{
			GeneratedSyncVarDeserialize_NetworkBehaviour(ref _targetGame, null, reader, ref ____targetGameNetId);
		}
	}
}

}
