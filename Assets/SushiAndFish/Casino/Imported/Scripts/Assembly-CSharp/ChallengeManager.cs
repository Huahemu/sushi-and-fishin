namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Linq;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class ChallengeManager : NetworkSingleton<ChallengeManager>
{
	[Header("Challenge Settings")]
	[Tooltip("List of all available challenges (auto-populated from ChallengeSettings)")]
	[SerializeField]
	private List<Challenge> allChallenges = new List<Challenge>();

	[Tooltip("Whether challenges are enabled")]
	[SerializeField]
	private bool challengesEnabled = true;

	[Tooltip("Whether challenges can be completed (progress tracking still works even if false)")]
	[SerializeField]
	private bool challengesCanComplete = true;

	private Dictionary<Challenge, ChallengeProgress> activeChallenges = new Dictionary<Challenge, ChallengeProgress>();

	private ConditionStateTracker conditionStateTracker = new ConditionStateTracker();

	private ChallengeSettings _challengeSettings;

	[Header("Challenge UI")]
	[Tooltip("Parent transform that receives spawned challenge UI entries")]
	[SerializeField]
	private Transform activeChallengeListParent;

	[Tooltip("Prefab with a ChallengeEntryUI component")]
	[SerializeField]
	private ChallengeEntryUI activeChallengeEntryPrefab;

	[Header("SFX")]
	[SerializeField]
	private SFXComponent newChallengeSfx;

	[SerializeField]
	private SFXComponent challengeCompleteSfx;

	private readonly Dictionary<int, ChallengeEntryUI> activeChallengeEntries = new Dictionary<int, ChallengeEntryUI>();

	private readonly HashSet<int> completedChallengeEntryIds = new HashSet<int>();

	public event Action<Challenge> OnChallengeCompleted;

	public event Action<Challenge> OnChallengeProgressUpdated;

	protected override void OnAwake()
	{
		base.OnAwake();
		_challengeSettings = SushiAndFish.Casino.Original.CasinoResources.Load<ChallengeSettings>("ChallengeSettings");
		LoadChallengesFromSettings();
	}

	public override void OnStartServer()
	{
		base.OnStartServer();
		SubscribeToGameResults();
	}

	public override void OnStartClient()
	{
		base.OnStartClient();
		if (base.isServer)
		{
			UpdateActiveChallengesDisplay();
		}
		else
		{
			CmdRequestChallengesSync();
		}
	}

	private void LoadChallengesFromSettings()
	{
		allChallenges = new List<Challenge>(_challengeSettings.challenges);
		Debug.Log($"[ChallengeManager] Loaded {allChallenges.Count} challenges from ChallengeSettings");
	}

	private void OnEnable()
	{
		SubscribeToGameResults();
	}

	private void SubscribeToGameResults()
	{
		if (NetworkSingleton<GameResultsManager>.Instance != null)
		{
			NetworkSingleton<GameResultsManager>.Instance.OnResultRegistered -= OnGameResultRegistered;
			NetworkSingleton<GameResultsManager>.Instance.OnResultRegistered += OnGameResultRegistered;
		}
	}

	private void OnDisable()
	{
		if (NetworkSingleton<GameResultsManager>.Instance != null)
		{
			NetworkSingleton<GameResultsManager>.Instance.OnResultRegistered -= OnGameResultRegistered;
		}
		if (_challengeSettings != null)
		{
			_challengeSettings.activeChallenges.Clear();
		}
	}

	public List<ChallengeProgress> GetActiveChallenges()
	{
		return activeChallenges.Values.ToList();
	}

	public ChallengeProgress GetChallengeProgress(Challenge challenge)
	{
		if (challenge == null)
		{
			return null;
		}
		if (!activeChallenges.ContainsKey(challenge))
		{
			return null;
		}
		return activeChallenges[challenge];
	}

	public bool IsChallengeActive(Challenge challenge)
	{
		if (challenge != null)
		{
			return activeChallenges.ContainsKey(challenge);
		}
		return false;
	}

	[Server]
	public void ClaimChallengeReward(Challenge challenge)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void ChallengeManager::ClaimChallengeReward(Challenge)' called when server was not active");
		}
		else
		{
			if (challenge == null || !activeChallenges.ContainsKey(challenge))
			{
				return;
			}
			ChallengeProgress challengeProgress = activeChallenges[challenge];
			if (!challengeProgress.isCompleted || challengeProgress.isClaimed)
			{
				return;
			}
			challengeProgress.isClaimed = true;
			int ticketReward = challenge.GetTicketReward();
			if (ticketReward > 0)
			{
				NetworkSingleton<MoneyManager>.Instance.TryChangeTicketBalance(ticketReward);
				Debug.Log($"[ChallengeManager] Automatically received {ticketReward} tickets for completing challenge: {challenge.challengeName}");
				RpcNotifyChallengeRewardAwarded(challenge.challengeName, ticketReward);
			}
			challenge.ResetChallenge();
			if (challenge.conditions != null)
			{
				foreach (ChallengeConditionData condition in challenge.conditions)
				{
					if (condition != null)
					{
						condition.ResetCondition();
						conditionStateTracker.GetOrCreateState(condition)?.Reset();
					}
				}
			}
			activeChallenges.Remove(challenge);
			Debug.Log("[ChallengeManager] Challenge " + challenge.challengeName + " reset and removed. Can be purchased again.");
			UpdateChallenges();
		}
	}

	[ClientRpc]
	private void RpcNotifyChallengeRewardAwarded(string challengeName, int ticketReward)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteString(challengeName);
		writer.WriteVarInt(ticketReward);
		SendRPCInternal("System.Void ChallengeManager::RpcNotifyChallengeRewardAwarded(System.String,System.Int32)", -1363418638, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	private void OnGameResultRegistered(long bet, long payout, PlayerProfile playerProfile, CasinoGameType gameType, Vector3 position, bool hadTipsyFortune, bool hadInspiringMelody, bool hadImmunity, Dictionary<string, object> gameSpecificData)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void ChallengeManager::OnGameResultRegistered(System.Int64,System.Int64,PlayerProfile,CasinoGameType,UnityEngine.Vector3,System.Boolean,System.Boolean,System.Boolean,System.Collections.Generic.Dictionary`2<System.String,System.Object>)' called when server was not active");
		}
		else
		{
			if (!challengesEnabled)
			{
				return;
			}
			ChallengeContext challengeContext = new ChallengeContext
			{
				bet = bet,
				payout = payout,
				gameType = gameType,
				gamePosition = position,
				hadTipsyFortuneBuff = hadTipsyFortune,
				hadInspiringMelodyBuff = hadInspiringMelody,
				hadImmunityBuff = hadImmunity,
				gameSpecificData = (gameSpecificData ?? new Dictionary<string, object>())
			};
			foreach (KeyValuePair<Challenge, ChallengeProgress> item in new List<KeyValuePair<Challenge, ChallengeProgress>>(activeChallenges))
			{
				Challenge key = item.Key;
				ChallengeProgress value = item.Value;
				if (!(key == null) && activeChallenges.ContainsKey(key))
				{
					challengeContext.quotaAtActivation = value.quotaAtActivation;
					key.OnGameResult(bet, payout, gameType, position, challengeContext);
					bool isCompleted = value.isCompleted;
					value.UpdateProgress(challengeContext);
					if (value.isCompleted && !isCompleted && challengesCanComplete)
					{
						OnChallengeCompleted?.Invoke(key);
						RpcNotifyChallengeCompleted(key.challengeName, value.progress, value.progressText);
						ClaimChallengeReward(key);
					}
					if (activeChallenges.ContainsKey(key))
					{
						OnChallengeProgressUpdated?.Invoke(key);
					}
				}
			}
			UpdateChallenges();
		}
	}

	[ClientRpc]
	private void RpcNotifyChallengeCompleted(string challengeName, float progress01, string progressText)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteString(challengeName);
		writer.WriteFloat(progress01);
		writer.WriteString(progressText);
		SendRPCInternal("System.Void ChallengeManager::RpcNotifyChallengeCompleted(System.String,System.Single,System.String)", -878636952, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public void UpdateChallengeProgress()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void ChallengeManager::UpdateChallengeProgress()' called when server was not active");
		}
		else
		{
			if (!challengesEnabled)
			{
				return;
			}
			ChallengeContext context = new ChallengeContext
			{
				bet = 0L,
				payout = 0L,
				gameType = CasinoGameType.Blackjack,
				gamePosition = Vector3.zero,
				hadTipsyFortuneBuff = false,
				hadInspiringMelodyBuff = false,
				hadImmunityBuff = false
			};
			PlayerProfile[] array = UnityEngine.Object.FindObjectsByType<PlayerProfile>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
			if (array.Length != 0)
			{
				_ = array[0];
			}
			foreach (KeyValuePair<Challenge, ChallengeProgress> activeChallenge in activeChallenges)
			{
				Challenge key = activeChallenge.Key;
				ChallengeProgress value = activeChallenge.Value;
				if (!(key == null))
				{
					bool isCompleted = value.isCompleted;
					value.UpdateProgress(context);
					if (value.isCompleted && !isCompleted && challengesCanComplete)
					{
						OnChallengeCompleted?.Invoke(key);
						RpcNotifyChallengeCompleted(key.challengeName, value.progress, value.progressText);
					}
					OnChallengeProgressUpdated?.Invoke(key);
				}
			}
			UpdateChallenges();
		}
	}

	[Server]
	public void ResetAllChallenges()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void ChallengeManager::ResetAllChallenges()' called when server was not active");
			return;
		}
		foreach (KeyValuePair<Challenge, ChallengeProgress> activeChallenge in activeChallenges)
		{
			Challenge key = activeChallenge.Key;
			ChallengeProgress value = activeChallenge.Value;
			if (!(key == null))
			{
				key.ResetChallenge();
				value.Reset();
			}
		}
		conditionStateTracker.ResetAll();
	}

	[Server]
	public void ServerResetAllChallenges()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void ChallengeManager::ServerResetAllChallenges()' called when server was not active");
			return;
		}
		ResetAllChallenges();
		activeChallenges.Clear();
		UpdateChallenges();
	}

	[Server]
	public bool ServerActivateChallengeById(int challengeId)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Boolean ChallengeManager::ServerActivateChallengeById(System.Int32)' called when server was not active");
			return default;
		}
		Challenge challenge = allChallenges.FirstOrDefault((Challenge x) => x != null && x.challengeID == challengeId);
		if (challenge == null)
		{
			return false;
		}
		return ActivateChallenge(challenge);
	}

	[Server]
	public void ServerCompleteAllActiveChallenges()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void ChallengeManager::ServerCompleteAllActiveChallenges()' called when server was not active");
			return;
		}
		foreach (ChallengeProgress item in GetActiveChallenges().ToList())
		{
			if (!(item?.challenge == null) && !item.isClaimed)
			{
				Challenge challenge = item.challenge;
				item.isCompleted = true;
				item.progress = 1f;
				ChallengeContext context = new ChallengeContext
				{
					bet = item.lastBet,
					payout = item.lastPayout,
					gameType = item.lastGameType,
					gamePosition = Vector3.zero,
					quotaAtActivation = item.quotaAtActivation,
					hadTipsyFortuneBuff = false,
					hadInspiringMelodyBuff = false,
					hadImmunityBuff = false
				};
				item.progressText = challenge.GetProgressText(context);
				RpcNotifyChallengeCompleted(challenge.challengeName, 1f, item.progressText);
				ClaimChallengeReward(challenge);
			}
		}
	}

	public ConditionState GetConditionState(ChallengeConditionData condition)
	{
		return conditionStateTracker.GetOrCreateState(condition);
	}

	private void UpdateActiveChallengesDisplay()
	{
		if (_challengeSettings != null)
		{
			_challengeSettings.UpdateActiveChallenges();
		}
		UpdateActiveChallengesUI();
	}

	private void UpdateActiveChallengesUI()
	{
		if (activeChallengeListParent == null || activeChallengeEntryPrefab == null)
		{
			return;
		}
		HashSet<int> hashSet = new HashSet<int>();
		foreach (ChallengeProgress activeChallenge in GetActiveChallenges())
		{
			if (activeChallenge != null && !(activeChallenge.challenge == null) && !activeChallenge.isClaimed)
			{
				int challengeID = activeChallenge.challenge.challengeID;
				hashSet.Add(challengeID);
				if (activeChallengeEntries.TryGetValue(challengeID, out var value))
				{
					value.SetData(activeChallenge);
					continue;
				}
				value = UnityEngine.Object.Instantiate(activeChallengeEntryPrefab, activeChallengeListParent);
				value.SetData(activeChallenge);
				activeChallengeEntries[challengeID] = value;
			}
		}
		List<int> list = new List<int>();
		foreach (KeyValuePair<int, ChallengeEntryUI> activeChallengeEntry in activeChallengeEntries)
		{
			if (!hashSet.Contains(activeChallengeEntry.Key) && !completedChallengeEntryIds.Contains(activeChallengeEntry.Key))
			{
				if (activeChallengeEntry.Value != null)
				{
					UnityEngine.Object.Destroy(activeChallengeEntry.Value.gameObject);
				}
				list.Add(activeChallengeEntry.Key);
			}
		}
		foreach (int item in list)
		{
			activeChallengeEntries.Remove(item);
		}
	}

	public void AddChallenge(Challenge challenge)
	{
		if (!(challenge == null) && !allChallenges.Contains(challenge))
		{
			allChallenges.Add(challenge);
		}
	}

	public void RemoveChallenge(Challenge challenge)
	{
		if (!(challenge == null))
		{
			allChallenges.Remove(challenge);
		}
	}

	[Server]
	public List<Challenge> GetChallengesByFloorIndex(int floorIndex)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Collections.Generic.List`1<Challenge> ChallengeManager::GetChallengesByFloorIndex(System.Int32)' called when server was not active");
			return null;
		}
		if (_challengeSettings != null)
		{
			List<Challenge> challengesByFloorIndex = _challengeSettings.GetChallengesByFloorIndex(floorIndex);
			if (challengesByFloorIndex != null && challengesByFloorIndex.Count > 0)
			{
				return challengesByFloorIndex;
			}
		}
		return allChallenges.Where((Challenge c) => c != null && c.floorIndex == floorIndex).ToList();
	}

	[Server]
	public Challenge GetRandomChallenge(int floorIndex)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'Challenge ChallengeManager::GetRandomChallenge(System.Int32)' called when server was not active");
			return null;
		}
		if (_challengeSettings != null)
		{
			Challenge randomChallenge = _challengeSettings.GetRandomChallenge(floorIndex);
			if (randomChallenge != null)
			{
				return randomChallenge;
			}
		}
		List<Challenge> list = allChallenges.Where((Challenge c) => c != null && c.floorIndex == floorIndex).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		return list.GetRandomElement();
	}

	[Server]
	public bool ActivateChallenge(Challenge challenge)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Boolean ChallengeManager::ActivateChallenge(Challenge)' called when server was not active");
			return default;
		}
		if (challenge == null)
		{
			return false;
		}
		ChallengeProgress challengeProgress = new ChallengeProgress(challenge);
		challengeProgress.quotaAtActivation = ((NetworkSingleton<GameManager>.Instance != null) ? NetworkSingleton<GameManager>.Instance.currentQuota : 0);
		if (challenge.conditions != null)
		{
			foreach (ChallengeConditionData condition in challenge.conditions)
			{
				if (condition != null)
				{
					conditionStateTracker.GetOrCreateState(condition);
				}
			}
		}
		challenge.ResetChallenge();
		activeChallenges[challenge] = challengeProgress;
		Debug.Log("[ChallengeManager] Activated challenge: " + challenge.challengeName);
		newChallengeSfx.RpcPlayOneShotWith3DPos();
		UpdateChallenges();
		return true;
	}

	private IEnumerator LerpCanvasGroupAlpha(CanvasGroup canvasGroup, float targetAlpha, float duration)
	{
		if (canvasGroup == null || duration <= 0f)
		{
			if (canvasGroup != null)
			{
				canvasGroup.alpha = targetAlpha;
			}
			yield break;
		}
		float startAlpha = canvasGroup.alpha;
		float time = 0f;
		while (time < duration && canvasGroup != null)
		{
			time += Time.deltaTime;
			float t = Mathf.Clamp01(time / duration);
			canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
			yield return null;
		}
		if (canvasGroup != null)
		{
			canvasGroup.alpha = targetAlpha;
		}
	}

	[Server]
	public void NotifyNewDailyChallengesGiven()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void ChallengeManager::NotifyNewDailyChallengesGiven()' called when server was not active");
		}
		else
		{
			RpcClearCompletedChallengeEntries();
		}
	}

	[ClientRpc]
	private void RpcClearCompletedChallengeEntries()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void ChallengeManager::RpcClearCompletedChallengeEntries()", -838261859, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public bool DeactivateChallenge(Challenge challenge)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Boolean ChallengeManager::DeactivateChallenge(Challenge)' called when server was not active");
			return default;
		}
		if (challenge == null)
		{
			return false;
		}
		if (!activeChallenges.ContainsKey(challenge))
		{
			return false;
		}
		challenge.ResetChallenge();
		activeChallenges.Remove(challenge);
		Debug.Log("[ChallengeManager] Deactivated challenge: " + challenge.challengeName);
		UpdateChallenges();
		return true;
	}

	[Server]
	private void UpdateChallenges()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void ChallengeManager::UpdateChallenges()' called when server was not active");
			return;
		}
		List<ChallengeSyncData> list = new List<ChallengeSyncData>();
		foreach (KeyValuePair<Challenge, ChallengeProgress> activeChallenge in activeChallenges)
		{
			ConditionStateSyncData[] conditionStates = BuildConditionStateSyncData(activeChallenge.Key);
			list.Add(new ChallengeSyncData
			{
				challengeID = activeChallenge.Key.challengeID,
				progress = activeChallenge.Value.progress,
				isCompleted = activeChallenge.Value.isCompleted,
				isClaimed = activeChallenge.Value.isClaimed,
				completionCount = activeChallenge.Value.completionCount,
				lastBet = activeChallenge.Value.lastBet,
				lastPayout = activeChallenge.Value.lastPayout,
				lastGameType = activeChallenge.Value.lastGameType,
				conditionStates = conditionStates
			});
		}
		RpcSyncChallenges(list.ToArray());
	}

	[Command(requiresAuthority = false)]
	private void CmdRequestChallengesSync()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendCommandInternal("System.Void ChallengeManager::CmdRequestChallengesSync()", 54202643, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	private ConditionStateSyncData[] BuildConditionStateSyncData(Challenge challenge)
	{
		if (challenge == null || challenge.conditions == null)
		{
			return Array.Empty<ConditionStateSyncData>();
		}
		ConditionStateSyncData[] array = new ConditionStateSyncData[challenge.conditions.Count];
		for (int i = 0; i < challenge.conditions.Count; i++)
		{
			ChallengeConditionData condition = challenge.conditions[i];
			ConditionState orCreateState = conditionStateTracker.GetOrCreateState(condition);
			if (orCreateState == null)
			{
				array[i] = default;
				continue;
			}
			array[i] = new ConditionStateSyncData
			{
				currentWinCount = orCreateState.currentWinCount,
				consecutiveWinCount = orCreateState.consecutiveWinCount,
				currentLossCount = orCreateState.currentLossCount,
				consecutiveLossCount = orCreateState.consecutiveLossCount,
				totalBetAmount = orCreateState.totalBetAmount,
				totalPayoutAmount = orCreateState.totalPayoutAmount,
				totalProfit = orCreateState.totalProfit,
				elapsedSinceStart = Time.time - orCreateState.startTime,
				elapsedSinceLastGame = Time.time - orCreateState.lastGameTime
			};
		}
		return array;
	}

	public ConditionStateSyncData[] GetConditionStateSnapshot(Challenge challenge)
	{
		return BuildConditionStateSyncData(challenge);
	}

	[ClientRpc]
	private void RpcSyncChallenges(ChallengeSyncData[] challengeSyncData)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		GeneratedNetworkCode._Write_ChallengeSyncData_005B_005D(writer, challengeSyncData);
		SendRPCInternal("System.Void ChallengeManager::RpcSyncChallenges(ChallengeSyncData[])", -304260989, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void ApplyConditionStates(Challenge challenge, ConditionStateSyncData[] conditionStates)
	{
		if (challenge == null || challenge.conditions == null || conditionStates == null)
		{
			return;
		}
		int num = Mathf.Min(challenge.conditions.Count, conditionStates.Length);
		for (int i = 0; i < num; i++)
		{
			ChallengeConditionData condition = challenge.conditions[i];
			ConditionState orCreateState = conditionStateTracker.GetOrCreateState(condition);
			if (orCreateState != null)
			{
				ConditionStateSyncData conditionStateSyncData = conditionStates[i];
				orCreateState.currentWinCount = conditionStateSyncData.currentWinCount;
				orCreateState.consecutiveWinCount = conditionStateSyncData.consecutiveWinCount;
				orCreateState.currentLossCount = conditionStateSyncData.currentLossCount;
				orCreateState.consecutiveLossCount = conditionStateSyncData.consecutiveLossCount;
				orCreateState.totalBetAmount = conditionStateSyncData.totalBetAmount;
				orCreateState.totalPayoutAmount = conditionStateSyncData.totalPayoutAmount;
				orCreateState.totalProfit = conditionStateSyncData.totalProfit;
				orCreateState.startTime = Time.time - conditionStateSyncData.elapsedSinceStart;
				orCreateState.lastGameTime = Time.time - conditionStateSyncData.elapsedSinceLastGame;
			}
		}
	}

	[Server]
	public void RestoreChallenge(Challenge challenge, bool isCompleted, bool isClaimed, float progress, int completionCount, long lastBet, long lastPayout, CasinoGameType lastGameType, long quotaAtActivation, ConditionStateSyncData[] conditionStates)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void ChallengeManager::RestoreChallenge(Challenge,System.Boolean,System.Boolean,System.Single,System.Int32,System.Int64,System.Int64,CasinoGameType,System.Int64,ConditionStateSyncData[])' called when server was not active");
		}
		else
		{
			if (challenge == null)
			{
				return;
			}
			if (!activeChallenges.ContainsKey(challenge))
			{
				ChallengeProgress value = new ChallengeProgress(challenge);
				activeChallenges[challenge] = value;
				if (challenge.conditions != null)
				{
					foreach (ChallengeConditionData condition in challenge.conditions)
					{
						if (condition != null)
						{
							conditionStateTracker.GetOrCreateState(condition);
						}
					}
				}
			}
			ChallengeProgress challengeProgress = activeChallenges[challenge];
			challengeProgress.isCompleted = isCompleted;
			challengeProgress.isClaimed = isClaimed;
			challengeProgress.progress = progress;
			challengeProgress.completionCount = completionCount;
			challengeProgress.lastBet = lastBet;
			challengeProgress.lastPayout = lastPayout;
			challengeProgress.lastGameType = lastGameType;
			challengeProgress.quotaAtActivation = quotaAtActivation;
			ApplyConditionStates(challenge, conditionStates);
			ChallengeContext context = new ChallengeContext
			{
				bet = challengeProgress.lastBet,
				payout = challengeProgress.lastPayout,
				gameType = challengeProgress.lastGameType,
				gamePosition = Vector3.zero,
				quotaAtActivation = challengeProgress.quotaAtActivation,
				hadTipsyFortuneBuff = false,
				hadInspiringMelodyBuff = false,
				hadImmunityBuff = false
			};
			challengeProgress.progressText = challenge.GetProgressText(context);
			UpdateChallenges();
		}
	}

	[Server]
	public void InitializeChallenges(bool enabled, bool canComplete)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void ChallengeManager::InitializeChallenges(System.Boolean,System.Boolean)' called when server was not active");
			return;
		}
		challengesEnabled = enabled;
		challengesCanComplete = canComplete;
		Debug.Log($"[ChallengeManager] Initialized - Enabled: {enabled}, Can Complete: {canComplete}");
	}

	[Server]
	public void ServerClearChallengesUI()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void ChallengeManager::ServerClearChallengesUI()' called when server was not active");
		}
		else
		{
			RpcClearCompletedChallengeEntries();
		}
	}

	[Server]
	public void SetChallengesEnabled(bool enabled)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void ChallengeManager::SetChallengesEnabled(System.Boolean)' called when server was not active");
			return;
		}
		challengesEnabled = enabled;
		Debug.Log($"[ChallengeManager] Challenges enabled: {enabled}");
	}

	[Server]
	public void SetChallengesCanComplete(bool canComplete)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void ChallengeManager::SetChallengesCanComplete(System.Boolean)' called when server was not active");
			return;
		}
		challengesCanComplete = canComplete;
		Debug.Log($"[ChallengeManager] Challenges can complete: {canComplete}");
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcNotifyChallengeRewardAwarded__String__Int32(string challengeName, int ticketReward)
	{
		Debug.Log($"[ChallengeManager] Challenge completed! Received {ticketReward} tickets for: {challengeName}");
		Challenge challenge = allChallenges.FirstOrDefault((Challenge c) => c != null && c.challengeName == challengeName);
		if (challenge != null)
		{
			completedChallengeEntryIds.Add(challenge.challengeID);
			if ((bool)challenge.linkedAchievement)
			{
				challenge.linkedAchievement.UnlockAchievement();
			}
		}
		if (MonoSingleton<DaySummaryRuntime>.Instance != null)
		{
			MonoSingleton<DaySummaryRuntime>.Instance.Add(challengeName, ticketReward);
		}
	}

	protected static void InvokeUserCode_RpcNotifyChallengeRewardAwarded__String__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcNotifyChallengeRewardAwarded called on server.");
		}
		else
		{
			((ChallengeManager)obj).UserCode_RpcNotifyChallengeRewardAwarded__String__Int32(reader.ReadString(), reader.ReadVarInt());
		}
	}

	protected void UserCode_RpcNotifyChallengeCompleted__String__Single__String(string challengeName, float progress01, string progressText)
	{
		Debug.Log("[ChallengeManager] Challenge completed: " + challengeName);
		challengeCompleteSfx.PlayOneShotWith3DPos();
		Challenge challenge = allChallenges.FirstOrDefault((Challenge c) => c != null && c.challengeName == challengeName);
		if (challenge == null)
		{
			return;
		}
		completedChallengeEntryIds.Add(challenge.challengeID);
		if (activeChallengeEntries.TryGetValue(challenge.challengeID, out var value) && value != null)
		{
			float num = Mathf.Clamp01(progress01);
			if (num < 1f)
			{
				num = 1f;
			}
			ChallengeProgress data = new ChallengeProgress(challenge)
			{
				isCompleted = true,
				isClaimed = false,
				progress = num,
				progressText = (progressText ?? string.Empty)
			};
			value.SetData(data);
			CanvasGroup componentInChildren = value.GetComponentInChildren<CanvasGroup>();
			if (componentInChildren != null)
			{
				StartCoroutine(LerpCanvasGroupAlpha(componentInChildren, 1f, 0.25f));
			}
		}
	}

	protected static void InvokeUserCode_RpcNotifyChallengeCompleted__String__Single__String(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcNotifyChallengeCompleted called on server.");
		}
		else
		{
			((ChallengeManager)obj).UserCode_RpcNotifyChallengeCompleted__String__Single__String(reader.ReadString(), reader.ReadFloat(), reader.ReadString());
		}
	}

	protected void UserCode_RpcClearCompletedChallengeEntries()
	{
		completedChallengeEntryIds.Clear();
		UpdateActiveChallengesDisplay();
	}

	protected static void InvokeUserCode_RpcClearCompletedChallengeEntries(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcClearCompletedChallengeEntries called on server.");
		}
		else
		{
			((ChallengeManager)obj).UserCode_RpcClearCompletedChallengeEntries();
		}
	}

	protected void UserCode_CmdRequestChallengesSync()
	{
		if (base.isServer)
		{
			UpdateChallenges();
		}
	}

	protected static void InvokeUserCode_CmdRequestChallengesSync(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdRequestChallengesSync called on client.");
		}
		else
		{
			((ChallengeManager)obj).UserCode_CmdRequestChallengesSync();
		}
	}

	protected void UserCode_RpcSyncChallenges__ChallengeSyncData_005B_005D(ChallengeSyncData[] challengeSyncData)
	{
		HashSet<int> hashSet = new HashSet<int>();
		ChallengeSyncData[] array;
		if (challengeSyncData != null)
		{
			array = challengeSyncData;
			for (int i = 0; i < array.Length; i++)
			{
				ChallengeSyncData challengeSyncData2 = array[i];
				hashSet.Add(challengeSyncData2.challengeID);
			}
		}
		if (hashSet.Count == 0)
		{
			activeChallenges.Clear();
			UpdateActiveChallengesDisplay();
			return;
		}
		List<Challenge> list = new List<Challenge>();
		foreach (Challenge key in activeChallenges.Keys)
		{
			if (key == null || !hashSet.Contains(key.challengeID))
			{
				list.Add(key);
			}
		}
		foreach (Challenge item in list)
		{
			activeChallenges.Remove(item);
		}
		array = challengeSyncData;
		for (int i = 0; i < array.Length; i++)
		{
			ChallengeSyncData syncData = array[i];
			Challenge challenge = allChallenges.FirstOrDefault((Challenge c) => c != null && c.challengeID == syncData.challengeID);
			if (!(challenge == null))
			{
				ChallengeProgress challengeProgress = new ChallengeProgress(challenge);
				challengeProgress.progress = syncData.progress;
				challengeProgress.isCompleted = syncData.isCompleted;
				challengeProgress.isClaimed = syncData.isClaimed;
				challengeProgress.completionCount = syncData.completionCount;
				challengeProgress.lastBet = syncData.lastBet;
				challengeProgress.lastPayout = syncData.lastPayout;
				challengeProgress.lastGameType = syncData.lastGameType;
				ApplyConditionStates(challenge, syncData.conditionStates);
				ChallengeContext context = new ChallengeContext
				{
					bet = challengeProgress.lastBet,
					payout = challengeProgress.lastPayout,
					gameType = challengeProgress.lastGameType,
					gamePosition = Vector3.zero,
					quotaAtActivation = challengeProgress.quotaAtActivation,
					hadTipsyFortuneBuff = false,
					hadInspiringMelodyBuff = false,
					hadImmunityBuff = false
				};
				challengeProgress.progressText = challenge.GetProgressText(context);
				activeChallenges[challenge] = challengeProgress;
			}
		}
		UpdateActiveChallengesDisplay();
	}

	protected static void InvokeUserCode_RpcSyncChallenges__ChallengeSyncData_005B_005D(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSyncChallenges called on server.");
		}
		else
		{
			((ChallengeManager)obj).UserCode_RpcSyncChallenges__ChallengeSyncData_005B_005D(GeneratedNetworkCode._Read_ChallengeSyncData_005B_005D(reader));
		}
	}

	static ChallengeManager()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(ChallengeManager), "System.Void ChallengeManager::CmdRequestChallengesSync()", InvokeUserCode_CmdRequestChallengesSync, requiresAuthority: false);
		RemoteProcedureCalls.RegisterRpc(typeof(ChallengeManager), "System.Void ChallengeManager::RpcNotifyChallengeRewardAwarded(System.String,System.Int32)", InvokeUserCode_RpcNotifyChallengeRewardAwarded__String__Int32);
		RemoteProcedureCalls.RegisterRpc(typeof(ChallengeManager), "System.Void ChallengeManager::RpcNotifyChallengeCompleted(System.String,System.Single,System.String)", InvokeUserCode_RpcNotifyChallengeCompleted__String__Single__String);
		RemoteProcedureCalls.RegisterRpc(typeof(ChallengeManager), "System.Void ChallengeManager::RpcClearCompletedChallengeEntries()", InvokeUserCode_RpcClearCompletedChallengeEntries);
		RemoteProcedureCalls.RegisterRpc(typeof(ChallengeManager), "System.Void ChallengeManager::RpcSyncChallenges(ChallengeSyncData[])", InvokeUserCode_RpcSyncChallenges__ChallengeSyncData_005B_005D);
	}
}

}
