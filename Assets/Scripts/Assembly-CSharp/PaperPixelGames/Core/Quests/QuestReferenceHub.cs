using System;
using System.Collections.Generic;
using System.Reflection;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Utility;
using UnityEngine;
using UnityEngine.Serialization;

namespace PaperPixelGames.Core.Quests
{
	public class QuestReferenceHub : MonoBehaviour
	{
		[Header("001 - Your First Day")]
		[SerializeField]
		private QuestSO _goToTheRestaurant;

		[SerializeField]
		private QuestSO _inspectTheRestaurant;

		[SerializeField]
		private QuestSO _cleanUpTheTrash;

		[SerializeField]
		private QuestSO _repairTheSign;

		[SerializeField]
		private QuestSO _nameYourRestaurant;

		[Header("002 - Restore the Restaurant")]
		[SerializeField]
		private QuestSO _repairTheDiningTable;

		[SerializeField]
		private QuestSO _repairThePrepStation;

		[SerializeField]
		private QuestSO _repairTheChefDesk;

		[Header("003 - Buy Ingredients")]
		[SerializeField]
		private QuestSO _goToMarinasMarket;

		[SerializeField]
		private QuestSO _buyNori;

		[SerializeField]
		private QuestSO _buyRice;

		[SerializeField]
		private QuestSO _buyCucumber;

		[FormerlySerializedAs("_placeIngredientsStorage")]
		[SerializeField]
		private QuestSO _placeIngredientStorage;

		[Header("004 - Cook the Rice")]
		[SerializeField]
		private QuestSO _takeRawRiceFromRiceSack;

		[SerializeField]
		private QuestSO _addRiceToRiceCooker;

		[SerializeField]
		private QuestSO _startTheRiceCooker;

		[SerializeField]
		private QuestSO _takeCookedRice;

		[Header("005 - Slice the Cucumber")]
		[SerializeField]
		private QuestSO _takeOneCucumberFromCucumberWholesale;

		[SerializeField]
		private QuestSO _usePrepStation;

		[SerializeField]
		private QuestSO _selectLongStripKnifePrepStation;

		[SerializeField]
		private QuestSO _sliceCucumberAtPrepStation;

		[SerializeField]
		private QuestSO _takeSlicedCucumber;

		[Header("006 - Prepare the Chef Desk")]
		[FormerlySerializedAs("_takeOneNoriFromNoriPack")]
		[SerializeField]
		private QuestSO _addNoriToChefDeskGastroPan;

		[SerializeField]
		private QuestSO _addCookedRiceToChefDeskGastroPan;

		[SerializeField]
		private QuestSO _addSlicedCucumberToChefDeskGastroPan;

		[Header("007 - Make Cucumber Maki")]
		[SerializeField]
		private QuestSO _useChefDesk;

		[SerializeField]
		private QuestSO _selectCucumberMakiRecipeFromRecipeBook;

		[SerializeField]
		private QuestSO _makeCucumberMaki;

		[SerializeField]
		private QuestSO _placeCucumberMakiOnTray;

		[Header("008 - Set the Menu")]
		[SerializeField]
		private QuestSO _openTablet;

		[SerializeField]
		private QuestSO _openMenuApp;

		[SerializeField]
		private QuestSO _addCucumberMakiToMenu;

		[Header("009 - Open the Restaurant")]
		[SerializeField]
		private QuestSO _turnTheSignToOpen;

		[SerializeField]
		private QuestSO _waitForYourFirstCustomer;

		[Header("010 - Take Your First Order")]
		[SerializeField]
		private QuestSO _takeTheCustomersOrder;

		[SerializeField]
		private QuestSO _openTheOrdersApp;

		[Header("011 - Serve Your First Customer")]
		[SerializeField]
		private QuestSO _serveCucumberMaki;

		[SerializeField]
		private QuestSO _collectPayment;

		[Header("012 - Clean the Table")]
		[SerializeField]
		private QuestSO _pickUpTheDirtyTray;

		[SerializeField]
		private QuestSO _clearTheTray;

		[Header("013 - Finish Your First Workday")]
		[SerializeField]
		private QuestSO _serveCustomersUntilNight;

		[Header("014 - End the Day")]
		[SerializeField]
		private QuestSO _sleep;

		[Header("015 - Meet Gabe")]
		[FormerlySerializedAs("_openMap")]
		[SerializeField]
		private QuestSO _openMapApp;

		[SerializeField]
		private QuestSO _goToTheBlueMarket;

		[SerializeField]
		private QuestSO _talkToTheFisherman;

		[Header("016 - Unlock the Harpoon")]
		[SerializeField]
		private QuestSO _unlockTheHarpoon;

		[SerializeField]
		private QuestSO _openTheToolWheel;

		[SerializeField]
		private QuestSO _equipTheHarpoon;

		[Header("017 - Catch Your First Fish")]
		[SerializeField]
		private QuestSO _goToBluecurrentReef;

		[SerializeField]
		private QuestSO _scanMackerel;

		[SerializeField]
		private QuestSO _catchAMackerel;

		[Header("018 - Fillet the Mackerel")]
		[SerializeField]
		private QuestSO _placeMackerelInFilletMachine;

		[SerializeField]
		private QuestSO _startTheFilletMachine;

		[SerializeField]
		private QuestSO _takeMackerelFillet;

		[Header("019 - Prepare Mackerel for Sushi")]
		[FormerlySerializedAs("_usePrepStation01")]
		[SerializeField]
		private QuestSO _usePrepStation_01;

		[SerializeField]
		private QuestSO _selectNigiriCutKnifePrepStation;

		[SerializeField]
		private QuestSO _sliceMackerelFilletIntoNigiri;

		[SerializeField]
		private QuestSO _takeSlicedMackerel;

		[Header("020 - Make Mackerel Nigiri Sushi")]
		[SerializeField]
		private QuestSO _useChefDesk_01;

		[SerializeField]
		private QuestSO _selectMackerelNigiriRecipeFromRecipeBook;

		[SerializeField]
		private QuestSO _makeMackerelNigiri;

		[SerializeField]
		private QuestSO _placeMackerelNigiriOnTray;

		[Header("021 - Set Menu for Mackerel Nigiri")]
		[SerializeField]
		private QuestSO _openMenuApp01;

		[SerializeField]
		private QuestSO _addMackerelNigiriToMenu;

		[Header("022 - Complete Your First Fish Order")]
		[SerializeField]
		private QuestSO _openTheRestaurantForFishOrders;

		[SerializeField]
		private QuestSO _serveMackerelNigiri;

		[Header("023 - Open Second Table")]
		[SerializeField]
		private QuestSO _unlockAnotherTable;

		[Header("024 - Increase Order Value")]
		[SerializeField]
		private QuestSO _goToGreenMarketForSideDish;

		[SerializeField]
		private QuestSO _buyASideDish;

		[SerializeField]
		private QuestSO _addASideDishToMenu;

		[SerializeField]
		private QuestSO _takeSideDishAndAddToTray;

		[Header("025 - Serve Side Dishes")]
		[SerializeField]
		private QuestSO _serveSideDish;

		[Header("026 - Prepare More Fish for Service")]
		[SerializeField]
		private QuestSO _goToSilverstreamShoalsArea;

		[SerializeField]
		private QuestSO _scanBlackSeaBream;

		[SerializeField]
		private QuestSO _catchBlackSeaBream;

		[SerializeField]
		private QuestSO _useTheFilletMachine;

		[Header("027 - Strange Compass")]
		[SerializeField]
		private QuestSO _pickUpCompass;

		[SerializeField]
		private QuestSO _askFisherman;

		[SerializeField]
		private QuestSO _equipCompass;

		[Header("028 - Prepare for a Deeper Dive")]
		[SerializeField]
		private QuestSO _earnOneThousandDollars;

		[SerializeField]
		private QuestSO _collectScrapForDivingSuit;

		[SerializeField]
		private QuestSO _upgradeDivingSuitToLevelThree;

		[SerializeField]
		private QuestSO _upgradeHarpoonToLevelTwo;

		[Header("029 - Find the New Compass Piece")]
		[FormerlySerializedAs("_fallowThoeCompassSignal")]
		[SerializeField]
		private QuestSO _fallowTheCompassSignal;

		[Header("030 - Unlock the Fishing Rod")]
		[SerializeField]
		private QuestSO _unlockTheFishingRod;

		[SerializeField]
		private QuestSO _equipTheFishingRod;

		[SerializeField]
		private QuestSO _catchFiveFishWithTheFishingRod;

		[Header("031 - Bring New Fish to the Menu")]
		[SerializeField]
		private QuestSO _goToGoldenfinDrift;

		[FormerlySerializedAs("_catchChinookSalmonAtDeepCurrentGate")]
		[SerializeField]
		private QuestSO _catchChinookSalmonAtGoldenfinDrift;

		[SerializeField]
		private QuestSO _makeAChinookSalmonDish;

		[SerializeField]
		private QuestSO _addChinookSalmonDishToMenu;

		[SerializeField]
		private QuestSO _serveChinookSalmonDish;

		[Header("032 - Save Money for the Conveyor")]
		[SerializeField]
		private QuestSO _earnThreeThousandDollars;

		[Header("033 - Restore the Conveyor")]
		[SerializeField]
		private QuestSO _repairTheConveyor;

		[SerializeField]
		private QuestSO _repairConveyorChair;

		[Header("034 - Use the Conveyor")]
		[SerializeField]
		private QuestSO _addPlateToConveyor;

		[SerializeField]
		private QuestSO _earnMoneyFromConveyor;

		[Header("035 - Dive Deeper")]
		[SerializeField]
		private QuestSO _upgradeDivingSuitToLevelFour;

		[Header("036 - Follow the Compass Signal")]
		[SerializeField]
		private QuestSO _findTheNewCompassPiece;

		[Header("037 - Unlock the Sea Scooter")]
		[SerializeField]
		private QuestSO _unlockTheSeaScooter;

		[SerializeField]
		private QuestSO _equipTheSeaScooter;

		[Header("038 - New Fish")]
		[SerializeField]
		private QuestSO _goToTheDeepCurrentGateArea;

		[SerializeField]
		private QuestSO _scanSquid;

		[SerializeField]
		private QuestSO _catchASquid;

		[Header("039 - New Sushi")]
		[SerializeField]
		private QuestSO _makeSquidNigiri;

		[SerializeField]
		private QuestSO _addSquidNigiriToTheMenu;

		[SerializeField]
		private QuestSO _serveSquidNigiri;

		[Header("040 - Prepare for the Final Dive")]
		[FormerlySerializedAs("_upgradeDivingSuitToLevelFour")]
		[FormerlySerializedAs("_upgradeDivingSuitToLevelTFour")]
		[SerializeField]
		private QuestSO _upgradeDivingSuitToLevelFive;

		[FormerlySerializedAs("_upgradeSeaScooterToLevelTwo")]
		[SerializeField]
		private QuestSO _upgradeHarpoonToLevelThree;

		[Header("041 - The Final Dive")]
		[FormerlySerializedAs("_findTheFinalCompassFragment")]
		[SerializeField]
		private QuestSO _followTheCompassSignalAndFindTheFinalPiece;

		[Header("042 - Discover the Lighthouse")]
		[FormerlySerializedAs("_openTheGate")]
		[SerializeField]
		private QuestSO _discoverTheLighthouse;

		[Header("043 - Defeat the Red King Crab")]
		[SerializeField]
		private QuestSO _defeatTheRedKingCrab;

		[Header("044 - Use the Trident")]
		[SerializeField]
		private QuestSO _equipTheTrident;

		[SerializeField]
		private QuestSO _catchTwentyFishWithTheTrident;

		[SerializeField]
		private QuestSO _dashWithTheTrident;

		private readonly Dictionary<QuestId, QuestSO> _questsById = new Dictionary<QuestId, QuestSO>();

		public static QuestReferenceHub Instance { get; private set; }

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				UnityEngine.Object.Destroy(base.gameObject);
				return;
			}
			Instance = this;
			BuildLookup();
		}

		public QuestSO GetQuest(QuestId questId)
		{
			if (questId == QuestId.None)
			{
				return null;
			}
			if (_questsById.Count == 0)
			{
				BuildLookup();
			}
			if (_questsById.TryGetValue(questId, out var value))
			{
				return value;
			}
			UtilityMethods.Log($"[QuestReferenceHub] Quest reference not found: {questId}", LogType.Warning);
			return null;
		}

		public bool QuestComplete(QuestId questId)
		{
			QuestSO quest = GetQuest(questId);
			if (quest == null)
			{
				return false;
			}
			QuestManager instance = QuestManager.Instance;
			if (instance == null)
			{
				UtilityMethods.Log("[QuestReferenceHub] QuestManager is null.", LogType.Warning);
				return false;
			}
			return instance.RequestCompleteQuest(quest);
		}

		public bool CompleteIfActive(QuestId questId)
		{
			if (!IsActive(questId))
			{
				return false;
			}
			return QuestComplete(questId);
		}

		public bool CompleteCurrentMainGroupIfContains(QuestId questId)
		{
			QuestSO quest = GetQuest(questId);
			if (quest == null)
			{
				return false;
			}
			QuestManager instance = QuestManager.Instance;
			if (instance == null)
			{
				UtilityMethods.Log("[QuestReferenceHub] QuestManager is null.", LogType.Warning);
				return false;
			}
			return instance.RequestCompleteCurrentMainGroupIfContainsQuest(quest);
		}

		public bool AddProgress(QuestId questId, int amount = 1)
		{
			QuestSO quest = GetQuest(questId);
			if (quest == null)
			{
				return false;
			}
			QuestManager instance = QuestManager.Instance;
			if (instance == null)
			{
				UtilityMethods.Log("[QuestReferenceHub] QuestManager is null.", LogType.Warning);
				return false;
			}
			return instance.RequestAddQuestProgress(quest, amount);
		}

		public bool SetProgressAtLeast(QuestId questId, int targetProgress)
		{
			QuestSO quest = GetQuest(questId);
			if (quest == null)
			{
				return false;
			}
			QuestManager instance = QuestManager.Instance;
			if (instance == null)
			{
				UtilityMethods.Log("[QuestReferenceHub] QuestManager is null.", LogType.Warning);
				return false;
			}
			return instance.RequestSetQuestProgressAtLeast(quest, targetProgress);
		}

		public bool IsActive(QuestId questId)
		{
			QuestSO quest = GetQuest(questId);
			if (quest == null || QuestManager.Instance == null)
			{
				return false;
			}
			return QuestManager.Instance.IsQuestActive(quest);
		}

		public bool IsCompleted(QuestId questId)
		{
			QuestSO quest = GetQuest(questId);
			if (quest == null || QuestManager.Instance == null)
			{
				return false;
			}
			return QuestManager.Instance.IsQuestCompleted(quest);
		}

		public bool IsActiveOrCompleted(QuestId questId)
		{
			if (!IsActive(questId))
			{
				return IsCompleted(questId);
			}
			return true;
		}

		public bool IsCurrentOrAlreadyDone(QuestId questId)
		{
			return IsActiveOrCompleted(questId);
		}

		private void BuildLookup()
		{
			_questsById.Clear();
			foreach (QuestId value in Enum.GetValues(typeof(QuestId)))
			{
				if (value != QuestId.None)
				{
					QuestSO questFromField = GetQuestFromField(value);
					if (!(questFromField == null))
					{
						_questsById[value] = questFromField;
					}
				}
			}
		}

		private QuestSO GetQuestFromField(QuestId questId)
		{
			string arg = ToFieldName(questId);
			FieldInfo field = GetType().GetField(arg, BindingFlags.Instance | BindingFlags.NonPublic);
			if (field == null)
			{
				UtilityMethods.Log($"[QuestReferenceHub] Field not found for {questId}. Expected field name: {arg}", LogType.Warning);
				return null;
			}
			return field.GetValue(this) as QuestSO;
		}

		private static string ToFieldName(QuestId questId)
		{
			string text = questId.ToString();
			if (string.IsNullOrWhiteSpace(text))
			{
				return string.Empty;
			}
			return "_" + char.ToLowerInvariant(text[0]) + text.Substring(1);
		}
	}
}
