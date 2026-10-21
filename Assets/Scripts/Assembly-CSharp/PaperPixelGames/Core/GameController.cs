using System;
using DG.Tweening;
using Michsky.LSS;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Core.SteamAchievements;
using PaperPixelGames.Framework.Analytics;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.States;
using PaperPixelGames.Framework.States.MainMenu;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Networking;
using PaperPixelGames.Networking.Chat;
using UnityEngine;

namespace PaperPixelGames.Core
{
	[DefaultExecutionOrder(10001)]
	public class GameController : Singleton<GameController>
	{
		public const bool RuntimeDebugToolsAvailable = false;

		public int Version = 1;

		public bool IsApplicationQuitting;

		private bool _runtimeDebugToolsEnabled;

		[SerializeField]
		private LSS_LoadingScreen _mainLoading;

		[Header("Exit Feedback")]
		[SerializeField]
		private bool _openExitFeedbackForm = true;

		[SerializeField]
		private string _exitFeedbackFormUrl;

		private float _fpsElapsedSeconds;

		private int _fpsFrameCount;

		private bool _exitFeedbackFormOpened;

		public bool RuntimeDebugToolsEnabled => false;

		public void SetRuntimeDebugToolsEnabled(bool enabled)
		{
			bool flag = false;
			if (_runtimeDebugToolsEnabled != flag)
			{
				_runtimeDebugToolsEnabled = flag;
				UtilityMethods.Log($"[GameController] Runtime debug tools enabled: {_runtimeDebugToolsEnabled}");
			}
		}

		protected override void Awake()
		{
			UtilityMethods.Log("[GameController] Awake");
			_runtimeDebugToolsEnabled = false;
			DOTween.SetTweensCapacity(1000, 200);
			base.Awake();
		}

		private void Start()
		{
			UtilityMethods.Log("[GameController] Start");
			SteamAchievementService.Initialize();
			Singleton<AnalyticsManager>.Instance.Initialize();
			Singleton<HintsManager>.Instance.Initialize();
			Singleton<SaveManager>.Instance.Initialize();
			Singleton<MultiplayerCoordinator>.Instance.Initialize();
			Singleton<DelayHelper>.Instance.DelayActionUntilEndOfFrame(() =>
			{
				Singleton<GlobalChatManager>.Instance.Initialize(Singleton<MultiplayerCoordinator>.Instance.LobbyService);
				LSS_LoadingScreen.SetupLoadingScreen(_mainLoading);
				Singleton<StateManager>.Instance.SwitchState<MainMenuState>();
			});
		}

		private void Update()
		{
			float unscaledDeltaTime = Time.unscaledDeltaTime;
			if (!(unscaledDeltaTime <= 0f))
			{
				_fpsElapsedSeconds += unscaledDeltaTime;
				_fpsFrameCount++;
			}
		}

		private void OnApplicationQuit()
		{
			UtilityMethods.Log($"[GameController] OnApplicationQuit datetime={DateTime.Now}");
			TryOpenExitFeedbackForm();
			SendAverageFpsAnalytics();
			IsApplicationQuitting = true;
			Singleton<DelayHelper>.Instance.StopAllCoroutines();
		}

		private void SendAverageFpsAnalytics()
		{
			if (!(_fpsElapsedSeconds <= 0f) && _fpsFrameCount > 0)
			{
				_ = (float)_fpsFrameCount / _fpsElapsedSeconds;
			}
		}

		private void TryOpenExitFeedbackForm()
		{
			if (!_exitFeedbackFormOpened && _openExitFeedbackForm && !string.IsNullOrWhiteSpace(_exitFeedbackFormUrl))
			{
				if (!Uri.TryCreate(_exitFeedbackFormUrl, UriKind.Absolute, out var result) || (result.Scheme != Uri.UriSchemeHttp && result.Scheme != Uri.UriSchemeHttps))
				{
					UtilityMethods.Log("[GameController] Exit feedback form URL is invalid: " + _exitFeedbackFormUrl, LogType.Warning);
					return;
				}
				_exitFeedbackFormOpened = true;
				UtilityMethods.Log("[GameController] Opening exit feedback form.");
				Application.OpenURL(result.AbsoluteUri);
			}
		}
	}
}
