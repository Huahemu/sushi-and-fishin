namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using global::System.Runtime.InteropServices;
using global::DG.Tweening;
using SushiAndFish.Casino.Original.Extensions;
using global::JetBrains.Annotations;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class GoldenChip : ConsumableItem
{
	[SerializeField]
	private Animator anim;

	[Header("Settings")]
	[SerializeField]
	private float applyTime = 1f;

	[SerializeField]
	private float raycastDistance = 2f;

	[SerializeField]
	private LayerMask rayMask;

	[Header("SFX")]
	[SerializeField]
	private SFXLoopComponent activateSfx;

	[SyncVar]
	private GameBase _targetGame;

	private Coroutine _setTargetGameRoutine;

	private Coroutine _applyChipRoutine;

	private readonly RaycastHit[] _raycastHits = new RaycastHit[8];

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

	protected override void OnDropped(PlayerInventory playerInventory)
	{
		base.OnDropped(playerInventory);
		if (_applyChipRoutine != null)
		{
			StopCoroutine(_applyChipRoutine);
		}
		RpcOnDropped();
	}

	[ClientRpc]
	private void RpcOnDropped()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void GoldenChip::RpcOnDropped()", -1265912124, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	protected override void OnPickedUp(PlayerInventory playerInventory)
	{
		base.OnPickedUp(playerInventory);
		RpcOnPickedUp();
	}

	[ClientRpc]
	private void RpcOnPickedUp()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void GoldenChip::RpcOnPickedUp()", 358503685, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	protected override void OnUseItem(bool isPressed)
	{
		if ((bool)base.NetworkHolder && base.NetworkHolder.isLocalPlayer)
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
			if (raycastHit.transform.TryGetComponent<Keypad>(out var component) && !((base.transform.position - component.transform.position).sqrMagnitude > raycastDistance * raycastDistance) && !component.NetworkcasinoGame.isGoldenChipApplied)
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
			bool isApplied = targetGame;
			ApplyChip(isApplied);
			CmdSetTargetGame(Network_targetGame);
		}
	}

	[Command(requiresAuthority = false)]
	private void CmdSetTargetGame([CanBeNull] GameBase targetGame)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(targetGame);
		SendCommandInternal("System.Void GoldenChip::CmdSetTargetGame(GameBase)", 481031430, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcSetTargetGame([CanBeNull] GameBase targetGame)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(targetGame);
		SendRPCInternal("System.Void GoldenChip::RpcSetTargetGame(GameBase)", 2051719151, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcApplyChip(bool isApplied)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isApplied);
		SendRPCInternal("System.Void GoldenChip::RpcApplyChip(System.Boolean)", 1547888810, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void ApplyChip(bool isApplied)
	{
		if (base.isServer && _applyChipRoutine != null)
		{
			StopCoroutine(_applyChipRoutine);
		}
		modelTransform.DOKill();
		onHandFb.StopFeedbacks();
		if (isApplied)
		{
			anim.SetBool("IsApplying", value: true);
			activateSfx.LoopSFX(play: true);
			Vector3 targetPosition = Network_targetGame.keypadSpawnPoint.transform.position + Network_targetGame.keypadSpawnPoint.transform.forward * 0.4f + Network_targetGame.keypadSpawnPoint.transform.up * -0.2f + Network_targetGame.keypadSpawnPoint.transform.right * -0.2f;
			Quaternion targetRotation = Quaternion.LookRotation(-Network_targetGame.keypadSpawnPoint.transform.forward, Network_targetGame.keypadSpawnPoint.transform.up);
			modelTransform.DOMove(targetPosition, applyTime - 0.2f).SetEase(Ease.OutCubic).OnComplete(() =>
			{
				modelTransform.DOMove(targetPosition, 0.2f);
			});
			modelTransform.DORotateQuaternion(targetRotation, applyTime - 0.2f).SetEase(Ease.OutCubic).OnComplete(() =>
			{
				modelTransform.DORotateQuaternion(targetRotation, 0.2f);
			});
			if (base.isServer)
			{
				_applyChipRoutine = StartCoroutine(ApplyChipRoutine());
			}
		}
		else
		{
			anim.SetBool("IsApplying", value: false);
			activateSfx.LoopSFX(play: false);
			modelTransform.DOLocalMove(Vector3.zero, 0.1f).SetEase(Ease.OutCubic);
			modelTransform.DOLocalRotate(Vector3.zero, 0.1f).SetEase(Ease.OutCubic);
		}
	}

	private IEnumerator ApplyChipRoutine()
	{
		yield return new WaitForSeconds(applyTime);
		Network_targetGame.ApplyGoldenChip(NetworkSingleton<UpgradeManager>.Instance.GetUpgradeData(base.NetworkHolder.GetComponent<PlayerProfile>().steamId, PlayerUpgradeType.Stakeholder));
		DestroyItem();
	}

	private void Update()
	{
		if ((bool)base.NetworkHolder && (bool)Network_targetGame && base.NetworkHolder.isLocalPlayer && (Network_targetGame.isGoldenChipApplied || (base.transform.position - Network_targetGame.keypadSpawnPoint.transform.position).sqrMagnitude > raycastDistance * raycastDistance))
		{
			SetTargetGame(null);
			if (_setTargetGameRoutine != null)
			{
				StopCoroutine(_setTargetGameRoutine);
			}
			_setTargetGameRoutine = StartCoroutine(SetTargetGameRoutine());
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
		anim.SetBool("IsApplying", value: false);
		anim.Play("Default", 0, 0f);
		anim.Update(0f);
		activateSfx.LoopSFX(play: false);
	}

	protected static void InvokeUserCode_RpcOnDropped(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcOnDropped called on server.");
		}
		else
		{
			((GoldenChip)obj).UserCode_RpcOnDropped();
		}
	}

	protected void UserCode_RpcOnPickedUp()
	{
		anim.SetTrigger("PickUp");
	}

	protected static void InvokeUserCode_RpcOnPickedUp(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcOnPickedUp called on server.");
		}
		else
		{
			((GoldenChip)obj).UserCode_RpcOnPickedUp();
		}
	}

	protected void UserCode_CmdSetTargetGame__GameBase(GameBase targetGame)
	{
		Network_targetGame = targetGame;
		RpcSetTargetGame(targetGame);
		bool isApplied = targetGame;
		RpcApplyChip(isApplied);
	}

	protected static void InvokeUserCode_CmdSetTargetGame__GameBase(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdSetTargetGame called on client.");
		}
		else
		{
			((GoldenChip)obj).UserCode_CmdSetTargetGame__GameBase(reader.ReadNetworkBehaviour<GameBase>());
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
			((GoldenChip)obj).UserCode_RpcSetTargetGame__GameBase(reader.ReadNetworkBehaviour<GameBase>());
		}
	}

	protected void UserCode_RpcApplyChip__Boolean(bool isApplied)
	{
		if ((bool)base.NetworkHolder && !base.NetworkHolder.isLocalPlayer)
		{
			ApplyChip(isApplied);
		}
	}

	protected static void InvokeUserCode_RpcApplyChip__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcApplyChip called on server.");
		}
		else
		{
			((GoldenChip)obj).UserCode_RpcApplyChip__Boolean(reader.ReadBool());
		}
	}

	static GoldenChip()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(GoldenChip), "System.Void GoldenChip::CmdSetTargetGame(GameBase)", InvokeUserCode_CmdSetTargetGame__GameBase, requiresAuthority: false);
		RemoteProcedureCalls.RegisterRpc(typeof(GoldenChip), "System.Void GoldenChip::RpcOnDropped()", InvokeUserCode_RpcOnDropped);
		RemoteProcedureCalls.RegisterRpc(typeof(GoldenChip), "System.Void GoldenChip::RpcOnPickedUp()", InvokeUserCode_RpcOnPickedUp);
		RemoteProcedureCalls.RegisterRpc(typeof(GoldenChip), "System.Void GoldenChip::RpcSetTargetGame(GameBase)", InvokeUserCode_RpcSetTargetGame__GameBase);
		RemoteProcedureCalls.RegisterRpc(typeof(GoldenChip), "System.Void GoldenChip::RpcApplyChip(System.Boolean)", InvokeUserCode_RpcApplyChip__Boolean);
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
