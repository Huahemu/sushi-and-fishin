namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::System.Linq;
using global::System.Reflection;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::SushiAndFish.Casino.Steamworks;
using global::TMPro;
using global::UnityEngine;
using global::UnityEngine.UI;

public class NewConsole : NetworkSingleton<NewConsole>, IUIManager
{
	private enum ConsoleState
	{
		Main = 0,
		Spawn = 1,
		Count = 2,
		Value = 3,
		Teleport = 4,
		Money = 5,
		Simulate = 6,
		Settings = 7,
		CasinoLevel = 8,
		Cosmetics = 9,
		Cameraman = 10,
		Challenges = 11
	}

	private enum TeleportLocation
	{
		Lobby = 0,
		TestScene = 1,
		MainScene = 2
	}

	public ToggleSettingItem _devConsoleSetting;

	[Header("UI References")]
	[SerializeField]
	private GameObject consolePanel;

	[SerializeField]
	private Transform sectionContainer;

	[SerializeField]
	private Transform buttonContainer;

	[SerializeField]
	private SpawnableSettings spawnableSettings;

	[Header("Button Prefab")]
	[SerializeField]
	private GameObject sectionButtonPrefab;

	[SerializeField]
	private GameObject buttonPrefab;

	[Header("Settings")]
	[SerializeField]
	private float spawnRadius = 3f;

	[SerializeField]
	private Vector3 spawnOffset = new Vector3(5f, 1f, 5f);

	private bool isVisible;

	private ConsoleState lastUsedState = ConsoleState.Spawn;

	private bool _sectionsInitialized;

	private int _spawnableId = -1;

	private int _spawnIndex;

	private int selectedCount = 1;

	private int selectedValue = 1;

	private List<SpawnableSO> availablePrefabs = new List<SpawnableSO>();

	private GameSettings _gs;

	public bool IsActive => isVisible;

	public int Priority => 10;

	protected override void OnAwake()
	{
		base.OnAwake();
		_gs = SushiAndFish.Casino.Original.CasinoResources.Load<GameSettings>("GameSettings");
	}

	private void OnEnable()
	{
		InputEvents.OnConsoleEvent = (Action)Delegate.Combine(InputEvents.OnConsoleEvent, new Action(ToggleConsole));
		if (NetworkSingleton<MoneyManager>.Instance != null)
		{
			InputEvents.OnF1Event = (Action)Delegate.Combine(InputEvents.OnF1Event, new Action(HandleF1MoneyChange));
			InputEvents.OnF2Event = (Action)Delegate.Combine(InputEvents.OnF2Event, new Action(HandleF2MoneyChange));
		}
		if (UIManager.Instance != null)
		{
			UIManager.Instance.RegisterUI(this);
		}
		InputEvents.OnF3Event = (Action)Delegate.Combine(InputEvents.OnF3Event, new Action(ProgressGame));
	}

	private void OnDisable()
	{
		InputEvents.OnConsoleEvent = (Action)Delegate.Remove(InputEvents.OnConsoleEvent, new Action(ToggleConsole));
		if (NetworkSingleton<MoneyManager>.Instance != null)
		{
			InputEvents.OnF1Event = (Action)Delegate.Remove(InputEvents.OnF1Event, new Action(HandleF1MoneyChange));
			InputEvents.OnF2Event = (Action)Delegate.Remove(InputEvents.OnF2Event, new Action(HandleF2MoneyChange));
		}
		if (UIManager.Instance != null)
		{
			UIManager.Instance.UnregisterUI(this);
		}
		InputEvents.OnF3Event = (Action)Delegate.Remove(InputEvents.OnF3Event, new Action(ProgressGame));
	}

	private void HandleF1MoneyChange()
	{
		if (NetworkSingleton<MoneyManager>.Instance != null)
		{
			NetworkSingleton<MoneyManager>.Instance.CmdTryChangeBalance(100L, null, ChangeType.Misc);
		}
	}

	private void HandleF2MoneyChange()
	{
		if (NetworkSingleton<MoneyManager>.Instance != null)
		{
			NetworkSingleton<MoneyManager>.Instance.CmdTryChangeBalance(-100L, null, ChangeType.Misc);
		}
	}

	private void Start()
	{
		if (consolePanel != null)
		{
			consolePanel.SetActive(value: false);
		}
		GetAvailablePrefabs();
	}

	private void GetAvailablePrefabs()
	{
		availablePrefabs.Clear();
		if (spawnableSettings == null)
		{
			spawnableSettings = SushiAndFish.Casino.Original.CasinoResources.Load<SpawnableSettings>("SpawnableSettings");
		}
		if (spawnableSettings != null && spawnableSettings.isEnabled)
		{
			foreach (SpawnableSO spawnable in spawnableSettings.spawnables)
			{
				if (spawnable != null)
				{
					availablePrefabs.Add(spawnable);
				}
			}
			Debug.Log($"Found {availablePrefabs.Count} spawnable prefabs from SpawnableSettings");
		}
		else
		{
			Debug.LogWarning("SpawnableSettings not found or disabled. Please create a SpawnableSettings asset in Resources folder.");
		}
	}

	public void CloseUI()
	{
		if (isVisible)
		{
			isVisible = false;
			UICursorSimple.Instance?.HideCursor();
			if (consolePanel != null)
			{
				consolePanel.SetActive(value: false);
			}
			if (UIManager.Instance != null)
			{
				UIManager.Instance.ClearActiveUI(this);
			}
		}
	}

	public void OpenUI()
	{
		if (!isVisible)
		{
			isVisible = true;
			NetworkClient.localPlayer.GetComponent<PlayerController>().head.isLocked = true;
			UICursorSimple.Instance?.ShowCursor();
			if (consolePanel != null)
			{
				consolePanel.SetActive(value: true);
			}
			ShowMainMenu();
			ShowLastUsedTab();
			if (UIManager.Instance != null)
			{
				UIManager.Instance.SetActiveUI(this);
			}
		}
	}

	private void ToggleConsole()
	{
		if (IsActive)
		{
			CloseUI();
			Cursor.lockState = CursorLockMode.Locked;
			UICursorSimple.Instance?.HideCursor();
			NetworkIdentity localPlayer = NetworkClient.localPlayer;
			if (localPlayer != null)
			{
				PlayerController component = localPlayer.GetComponent<PlayerController>();
				if (component != null && component.head != null)
				{
					component.head.isLocked = false;
				}
			}
		}
		else
		{
			OpenUI();
		}
	}

	private void ShowMainMenu()
	{
		ClearButtons();
		if (!_sectionsInitialized && sectionContainer != null && sectionContainer.childCount == 0)
		{
			CreateSectionButton("Spawn", () =>
			{
				ShowSpawnMenu();
			});
			CreateSectionButton("Teleport", () =>
			{
				ShowTeleportMenu();
			});
			CreateSectionButton(GetNoclipButtonText(), () =>
			{
				ToggleNoclip();
			});
			CreateSectionButton("Money", () =>
			{
				ShowMoneyMenu();
			});
			CreateSectionButton("Simulate", () =>
			{
				ShowSimulateMenu();
			});
			CreateSectionButton("Cosmetics", () =>
			{
				ShowCosmeticsMenu();
			});
			CreateSectionButton(GetCameramanButtonText(), () =>
			{
				ShowCameramanMenu();
			});
			CreateSectionButton("Challenges", () =>
			{
				ShowChallengesMenu();
			});
			_sectionsInitialized = true;
		}
	}

	private void ShowLastUsedTab()
	{
		switch (lastUsedState)
		{
		case ConsoleState.Spawn:
			ShowSpawnMenu();
			break;
		case ConsoleState.Teleport:
			ShowTeleportMenu();
			break;
		case ConsoleState.Money:
			ShowMoneyMenu();
			break;
		case ConsoleState.Simulate:
			ShowSimulateMenu();
			break;
		case ConsoleState.Cosmetics:
			ShowCosmeticsMenu();
			break;
		case ConsoleState.Cameraman:
			ShowCameramanMenu();
			break;
		case ConsoleState.Challenges:
			ShowChallengesMenu();
			break;
		case ConsoleState.Count:
			ShowCountMenu();
			break;
		default:
			ShowSpawnMenu();
			break;
		}
	}

	private void ShowMoneyMenu()
	{
		lastUsedState = ConsoleState.Money;
		ClearButtons();
		CreateButton("Back", () =>
		{
			ShowMainMenu();
		});
		CreateButton("Reset To Default", () =>
		{
			NetworkSingleton<MoneyManager>.Instance.CmdResetBalancesToDefault();
		});
		CreateButton("+ 1 Ticket", () =>
		{
			NetworkSingleton<MoneyManager>.Instance.CmdTryChangeTicketBalance(1L);
		});
		CreateButton("+ 5 Ticket", () =>
		{
			NetworkSingleton<MoneyManager>.Instance.CmdTryChangeTicketBalance(5L);
		});
		CreateButton("+ 10 Ticket", () =>
		{
			NetworkSingleton<MoneyManager>.Instance.CmdTryChangeTicketBalance(10L);
		});
		CreateButton("- 1 Ticket", () =>
		{
			NetworkSingleton<MoneyManager>.Instance.CmdTryChangeTicketBalance(-1L);
		});
		CreateButton("- 5 Ticket", () =>
		{
			NetworkSingleton<MoneyManager>.Instance.CmdTryChangeTicketBalance(-5L);
		});
		CreateButton("- 10 Ticket", () =>
		{
			NetworkSingleton<MoneyManager>.Instance.CmdTryChangeTicketBalance(-10L);
		});
		CreateButton("+ $10", () =>
		{
			NetworkSingleton<MoneyManager>.Instance.CmdTryChangeBalance(10L, null, ChangeType.Misc);
		});
		CreateButton("+ $100", () =>
		{
			NetworkSingleton<MoneyManager>.Instance.CmdTryChangeBalance(100L, null, ChangeType.Misc);
		});
		CreateButton("+ $1K", () =>
		{
			NetworkSingleton<MoneyManager>.Instance.CmdTryChangeBalance(1000L, null, ChangeType.Misc);
		});
		CreateButton("+ $10K", () =>
		{
			NetworkSingleton<MoneyManager>.Instance.CmdTryChangeBalance(10000L, null, ChangeType.Misc);
		});
		CreateButton("+ $100K", () =>
		{
			NetworkSingleton<MoneyManager>.Instance.CmdTryChangeBalance(100000L, null, ChangeType.Misc);
		});
		CreateButton("+ $1M", () =>
		{
			NetworkSingleton<MoneyManager>.Instance.CmdTryChangeBalance(1000000L, null, ChangeType.Misc);
		});
		CreateButton("+ $10M", () =>
		{
			NetworkSingleton<MoneyManager>.Instance.CmdTryChangeBalance(10000000L, null, ChangeType.Misc);
		});
		CreateButton("+ $100M", () =>
		{
			NetworkSingleton<MoneyManager>.Instance.CmdTryChangeBalance(100000000L, null, ChangeType.Misc);
		});
		CreateButton("+ $1B", () =>
		{
			NetworkSingleton<MoneyManager>.Instance.CmdTryChangeBalance(1000000000L, null, ChangeType.Misc);
		});
		CreateButton("+ $10B", () =>
		{
			NetworkSingleton<MoneyManager>.Instance.CmdTryChangeBalance(10000000000L, null, ChangeType.Misc);
		});
		CreateButton("+ $100B", () =>
		{
			NetworkSingleton<MoneyManager>.Instance.CmdTryChangeBalance(100000000000L, null, ChangeType.Misc);
		});
		CreateButton("+ $1T", () =>
		{
			NetworkSingleton<MoneyManager>.Instance.CmdTryChangeBalance(1000000000000L, null, ChangeType.Misc);
		});
		CreateButton("- 10", () =>
		{
			NetworkSingleton<MoneyManager>.Instance.CmdTryChangeBalance(-10L, null, ChangeType.Misc);
		});
		CreateButton("- $100", () =>
		{
			NetworkSingleton<MoneyManager>.Instance.CmdTryChangeBalance(-100L, null, ChangeType.Misc);
		});
		CreateButton("- $1K", () =>
		{
			NetworkSingleton<MoneyManager>.Instance.CmdTryChangeBalance(-1000L, null, ChangeType.Misc);
		});
		CreateButton("- $10K", () =>
		{
			NetworkSingleton<MoneyManager>.Instance.CmdTryChangeBalance(-10000L, null, ChangeType.Misc);
		});
		CreateButton("- $100K", () =>
		{
			NetworkSingleton<MoneyManager>.Instance.CmdTryChangeBalance(-100000L, null, ChangeType.Misc);
		});
		CreateButton("- $1M", () =>
		{
			NetworkSingleton<MoneyManager>.Instance.CmdTryChangeBalance(-1000000L, null, ChangeType.Misc);
		});
		CreateButton("- $10M", () =>
		{
			NetworkSingleton<MoneyManager>.Instance.CmdTryChangeBalance(-10000000L, null, ChangeType.Misc);
		});
		CreateButton("- $100M", () =>
		{
			NetworkSingleton<MoneyManager>.Instance.CmdTryChangeBalance(-100000000L, null, ChangeType.Misc);
		});
		CreateButton("- $1B", () =>
		{
			NetworkSingleton<MoneyManager>.Instance.CmdTryChangeBalance(-1000000000L, null, ChangeType.Misc);
		});
		CreateButton("- $10B", () =>
		{
			NetworkSingleton<MoneyManager>.Instance.CmdTryChangeBalance(-10000000000L, null, ChangeType.Misc);
		});
		CreateButton("- $100B", () =>
		{
			NetworkSingleton<MoneyManager>.Instance.CmdTryChangeBalance(-100000000000L, null, ChangeType.Misc);
		});
		CreateButton("- $1T", () =>
		{
			NetworkSingleton<MoneyManager>.Instance.CmdTryChangeBalance(-1000000000000L, null, ChangeType.Misc);
		});
	}

	private void ShowSimulateMenu()
	{
		lastUsedState = ConsoleState.Simulate;
		ClearButtons();
		CreateButton("Back", () =>
		{
			ShowMainMenu();
		});
		CreateButton("Win", () =>
		{
			SimulateWinLose(isWin: true);
		});
		CreateButton("Lose", () =>
		{
			SimulateWinLose(isWin: false);
		});
		CreateButton("EnableFloors", () =>
		{
			CmdEnableAllFloors();
		});
		CreateButton("Customization", () =>
		{
			SimulateCustomization();
		});
		CreateButton("Reset Customization", () =>
		{
			ResetCustomization();
		});
		CreateButton("Graph", () =>
		{
			ToggleGraph();
		});
		CreateButton("Stats", () =>
		{
			ShowStats();
		});
		CreateButton("GameOver", () =>
		{
			ShowGameOver();
		});
	}

	private void ToggleGraph()
	{
		EndOfRoundData endOfRoundData = UnityEngine.Object.FindFirstObjectByType<EndOfRoundData>();
		if (endOfRoundData == null || endOfRoundData.canvasGroup == null)
		{
			Debug.LogWarning("[Console] EndOfRoundData or canvasGroup not found!");
			return;
		}
		bool flag = endOfRoundData.canvasGroup.alpha > 0.5f;
		endOfRoundData.canvasGroup.alpha = ((!flag) ? 1 : 0);
		if (!flag)
		{
			ProfitLineGraph3D profitLineGraph3D = UnityEngine.Object.FindFirstObjectByType<ProfitLineGraph3D>();
			if (profitLineGraph3D != null)
			{
				profitLineGraph3D.ResetAndAnimate();
			}
			else
			{
				Debug.LogWarning("[Console] ProfitLineGraph3D not found!");
			}
		}
	}

	private void ShowStats()
	{
		ToggleConsole();
		NetworkSingleton<GameManager>.Instance.ShowDayStats();
	}

	private void ShowGameOver()
	{
		ToggleConsole();
		NetworkSingleton<GameManager>.Instance.ShowGameOverStats();
	}

	private void ShowCosmeticsMenu()
	{
		lastUsedState = ConsoleState.Cosmetics;
		ClearButtons();
		CreateButton("Back", () =>
		{
			ShowMainMenu();
		});
		if (MonoSingleton<CosmeticsUnlockManager>.Instance == null)
		{
			CreateButton("ERROR: CosmeticsUnlockManager not found!", () =>
			{
			});
			return;
		}
		CosmeticsUnlockManager instance = MonoSingleton<CosmeticsUnlockManager>.Instance;
		CreateButton($"Unlocked: {instance.GetUnlockedCount()}/{instance.GetTotalCosmeticsCount()}", () =>
		{
		});
		CreateButton("Unlock Random Cosmetic", () =>
		{
			UnlockRandomCosmetic();
		});
		CreateButton("Unlock All Cosmetics", () =>
		{
			UnlockAllCosmetics();
		});
		CreateButton("Reset All Unlocks", () =>
		{
			ResetAllCosmetics();
		});
		CreateButton("Apply Random Cosmetic", () =>
		{
			ApplyRandomUnlockedCosmetic();
		});
		CreateButton("Reset Player Appearance", () =>
		{
			ResetPlayerAppearance();
		});
		bool initialized = SteamManager.Initialized;
		string text = (initialized ? "Steam: Connected" : "Steam: Offline");
		CreateButton(text, () =>
		{
		});
		if (initialized)
		{
			string text2 = (SteamRemoteStorage.FileExists("cosmetics_unlocks.json") ? "Cloud Save: Available" : "Cloud Save: None");
			CreateButton(text2, () =>
			{
			});
		}
	}

	private void ShowChallengesMenu()
	{
		lastUsedState = ConsoleState.Challenges;
		ClearButtons();
		CreateButton("Back", () =>
		{
			ShowMainMenu();
		});
		CreateButton("Complete All Active", () =>
		{
			CmdCompleteAllChallenges();
		});
		CreateButton("Clear All Challenges", () =>
		{
			CmdClearAllChallenges();
		});
		if (SushiAndFish.Casino.Original.CasinoResources.Load<ChallengeSettings>("ChallengeSettings") == null)
		{
			CreateButton("(No challenges in settings)", () =>
			{
			});
			return;
		}
		int num = ((NetworkSingleton<ChallengeManager>.Instance != null) ? NetworkSingleton<ChallengeManager>.Instance.GetActiveChallenges().Count : 0);
		CreateButton($"Active: {num}", () =>
		{
		});
		CreateButton("Floor 1", () =>
		{
			ShowChallengesForFloor(0);
		});
		CreateButton("Floor 2", () =>
		{
			ShowChallengesForFloor(1);
		});
		CreateButton("Floor 3", () =>
		{
			ShowChallengesForFloor(2);
		});
		CreateButton("Floor 4", () =>
		{
			ShowChallengesForFloor(3);
		});
	}

	private void ShowChallengesForFloor(int floorIndex)
	{
		ClearButtons();
		CreateButton("Back", () =>
		{
			ShowChallengesMenu();
		});
		ChallengeSettings challengeSettings = SushiAndFish.Casino.Original.CasinoResources.Load<ChallengeSettings>("ChallengeSettings");
		if (challengeSettings == null)
		{
			return;
		}
		List<Challenge> challengesByFloorIndex = challengeSettings.GetChallengesByFloorIndex(floorIndex);
		if (challengesByFloorIndex == null || challengesByFloorIndex.Count == 0)
		{
			CreateButton($"(No challenges for floor {floorIndex + 1})", () =>
			{
			});
			return;
		}
		foreach (Challenge item in challengesByFloorIndex.OrderBy((Challenge x) => (!(x != null)) ? "" : x.challengeName))
		{
			if (!(item == null))
			{
				int id = item.challengeID;
				string challengeName = item.challengeName;
				CreateButton(challengeName ?? "", () =>
				{
					CmdGiveChallengeById(id);
				});
			}
		}
	}

	[Command(requiresAuthority = false)]
	private void CmdGiveChallengeById(int challengeId)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(challengeId);
		SendCommandInternal("System.Void NewConsole::CmdGiveChallengeById(System.Int32)", 67421307, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[Command(requiresAuthority = false)]
	private void CmdCompleteAllChallenges()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendCommandInternal("System.Void NewConsole::CmdCompleteAllChallenges()", -1411737432, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[Command(requiresAuthority = false)]
	private void CmdClearAllChallenges()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendCommandInternal("System.Void NewConsole::CmdClearAllChallenges()", 1966610104, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	private void SimulateCustomization()
	{
		PlayerCustomization component = NetworkClient.localPlayer.GetComponent<PlayerCustomization>();
		CosmeticData[] array = SushiAndFish.Casino.Original.CasinoResources.LoadAll<CosmeticData>("Cosmetics");
		CosmeticData cosmeticData = array[UnityEngine.Random.Range(0, array.Length)];
		component.CmdChangeCustomization(cosmeticData.cosmeticId, shouldSave: true);
	}

	private void ResetCustomization()
	{
		NetworkClient.localPlayer.GetComponent<PlayerCustomization>().ResetCustomization();
	}

	private void UnlockRandomCosmetic()
	{
		if (MonoSingleton<CosmeticsUnlockManager>.Instance == null)
		{
			Debug.LogError("[Console] CosmeticsUnlockManager not found!");
			return;
		}
		int num = MonoSingleton<CosmeticsUnlockManager>.Instance.UnlockRandomCosmetic();
		if (num != -1)
		{
			Debug.Log($"[Console] Unlocked cosmetic ID: {num}");
			ShowCosmeticsMenu();
		}
		else
		{
			Debug.Log("[Console] All cosmetics are already unlocked!");
		}
	}

	private void UnlockAllCosmetics()
	{
		if (MonoSingleton<CosmeticsUnlockManager>.Instance == null)
		{
			Debug.LogError("[Console] CosmeticsUnlockManager not found!");
			return;
		}
		CosmeticData[] array = SushiAndFish.Casino.Original.CasinoResources.LoadAll<CosmeticData>("Cosmetics");
		int num = 0;
		CosmeticData[] array2 = array;
		foreach (CosmeticData cosmeticData in array2)
		{
			if (MonoSingleton<CosmeticsUnlockManager>.Instance.UnlockCosmetic(cosmeticData.cosmeticId))
			{
				num++;
			}
		}
		Debug.Log($"[Console] Unlocked {num} cosmetics!");
		ShowCosmeticsMenu();
	}

	private void ResetAllCosmetics()
	{
		if (MonoSingleton<CosmeticsUnlockManager>.Instance == null)
		{
			Debug.LogError("[Console] CosmeticsUnlockManager not found!");
			return;
		}
		MonoSingleton<CosmeticsUnlockManager>.Instance.ResetAllUnlocks();
		NetworkClient.localPlayer.GetComponent<PlayerCustomization>().ResetCustomization();
		Debug.Log("[Console] All cosmetic unlocks have been reset!");
		ShowCosmeticsMenu();
	}

	private void ApplyRandomUnlockedCosmetic()
	{
		if (MonoSingleton<CosmeticsUnlockManager>.Instance == null)
		{
			Debug.LogError("[Console] CosmeticsUnlockManager not found!");
			return;
		}
		int[] unlockedCosmetics = MonoSingleton<CosmeticsUnlockManager>.Instance.GetUnlockedCosmetics();
		if (unlockedCosmetics.Length == 0)
		{
			Debug.LogWarning("[Console] No cosmetics are unlocked yet!");
			return;
		}
		int num = unlockedCosmetics[UnityEngine.Random.Range(0, unlockedCosmetics.Length)];
		NetworkClient.localPlayer.GetComponent<PlayerCustomization>().CmdChangeCustomization(num, shouldSave: true);
		Debug.Log($"[Console] Applied random unlocked cosmetic ID: {num}");
	}

	private void ResetPlayerAppearance()
	{
		NetworkClient.localPlayer.GetComponent<PlayerCustomization>().ResetCustomization();
		Debug.Log("[Console] Player appearance has been reset!");
	}

	private void SimulateWinLose(bool isWin)
	{
		NetworkIdentity localPlayer = NetworkClient.localPlayer;
		if (localPlayer == null)
		{
			Debug.LogWarning("[Console] Cannot simulate win/loss: Local player not found");
			return;
		}
		PlayerProfile component = localPlayer.GetComponent<PlayerProfile>();
		if (component == null)
		{
			Debug.LogWarning("[Console] Cannot simulate win/loss: PlayerProfile not found on local player");
			return;
		}
		Vector3 position = localPlayer.transform.position;
		Debug.Log(string.Format("[Console] Requesting simulate {0} for player at {1}", isWin ? "WIN" : "LOSS", position));
		CmdSimulateWinLose(component.netId, position, isWin);
	}

	[Command(requiresAuthority = false)]
	private void CmdSimulateWinLose(uint playerNetId, Vector3 playerPosition, bool isWin)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarUInt(playerNetId);
		writer.WriteVector3(playerPosition);
		writer.WriteBool(isWin);
		SendCommandInternal("System.Void NewConsole::CmdSimulateWinLose(System.UInt32,UnityEngine.Vector3,System.Boolean)", 1061694545, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[Command(requiresAuthority = false)]
	private void CmdEnableAllFloors()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendCommandInternal("System.Void NewConsole::CmdEnableAllFloors()", -145919939, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	private GameBase FindNearestGameServer(Vector3 position)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'GameBase NewConsole::FindNearestGameServer(UnityEngine.Vector3)' called when server was not active");
			return null;
		}
		GameBase[] array = UnityEngine.Object.FindObjectsByType<GameBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
		if (array == null || array.Length == 0)
		{
			return null;
		}
		GameBase result = null;
		float num = float.MaxValue;
		GameBase[] array2 = array;
		foreach (GameBase gameBase in array2)
		{
			if (!(gameBase == null))
			{
				float num2 = Vector3.Distance(position, gameBase.transform.position);
				if (num2 < num)
				{
					num = num2;
					result = gameBase;
				}
			}
		}
		return result;
	}

	private void ShowSpawnMenu()
	{
		lastUsedState = ConsoleState.Spawn;
		ClearButtons();
		foreach (SpawnableSO prefab in availablePrefabs.OrderBy((SpawnableSO p) => p.name))
		{
			CreateButton(prefab.name, () =>
			{
				OnSpawnableSelected(prefab.spawnableID);
			}, prefab.prefab);
		}
	}

	private void ShowCountMenu()
	{
		lastUsedState = ConsoleState.Count;
		ClearButtons();
		CreateButton("Back", () =>
		{
			ResetSelection();
			ShowSpawnMenu();
		});
		int[] array = new int[6] { 1, 5, 10, 25, 50, 100 };
		for (int num = 0; num < array.Length; num++)
		{
			int count = array[num];
			CreateButton("x" + count, () =>
			{
				OnCountSelected(count);
			});
		}
	}

	private void CreateButton(string text, Action onClick)
	{
		CreateButton(text, onClick, null);
	}

	private void CreateButton(string text, Action onClick, GameObject prefab)
	{
		if (buttonPrefab == null || buttonContainer == null)
		{
			return;
		}
		GameObject obj = UnityEngine.Object.Instantiate(buttonPrefab, buttonContainer);
		Button component = obj.GetComponent<Button>();
		TextMeshProUGUI componentInChildren = obj.GetComponentInChildren<TextMeshProUGUI>();
		if (componentInChildren != null)
		{
			componentInChildren.text = text;
		}
		if (component != null)
		{
			component.onClick.AddListener(() =>
			{
				onClick();
			});
		}
	}

	private void CreateSectionButton(string text, Action onClick)
	{
		if (sectionButtonPrefab == null || sectionContainer == null)
		{
			return;
		}
		GameObject obj = UnityEngine.Object.Instantiate(sectionButtonPrefab, sectionContainer);
		Button component = obj.GetComponent<Button>();
		TextMeshProUGUI componentInChildren = obj.GetComponentInChildren<TextMeshProUGUI>();
		if (componentInChildren != null)
		{
			componentInChildren.text = text;
		}
		if (component != null)
		{
			component.onClick.AddListener(() =>
			{
				onClick();
			});
		}
	}

	private Texture2D GetPrefabPreview(GameObject prefab)
	{
		_ = prefab == null;
		return null;
	}

	private void ClearButtons()
	{
		if (buttonContainer != null)
		{
			for (int num = buttonContainer.childCount - 1; num >= 0; num--)
			{
				UnityEngine.Object.DestroyImmediate(buttonContainer.GetChild(num).gameObject);
			}
		}
	}

	private void OnSpawnableSelected(int id)
	{
		_spawnableId = id;
		ShowCountMenu();
	}

	private void OnCountSelected(int count)
	{
		selectedCount = count;
		SpawnSelectedPrefab();
	}

	private bool IsPhysicObject(int id)
	{
		SpawnableSO spawnableSoById = SpawnableSettings.GetSpawnableSoById(id);
		if (spawnableSoById == null || spawnableSoById.prefab == null)
		{
			return false;
		}
		if (spawnableSoById.prefab.TryGetComponent<Rigidbody>(out var component) && !component.isKinematic)
		{
			return true;
		}
		return false;
	}

	private void SpawnSelectedPrefab()
	{
		if (_spawnableId < 0)
		{
			Debug.LogError($"Cannot spawn: Invalid spawnable ID {_spawnableId}. Please select a spawnable first.");
			return;
		}
		if (selectedCount <= 0)
		{
			Debug.LogWarning($"Cannot spawn: Invalid count {selectedCount}. Setting to 1.");
			selectedCount = 1;
		}
		Vector3 playerPosition = GetPlayerPosition(!IsPhysicObject(_spawnableId));
		for (int i = 0; i < selectedCount; i++)
		{
			SpawnPrefab(_spawnableId, playerPosition, 1);
		}
	}

	private void ResetSelection()
	{
		_spawnableId = -1;
		selectedCount = 1;
		selectedValue = 1;
	}

	private void ShowTeleportMenu()
	{
		lastUsedState = ConsoleState.Teleport;
		ClearButtons();
		CreateButton("Back", () =>
		{
			ShowMainMenu();
		});
		CreateButton("Progress", () =>
		{
			ProgressGame();
		});
		CreateButton("Lobby", () =>
		{
			TeleportToScene(GameState.Lobby);
		});
		CreateButton("Game", () =>
		{
			TeleportToScene(GameState.Game);
		});
		CreateButton("Lose", () =>
		{
			TeleportToScene(GameState.Lose);
		});
		CreateButton("Win", () =>
		{
			TeleportToScene(GameState.Win);
		});
		CreateButton("CutsceneCoinflipWon", () =>
		{
			TeleportToCutscene(0);
		});
		CreateButton("CutsceneCoinflipLost", () =>
		{
			TeleportToCutscene(1);
		});
		CreateButton("CutsceneDebtPaid", () =>
		{
			TeleportToCutscene(2);
		});
		CreateButton("Summary", () =>
		{
			TeleportToScene(GameState.Summary);
		});
		CreateButton("FollowUs", () =>
		{
			TeleportToScene(GameState.FollowUs);
		});
		CreateButton("Test", () =>
		{
			TeleportToScene(GameState.Test);
		});
		foreach (PlayerProfile player in GetAllPlayerProfiles())
		{
			if (player != null && !string.IsNullOrEmpty(player.playerName))
			{
				CreateButton("Teleport to " + player.playerName, () =>
				{
					TeleportToPlayer(player);
				});
			}
		}
	}

	private void TeleportToScene(GameState state)
	{
		ToggleConsole();
		CmdTeleportPlayers(state);
	}

	private void TeleportToCutscene(int cutsceneIndex)
	{
		ToggleConsole();
		CmdTeleportToCutscene(cutsceneIndex);
	}

	private void ProgressGame()
	{
		if (isVisible)
		{
			ToggleConsole();
		}
		CmdProgressGame();
	}

	[Command(requiresAuthority = false)]
	private void CmdTeleportPlayers(GameState sceneState)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		GeneratedNetworkCode._Write_GameState(writer, sceneState);
		SendCommandInternal("System.Void NewConsole::CmdTeleportPlayers(GameState)", -1457229490, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[Command(requiresAuthority = false)]
	private void CmdTeleportToCutscene(int cutsceneIndex)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(cutsceneIndex);
		SendCommandInternal("System.Void NewConsole::CmdTeleportToCutscene(System.Int32)", 1143405009, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[Command(requiresAuthority = false)]
	private void CmdProgressGame()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendCommandInternal("System.Void NewConsole::CmdProgressGame()", 28004343, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	private Vector3 GetNextSpawnPos()
	{
		int num = NetworkServer.connections.Count;
		if (num <= 0)
		{
			num = 1;
		}
		float num2 = 360f / (float)num;
		float f = (float)_spawnIndex * (MathF.PI / 180f) * num2;
		float x = Mathf.Sin(f) * spawnRadius;
		float z = Mathf.Cos(f) * spawnRadius;
		_spawnIndex = (_spawnIndex + 1) % num;
		return new Vector3(x, 0f, z) + spawnOffset;
	}

	private void ToggleNoclip()
	{
		NetworkIdentity localPlayer = NetworkClient.localPlayer;
		if (localPlayer != null)
		{
			Noclip component = localPlayer.GetComponent<Noclip>();
			if (component != null)
			{
				component.ToggleNoclip();
				UpdateNoclipButtonText();
			}
			else
			{
				Debug.LogWarning("Noclip component not found on player. Please add the Noclip component to the player prefab.");
			}
		}
		else
		{
			Debug.LogWarning("Local player not found. Cannot toggle noclip mode.");
		}
	}

	private string GetNoclipButtonText()
	{
		if (!IsNoclipActive())
		{
			return "Noclip: OFF";
		}
		return "Noclip: ON";
	}

	private bool IsNoclipActive()
	{
		NetworkIdentity localPlayer = NetworkClient.localPlayer;
		if (localPlayer != null)
		{
			Noclip component = localPlayer.GetComponent<Noclip>();
			if (component != null)
			{
				FieldInfo field = typeof(Noclip).GetField("_isNoclipActive", BindingFlags.Instance | BindingFlags.NonPublic);
				if (field != null)
				{
					return (bool)field.GetValue(component);
				}
			}
		}
		return false;
	}

	private void UpdateNoclipButtonText()
	{
		if (sectionContainer == null)
		{
			return;
		}
		for (int i = 0; i < sectionContainer.childCount; i++)
		{
			TextMeshProUGUI componentInChildren = sectionContainer.GetChild(i).gameObject.GetComponentInChildren<TextMeshProUGUI>();
			if (componentInChildren != null && (componentInChildren.text == "Noclip: ON" || componentInChildren.text == "Noclip: OFF"))
			{
				componentInChildren.text = GetNoclipButtonText();
				break;
			}
		}
	}

	private void SpawnPrefab(int id, Vector3 position, int chipValue)
	{
		if (!NetworkManager.singleton.isNetworkActive)
		{
			Debug.LogWarning("Network not active. Cannot spawn prefabs.");
		}
		else
		{
			RequestSpawnPrefab(id, position, chipValue);
		}
	}

	[Server]
	private void SpawnPrefabServer(GameObject prefab, Vector3 position, int chipValue)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void NewConsole::SpawnPrefabServer(UnityEngine.GameObject,UnityEngine.Vector3,System.Int32)' called when server was not active");
			return;
		}
		NetworkServer.Spawn(UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity));
		Debug.Log($"Spawned networked prefab '{prefab.name}' at {position}");
	}

	[Command(requiresAuthority = false)]
	private void RequestSpawnPrefab(int id, Vector3 position, int chipValue)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(id);
		writer.WriteVector3(position);
		writer.WriteVarInt(chipValue);
		SendCommandInternal("System.Void NewConsole::RequestSpawnPrefab(System.Int32,UnityEngine.Vector3,System.Int32)", -169869193, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	private Vector3 GetPlayerPosition(bool shouldBeGrounded)
	{
		NetworkIdentity localPlayer = NetworkClient.localPlayer;
		if (!localPlayer || !localPlayer.TryGetComponent<PlayerController>(out var component))
		{
			return Vector3.zero;
		}
		if (shouldBeGrounded)
		{
			Vector3 normalized = Vector3.ProjectOnPlane(component.head.transform.forward, Vector3.up).normalized;
			return component.transform.position + normalized * 3f;
		}
		return component.head.transform.position + component.head.transform.forward * 2f;
	}

	private List<PlayerProfile> GetAllPlayerProfiles()
	{
		List<PlayerProfile> list = new List<PlayerProfile>();
		PlayerProfile[] array = UnityEngine.Object.FindObjectsByType<PlayerProfile>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
		foreach (PlayerProfile playerProfile in array)
		{
			if (playerProfile != null && playerProfile.hasSynced && !string.IsNullOrEmpty(playerProfile.playerName))
			{
				list.Add(playerProfile);
			}
		}
		return list;
	}

	private void TeleportToPlayer(PlayerProfile targetPlayer)
	{
		if (!(targetPlayer == null))
		{
			Vector3 position = targetPlayer.transform.position + Vector3.up * 2f;
			CmdTeleportToPlayer(targetPlayer.netId, position);
		}
	}

	[Command(requiresAuthority = false)]
	private void CmdTeleportToPlayer(uint targetPlayerNetId, Vector3 position, NetworkConnectionToClient conn = null)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarUInt(targetPlayerNetId);
		writer.WriteVector3(position);
		SendCommandInternal("System.Void NewConsole::CmdTeleportToPlayer(System.UInt32,UnityEngine.Vector3,Mirror.NetworkConnectionToClient)", 302698187, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	private void ShowCameramanMenu()
	{
		lastUsedState = ConsoleState.Cameraman;
		ClearButtons();
		CreateButton("Back", () =>
		{
			ShowMainMenu();
		});
		CreateButton(GetCameramanToggleButtonText(), () =>
		{
			ToggleCameramanMode();
		});
		CreateButton(GetVisibilityToggleButtonText(), () =>
		{
			ToggleCameramanVisibility();
		});
		CreateButton(GetCanvasToggleButtonText(), () =>
		{
			ToggleCameramanCanvas();
		});
	}

	private void ToggleCameramanMode()
	{
		NetworkIdentity localPlayer = NetworkClient.localPlayer;
		if (localPlayer != null)
		{
			CameramanMode component = localPlayer.GetComponent<CameramanMode>();
			if (component != null)
			{
				component.ToggleCameramanMode();
				UpdateCameramanButtonText();
			}
			else
			{
				Debug.LogWarning("CameramanMode component not found on player.");
			}
		}
	}

	private string GetCameramanButtonText()
	{
		if (!IsCameramanActive())
		{
			return "Cameraman: OFF";
		}
		return "Cameraman: ON";
	}

	private string GetCameramanToggleButtonText()
	{
		if (!IsCameramanActive())
		{
			return "Enable Cameraman Mode";
		}
		return "Disable Cameraman Mode";
	}

	private bool IsCameramanActive()
	{
		NetworkIdentity localPlayer = NetworkClient.localPlayer;
		if (localPlayer != null)
		{
			CameramanMode component = localPlayer.GetComponent<CameramanMode>();
			if (component != null)
			{
				return component.IsActive;
			}
		}
		return false;
	}

	private void ToggleCameramanVisibility()
	{
		NetworkIdentity localPlayer = NetworkClient.localPlayer;
		if (localPlayer != null)
		{
			CameramanMode component = localPlayer.GetComponent<CameramanMode>();
			if (component != null)
			{
				component.ToggleVisibility();
				UpdateCameramanButtonText();
			}
		}
	}

	private string GetVisibilityToggleButtonText()
	{
		if (!IsCameramanVisible())
		{
			return "Show Player";
		}
		return "Hide Player";
	}

	private bool IsCameramanVisible()
	{
		NetworkIdentity localPlayer = NetworkClient.localPlayer;
		if (localPlayer != null)
		{
			CameramanMode component = localPlayer.GetComponent<CameramanMode>();
			if (component != null)
			{
				return component.IsVisible;
			}
		}
		return true;
	}

	private void ToggleCameramanCanvas()
	{
		NetworkIdentity localPlayer = NetworkClient.localPlayer;
		if (localPlayer != null)
		{
			CameramanMode component = localPlayer.GetComponent<CameramanMode>();
			if (component != null)
			{
				component.ToggleCameramanCanvas();
				UpdateCameramanButtonText();
			}
		}
	}

	private string GetCanvasToggleButtonText()
	{
		if (!IsCameramanCanvasActive())
		{
			return "Show Camera Canvas";
		}
		return "Hide Camera Canvas";
	}

	private bool IsCameramanCanvasActive()
	{
		NetworkIdentity localPlayer = NetworkClient.localPlayer;
		if (localPlayer != null)
		{
			CameramanMode component = localPlayer.GetComponent<CameramanMode>();
			if (component != null)
			{
				return component.IsCameramanCanvasActive();
			}
		}
		return false;
	}

	private void UpdateCameramanButtonText()
	{
		if (sectionContainer == null)
		{
			return;
		}
		for (int i = 0; i < sectionContainer.childCount; i++)
		{
			TextMeshProUGUI componentInChildren = sectionContainer.GetChild(i).gameObject.GetComponentInChildren<TextMeshProUGUI>();
			if (componentInChildren != null && (componentInChildren.text == "Cameraman: ON" || componentInChildren.text == "Cameraman: OFF"))
			{
				componentInChildren.text = GetCameramanButtonText();
				break;
			}
		}
		if (lastUsedState == ConsoleState.Cameraman)
		{
			ShowCameramanMenu();
		}
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_CmdGiveChallengeById__Int32(int challengeId)
	{
		if (NetworkSingleton<ChallengeManager>.Instance != null)
		{
			NetworkSingleton<ChallengeManager>.Instance.ServerActivateChallengeById(challengeId);
		}
	}

	protected static void InvokeUserCode_CmdGiveChallengeById__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdGiveChallengeById called on client.");
		}
		else
		{
			((NewConsole)obj).UserCode_CmdGiveChallengeById__Int32(reader.ReadVarInt());
		}
	}

	protected void UserCode_CmdCompleteAllChallenges()
	{
		NetworkSingleton<ChallengeManager>.Instance?.ServerCompleteAllActiveChallenges();
	}

	protected static void InvokeUserCode_CmdCompleteAllChallenges(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdCompleteAllChallenges called on client.");
		}
		else
		{
			((NewConsole)obj).UserCode_CmdCompleteAllChallenges();
		}
	}

	protected void UserCode_CmdClearAllChallenges()
	{
		NetworkSingleton<ChallengeManager>.Instance?.ServerResetAllChallenges();
	}

	protected static void InvokeUserCode_CmdClearAllChallenges(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdClearAllChallenges called on client.");
		}
		else
		{
			((NewConsole)obj).UserCode_CmdClearAllChallenges();
		}
	}

	protected void UserCode_CmdSimulateWinLose__UInt32__Vector3__Boolean(uint playerNetId, Vector3 playerPosition, bool isWin)
	{
		if (!NetworkServer.spawned.TryGetValue(playerNetId, out var value))
		{
			Debug.LogWarning($"[Console] Cannot simulate payout: Player with netId {playerNetId} not found");
			return;
		}
		PlayerProfile component = value.GetComponent<PlayerProfile>();
		if (component == null)
		{
			Debug.LogWarning("[Console] Cannot simulate payout: PlayerProfile component not found on " + value.name);
			return;
		}
		GameBase gameBase = FindNearestGameServer(playerPosition);
		if (gameBase == null)
		{
			Debug.LogWarning($"[Console] Cannot simulate payout: No game found near player at {playerPosition}");
			return;
		}
		Debug.Log(string.Format("[Console] Simulating {0} on game: {1} at {2}", isWin ? "WIN" : "LOSS", gameBase.GameName, gameBase.transform.position));
		gameBase.ServerSimulatePayout(component, isWin);
	}

	protected static void InvokeUserCode_CmdSimulateWinLose__UInt32__Vector3__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdSimulateWinLose called on client.");
		}
		else
		{
			((NewConsole)obj).UserCode_CmdSimulateWinLose__UInt32__Vector3__Boolean(reader.ReadVarUInt(), reader.ReadVector3(), reader.ReadBool());
		}
	}

	protected void UserCode_CmdEnableAllFloors()
	{
		NetworkSingleton<ElevatorManager>.Instance?.RpcEnableAllButtons();
	}

	protected static void InvokeUserCode_CmdEnableAllFloors(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdEnableAllFloors called on client.");
		}
		else
		{
			((NewConsole)obj).UserCode_CmdEnableAllFloors();
		}
	}

	protected void UserCode_CmdTeleportPlayers__GameState(GameState sceneState)
	{
		NetworkSingleton<GameManager>.Instance.ServerSetScene(sceneState);
	}

	protected static void InvokeUserCode_CmdTeleportPlayers__GameState(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdTeleportPlayers called on client.");
		}
		else
		{
			((NewConsole)obj).UserCode_CmdTeleportPlayers__GameState(GeneratedNetworkCode._Read_GameState(reader));
		}
	}

	protected void UserCode_CmdTeleportToCutscene__Int32(int cutsceneIndex)
	{
		NetworkSingleton<GameManager>.Instance.ServerSetCutscene(cutsceneIndex);
	}

	protected static void InvokeUserCode_CmdTeleportToCutscene__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdTeleportToCutscene called on client.");
		}
		else
		{
			((NewConsole)obj).UserCode_CmdTeleportToCutscene__Int32(reader.ReadVarInt());
		}
	}

	protected void UserCode_CmdProgressGame()
	{
		NetworkSingleton<GameManager>.Instance.ProgressGame();
	}

	protected static void InvokeUserCode_CmdProgressGame(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdProgressGame called on client.");
		}
		else
		{
			((NewConsole)obj).UserCode_CmdProgressGame();
		}
	}

	protected void UserCode_RequestSpawnPrefab__Int32__Vector3__Int32(int id, Vector3 position, int chipValue)
	{
		SpawnableSO spawnableSoById = SpawnableSettings.GetSpawnableSoById(id);
		if (spawnableSoById == null || spawnableSoById.prefab == null)
		{
			Debug.LogError($"Cannot spawn prefab: Spawnable with ID {id} not found or prefab is null.");
		}
		else
		{
			SpawnPrefabServer(spawnableSoById.prefab, position, chipValue);
		}
	}

	protected static void InvokeUserCode_RequestSpawnPrefab__Int32__Vector3__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command RequestSpawnPrefab called on client.");
		}
		else
		{
			((NewConsole)obj).UserCode_RequestSpawnPrefab__Int32__Vector3__Int32(reader.ReadVarInt(), reader.ReadVector3(), reader.ReadVarInt());
		}
	}

	protected void UserCode_CmdTeleportToPlayer__UInt32__Vector3__NetworkConnectionToClient(uint targetPlayerNetId, Vector3 position, NetworkConnectionToClient conn)
	{
		if (!(NetworkServer.spawned[targetPlayerNetId] == null))
		{
			conn?.identity.GetComponent<PlayerController>().ServerTeleport(position);
		}
	}

	protected static void InvokeUserCode_CmdTeleportToPlayer__UInt32__Vector3__NetworkConnectionToClient(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdTeleportToPlayer called on client.");
		}
		else
		{
			((NewConsole)obj).UserCode_CmdTeleportToPlayer__UInt32__Vector3__NetworkConnectionToClient(reader.ReadVarUInt(), reader.ReadVector3(), senderConnection);
		}
	}

	static NewConsole()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(NewConsole), "System.Void NewConsole::CmdGiveChallengeById(System.Int32)", InvokeUserCode_CmdGiveChallengeById__Int32, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(NewConsole), "System.Void NewConsole::CmdCompleteAllChallenges()", InvokeUserCode_CmdCompleteAllChallenges, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(NewConsole), "System.Void NewConsole::CmdClearAllChallenges()", InvokeUserCode_CmdClearAllChallenges, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(NewConsole), "System.Void NewConsole::CmdSimulateWinLose(System.UInt32,UnityEngine.Vector3,System.Boolean)", InvokeUserCode_CmdSimulateWinLose__UInt32__Vector3__Boolean, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(NewConsole), "System.Void NewConsole::CmdEnableAllFloors()", InvokeUserCode_CmdEnableAllFloors, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(NewConsole), "System.Void NewConsole::CmdTeleportPlayers(GameState)", InvokeUserCode_CmdTeleportPlayers__GameState, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(NewConsole), "System.Void NewConsole::CmdTeleportToCutscene(System.Int32)", InvokeUserCode_CmdTeleportToCutscene__Int32, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(NewConsole), "System.Void NewConsole::CmdProgressGame()", InvokeUserCode_CmdProgressGame, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(NewConsole), "System.Void NewConsole::RequestSpawnPrefab(System.Int32,UnityEngine.Vector3,System.Int32)", InvokeUserCode_RequestSpawnPrefab__Int32__Vector3__Int32, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(NewConsole), "System.Void NewConsole::CmdTeleportToPlayer(System.UInt32,UnityEngine.Vector3,Mirror.NetworkConnectionToClient)", InvokeUserCode_CmdTeleportToPlayer__UInt32__Vector3__NetworkConnectionToClient, requiresAuthority: false);
	}
}

}
