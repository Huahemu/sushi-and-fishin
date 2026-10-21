namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::UnityEngine;

public class OrganManager : NetworkSingleton<OrganManager>
{
	public Dictionary<int, PlayerOrganData> OrganData = new Dictionary<int, PlayerOrganData>();

	private Dictionary<ulong, PlayerOrganData> OrganDataBySteamId = new Dictionary<ulong, PlayerOrganData>();

	public override void OnStartServer()
	{
		base.OnStartServer();
		NetworkSingleton<PlayerSpawnManager>.Instance.OnPlayerLateJoined += ServerApplyAllOrganSettings;
	}

	public override void OnStopServer()
	{
		base.OnStopServer();
		NetworkSingleton<PlayerSpawnManager>.Instance.OnPlayerLateJoined -= ServerApplyAllOrganSettings;
	}

	[Server]
	public void ServerRegisterPlayer(PlayerOrgans po)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void OrganManager::ServerRegisterPlayer(PlayerOrgans)' called when server was not active");
			return;
		}
		ulong num = 0uL;
		PlayerProfile component = po.GetComponent<PlayerProfile>();
		if (component != null)
		{
			num = component.steamId;
		}
		int connectionId = po.connectionToClient.connectionId;
		PlayerOrganData playerOrganData;
		if (num != 0L && OrganDataBySteamId.TryGetValue(num, out var value))
		{
			playerOrganData = new PlayerOrganData
			{
				organsReference = po,
				leftEye = value.leftEye,
				rightEye = value.rightEye,
				body = value.body,
				mouth = value.mouth
			};
			value.organsReference = po;
			OrganDataBySteamId[num] = value;
		}
		else
		{
			playerOrganData = new PlayerOrganData
			{
				organsReference = po,
				leftEye = true,
				rightEye = true,
				body = true,
				mouth = true
			};
			if (num != 0L)
			{
				OrganDataBySteamId[num] = playerOrganData;
			}
		}
		OrganData[connectionId] = playerOrganData;
		po.ServerSetBodyParts(playerOrganData);
	}

	[Server]
	public void ServerToggleOrgan(PlayerOrgans organs, OrganType organType, bool isEnabled)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void OrganManager::ServerToggleOrgan(PlayerOrgans,OrganType,System.Boolean)' called when server was not active");
			return;
		}
		int connectionId = organs.connectionToClient.connectionId;
		if (!OrganData.TryGetValue(connectionId, out var value))
		{
			return;
		}
		switch (organType)
		{
		case OrganType.LeftEye:
			value.leftEye = isEnabled;
			break;
		case OrganType.RightEye:
			value.rightEye = isEnabled;
			break;
		case OrganType.Body:
			value.body = isEnabled;
			break;
		case OrganType.Mouth:
			value.mouth = isEnabled;
			break;
		}
		PlayerProfile component = organs.GetComponent<PlayerProfile>();
		if (component != null && component.steamId != 0L)
		{
			if (OrganDataBySteamId.TryGetValue(component.steamId, out var value2))
			{
				value2.leftEye = value.leftEye;
				value2.rightEye = value.rightEye;
				value2.body = value.body;
				value2.mouth = value.mouth;
			}
			else
			{
				OrganDataBySteamId[component.steamId] = value;
			}
		}
		organs.ServerSetBodyParts(value);
	}

	[Server]
	public void ServerApplyAllOrganSettings()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void OrganManager::ServerApplyAllOrganSettings()' called when server was not active");
			return;
		}
		foreach (KeyValuePair<int, PlayerOrganData> organDatum in OrganData)
		{
			PlayerOrganData value = organDatum.Value;
			if (value.organsReference != null)
			{
				value.organsReference.ServerSetBodyParts(value);
			}
		}
	}

	[Server]
	public void ServerResetAllOrgansToDefaults()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void OrganManager::ServerResetAllOrgansToDefaults()' called when server was not active");
			return;
		}
		OrganDataBySteamId.Clear();
		foreach (KeyValuePair<int, PlayerOrganData> organDatum in OrganData)
		{
			PlayerOrganData value = organDatum.Value;
			value.leftEye = true;
			value.rightEye = true;
			value.body = true;
			value.mouth = true;
			if (value.organsReference != null)
			{
				value.organsReference.ServerSetBodyParts(value);
				PlayerProfile component = value.organsReference.GetComponent<PlayerProfile>();
				if (component != null && component.steamId != 0L)
				{
					OrganDataBySteamId[component.steamId] = value;
				}
			}
		}
	}

	[Server]
	public Dictionary<ulong, PlayerOrganData> GetAllOrganDataBySteamId()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Collections.Generic.Dictionary`2<System.UInt64,PlayerOrganData> OrganManager::GetAllOrganDataBySteamId()' called when server was not active");
			return null;
		}
		return new Dictionary<ulong, PlayerOrganData>(OrganDataBySteamId);
	}

	[Server]
	public PlayerOrganData GetOrganData(PlayerOrgans organs)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'PlayerOrganData OrganManager::GetOrganData(PlayerOrgans)' called when server was not active");
			return null;
		}
		int connectionId = organs.connectionToClient.connectionId;
		if (!OrganData.TryGetValue(connectionId, out var value))
		{
			return null;
		}
		return value;
	}

	[Server]
	public PlayerOrganData GetOrganData(ulong steamId)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'PlayerOrganData OrganManager::GetOrganData(System.UInt64)' called when server was not active");
			return null;
		}
		return OrganDataBySteamId[steamId];
	}

	[Server]
	public void SetOrganDataBySteamId(ulong steamId, bool leftEye, bool rightEye, bool body, bool mouth)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void OrganManager::SetOrganDataBySteamId(System.UInt64,System.Boolean,System.Boolean,System.Boolean,System.Boolean)' called when server was not active");
			return;
		}
		if (OrganDataBySteamId.TryGetValue(steamId, out var value))
		{
			value.leftEye = leftEye;
			value.rightEye = rightEye;
			value.body = body;
			value.mouth = mouth;
			if (value.organsReference != null)
			{
				int connectionId = value.organsReference.connectionToClient.connectionId;
				if (OrganData.TryGetValue(connectionId, out var value2))
				{
					value2.leftEye = leftEye;
					value2.rightEye = rightEye;
					value2.body = body;
					value2.mouth = mouth;
				}
				value.organsReference.ServerSetBodyParts(value);
			}
			return;
		}
		OrganDataBySteamId[steamId] = new PlayerOrganData
		{
			organsReference = null,
			leftEye = leftEye,
			rightEye = rightEye,
			body = body,
			mouth = mouth
		};
		foreach (KeyValuePair<int, PlayerOrganData> organDatum in OrganData)
		{
			PlayerOrganData value3 = organDatum.Value;
			if (value3.organsReference != null)
			{
				PlayerProfile component = value3.organsReference.GetComponent<PlayerProfile>();
				if (component != null && component.steamId == steamId)
				{
					value3.leftEye = leftEye;
					value3.rightEye = rightEye;
					value3.body = body;
					value3.mouth = mouth;
					OrganDataBySteamId[steamId].organsReference = value3.organsReference;
					value3.organsReference.ServerSetBodyParts(value3);
					break;
				}
			}
		}
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
