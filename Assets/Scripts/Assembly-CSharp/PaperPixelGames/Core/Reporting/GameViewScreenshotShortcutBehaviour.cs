using PaperPixelGames.Framework.Patterns;
using UnityEngine;

namespace PaperPixelGames.Core.Reporting
{
	public sealed class GameViewScreenshotShortcutBehaviour : MonoBehaviour
	{
		private GUIStyle _panelStyle;

		private GUIStyle _labelStyle;

		private void Update()
		{
			if (Singleton<GameController>.TryGetInstance(out var instance) && instance.RuntimeDebugToolsEnabled)
			{
				if (GameViewScreenshotTool.ShortcutInfoShortcutEnabled && Input.GetKeyDown(KeyCode.F8))
				{
					GameViewScreenshotTool.ToggleShortcutInfo();
				}
				if (GameViewScreenshotTool.CaptureShortcutEnabled && Input.GetKeyDown(KeyCode.F9))
				{
					GameViewScreenshotTool.CaptureScreenshot();
				}
				if (GameViewScreenshotTool.ToggleUiShortcutEnabled && Input.GetKeyDown(KeyCode.F10))
				{
					GameViewScreenshotTool.ToggleUi();
				}
			}
		}

		private void OnGUI()
		{
			if (Singleton<GameController>.TryGetInstance(out var instance) && instance.RuntimeDebugToolsEnabled && GameViewScreenshotTool.ShortcutInfoVisible)
			{
				EnsureStyles();
				float num = 50f;
				if (GameViewScreenshotTool.ShortcutInfoShortcutEnabled)
				{
					num += 22f;
				}
				if (GameViewScreenshotTool.CaptureShortcutEnabled)
				{
					num += 22f;
				}
				if (GameViewScreenshotTool.ToggleUiShortcutEnabled)
				{
					num += 22f;
				}
				Rect position = new Rect(16f, 16f, 320f, num);
				GUI.Box(position, GUIContent.none, _panelStyle);
				GUILayout.BeginArea(new Rect(position.x + 14f, position.y + 12f, position.width - 28f, position.height - 24f));
				GUILayout.Label("<b>Screenshot Shortcuts</b>", _labelStyle);
				GUILayout.Space(6f);
				if (GameViewScreenshotTool.ShortcutInfoShortcutEnabled)
				{
					GUILayout.Label("F8  - Toggle shortcut info", _labelStyle);
				}
				if (GameViewScreenshotTool.CaptureShortcutEnabled)
				{
					GUILayout.Label("F9  - Capture screenshot", _labelStyle);
				}
				if (GameViewScreenshotTool.ToggleUiShortcutEnabled)
				{
					GUILayout.Label("F10 - Toggle UI", _labelStyle);
				}
				GUILayout.EndArea();
			}
		}

		private void EnsureStyles()
		{
			if (_panelStyle == null)
			{
				_panelStyle = new GUIStyle(GUI.skin.box)
				{
					padding = new RectOffset(12, 12, 10, 10),
					normal = 
					{
						textColor = Color.white
					}
				};
			}
			if (_labelStyle == null)
			{
				_labelStyle = new GUIStyle(GUI.skin.label)
				{
					fontSize = 14,
					richText = true,
					normal = 
					{
						textColor = Color.white
					}
				};
			}
		}
	}
}
