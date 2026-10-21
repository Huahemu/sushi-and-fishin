using System;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.States;
using PaperPixelGames.Framework.States.Game;
using PaperPixelGames.Networking;
using UnityEngine;

namespace PaperPixelGames.Core.NetworkDevelopment
{
	public sealed class JoinBootstrapCoordinator : Singleton<JoinBootstrapCoordinator>
	{
		private readonly BootstrapCompletionState completion = new BootstrapCompletionState();

		private bool beginRequested;

		private bool mandatoryPreparationFailed;

		private double nextCheck;

		public override SingletonScope Scope => SingletonScope.Scene;

		public PPG_Player LocalPlayer { get; private set; }

		public JoinSessionContext CurrentContext { get; private set; }

		public bool LocalPlayerSpawned { get; private set; }

		public bool LocalPlayerRegistered { get; private set; }

		public bool BootstrapRequested { get; private set; }

		public bool BootstrapApplied { get; private set; }

		public bool BootstrapCompleted => completion.Started;

		private static bool ReturningToMenu
		{
			get
			{
				if (!Singleton<GameSessionLifecycle>.TryGetInstance(out var instance) || !instance.ReturnToMenuInProgress)
				{
					if (Singleton<MultiplayerCoordinator>.TryGetInstance(out var instance2))
					{
						return instance2.IsClientSessionWorkBlocked;
					}
					return false;
				}
				return true;
			}
		}

		private bool PrerequisitesReady
		{
			get
			{
				if (!mandatoryPreparationFailed && !ReturningToMenu && CurrentContext.IsValid && MatchesSession(CurrentContext) && LocalPlayerSpawned && LocalPlayerRegistered && BootstrapApplied && LocalPlayer != null && LocalPlayer.IsSpawned && LocalPlayer.IsOwner && LocalPlayer.IsLocalGameplayPreparationReady && QuestManager.Instance != null)
				{
					return QuestManager.Instance.IsStateReady;
				}
				return false;
			}
		}

		protected override void Awake()
		{
			base.Awake();
			ResetState();
		}

		private void Update()
		{
			if (!completion.Started && !mandatoryPreparationFailed && !(MultiplayerDiagnostics.Now < nextCheck))
			{
				nextCheck = MultiplayerDiagnostics.Now + 0.25;
				TryReportBootstrapCompletion();
				if (beginRequested)
				{
					TryBeginGameplay();
				}
			}
		}

		public void NotifyLocalPlayerSpawned(PPG_Player player)
		{
			if (!(player == null) && player.IsOwner && player.IsSpawned && !ReturningToMenu)
			{
				LocalPlayer = player;
				LocalPlayerSpawned = true;
				MultiplayerDiagnostics.Record("local_player_spawned", "valid=True", ulong.MaxValue);
			}
		}

		public void NotifyLocalPlayerRegistered(PPG_Player player)
		{
			if (!(player == null) && !(player != LocalPlayer) && player.IsOwner && player.IsSpawned && player.IsLocalGameplayPreparationReady && !ReturningToMenu)
			{
				LocalPlayerRegistered = true;
				MultiplayerDiagnostics.Record("local_player_registered", "valid=True cosmeticsRequired=False", ulong.MaxValue);
				TryReportBootstrapCompletion();
			}
		}

		public void NotifyBootstrapApplied(JoinSessionContext context)
		{
			if (!context.SameSnapshot(CurrentContext) || !MatchesSession(context) || ReturningToMenu)
			{
				MultiplayerDiagnostics.Record("bootstrap_context_rejected", $"snapshotEpoch={context.SessionEpoch}", ulong.MaxValue, critical: true);
				return;
			}
			CurrentContext = context;
			BootstrapApplied = true;
			MultiplayerDiagnostics.Record("bootstrap_applied", $"mode={context.Mode} snapshotEpoch={context.SessionEpoch}", ulong.MaxValue);
			TryReportBootstrapCompletion();
		}

		public bool PrepareSnapshot(JoinSessionContext context)
		{
			if (!context.HasSnapshotIdentity || !MatchesSession(context) || ReturningToMenu || mandatoryPreparationFailed || !completion.InvalidateSnapshot())
			{
				return false;
			}
			CurrentContext = context;
			BootstrapApplied = false;
			beginRequested = false;
			nextCheck = 0.0;
			return true;
		}

		public void NotifyBootstrapRequested()
		{
			BootstrapRequested = true;
			MultiplayerDiagnostics.Record("bootstrap_request_sent", "", ulong.MaxValue);
		}

		public void NotifyQuestStateReady()
		{
			MultiplayerDiagnostics.Record("quest_state_ready", "", ulong.MaxValue);
			TryReportBootstrapCompletion();
		}

		public void ResetState()
		{
			completion.Reset();
			beginRequested = (mandatoryPreparationFailed = false);
			nextCheck = 0.0;
			LocalPlayer = null;
			CurrentContext = default;
			bool flag = (BootstrapApplied = false);
			bool flag3 = (BootstrapRequested = flag);
			bool localPlayerSpawned = (LocalPlayerRegistered = flag3);
			LocalPlayerSpawned = localPlayerSpawned;
		}

		private static bool MatchesSession(JoinSessionContext context)
		{
			if (Singleton<MultiplayerCoordinator>.TryGetInstance(out var instance))
			{
				return instance.SessionEpoch == context.SessionEpoch;
			}
			return false;
		}

		private void TryReportBootstrapCompletion()
		{
			if (completion.Reported || mandatoryPreparationFailed)
			{
				return;
			}
			try
			{
				completion.TryReport(PrerequisitesReady, () =>
				{
					if (!Singleton<SaveManager>.TryGetInstance(out var instance))
					{
						return false;
					}
					MultiplayerDiagnostics.Record("profile_load_begin", "", ulong.MaxValue);
					bool num = instance.TryLoadClientProfile(CurrentContext.HostSteamId, CurrentContext.SlotId);
					if (num)
					{
						MultiplayerDiagnostics.Record("profile_load_complete", "", ulong.MaxValue);
					}
					return num && PrerequisitesReady;
				}, () =>
				{
					if (!PrerequisitesReady || !Singleton<GameProgressManager>.TryGetInstance(out var instance))
					{
						return false;
					}
					RpcRelay rpcRelay = instance.RpcRelay;
					if (rpcRelay == null || !rpcRelay.IsSpawned)
					{
						return false;
					}
					rpcRelay.ServerReportBootstrapCompleteRpc(CurrentContext.Mode, CurrentContext.SessionEpoch, CurrentContext.SnapshotId, CurrentContext.WorldRevision);
					MultiplayerDiagnostics.Record("bootstrap_completion_sent", "", ulong.MaxValue);
					return true;
				});
				if (beginRequested)
				{
					TryBeginGameplay();
				}
			}
			catch (Exception exception)
			{
				FailMandatoryPreparation(exception);
			}
		}

		public void ResendBootstrapCompletion()
		{
			if (completion.Reported && PrerequisitesReady)
			{
				RpcRelay rpcRelay = (Singleton<GameProgressManager>.TryGetInstance(out var instance) ? instance.RpcRelay : null);
				if (rpcRelay != null && rpcRelay.IsSpawned)
				{
					rpcRelay.ServerReportBootstrapCompleteRpc(CurrentContext.Mode, CurrentContext.SessionEpoch, CurrentContext.SnapshotId, CurrentContext.WorldRevision);
				}
			}
		}

		public void OnBeginGameplayReceived(ulong epoch, ulong snapshotId, ulong revision)
		{
			if (!ReturningToMenu && !mandatoryPreparationFailed && MatchesSession(CurrentContext) && CurrentContext.SessionEpoch == epoch && CurrentContext.SnapshotId == snapshotId && CurrentContext.WorldRevision == revision)
			{
				beginRequested = true;
				TryBeginGameplay();
			}
		}

		private void TryBeginGameplay()
		{
			try
			{
				RpcRelay activeInstance = RpcRelay.ActiveInstance;
				if (completion.TryStart(PrerequisitesReady && activeInstance != null && activeInstance.ClientReplayReady, () =>
				{
					if (!Singleton<StateManager>.TryGetInstance(out var instance) || !(instance.CurrentState is GameState gameState))
					{
						return false;
					}
					gameState.StartGame();
					return !ReturningToMenu;
				}))
				{
					activeInstance.ReleaseBootstrapHistory();
					MultiplayerDiagnostics.Record("local_gameplay_begin", "cosmeticsRequired=False profileReady=True", ulong.MaxValue);
					MultiplayerDiagnostics.Flush("local_gameplay_begin");
					Singleton<MultiplayerCoordinator>.Instance?.NotifyLocalGameplayStarted();
				}
			}
			catch (Exception exception)
			{
				FailMandatoryPreparation(exception);
			}
		}

		private void FailMandatoryPreparation(Exception exception)
		{
			mandatoryPreparationFailed = true;
			MultiplayerDiagnostics.Record("mandatory_preparation_failed", exception.GetType().Name, ulong.MaxValue, critical: true);
			Debug.LogException(exception);
			if (Singleton<GameSessionLifecycle>.TryGetInstance(out var instance))
			{
				instance.ReturnToMainMenu(ReturnToMainMenuRequest.StartupFailure(MultiplayerFailure.Create(MultiplayerFailureCode.BootstrapFailed, MultiplayerFailureStage.Bootstrap, "Mandatory player preparation failed: " + exception.Message)));
			}
		}
	}
}
