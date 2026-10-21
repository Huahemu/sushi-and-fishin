using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Core.Streaming;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Networking.Chat;
using PaperPixelGames.States;
using PaperPixelGames.Wheel;
using Unity.Netcode;
using UnityEngine.SceneManagement;

namespace PaperPixelGames.Framework.States.Game
{
	public class GameState : BaseState
	{
		private bool _enter;

		private bool _started;

		public override void LoadScene()
		{
			if (NetworkManager.Singleton.IsServer)
			{
				UtilityMethods.Log("[GameState] LoadScene");
				NetworkManager.Singleton.SceneManager.LoadScene("GameState", LoadSceneMode.Single);
			}
		}

		public override void EnterState()
		{
			if (!_enter)
			{
				UtilityMethods.Log("[GameState] EnterState");
				_enter = true;
				if (NetworkManager.Singleton.IsHost)
				{
					Singleton<GameProgressManager>.Instance.Load(Singleton<SaveManager>.Instance.CurrentSaveSlotData.GameProgress);
				}
				else
				{
					Singleton<GameProgressManager>.Instance.ClientSyncData();
				}
				Singleton<InputManager>.Instance.InputHandler.ResetUIMap();
				Singleton<GameUIManager>.Instance.Initialize();
				Singleton<PPG_WheelManager>.Instance.Initialize();
				Singleton<GlobalChatManager>.Instance.ClearNewContext();
			}
		}

		public void StartGame()
		{
			if (!_started)
			{
				UtilityMethods.Log("[GameState] StartGame:" + _started);
				_started = true;
				Singleton<ResidentManager>.Instance.Initialize();
				Singleton<GameProgressManager>.Instance.Initialize();
				Singleton<DressingSceneManager>.Instance.Initialize();
			}
		}

		public override void ExitState()
		{
			UtilityMethods.Log("[GameState] ExitState");
			_enter = false;
			_started = false;
			if (Singleton<HintsManager>.TryGetInstance(out var instance))
			{
				instance.ResetSession();
			}
			if (Singleton<SoundManager>.TryGetInstance(out var instance2))
			{
				instance2.ResetManager();
			}
			if (Singleton<DelayHelper>.TryGetInstance(out var instance3))
			{
				instance3.StopAllCoroutines();
			}
		}
	}
}
