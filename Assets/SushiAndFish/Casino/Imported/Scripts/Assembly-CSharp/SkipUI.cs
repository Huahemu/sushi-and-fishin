namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::DG.Tweening;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;
using global::UnityEngine.Events;
using global::UnityEngine.UI;

public class SkipUI : NetworkBehaviour
{
	private class HeadIconView
	{
		public Image Image;

		public CanvasGroup CanvasGroup;

		public void SetSkipped(bool skipped, float fadedAlpha)
		{
			if ((bool)CanvasGroup)
			{
				CanvasGroup.alpha = (skipped ? 1f : fadedAlpha);
			}
		}
	}

	[Header("Settings")]
	[SerializeField]
	private float fadeTime = 1f;

	[SerializeField]
	private float skipTime = 1f;

	[Header("References")]
	[SerializeField]
	private CanvasGroup canvasGroup;

	[SerializeField]
	private Image skipFillImage;

	[Header("Skipped Heads")]
	[SerializeField]
	private Transform skippedHeadsRoot;

	[SerializeField]
	private GameObject headIconPrefab;

	[SerializeField]
	private float fadedAlpha = 0.25f;

	[Header("SFX")]
	[SerializeField]
	private SFXLocalLoopComponent skipBarSfx;

	[SerializeField]
	private SFXLocalPlayer headAppearSfx;

	private UIColorPalette _colorPalette;

	private LobbySettings _lobbySettings;

	private bool _isSkippable;

	private bool _isSkippableLocal;

	private bool _hasSkipped;

	private Coroutine _skipRoutine;

	private readonly HashSet<NetworkConnectionToClient> _skippedPlayers = new HashSet<NetworkConnectionToClient>();

	private readonly Dictionary<uint, HeadIconView> _headIconViews = new Dictionary<uint, HeadIconView>();

	public UnityAction OnSkipped;

	private void Awake()
	{
		_colorPalette = SushiAndFish.Casino.Original.CasinoResources.Load<UIColorPalette>("ColorSettings");
		_lobbySettings = SushiAndFish.Casino.Original.CasinoResources.Load<LobbySettings>("LobbySettings");
	}

	private void OnEnable()
	{
		InputEvents.OnSkipUIEvent = (Action<bool>)Delegate.Combine(InputEvents.OnSkipUIEvent, new Action<bool>(SkipCredits));
	}

	private void OnDisable()
	{
		InputEvents.OnSkipUIEvent = (Action<bool>)Delegate.Remove(InputEvents.OnSkipUIEvent, new Action<bool>(SkipCredits));
	}

	private void SkipCredits(bool isPressed)
	{
		if (_hasSkipped || !_isSkippableLocal)
		{
			return;
		}
		if (_skipRoutine != null)
		{
			StopCoroutine(_skipRoutine);
		}
		if (isPressed)
		{
			_skipRoutine = StartCoroutine(SkipRoutine());
			return;
		}
		skipFillImage.fillAmount = 0f;
		if (skipBarSfx != null)
		{
			skipBarSfx.LoopSFX(play: false);
		}
	}

	private IEnumerator SkipRoutine()
	{
		if (skipBarSfx != null)
		{
			skipBarSfx.LoopSFX(play: true);
		}
		float t = 0f;
		while (t < skipTime)
		{
			yield return null;
			t += Time.deltaTime;
			skipFillImage.fillAmount = t / skipTime;
		}
		OnSkipFilled();
	}

	private void OnSkipFilled()
	{
		_hasSkipped = true;
		if (skipBarSfx != null)
		{
			skipBarSfx.LoopSFX(play: false);
		}
		RequestSkip();
	}

	[Server]
	public void ServerRegisterSkipFromConnection(NetworkConnectionToClient conn)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void SkipUI::ServerRegisterSkipFromConnection(Mirror.NetworkConnectionToClient)' called when server was not active");
		}
		else if (_isSkippable && _skippedPlayers.Add(conn))
		{
			RpcMarkPlayerSkipped(conn.identity.netId);
			if (_skippedPlayers.Count >= MonoSingleton<LocalManager>.Instance.players.Count)
			{
				ServerSkip();
			}
		}
	}

	[Command(requiresAuthority = false)]
	private void RequestSkip(NetworkConnectionToClient sender = null)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendCommandInternal("System.Void SkipUI::RequestSkip(Mirror.NetworkConnectionToClient)", -1245224911, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	private void ServerSkip()
	{
		_isSkippable = false;
		NetworkSingleton<GameManager>.Instance.ProgressGame();
		RpcSkip();
	}

	[ClientRpc]
	private void RpcSkip()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void SkipUI::RpcSkip()", 1253806432, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcMarkPlayerSkipped(uint id)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarUInt(id);
		SendRPCInternal("System.Void SkipUI::RpcMarkPlayerSkipped(System.UInt32)", 977291795, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public void Reset()
	{
		_isSkippable = false;
		_isSkippableLocal = false;
		_hasSkipped = false;
		_skippedPlayers.Clear();
		if ((bool)skippedHeadsRoot)
		{
			for (int num = skippedHeadsRoot.childCount - 1; num >= 0; num--)
			{
				UnityEngine.Object.Destroy(skippedHeadsRoot.GetChild(num).gameObject);
			}
		}
		_headIconViews.Clear();
		skipFillImage.fillAmount = 0f;
		canvasGroup.alpha = 0f;
	}

	public void SetSkippableServer()
	{
		if (base.isServer && !_isSkippable)
		{
			_isSkippable = true;
			RpcSetSkippableUI();
		}
	}

	[ClientRpc]
	private void RpcSetSkippableUI()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void SkipUI::RpcSetSkippableUI()", 1580893612, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public void SetSkippableForLocal()
	{
		canvasGroup.DOFade(1f, fadeTime);
		_isSkippableLocal = true;
	}

	private void BuildHeadIcons()
	{
		for (int num = skippedHeadsRoot.childCount - 1; num >= 0; num--)
		{
			UnityEngine.Object.Destroy(skippedHeadsRoot.GetChild(num).gameObject);
		}
		_headIconViews.Clear();
		foreach (PlayerReferences player in MonoSingleton<LocalManager>.Instance.players)
		{
			if (!(player?.identity == null))
			{
				GameObject obj = UnityEngine.Object.Instantiate(headIconPrefab, skippedHeadsRoot);
				Image component = obj.GetComponent<Image>();
				CanvasGroup componentInChildren = obj.GetComponentInChildren<CanvasGroup>(includeInactive: true);
				HeadIconView headIconView = new HeadIconView
				{
					Image = component,
					CanvasGroup = componentInChildren
				};
				if ((bool)headIconView.Image)
				{
					Color playerColor = GetPlayerColor(player);
					playerColor.a = 1f;
					headIconView.Image.color = playerColor;
				}
				headIconView.SetSkipped(skipped: false, fadedAlpha);
				_headIconViews[player.identity.netId] = headIconView;
			}
		}
	}

	private Color GetPlayerColor(PlayerReferences player)
	{
		if (!(player?.profile))
		{
			if (!_colorPalette)
			{
				return Color.white;
			}
			return _colorPalette.playerColor;
		}
		PlayerInfo obj = (_lobbySettings ? _lobbySettings.GetPlayerBySteamId(player.profile.steamId) : null);
		if (obj == null)
		{
			if (!_colorPalette)
			{
				return Color.white;
			}
			return _colorPalette.playerColor;
		}
		return obj.playerColor;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RequestSkip__NetworkConnectionToClient(NetworkConnectionToClient sender)
	{
		if (_isSkippable && _skippedPlayers.Add(sender))
		{
			RpcMarkPlayerSkipped(sender.identity.netId);
			if (_skippedPlayers.Count >= MonoSingleton<LocalManager>.Instance.players.Count)
			{
				ServerSkip();
			}
		}
	}

	protected static void InvokeUserCode_RequestSkip__NetworkConnectionToClient(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command RequestSkip called on client.");
		}
		else
		{
			((SkipUI)obj).UserCode_RequestSkip__NetworkConnectionToClient(senderConnection);
		}
	}

	protected void UserCode_RpcSkip()
	{
		OnSkipped?.Invoke();
		canvasGroup.DOFade(0f, fadeTime);
	}

	protected static void InvokeUserCode_RpcSkip(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSkip called on server.");
		}
		else
		{
			((SkipUI)obj).UserCode_RpcSkip();
		}
	}

	protected void UserCode_RpcMarkPlayerSkipped__UInt32(uint id)
	{
		if (_headIconViews.TryGetValue(id, out var value))
		{
			value.SetSkipped(skipped: true, fadedAlpha);
			if (headAppearSfx != null)
			{
				headAppearSfx.PlayOneShotWith3DPos();
			}
		}
	}

	protected static void InvokeUserCode_RpcMarkPlayerSkipped__UInt32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcMarkPlayerSkipped called on server.");
		}
		else
		{
			((SkipUI)obj).UserCode_RpcMarkPlayerSkipped__UInt32(reader.ReadVarUInt());
		}
	}

	protected void UserCode_RpcSetSkippableUI()
	{
		BuildHeadIcons();
	}

	protected static void InvokeUserCode_RpcSetSkippableUI(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetSkippableUI called on server.");
		}
		else
		{
			((SkipUI)obj).UserCode_RpcSetSkippableUI();
		}
	}

	static SkipUI()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(SkipUI), "System.Void SkipUI::RequestSkip(Mirror.NetworkConnectionToClient)", InvokeUserCode_RequestSkip__NetworkConnectionToClient, requiresAuthority: false);
		RemoteProcedureCalls.RegisterRpc(typeof(SkipUI), "System.Void SkipUI::RpcSkip()", InvokeUserCode_RpcSkip);
		RemoteProcedureCalls.RegisterRpc(typeof(SkipUI), "System.Void SkipUI::RpcMarkPlayerSkipped(System.UInt32)", InvokeUserCode_RpcMarkPlayerSkipped__UInt32);
		RemoteProcedureCalls.RegisterRpc(typeof(SkipUI), "System.Void SkipUI::RpcSetSkippableUI()", InvokeUserCode_RpcSetSkippableUI);
	}
}

}
