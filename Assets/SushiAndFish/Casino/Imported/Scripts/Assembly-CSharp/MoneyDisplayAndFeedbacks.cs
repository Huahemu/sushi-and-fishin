namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::System.Linq;
using global::DG.Tweening;
using SushiAndFish.Casino.Original.Extensions;
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::MoreMountains.Feedbacks;
using global::TMPro;
using global::UnityEngine;
using global::UnityEngine.UI;

public class MoneyDisplayAndFeedbacks : NetworkSingleton<MoneyDisplayAndFeedbacks>
{
	private class HistoryEntry
	{
		public Transform root;

		public TextMeshProUGUI profitText;

		public TextMeshProUGUI nameText;

		public double displayedValue;

		public Tween tween;
	}

	private class ActiveMoneyChange
	{
		public MoneyChangeText instance;

		public double displayedValue;

		public double targetValue;

		public Tween valueTween;

		public Tween hideTween;
	}

	[Header("References")]
	[SerializeField]
	private TextMeshProUGUI ticketBalanceLabel;

	[SerializeField]
	private TextMeshProUGUI balanceLabel;

	[SerializeField]
	private TextMeshProUGUI quotaLabel;

	[Header("Feedbacks (Visual)")]
	[Tooltip("Prefabs are instantiated under this transform for the display duration, then destroyed.")]
	[SerializeField]
	private Transform feedbackParent;

	[SerializeField]
	private MMF_Player onBalanceChangedFeedbacks;

	[SerializeField]
	private MoneyChangeText moneyChangePrefab;

	[SerializeField]
	private float disappearTime = 1f;

	[Tooltip("Only show money change popup when |change| is at least this fraction of current quota (e.g. 0.05 = 5%). Big wins/losses only.")]
	[SerializeField]
	[Range(0f, 1f)]
	private float minQuotaFractionToShow = 0.05f;

	[Header("Feedbacks (Audio)")]
	[SerializeField]
	private EventReference sfxMoneyUp;

	[SerializeField]
	private EventReference sfxMoneyDown;

	[SerializeField]
	private EventReference sfxMoneyCountUp;

	[SerializeField]
	private EventReference sfxMoneyCountDown;

	[Header("Profit History")]
	[SerializeField]
	private Transform playerHistoryContainer;

	[SerializeField]
	private Transform playerHistoryEntryPrefab;

	private readonly Dictionary<string, HistoryEntry> _historyEntries = new Dictionary<string, HistoryEntry>();

	private LobbySettings _lobbySettings;

	[Header("Money lerp")]
	[SerializeField]
	private float moneyLerpDuration = 0.5f;

	[SerializeField]
	private float goalReachedPunchScale = 0.15f;

	[SerializeField]
	private float goalReachedPunchDuration = 0.25f;

	private double _displayedTicketBalance;

	private Tween _ticketTween;

	private double _displayedBalance;

	private double _displayedQuota;

	private Tween _balanceTween;

	private Tween _quotaTween;

	public readonly SyncDictionary<string, long> PlayerProfitHistory = new SyncDictionary<string, long>();

	private bool _suppressProfitHistoryUi;

	private readonly Dictionary<string, ActiveMoneyChange> _activeMoneyChanges = new Dictionary<string, ActiveMoneyChange>();

	public override void OnStartClient()
	{
		base.OnStartClient();
		SyncDictionary<string, long> playerProfitHistory = PlayerProfitHistory;
		playerProfitHistory.OnChange = (Action<SyncIDictionary<string, long>.Operation, string, long>)Delegate.Remove(playerProfitHistory.OnChange, new Action<SyncIDictionary<string, long>.Operation, string, long>(OnProfitHistoryChanged));
		SyncDictionary<string, long> playerProfitHistory2 = PlayerProfitHistory;
		playerProfitHistory2.OnChange = (Action<SyncIDictionary<string, long>.Operation, string, long>)Delegate.Combine(playerProfitHistory2.OnChange, new Action<SyncIDictionary<string, long>.Operation, string, long>(OnProfitHistoryChanged));
		UpdatePlayerHistoryLabel();
	}

	private void OnEnable()
	{
		SubscribeEvents();
		SubscribeLobbyEvents();
	}

	private void OnDisable()
	{
		UnsubscribeEvents();
		UnsubscribeLobbyEvents();
	}

	private void Start()
	{
		_displayedTicketBalance = NetworkSingleton<MoneyManager>.Instance.ticketBalance;
		ticketBalanceLabel.text = ((long)_displayedTicketBalance).ToString("N0");
		_displayedBalance = NetworkSingleton<MoneyManager>.Instance.balance;
		_displayedQuota = ((NetworkSingleton<GameManager>.Instance != null) ? NetworkSingleton<GameManager>.Instance.currentQuota : 0);
		if (balanceLabel != null)
		{
			balanceLabel.text = MoneyFormatter.FormatWithDollar((long)_displayedBalance);
		}
		if (quotaLabel != null)
		{
			quotaLabel.text = "/ " + MoneyFormatter.FormatWithDollar((long)_displayedQuota);
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		DOTween.Kill(this);
	}

	private static void SetProfitEntryText(HistoryEntry entry)
	{
		long num = (long)entry.displayedValue;
		entry.profitText.text = ((num >= 0) ? ("+" + MoneyFormatter.FormatWithDollar(num)) : MoneyFormatter.FormatWithDollar(num));
		entry.profitText.color = ((num >= 0) ? Color.green : Color.red);
	}

	private void PlayGoalReachedScale(Transform target)
	{
		if (!(target == null))
		{
			target.DOPunchScale(Vector3.one * goalReachedPunchScale, goalReachedPunchDuration).SetTarget(this);
		}
	}

	private void SubscribeEvents()
	{
		if (NetworkSingleton<MoneyManager>.Instance != null)
		{
			MoneyManager instance = NetworkSingleton<MoneyManager>.Instance;
			instance.OnBalanceChanged = (Action<BalanceChangeData>)Delegate.Combine(instance.OnBalanceChanged, new Action<BalanceChangeData>(UpdateDisplay));
			MoneyManager instance2 = NetworkSingleton<MoneyManager>.Instance;
			instance2.OnTicketBalanceChanged = (Action<long>)Delegate.Combine(instance2.OnTicketBalanceChanged, new Action<long>(UpdateTicketDisplay));
		}
		if (NetworkSingleton<GameManager>.Instance != null)
		{
			NetworkSingleton<GameManager>.Instance.OnQuotaChangedEvent += OnQuotaChanged;
		}
	}

	private void UnsubscribeEvents()
	{
		if (NetworkSingleton<MoneyManager>.Instance != null)
		{
			MoneyManager instance = NetworkSingleton<MoneyManager>.Instance;
			instance.OnBalanceChanged = (Action<BalanceChangeData>)Delegate.Remove(instance.OnBalanceChanged, new Action<BalanceChangeData>(UpdateDisplay));
			MoneyManager instance2 = NetworkSingleton<MoneyManager>.Instance;
			instance2.OnTicketBalanceChanged = (Action<long>)Delegate.Remove(instance2.OnTicketBalanceChanged, new Action<long>(UpdateTicketDisplay));
		}
		if (NetworkSingleton<GameManager>.Instance != null)
		{
			NetworkSingleton<GameManager>.Instance.OnQuotaChangedEvent -= OnQuotaChanged;
		}
	}

	private void SubscribeLobbyEvents()
	{
		if (_lobbySettings == null)
		{
			_lobbySettings = SushiAndFish.Casino.Original.CasinoResources.Load<LobbySettings>("LobbySettings");
		}
		LobbySettings.SettingsChanged -= OnLobbySettingsChanged;
		LobbySettings.SettingsChanged += OnLobbySettingsChanged;
	}

	private void UnsubscribeLobbyEvents()
	{
		LobbySettings.SettingsChanged -= OnLobbySettingsChanged;
	}

	private void OnLobbySettingsChanged(LobbySettings _)
	{
		UpdatePlayerHistoryLabel();
	}

	private void OnProfitHistoryChanged(SyncIDictionary<string, long>.Operation op, string key, long value)
	{
		UpdatePlayerHistoryLabel();
	}

	private void UpdateDisplay(BalanceChangeData balanceChangeData)
	{
		AddEntryToPlayerHistory(balanceChangeData);
		PlayFeedbacks(balanceChangeData);
		LerpBalance();
	}

	private void LerpBalance()
	{
		_balanceTween?.Kill();
		long balance = NetworkSingleton<MoneyManager>.Instance.balance;
		_balanceTween = DOTween.To(() => _displayedBalance, (double x) =>
		{
			_displayedBalance = x;
			if (balanceLabel != null)
			{
				balanceLabel.text = MoneyFormatter.FormatWithDollar((long)x);
			}
		}, balance, moneyLerpDuration).SetTarget(this).OnComplete(() =>
		{
			PlayGoalReachedScale((balanceLabel != null) ? balanceLabel.transform : null);
		});
	}

	private void OnQuotaChanged(long _, long newQuota)
	{
		_quotaTween?.Kill();
		_quotaTween = DOTween.To(() => _displayedQuota, (double x) =>
		{
			_displayedQuota = x;
			if (quotaLabel != null)
			{
				quotaLabel.text = "/ " + MoneyFormatter.FormatWithDollar((long)x);
			}
		}, newQuota, moneyLerpDuration).SetTarget(this).OnComplete(() =>
		{
			PlayGoalReachedScale((quotaLabel != null) ? quotaLabel.transform : null);
		});
	}

	private void UpdateTicketDisplay(long change)
	{
		_ticketTween?.Kill();
		long ticketBalance = NetworkSingleton<MoneyManager>.Instance.ticketBalance;
		_ticketTween = DOTween.To(() => _displayedTicketBalance, (double x) =>
		{
			_displayedTicketBalance = x;
			ticketBalanceLabel.text = ((long)x).ToString("N0");
		}, ticketBalance, moneyLerpDuration).SetTarget(this).OnComplete(() =>
		{
			PlayGoalReachedScale(ticketBalanceLabel.transform);
		});
	}

	private void UpdatePlayerHistoryLabel()
	{
		HashSet<string> currentPlayerNames = GetCurrentPlayerNames();
		HashSet<string> hashSet = new HashSet<string>(currentPlayerNames);
		foreach (string key2 in PlayerProfitHistory.Keys)
		{
			if (!string.IsNullOrWhiteSpace(key2) && currentPlayerNames.Contains(key2))
			{
				hashSet.Add(key2);
			}
		}
		List<KeyValuePair<string, long>> list = (from e in hashSet.Select((string name) =>
			{
				long value2 = 0L;
				PlayerProfitHistory.TryGetValue(name, out value2);
				return new KeyValuePair<string, long>(name, value2);
			})
			orderby e.Value descending
			select e).ToList();
		foreach (KeyValuePair<string, long> item in list)
		{
			string key = item.Key;
			long value = item.Value;
			if (!_historyEntries.TryGetValue(key, out var entry))
			{
				Transform transform = UnityEngine.Object.Instantiate(playerHistoryEntryPrefab, playerHistoryContainer);
				TextMeshProUGUI component = transform.GetChild(0).GetComponent<TextMeshProUGUI>();
				TextMeshProUGUI component2 = transform.GetChild(2).GetComponent<TextMeshProUGUI>();
				entry = new HistoryEntry
				{
					root = transform,
					profitText = component,
					nameText = component2,
					displayedValue = value
				};
				_historyEntries[key] = entry;
				SetProfitEntryText(entry);
			}
			else
			{
				entry.tween?.Kill();
				HistoryEntry entryCopy = entry;
				entry.tween = DOTween.To(() => entry.displayedValue, (double x) =>
				{
					entry.displayedValue = x;
					SetProfitEntryText(entry);
				}, value, moneyLerpDuration).SetTarget(this).OnComplete(() =>
				{
					PlayGoalReachedScale(entryCopy.profitText.transform);
				});
			}
			entry.nameText.text = key;
		}
		HashSet<string> hashSet2 = new HashSet<string>(list.Select((KeyValuePair<string, long> e) => e.Key));
		foreach (string item2 in _historyEntries.Keys.ToList())
		{
			if (!hashSet2.Contains(item2))
			{
				_historyEntries[item2].tween?.Kill();
				UnityEngine.Object.Destroy(_historyEntries[item2].root.gameObject);
				_historyEntries.Remove(item2);
			}
		}
		for (int num = 0; num < list.Count; num++)
		{
			_historyEntries[list[num].Key].root.SetSiblingIndex(num);
		}
	}

	private HashSet<string> GetCurrentPlayerNames()
	{
		if (_lobbySettings == null)
		{
			_lobbySettings = SushiAndFish.Casino.Original.CasinoResources.Load<LobbySettings>("LobbySettings");
		}
		if (_lobbySettings == null || _lobbySettings.players == null)
		{
			return new HashSet<string>();
		}
		return (from p in _lobbySettings.players
			where !string.IsNullOrWhiteSpace(p.playerName)
			select p.playerName).ToHashSet();
	}

	private void AddEntryToPlayerHistory(BalanceChangeData balanceChangeData)
	{
		if (base.isServer && (bool)balanceChangeData.changer && !string.IsNullOrWhiteSpace(balanceChangeData.changer.playerName) && balanceChangeData.changeType != ChangeType.Misc)
		{
			if (PlayerProfitHistory.ContainsKey(balanceChangeData.changer.playerName))
			{
				PlayerProfitHistory[balanceChangeData.changer.playerName] += balanceChangeData.changeAmount;
			}
			else
			{
				PlayerProfitHistory.TryAdd(balanceChangeData.changer.playerName, balanceChangeData.changeAmount);
			}
		}
	}

	private void PlayFeedbacks(BalanceChangeData balanceChangeData)
	{
		long num = ((NetworkSingleton<GameManager>.Instance != null) ? NetworkSingleton<GameManager>.Instance.currentQuota : 0);
		if (num > 0)
		{
			long num2 = (long)((float)num * minQuotaFractionToShow);
			if (Math.Abs(balanceChangeData.changeAmount) >= num2)
			{
				SpawnMoneyChangeText(balanceChangeData);
			}
		}
	}

	private void SpawnMoneyChangeText(BalanceChangeData balanceChangeData)
	{
		if (!balanceChangeData.changer || string.IsNullOrWhiteSpace(balanceChangeData.changer.playerName))
		{
			return;
		}
		string key = (string.IsNullOrEmpty(balanceChangeData.changer.playerName) ? string.Empty : balanceChangeData.changer.playerName);
		if (!_activeMoneyChanges.TryGetValue(key, out var active) || active.instance == null)
		{
			MoneyChangeText instance = UnityEngine.Object.Instantiate(moneyChangePrefab, feedbackParent);
			active = new ActiveMoneyChange
			{
				instance = instance,
				displayedValue = 0.0,
				targetValue = 0.0
			};
			_activeMoneyChanges[key] = active;
		}
		else if (active.hideTween != null && active.hideTween.IsActive())
		{
			active.hideTween.Kill();
			active.hideTween = null;
		}
		active.instance.playerNameText.text = balanceChangeData.changer.playerName;
		active.instance.playerNameText.color = new Color(1f, 1f, 1f, 1f);
		if (!string.IsNullOrEmpty(balanceChangeData.changer.playerName) && active.instance.playerColorImage != null)
		{
			LobbySettings lobbySettings = SushiAndFish.Casino.Original.CasinoResources.Load<LobbySettings>("LobbySettings");
			if (lobbySettings != null)
			{
				PlayerInfo playerInfo = lobbySettings.players.Find((PlayerInfo p) => p.playerName == balanceChangeData.changer.playerName);
				if (playerInfo != null)
				{
					active.instance.playerColorImage.color = playerInfo.playerColor.PastelizeColor(0.6f, 0.3f);
				}
			}
		}
		active.targetValue += balanceChangeData.changeAmount;
		double newTotal = active.targetValue;
		if (active.valueTween != null && active.valueTween.IsActive())
		{
			active.valueTween.Kill();
		}
		SFXManager.SFXOneShot((newTotal > 0.0) ? sfxMoneyCountUp : sfxMoneyCountDown);
		active.valueTween = DOTween.To(() => active.displayedValue, (double x) =>
		{
			active.displayedValue = x;
			long num = (long)x;
			active.instance.changeAmountText.text = ((num >= 0) ? ("+" + MoneyFormatter.FormatWithDollar(num)) : MoneyFormatter.FormatWithDollar(num));
			active.instance.changeAmountText.color = ((num > 0) ? Color.green : ((num < 0) ? Color.red : Color.white));
			active.instance.GetComponent<Image>().color = active.instance.changeAmountText.color;
		}, newTotal, moneyLerpDuration).SetTarget(this).OnComplete(() =>
		{
			SFXManager.SFXOneShot((newTotal > 0.0) ? sfxMoneyUp : sfxMoneyDown);
			active.hideTween = DOVirtual.DelayedCall(disappearTime, () =>
			{
				_activeMoneyChanges.Remove(key);
				if (active.instance != null)
				{
					UnityEngine.Object.Destroy(active.instance.gameObject);
				}
			}).SetTarget(this);
		});
		onBalanceChangedFeedbacks.PlayFeedbacks();
	}

	public Dictionary<string, long> GetProfitHistorySnapshot()
	{
		return new Dictionary<string, long>(PlayerProfitHistory);
	}

	[Server]
	public void SetProfitHistory(Dictionary<string, long> history)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void MoneyDisplayAndFeedbacks::SetProfitHistory(System.Collections.Generic.Dictionary`2<System.String,System.Int64>)' called when server was not active");
			return;
		}
		RpcBeginProfitHistoryBulkSync();
		PlayerProfitHistory.Clear();
		if (history == null)
		{
			RpcEndProfitHistoryBulkSync();
			return;
		}
		foreach (KeyValuePair<string, long> item in history)
		{
			PlayerProfitHistory[item.Key] = item.Value;
		}
		RpcEndProfitHistoryBulkSync();
	}

	[ClientRpc]
	private void RpcBeginProfitHistoryBulkSync()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void MoneyDisplayAndFeedbacks::RpcBeginProfitHistoryBulkSync()", -1089434775, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcEndProfitHistoryBulkSync()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void MoneyDisplayAndFeedbacks::RpcEndProfitHistoryBulkSync()", 1866625689, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public void ServerResetProfitHistory()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void MoneyDisplayAndFeedbacks::ServerResetProfitHistory()' called when server was not active");
			return;
		}
		PlayerProfitHistory.Clear();
		RpcRefreshProfitHistoryLabel();
	}

	[ClientRpc]
	private void RpcRefreshProfitHistoryLabel()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void MoneyDisplayAndFeedbacks::RpcRefreshProfitHistoryLabel()", -2118369112, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public MoneyDisplayAndFeedbacks()
	{
		InitSyncObject(PlayerProfitHistory);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcBeginProfitHistoryBulkSync()
	{
		_suppressProfitHistoryUi = true;
	}

	protected static void InvokeUserCode_RpcBeginProfitHistoryBulkSync(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcBeginProfitHistoryBulkSync called on server.");
		}
		else
		{
			((MoneyDisplayAndFeedbacks)obj).UserCode_RpcBeginProfitHistoryBulkSync();
		}
	}

	protected void UserCode_RpcEndProfitHistoryBulkSync()
	{
		_suppressProfitHistoryUi = false;
		UpdatePlayerHistoryLabel();
	}

	protected static void InvokeUserCode_RpcEndProfitHistoryBulkSync(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcEndProfitHistoryBulkSync called on server.");
		}
		else
		{
			((MoneyDisplayAndFeedbacks)obj).UserCode_RpcEndProfitHistoryBulkSync();
		}
	}

	protected void UserCode_RpcRefreshProfitHistoryLabel()
	{
		UpdatePlayerHistoryLabel();
	}

	protected static void InvokeUserCode_RpcRefreshProfitHistoryLabel(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcRefreshProfitHistoryLabel called on server.");
		}
		else
		{
			((MoneyDisplayAndFeedbacks)obj).UserCode_RpcRefreshProfitHistoryLabel();
		}
	}

	static MoneyDisplayAndFeedbacks()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(MoneyDisplayAndFeedbacks), "System.Void MoneyDisplayAndFeedbacks::RpcBeginProfitHistoryBulkSync()", InvokeUserCode_RpcBeginProfitHistoryBulkSync);
		RemoteProcedureCalls.RegisterRpc(typeof(MoneyDisplayAndFeedbacks), "System.Void MoneyDisplayAndFeedbacks::RpcEndProfitHistoryBulkSync()", InvokeUserCode_RpcEndProfitHistoryBulkSync);
		RemoteProcedureCalls.RegisterRpc(typeof(MoneyDisplayAndFeedbacks), "System.Void MoneyDisplayAndFeedbacks::RpcRefreshProfitHistoryLabel()", InvokeUserCode_RpcRefreshProfitHistoryLabel);
	}
}

}
