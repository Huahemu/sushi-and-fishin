using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

public class HintItem : MonoBehaviour
{
	[SerializeField]
	private TextMeshProUGUI _hintIconText;

	[SerializeField]
	private LocalizeStringEvent _hintInfoText;

	[SerializeField]
	private ContentSizeFitter _sizeFitter;

	private InputAction _action;

	private int _bindingIndex;

	private static readonly HashSet<string> KnownKeyboardMouseSpriteNames = new HashSet<string>
	{
		"key1_a", "key1_b", "key1_c", "key1_d", "key1_e", "key1_f", "key1_g", "key1_h", "key1_j", "key1_k",
		"key1_m", "key1_o", "key1_p", "key1_q", "key1_r", "key1_s", "key1_v", "key1_w", "key1_x", "key1_y",
		"key1_z", "key1_1", "key1_2", "key1_3", "key1_4", "key1_5", "key1_6", "key1_7", "key1_8", "key1_9",
		"key1_ctrl", "key1_down", "key1_enter", "key1_enter2", "key1_escape", "key1_left", "key1_leftbutton", "key1_mousemove", "key1_right", "key1_rightbutton",
		"key1_scroll", "key1_shift", "key1_space", "key1_tab", "key1_up"
	};

	private void Awake()
	{
		ConfigureIconText();
	}

	public void Set(InputAction action, int bindingIndex, string locKey, string tableKey = "GameStateTable")
	{
		_action = action;
		_bindingIndex = bindingIndex;
		UpdateSprite();
		_hintInfoText.StringReference.TableReference = tableKey;
		_hintInfoText.StringReference.TableEntryReference = locKey;
		_hintInfoText.RefreshString();
	}

	public void RefreshIcon()
	{
		UpdateSprite();
	}

	public static bool TryGetBindingSpriteName(InputAction action, int bindingIndex, out string spriteName)
	{
		spriteName = null;
		if (action != null && bindingIndex >= 0 && bindingIndex < action.bindings.Count && TryGetAtlasSpriteName(action.bindings[bindingIndex].effectivePath, out spriteName))
		{
			return KnownKeyboardMouseSpriteNames.Contains(spriteName);
		}
		return false;
	}

	private void UpdateSprite()
	{
		if (!(_hintIconText == null) && _action != null && _bindingIndex >= 0 && _bindingIndex < _action.bindings.Count)
		{
			ConfigureIconText();
			InputBinding inputBinding = _action.bindings[_bindingIndex];
			string path = (string.IsNullOrEmpty(inputBinding.effectivePath) ? inputBinding.path : inputBinding.effectivePath);
			if (TryGetAtlasSpriteName(path, out var spriteName) && KnownKeyboardMouseSpriteNames.Contains(spriteName))
			{
				_hintIconText.text = "<sprite name=" + spriteName + ">";
			}
			else
			{
				_hintIconText.text = GetFallbackText(path);
			}
		}
	}

	private void ConfigureIconText()
	{
		if (!(_hintIconText == null))
		{
			_hintIconText.textWrappingMode = TextWrappingModes.NoWrap;
			_hintIconText.overflowMode = TextOverflowModes.Overflow;
		}
	}

	private static bool TryGetAtlasSpriteName(string path, out string spriteName)
	{
		spriteName = null;
		if (!TryGetControlName(path, out var control))
		{
			return false;
		}
		string text = NormalizeControlName(control);
		if (string.IsNullOrEmpty(text))
		{
			return false;
		}
		spriteName = "key1_" + text;
		return true;
	}

	private static bool TryGetControlName(string path, out string control)
	{
		control = null;
		if (string.IsNullOrEmpty(path))
		{
			return false;
		}
		int num = path.LastIndexOf('/');
		if (num < 0 || num == path.Length - 1)
		{
			return false;
		}
		int num2 = num + 1;
		control = path.Substring(num2, path.Length - num2);
		return !string.IsNullOrEmpty(control);
	}

	private static string NormalizeControlName(string control)
	{
		string text = control.ToLowerInvariant();
		switch (text)
		{
		case "leftshift":
		case "rightshift":
		case "shift":
			return "shift";
		case "leftctrl":
		case "rightctrl":
		case "ctrl":
			return "ctrl";
		case "leftcontrol":
		case "rightcontrol":
		case "control":
			return "ctrl";
		case "leftbutton":
			return "leftbutton";
		case "rightbutton":
			return "rightbutton";
		case "middlebutton":
			return "scroll";
		case "scroll":
		case "scrollwheel":
			return "scroll";
		case "delta":
			return "mousemove";
		case "leftarrow":
			return "left";
		case "rightarrow":
			return "right";
		case "uparrow":
			return "up";
		case "downarrow":
			return "down";
		case "escape":
			return "escape";
		case "space":
			return "space";
		case "tab":
			return "tab";
		case "enter":
		case "return":
			return "enter";
		default:
			if (text.Length == 1 && char.IsLetterOrDigit(text[0]))
			{
				return text;
			}
			return text.Replace(" ", "").Replace("/", "");
		}
	}

	private static string GetFallbackText(string path)
	{
		if (!TryGetControlName(path, out var control))
		{
			return "?";
		}
		switch (control.ToLowerInvariant())
		{
		case "leftshift":
		case "rightshift":
		case "shift":
			return "Shift";
		case "leftctrl":
		case "rightctrl":
		case "ctrl":
			return "Ctrl";
		case "leftcontrol":
		case "rightcontrol":
		case "control":
			return "Ctrl";
		case "leftbutton":
			return "LMB";
		case "rightbutton":
			return "RMB";
		case "middlebutton":
			return "MMB";
		case "scroll":
		case "scrollwheel":
			return "Scroll";
		case "leftarrow":
			return "Left";
		case "rightarrow":
			return "Right";
		case "uparrow":
			return "Up";
		case "downarrow":
			return "Down";
		case "escape":
			return "Esc";
		case "space":
			return "Space";
		case "tab":
			return "Tab";
		case "enter":
		case "return":
			return "Enter";
		case "leftshoulder":
			return "LB";
		case "rightshoulder":
			return "RB";
		case "lefttrigger":
			return "LT";
		case "righttrigger":
			return "RT";
		case "buttonsouth":
			return "A";
		case "buttoneast":
			return "B";
		case "buttonwest":
			return "X";
		case "buttonnorth":
			return "Y";
		default:
			if (control.Length == 1)
			{
				return control.ToUpperInvariant();
			}
			return "?";
		}
	}
}
