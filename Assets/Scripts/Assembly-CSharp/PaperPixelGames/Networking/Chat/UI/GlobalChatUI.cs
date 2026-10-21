using System.Collections.Generic;
using DG.Tweening;
using PaperPixelGames.Core.Settings;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PaperPixelGames.Networking.Chat.UI
{
	public sealed class GlobalChatUI : MonoBehaviour
	{
		[Header("References")]
		[SerializeField]
		private GameObject _fadeBackground;

		[SerializeField]
		private TMP_InputField _inputField;

		[SerializeField]
		private RectTransform _newContentRoot;

		[SerializeField]
		private RectTransform _oldContentRoot;

		[SerializeField]
		private ChatMessageRowUI _messageRowPrefab;

		[SerializeField]
		private RectTransform _contentsRoot;

		[SerializeField]
		private float _maxUpY;

		[SerializeField]
		private BaseButton _upArrow;

		[SerializeField]
		private BaseButton _downArrow;

		[SerializeField]
		private float _arrowMoveStep = 80f;

		[SerializeField]
		private float _arrowMoveDuration = 0.15f;

		[Header("Input")]
		[SerializeField]
		private InputManager _inputManager;

		[SerializeField]
		private bool _closeAfterSubmit = true;

		[SerializeField]
		private bool _clearInputAfterSubmit = true;

		[Header("Layout")]
		[SerializeField]
		private RectTransform _layoutRoot;

		[SerializeField]
		private CanvasGroup _oldContentCanvasGroup;

		[SerializeField]
		private LayoutElement _oldContentLayoutElement;

		[Header("Pooling")]
		[SerializeField]
		[Min(0f)]
		private int _prewarmRowCount = 12;

		[SerializeField]
		[Min(1f)]
		private int _maxVisibleRowCount = 20;

		[SerializeField]
		private RectTransform _poolRoot;

		[Header("Visual")]
		[SerializeField]
		private bool _startClosed = true;

		[Header("Events")]
		[SerializeField]
		private UnityEvent<bool> _chatFocusChanged;

		private readonly List<ChatMessageRowUI> _activeRows = new List<ChatMessageRowUI>();

		private readonly Queue<ChatMessageRowUI> _rowPool = new Queue<ChatMessageRowUI>();

		private bool _isOpen;

		private bool _chatEventsBound;

		private bool _inputEventsBound;

		private bool _layoutDirty;

		private int _lastChatInputFrame = -1;

		private bool _refocusInput;

		public bool IsOpen => _isOpen;

		public void Initialize()
		{
			if (_inputField != null)
			{
				_inputField.lineType = TMP_InputField.LineType.SingleLine;
				_inputField.richText = false;
				_inputField.restoreOriginalTextOnEscape = false;
			}
			CacheLayoutReferences();
			CachePoolRoot();
			PrewarmPool();
			if (_startClosed)
			{
				ApplyClosedInstant();
			}
			else
			{
				ApplyOpenInstant();
			}
			MarkLayoutDirty();
			BindChatEvents();
			BindInputEvents();
			BindSettingsEvents();
			RebuildVisibleHistory();
			_upArrow.Clicked += OnUpArrow;
			_downArrow.Clicked += OnDownArrow;
		}

		private void OnDisable()
		{
			Close();
		}

		private void OnDestroy()
		{
			Close();
			UnbindChatEvents();
			UnbindInputEvents();
			UnbindSettingsEvents();
			_upArrow.Clicked -= OnUpArrow;
			_downArrow.Clicked -= OnDownArrow;
		}

		private void LateUpdate()
		{
			if (_refocusInput && _isOpen)
			{
				_refocusInput = false;
				FocusInput();
			}
			if (_layoutDirty)
			{
				_layoutDirty = false;
				RebuildLayoutNow();
			}
		}

		private void OnUpArrow()
		{
			MoveContentY(_arrowMoveStep);
		}

		private void OnDownArrow()
		{
			MoveContentY(0f - _arrowMoveStep);
		}

		private void MoveContentY(float delta)
		{
			if (!(_contentsRoot == null))
			{
				float endValue = Mathf.Clamp(_contentsRoot.anchoredPosition.y + delta, 0f, _maxUpY);
				_contentsRoot.DOKill();
				_contentsRoot.DOAnchorPosY(endValue, _arrowMoveDuration).SetEase(Ease.OutCubic).SetUpdate(isIndependentUpdate: true);
			}
		}

		private void CacheLayoutReferences()
		{
			if (_oldContentRoot != null)
			{
				if (_oldContentCanvasGroup == null)
				{
					_oldContentCanvasGroup = _oldContentRoot.GetComponent<CanvasGroup>();
					if (_oldContentCanvasGroup == null)
					{
						_oldContentCanvasGroup = _oldContentRoot.gameObject.AddComponent<CanvasGroup>();
					}
				}
				if (_oldContentLayoutElement == null)
				{
					_oldContentLayoutElement = _oldContentRoot.GetComponent<LayoutElement>();
					if (_oldContentLayoutElement == null)
					{
						_oldContentLayoutElement = _oldContentRoot.gameObject.AddComponent<LayoutElement>();
					}
				}
			}
			if (_layoutRoot == null && _newContentRoot != null)
			{
				_layoutRoot = _newContentRoot.parent as RectTransform;
			}
		}

		private void CachePoolRoot()
		{
			if (!(_poolRoot != null))
			{
				GameObject gameObject = new GameObject("ChatMessageRowPool", typeof(RectTransform));
				_poolRoot = gameObject.GetComponent<RectTransform>();
				_poolRoot.SetParent(base.transform, worldPositionStays: false);
				_poolRoot.gameObject.SetActive(value: false);
			}
		}

		private void PrewarmPool()
		{
			if (!(_messageRowPrefab == null) && !(_poolRoot == null))
			{
				for (int i = 0; i < _prewarmRowCount; i++)
				{
					ChatMessageRowUI chatMessageRowUI = Object.Instantiate(_messageRowPrefab, _poolRoot);
					chatMessageRowUI.gameObject.SetActive(value: false);
					chatMessageRowUI.Release();
					_rowPool.Enqueue(chatMessageRowUI);
				}
			}
		}

		private void BindChatEvents()
		{
			if (!_chatEventsBound)
			{
				_chatEventsBound = true;
				if (Singleton<GlobalChatManager>.Instance != null)
				{
					Singleton<GlobalChatManager>.Instance.MessageReceived -= OnMessageReceived;
					Singleton<GlobalChatManager>.Instance.SessionCleared -= OnSessionCleared;
					Singleton<GlobalChatManager>.Instance.MessageReceived += OnMessageReceived;
					Singleton<GlobalChatManager>.Instance.SessionCleared += OnSessionCleared;
				}
			}
		}

		private void UnbindChatEvents()
		{
			if (_chatEventsBound)
			{
				_chatEventsBound = false;
				if (Singleton<GlobalChatManager>.Instance != null)
				{
					Singleton<GlobalChatManager>.Instance.MessageReceived -= OnMessageReceived;
					Singleton<GlobalChatManager>.Instance.SessionCleared -= OnSessionCleared;
				}
			}
		}

		private void BindInputEvents()
		{
			if (!_inputEventsBound)
			{
				_inputManager.InputHandler.OpenChat -= OnChatSubmitInput;
				_inputManager.InputHandler.OpenChat += OnChatSubmitInput;
				_inputManager.InputHandler.Submit += OnUISubmitInput;
				_inputManager.InputHandler.Cancel += OnCancelInput;
				if (_inputField != null)
				{
					_inputField.onSubmit.AddListener(OnFieldSubmit);
					_inputField.onEndEdit.AddListener(OnFieldEndEdit);
				}
				_inputEventsBound = true;
			}
		}

		private void UnbindInputEvents()
		{
			if (_inputEventsBound)
			{
				_inputEventsBound = false;
				_inputManager.InputHandler.OpenChat -= OnChatSubmitInput;
				_inputManager.InputHandler.Submit -= OnUISubmitInput;
				_inputManager.InputHandler.Cancel -= OnCancelInput;
				if (_inputField != null)
				{
					_inputField.onSubmit.RemoveListener(OnFieldSubmit);
					_inputField.onEndEdit.RemoveListener(OnFieldEndEdit);
				}
			}
		}

		private void OnChatSubmitInput(InputActionPhase phase)
		{
			if (phase == InputActionPhase.Performed && base.isActiveAndEnabled && _lastChatInputFrame != Time.frameCount)
			{
				if (!_isOpen)
				{
					Open();
				}
				else
				{
					SubmitInput();
				}
			}
		}

		private void OnUISubmitInput(InputActionPhase phase)
		{
			if (phase == InputActionPhase.Performed && _isOpen)
			{
				SubmitInput();
			}
		}

		private void OnFieldSubmit(string _)
		{
			SubmitInput();
		}

		private void OnFieldEndEdit(string _)
		{
			if (_isOpen)
			{
				if (_inputField.wasCanceled)
				{
					Close();
				}
				else
				{
					_refocusInput = true;
				}
			}
		}

		private void OnCancelInput(InputActionPhase phase)
		{
			if (phase == InputActionPhase.Performed && _isOpen)
			{
				Close();
			}
		}

		public void Open()
		{
			if (base.isActiveAndEnabled)
			{
				if (_isOpen)
				{
					ApplyChatFocusVisibility(active: true);
					FocusInput();
					return;
				}
				_isOpen = true;
				_lastChatInputFrame = Time.frameCount;
				ApplyChatFocusVisibility(active: true);
				FocusInput();
				_chatFocusChanged?.Invoke(arg0: true);
				EventBus<GlobalChatEvent>.Raise(new GlobalChatEvent
				{
					Status = true
				});
				UtilityMethods.Log("[GlobalChatUI] Opened.");
			}
		}

		public void Close()
		{
			if (_isOpen)
			{
				_isOpen = false;
				_refocusInput = false;
				_lastChatInputFrame = Time.frameCount;
				ClearInputFocus();
				ApplyChatFocusVisibility(active: false);
				_chatFocusChanged?.Invoke(arg0: false);
				EventBus<GlobalChatEvent>.Raise(new GlobalChatEvent
				{
					Status = false
				});
				UtilityMethods.Log("[GlobalChatUI] Closed.");
			}
		}

		public void SubmitInput()
		{
			if (!_isOpen || !base.isActiveAndEnabled || _inputField == null || _lastChatInputFrame == Time.frameCount)
			{
				return;
			}
			_lastChatInputFrame = Time.frameCount;
			if (string.IsNullOrWhiteSpace(_inputField.text))
			{
				_inputField.SetTextWithoutNotify(string.Empty);
				Close();
				return;
			}
			if (Singleton<GlobalChatManager>.Instance == null)
			{
				UtilityMethods.Log("[GlobalChatUI] Send failed. GlobalChatManager is missing.", LogType.Warning);
				_refocusInput = true;
				return;
			}
			string text = _inputField.text;
			if (!Singleton<GlobalChatManager>.Instance.TrySendGlobalMessage(text, out var error))
			{
				if (!string.IsNullOrWhiteSpace(error))
				{
					UtilityMethods.Log("[GlobalChatUI] Send failed. " + error);
				}
				_refocusInput = true;
				return;
			}
			if (_clearInputAfterSubmit)
			{
				_inputField.SetTextWithoutNotify(string.Empty);
			}
			if (_closeAfterSubmit)
			{
				Close();
			}
			else
			{
				_refocusInput = true;
			}
		}

		private void FocusInput()
		{
			if (!(_inputField == null))
			{
				if (!_inputField.gameObject.activeSelf)
				{
					_inputField.gameObject.SetActive(value: true);
				}
				_inputField.ActivateInputField();
				_inputField.Select();
				EventSystem.current?.SetSelectedGameObject(_inputField.gameObject);
			}
		}

		private void ClearInputFocus()
		{
			if (_inputField == null)
			{
				return;
			}
			_inputField.DeactivateInputField();
			if (!(EventSystem.current == null))
			{
				GameObject currentSelectedGameObject = EventSystem.current.currentSelectedGameObject;
				if (currentSelectedGameObject != null && currentSelectedGameObject == _inputField.gameObject)
				{
					EventSystem.current.SetSelectedGameObject(null);
				}
			}
		}

		private void ApplyChatFocusVisibility(bool active)
		{
			if (_inputField != null && _inputField.gameObject.activeSelf != active)
			{
				_inputField.gameObject.SetActive(active);
			}
			if (_fadeBackground != null && _fadeBackground.activeSelf != active)
			{
				_fadeBackground.SetActive(active);
			}
			ApplyOldContentVisibility(active);
			MarkLayoutDirty();
		}

		private void ApplyOldContentVisibility(bool active)
		{
			if (_oldContentRoot == null)
			{
				return;
			}
			if (!_oldContentRoot.gameObject.activeSelf)
			{
				_oldContentRoot.gameObject.SetActive(value: true);
			}
			if (_oldContentCanvasGroup != null)
			{
				float num = (active ? 1f : 0f);
				if (!Mathf.Approximately(_oldContentCanvasGroup.alpha, num))
				{
					_oldContentCanvasGroup.alpha = num;
				}
				_oldContentCanvasGroup.interactable = active;
				_oldContentCanvasGroup.blocksRaycasts = active;
			}
			if (_oldContentLayoutElement != null)
			{
				_oldContentLayoutElement.ignoreLayout = !active;
			}
		}

		private void MarkLayoutDirty()
		{
			_layoutDirty = true;
		}

		private void RebuildLayoutNow()
		{
			if (_layoutRoot != null)
			{
				LayoutRebuilder.ForceRebuildLayoutImmediate(_layoutRoot);
				return;
			}
			if (_newContentRoot != null)
			{
				LayoutRebuilder.ForceRebuildLayoutImmediate(_newContentRoot);
			}
			if (_oldContentRoot != null)
			{
				LayoutRebuilder.ForceRebuildLayoutImmediate(_oldContentRoot);
			}
		}

		private void OnMessageReceived(ChatMessageData data)
		{
			if (ShouldDisplay(data))
			{
				SpawnRow(data);
			}
		}

		private void BindSettingsEvents()
		{
			RuntimeSettings.ChatLogsChanged -= OnChatLogSettingChanged;
			RuntimeSettings.RestaurantChatLogsChanged -= OnChatLogSettingChanged;
			RuntimeSettings.ChatLogsChanged += OnChatLogSettingChanged;
			RuntimeSettings.RestaurantChatLogsChanged += OnChatLogSettingChanged;
		}

		private void UnbindSettingsEvents()
		{
			RuntimeSettings.ChatLogsChanged -= OnChatLogSettingChanged;
			RuntimeSettings.RestaurantChatLogsChanged -= OnChatLogSettingChanged;
		}

		private void OnChatLogSettingChanged(bool _)
		{
			RebuildVisibleHistory();
		}

		private void RebuildVisibleHistory()
		{
			ClearRows();
			if (Singleton<GlobalChatManager>.Instance == null)
			{
				return;
			}
			IReadOnlyList<ChatMessageData> history = Singleton<GlobalChatManager>.Instance.History;
			for (int i = 0; i < history.Count; i++)
			{
				ChatMessageData data = history[i];
				if (ShouldDisplay(data))
				{
					SpawnRow(data, useOldContent: true);
				}
			}
		}

		private static bool ShouldDisplay(ChatMessageData data)
		{
			if (data == null || !RuntimeSettings.ChatLogsEnabled)
			{
				return false;
			}
			if (data.Type == ChatMessageType.Restaurant)
			{
				return RuntimeSettings.RestaurantChatLogsEnabled;
			}
			return true;
		}

		private void OnSessionCleared()
		{
			Close();
			ClearRows();
			if (_inputField != null)
			{
				_inputField.SetTextWithoutNotify(string.Empty);
			}
			MarkLayoutDirty();
		}

		private void SpawnRow(ChatMessageData data, bool useOldContent = false)
		{
			if (_messageRowPrefab == null || _newContentRoot == null)
			{
				UtilityMethods.Log("[GlobalChatUI] Cannot spawn message row. Missing prefab or content root.", LogType.Warning);
				return;
			}
			ChatMessageRowUI row = GetRow();
			RectTransform parent = ((useOldContent && _oldContentRoot != null) ? _oldContentRoot : _newContentRoot);
			row.transform.SetParent(parent, worldPositionStays: false);
			row.transform.SetAsLastSibling();
			row.gameObject.SetActive(value: true);
			row.PrepareForReuse();
			row.Set(data, _oldContentRoot, MarkLayoutDirty);
			_activeRows.Add(row);
			EnforceMaxVisibleRows();
			MarkLayoutDirty();
		}

		private ChatMessageRowUI GetRow()
		{
			if (_rowPool.Count > 0)
			{
				return _rowPool.Dequeue();
			}
			return Object.Instantiate(_messageRowPrefab, _poolRoot);
		}

		private void EnforceMaxVisibleRows()
		{
			if (_maxVisibleRowCount > 0)
			{
				while (_activeRows.Count > _maxVisibleRowCount)
				{
					ChatMessageRowUI row = _activeRows[0];
					_activeRows.RemoveAt(0);
					ReleaseRowToPool(row);
				}
			}
		}

		private void ClearRows()
		{
			for (int num = _activeRows.Count - 1; num >= 0; num--)
			{
				ReleaseRowToPool(_activeRows[num]);
			}
			_activeRows.Clear();
			MarkLayoutDirty();
		}

		private void ReleaseRowToPool(ChatMessageRowUI row)
		{
			if (!(row == null))
			{
				row.Release();
				if (_poolRoot != null)
				{
					row.transform.SetParent(_poolRoot, worldPositionStays: false);
				}
				row.gameObject.SetActive(value: false);
				_rowPool.Enqueue(row);
			}
		}

		private void ApplyOpenInstant()
		{
			_isOpen = true;
			ApplyChatFocusVisibility(active: true);
		}

		private void ApplyClosedInstant()
		{
			_isOpen = false;
			ApplyChatFocusVisibility(active: false);
		}

		public void ClearNewContext()
		{
			if (_newContentRoot == null || _oldContentRoot == null)
			{
				return;
			}
			int num = 0;
			List<Transform> list = new List<Transform>();
			for (int i = 0; i < _newContentRoot.childCount; i++)
			{
				Transform child = _newContentRoot.GetChild(i);
				if (!(child == null) && child.TryGetComponent<ChatMessageRowUI>(out var _))
				{
					list.Add(child);
				}
			}
			for (int j = 0; j < list.Count; j++)
			{
				Transform transform = list[j];
				if (!(transform == null))
				{
					transform.DOKill();
					transform.SetParent(_oldContentRoot, worldPositionStays: false);
					transform.SetAsLastSibling();
					num++;
				}
			}
			MarkLayoutDirty();
			if (base.isActiveAndEnabled)
			{
				RebuildLayoutNow();
				Canvas.ForceUpdateCanvases();
			}
			UtilityMethods.Log($"[GlobalChatUI] Forced {num} new context rows into old context.");
		}
	}
}
