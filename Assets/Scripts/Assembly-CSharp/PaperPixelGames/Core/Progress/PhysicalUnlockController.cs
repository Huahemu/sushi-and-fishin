using PaperPixelGames.Core.Economy;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Interaction.ToolTip;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Progression;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Progress
{
	public class PhysicalUnlockController : NetworkBehaviour
	{
		[SerializeField]
		private PhysicalStateController _physicalStateController;

		[SerializeField]
		private ProgressionUnlockKey _progressionUnlockKey;

		[SerializeField]
		[Min(0f)]
		private int _unlockPrice;

		[SerializeField]
		private string _displayName = "Station";

		[SerializeField]
		private InteractableTooltip _interactableTooltip;

		private ProgressionManager _progressionManager;

		private bool _isTooltipShowing;

		public PhysicalStateController PhysicalStateController => _physicalStateController;

		public ProgressionUnlockKey ProgressionUnlockKey => _progressionUnlockKey;

		public int UnlockPrice => Mathf.Max(0, _unlockPrice);

		public bool IsPhysicallyUsable
		{
			get
			{
				if (!(_physicalStateController == null))
				{
					return _physicalStateController.IsPhysicallyUsable;
				}
				return true;
			}
		}

		private void OnEnable()
		{
			TryBindProgressionManager();
		}

		private void OnDisable()
		{
			HideTooltipLocal();
			UnbindProgressionManager();
		}

		public bool TryUnlock(Interactor interactor)
		{
			if (IsPhysicallyUsable)
			{
				return true;
			}
			if (interactor == null || interactor.NetworkObject == null)
			{
				return false;
			}
			if (!IsProgressionUnlockedLocal())
			{
				ShowBlockedToast(GetProgressionConditionText());
				RefreshTooltipLocal();
				return false;
			}
			if (!HasEnoughMoneyLocal())
			{
				ShowBlockedToast(GetMoneyConditionText());
				RefreshTooltipLocal();
				return false;
			}
			ServerTryUnlockRpc(new NetworkObjectReference(interactor.NetworkObject));
			return true;
		}

		public void ShowTooltipLocal()
		{
			if (!(_interactableTooltip == null) && !(Singleton<ToolTipHandler>.Instance == null) && !IsPhysicallyUsable)
			{
				RefreshTooltipLocal();
				_interactableTooltip.IsEnabled = true;
				Singleton<ToolTipHandler>.Instance.OnGazeEnter(_interactableTooltip, _interactableTooltip.GetWorldPosition());
				_isTooltipShowing = true;
			}
		}

		public void HideTooltipLocal()
		{
			if (!(_interactableTooltip == null) && !(Singleton<ToolTipHandler>.Instance == null))
			{
				Singleton<ToolTipHandler>.Instance.OnGazeExit(_interactableTooltip);
				_isTooltipShowing = false;
			}
		}

		public void RefreshTooltipLocal()
		{
			if (!(_interactableTooltip == null))
			{
				TooltipData baseTooltip = _interactableTooltip.GetBaseTooltip();
				baseTooltip.Title = ProgressionTooltipLocalization.GetPhysicalUnlockTitle(_progressionUnlockKey, string.IsNullOrWhiteSpace(baseTooltip.Title) ? GetDisplayName() : baseTooltip.Title);
				baseTooltip.Body = GetCurrentConditionText();
				_interactableTooltip.SetRuntimeTooltip(baseTooltip);
				if (_isTooltipShowing && Singleton<ToolTipHandler>.Instance != null)
				{
					Singleton<ToolTipHandler>.Instance.OnGazeEnter(_interactableTooltip, _interactableTooltip.GetWorldPosition());
				}
			}
		}

		private void TryBindProgressionManager()
		{
			ProgressionManager progressionManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.ProgressionManager : null);
			if (!(_progressionManager == progressionManager))
			{
				UnbindProgressionManager();
				_progressionManager = progressionManager;
				if (_progressionManager != null)
				{
					_progressionManager.ProgressionChanged += OnProgressionChanged;
				}
			}
		}

		private void UnbindProgressionManager()
		{
			if (_progressionManager != null)
			{
				_progressionManager.ProgressionChanged -= OnProgressionChanged;
			}
			_progressionManager = null;
		}

		private void OnProgressionChanged(int level, double currentXp, double nextLevelRequiredTotalXp)
		{
			if (_isTooltipShowing)
			{
				RefreshTooltipLocal();
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerTryUnlockRpc(NetworkObjectReference interactorRef)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(4040064763u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 4040064763u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (_physicalStateController == null)
			{
				UtilityMethods.Log("[PhysicalUnlock] Unlock failed. PhysicalStateController is missing. Target=" + GetDisplayName(), LogType.Warning);
			}
			else
			{
				if (_physicalStateController.IsPhysicallyUsable)
				{
					return;
				}
				ulong targetClientId = 0uL;
				Interactor component = null;
				bool flag = interactorRef.TryGet(out var networkObject) && networkObject != null && networkObject.TryGetComponent<Interactor>(out component);
				if (flag)
				{
					targetClientId = component.OwnerClientId;
				}
				if (!IsProgressionUnlockedServer())
				{
					SendBlockedToast(flag, targetClientId, GetProgressionConditionText());
					return;
				}
				EconomyHandler economyHandler = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.EconomyHandler : null);
				int unlockPrice = UnlockPrice;
				if (unlockPrice > 0)
				{
					if (economyHandler == null)
					{
						UtilityMethods.Log("[PhysicalUnlock] Unlock failed. EconomyHandler is missing. Target=" + GetDisplayName(), LogType.Warning);
						return;
					}
					if (!economyHandler.HasSufficientBalance(unlockPrice))
					{
						SendBlockedToast(flag, targetClientId, GetMoneyConditionText());
						return;
					}
					economyHandler.SpendMoney(unlockPrice);
				}
				UtilityMethods.Log($"[PhysicalUnlock] Unlock completed. Target={GetDisplayName()}, Key={_progressionUnlockKey}, Price={unlockPrice}");
				_physicalStateController.Repair();
			}
		}

		private bool IsProgressionUnlockedLocal()
		{
			GameProgressManager instance = Singleton<GameProgressManager>.Instance;
			if (!(instance == null))
			{
				return instance.IsProgressionUnlocked(_progressionUnlockKey);
			}
			return true;
		}

		private bool IsProgressionUnlockedServer()
		{
			GameProgressManager instance = Singleton<GameProgressManager>.Instance;
			if (!(instance == null))
			{
				return instance.IsProgressionUnlocked(_progressionUnlockKey);
			}
			return true;
		}

		private bool HasEnoughMoneyLocal()
		{
			int unlockPrice = UnlockPrice;
			if (unlockPrice <= 0)
			{
				return true;
			}
			EconomyHandler economyHandler = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.EconomyHandler : null);
			if (!(economyHandler == null))
			{
				return economyHandler.HasSufficientBalance(unlockPrice);
			}
			return true;
		}

		private string GetCurrentConditionText()
		{
			if (!IsProgressionUnlockedLocal())
			{
				return GetProgressionConditionText();
			}
			return GetMoneyConditionText();
		}

		private string GetProgressionConditionText()
		{
			int num = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.GetRequiredProgressionLevel(_progressionUnlockKey) : (-1));
			if (num > 0)
			{
				return ProgressionTooltipLocalization.GetReachLevelText(num);
			}
			return ProgressionTooltipLocalization.GetRequiredLevelText();
		}

		private string GetMoneyConditionText()
		{
			int unlockPrice = UnlockPrice;
			if (unlockPrice <= 0)
			{
				return ProgressionTooltipLocalization.GetUnlockActionText();
			}
			return ProgressionTooltipLocalization.GetUnlockCostText(unlockPrice);
		}

		private string GetDisplayName()
		{
			string fallback = (string.IsNullOrWhiteSpace(_displayName) ? base.name : _displayName);
			return ProgressionTooltipLocalization.GetUnlockDisplayName(_progressionUnlockKey, fallback);
		}

		private void SendBlockedToast(bool hasTargetClient, ulong targetClientId, string message)
		{
			if (hasTargetClient)
			{
				ClientShowBlockedToastRpc(message, base.RpcTarget.Single(targetClientId, RpcTargetUse.Temp));
			}
			else
			{
				UtilityMethods.Log("[PhysicalUnlock] Unlock blocked. Target=" + GetDisplayName() + ", Reason=" + message);
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientShowBlockedToastRpc(string message, RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(4240958683u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bool value = message != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(message);
				}
				__endSendRpc(ref bufferWriter, 4240958683u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ShowBlockedToast(message);
			}
		}

		private static void ShowBlockedToast(string message)
		{
			Singleton<NotifyManager>.Instance?.ShowToast(message, NotificationState.Info, 2f);
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(4040064763u, __rpc_handler_4040064763, "ServerTryUnlockRpc", RpcInvokePermission.Everyone);
			__registerRpc(4240958683u, __rpc_handler_4240958683, "ClientShowBlockedToastRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_4040064763(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				((PhysicalUnlockController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PhysicalUnlockController)target).ServerTryUnlockRpc(value);
				((PhysicalUnlockController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4240958683(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				string s = null;
				if (value)
				{
					reader.ReadValueSafe(out s, false);
				}
				RpcParams ext = rpcParams.Ext;
				((PhysicalUnlockController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PhysicalUnlockController)target).ClientShowBlockedToastRpc(s, ext);
				((PhysicalUnlockController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "PhysicalUnlockController";
		}
	}
}
