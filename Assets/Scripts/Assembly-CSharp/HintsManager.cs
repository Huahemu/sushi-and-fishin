using System.Collections.Generic;
using Lean.Pool;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

public class HintsManager : Singleton<HintsManager>
{
	[SerializeField]
	private Canvas _hintCanvas;

	[SerializeField]
	private Transform _hintArea;

	[SerializeField]
	private HintItem _hintPrefab;

	private Dictionary<string, HintItem> _hints = new Dictionary<string, HintItem>();

	private readonly HashSet<string> _permanentHintBindingIds = new HashSet<string>();

	private bool _hintsVisible;

	private bool _visibilitySuppressed;

	private PlayerInput _playerInput;

	private HintItem _exclusiveHint;

	private object _exclusiveHintOwner;

	public void Initialize()
	{
		InputUser.onChange -= OnInputUserChange;
		InputUser.onChange += OnInputUserChange;
	}

	public void CachePlayerInput(PlayerInput input)
	{
		_playerInput = input;
	}

	private void OnInputUserChange(InputUser user, InputUserChange change, InputDevice device)
	{
		if (change == InputUserChange.ControlSchemeChanged)
		{
			RefreshAllHints();
		}
	}

	public void HintStatus(bool status)
	{
		_hintsVisible = status;
		ApplyVisibility();
	}

	public void SetVisibilitySuppressed(bool suppressed)
	{
		_visibilitySuppressed = suppressed;
		ApplyVisibility();
	}

	private void ApplyVisibility()
	{
		if (_hintCanvas != null)
		{
			_hintCanvas.enabled = (_hintsVisible || _exclusiveHint != null) && !_visibilitySuppressed;
		}
	}

	public void RefreshAllHints()
	{
		foreach (HintItem value in _hints.Values)
		{
			value.RefreshIcon();
		}
		_exclusiveHint?.RefreshIcon();
	}

	public bool ShowExclusiveHint(object owner, string actionName, string locKey, string tableKey = "GameStateTable")
	{
		if (owner == null)
		{
			return false;
		}
		if (_exclusiveHintOwner != null && _exclusiveHintOwner != owner)
		{
			return false;
		}
		if (!TryResolveHintBinding(actionName, out var action, out var index, out var _))
		{
			return false;
		}
		if (_exclusiveHint == null)
		{
			_exclusiveHintOwner = owner;
			SetRegisteredHintsActive(active: false);
			_exclusiveHint = LeanPool.Spawn(_hintPrefab, _hintArea);
		}
		_exclusiveHint.Set(action, index, locKey, tableKey);
		_exclusiveHint.RefreshIcon();
		ApplyVisibility();
		return true;
	}

	public void HideExclusiveHint(object owner)
	{
		if (_exclusiveHintOwner != null && _exclusiveHintOwner == owner)
		{
			if (_exclusiveHint != null)
			{
				LeanPool.Despawn(_exclusiveHint);
			}
			_exclusiveHint = null;
			_exclusiveHintOwner = null;
			SetRegisteredHintsActive(active: true);
			HintStatus(_hints.Count > 0);
		}
	}

	public void ClearAllHints()
	{
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, HintItem> hint in _hints)
		{
			if (!_permanentHintBindingIds.Contains(hint.Key))
			{
				LeanPool.Despawn(hint.Value);
				list.Add(hint.Key);
			}
		}
		for (int i = 0; i < list.Count; i++)
		{
			_hints.Remove(list[i]);
		}
		HintStatus(_hints.Count > 0);
	}

	public void ResetSession()
	{
		foreach (HintItem value in _hints.Values)
		{
			LeanPool.Despawn(value);
		}
		if (_exclusiveHint != null)
		{
			LeanPool.Despawn(_exclusiveHint);
		}
		_hints.Clear();
		_permanentHintBindingIds.Clear();
		_exclusiveHint = null;
		_exclusiveHintOwner = null;
		_playerInput = null;
		_visibilitySuppressed = false;
		HintStatus(status: false);
	}

	public void AddHint(string actionName, string locKey, string tableKey = "GameStateTable")
	{
		if (TryResolveHintBinding(actionName, out var action, out var index, out var bindingId) && !_permanentHintBindingIds.Contains(bindingId))
		{
			if (_hints.TryGetValue(bindingId, out var value))
			{
				value.Set(action, index, locKey, tableKey);
				value.RefreshIcon();
				ApplyRegisteredHintVisibility(value);
				HintStatus(status: true);
			}
			else
			{
				HintItem hintItem = LeanPool.Spawn(_hintPrefab, _hintArea);
				_hints.Add(bindingId, hintItem);
				hintItem.Set(action, index, locKey, tableKey);
				ApplyRegisteredHintVisibility(hintItem);
				HintStatus(status: true);
			}
		}
	}

	public void AddPermanentHint(string actionName, string locKey, string tableKey = "GameStateTable")
	{
		if (TryResolveHintBinding(actionName, out var action, out var index, out var bindingId))
		{
			if (_hints.TryGetValue(bindingId, out var value))
			{
				value.Set(action, index, locKey, tableKey);
				value.RefreshIcon();
				ApplyRegisteredHintVisibility(value);
			}
			else
			{
				HintItem hintItem = LeanPool.Spawn(_hintPrefab, _hintArea);
				_hints.Add(bindingId, hintItem);
				hintItem.Set(action, index, locKey, tableKey);
				ApplyRegisteredHintVisibility(hintItem);
			}
			_permanentHintBindingIds.Add(bindingId);
			HintStatus(status: true);
		}
	}

	public void RemoveHint(string actionName)
	{
		if (TryResolveAction(actionName, out var action))
		{
			RemoveAllActionHints(action);
			HintStatus(_hints.Count > 0);
		}
	}

	public void RemovePermanentHint(string actionName)
	{
		if (TryResolveHintBinding(actionName, out var _, out var _, out var bindingId))
		{
			_permanentHintBindingIds.Remove(bindingId);
			if (_hints.TryGetValue(bindingId, out var value))
			{
				LeanPool.Despawn(value);
				_hints.Remove(bindingId);
			}
			HintStatus(_hints.Count > 0);
		}
	}

	private bool TryResolveHintBinding(string actionName, out InputAction action, out int index, out string bindingId)
	{
		action = null;
		index = 0;
		bindingId = null;
		if (!TryResolveAction(actionName, out action))
		{
			return false;
		}
		return TryResolveHintBinding(action, out index, out bindingId);
	}

	private bool TryResolveAction(string actionName, out InputAction action)
	{
		action = null;
		if (_playerInput == null)
		{
			return false;
		}
		action = _playerInput.actions[actionName];
		return action != null;
	}

	private bool TryResolveHintBinding(InputAction action, out int index, out string bindingId)
	{
		index = 0;
		bindingId = null;
		string currentControlScheme = _playerInput.currentControlScheme;
		if (!TryGetBindingIndexForControlScheme(action, currentControlScheme, out index))
		{
			return false;
		}
		bindingId = action.bindings[index].id.ToString();
		return true;
	}

	private bool TryGetBindingIndexForControlScheme(InputAction action, string scheme, out int index)
	{
		index = 0;
		if (action.bindings.Count == 0)
		{
			return false;
		}
		if (!string.IsNullOrEmpty(scheme))
		{
			for (int i = 0; i < action.bindings.Count; i++)
			{
				InputBinding inputBinding = action.bindings[i];
				if (!string.IsNullOrEmpty(inputBinding.groups) && inputBinding.groups.Contains(scheme))
				{
					index = i;
					return true;
				}
			}
		}
		return true;
	}

	private void RemoveAllActionHints(InputAction action)
	{
		for (int i = 0; i < action.bindings.Count; i++)
		{
			string text = action.bindings[i].id.ToString();
			if (!_permanentHintBindingIds.Contains(text) && _hints.TryGetValue(text, out var value))
			{
				LeanPool.Despawn(value);
				_hints.Remove(text);
			}
		}
	}

	private void SetRegisteredHintsActive(bool active)
	{
		foreach (HintItem value in _hints.Values)
		{
			if (value != null)
			{
				value.gameObject.SetActive(active);
			}
		}
	}

	private void ApplyRegisteredHintVisibility(HintItem hint)
	{
		if (hint != null)
		{
			hint.gameObject.SetActive(_exclusiveHint == null);
		}
	}

	public void SetupHint(HintItem hint, string actionName, string localizationKey, string tableKey)
	{
		if (TryResolveHintBinding(actionName, out var action, out var index, out var _))
		{
			hint.Set(action, index, localizationKey, tableKey);
		}
	}
}
