namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::SushiAndFish.Casino.Steamworks;
using global::UnityEngine;
using global::UnityEngine.Rendering;

public class PlayerCustomization : NetworkBehaviour
{
	[Header("Cosmetic Mesh Filters")]
	[SerializeField]
	private MeshFilter hat;

	[SerializeField]
	private MeshFilter hair;

	[SerializeField]
	private MeshFilter mustache;

	[SerializeField]
	private MeshFilter beard;

	[SerializeField]
	private MeshFilter neckwear;

	[SerializeField]
	private MeshFilter clothing;

	[SerializeField]
	private MeshFilter facewear;

	private Dictionary<CosmeticType, int> equippedCosmetics = new Dictionary<CosmeticType, int>();

	private Dictionary<CosmeticType, GameObject> shadowOnlyClones = new Dictionary<CosmeticType, GameObject>();

	private LobbySettings lobbySettings;

	private const int ShadowOnlyLayer = 0;

	private void Awake()
	{
		lobbySettings = SushiAndFish.Casino.Original.CasinoResources.Load<LobbySettings>("LobbySettings");
	}

	public void LoadCosmetics()
	{
		if (CasinoLab.CasinoLabMode.Active) return;
		if (!base.isLocalPlayer || MonoSingleton<CosmeticsUnlockManager>.Instance == null)
		{
			return;
		}
		MonoSingleton<CosmeticsUnlockManager>.Instance.LoadFromFile();
		foreach (KeyValuePair<CosmeticType, int> equippedCosmetic in MonoSingleton<CosmeticsUnlockManager>.Instance.GetEquippedCosmetics())
		{
			int value = equippedCosmetic.Value;
			if (MonoSingleton<CosmeticsUnlockManager>.Instance.IsCosmeticUnlocked(value))
			{
				CmdChangeCustomization(value, shouldSave: false);
			}
		}
	}

	public void SaveCosmetics()
	{
		if (CasinoLab.CasinoLabMode.Active) return;
		if (base.isLocalPlayer && !(MonoSingleton<CosmeticsUnlockManager>.Instance == null))
		{
			MonoSingleton<CosmeticsUnlockManager>.Instance.SetEquippedCosmetics(equippedCosmetics);
		}
	}

	[Command]
	public void CmdChangeCustomization(int cosmeticId, bool shouldSave)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(cosmeticId);
		writer.WriteBool(shouldSave);
		SendCommandInternal("System.Void PlayerCustomization::CmdChangeCustomization(System.Int32,System.Boolean)", 1528149408, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcChangeCustomization(int cosmeticId, bool shouldSave)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(cosmeticId);
		writer.WriteBool(shouldSave);
		SendRPCInternal("System.Void PlayerCustomization::RpcChangeCustomization(System.Int32,System.Boolean)", -207402373, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void ApplyCosmetic(int cosmeticId, bool shouldSave)
	{
		if (CasinoLab.CasinoLabMode.Active) return;
		CosmeticData cosmeticById = CosmeticDataManager.GetCosmeticById(cosmeticId);
		if (cosmeticById == null)
		{
			Debug.LogError($"[PlayerCustomization] Cosmetic {cosmeticId} not found");
			return;
		}
		MeshFilter meshFilterForType = GetMeshFilterForType(cosmeticById.cosmeticType);
		Material fallbackMaterial = ((cosmeticById.cosmeticMaterial != null) ? cosmeticById.cosmeticMaterial : cosmeticById.cosmeticModel.GetComponentInChildren<MeshRenderer>()?.sharedMaterial);
		if (meshFilterForType == null || !cosmeticById.cosmeticModel.TryGetComponent<MeshFilter>(out var component))
		{
			return;
		}
		Mesh sharedMesh = component.sharedMesh;
		if (!(sharedMesh == null))
		{
			meshFilterForType.mesh = sharedMesh;
			CreateShadowOnlyDuplicate(cosmeticById.cosmeticType, meshFilterForType, sharedMesh, fallbackMaterial);
			equippedCosmetics[cosmeticById.cosmeticType] = cosmeticId;
			if (base.isLocalPlayer & shouldSave)
			{
				SaveCosmetics();
			}
		}
	}

	public void ResetCustomization()
	{
		CmdResetCustomization();
	}

	[Command]
	private void CmdResetCustomization()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendCommandInternal("System.Void PlayerCustomization::CmdResetCustomization()", -1499202751, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcResetCustomization()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void PlayerCustomization::RpcResetCustomization()", 1758572570, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void ClearAllCosmetics(bool shouldSave = true)
	{
		foreach (GameObject value in shadowOnlyClones.Values)
		{
			if (value != null)
			{
				Object.Destroy(value);
			}
		}
		shadowOnlyClones.Clear();
		hat.mesh = null;
		hair.mesh = null;
		mustache.mesh = null;
		beard.mesh = null;
		neckwear.mesh = null;
		equippedCosmetics.Clear();
		if (base.isLocalPlayer & shouldSave)
		{
			SaveCosmetics();
		}
	}

	public void ClearCategory(CosmeticType category)
	{
		CmdClearCategory(category);
	}

	[Command]
	private void CmdClearCategory(CosmeticType category)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		GeneratedNetworkCode._Write_CosmeticType(writer, category);
		SendCommandInternal("System.Void PlayerCustomization::CmdClearCategory(CosmeticType)", 1714529541, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcClearCategory(CosmeticType category)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		GeneratedNetworkCode._Write_CosmeticType(writer, category);
		SendRPCInternal("System.Void PlayerCustomization::RpcClearCategory(CosmeticType)", -866626640, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public void LoadSavedPlayerColor()
	{
		if (!base.isLocalPlayer || MonoSingleton<CosmeticsUnlockManager>.Instance == null)
		{
			return;
		}
		Color? playerColor = MonoSingleton<CosmeticsUnlockManager>.Instance.GetPlayerColor();
		if (playerColor.HasValue)
		{
			PlayerProfile component = GetComponent<PlayerProfile>();
			if (!(component == null))
			{
				CmdChangePlayerColor(component.steamId, playerColor.Value);
				SavePlayerColorToSteamLobby(component.steamId, playerColor.Value);
			}
		}
	}

	[Command]
	private void CmdChangePlayerColor(ulong steamId, Color newColor)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarULong(steamId);
		writer.WriteColor(newColor);
		SendCommandInternal("System.Void PlayerCustomization::CmdChangePlayerColor(System.UInt64,UnityEngine.Color)", 615657454, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcUpdatePlayerColorOnClients(ulong steamId, Color newColor)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarULong(steamId);
		writer.WriteColor(newColor);
		SendRPCInternal("System.Void PlayerCustomization::RpcUpdatePlayerColorOnClients(System.UInt64,UnityEngine.Color)", 2115703653, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void SavePlayerColorToSteamLobby(ulong steamId, Color color)
	{
		if (SteamManager.Initialized && !(lobbySettings?.steamLobbyID == CSteamID.Nil) && steamId == SteamUser.GetSteamID().m_SteamID)
		{
			string pchValue = ColorHexUtility.ColorToHex(color);
			SteamMatchmaking.SetLobbyMemberData(lobbySettings.steamLobbyID, "PlayerColor", pchValue);
		}
	}

	public Dictionary<CosmeticType, int> GetEquippedCosmetics()
	{
		return new Dictionary<CosmeticType, int>(equippedCosmetics);
	}

	private MeshFilter GetMeshFilterForType(CosmeticType type)
	{
		return type switch
		{
			CosmeticType.Hat => hat, 
			CosmeticType.Hair => hair, 
			CosmeticType.Mustache => mustache, 
			CosmeticType.Beard => beard, 
			CosmeticType.Neckwear => neckwear, 
			CosmeticType.Clothing => clothing, 
			CosmeticType.Facewear => facewear, 
			_ => null, 
		};
	}

	private void CreateShadowOnlyDuplicate(CosmeticType type, MeshFilter parentFilter, Mesh mesh, Material fallbackMaterial)
	{
		if (shadowOnlyClones.TryGetValue(type, out var value) && value != null)
		{
			Object.Destroy(value);
			shadowOnlyClones.Remove(type);
		}
		Transform parent = parentFilter.transform;
		GameObject gameObject = new GameObject("ShadowOnly_" + type);
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		gameObject.transform.localPosition = Vector3.zero;
		gameObject.transform.localRotation = Quaternion.identity;
		gameObject.transform.localScale = Vector3.one;
		gameObject.layer = 0;
		gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
		MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
		Material material = parentFilter.GetComponent<MeshRenderer>()?.sharedMaterial;
		meshRenderer.sharedMaterial = ((material != null) ? material : fallbackMaterial);
		meshRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
		meshRenderer.receiveShadows = false;
		shadowOnlyClones[type] = gameObject;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_CmdChangeCustomization__Int32__Boolean(int cosmeticId, bool shouldSave)
	{
		RpcChangeCustomization(cosmeticId, shouldSave);
	}

	protected static void InvokeUserCode_CmdChangeCustomization__Int32__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdChangeCustomization called on client.");
		}
		else
		{
			((PlayerCustomization)obj).UserCode_CmdChangeCustomization__Int32__Boolean(reader.ReadVarInt(), reader.ReadBool());
		}
	}

	protected void UserCode_RpcChangeCustomization__Int32__Boolean(int cosmeticId, bool shouldSave)
	{
		ApplyCosmetic(cosmeticId, shouldSave);
	}

	protected static void InvokeUserCode_RpcChangeCustomization__Int32__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcChangeCustomization called on server.");
		}
		else
		{
			((PlayerCustomization)obj).UserCode_RpcChangeCustomization__Int32__Boolean(reader.ReadVarInt(), reader.ReadBool());
		}
	}

	protected void UserCode_CmdResetCustomization()
	{
		RpcResetCustomization();
	}

	protected static void InvokeUserCode_CmdResetCustomization(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdResetCustomization called on client.");
		}
		else
		{
			((PlayerCustomization)obj).UserCode_CmdResetCustomization();
		}
	}

	protected void UserCode_RpcResetCustomization()
	{
		ClearAllCosmetics(shouldSave: false);
		int num = ((MonoSingleton<CosmeticsUnlockManager>.Instance != null) ? MonoSingleton<CosmeticsUnlockManager>.Instance.GetDefaultClothingCosmeticId() : (-1));
		if (num > 0 && CosmeticDataManager.HasCosmetic(num))
		{
			ApplyCosmetic(num, shouldSave: true);
		}
		else if (base.isLocalPlayer)
		{
			SaveCosmetics();
		}
	}

	protected static void InvokeUserCode_RpcResetCustomization(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcResetCustomization called on server.");
		}
		else
		{
			((PlayerCustomization)obj).UserCode_RpcResetCustomization();
		}
	}

	protected void UserCode_CmdClearCategory__CosmeticType(CosmeticType category)
	{
		RpcClearCategory(category);
	}

	protected static void InvokeUserCode_CmdClearCategory__CosmeticType(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdClearCategory called on client.");
		}
		else
		{
			((PlayerCustomization)obj).UserCode_CmdClearCategory__CosmeticType(GeneratedNetworkCode._Read_CosmeticType(reader));
		}
	}

	protected void UserCode_RpcClearCategory__CosmeticType(CosmeticType category)
	{
		if (shadowOnlyClones.TryGetValue(category, out var value))
		{
			if (value != null)
			{
				Object.Destroy(value);
			}
			shadowOnlyClones.Remove(category);
		}
		MeshFilter meshFilterForType = GetMeshFilterForType(category);
		if (meshFilterForType != null)
		{
			meshFilterForType.mesh = null;
		}
		equippedCosmetics.Remove(category);
		if (base.isLocalPlayer)
		{
			SaveCosmetics();
		}
	}

	protected static void InvokeUserCode_RpcClearCategory__CosmeticType(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcClearCategory called on server.");
		}
		else
		{
			((PlayerCustomization)obj).UserCode_RpcClearCategory__CosmeticType(GeneratedNetworkCode._Read_CosmeticType(reader));
		}
	}

	protected void UserCode_CmdChangePlayerColor__UInt64__Color(ulong steamId, Color newColor)
	{
		lobbySettings?.UpdatePlayerColor(steamId, newColor);
		RpcUpdatePlayerColorOnClients(steamId, newColor);
	}

	protected static void InvokeUserCode_CmdChangePlayerColor__UInt64__Color(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdChangePlayerColor called on client.");
		}
		else
		{
			((PlayerCustomization)obj).UserCode_CmdChangePlayerColor__UInt64__Color(reader.ReadVarULong(), reader.ReadColor());
		}
	}

	protected void UserCode_RpcUpdatePlayerColorOnClients__UInt64__Color(ulong steamId, Color newColor)
	{
		lobbySettings?.UpdatePlayerColor(steamId, newColor);
		if (SteamManager.Initialized)
		{
			SavePlayerColorToSteamLobby(steamId, newColor);
		}
	}

	protected static void InvokeUserCode_RpcUpdatePlayerColorOnClients__UInt64__Color(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcUpdatePlayerColorOnClients called on server.");
		}
		else
		{
			((PlayerCustomization)obj).UserCode_RpcUpdatePlayerColorOnClients__UInt64__Color(reader.ReadVarULong(), reader.ReadColor());
		}
	}

	static PlayerCustomization()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(PlayerCustomization), "System.Void PlayerCustomization::CmdChangeCustomization(System.Int32,System.Boolean)", InvokeUserCode_CmdChangeCustomization__Int32__Boolean, requiresAuthority: true);
		RemoteProcedureCalls.RegisterCommand(typeof(PlayerCustomization), "System.Void PlayerCustomization::CmdResetCustomization()", InvokeUserCode_CmdResetCustomization, requiresAuthority: true);
		RemoteProcedureCalls.RegisterCommand(typeof(PlayerCustomization), "System.Void PlayerCustomization::CmdClearCategory(CosmeticType)", InvokeUserCode_CmdClearCategory__CosmeticType, requiresAuthority: true);
		RemoteProcedureCalls.RegisterCommand(typeof(PlayerCustomization), "System.Void PlayerCustomization::CmdChangePlayerColor(System.UInt64,UnityEngine.Color)", InvokeUserCode_CmdChangePlayerColor__UInt64__Color, requiresAuthority: true);
		RemoteProcedureCalls.RegisterRpc(typeof(PlayerCustomization), "System.Void PlayerCustomization::RpcChangeCustomization(System.Int32,System.Boolean)", InvokeUserCode_RpcChangeCustomization__Int32__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(PlayerCustomization), "System.Void PlayerCustomization::RpcResetCustomization()", InvokeUserCode_RpcResetCustomization);
		RemoteProcedureCalls.RegisterRpc(typeof(PlayerCustomization), "System.Void PlayerCustomization::RpcClearCategory(CosmeticType)", InvokeUserCode_RpcClearCategory__CosmeticType);
		RemoteProcedureCalls.RegisterRpc(typeof(PlayerCustomization), "System.Void PlayerCustomization::RpcUpdatePlayerColorOnClients(System.UInt64,UnityEngine.Color)", InvokeUserCode_RpcUpdatePlayerColorOnClients__UInt64__Color);
	}
}

}
