using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Data.Ai.NPCs.Dialogue;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.Utility;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Managers
{
	public sealed partial class DialoguePanelController : MonoBehaviour
	{
		private enum DialogueMode
		{
			Menu = 0,
			QuestInfo = 1,
			QuestAnswer = 2,
			QuestEnd = 3,
			JokeQuestion = 4,
			JokeAnswer = 5
		}

		[Header("Data")]
		[SerializeField]
		private EmployeeDialogueDatabaseSO _employeeDialogueDb;

		[Header("Refs")]
		[SerializeField]
		private GameObject _panelRoot;

		[Header("Buttons Root")]
		[SerializeField]
		private GameObject _buttonsRoot;

		[Header("Menu Buttons")]
		[SerializeField]
		private BaseButton _purchaseButton;

		[SerializeField]
		private BaseButton _questButton;

		[SerializeField]
		private BaseButton _jokeButton;

		[SerializeField]
		private BaseButton _quitButton;

		[SerializeField]
		private Color _questButtonImageColor = new Color(0.55f, 0.38f, 0.08f, 0.85f);

		[Header("Sub Button")]
		[SerializeField]
		private BaseButton _continueButton;

		[Header("Quest Dialogues")]
		[SerializeField]
		private List<QuestDialogueSO> _questDialogues;

		private DialogueMode _mode;

		private ActorType _actorType;

		private ResidentController _currentInteractedResident;

		private ResidentBillboardText _billboard;

		private EmployeeDialogueSO _employee;

		private QuestDialogueSO _questForThisNpc;

		private EmployeeDialogueSO.TwoPartJoke _currentJoke;

		private ActorType _lastShopActorType;

		private ResidentBillboardText _lastShopBillboard;

		private ResidentController _lastShopResident;

		private bool _isTyping;

		private bool _cancelRegistered;

		public Action<string> OnQuestEndAction;

		public bool IsVisible => _panelRoot.activeSelf;

		private void OnValidate()
		{
			ApplyQuestButtonImageColor();
		}

		public void Initialize()
		{
			SetVisible(visible: false);
			ApplyQuestButtonImageColor();
			_purchaseButton.Clicked += HandlePurchase;
			_questButton.Clicked += HandleQuestInfo;
			_jokeButton.Clicked += HandleJoke;
			_continueButton.Clicked += HandleContinue;
			_quitButton.Clicked += HandleQuit;
			_purchaseButton.SetLocalizedText("UiTable", "dialogue.menu.purchase", "I'd like to purchase something");
			_jokeButton.SetLocalizedText("UiTable", "dialogue.menu.joke", "Do you wanna hear a joke?");
			_quitButton.SetLocalizedText("UiTable", "dialogue.menu.quit", "Quit");
		}

		private void ApplyQuestButtonImageColor()
		{
			if (!(_questButton == null) && !(_questButton.targetGraphic == null))
			{
				_questButton.targetGraphic.color = _questButtonImageColor;
			}
		}

		private void OnDestroy()
		{
			UnregisterCancelEvent();
			if (_purchaseButton != null)
			{
				_purchaseButton.Clicked -= HandlePurchase;
			}
			if (_questButton != null)
			{
				_questButton.Clicked -= HandleQuestInfo;
			}
			if (_jokeButton != null)
			{
				_jokeButton.Clicked -= HandleJoke;
			}
			if (_continueButton != null)
			{
				_continueButton.Clicked -= HandleContinue;
			}
			if (_quitButton != null)
			{
				_quitButton.Clicked -= HandleQuit;
			}
		}

		public void ShowDialoguePanel(ActorType actorType, ResidentBillboardText billboard, List<string> relatedQuestIds, ResidentController interactedResident)
		{
			if (!(_employeeDialogueDb == null) && _employeeDialogueDb.TryGet(actorType, out var employee) && !(employee == null))
			{
				_currentInteractedResident = interactedResident;
				_actorType = actorType;
				_billboard = billboard;
				_employee = employee;
				_questForThisNpc = ResolveActiveQuestForNpc(relatedQuestIds);
				_mode = DialogueMode.Menu;
				RegisterCancelEvent();
				SetVisible(visible: true);
				ApplyModeUI(showButtons: false);
				PlayBillboardText(_employee.GetRandomGreeting());
			}
		}

		public bool TryFastForwardForResident(ResidentController resident)
		{
			if (resident == null)
			{
				return false;
			}
			if (_panelRoot == null || !_panelRoot.activeSelf)
			{
				return false;
			}
			if (_currentInteractedResident != resident)
			{
				return false;
			}
			if (_isTyping)
			{
				_billboard?.CompleteInstant();
				if (_isTyping)
				{
					UtilityMethods.Log($"[DialoguePanel] Billboard callback missed after resident fast-forward. Forcing finish. Mode={_mode}", LogType.Warning);
					OnTypingFinished();
				}
				return true;
			}
			return true;
		}

		public void NotifyBillboardTypingFinished(ResidentController resident)
		{
			if (!(resident == null) && !(_panelRoot == null) && _panelRoot.activeSelf && !(_currentInteractedResident != resident) && _isTyping)
			{
				OnTypingFinished();
			}
		}

		public void Hide(bool restorePlayerInput = true, bool enableResidentInteract = true, bool clearShopActiveFlag = true)
		{
			Action customClosed = EndCustomConversation();
			UnregisterCancelEvent();
			ResidentController currentInteractedResident = _currentInteractedResident;
			SetVisible(visible: false);
			currentInteractedResident?.RequestHideDialogueBillboard();
			if (clearShopActiveFlag && Singleton<GameUIManager>.Instance != null)
			{
				Singleton<GameUIManager>.Instance.IsShopUIActive = false;
			}
			if (restorePlayerInput)
			{
				PPG_Player localPlayer = Singleton<PlayersManager>.Instance.LocalPlayer;
				if (localPlayer != null)
				{
					localPlayer.SwitchTo("Player", cursorVisible: false);
					localPlayer.ShowCursor();
				}
			}
			if (enableResidentInteract)
			{
				currentInteractedResident?.ServerEnableInteractRpc();
			}
			_mode = DialogueMode.Menu;
			_isTyping = false;
			_actorType = ActorType.Empty;
			_billboard = null;
			_employee = null;
			_questForThisNpc = null;
			_currentInteractedResident = null;
			customClosed?.Invoke();
		}

		public void NotifyShopClosedAfterPurchase(bool completedPurchase)
		{
			ResidentController resident = _lastShopResident;
			ResidentBillboardText billboard = _lastShopBillboard;
			if (billboard == null)
			{
				resident?.ServerEnableInteractRpc();
				_lastShopResident = null;
				_currentInteractedResident = null;
				return;
			}
			if (_employeeDialogueDb != null && _employeeDialogueDb.TryGet(_lastShopActorType, out var employee) && employee != null)
			{
				string randomFarewell = employee.GetRandomFarewell();
				resident?.RequestDialogueBillboardText(randomFarewell);
				resident?.PlaySFX(ResidentSounds.Farewell);
			}
			if (completedPurchase)
			{
				resident?.RequestPurchaseReaction();
			}
			Singleton<DelayHelper>.Instance.DelayAction(1.5f, () =>
			{
				resident?.RequestHideDialogueBillboard();
				if (_lastShopBillboard == billboard)
				{
					_lastShopBillboard = null;
				}
				if (_lastShopResident == resident)
				{
					_lastShopResident = null;
				}
				resident?.ServerEnableInteractRpc();
			});
			_currentInteractedResident = null;
		}

		private void HandlePurchase()
		{
			if (!TrySkipTyping())
			{
				_lastShopActorType = _actorType;
				_lastShopBillboard = _billboard;
				_lastShopResident = _currentInteractedResident;
				Singleton<GameUIManager>.Instance.ShowWholesaleUI(_actorType);
				Hide(restorePlayerInput: false, enableResidentInteract: false, clearShopActiveFlag: false);
			}
		}

		private void HandleQuestInfo()
		{
			if (!(_questForThisNpc == null) && !TrySkipTyping())
			{
				_mode = DialogueMode.QuestInfo;
				ApplyModeUI(showButtons: false);
				PlayLocalizedBillboardText(_questForThisNpc.InfoLocalizationKey, _questForThisNpc.InfoText);
			}
		}

		private void HandleJoke()
		{
			if (!TrySkipTyping() && !(_employee == null) && _employee.HasJokes)
			{
				_currentJoke = _employee.GetRandomJoke();
				_mode = DialogueMode.JokeQuestion;
				ApplyModeUI(showButtons: false);
				_currentInteractedResident?.PlaySFX(ResidentSounds.Joke);
				PlayBillboardText(_currentJoke.Question);
			}
		}

		private void HandleContinue()
		{
			if (!TrySkipTyping())
			{
				switch (_mode)
				{
				case DialogueMode.QuestInfo:
					_mode = DialogueMode.QuestAnswer;
					PlayLocalizedBillboardText(_questForThisNpc.AnswerLocalizationKey, _questForThisNpc.AnswerText);
					ApplyModeUI(showButtons: false);
					break;
				case DialogueMode.QuestAnswer:
					Hide();
					break;
				case DialogueMode.QuestEnd:
					OnQuestEndAction?.Invoke(_questForThisNpc.QuestId);
					UtilityMethods.Log("[DialoguePanel] Quest interaction ended. OnQuestEndAction invoked.");
					Hide();
					break;
				case DialogueMode.JokeQuestion:
					_mode = DialogueMode.JokeAnswer;
					ApplyModeUI(showButtons: false);
					PlayBillboardText(_currentJoke.Answer);
					break;
				case DialogueMode.JokeAnswer:
					_currentInteractedResident?.PlaySFX(ResidentSounds.Laugh);
					Hide();
					break;
				}
			}
		}

		private void HandleQuit()
		{
			if (!TrySkipTyping())
			{
				Hide();
			}
		}

		private void PlayBillboardText(string text)
		{
			if (!(_billboard == null) || !(_currentInteractedResident == null))
			{
				_isTyping = true;
				ApplyModeUI(showButtons: false);
				if (_currentInteractedResident != null)
				{
					_currentInteractedResident.RequestDialogueBillboardText(text);
				}
				else
				{
					_billboard.PlayTypewriter(text, OnTypingFinished);
				}
			}
		}

		private void PlayLocalizedBillboardText(string localizationKey, string fallback)
		{
			if (!(_billboard == null) || !(_currentInteractedResident == null))
			{
				_isTyping = true;
				ApplyModeUI(showButtons: false);
				if (_currentInteractedResident != null)
				{
					_currentInteractedResident.RequestLocalizedDialogueBillboardText("UiTable", localizationKey, fallback);
					return;
				}
				string message = LocalizationDatabase.GetString("UiTable", localizationKey, fallback);
				_billboard.PlayTypewriter(message, OnTypingFinished);
			}
		}

		private void OnTypingFinished()
		{
			_isTyping = false;
			if (_mode == DialogueMode.JokeAnswer)
			{
				_currentInteractedResident?.PlaySFX(ResidentSounds.Laugh);
				Singleton<DelayHelper>.Instance.DelayAction(1f, () =>
				{
					Hide();
				});
			}
			else
			{
				ApplyModeUI(showButtons: true);
				UtilityMethods.Log($"[DialoguePanel] Typing finished. Mode={_mode}. Showing buttons.");
			}
		}

		private bool TrySkipTyping()
		{
			if (_panelRoot == null || !_panelRoot.activeSelf)
			{
				return false;
			}
			if (!_isTyping)
			{
				return false;
			}
			_billboard?.CompleteInstant();
			if (_isTyping)
			{
				UtilityMethods.Log($"[DialoguePanel] Billboard callback missed after skip. Forcing finish. Mode={_mode}", LogType.Warning);
				OnTypingFinished();
			}
			return true;
		}

		private QuestDialogueSO ResolveActiveQuestForNpc(List<string> relatedQuestIds)
		{
			if (relatedQuestIds == null || relatedQuestIds.Count == 0)
			{
				return null;
			}
			QuestManager instance = QuestManager.Instance;
			if (instance == null)
			{
				return null;
			}
			for (int i = 0; i < relatedQuestIds.Count; i++)
			{
				string text = relatedQuestIds[i];
				if (!string.IsNullOrWhiteSpace(text) && instance.IsQuestOrGroupActive(text))
				{
					QuestDialogueSO questDialogueById = GetQuestDialogueById(text);
					if (questDialogueById != null)
					{
						return questDialogueById;
					}
				}
			}
			return null;
		}

		private QuestDialogueSO GetQuestDialogueById(string questId)
		{
			for (int i = 0; i < _questDialogues.Count; i++)
			{
				if (_questDialogues[i] != null && _questDialogues[i].QuestId == questId)
				{
					return _questDialogues[i];
				}
			}
			return null;
		}

		private void ApplyModeUI(bool showButtons)
		{
			HideCustomButtons();
			_buttonsRoot.SetActive(value: false);
			_purchaseButton.gameObject.SetActive(value: false);
			_questButton.gameObject.SetActive(value: false);
			_jokeButton.gameObject.SetActive(value: false);
			_continueButton.gameObject.SetActive(value: false);
			_quitButton.gameObject.SetActive(value: false);
			if (!showButtons)
			{
				return;
			}
			_buttonsRoot.SetActive(value: true);
			if (ApplyCustomButtons()) return;
			bool flag = _questForThisNpc != null;
			bool active = _employee != null && _employee.HasJokes;
			switch (_mode)
			{
			case DialogueMode.Menu:
				_purchaseButton.gameObject.SetActive(value: true);
				_jokeButton.gameObject.SetActive(active);
				_quitButton.gameObject.SetActive(value: true);
				_questButton.gameObject.SetActive(flag);
				if (flag)
				{
					_questButton.SetLocalizedText("UiTable", _questForThisNpc.OptionLocalizationKey, _questForThisNpc.OptionLabel);
				}
				break;
			case DialogueMode.QuestInfo:
				_continueButton.gameObject.SetActive(value: true);
				_continueButton.SetLocalizedText("UiTable", _questForThisNpc.ContinueLocalizationKey, _questForThisNpc.ContinueLabel);
				break;
			case DialogueMode.JokeQuestion:
			{
				_continueButton.gameObject.SetActive(value: true);
				string fallback = (string.IsNullOrWhiteSpace(_currentJoke.ContinueLabel) ? "..." : _currentJoke.ContinueLabel);
				_continueButton.SetLocalizedText("EmployeeDialogueTable", _currentJoke.ContinueLocalizationKey, fallback);
				break;
			}
			case DialogueMode.JokeAnswer:
				_buttonsRoot.SetActive(value: false);
				break;
			case DialogueMode.QuestAnswer:
				_mode = DialogueMode.QuestEnd;
				_continueButton.gameObject.SetActive(value: true);
				_continueButton.SetLocalizedText("UiTable", _questForThisNpc.EndTalkLocalizationKey, _questForThisNpc.EndTalkLabel);
				break;
			case DialogueMode.QuestEnd:
				break;
			}
		}

		private void SetVisible(bool visible)
		{
			_panelRoot.SetActive(visible);
			if (!visible && _buttonsRoot != null)
			{
				_buttonsRoot.SetActive(value: false);
			}
		}

		private void RegisterCancelEvent()
		{
			if (!_cancelRegistered)
			{
				InputHandler inputHandler = ((Singleton<InputManager>.Instance != null) ? Singleton<InputManager>.Instance.InputHandler : null);
				if (inputHandler == null)
				{
					UtilityMethods.Log("[DialoguePanel] Failed to register Cancel. InputHandler is null.", LogType.Warning);
					return;
				}
				inputHandler.Cancel += OnCancel;
				_cancelRegistered = true;
				UtilityMethods.Log("[DialoguePanel] Cancel registered.");
			}
		}

		private void UnregisterCancelEvent()
		{
			if (_cancelRegistered)
			{
				InputHandler inputHandler = ((Singleton<InputManager>.Instance != null) ? Singleton<InputManager>.Instance.InputHandler : null);
				if (inputHandler != null)
				{
					inputHandler.Cancel -= OnCancel;
				}
				_cancelRegistered = false;
				UtilityMethods.Log("[DialoguePanel] Cancel unregistered.");
			}
		}

		private void OnCancel(InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started && !(_panelRoot == null) && _panelRoot.activeSelf)
			{
				UtilityMethods.Log($"[DialoguePanel] Cancel pressed. Mode={_mode}, IsTyping={_isTyping}");
				if (!TrySkipTyping())
				{
					Hide();
				}
			}
		}
	}
}
