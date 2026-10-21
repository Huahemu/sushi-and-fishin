namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::SushiAndFish.Casino.Steamworks;
using global::UnityEngine;

public class UpgradeManager : NetworkSingleton<UpgradeManager>
{
	private readonly Dictionary<ulong, PlayerUpgradeData> _upgradeDataBySteamId = new Dictionary<ulong, PlayerUpgradeData>();

	public override void OnStartClient()
	{
		CmdUpdateUI();
	}

	[Command(requiresAuthority = false)]
	private void CmdUpdateUI()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendCommandInternal("System.Void UpgradeManager::CmdUpdateUI()", -834980475, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public void ChangeUpgradeData(ulong steamId, PlayerUpgradeType type, float amount)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void UpgradeManager::ChangeUpgradeData(System.UInt64,PlayerUpgradeType,System.Single)' called when server was not active");
			return;
		}
		if (!_upgradeDataBySteamId.TryGetValue(steamId, out var value))
		{
			value = new PlayerUpgradeData();
			_upgradeDataBySteamId[steamId] = value;
		}
		if (type == PlayerUpgradeType.Insurance)
		{
			float num = 1f - value.Upgrades[type];
			float value2 = 1f - num * (1f - amount);
			value.Upgrades[type] = value2;
		}
		else
		{
			value.Upgrades[type] += amount;
		}
		RpcOnDataChanged(steamId, type, _upgradeDataBySteamId[steamId].Upgrades[type], amount);
	}

	[Server]
	public void SetUpgradeData(ulong steamId, PlayerUpgradeType type, float value)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void UpgradeManager::SetUpgradeData(System.UInt64,PlayerUpgradeType,System.Single)' called when server was not active");
			return;
		}
		if (_upgradeDataBySteamId.TryGetValue(steamId, out var value2))
		{
			value2.Upgrades[type] = value;
		}
		else
		{
			_upgradeDataBySteamId[steamId] = new PlayerUpgradeData();
			_upgradeDataBySteamId[steamId].Upgrades[type] = value;
		}
		float num = new PlayerUpgradeData().Upgrades[type];
		if (value != num)
		{
			RpcOnDataChanged(steamId, type, _upgradeDataBySteamId[steamId].Upgrades[type], 0f);
		}
	}

	[Server]
	public float GetUpgradeData(ulong steamId, PlayerUpgradeType type)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Single UpgradeManager::GetUpgradeData(System.UInt64,PlayerUpgradeType)' called when server was not active");
			return default;
		}
		if (_upgradeDataBySteamId.TryGetValue(steamId, out var value))
		{
			return value.Upgrades[type];
		}
		_upgradeDataBySteamId[steamId] = new PlayerUpgradeData();
		return _upgradeDataBySteamId[steamId].Upgrades[type];
	}

	[Server]
	public IReadOnlyDictionary<ulong, PlayerUpgradeData> GetAllUpgradeDataBySteamId()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Collections.Generic.IReadOnlyDictionary`2<System.UInt64,PlayerUpgradeData> UpgradeManager::GetAllUpgradeDataBySteamId()' called when server was not active");
			return null;
		}
		return _upgradeDataBySteamId;
	}

	[Server]
	public void ServerResetAllUpgradesToDefaults()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void UpgradeManager::ServerResetAllUpgradesToDefaults()' called when server was not active");
			return;
		}
		_upgradeDataBySteamId.Clear();
		RpcClearUpgradeUI();
	}

	[ClientRpc]
	private void RpcOnDataChanged(ulong steamId, PlayerUpgradeType type, float value, float amount)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarULong(steamId);
		GeneratedNetworkCode._Write_PlayerUpgradeType(writer, type);
		writer.WriteFloat(value);
		writer.WriteFloat(amount);
		SendRPCInternal("System.Void UpgradeManager::RpcOnDataChanged(System.UInt64,PlayerUpgradeType,System.Single,System.Single)", 1329345160, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcClearUpgradeUI()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void UpgradeManager::RpcClearUpgradeUI()", -1500172496, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_CmdUpdateUI()
	{
		foreach (KeyValuePair<ulong, PlayerUpgradeData> item in _upgradeDataBySteamId)
		{
			foreach (KeyValuePair<PlayerUpgradeType, float> upgrade in item.Value.Upgrades)
			{
				float num = new PlayerUpgradeData().Upgrades[upgrade.Key];
				if (upgrade.Value != num)
				{
					RpcOnDataChanged(item.Key, upgrade.Key, upgrade.Value, 0f);
				}
			}
		}
	}

	protected static void InvokeUserCode_CmdUpdateUI(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdUpdateUI called on client.");
		}
		else
		{
			((UpgradeManager)obj).UserCode_CmdUpdateUI();
		}
	}

	protected void UserCode_RpcOnDataChanged__UInt64__PlayerUpgradeType__Single__Single(ulong steamId, PlayerUpgradeType type, float value, float amount)
	{
		if (SteamUser.GetSteamID().m_SteamID == steamId)
		{
			MonoSingleton<UpgradeUI>.Instance.UpdateUpgradeUI(type, value, amount);
		}
	}

	protected static void InvokeUserCode_RpcOnDataChanged__UInt64__PlayerUpgradeType__Single__Single(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcOnDataChanged called on server.");
		}
		else
		{
			((UpgradeManager)obj).UserCode_RpcOnDataChanged__UInt64__PlayerUpgradeType__Single__Single(reader.ReadVarULong(), GeneratedNetworkCode._Read_PlayerUpgradeType(reader), reader.ReadFloat(), reader.ReadFloat());
		}
	}

	protected void UserCode_RpcClearUpgradeUI()
	{
		MonoSingleton<UpgradeUI>.Instance.ClearUpgradeUI();
	}

	protected static void InvokeUserCode_RpcClearUpgradeUI(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcClearUpgradeUI called on server.");
		}
		else
		{
			((UpgradeManager)obj).UserCode_RpcClearUpgradeUI();
		}
	}

	static UpgradeManager()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(UpgradeManager), "System.Void UpgradeManager::CmdUpdateUI()", InvokeUserCode_CmdUpdateUI, requiresAuthority: false);
		RemoteProcedureCalls.RegisterRpc(typeof(UpgradeManager), "System.Void UpgradeManager::RpcOnDataChanged(System.UInt64,PlayerUpgradeType,System.Single,System.Single)", InvokeUserCode_RpcOnDataChanged__UInt64__PlayerUpgradeType__Single__Single);
		RemoteProcedureCalls.RegisterRpc(typeof(UpgradeManager), "System.Void UpgradeManager::RpcClearUpgradeUI()", InvokeUserCode_RpcClearUpgradeUI);
	}
}

}
