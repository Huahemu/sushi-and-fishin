using System;
using EPOOutline;
using PaperPixelGames.Core.Economy;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityServiceLocator;

namespace PaperPixelGames.Core.ScrapSystem
{
	public class WorldPickup : NetworkBehaviour, IBasicInteractable
	{
		private enum PickupInitializationMode
		{
			RuntimeGenerated = 0,
			SceneAuthored = 1
		}

		[Header("References")]
		[SerializeField]
		private Outlinable _outlinable;

		[SerializeField]
		private Transform _modelRoot;

		[Header("Scene Authored Setup")]
		[SerializeField]
		private PickupInitializationMode _initializationMode;

		[SerializeField]
		private PickupType _scenePickupType = PickupType.Scrap;

		[SerializeField]
		private int _sceneAmount = 1;

		[SerializeField]
		private int _sceneModelIndex;

		[Header("Settings")]
		[SerializeField]
		private int _minScrapAmount = 2;

		[SerializeField]
		private int _maxScrapAmount = 3;

		[SerializeField]
		private int _minCoinAmount = 1;

		[SerializeField]
		private int _maxCoinAmount = 3;

		private GameObject _spawnedModel;

		private readonly NetworkVariable<byte> _modelIndex = new NetworkVariable<byte>(0);

		private readonly NetworkVariable<byte> _amount = new NetworkVariable<byte>(0);

		private readonly NetworkVariable<byte> _pickupType = new NetworkVariable<byte>(0);

		private bool _consumedServer;

		private bool _serverStateInitialized;

		public bool CanGaze { get; set; }

		public bool CanInteractable { get; set; }

		private bool IsSceneAuthored()
		{
			return _initializationMode == PickupInitializationMode.SceneAuthored;
		}

		private bool IsRuntimeAuthored()
		{
			return _initializationMode == PickupInitializationMode.RuntimeGenerated;
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			CanGaze = true;
			CanInteractable = true;
			NetworkVariable<byte> modelIndex = _modelIndex;
			modelIndex.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Combine(modelIndex.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnPickupStateChanged));
			NetworkVariable<byte> amount = _amount;
			amount.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Combine(amount.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnPickupStateChanged));
			NetworkVariable<byte> pickupType = _pickupType;
			pickupType.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Combine(pickupType.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnPickupStateChanged));
			if (base.IsServer)
			{
				EnsureServerStateInitialized();
			}
			TrySpawnFromState();
		}

		public override void OnNetworkDespawn()
		{
			base.OnNetworkDespawn();
			NetworkVariable<byte> modelIndex = _modelIndex;
			modelIndex.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Remove(modelIndex.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnPickupStateChanged));
			NetworkVariable<byte> amount = _amount;
			amount.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Remove(amount.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnPickupStateChanged));
			NetworkVariable<byte> pickupType = _pickupType;
			pickupType.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Remove(pickupType.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnPickupStateChanged));
			SafeClearHintAndHighlight();
			ClearSpawnedModel();
		}

		private void EnsureServerStateInitialized()
		{
			if (base.IsServer && !_serverStateInitialized)
			{
				if (_initializationMode == PickupInitializationMode.SceneAuthored)
				{
					ApplySceneAuthoredState();
					_serverStateInitialized = true;
				}
				else if (_amount.Value > 0)
				{
					_serverStateInitialized = true;
				}
				else
				{
					UtilityMethods.Log("[WorldPickup] <color=yellow>State not initialized</color> RuntimeGenerated pickup requires Setup(PickupType) call. Object: " + base.name, LogType.Warning);
				}
			}
		}

		public void Setup(PickupType type)
		{
			if (base.IsServer)
			{
				GeneratePickupState(type, out var modelIndex, out var amount);
				if (amount <= 0)
				{
					UtilityMethods.Log($"[WorldPickup] <color=red>Setup failed</color> Generated amount is 0. Type: {type}", LogType.Warning);
					return;
				}
				_pickupType.Value = (byte)type;
				_modelIndex.Value = modelIndex;
				_amount.Value = amount;
				_serverStateInitialized = true;
				TrySpawnFromState();
			}
		}

		private void ApplySceneAuthoredState()
		{
			byte b = (byte)Mathf.Clamp(_sceneAmount, 1, 255);
			byte b2 = (byte)Mathf.Clamp(_sceneModelIndex, 0, 255);
			if (TryResolvePickupHandler(out var pickupHandler))
			{
				bool flag = b == 1;
				int modelCount = pickupHandler.GetModelCount(_scenePickupType, flag);
				if (modelCount > 0)
				{
					b2 = (byte)Mathf.Clamp(_sceneModelIndex, 0, modelCount - 1);
				}
				else
				{
					UtilityMethods.Log("[WorldPickup] <color=yellow>Scene setup warning</color> " + $"No model found for Type: {_scenePickupType}, IsSingle: {flag}. Object: {base.name}", LogType.Warning);
				}
			}
			else
			{
				UtilityMethods.Log("[WorldPickup] <color=yellow>Scene setup warning</color> WorldPickupHandler not found while applying scene-authored state. Object: " + base.name, LogType.Warning);
			}
			_pickupType.Value = (byte)_scenePickupType;
			_modelIndex.Value = b2;
			_amount.Value = b;
			UtilityMethods.Log("[WorldPickup] Scene-authored state applied. " + $"Object: {base.name}, Type: {_scenePickupType}, Amount: {b}, ModelIndex: {b2}");
			TrySpawnFromState();
		}

		private void GeneratePickupState(PickupType pickupType, out byte modelIndex, out byte amount)
		{
			modelIndex = 0;
			amount = 0;
			ServiceLocator.ForSceneOf(this).Get<WorldPickupHandler>(out var service);
			if (service == null)
			{
				UtilityMethods.Log("[WorldPickup] <color=red>Spawn failed</color> WorldPickupHandler not found!");
				return;
			}
			bool flag = UnityEngine.Random.value > 0.5f;
			if (!service.TryGetRandomModelIndex(pickupType, flag, out modelIndex))
			{
				UtilityMethods.Log($"[WorldPickup] <color=red>Spawn failed</color> No model found for pickup type: {pickupType}, IsSingle: {flag}");
			}
			else
			{
				amount = (byte)(flag ? 1 : ((byte)GetRandomGroupAmount(pickupType)));
			}
		}

		private int GetRandomGroupAmount(PickupType pickupType)
		{
			int minInclusive;
			int num;
			if (pickupType == PickupType.Coin)
			{
				minInclusive = Mathf.Min(_minCoinAmount, _maxCoinAmount);
				num = Mathf.Max(_minCoinAmount, _maxCoinAmount);
			}
			else
			{
				minInclusive = Mathf.Min(_minScrapAmount, _maxScrapAmount);
				num = Mathf.Max(_minScrapAmount, _maxScrapAmount);
			}
			return UnityEngine.Random.Range(minInclusive, num + 1);
		}

		public void GazeEnter()
		{
			Singleton<HintsManager>.Instance.AddHint("Interact", "key_take");
			if (_outlinable != null)
			{
				Singleton<HighlightManager>.Instance.SetHighlight(_outlinable);
			}
		}

		public void GazeExit()
		{
			Singleton<HintsManager>.Instance.RemoveHint("Interact");
			Singleton<HighlightManager>.Instance.ClearCurrentHighlight();
		}

		public void Interact(Interactor interactor, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started && CanInteractable && !(interactor == null))
			{
				ServerRequestPickupRpc(interactor.OwnerClientId);
			}
		}

		public void TriggerInput(Interactor interactor, InputKind inputKind, InputActionPhase phase)
		{
			if (CanInteractable && phase == InputActionPhase.Started && inputKind == InputKind.Interact)
			{
				Interact(interactor, phase);
			}
		}

		public void TriggerCrossInput(Interactor interactor, BaseHoldable held, InputKind inputKind, InputActionPhase phase)
		{
			if (CanInteractable && phase == InputActionPhase.Started && inputKind == InputKind.Interact)
			{
				Interact(interactor, phase);
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerRequestPickupRpc(ulong senderID)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3534983315u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, senderID);
				__endSendRpc(ref bufferWriter, 3534983315u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (_consumedServer)
			{
				return;
			}
			EnsureServerStateInitialized();
			if (_amount.Value <= 0)
			{
				UtilityMethods.Log("[WorldPickup] <color=red>Pickup failed</color> Amount is 0. " + $"Object: {base.name}, Mode: {_initializationMode}, SceneAmount: {_sceneAmount}, SceneType: {_scenePickupType}", LogType.Warning);
				return;
			}
			ServiceLocator.ForSceneOf(this).Get<EconomyHandler>(out var service);
			if (service == null)
			{
				UtilityMethods.Log("[WorldPickup] <color=red>Pickup failed</color> EconomyHandler not found!");
				return;
			}
			_consumedServer = true;
			ApplyPickupReward(service, senderID);
			base.NetworkObject.Despawn();
		}

		private void ApplyPickupReward(EconomyHandler economyHandler, ulong senderID)
		{
			PickupType value = (PickupType)_pickupType.Value;
			switch (value)
			{
			case PickupType.Scrap:
				economyHandler.EarnScrap(_amount.Value, senderID);
				break;
			case PickupType.Coin:
				economyHandler.EarnMoney(_amount.Value, senderID);
				break;
			default:
				UtilityMethods.Log($"[WorldPickup] <color=red>Reward failed</color> Unknown pickup type: {value}");
				break;
			}
		}

		private void OnPickupStateChanged(byte previousValue, byte newValue)
		{
			TrySpawnFromState();
		}

		private void TrySpawnFromState()
		{
			if ((!Application.isPlaying || _amount.Value > 0) && (Application.isPlaying || GetEffectiveAmount() > 0))
			{
				SpawnPickupVisual();
			}
		}

		private void SpawnPickupVisual()
		{
			GameObject gameObject = ResolvePickupVisualPrefab();
			if (gameObject == null)
			{
				UtilityMethods.Log("[WorldPickup] <color=red>Spawn failed</color> Pickup visual prefab not found!");
				return;
			}
			ClearSpawnedModel();
			Transform parent = ((_modelRoot != null) ? _modelRoot : base.transform);
			_spawnedModel = UnityEngine.Object.Instantiate(gameObject, parent);
			_spawnedModel.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			_spawnedModel.transform.localScale = Vector3.one;
			RegisterRenderersToOutline(_spawnedModel);
		}

		private GameObject ResolvePickupVisualPrefab()
		{
			if (!TryResolvePickupHandler(out var pickupHandler))
			{
				return null;
			}
			PickupType effectivePickupType = GetEffectivePickupType();
			int effectiveAmount = GetEffectiveAmount();
			int effectiveModelIndex = GetEffectiveModelIndex();
			bool isSingle = effectiveAmount == 1;
			return pickupHandler.GetPickupModel(effectivePickupType, isSingle, effectiveModelIndex);
		}

		private PickupType GetEffectivePickupType()
		{
			if (!Application.isPlaying && _initializationMode == PickupInitializationMode.SceneAuthored)
			{
				return _scenePickupType;
			}
			return (PickupType)_pickupType.Value;
		}

		private int GetEffectiveAmount()
		{
			if (!Application.isPlaying && _initializationMode == PickupInitializationMode.SceneAuthored)
			{
				return Mathf.Clamp(_sceneAmount, 1, 255);
			}
			return _amount.Value;
		}

		private int GetEffectiveModelIndex()
		{
			if (!Application.isPlaying && _initializationMode == PickupInitializationMode.SceneAuthored)
			{
				return Mathf.Clamp(_sceneModelIndex, 0, 255);
			}
			return _modelIndex.Value;
		}

		private bool TryResolvePickupHandler(out WorldPickupHandler pickupHandler)
		{
			pickupHandler = Singleton<GameProgressManager>.Instance.WorldPickupHandler;
			return pickupHandler != null;
		}

		private void ClearSpawnedModel()
		{
			ClearOutlinableTargets();
			if (_modelRoot != null)
			{
				for (int num = _modelRoot.childCount - 1; num >= 0; num--)
				{
					GameObject obj = _modelRoot.GetChild(num).gameObject;
					if (Application.isPlaying)
					{
						UnityEngine.Object.Destroy(obj);
					}
					else
					{
						UnityEngine.Object.DestroyImmediate(obj);
					}
				}
				_spawnedModel = null;
			}
			else if (!(_spawnedModel == null))
			{
				if (Application.isPlaying)
				{
					UnityEngine.Object.Destroy(_spawnedModel);
				}
				else
				{
					UnityEngine.Object.DestroyImmediate(_spawnedModel);
				}
				_spawnedModel = null;
			}
		}

		private void ClearOutlinableTargets()
		{
			if (!(_outlinable == null))
			{
				while (_outlinable.OutlineTargetsCount > 0)
				{
					_outlinable.RemoveTarget(_outlinable[0]);
				}
			}
		}

		private void RegisterRenderersToOutline(GameObject model)
		{
			if (_outlinable == null || model == null)
			{
				return;
			}
			Renderer[] componentsInChildren = model.GetComponentsInChildren<Renderer>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				if (componentsInChildren[i] != null)
				{
					_outlinable.AddRenderer(componentsInChildren[i]);
				}
			}
		}

		private void SafeClearHintAndHighlight()
		{
			if (Singleton<HintsManager>.Instance != null)
			{
				Singleton<HintsManager>.Instance.RemoveHint("Interact");
			}
			if (Singleton<HighlightManager>.Instance != null)
			{
				Singleton<HighlightManager>.Instance.ClearCurrentHighlight();
			}
		}

		protected override void __initializeVariables()
		{
			if (_modelIndex == null)
			{
				throw new Exception("WorldPickup._modelIndex cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_modelIndex.Initialize(this);
			__nameNetworkVariable(_modelIndex, "_modelIndex");
			NetworkVariableFields.Add(_modelIndex);
			if (_amount == null)
			{
				throw new Exception("WorldPickup._amount cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_amount.Initialize(this);
			__nameNetworkVariable(_amount, "_amount");
			NetworkVariableFields.Add(_amount);
			if (_pickupType == null)
			{
				throw new Exception("WorldPickup._pickupType cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_pickupType.Initialize(this);
			__nameNetworkVariable(_pickupType, "_pickupType");
			NetworkVariableFields.Add(_pickupType);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(3534983315u, __rpc_handler_3534983315, "ServerRequestPickupRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_3534983315(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				((WorldPickup)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((WorldPickup)target).ServerRequestPickupRpc(value);
				((WorldPickup)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "WorldPickup";
		}
	}
}
