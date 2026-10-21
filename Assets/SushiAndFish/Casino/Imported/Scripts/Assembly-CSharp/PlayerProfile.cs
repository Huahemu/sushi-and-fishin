namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Runtime.InteropServices;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::SkyBrave_Toolkit.Scripts.Scriptable_Game_Events;
using global::SushiAndFish.Casino.Steamworks;
using global::TMPro;
using global::UnityEngine;

public class PlayerProfile : NetworkBehaviour
{
	[SyncVar]
	public string playerName;

	[SyncVar]
	public ulong steamId;

	[SyncVar]
	public Texture2D steamProfilePicture;

	[SyncVar(hook = "OnSync")]
	public bool hasSynced;

	[Space(10f)]
	public GameEvent clientOnPlayerProfileUpdated;

	public Action OnPlayerProfileUpdated;

	[SerializeField]
	private TextMeshProUGUI playerNameLabel;

	public Action<bool, bool> _Mirror_SyncVarHookDelegate_hasSynced;

	public string NetworkplayerName
	{
		get
		{
			return playerName;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref playerName, 1uL, null);
		}
	}

	public ulong NetworksteamId
	{
		get
		{
			return steamId;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref steamId, 2uL, null);
		}
	}

	public Texture2D NetworksteamProfilePicture
	{
		get
		{
			return steamProfilePicture;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref steamProfilePicture, 4uL, null);
		}
	}

	public bool NetworkhasSynced
	{
		get
		{
			return hasSynced;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref hasSynced, 8uL, _Mirror_SyncVarHookDelegate_hasSynced);
		}
	}

	private void Start()
	{
		if (hasSynced)
		{
			SetPlayerNameTag();
		}
	}

	public override void OnStartClient()
	{
		base.OnStartClient();
		MonoSingleton<LocalManager>.Instance.RegisterPlayer(base.netIdentity);
		if (!base.isLocalPlayer)
		{
			base.enabled = false;
		}
		else if (SteamManager.Initialized)
		{
			string personaName = SteamFriends.GetPersonaName();
			ulong steamID = SteamUser.GetSteamID().m_SteamID;
			Texture2D steamImageAsTexture = GetSteamImageAsTexture(SteamFriends.GetLargeFriendAvatar(new CSteamID(SteamUser.GetSteamID().m_SteamID)));
			SetVariables(personaName, steamID, steamImageAsTexture);
		}
		else
		{
			SetVariables("Guest", 0uL, null);
		}
	}

	public override void OnStopClient()
	{
		base.OnStopClient();
		MonoSingleton<LocalManager>.Instance.UnregisterPlayer(base.netIdentity);
	}

	private void SetVariables(string profileName, ulong id, Texture2D profilePicture)
	{
		NetworkplayerName = profileName;
		NetworksteamId = id;
		NetworksteamProfilePicture = profilePicture;
		bool oldValue = hasSynced;
		NetworkhasSynced = true;
		if (!base.isServer)
		{
			OnSync(oldValue, hasSynced);
		}
	}

	private void OnSync(bool oldValue, bool newValue)
	{
		if (oldValue != newValue && newValue)
		{
			if ((bool)clientOnPlayerProfileUpdated)
			{
				clientOnPlayerProfileUpdated?.Raise();
			}
			OnPlayerProfileUpdated?.Invoke();
			SetPlayerNameTag();
			GetComponent<SteamIdComponent>().SetSteamID(steamId);
		}
	}

	private void SetPlayerNameTag()
	{
		if (playerNameLabel != null)
		{
			playerNameLabel.text = playerName;
		}
	}

	private Texture2D GetSteamImageAsTexture(int imageHandle)
	{
		if (imageHandle == 0)
		{
			return null;
		}
		if (SteamUtils.GetImageSize(imageHandle, out var pnWidth, out var pnHeight))
		{
			byte[] array = new byte[pnWidth * pnHeight * 4];
			if (SteamUtils.GetImageRGBA(imageHandle, array, (int)(pnWidth * pnHeight * 4)))
			{
				Texture2D texture2D = new Texture2D((int)pnWidth, (int)pnHeight, TextureFormat.RGBA32, mipChain: false, linear: true);
				texture2D.LoadRawTextureData(array);
				texture2D.Apply();
				return texture2D;
			}
		}
		return null;
	}

	public PlayerProfile()
	{
		_Mirror_SyncVarHookDelegate_hasSynced = OnSync;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			writer.WriteString(playerName);
			writer.WriteVarULong(steamId);
			writer.WriteTexture2D(steamProfilePicture);
			writer.WriteBool(hasSynced);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 1L) != 0L)
		{
			writer.WriteString(playerName);
		}
		if ((syncVarDirtyBits & 2L) != 0L)
		{
			writer.WriteVarULong(steamId);
		}
		if ((syncVarDirtyBits & 4L) != 0L)
		{
			writer.WriteTexture2D(steamProfilePicture);
		}
		if ((syncVarDirtyBits & 8L) != 0L)
		{
			writer.WriteBool(hasSynced);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref playerName, null, reader.ReadString());
			GeneratedSyncVarDeserialize(ref steamId, null, reader.ReadVarULong());
			GeneratedSyncVarDeserialize(ref steamProfilePicture, null, reader.ReadTexture2D());
			GeneratedSyncVarDeserialize(ref hasSynced, _Mirror_SyncVarHookDelegate_hasSynced, reader.ReadBool());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 1L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref playerName, null, reader.ReadString());
		}
		if ((num & 2L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref steamId, null, reader.ReadVarULong());
		}
		if ((num & 4L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref steamProfilePicture, null, reader.ReadTexture2D());
		}
		if ((num & 8L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref hasSynced, _Mirror_SyncVarHookDelegate_hasSynced, reader.ReadBool());
		}
	}
}

}
