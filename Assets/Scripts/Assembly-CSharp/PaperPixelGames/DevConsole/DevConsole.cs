using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PaperPixelGames.DevConsole
{
	public class DevConsole : MonoBehaviour
	{
		[Header("UI")]
		[SerializeField]
		private GameObject _consolePanel;

		[SerializeField]
		private TMP_InputField _inputField;

		[SerializeField]
		private GameObject _infoArea;

		[SerializeField]
		private TextMeshProUGUI _infoText;

		[Header("Behavior")]
		[SerializeField]
		private bool _openOnStart;

		[SerializeField]
		private bool _forceEnabled;

		[Header("Data")]
		[SerializeField]
		private ItemDatabaseSO _itemDatabase;

		private bool _isOpen;

		private DevConsoleContext _context;

		private Dictionary<string, IDevCommand> _commands = new Dictionary<string, IDevCommand>();

		private const string HistoryKey = "DevConsole.CommandHistory";

		private const int MaxHistoryCount = 50;

		private List<string> _commandHistory = new List<string>();

		private int _historyIndex = -1;

		public static DevConsole Instance { get; private set; }

		public static bool IsEnabled => false;

		private void Awake()
		{
			Instance = this;
			_isOpen = false;
			if (_consolePanel != null)
			{
				_consolePanel.SetActive(value: false);
			}
			base.enabled = false;
		}

		private void RegisterCommands()
		{
			_commands.Clear();
			SetCommand setCommand = new SetCommand();
			ProgressionCommand progressionCommand = new ProgressionCommand();
			foreach (IDevCommand item in new List<IDevCommand>
			{
				new HelpCommand(),
				new CurrencyShortcutCommand("money", setCommand),
				new CurrencyShortcutCommand("scrap", setCommand),
				new UiShortcutCommand(setCommand),
				new QuestShortcutCommand("quest", setCommand),
				new QuestShortcutCommand("group", setCommand),
				new QuestTestCommand(),
				new XpShortcutCommand(progressionCommand),
				new ProgressShortcutCommand(progressionCommand),
				new SpawnCommand(),
				new TutorialCommand(),
				new OxygenCommand(),
				new BalanceCommand(),
				new ToolCommand(),
				new BossCommand(),
				new FishBookCommand(),
				new TrophyCommand(),
				new ChefDeskCommand(),
				new BeltCommand(),
				new SharkCommand(),
				new NpcCommand(),
				progressionCommand,
				new TimeCommand(),
				new TeleportCommand(),
				setCommand,
				new GetCommand()
			})
			{
				_commands[item.Name] = item;
			}
		}

		private void Start()
		{
			if (!IsEnabled)
			{
				CloseConsolePanel();
				return;
			}
			if (_itemDatabase == null)
			{
				Debug.LogError("[DevConsole] ItemDatabaseSO is not assigned!");
			}
			EnsureInitialized();
			if (_inputField != null)
			{
				_inputField.onValueChanged.AddListener(UpdateInfoText);
			}
			LoadHistory();
			if (_openOnStart && IsEnabled)
			{
				OpenConsolePanel();
			}
			else
			{
				CloseConsolePanel();
			}
			WriteToInfo("");
		}

		private void OnDestroy()
		{
			if (Instance == this)
			{
				Instance = null;
			}
			SaveHistory();
		}

		public bool TryExecuteCommand(string input, Action<string> feedback, bool addToHistory)
		{
			if (!IsEnabled)
			{
				feedback?.Invoke("DevConsole is disabled.");
				return false;
			}
			EnsureInitialized();
			string text = ((input != null) ? input.Trim() : string.Empty);
			if (string.IsNullOrWhiteSpace(text))
			{
				feedback?.Invoke("Command is empty.");
				return false;
			}
			ExecuteCommand(text, feedback);
			if (addToHistory)
			{
				AddCommandHistory(text);
			}
			return true;
		}

		private void EnsureInitialized()
		{
			if (_context == null)
			{
				_context = new DevConsoleContext(_itemDatabase, base.transform);
			}
			if (_commands == null)
			{
				_commands = new Dictionary<string, IDevCommand>();
			}
			if (_commands.Count == 0)
			{
				RegisterCommands();
			}
		}

		private void LoadHistory()
		{
			if (PlayerPrefs.HasKey("DevConsole.CommandHistory"))
			{
				string json = PlayerPrefs.GetString("DevConsole.CommandHistory");
				_commandHistory = JsonUtility.FromJson<CommandHistoryWrapper>(json)?.History ?? new List<string>();
				_historyIndex = _commandHistory.Count;
			}
		}

		private void SaveHistory()
		{
			string value = JsonUtility.ToJson(new CommandHistoryWrapper
			{
				History = _commandHistory
			});
			PlayerPrefs.SetString("DevConsole.CommandHistory", value);
			PlayerPrefs.Save();
		}

		private void Update()
		{
			if (!IsEnabled)
			{
				if (_isOpen)
				{
					CloseConsolePanel();
				}
				return;
			}
			if (Input.GetKeyDown(KeyCode.BackQuote) || Input.GetKeyDown(KeyCode.F1))
			{
				ToggleConsole();
			}
			if (!_isOpen)
			{
				return;
			}
			if (Input.GetKeyDown(KeyCode.UpArrow) && _commandHistory.Count > 0)
			{
				_historyIndex = Mathf.Clamp(_historyIndex - 1, 0, _commandHistory.Count - 1);
				_inputField.text = _commandHistory[_historyIndex];
				_inputField.caretPosition = _inputField.text.Length;
			}
			if (Input.GetKeyDown(KeyCode.DownArrow) && _commandHistory.Count > 0)
			{
				_historyIndex = Mathf.Clamp(_historyIndex + 1, 0, _commandHistory.Count);
				if (_historyIndex == _commandHistory.Count)
				{
					_inputField.text = "";
				}
				else
				{
					_inputField.text = _commandHistory[_historyIndex];
				}
				_inputField.caretPosition = _inputField.text.Length;
			}
			if (Input.GetKeyDown(KeyCode.Return))
			{
				string text = _inputField.text.Trim();
				if (!string.IsNullOrEmpty(text))
				{
					TryExecuteCommand(text, WriteToInfo, addToHistory: true);
					_historyIndex = _commandHistory.Count;
					_inputField.text = "";
					_inputField.ActivateInputField();
					WriteToInfo("");
				}
			}
			if (!Input.GetKeyDown(KeyCode.Tab))
			{
				return;
			}
			string value = _inputField.text.Trim().ToLowerInvariant();
			if (string.IsNullOrEmpty(value))
			{
				return;
			}
			foreach (string item in _commands.Keys.OrderBy((string x) => x))
			{
				if (item.StartsWith(value))
				{
					_inputField.text = item + " ";
					_inputField.caretPosition = _inputField.text.Length;
					break;
				}
			}
		}

		private void ToggleConsole()
		{
			if (_isOpen)
			{
				CloseConsolePanel();
			}
			else
			{
				OpenConsolePanel();
			}
			WriteToInfo("");
		}

		private void OpenConsolePanel()
		{
			_isOpen = true;
			base.transform.localScale = Vector3.one;
			EnsureEventSystem();
			if (_consolePanel != null)
			{
				_consolePanel.SetActive(value: true);
			}
			if (_inputField != null)
			{
				_inputField.text = "";
				_inputField.ActivateInputField();
			}
			if (Singleton<PlayersManager>.Instance != null && Singleton<PlayersManager>.Instance.LocalPlayer != null)
			{
				Singleton<PlayersManager>.Instance.LocalPlayer.PlayerInput.DeactivateInput();
			}
		}

		private static void EnsureEventSystem()
		{
			if (!(EventSystem.current != null))
			{
				GameObject obj = new GameObject("[DevConsole EventSystem]");
				obj.AddComponent<EventSystem>();
				obj.AddComponent<StandaloneInputModule>();
			}
		}

		private void CloseConsolePanel()
		{
			bool isOpen = _isOpen;
			_isOpen = false;
			if (_consolePanel != null)
			{
				_consolePanel.SetActive(value: false);
			}
			if (isOpen)
			{
				SaveHistory();
			}
			if (isOpen && Singleton<PlayersManager>.Instance != null && Singleton<PlayersManager>.Instance.LocalPlayer != null)
			{
				Singleton<PlayersManager>.Instance.LocalPlayer.PlayerInput.ActivateInput();
			}
		}

		private void UpdateInfoText(string input)
		{
			EnsureInitialized();
			string key = input.Trim().Split(' ')[0].ToLowerInvariant();
			if (_commands.TryGetValue(key, out var value))
			{
				WriteToInfo(value.Description);
			}
			else
			{
				WriteToInfo("");
			}
		}

		private void WriteToInfo(string message)
		{
			if (!(_infoText == null) && !(_infoArea == null))
			{
				_infoText.text = message;
				_infoArea.SetActive(_infoText.text != "");
			}
		}

		private void AddCommandHistory(string input)
		{
			if (_commandHistory.Count != 0)
			{
				List<string> commandHistory = _commandHistory;
				if (!(commandHistory[commandHistory.Count - 1] != input))
				{
					goto IL_004f;
				}
			}
			_commandHistory.Add(input);
			if (_commandHistory.Count > 50)
			{
				_commandHistory.RemoveAt(0);
			}
			goto IL_004f;
			IL_004f:
			_historyIndex = _commandHistory.Count;
		}

		private void ExecuteCommand(string input, Action<string> feedback)
		{
			string[] array = input.Split(' ');
			if (array.Length != 0)
			{
				string text = array[0].ToLowerInvariant();
				if (_commands.TryGetValue(text, out var value))
				{
					value.Execute(_context, array, feedback);
				}
				else
				{
					feedback?.Invoke("Unknown command: " + text);
				}
			}
		}
	}
}
