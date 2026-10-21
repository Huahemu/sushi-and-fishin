namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::SushiAndFish.Casino.Steamworks;
using global::UnityEngine;
using global::UnityEngine.UI;

public class ColorCauldron : NetworkBehaviour
{
	[Header("Color Selection")]
	[SerializeField]
	private UIColorPalette colorPalette;

	[SerializeField]
	private List<InteractableEventTrigger> colorTriggers = new List<InteractableEventTrigger>();

	[SerializeField]
	private MeshRenderer selectedColorPreviewRenderer;

	[SerializeField]
	private Collider cauldronTrigger;

	[Header("SFX")]
	[SerializeField]
	private EventReference colorChangeSFX;

	[SerializeField]
	private Color _color;

	private LobbySettings _lobbySettings;

	private MaterialPropertyBlock _mpb;

	private void Awake()
	{
		_lobbySettings = SushiAndFish.Casino.Original.CasinoResources.Load<LobbySettings>("LobbySettings");
		_mpb = new MaterialPropertyBlock();
		selectedColorPreviewRenderer.GetPropertyBlock(_mpb);
		ApplyPaletteToTriggers();
	}

	public override void OnStartServer()
	{
		base.OnStartServer();
		SubscribeTriggers();
	}

	private void Start()
	{
		_color = colorPalette.playerColors[0];
		_mpb.SetColor("_BaseColor", _color);
		selectedColorPreviewRenderer.SetPropertyBlock(_mpb);
	}

	private void SubscribeTriggers()
	{
		for (int i = 0; i < colorTriggers.Count && i < (colorPalette?.playerColors?.Length).GetValueOrDefault(); i++)
		{
			int index = i;
			colorTriggers[i].serverOnInteractEvent.AddListener((PlayerInteract _) =>
			{
				ServerSetSelectedColor(index);
			});
		}
	}

	private void OnTriggerEnter(Collider other)
	{
		if (IsLocalPlayerCollider(other) && (bool)other.attachedRigidbody && other.attachedRigidbody.TryGetComponent<PlayerProfile>(out var component))
		{
			CmdChangePlayerColor(component.steamId, _color);
		}
	}

	[Server]
	private void ServerSetSelectedColor(int index)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void ColorCauldron::ServerSetSelectedColor(System.Int32)' called when server was not active");
		}
		else if ((bool)colorPalette && colorPalette.playerColors != null && colorPalette.playerColors.Length != 0)
		{
			int index2 = Mathf.Clamp(index, 0, colorPalette.playerColors.Length - 1);
			RpcSetSelectedColor(index2);
		}
	}

	[ClientRpc]
	private void RpcSetSelectedColor(int index)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(index);
		SendRPCInternal("System.Void ColorCauldron::RpcSetSelectedColor(System.Int32)", 1659102954, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private bool IsLocalPlayerCollider(Collider other)
	{
		NetworkIdentity componentInParent = other.GetComponentInParent<NetworkIdentity>();
		if ((bool)componentInParent)
		{
			return componentInParent.isLocalPlayer;
		}
		return false;
	}

	[Command(requiresAuthority = false)]
	private void CmdChangePlayerColor(ulong steamId, Color newColor)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarULong(steamId);
		writer.WriteColor(newColor);
		SendCommandInternal("System.Void ColorCauldron::CmdChangePlayerColor(System.UInt64,UnityEngine.Color)", 1073795479, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	private void ApplyPaletteToTriggers()
	{
		if (colorPalette == null || colorPalette.playerColors == null || colorTriggers == null || colorTriggers.Count == 0)
		{
			return;
		}
		int num = Mathf.Min(colorTriggers.Count, colorPalette.playerColors.Length);
		for (int i = 0; i < num; i++)
		{
			InteractableEventTrigger interactableEventTrigger = colorTriggers[i];
			if (!(interactableEventTrigger == null))
			{
				Color color = colorPalette.playerColors[i];
				Image image = interactableEventTrigger.GetComponent<Image>() ?? interactableEventTrigger.GetComponentInChildren<Image>();
				if (image != null)
				{
					image.color = color;
				}
				SpriteRenderer spriteRenderer = interactableEventTrigger.GetComponent<SpriteRenderer>() ?? interactableEventTrigger.GetComponentInChildren<SpriteRenderer>();
				if (spriteRenderer != null)
				{
					spriteRenderer.color = color;
				}
				MeshRenderer[] componentsInChildren = interactableEventTrigger.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
				foreach (MeshRenderer obj in componentsInChildren)
				{
					obj.material = new Material(obj.sharedMaterial);
					obj.material.color = color;
				}
			}
		}
	}

	[ClientRpc]
	private void RpcUpdatePlayerColorOnClients(ulong steamId, Color newColor)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarULong(steamId);
		writer.WriteColor(newColor);
		SendRPCInternal("System.Void ColorCauldron::RpcUpdatePlayerColorOnClients(System.UInt64,UnityEngine.Color)", 859109838, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void SavePlayerColorToSteamLobby(ulong steamId, Color color)
	{
		if (!SteamManager.Initialized)
		{
			return;
		}
		LobbySettings lobbySettings = SushiAndFish.Casino.Original.CasinoResources.Load<LobbySettings>("LobbySettings");
		if (!(lobbySettings == null) && !(lobbySettings.steamLobbyID == CSteamID.Nil))
		{
			ulong steamID = SteamUser.GetSteamID().m_SteamID;
			if (steamId == steamID)
			{
				string pchValue = ColorHexUtility.ColorToHex(color);
				SteamMatchmaking.SetLobbyMemberData(lobbySettings.steamLobbyID, "PlayerColor", pchValue);
			}
		}
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcSetSelectedColor__Int32(int index)
	{
		_color = colorPalette.playerColors[index];
		_mpb.SetColor("_BaseColor", _color);
		selectedColorPreviewRenderer.SetPropertyBlock(_mpb);
		SFXManager.SFXOneShot(colorChangeSFX, base.transform.position);
		if (cauldronTrigger.bounds.Contains(NetworkClient.localPlayer.transform.position) && NetworkClient.localPlayer.TryGetComponent<PlayerProfile>(out var component))
		{
			CmdChangePlayerColor(component.steamId, _color);
		}
	}

	protected static void InvokeUserCode_RpcSetSelectedColor__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetSelectedColor called on server.");
		}
		else
		{
			((ColorCauldron)obj).UserCode_RpcSetSelectedColor__Int32(reader.ReadVarInt());
		}
	}

	protected void UserCode_CmdChangePlayerColor__UInt64__Color(ulong steamId, Color newColor)
	{
		_lobbySettings.UpdatePlayerColor(steamId, newColor);
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
			((ColorCauldron)obj).UserCode_CmdChangePlayerColor__UInt64__Color(reader.ReadVarULong(), reader.ReadColor());
		}
	}

	protected void UserCode_RpcUpdatePlayerColorOnClients__UInt64__Color(ulong steamId, Color newColor)
	{
		_lobbySettings.UpdatePlayerColor(steamId, newColor);
		if (SteamManager.Initialized)
		{
			ulong steamID = SteamUser.GetSteamID().m_SteamID;
			if (steamId == steamID && MonoSingleton<CosmeticsUnlockManager>.Instance != null)
			{
				MonoSingleton<CosmeticsUnlockManager>.Instance.SetPlayerColor(newColor);
			}
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
			((ColorCauldron)obj).UserCode_RpcUpdatePlayerColorOnClients__UInt64__Color(reader.ReadVarULong(), reader.ReadColor());
		}
	}

	static ColorCauldron()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(ColorCauldron), "System.Void ColorCauldron::CmdChangePlayerColor(System.UInt64,UnityEngine.Color)", InvokeUserCode_CmdChangePlayerColor__UInt64__Color, requiresAuthority: false);
		RemoteProcedureCalls.RegisterRpc(typeof(ColorCauldron), "System.Void ColorCauldron::RpcSetSelectedColor(System.Int32)", InvokeUserCode_RpcSetSelectedColor__Int32);
		RemoteProcedureCalls.RegisterRpc(typeof(ColorCauldron), "System.Void ColorCauldron::RpcUpdatePlayerColorOnClients(System.UInt64,UnityEngine.Color)", InvokeUserCode_RpcUpdatePlayerColorOnClients__UInt64__Color);
	}
}

}
