using System;
using System.IO;
using PaperPixelGames.DevConsole;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using UnityEngine;

namespace PaperPixelGames.Core.Reporting
{
	public static class GameViewScreenshotTool
	{
		public const KeyCode ShortcutInfoKey = KeyCode.F8;

		public const KeyCode CaptureKey = KeyCode.F9;

		public const KeyCode ToggleUiKey = KeyCode.F10;

		public static readonly bool ShortcutInfoShortcutEnabled = false;

		public static readonly bool CaptureShortcutEnabled = true;

		public static readonly bool ToggleUiShortcutEnabled = true;

		private const string ScreenshotFolderName = "SushiSS";

		private const string ScreenshotFilePrefix = "SushiSS";

		private const int MaxIndexedFileAttempts = 10000;

		private static readonly SetCommand SetCommand = new SetCommand();

		private static bool _uiVisible = true;

		private static bool _shortcutInfoVisible;

		private static GameViewScreenshotShortcutBehaviour _shortcutBehaviour;

		public static bool ShortcutInfoVisible => _shortcutInfoVisible;

		public static bool UiVisible => _uiVisible;

		public static bool AnyShortcutEnabled => false;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void BootstrapRuntimeShortcutBehaviour()
		{
			EnsureShortcutBehaviour();
		}

		public static void EnsureShortcutBehaviour()
		{
			if (AnyShortcutEnabled && !(_shortcutBehaviour != null))
			{
				GameViewScreenshotShortcutBehaviour gameViewScreenshotShortcutBehaviour = UnityEngine.Object.FindFirstObjectByType<GameViewScreenshotShortcutBehaviour>();
				if (gameViewScreenshotShortcutBehaviour != null)
				{
					_shortcutBehaviour = gameViewScreenshotShortcutBehaviour;
					return;
				}
				GameObject gameObject = new GameObject("[GameViewScreenshotShortcuts]");
				UnityEngine.Object.DontDestroyOnLoad(gameObject);
				_shortcutBehaviour = gameObject.AddComponent<GameViewScreenshotShortcutBehaviour>();
			}
		}

		public static void ToggleShortcutInfo()
		{
			if (AreRuntimeDebugToolsEnabled())
			{
				_shortcutInfoVisible = !_shortcutInfoVisible;
			}
		}

		public static void ToggleUi()
		{
			if (AreRuntimeDebugToolsEnabled())
			{
				SetUiVisible(!_uiVisible);
			}
		}

		public static void SetUiVisible(bool visible)
		{
			if (!AreRuntimeDebugToolsEnabled())
			{
				return;
			}
			string[] args = new string[3]
			{
				"set",
				"ui",
				visible ? "true" : "false"
			};
			SetCommand.Execute(null, args, (string message) =>
			{
				if (!string.IsNullOrWhiteSpace(message))
				{
					UtilityMethods.Log("[GameViewScreenshot] " + message);
				}
			});
			_uiVisible = visible;
		}

		public static string CaptureScreenshot()
		{
			if (!AreRuntimeDebugToolsEnabled())
			{
				return string.Empty;
			}
			string nextScreenshotPath = GetNextScreenshotPath();
			Directory.CreateDirectory(Path.GetDirectoryName(nextScreenshotPath));
			ScreenCapture.CaptureScreenshot(nextScreenshotPath);
			UtilityMethods.Log("[GameViewScreenshot] Screenshot requested: " + nextScreenshotPath);
			return nextScreenshotPath;
		}

		private static bool AreRuntimeDebugToolsEnabled()
		{
			if (Singleton<GameController>.TryGetInstance(out var instance))
			{
				return instance.RuntimeDebugToolsEnabled;
			}
			return false;
		}

		public static string GetScreenshotDirectory()
		{
			string text = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
			if (string.IsNullOrWhiteSpace(text))
			{
				text = Application.persistentDataPath;
			}
			return Path.Combine(text, "SushiSS");
		}

		private static string GetNextScreenshotPath()
		{
			string screenshotDirectory = GetScreenshotDirectory();
			string text = DateTime.Now.ToString("yyyyMMdd_HHmmss");
			string text2 = Path.Combine(screenshotDirectory, "SushiSS_" + text + ".png");
			if (!File.Exists(text2))
			{
				return text2;
			}
			for (int i = 1; i < 10000; i++)
			{
				string text3 = Path.Combine(screenshotDirectory, string.Format("{0}_{1}_{2:000}.png", "SushiSS", text, i));
				if (!File.Exists(text3))
				{
					return text3;
				}
			}
			return Path.Combine(screenshotDirectory, string.Format("{0}_{1}_{2:N}.png", "SushiSS", text, Guid.NewGuid()));
		}
	}
}
