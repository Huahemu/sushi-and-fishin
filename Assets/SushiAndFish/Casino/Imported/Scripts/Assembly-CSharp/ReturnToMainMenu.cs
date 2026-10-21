namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::SushiAndFish.Casino.Steamworks;
using global::UnityEngine;

public class ReturnToMainMenu : NetworkBehaviour
{
	public void ClientReturnToMainMenu()
	{
		if (base.isClient && !base.isServer && NetworkClient.isConnected)
		{
			CmdDropHeldItemBeforeExit();
		}
		LeaveSteamLobbyIfInLobby();
		if (base.isServer)
		{
			RpcReturnToMainMenu();
		}
		StartCoroutine(ReturnToMainMenuCoroutine());
	}

	private IEnumerator ReturnToMainMenuCoroutine()
	{
		MonoSingleton<SceneTransitioner>.Instance.SetLoadingScreen(isEnabled: true, 0.5f, loadingScreen: false);
		yield return new WaitForSeconds(1f);
		LobbySettings lobbySettings = SushiAndFish.Casino.Original.CasinoResources.Load<LobbySettings>("LobbySettings");
		if (base.isServer && SteamManager.Initialized && lobbySettings != null && lobbySettings.steamLobbyID != CSteamID.Nil)
		{
			CSteamID steamLobbyID = lobbySettings.steamLobbyID;
			if (SteamMatchmaking.GetLobbyOwner(steamLobbyID) == SteamUser.GetSteamID())
			{
				Debug.Log("Host quitting - setting GameStarted to false in Steam lobby");
				SteamMatchmaking.SetLobbyData(steamLobbyID, "GameStarted", "0");
			}
		}
		if (base.isServer)
		{
			NetworkManager.singleton.StopHost();
		}
		if (base.isClient)
		{
			NetworkManager.singleton.StopClient();
		}
	}

	[ClientRpc]
	public void RpcReturnToMainMenu()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void ReturnToMainMenu::RpcReturnToMainMenu()", 1385993414, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Command]
	private void CmdDropHeldItemBeforeExit()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendCommandInternal("System.Void ReturnToMainMenu::CmdDropHeldItemBeforeExit()", 455386482, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	private void LeaveSteamLobbyIfInLobby()
	{
		if (!(MonoSingleton<LobbyManager>.Instance == null))
		{
			LobbySettings lobbySettings = SushiAndFish.Casino.Original.CasinoResources.Load<LobbySettings>("LobbySettings");
			if (lobbySettings != null && lobbySettings.inALobby && lobbySettings.steamLobbyID != CSteamID.Nil)
			{
				Debug.Log("Client quitting - leaving Steam lobby...");
				MonoSingleton<LobbyManager>.Instance.LeaveLobby();
			}
		}
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcReturnToMainMenu()
	{
		LeaveSteamLobbyIfInLobby();
		StartCoroutine(ReturnToMainMenuCoroutine());
	}

	protected static void InvokeUserCode_RpcReturnToMainMenu(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcReturnToMainMenu called on server.");
		}
		else
		{
			((ReturnToMainMenu)obj).UserCode_RpcReturnToMainMenu();
		}
	}

	protected void UserCode_CmdDropHeldItemBeforeExit()
	{
		if (!(base.connectionToClient?.identity == null))
		{
			if (base.connectionToClient.identity.TryGetComponent<PlayerCarry>(out var component) && component.TryGetHolderInventory(out var holderInventory))
			{
				holderInventory.ServerDropHoldingItem();
			}
			if (base.connectionToClient.identity.TryGetComponent<PlayerInventory>(out var component2))
			{
				component2.ServerDropHoldingItem();
			}
		}
	}

	protected static void InvokeUserCode_CmdDropHeldItemBeforeExit(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdDropHeldItemBeforeExit called on client.");
		}
		else
		{
			((ReturnToMainMenu)obj).UserCode_CmdDropHeldItemBeforeExit();
		}
	}

	static ReturnToMainMenu()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(ReturnToMainMenu), "System.Void ReturnToMainMenu::CmdDropHeldItemBeforeExit()", InvokeUserCode_CmdDropHeldItemBeforeExit, requiresAuthority: true);
		RemoteProcedureCalls.RegisterRpc(typeof(ReturnToMainMenu), "System.Void ReturnToMainMenu::RpcReturnToMainMenu()", InvokeUserCode_RpcReturnToMainMenu);
	}
}

}
