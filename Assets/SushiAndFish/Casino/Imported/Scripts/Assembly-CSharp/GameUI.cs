namespace SushiAndFish.Casino.Original
{
using SushiAndFish.Casino.Original.Extensions;
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::TMPro;
using global::UnityEngine;

public class GameUI : NetworkSingleton<GameUI>
{
	[Header("References")]
	[SerializeField]
	private GameObject statusHolder;

	[SerializeField]
	private GameObject timerHolder;

	[SerializeField]
	private GameObject moneyHolder;

	[SerializeField]
	private GameObject contributionsHolder;

	[SerializeField]
	private TextMeshProUGUI dayText;

	[SerializeField]
	private TextMeshProUGUI floorText;

	[SerializeField]
	private TextMeshProUGUI timerText;

	private void Start()
	{
		SetFloorText(NetworkSingleton<GameManager>.Instance.currentFloor);
	}

	[Server]
	public void ServerSetLoadingScreen(bool isEnabled, float duration, bool loadingScreen = true)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void GameUI::ServerSetLoadingScreen(System.Boolean,System.Single,System.Boolean)' called when server was not active");
		}
		else
		{
			RpcSetLoadingScreen(isEnabled, duration, loadingScreen);
		}
	}

	[ClientRpc]
	private void RpcSetLoadingScreen(bool isEnabled, float duration, bool loadingScreen)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isEnabled);
		writer.WriteFloat(duration);
		writer.WriteBool(loadingScreen);
		SendRPCInternal("System.Void GameUI::RpcSetLoadingScreen(System.Boolean,System.Single,System.Boolean)", -1385870491, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public void SetDaysText(int days)
	{
		dayText.text = $"DAY {days}";
	}

	public void SetFloorText(int floor)
	{
		floorText.text = $"FLOOR {floor + 1}";
	}

	public void SetTimerText(float time)
	{
		int num = Mathf.FloorToInt(time / 60f);
		int num2 = Mathf.FloorToInt(time % 60f);
		timerText.text = $"{num:#0}:{num2:00}";
		timerText.color = ((time > 30f) ? Color.white : new Color(0.925f, 0.18f, 0.247f));
		RuntimeManager.StudioSystem.setParameterByName("StressMode", (!(time > 30f)) ? 1 : 0);
	}

	[Server]
	public void ServerToggleTimer(bool isEnabled)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void GameUI::ServerToggleTimer(System.Boolean)' called when server was not active");
		}
		else
		{
			RpcToggleTimer(isEnabled);
		}
	}

	[ClientRpc]
	private void RpcToggleTimer(bool isEnabled)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isEnabled);
		SendRPCInternal("System.Void GameUI::RpcToggleTimer(System.Boolean)", -1414466594, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void ToggleTimer(bool isEnabled)
	{
		timerHolder.gameObject.SetActive(isEnabled);
	}

	[Server]
	public void ServerToggleMoneyUI(bool isEnabled, bool showCont = true)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void GameUI::ServerToggleMoneyUI(System.Boolean,System.Boolean)' called when server was not active");
		}
		else
		{
			RpcToggleMoneyUI(isEnabled, showCont);
		}
	}

	[ClientRpc]
	private void RpcToggleMoneyUI(bool isEnabled, bool showCont)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isEnabled);
		writer.WriteBool(showCont);
		SendRPCInternal("System.Void GameUI::RpcToggleMoneyUI(System.Boolean,System.Boolean)", -1857566996, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void ToggleMoneyUI(bool isEnabled, bool showCont)
	{
		moneyHolder.SetActive(isEnabled);
		contributionsHolder.SetActive(showCont);
	}

	[Server]
	public void ServerToggleStatusUI(bool isEnabled)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void GameUI::ServerToggleStatusUI(System.Boolean)' called when server was not active");
		}
		else
		{
			RpcToggleStatusUI(isEnabled);
		}
	}

	[ClientRpc]
	private void RpcToggleStatusUI(bool isEnabled)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isEnabled);
		SendRPCInternal("System.Void GameUI::RpcToggleStatusUI(System.Boolean)", 1917469469, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void ToggleStatusUI(bool isEnabled)
	{
		statusHolder.SetActive(isEnabled);
	}

	[Server]
	public void ServerToggleCrosshair(bool isEnabled)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void GameUI::ServerToggleCrosshair(System.Boolean)' called when server was not active");
		}
		else
		{
			RpcToggleCrosshair(isEnabled);
		}
	}

	[ClientRpc]
	private void RpcToggleCrosshair(bool isEnabled)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isEnabled);
		SendRPCInternal("System.Void GameUI::RpcToggleCrosshair(System.Boolean)", -1365095413, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void ToggleCrosshair(bool isEnabled)
	{
		MonoSingleton<LocalManager>.Instance.SetCrosshair(isEnabled);
	}

	[Server]
	public void ServerToggleItemInputsUI(bool isEnabled)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void GameUI::ServerToggleItemInputsUI(System.Boolean)' called when server was not active");
		}
		else
		{
			RpcToggleItemInputsUI(isEnabled);
		}
	}

	[ClientRpc]
	private void RpcToggleItemInputsUI(bool isEnabled)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isEnabled);
		SendRPCInternal("System.Void GameUI::RpcToggleItemInputsUI(System.Boolean)", -1451531007, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void ToggleItemInputsUI(bool isEnabled)
	{
		MonoSingleton<LocalManager>.Instance.itemInputsUI.SetActive(isEnabled);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcSetLoadingScreen__Boolean__Single__Boolean(bool isEnabled, float duration, bool loadingScreen)
	{
		MonoSingleton<SceneTransitioner>.Instance.SetLoadingScreen(isEnabled, duration, loadingScreen);
	}

	protected static void InvokeUserCode_RpcSetLoadingScreen__Boolean__Single__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetLoadingScreen called on server.");
		}
		else
		{
			((GameUI)obj).UserCode_RpcSetLoadingScreen__Boolean__Single__Boolean(reader.ReadBool(), reader.ReadFloat(), reader.ReadBool());
		}
	}

	protected void UserCode_RpcToggleTimer__Boolean(bool isEnabled)
	{
		ToggleTimer(isEnabled);
	}

	protected static void InvokeUserCode_RpcToggleTimer__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcToggleTimer called on server.");
		}
		else
		{
			((GameUI)obj).UserCode_RpcToggleTimer__Boolean(reader.ReadBool());
		}
	}

	protected void UserCode_RpcToggleMoneyUI__Boolean__Boolean(bool isEnabled, bool showCont)
	{
		ToggleMoneyUI(isEnabled, showCont);
	}

	protected static void InvokeUserCode_RpcToggleMoneyUI__Boolean__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcToggleMoneyUI called on server.");
		}
		else
		{
			((GameUI)obj).UserCode_RpcToggleMoneyUI__Boolean__Boolean(reader.ReadBool(), reader.ReadBool());
		}
	}

	protected void UserCode_RpcToggleStatusUI__Boolean(bool isEnabled)
	{
		ToggleStatusUI(isEnabled);
	}

	protected static void InvokeUserCode_RpcToggleStatusUI__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcToggleStatusUI called on server.");
		}
		else
		{
			((GameUI)obj).UserCode_RpcToggleStatusUI__Boolean(reader.ReadBool());
		}
	}

	protected void UserCode_RpcToggleCrosshair__Boolean(bool isEnabled)
	{
		ToggleCrosshair(isEnabled);
	}

	protected static void InvokeUserCode_RpcToggleCrosshair__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcToggleCrosshair called on server.");
		}
		else
		{
			((GameUI)obj).UserCode_RpcToggleCrosshair__Boolean(reader.ReadBool());
		}
	}

	protected void UserCode_RpcToggleItemInputsUI__Boolean(bool isEnabled)
	{
		ToggleItemInputsUI(isEnabled);
	}

	protected static void InvokeUserCode_RpcToggleItemInputsUI__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcToggleItemInputsUI called on server.");
		}
		else
		{
			((GameUI)obj).UserCode_RpcToggleItemInputsUI__Boolean(reader.ReadBool());
		}
	}

	static GameUI()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(GameUI), "System.Void GameUI::RpcSetLoadingScreen(System.Boolean,System.Single,System.Boolean)", InvokeUserCode_RpcSetLoadingScreen__Boolean__Single__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(GameUI), "System.Void GameUI::RpcToggleTimer(System.Boolean)", InvokeUserCode_RpcToggleTimer__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(GameUI), "System.Void GameUI::RpcToggleMoneyUI(System.Boolean,System.Boolean)", InvokeUserCode_RpcToggleMoneyUI__Boolean__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(GameUI), "System.Void GameUI::RpcToggleStatusUI(System.Boolean)", InvokeUserCode_RpcToggleStatusUI__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(GameUI), "System.Void GameUI::RpcToggleCrosshair(System.Boolean)", InvokeUserCode_RpcToggleCrosshair__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(GameUI), "System.Void GameUI::RpcToggleItemInputsUI(System.Boolean)", InvokeUserCode_RpcToggleItemInputsUI__Boolean);
	}
}

}
