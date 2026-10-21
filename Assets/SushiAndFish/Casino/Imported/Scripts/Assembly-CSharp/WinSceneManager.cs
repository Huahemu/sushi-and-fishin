namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::Unity.Cinemachine;
using global::UnityEngine;

public class WinSceneManager : NetworkSingleton<WinSceneManager>
{
	public List<DebtBag> debtBags = new List<DebtBag>();

	[Header("CoinFlip")]
	[SerializeField]
	private CoinFlip coinFlip;

	[SerializeField]
	private Transform[] playerPositionsCoinFlip;

	[SerializeField]
	private Transform[] debtBagPositionsCoinFlip;

	[SerializeField]
	private CinemachineCamera cameraParentCoinFlip;

	[SerializeField]
	private MoneyPipe moneyPipeCoinFlip;

	[Header("PayDebt")]
	[SerializeField]
	private Transform[] playerPositionsPayDebt;

	[SerializeField]
	private Transform[] debtBagPositionsPayDebt;

	[SerializeField]
	private CinemachineCamera cameraParentPayDebt;

	[SerializeField]
	private MoneyPipe moneyPipePayDebt;

	[Header("SFX")]
	[SerializeField]
	private SFXLoopComponent startCoinflipSFX;

	[SerializeField]
	private SFXComponent startPayDebtSFX;

	private bool _isChoiceMade;

	[Server]
	public void ServerInitializePlayCoinFlip()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void WinSceneManager::ServerInitializePlayCoinFlip()' called when server was not active");
		}
		else if (!_isChoiceMade)
		{
			_isChoiceMade = true;
			NetworkSingleton<GameUI>.Instance.ServerToggleMoneyUI(isEnabled: false);
			NetworkSingleton<GameUI>.Instance.ServerToggleCrosshair(isEnabled: false);
			StartCoroutine(PlayCoinFlipRoutine());
		}
	}

	private IEnumerator PlayCoinFlipRoutine()
	{
		RpcFadeCamera(isEnabled: true);
		yield return new WaitForSeconds(1f);
		NetworkSingleton<OrganManager>.Instance.ServerResetAllOrgansToDefaults();
		ServerLockDebtBags();
		yield return null;
		ServerTeleportDebtBags(isCoinFlip: true);
		RpcSetCameraLayer();
		RpcLockPlayerInputs();
		for (int i = 0; i < MonoSingleton<LocalManager>.Instance.players.Count; i++)
		{
			MonoSingleton<LocalManager>.Instance.players[i].controller.ServerLock(isLocked: true);
			MonoSingleton<LocalManager>.Instance.players[i].controller.ServerLockHead(isLocked: true);
			MonoSingleton<LocalManager>.Instance.players[i].controller.ServerTeleport(playerPositionsCoinFlip[i].position);
			MonoSingleton<LocalManager>.Instance.players[i].controller.ServerRotate(new Vector2(playerPositionsCoinFlip[i].eulerAngles.y, 0f));
		}
		RpcChangeCameraCoinFlip();
		RpcFadeCamera(isEnabled: false);
		yield return new WaitForSeconds(1f);
		startCoinflipSFX.RpcLoopSFX(play: true);
		coinFlip.ServerPlayCoinFlip();
	}

	[ClientRpc]
	private void RpcChangeCameraCoinFlip()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void WinSceneManager::RpcChangeCameraCoinFlip()", 879864906, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public void ServerConcludeCoinFlip(bool isWin)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void WinSceneManager::ServerConcludeCoinFlip(System.Boolean)' called when server was not active");
			return;
		}
		StartCoroutine(ConcludeCoinFlipRoutine(isWin));
		RpcSetLoopIsWin(isWin);
		startCoinflipSFX.RpcLoopSFX(play: false);
	}

	private IEnumerator ConcludeCoinFlipRoutine(bool isWin)
	{
		if (!isWin)
		{
			moneyPipeCoinFlip.ServerStartSucking();
			yield return new WaitForSeconds(2f);
		}
		else
		{
			yield return new WaitForSeconds(1f);
		}
		RpcFadeCamera(isEnabled: false);
		yield return new WaitForSeconds(1f);
		NetworkSingleton<GameManager>.Instance.ServerSetCutscene((!isWin) ? 1 : 0);
	}

	[Server]
	public void ServerInitializePayDebt()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void WinSceneManager::ServerInitializePayDebt()' called when server was not active");
		}
		else if (!_isChoiceMade)
		{
			_isChoiceMade = true;
			StartCoroutine(PayDebtRoutine());
		}
	}

	private IEnumerator PayDebtRoutine()
	{
		RpcFadeCamera(isEnabled: true);
		yield return new WaitForSeconds(1f);
		NetworkSingleton<OrganManager>.Instance.ServerResetAllOrgansToDefaults();
		ServerLockDebtBags();
		yield return null;
		ServerTeleportDebtBags(isCoinFlip: false);
		RpcSetCameraLayer();
		RpcLockPlayerInputs();
		for (int i = 0; i < MonoSingleton<LocalManager>.Instance.players.Count; i++)
		{
			MonoSingleton<LocalManager>.Instance.players[i].controller.ServerLock(isLocked: true);
			MonoSingleton<LocalManager>.Instance.players[i].controller.ServerLockHead(isLocked: true);
			MonoSingleton<LocalManager>.Instance.players[i].controller.ServerTeleport(playerPositionsPayDebt[i].position);
			MonoSingleton<LocalManager>.Instance.players[i].controller.ServerRotate(new Vector2(playerPositionsPayDebt[i].eulerAngles.y, 0f));
		}
		RpcChangeCameraPayDebt();
		RpcFadeCamera(isEnabled: false);
		yield return new WaitForSeconds(1f);
		startPayDebtSFX.RpcPlayOneShotWith3DPos();
		moneyPipePayDebt.ServerStartSucking();
		yield return new WaitForSeconds(2f);
		RpcFadeCamera(isEnabled: true);
		yield return new WaitForSeconds(1f);
		NetworkSingleton<GameManager>.Instance.ServerSetCutscene(2);
	}

	[ClientRpc]
	private void RpcChangeCameraPayDebt()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void WinSceneManager::RpcChangeCameraPayDebt()", 1449297785, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	private void ServerLockDebtBags()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void WinSceneManager::ServerLockDebtBags()' called when server was not active");
			return;
		}
		foreach (DebtBag debtBag in debtBags)
		{
			debtBag.ServerLock();
		}
	}

	[Server]
	private void ServerTeleportDebtBags(bool isCoinFlip)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void WinSceneManager::ServerTeleportDebtBags(System.Boolean)' called when server was not active");
			return;
		}
		for (int i = 0; i < debtBags.Count; i++)
		{
			DebtBag debtBag = debtBags[i];
			Transform transform = (isCoinFlip ? debtBagPositionsCoinFlip[i] : debtBagPositionsPayDebt[i]);
			debtBag.ServerTeleport(transform.position);
			debtBag.ServerRotate(transform.rotation);
		}
	}

	[ClientRpc]
	private void RpcFadeCamera(bool isEnabled)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isEnabled);
		SendRPCInternal("System.Void WinSceneManager::RpcFadeCamera(System.Boolean)", -1034923765, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcLockPlayerInputs()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void WinSceneManager::RpcLockPlayerInputs()", 256121420, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcSetCameraLayer()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void WinSceneManager::RpcSetCameraLayer()", 466922551, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcSetLoopIsWin(bool isWin)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isWin);
		SendRPCInternal("System.Void WinSceneManager::RpcSetLoopIsWin(System.Boolean)", -2093602558, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcChangeCameraCoinFlip()
	{
		cameraParentCoinFlip.enabled = true;
	}

	protected static void InvokeUserCode_RpcChangeCameraCoinFlip(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcChangeCameraCoinFlip called on server.");
		}
		else
		{
			((WinSceneManager)obj).UserCode_RpcChangeCameraCoinFlip();
		}
	}

	protected void UserCode_RpcChangeCameraPayDebt()
	{
		cameraParentPayDebt.enabled = true;
	}

	protected static void InvokeUserCode_RpcChangeCameraPayDebt(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcChangeCameraPayDebt called on server.");
		}
		else
		{
			((WinSceneManager)obj).UserCode_RpcChangeCameraPayDebt();
		}
	}

	protected void UserCode_RpcFadeCamera__Boolean(bool isEnabled)
	{
		MonoSingleton<SceneTransitioner>.Instance.SetLoadingScreen(isEnabled, 1f, loadingScreen: false);
	}

	protected static void InvokeUserCode_RpcFadeCamera__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcFadeCamera called on server.");
		}
		else
		{
			((WinSceneManager)obj).UserCode_RpcFadeCamera__Boolean(reader.ReadBool());
		}
	}

	protected void UserCode_RpcLockPlayerInputs()
	{
		InputEvents.ActiveLayer = InputLayer.Cutscene;
	}

	protected static void InvokeUserCode_RpcLockPlayerInputs(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcLockPlayerInputs called on server.");
		}
		else
		{
			((WinSceneManager)obj).UserCode_RpcLockPlayerInputs();
		}
	}

	protected void UserCode_RpcSetCameraLayer()
	{
		MonoSingleton<LocalManager>.Instance.mainCamera.cullingMask |= 1 << SushiAndFish.Casino.CasinoLayers.NameToLayer("SelfMeshPlayer");
	}

	protected static void InvokeUserCode_RpcSetCameraLayer(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetCameraLayer called on server.");
		}
		else
		{
			((WinSceneManager)obj).UserCode_RpcSetCameraLayer();
		}
	}

	protected void UserCode_RpcSetLoopIsWin__Boolean(bool isWin)
	{
		startCoinflipSFX.loopInstance.setParameterByName("IsWin", isWin ? 1 : 0);
	}

	protected static void InvokeUserCode_RpcSetLoopIsWin__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetLoopIsWin called on server.");
		}
		else
		{
			((WinSceneManager)obj).UserCode_RpcSetLoopIsWin__Boolean(reader.ReadBool());
		}
	}

	static WinSceneManager()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(WinSceneManager), "System.Void WinSceneManager::RpcChangeCameraCoinFlip()", InvokeUserCode_RpcChangeCameraCoinFlip);
		RemoteProcedureCalls.RegisterRpc(typeof(WinSceneManager), "System.Void WinSceneManager::RpcChangeCameraPayDebt()", InvokeUserCode_RpcChangeCameraPayDebt);
		RemoteProcedureCalls.RegisterRpc(typeof(WinSceneManager), "System.Void WinSceneManager::RpcFadeCamera(System.Boolean)", InvokeUserCode_RpcFadeCamera__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(WinSceneManager), "System.Void WinSceneManager::RpcLockPlayerInputs()", InvokeUserCode_RpcLockPlayerInputs);
		RemoteProcedureCalls.RegisterRpc(typeof(WinSceneManager), "System.Void WinSceneManager::RpcSetCameraLayer()", InvokeUserCode_RpcSetCameraLayer);
		RemoteProcedureCalls.RegisterRpc(typeof(WinSceneManager), "System.Void WinSceneManager::RpcSetLoopIsWin(System.Boolean)", InvokeUserCode_RpcSetLoopIsWin__Boolean);
	}
}

}
