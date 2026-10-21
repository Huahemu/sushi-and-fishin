using System;
using System.Collections;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.SceneManagement;
using PaperPixelGames.Framework.States.MainMenu;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Networking;
using PaperPixelGames.Networking.Chat;
using UnityEngine;

namespace PaperPixelGames.Framework.States.Game
{
	public sealed class GameSessionLifecycle : Singleton<GameSessionLifecycle>
	{
		[Header("Transition")]
		[SerializeField]
		private float _returnToMenuCoverDelaySeconds = 1.5f;

		[SerializeField]
		[Min(1f)]
		private float _mainMenuEntryTimeoutSeconds = 30f;

		private bool _returnToMenuInProgress;

		private bool _returnAudioPaused;

		private bool _mainMenuEnteredDuringReturn;

		private MultiplayerFailure _pendingFailureToast;

		private MultiplayerFailure _pendingFailureChat;

		public bool ReturnToMenuInProgress => _returnToMenuInProgress;

		private void Update()
		{
			if (!_returnToMenuInProgress)
			{
				TryShowPendingFailureToast();
				TryShowPendingFailureChat();
			}
		}

		public void ReturnToMainMenu(ReturnToMainMenuRequest request)
		{
			if (_returnToMenuInProgress)
			{
				UtilityMethods.Log("<b><color=#FFB020>[Session]</color></b> ReturnToMainMenu ignored. Already in progress." + $"\n   Reason: {request.Reason}" + "\n   DebugReason: " + request.DebugReason, LogType.Warning);
				return;
			}
			if (!request.ShowErrorToast)
			{
				_pendingFailureToast = default;
				_pendingFailureChat = default;
			}
			QueueFailureToast(request);
			StartCoroutine(CoReturnToMainMenu(request));
		}

		private IEnumerator CoReturnToMainMenu(ReturnToMainMenuRequest request)
		{
			_returnToMenuInProgress = true;
			_mainMenuEnteredDuringReturn = false;
			Singleton<MultiplayerCoordinator>.TryGetInstance(out var coordinator);
			PPGSceneManager sceneManager = Singleton<PPGSceneManager>.Instance;
			bool menuReady = Singleton<StateManager>.TryGetInstance(out var instance) && instance.CurrentState is MainMenuState mainMenuState && mainMenuState.IsInitialized;
			bool useCover = RequiresReturnCover(menuReady, sceneManager != null && sceneManager.IsLoading, coordinator == null || coordinator.IsSessionShutdownComplete);
			long coverVersion = (useCover ? LoadingScreen.BeginReturnCover() : 0);
			if (coordinator != null)
			{
				coordinator.BeginSessionReturn(request);
			}
			MultiplayerDiagnostics.Record("return_to_menu_begin", $"reason={request.Reason} code={request.Failure.Code} stage={request.Failure.Stage} detail={request.DebugReason}", ulong.MaxValue, critical: true);
			MultiplayerDiagnostics.Flush("return_to_menu_begin");
			UtilityMethods.Log("<b><color=#FFB020>[Session]</color></b> Returning to main menu" + $"\n   Reason: {request.Reason}" + "\n   DebugReason: " + request.DebugReason);
			if (Singleton<StateManager>.Instance != null && !(Singleton<StateManager>.Instance.CurrentState is MainMenuState))
			{
				PauseAudioForReturnTransition();
			}
			if (useCover && _returnToMenuCoverDelaySeconds > 0f)
			{
				yield return new WaitForSecondsRealtime(_returnToMenuCoverDelaySeconds);
			}
			float sceneWarningAt = Time.realtimeSinceStartup + _mainMenuEntryTimeoutSeconds;
			bool warned = false;
			while ((sceneManager != null && sceneManager.IsLoading) || (coordinator != null && !coordinator.IsSessionShutdownComplete))
			{
				if (!warned && Time.realtimeSinceStartup >= sceneWarningAt)
				{
					warned = true;
					MultiplayerDiagnostics.Record("return_waiting_for_native_scene", "Network/scene work is still draining; replacement session remains blocked.", ulong.MaxValue, critical: true);
				}
				yield return null;
			}
			bool success = false;
			try
			{
				DeformableManagerResetUtility.ResetDefaultManager();
				if (Singleton<MultiplayerCoordinator>.Instance != null)
				{
					Singleton<MultiplayerCoordinator>.Instance.ResetSessionForMainMenu(request);
				}
				if (!(Singleton<StateManager>.Instance.CurrentState is MainMenuState mainMenuState2))
				{
					Singleton<StateManager>.Instance.SwitchState<MainMenuState>();
				}
				else
				{
					_mainMenuEnteredDuringReturn = mainMenuState2.IsInitialized;
					if (Singleton<MultiplayerCoordinator>.Instance != null)
					{
						Singleton<MultiplayerCoordinator>.Instance.EnsureMenuLobby();
					}
				}
				success = true;
			}
			catch (Exception arg)
			{
				ResumeAudioAfterReturnTransition();
				UtilityMethods.Log("<b><color=#FF4040>[Session]</color></b> ReturnToMainMenu failed." + $"\n   Reason: {request.Reason}" + "\n   DebugReason: " + request.DebugReason + $"\n   Exception: {arg}", LogType.Error);
			}
			if (success)
			{
				float mainMenuDeadline = Time.realtimeSinceStartup + _mainMenuEntryTimeoutSeconds;
				while (!_mainMenuEnteredDuringReturn && Time.realtimeSinceStartup < mainMenuDeadline)
				{
					yield return null;
				}
				if (!_mainMenuEnteredDuringReturn)
				{
					success = false;
					ResumeAudioAfterReturnTransition();
					UtilityMethods.Log("<b><color=#FF4040>[Session]</color></b> Main menu entry timed out." + $"\n   TimeoutSeconds: {_mainMenuEntryTimeoutSeconds:0.0}" + $"\n   Reason: {request.Reason}", LogType.Error);
				}
				else if (useCover)
				{
					yield return null;
				}
			}
			if (useCover)
			{
				LoadingScreen.EndReturnCover(coverVersion);
			}
			else
			{
				LoadingScreen.ForceHide();
			}
			yield return ShowPendingFailureToastWhenReady();
			TryShowPendingFailureChat();
			_returnToMenuInProgress = false;
			if (success && coordinator != null)
			{
				coordinator.CompleteSessionReturn();
			}
			MultiplayerDiagnostics.Record("return_to_menu_complete", $"success={success} reason={request.Reason}", ulong.MaxValue);
			UtilityMethods.Log("<b><color=#8FD14F>[Session]</color></b> Return to main menu completed." + $"\n   Success: {success}" + $"\n   Reason: {request.Reason}");
		}

		private static bool RequiresReturnCover(bool menuReady, bool sceneLoading, bool networkStopped)
		{
			if (!(!menuReady | sceneLoading))
			{
				return !networkStopped;
			}
			return true;
		}

		private void QueueFailureToast(ReturnToMainMenuRequest request)
		{
			if (request.ShowErrorToast && request.Failure.HasFailure)
			{
				_pendingFailureToast = request.Failure;
				_pendingFailureChat = request.Failure;
			}
		}

		private IEnumerator ShowPendingFailureToastWhenReady()
		{
			if (_pendingFailureToast.HasFailure)
			{
				float deadline = Time.realtimeSinceStartup + 10f;
				NotifyManager instance;
				while (!Singleton<NotifyManager>.TryGetInstance(out instance) && Time.realtimeSinceStartup < deadline)
				{
					yield return null;
				}
				if (!Singleton<NotifyManager>.TryGetInstance(out var instance2))
				{
					UtilityMethods.Log("<b><color=#FF4040>[Session]</color></b> Failure toast could not be shown because NotifyManager was unavailable." + $"\n   Code: {_pendingFailureToast.Code}" + $"\n   Stage: {_pendingFailureToast.Stage}", LogType.Error);
				}
				else
				{
					TryShowPendingFailureToast(instance2);
				}
			}
		}

		private void TryShowPendingFailureToast()
		{
			if (_pendingFailureToast.HasFailure && Singleton<NotifyManager>.TryGetInstance(out var instance))
			{
				TryShowPendingFailureToast(instance);
			}
		}

		private void TryShowPendingFailureToast(NotifyManager notifyManager)
		{
			MultiplayerFailure pendingFailureToast = _pendingFailureToast;
			_pendingFailureToast = default;
			string message = LocalizationDatabase.GetString("UiTable", pendingFailureToast.LocalizationKey, pendingFailureToast.UserMessage);
			notifyManager.ShowToast(message, NotificationState.Error);
		}

		private void TryShowPendingFailureChat()
		{
			if (_pendingFailureChat.HasFailure && Singleton<GlobalChatManager>.TryGetInstance(out var instance))
			{
				MultiplayerFailure pendingFailureChat = _pendingFailureChat;
				_pendingFailureChat = default;
				instance.AddLocalizedMultiplayerFailure(pendingFailureChat, preserveForNextLobby: true);
			}
		}

		public void NotifyMainMenuEntered()
		{
			ResumeAudioAfterReturnTransition();
			if (_returnToMenuInProgress)
			{
				_mainMenuEnteredDuringReturn = true;
			}
		}

		private void PauseAudioForReturnTransition()
		{
			if (!_returnAudioPaused)
			{
				_returnAudioPaused = true;
				AudioListener.pause = true;
				UtilityMethods.Log("<b><color=#FFB020>[Session Audio]</color></b> Audio paused for return-to-menu transition.");
			}
		}

		public void ResumeAudioAfterReturnTransition()
		{
			if (_returnAudioPaused)
			{
				AudioListener.pause = false;
				_returnAudioPaused = false;
				UtilityMethods.Log("<b><color=#8FD14F>[Session Audio]</color></b> Audio resumed after entering main menu.");
			}
		}
	}
}
