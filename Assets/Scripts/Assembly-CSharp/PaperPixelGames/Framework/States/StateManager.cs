using System;
using PaperPixelGames.Core;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.States.Game;
using PaperPixelGames.Framework.States.MainMenu;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PaperPixelGames.Framework.States
{
	public class StateManager : Singleton<StateManager>
	{
		private IState _enteredState;

		public IState CurrentState { get; set; }

		private static bool IsReturningToMenu
		{
			get
			{
				if (Singleton<GameSessionLifecycle>.TryGetInstance(out var instance))
				{
					return instance.ReturnToMenuInProgress;
				}
				return false;
			}
		}

		protected override void Awake()
		{
			base.Awake();
			SceneManager.sceneLoaded += OnSceneLoaded;
			Application.wantsToQuit += OnWantsToQuit;
		}

		private void OnDestroy()
		{
			SceneManager.sceneLoaded -= OnSceneLoaded;
			Application.wantsToQuit -= OnWantsToQuit;
		}

		private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			HandleSceneLoaded(scene.name, mode, () =>
			{
				SceneManager.SetActiveScene(scene);
			});
		}

		private void HandleSceneLoaded(string sceneName, LoadSceneMode mode, Action activateScene)
		{
			UtilityMethods.Log("[StateManager] OnSceneLoaded:" + sceneName + "-" + mode);
			if (mode == LoadSceneMode.Single)
			{
				if (!TrySelectStateForScene(sceneName))
				{
					MultiplayerDiagnostics.Record("state_scene_ignored", $"scene={sceneName} state={CurrentState?.GetType().Name} returningToMenu={IsReturningToMenu}", ulong.MaxValue);
				}
				else if (_enteredState != CurrentState)
				{
					activateScene();
					_enteredState = CurrentState;
					MultiplayerDiagnostics.Record("state_scene_enter", "scene=" + sceneName + " state=" + CurrentState.GetType().Name, ulong.MaxValue);
					CurrentState.EnterState();
				}
			}
		}

		private bool TrySelectStateForScene(string sceneName)
		{
			if (IsReturningToMenu && sceneName != "MainMenuState")
			{
				return false;
			}
			switch (sceneName)
			{
			case "GameState":
			{
				if (Singleton<MultiplayerCoordinator>.TryGetInstance(out var instance) && instance.IsClientSessionWorkBlocked)
				{
					return false;
				}
				if (CurrentState is LoadingState)
				{
					CurrentState = new GameState();
				}
				return CurrentState is GameState;
			}
			case "MainMenuState":
				return CurrentState is MainMenuState;
			case "LoadingState":
				return CurrentState is LoadingState;
			default:
				return false;
			}
		}

		private static bool OnWantsToQuit()
		{
			if (Singleton<SaveManager>.TryGetInstance(out var instance))
			{
				instance.FlushPendingAutoSave();
			}
			bool flag = Singleton<GameController>.Instance != null && Singleton<GameController>.Instance.IsApplicationQuitting;
			bool flag2 = Singleton<SaveManager>.Instance != null && Singleton<SaveManager>.Instance.SaveFlag;
			UtilityMethods.Log($"[StateManager] OnWantsToQuit forceQuit={flag}, saveFlag={flag2}");
			if (flag)
			{
				return true;
			}
			if (flag2)
			{
				UtilityMethods.Log("[StateManager] Quit blocked because SaveFlag is true.");
				return false;
			}
			return true;
		}

		public void SwitchState<TState>() where TState : IState, new()
		{
			if (IsReturningToMenu && !typeof(MainMenuState).IsAssignableFrom(typeof(TState)))
			{
				MultiplayerDiagnostics.Record("state_transition_ignored", "requested=" + typeof(TState).Name + " reason=ReturningToMenu", ulong.MaxValue);
				return;
			}
			UtilityMethods.Log("[StateManager] Switching to " + typeof(TState).Name);
			CurrentState?.ExitState();
			CurrentState = new TState();
			CurrentState.LoadScene();
		}

		public void ExitCurrentState()
		{
			CurrentState?.ExitState();
		}

		private void Update()
		{
			CurrentState?.UpdateState();
		}

		private void LateUpdate()
		{
			CurrentState?.LateUpdateState();
		}
	}
}
