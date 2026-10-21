using System;
using DistantLands.Cozy;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Networking.Chat;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Restaurant
{
	public class OpenCloseSign : BaseInteractable
	{
		[SerializeField]
		private Animator _signAnimator;

		[Header("Opening Hours")]
		[SerializeField]
		[Range(0f, 23f)]
		private int _shopOpenHour;

		[SerializeField]
		[Range(0f, 23f)]
		private int _shopCloseHour;

		private bool _onAnim;

		private Coroutine _flagCor;

		private readonly NetworkVariable<bool> _isOpen = new NetworkVariable<bool>(value: true);

		private QuestManager _questManager;

		private bool IsServerOrOffline
		{
			get
			{
				if (!(NetworkManager.Singleton == null) && NetworkManager.Singleton.IsListening)
				{
					return base.IsServer;
				}
				return true;
			}
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			NetworkVariable<bool> isOpen = _isOpen;
			isOpen.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isOpen.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnOpenCloseBoolChange));
			if (base.IsServer)
			{
				RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
				if (restaurantManager != null)
				{
					_isOpen.Value = restaurantManager.IsRestaurantOpen;
				}
			}
			ApplyStateSafe(_isOpen.Value);
			TryBindQuestManager();
			TryCompleteFishOrderOpenQuestIfAlreadyOpen();
		}

		public override void OnNetworkDespawn()
		{
			UnbindQuestManager();
			NetworkVariable<bool> isOpen = _isOpen;
			isOpen.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isOpen.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnOpenCloseBoolChange));
			base.OnNetworkDespawn();
		}

		private void Start()
		{
			if (!base.IsSpawned)
			{
				TryBindQuestManager();
				TryCompleteFishOrderOpenQuestIfAlreadyOpen();
			}
		}

		public override void OnDestroy()
		{
			UnbindQuestManager();
			base.OnDestroy();
		}

		public override void EnableGazeHints()
		{
			Singleton<HintsManager>.Instance.AddHint("Interact", "key_use");
		}

		public override void DisableGazeHints()
		{
			Singleton<HintsManager>.Instance.RemoveHint("Interact");
		}

		protected override void ClientInteract(Interactor interactor, InputActionPhase phase)
		{
			base.ClientInteract(interactor, phase);
			if (phase == InputActionPhase.Started)
			{
				TryRequestSignToggle();
			}
		}

		protected override void ClientCrossInteract(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			base.ClientCrossInteract(interactor, held, phase);
			if (phase == InputActionPhase.Started)
			{
				TryRequestSignToggle();
			}
		}

		private void TryRequestSignToggle()
		{
			GameProgressManager instance = Singleton<GameProgressManager>.Instance;
			if (!(instance == null))
			{
				bool flag = !_isOpen.Value;
				if (flag && !CanOpenByTime())
				{
					Singleton<NotifyManager>.Instance.ShowToast($"Restaurant can only be opened between {_shopOpenHour:00}:00 and {_shopCloseHour:00}:00.");
				}
				else if (!CanUseSignClient(instance, flag))
				{
					Singleton<NotifyManager>.Instance.ShowToast("First complete the tutorial!");
				}
				else
				{
					ServerUpdateSignRpc();
				}
			}
		}

		private bool CanUseSignClient(GameProgressManager gameProgressManager, bool wantsToOpenRestaurant)
		{
			if (gameProgressManager.CompleteOpenSignTuto)
			{
				return true;
			}
			if (!wantsToOpenRestaurant)
			{
				return false;
			}
			return IsFirstOpenSignQuestActiveOrCompleted();
		}

		[Rpc(SendTo.Server)]
		private void ServerUpdateSignRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(2108185636u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 2108185636u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (_onAnim)
			{
				return;
			}
			GameProgressManager instance = Singleton<GameProgressManager>.Instance;
			if (instance == null)
			{
				return;
			}
			bool flag = !_isOpen.Value;
			if ((flag && !CanOpenByTime()) || !CanUseSignServer(instance, flag))
			{
				return;
			}
			_onAnim = true;
			bool flag2 = !_isOpen.Value;
			_isOpen.Value = flag2;
			RestaurantManager restaurantManager = instance.RestaurantManager;
			if (restaurantManager != null)
			{
				restaurantManager.IsRestaurantOpen = flag2;
				if (flag2)
				{
					restaurantManager.ServerOnRestaurantOpened();
					GlobalChatManager.BroadcastLocalizedInfoFromServer(this, "chat.info.restaurant_opened", "Restaurant is now open.");
					CompleteOpenSignRelatedQuestsServer(instance);
					TryCompleteFishOrderOpenQuestIfAlreadyOpen();
				}
				else
				{
					restaurantManager.ServerOnRestaurantClosed();
					GlobalChatManager.BroadcastLocalizedInfoFromServer(this, "chat.info.restaurant_closed", "Restaurant is now closed.");
				}
			}
			if (_flagCor != null)
			{
				Singleton<DelayHelper>.Instance.StopCoroutine(_flagCor);
			}
			_flagCor = Singleton<DelayHelper>.Instance.DelayAction(2.1f, () =>
			{
				_onAnim = false;
			});
		}

		private bool CanUseSignServer(GameProgressManager gameProgressManager, bool wantsToOpenRestaurant)
		{
			if (gameProgressManager.CompleteOpenSignTuto)
			{
				return true;
			}
			if (!wantsToOpenRestaurant)
			{
				return false;
			}
			return IsFirstOpenSignQuestActiveOrCompleted();
		}

		private bool IsFirstOpenSignQuestActiveOrCompleted()
		{
			QuestReferenceHub instance = QuestReferenceHub.Instance;
			if (instance == null)
			{
				return false;
			}
			return instance.IsActiveOrCompleted(QuestId.TurnTheSignToOpen);
		}

		private void CompleteOpenSignRelatedQuestsServer(GameProgressManager gameProgressManager)
		{
			QuestReferenceHub instance = QuestReferenceHub.Instance;
			if (!(instance == null))
			{
				if (!gameProgressManager.CompleteOpenSignTuto)
				{
					instance.CompleteIfActive(QuestId.TurnTheSignToOpen);
					gameProgressManager.CompleteOpenSignTutorial();
					gameProgressManager.CompleteTimeTutorial();
					ResumeTimeForEveryoneRpc();
				}
				else
				{
					instance.CompleteIfActive(QuestId.OpenTheRestaurantForFishOrders);
				}
			}
		}

		private bool TryBindQuestManager()
		{
			if (!IsServerOrOffline)
			{
				return false;
			}
			if (_questManager != null)
			{
				return true;
			}
			_questManager = QuestManager.Instance;
			if (_questManager == null)
			{
				return false;
			}
			_questManager.QuestStateChanged += HandleQuestStateChanged;
			return true;
		}

		private void UnbindQuestManager()
		{
			if (!(_questManager == null))
			{
				_questManager.QuestStateChanged -= HandleQuestStateChanged;
				_questManager = null;
			}
		}

		private void HandleQuestStateChanged()
		{
			TryCompleteFishOrderOpenQuestIfAlreadyOpen();
		}

		private void TryCompleteFishOrderOpenQuestIfAlreadyOpen()
		{
			if (IsServerOrOffline && _isOpen.Value)
			{
				QuestReferenceHub.Instance?.CompleteIfActive(QuestId.OpenTheRestaurantForFishOrders);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ResumeTimeForEveryoneRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(3206944577u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3206944577u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				GameProgressManager instance = Singleton<GameProgressManager>.Instance;
				if (!(instance == null) && !(instance.CozyWeatherManager == null))
				{
					instance.CozyWeatherManager.ClientResumeTime();
				}
			}
		}

		private void OnOpenCloseBoolChange(bool previousValue, bool nextValue)
		{
			ApplyStateSafe(nextValue);
		}

		private void ApplyStateSafe(bool isOpen)
		{
			if (_signAnimator != null)
			{
				_signAnimator.SetBool("IsOpen", isOpen);
			}
			GameProgressManager instance = Singleton<GameProgressManager>.Instance;
			if (instance == null || instance.RestaurantManager == null)
			{
				Singleton<DelayHelper>.Instance.DelayAction(0.1f, () =>
				{
					GameProgressManager instance2 = Singleton<GameProgressManager>.Instance;
					if (instance2 != null && instance2.RestaurantManager != null)
					{
						instance2.RestaurantManager.IsRestaurantOpen = isOpen;
					}
				});
			}
			else
			{
				instance.RestaurantManager.IsRestaurantOpen = isOpen;
			}
		}

		private bool CanOpenByTime()
		{
			GameProgressManager instance = Singleton<GameProgressManager>.Instance;
			if (instance == null || instance.CozyWeatherManager == null)
			{
				return true;
			}
			MeridiemTime currentTime = instance.CozyWeatherManager.CurrentTime;
			int num = currentTime.hours * 60 + currentTime.minutes;
			int num2 = _shopOpenHour * 60;
			int num3 = _shopCloseHour * 60;
			if (num >= num2)
			{
				return num < num3;
			}
			return false;
		}

		protected override void __initializeVariables()
		{
			if (_isOpen == null)
			{
				throw new Exception("OpenCloseSign._isOpen cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isOpen.Initialize(this);
			__nameNetworkVariable(_isOpen, "_isOpen");
			NetworkVariableFields.Add(_isOpen);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(2108185636u, __rpc_handler_2108185636, "ServerUpdateSignRpc", RpcInvokePermission.Everyone);
			__registerRpc(3206944577u, __rpc_handler_3206944577, "ResumeTimeForEveryoneRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_2108185636(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((OpenCloseSign)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((OpenCloseSign)target).ServerUpdateSignRpc();
				((OpenCloseSign)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3206944577(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((OpenCloseSign)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((OpenCloseSign)target).ResumeTimeForEveryoneRpc();
				((OpenCloseSign)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "OpenCloseSign";
		}
	}
}
