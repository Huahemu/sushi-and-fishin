namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::System.Runtime.InteropServices;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::TMPro;
using global::UnityEngine;
using global::UnityEngine.Events;

public partial class GameBase : NetworkBehaviour
{
	[Header("References")]
	public Transform keypadSpawnPoint;

	[SerializeField]
	protected CasinoGameFeedbacks gameFeedbacks;

	[SyncVar]
	public int casinoLevel;

	[SerializeField]
	protected UnityEvent onProfitEvent;

	[SerializeField]
	protected UnityEvent onTieEvent;

	[SerializeField]
	protected UnityEvent onLossEvent;

	[Header("Settings")]
	[SerializeField]
	protected string gameName = "";

	[SerializeField]
	protected CasinoGameType gameType;

	[SerializeField]
	private float estimatedValue = 1f;

	[SerializeField]
	private long baseMinBet = 1L;

	[SerializeField]
	private long baseMaxBet = 100L;

	[Header("Debug")]
	[SyncVar]
	[ReadOnly]
	public long currentBet;

	[SerializeField]
	[ReadOnly]
	protected bool canBet = true;

	[SerializeField]
	[ReadOnly]
	private double maxBetOverrideMultiplier = 1.0;

	[SerializeField]
	[ReadOnly]
	public bool isPlaying;

	[SerializeField]
	[ReadOnly]
	protected PlayerProfile interactingPlayer;

	[SerializeField]
	[ReadOnly]
	protected Keypad keypad;

	[SyncVar]
	[ReadOnly]
	public bool isGoldenChipApplied;

	[ReadOnly]
	public bool isGoldenBet;

	[SerializeField]
	protected int gameTurn;

	[Header("ACCESSIBILITY SETTINGS")]
	[SerializeField]
	private ToggleSettingItem casinoHelper;

	[SerializeField]
	private TextMeshPro[] casinoHelperTexts;

	private GameSettings _gs;

	public string GameName => gameName;

	public CasinoGameType GameType => gameType;

	public long BaseMinBet
	{
		get
		{
			return baseMinBet;
		}
		set
		{
			baseMinBet = value;
			if (base.isServer && (bool)Keypad)
			{
				Keypad.ServerUpdateMinMaxBetText(MinBet, MaxBet);
			}
		}
	}

	public long BaseMaxBet
	{
		get
		{
			return baseMaxBet;
		}
		set
		{
			baseMaxBet = value;
			if (base.isServer && (bool)Keypad)
			{
				Keypad.ServerUpdateMinMaxBetText(MinBet, MaxBet);
			}
		}
	}

	protected double EstimatedValue => ((bool)_gs && _gs.floorData != null && casinoLevel >= 0 && casinoLevel < _gs.floorData.Count && _gs.floorData[casinoLevel] != null) ? (estimatedValue * _gs.floorData[casinoLevel].estimatedValueMultiplier) : estimatedValue;

	public long MinBet
	{
		get
		{
			if (!_gs || !NetworkSingleton<GameManager>.Instance)
			{
				return baseMinBet;
			}
			return Math.Max(1L, (long)Math.Round(FathF.RoundByFirstNDigits((double)baseMinBet * (double)NetworkSingleton<GameManager>.Instance.currentQuota * 0.001 * Math.Pow(2.0, casinoLevel - NetworkSingleton<GameManager>.Instance.currentFloor - 1), 2), MidpointRounding.AwayFromZero));
		}
	}

	public long MaxBet
	{
		get
		{
			if (!_gs || !NetworkSingleton<GameManager>.Instance)
			{
				return baseMaxBet;
			}
			return Math.Max(5L, (long)Math.Round(FathF.RoundByFirstNDigits((double)baseMaxBet * (double)NetworkSingleton<GameManager>.Instance.currentQuota * 0.001 * Math.Pow(2.0, casinoLevel - NetworkSingleton<GameManager>.Instance.currentFloor - 1) * MaxBetOverrideMultiplier, 2), MidpointRounding.AwayFromZero));
		}
	}

	public Keypad Keypad => keypad;

	public double MaxBetOverrideMultiplier
	{
		get
		{
			return maxBetOverrideMultiplier;
		}
		set
		{
			maxBetOverrideMultiplier = value;
			if ((bool)Keypad)
			{
				Keypad.ServerUpdateMinMaxBetText(MinBet, MaxBet);
			}
		}
	}

	protected bool IsCasinoHelperEnabled
	{
		get
		{
			if (casinoHelper != null)
			{
				return casinoHelper.value;
			}
			return false;
		}
	}

	public int NetworkcasinoLevel
	{
		get
		{
			return casinoLevel;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref casinoLevel, 1uL, null);
		}
	}

	public long NetworkcurrentBet
	{
		get
		{
			return currentBet;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref currentBet, 2uL, null);
		}
	}

	public bool NetworkisGoldenChipApplied
	{
		get
		{
			return isGoldenChipApplied;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref isGoldenChipApplied, 4uL, null);
		}
	}

	protected void SetCasinoHelperText(int index, string text)
	{
		if (casinoHelperTexts != null && index >= 0 && index < casinoHelperTexts.Length && !(casinoHelperTexts[index] == null))
		{
			casinoHelperTexts[index].text = text;
		}
	}

	protected void ClearCasinoHelperTexts()
	{
		if (casinoHelperTexts == null)
		{
			return;
		}
		for (int i = 0; i < casinoHelperTexts.Length; i++)
		{
			if (casinoHelperTexts[i] != null)
			{
				casinoHelperTexts[i].text = "";
			}
		}
	}

	private void Awake()
	{
		_gs = SushiAndFish.Casino.Original.CasinoResources.Load<GameSettings>("GameSettings");
		NetworkcurrentBet = 0L;
		isPlaying = false;
		canBet = true;
		OnAwake();
	}

	protected virtual void OnAwake()
	{
	}

	protected virtual void OnDisable()
	{
		if(NativeHasActiveLease)
		{
			NativeRoutineFailed("Original owned machine disabled; retain same pending round without human refund or replay");
			return;
		}
		if (isPlaying)
		{
			NetworkSingleton<MoneyManager>.Instance.TryChangeBalance(currentBet, null, ChangeType.Bet);
			ResetGame();
		}
	}

	public override void OnStartServer()
	{
		base.OnStartServer();
		keypad = UnityEngine.Object.Instantiate(SushiAndFish.Casino.Original.CasinoResources.Load<Keypad>("Keypad"), keypadSpawnPoint.position, keypadSpawnPoint.rotation, keypadSpawnPoint);
		keypad.SetCasinoGame(this);
		NetworkServer.Spawn(keypad.gameObject);
		if ((bool)gameFeedbacks)
		{
			gameFeedbacks = GetComponent<CasinoGameFeedbacks>();
		}
	}

	public override void OnStartClient()
	{
		base.OnStartClient();
		if ((bool)keypad)
		{
			keypad.transform.SetParent(keypadSpawnPoint);
		}
	}

	[Server]
	public void ServerSetBet(long betAmount)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void GameBase::ServerSetBet(System.Int64)' called when server was not active");
		}
		else if (canBet)
		{
			NetworkcurrentBet = betAmount;
			keypad.RpcUpdateDisplay();
			OnBetSet();
		}
	}

	protected virtual void OnBetSet()
	{
	}

	[Server]
	public void ApplyGoldenChip(float multiplier)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void GameBase::ApplyGoldenChip(System.Single)' called when server was not active");
		}
		else
		{
			SetGoldenChip(apply: true, multiplier);
		}
	}

	protected virtual void SetGoldenChip(bool apply, float multiplier = 1f)
	{
		if (isGoldenChipApplied != apply)
		{
			NetworkisGoldenChipApplied = apply;
			if (!apply)
			{
				isGoldenBet = false;
			}
			keypad.SetGoldenChip(apply, multiplier);
		}
	}

	[Server]
	public virtual void TryStartGame(PlayerInteract playerInteract)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void GameBase::TryStartGame(PlayerInteract)' called when server was not active");
		}
		else
		{
			if (!CanGameStart() || isPlaying)
			{
				return;
			}
			if (!NativeAllowOriginalHumanStart()) return;
			if (playerInteract.TryGetComponent<PlayerProfile>(out var component))
			{
				interactingPlayer = component;
			}
			if (!isGoldenChipApplied)
			{
				if (gameType != CasinoGameType.CoinFlip && (currentBet < MinBet || currentBet > MaxBet || !NetworkSingleton<MoneyManager>.Instance.TryChangeBalance(-currentBet, interactingPlayer, ChangeType.Bet)))
				{
					keypad.ServerInvalidBetAmountFb();
					return;
				}
			}
			else
			{
				isGoldenBet = true;
			}
			NativeOnOriginalBetAccepted();
			isPlaying = true;
			canBet = false;
			StartGame();
		}
	}

	[Server]
	protected virtual void StartGame()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void GameBase::StartGame()' called when server was not active");
		}
	}

	protected virtual void ResetGame()
	{
		isPlaying = false;
		canBet = true;
		if (isGoldenBet)
		{
			SetGoldenChip(apply: false);
		}
		NetworkcurrentBet = keypad.GetCurrentInput();
		gameTurn++;
	}

	protected global::System.Random GetSeededRandom(int additionalContext = 0)
	{
		if (NetworkSingleton<SeededRandomManager>.Instance == null || NetworkSingleton<GameManager>.Instance == null)
		{
			return new global::System.Random(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
		}
		int currentSeed = NetworkSingleton<SeededRandomManager>.Instance.CurrentSeed;
		int daysPassed = NetworkSingleton<GameManager>.Instance.daysPassed;
		Vector3 position = base.transform.position;
		long num = (((((currentSeed * 2654435761u + daysPassed) ^ (gameTurn * 2246822519u)) * 2654435761u + (int)(position.x * 1000f)) ^ ((int)(position.y * 1000f) * 2246822519u)) * 2654435761u + (int)(position.z * 1000f)) ^ (additionalContext * 2246822519u);
		long num2 = (num ^ (num >> 32)) * 2246822507u;
		long num3 = (num2 ^ (num2 >> 16)) * 3266489917u;
		return new global::System.Random((int)(num3 ^ (num3 >> 13)));
	}

	[Server]
	protected virtual void Payout(double multiplier, ChangeType changeType = ChangeType.GameResult, Dictionary<string, object> gameSpecificData = null, long bet = -1L)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void GameBase::Payout(System.Double,ChangeType,System.Collections.Generic.Dictionary`2<System.String,System.Object>,System.Int64)' called when server was not active");
		}
		else
		{
			using var nativeScope=NativePayoutScope();
			using var nativeCommit=NativeHasActiveLease?SushiAndFish.FriendsLife.FriendsEconomyService.Instance.DeferExternalCommit():null;
			if (!interactingPlayer.TryGetComponent<PlayerBuff>(out var component))
			{
				return;
			}
			if (bet < 0)
			{
				bet = currentBet;
			}
			double num = component.GetValue(PlayerBuffType.TipsyFortune);
			double num2 = component.GetValue(PlayerBuffType.InspiringMelody);
			double num3 = component.GetValue(PlayerBuffType.Immunity);
			long num4 = (long)Math.Round(multiplier * (double)bet);
			long num5 = num4 - bet;
			if (num5 > 0)
			{
				float upgradeData = NetworkSingleton<UpgradeManager>.Instance.GetUpgradeData(interactingPlayer.steamId, PlayerUpgradeType.GamblersConfidence);
				num4 = bet + (long)Math.Round((double)num5 * num * (1.0 + num2) * (double)upgradeData);
				float upgradeData2 = NetworkSingleton<UpgradeManager>.Instance.GetUpgradeData(interactingPlayer.steamId, PlayerUpgradeType.BonusDraw);
				if (GetSeededRandom().NextDouble() <= (double)upgradeData2)
				{
					NetworkSingleton<MoneyManager>.Instance.TryChangeTicketBalance(1L);
				}
			}
			else if (num5 < 0)
			{
				if (num3 > 0.0)
				{
					num4 = bet;
				}
				else
				{
					long num6 = -num5;
					long num7 = bet - num6;
					double num8 = Mathf.Clamp01(NetworkSingleton<UpgradeManager>.Instance.GetUpgradeData(interactingPlayer.steamId, PlayerUpgradeType.Insurance) + component.GetValue(PlayerBuffType.InspiringMelody));
					num4 = num7 + (long)Math.Round((double)num6 * num8);
				}
			}
			else
			{
				num4 = bet;
			}
			if (num4 > 0)
			{
				bool paid=NetworkSingleton<MoneyManager>.Instance.TryChangeBalance(num4, interactingPlayer, changeType);
				if(NativeHasActiveLease&&!paid)throw new InvalidOperationException("Original native payout wallet commit refused");
			}
			if(NativeHasActiveLease&&num4==0&&!NetworkSingleton<MoneyManager>.Instance.TryChangeBalance(0,interactingPlayer,changeType))throw new InvalidOperationException("Native zero result receipt refused");
			if(!NativeRecordSourceResult(bet,num4,num>1.0,num2>0.0,num3>0.0,gameSpecificData))
			NetworkSingleton<GameResultsManager>.Instance.RegisterResult(bet, num4, interactingPlayer, gameType, base.transform.position, num > 1.0, num2 > 0.0, num3 > 0.0, gameSpecificData);
			if (!NativeHasActiveLease && (bool)NetworkSingleton<AnalyticsManager>.Instance && (bool)interactingPlayer && !Application.isEditor)
			{
				NetworkSingleton<AnalyticsManager>.Instance.SendAnalytics(this, interactingPlayer.playerName, bet, (long)Math.Round((double)bet * multiplier));
			}
			RpcEndGameFeedBacks(multiplier);
			NativeOnOriginalPayoutComplete();
		}
	}

	[ClientRpc]
	private void RpcEndGameFeedBacks(double multiplier)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteDouble(multiplier);
		SendRPCInternal("System.Void GameBase::RpcEndGameFeedBacks(System.Double)", 673425954, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	protected virtual bool CanGameStart()
	{
		return true;
	}

	public void SetEstimatedValue(float value)
	{
		estimatedValue = value;
	}

	[Server]
	public void ServerSimulatePayout(PlayerProfile player, bool isWin)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void GameBase::ServerSimulatePayout(PlayerProfile,System.Boolean)' called when server was not active");
			return;
		}
		if (player == null)
		{
			Debug.LogWarning("[GameBase] Cannot simulate payout: PlayerProfile is null");
			return;
		}
		interactingPlayer = player;
		if (currentBet <= 0)
		{
			NetworkcurrentBet = (long)Math.Round((double)(MinBet + MaxBet) / 2.0);
			if (currentBet < MinBet)
			{
				NetworkcurrentBet = MinBet;
			}
		}
		if (!NetworkSingleton<MoneyManager>.Instance.TryChangeBalance(-currentBet, player, ChangeType.Bet))
		{
			Debug.LogWarning($"[GameBase] Cannot simulate payout: Failed to deduct bet of {currentBet}");
		}
		else
		{
			Payout(isWin ? 2 : 0, ChangeType.GameResult, null, -1L);
		}
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcEndGameFeedBacks__Double(double multiplier)
	{
		gameFeedbacks.PlayGameResultFeedback(multiplier);
		if (multiplier > 1.0)
		{
			onProfitEvent?.Invoke();
		}
		else if (multiplier < 1.0)
		{
			onLossEvent?.Invoke();
		}
		else
		{
			onTieEvent?.Invoke();
		}
	}

	protected static void InvokeUserCode_RpcEndGameFeedBacks__Double(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcEndGameFeedBacks called on server.");
		}
		else
		{
			((GameBase)obj).UserCode_RpcEndGameFeedBacks__Double(reader.ReadDouble());
		}
	}

	static GameBase()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(GameBase), "System.Void GameBase::RpcEndGameFeedBacks(System.Double)", InvokeUserCode_RpcEndGameFeedBacks__Double);
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			writer.WriteVarInt(casinoLevel);
			writer.WriteVarLong(currentBet);
			writer.WriteBool(isGoldenChipApplied);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 1L) != 0L)
		{
			writer.WriteVarInt(casinoLevel);
		}
		if ((syncVarDirtyBits & 2L) != 0L)
		{
			writer.WriteVarLong(currentBet);
		}
		if ((syncVarDirtyBits & 4L) != 0L)
		{
			writer.WriteBool(isGoldenChipApplied);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref casinoLevel, null, reader.ReadVarInt());
			GeneratedSyncVarDeserialize(ref currentBet, null, reader.ReadVarLong());
			GeneratedSyncVarDeserialize(ref isGoldenChipApplied, null, reader.ReadBool());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 1L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref casinoLevel, null, reader.ReadVarInt());
		}
		if ((num & 2L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref currentBet, null, reader.ReadVarLong());
		}
		if ((num & 4L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref isGoldenChipApplied, null, reader.ReadBool());
		}
	}
}

}
