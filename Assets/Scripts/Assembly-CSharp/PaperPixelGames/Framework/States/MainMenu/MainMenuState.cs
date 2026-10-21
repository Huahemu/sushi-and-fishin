using System;
using Cysharp.Threading.Tasks;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Reporting;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.SceneManagement;
using PaperPixelGames.Framework.States.Game;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Networking;
using PaperPixelGames.States;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace PaperPixelGames.Framework.States.MainMenu
{
	public class MainMenuState : BaseState
	{
		public bool IsInitialized { get; private set; }

		public override void LoadScene()
		{
			Singleton<PPGSceneManager>.Instance.LoadScene("MainMenuState");
		}

		public override void EnterState()
		{
			EnterStateAsync().Forget();
		}

		private async UniTask EnterStateAsync()
		{
			UtilityMethods.Log("[MainMenuState] Entered Main Menu State");
			Singleton<ReportManager>.Instance.PlayfabInit();
			CursorHelper.CursorVisible = true;
			Singleton<InputManager>.Instance.InputHandler.ActivateUIMap();
			try
			{
				await LocalizationSettings.InitializationOperation;
			}
			catch (Exception ex)
			{
				UtilityMethods.Log("[MainMenuState] Localization initialization failed. Falling back to source text. Error=" + ex.Message, LogType.Error);
			}
			if (!(Singleton<StateManager>.Instance == null) && Singleton<StateManager>.Instance.CurrentState == this)
			{
				Singleton<MainMenuManager>.Instance.Initialize();
				if (Singleton<MultiplayerCoordinator>.Instance != null)
				{
					Singleton<MultiplayerCoordinator>.Instance.EnsureMenuLobby();
				}
				IsInitialized = true;
				MultiplayerDiagnostics.Record("main_menu_ready", "", ulong.MaxValue);
				if (Singleton<GameSessionLifecycle>.Instance != null)
				{
					Singleton<GameSessionLifecycle>.Instance.NotifyMainMenuEntered();
				}
				if (Singleton<GameSessionLifecycle>.Instance == null || !Singleton<GameSessionLifecycle>.Instance.ReturnToMenuInProgress)
				{
					LoadingScreen.ForceHide();
				}
			}
		}

		public override void ExitState()
		{
			if (Singleton<MainMenuManager>.TryGetInstance(out var instance))
			{
				instance.CancelBoatDeparture();
			}
			CursorHelper.CursorVisible = false;
			IsInitialized = false;
			Singleton<SoundManager>.Instance.ResetManager();
			Singleton<DelayHelper>.Instance.StopAllCoroutines();
		}
	}
}
