using System;
using System.Collections.Generic;
using PaperPixelGames.Core.DailyReport;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Economy;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Progression;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Core.ScrapSystem;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Managers
{
	public class GameProgressManager : Singleton<GameProgressManager>, ISaveable<GameProgressData>
	{
		[SerializeField]
		private EconomyHandler _economyHandler;

		[SerializeField]
		private QuestManager _questManager;

		[SerializeField]
		private QuestUI _questUI;

		[SerializeField]
		private CozyWeatherManager _cozyWeatherManager;

		[SerializeField]
		private RestaurantManager _restaurantManager;

		[SerializeField]
		private ItemDatabaseSO _itemDatabaseSO;

		[SerializeField]
		private RpcRelay _relay;

		[SerializeField]
		private DailyReportBroadcaster _dailyReportBroadcaster;

		[SerializeField]
		private GameObject _blueMarketToolRoot;

		[SerializeField]
		private WorldPickupHandler _worldPickupHandler;

		[SerializeField]
		private ProgressionManager _progressionManager;

		[Header("Late Join Sync")]
		[SerializeField]
		private List<MonoBehaviour> _bootstrapModuleBehaviours = new List<MonoBehaviour>();

		private JoinBootstrapHub _bootstrapHub;

		private bool _isInitDone;

		public override SingletonScope Scope => SingletonScope.Scene;

		public WorldPickupHandler WorldPickupHandler => _worldPickupHandler;

		public RestaurantManager RestaurantManager => _restaurantManager;

		public CozyWeatherManager CozyWeatherManager => _cozyWeatherManager;

		public QuestUI QuestUI => _questUI;

		public ItemDatabaseSO ItemDatabaseSO => _itemDatabaseSO;

		public RpcRelay RpcRelay => _relay;

		public EconomyHandler EconomyHandler => _economyHandler;

		public ProgressionManager ProgressionManager => _progressionManager;

		public DailyReportBroadcaster DailyReportBroadcaster => _dailyReportBroadcaster;

		public bool CompleteTimeTuto => _relay.CompleteTimeTuto.Value;

		public bool CompleteOpenSignTuto => _relay.CompleteOpenSignTuto.Value;

		public bool IsBlueMarketRootComplete => _relay.CompleteBlueMarket.Value;

		protected override void Awake()
		{
			base.Awake();
			List<IJoinBootstrapModule> list = new List<IJoinBootstrapModule>(_bootstrapModuleBehaviours.Count);
			for (int i = 0; i < _bootstrapModuleBehaviours.Count; i++)
			{
				MonoBehaviour monoBehaviour = _bootstrapModuleBehaviours[i];
				if (monoBehaviour is IJoinBootstrapModule item)
				{
					list.Add(item);
					continue;
				}
				string arg = ((monoBehaviour != null) ? monoBehaviour.GetType().Name : "null");
				UtilityMethods.Log($"<color=red>[GameProgressManager]</color> Invalid bootstrap module reference. Index={i}, Type={arg}", LogType.Error);
			}
			_bootstrapHub = new JoinBootstrapHub(list);
		}

		public bool ServerCaptureBootstrap(ulong targetClientId, JoinBootstrapMode mode, out string failReason)
		{
			return _bootstrapHub.ServerCaptureAll(targetClientId, mode, out failReason);
		}

		public byte[] ServerBuildBootstrapBlob(JoinSessionContext context)
		{
			return _bootstrapHub.ServerBuildBlob(context);
		}

		public BootstrapApplyResult ClientApplyBootstrapBlob(byte[] blob)
		{
			return _bootstrapHub.ClientApplyBlob(blob);
		}

		public BootstrapApplyOperation CreateBootstrapApply(byte[] blob, ulong expectedEpoch, Func<bool> isCurrent, bool sliced)
		{
			return _bootstrapHub.CreateClientApply(blob, expectedEpoch, isCurrent, null, sliced ? 2.0 : double.PositiveInfinity, sliced ? 8 : int.MaxValue);
		}

		public bool IsProgressionUnlocked(ProgressionUnlockKey key)
		{
			if (key != ProgressionUnlockKey.None && !(_progressionManager == null))
			{
				return _progressionManager.IsUnlocked(key);
			}
			return true;
		}

		public bool IsDiningTableProgressionUnlocked(int tableId)
		{
			if (!(_progressionManager == null))
			{
				return _progressionManager.IsDiningTableUnlocked(tableId);
			}
			return true;
		}

		public bool IsConveyorTableProgressionUnlocked(int tableId)
		{
			if (!(_progressionManager == null))
			{
				return _progressionManager.IsConveyorTableUnlocked(tableId);
			}
			return true;
		}

		public int GetAllowedDiningTableUnlockCount()
		{
			if (!(_progressionManager != null))
			{
				return int.MaxValue;
			}
			return _progressionManager.GetAllowedDiningTableUnlockCount();
		}

		public bool CanUnlockMoreDiningTables(int currentlyUnlockedCount)
		{
			if (!(_progressionManager == null))
			{
				return _progressionManager.CanUnlockMoreDiningTables(currentlyUnlockedCount);
			}
			return true;
		}

		public bool IsServedDishProgressionUnlocked(ServedDishSO servedDish)
		{
			if (!(_progressionManager == null))
			{
				return _progressionManager.IsServedDishUnlocked(servedDish);
			}
			return true;
		}

		public bool IsWholesaleProgressionUnlocked(WholesaleSO wholesale)
		{
			if (!(_progressionManager == null))
			{
				return _progressionManager.IsWholesaleUnlocked(wholesale);
			}
			return true;
		}

		public int GetRequiredProgressionLevel(ProgressionUnlockKey key)
		{
			if (!(_progressionManager != null))
			{
				return -1;
			}
			return _progressionManager.GetRequiredLevelForUnlock(key);
		}

		public int GetRequiredWholesaleProgressionLevel(WholesaleSO wholesale)
		{
			if (!(_progressionManager != null))
			{
				return -1;
			}
			return _progressionManager.GetRequiredLevelForWholesaleUnlock(wholesale);
		}

		public int GetRequiredDiningTableProgressionLevel(int tableId)
		{
			if (!(_progressionManager != null))
			{
				return -1;
			}
			return _progressionManager.GetRequiredLevelForDiningTableUnlock(tableId);
		}

		public int GetRequiredLevelForNextDiningTableUnlock(int currentlyUnlockedCount)
		{
			if (!(_progressionManager != null))
			{
				return -1;
			}
			return _progressionManager.GetRequiredLevelForNextDiningTableUnlock(currentlyUnlockedCount);
		}

		public int GetRequiredConveyorTableProgressionLevel(int tableId)
		{
			if (!(_progressionManager != null))
			{
				return -1;
			}
			return _progressionManager.GetRequiredLevelForConveyorTableUnlock(tableId);
		}

		public bool ServerGrantProgressionXp(ProgressionXpSource source, string contextId = null, ulong? feedbackClientId = null)
		{
			if (_progressionManager != null)
			{
				return _progressionManager.ServerGrantXp(source, contextId, feedbackClientId);
			}
			return false;
		}

		public bool ServerGrantProgressionXp(ProgressionXpSource source, int amount, string contextId = null, ulong? feedbackClientId = null)
		{
			if (_progressionManager != null)
			{
				return _progressionManager.ServerGrantXp(source, amount, contextId, feedbackClientId);
			}
			return false;
		}

		public void Initialize()
		{
			if (!_isInitDone)
			{
				_isInitDone = true;
				UtilityMethods.Log($"[GameProgressManager] Init - CompleteOpenSignTuto:{CompleteOpenSignTuto}");
				NetworkVariable<bool> blueMarketToolsAvailable = _relay.BlueMarketToolsAvailable;
				blueMarketToolsAvailable.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(blueMarketToolsAvailable.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnBlueMarketToolsAvailableChanged));
				SetBlueMarketTools(_relay.BlueMarketToolsAvailable.Value);
				if (!CompleteOpenSignTuto)
				{
					_cozyWeatherManager.ClientPauseTime();
					_cozyWeatherManager.ClientSetTime(13, 0);
				}
				else
				{
					_cozyWeatherManager.TriggerBlackBoardTimeEvents();
					_cozyWeatherManager.ClientResumeTime();
				}
				DialoguePanelController dialoguePanelController = Singleton<GameUIManager>.Instance.DialoguePanelController;
				dialoguePanelController.OnQuestEndAction = (Action<string>)Delegate.Combine(dialoguePanelController.OnQuestEndAction, new Action<string>(OnQuestEnd));
			}
		}

		private void OnDestroy()
		{
			if (_relay != null)
			{
				NetworkVariable<bool> blueMarketToolsAvailable = _relay.BlueMarketToolsAvailable;
				blueMarketToolsAvailable.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(blueMarketToolsAvailable.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnBlueMarketToolsAvailableChanged));
			}
			DialoguePanelController dialoguePanelController = Singleton<GameUIManager>.Instance.DialoguePanelController;
			dialoguePanelController.OnQuestEndAction = (Action<string>)Delegate.Remove(dialoguePanelController.OnQuestEndAction, new Action<string>(OnQuestEnd));
		}

		public GameProgressData Save()
		{
			GameProgressData gameProgressData = new GameProgressData
			{
				CurrentDay = _cozyWeatherManager.CurrentDay,
				ShopBalance = _economyHandler.ShopBalance.Value,
                ExactGoldStorage = _economyHandler.SavedGoldStorage,
                TownDiningLedger = SushiAndFish.CityExpansion.TownMealScheduler.CaptureLedger(),
				ScrapBalance = _economyHandler.ScrapBalance.Value,
				LastSaveTime = DateTime.Now.ToString("dd MMMM yyyy hh:mmtt"),
				QuestData = _questManager.GetSaveData(),
				HasSave = true,
				CompleteTimeTuto = CompleteTimeTuto,
				CompleteOpenSignTuto = CompleteOpenSignTuto,
				CompassIndex = _restaurantManager.CurrentCompassTargetIndex,
				CompassLevel = _restaurantManager.CurrentCompassTargetLevel,
				CompassStepIndex = _restaurantManager.CurrentCompassTargetStepIndex
			};
			_progressionManager?.WriteToSave(gameProgressData);
            // Preserve an existing anchor if fishing has not yet restored this slot.
            var fishingSaveManager = Singleton<SaveManager>.Instance;
            if (fishingSaveManager && fishingSaveManager.TryGetCurrentSaveSlot(out var fishingSlot) && fishingSlot.GameProgress != null)
            {
                gameProgressData.FishingWeaponsAnchored = fishingSlot.GameProgress.FishingWeaponsAnchored;
                gameProgressData.FishingWeaponsMask = fishingSlot.GameProgress.FishingWeaponsMask;
            }
            var fishingWorld = SushiAndFish.FishingIntegration.FishingMigration.Instance;
            if (fishingWorld && fishingWorld.TryCaptureNativeWeapons(out var fishingWeaponsMask))
            {
                gameProgressData.FishingWeaponsAnchored = true;
                gameProgressData.FishingWeaponsMask = fishingWeaponsMask;
            }
			return gameProgressData;
		}

		public void Load(GameProgressData data)
		{
			if (data == null)
			{
				UtilityMethods.Log("[GameProgressManager] Load failed. Data is null.", LogType.Warning);
				return;
			}
			UtilityMethods.Log($"[GameProgressManager] Load - CompleteOpenSignTuto:{data.CompleteOpenSignTuto}");
			_relay.CompleteTimeTuto.Value = data.CompleteTimeTuto;
			_relay.CompleteOpenSignTuto.Value = data.CompleteOpenSignTuto;
			_cozyWeatherManager.CurrentDay = ((!data.HasSave) ? 1 : data.CurrentDay);
            SushiAndFish.CityExpansion.TownMealScheduler.RestoreLedger(data.TownDiningLedger);
			_economyHandler.SetSavedGoldBalance(data.ShopBalance, data.ExactGoldStorage, data.ScrapBalance);
			_progressionManager?.LoadFromSave(data);
			if (_questManager != null)
			{
				if (data.HasSave)
				{
					_questManager.Load(data.QuestData);
				}
				else
				{
					_questManager.InitializeNewGame();
				}
				_relay.ServerReconcileHarpoonUnlockFromQuest();
			}
			_restaurantManager.CurrentCompassTargetLevel = data.CompassLevel;
			_restaurantManager.CurrentCompassTargetStepIndex = data.CompassStepIndex;
			_cozyWeatherManager.ServerSync();
			if (_questUI != null)
			{
				_questUI.Rebuild();
			}
		}

		public void ClientSyncData()
		{
			_cozyWeatherManager.SyncDay();
		}

		public bool BlueMarketHandle()
		{
			if (IsBlueMarketRootComplete)
			{
				return false;
			}
			if (!_blueMarketToolRoot.activeSelf)
			{
				_relay.ServerActivateBlueMarketToolsRpc();
			}
			return true;
		}

		public void SetBlueMarketTools(bool status)
		{
			_blueMarketToolRoot.SetActive(status);
		}

		private void OnBlueMarketToolsAvailableChanged(bool previousValue, bool currentValue)
		{
			SetBlueMarketTools(currentValue);
		}

		public void CompleteOpenSignTutorial()
		{
			if (!CompleteOpenSignTuto)
			{
				_relay.ServerCompleteOpenSignTutoRpc();
			}
		}

		public void CompleteTimeTutorial()
		{
			if (!CompleteTimeTuto)
			{
				_relay.ServerCompleteTimeTutoRpc();
			}
		}

		public void CompleteBlueMarketHandle()
		{
			if (!IsBlueMarketRootComplete)
			{
				_relay.ServerCompleteBlueMarketRpc();
			}
		}

		private void OnQuestEnd(string questId)
		{
			UtilityMethods.Log("[GPM] OnQuestEnd:" + questId);
			if (!string.IsNullOrWhiteSpace(questId) && !TryCompleteDialogueQuest(questId, QuestId.TalkToTheFisherman, OnTalkToFishermanQuestCompleted))
			{
				TryCompleteDialogueQuest(questId, QuestId.AskFisherman, OnAskFishermanQuestCompleted);
			}
		}

		private bool TryCompleteDialogueQuest(string incomingQuestId, QuestId targetQuestId, Action onCompleted)
		{
			QuestReferenceHub instance = QuestReferenceHub.Instance;
			if (instance == null)
			{
				UtilityMethods.Log("[GPM] QuestReferenceHub is null.", LogType.Warning);
				return false;
			}
			QuestSO quest = instance.GetQuest(targetQuestId);
			if (quest == null)
			{
				UtilityMethods.Log($"[GPM] QuestSO not found for QuestId: {targetQuestId}", LogType.Warning);
				return false;
			}
			if (incomingQuestId != quest.Id)
			{
				return false;
			}
			if (!instance.CompleteIfActive(targetQuestId))
			{
				return true;
			}
			onCompleted?.Invoke();
			return true;
		}

		private void OnTalkToFishermanQuestCompleted()
		{
			BlueMarketHandle();
		}

		private void OnAskFishermanQuestCompleted()
		{
			_relay.ServerCompleteBlueMarket2Rpc();
		}
	}
}
