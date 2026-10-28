using System;
using System.Collections.Generic;
using DG.Tweening;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Kitchen.ChefDesk.ChefDeskUIHandler;
using PaperPixelGames.Core.Kitchen.GastronormPanIngredients;
using PaperPixelGames.Core.Kitchen.PreparationStation.GastronomPan;
using PaperPixelGames.Core.Kitchen.Wholasale;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Core.ProcessedIngredient;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using Unity.Collections;
using Unity.Netcode;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Kitchen.ChefDesk
{
	public class ChefDeskGastroPan : BaseInteractable, IHeldItemMouse1Interactable, ISaveable<ChefDeskGastropanData>
	{
		private const string RAW_PREPARATION_MESSAGE = "这是待加工原料，请先在备料台切片或切条，再用料盘转入主厨台。";
		[SerializeField]
		private ChefDeskGastropanInteractor _gastropanInteractor;

		[SerializeField]
		private ChefDesk _chefDesk;

		[SerializeField]
		private GameObject _graphic;

		private float _chefDeskIngredientRotationX = 20f;

		private PanIngredientGraphics _panIngredientGraphics;

		private NetworkVariable<int> _maxCapacity = new NetworkVariable<int>(0);

		private NetworkVariable<byte> _ingredientType = new NetworkVariable<byte>(0);

		private NetworkVariable<byte> _ingredientCount = new NetworkVariable<byte>(0);

		private NetworkVariable<FixedString64Bytes> _ingredientID = new NetworkVariable<FixedString64Bytes>();

		private readonly NetworkVariable<FixedString64Bytes> _lastEmptiedPlacementIngredientId = new NetworkVariable<FixedString64Bytes>();

		private Transform _target;

		private string _spawnedGraphicIngredientId = string.Empty;

		private bool _staffVisualDirty;

		private readonly Dictionary<string, PanIngredientGraphics> _cachedGraphics = new Dictionary<string, PanIngredientGraphics>();

		private static readonly ProfilerMarker VisualMarker = new ProfilerMarker("Staff.Chef.RefreshPanVisual");

		public static readonly HashSet<ChefDeskGastroPan> StaffStockSources = new HashSet<ChefDeskGastroPan>();

		public GameObject Graphic => _graphic;

		public ChefDesk ChefDesk => _chefDesk;

		public FixedString64Bytes CurrentIngredientID => _ingredientID.Value;

		public byte IngredientCount => _ingredientCount.Value;

		internal FixedString64Bytes LastEmptiedPlacementIngredientId => _lastEmptiedPlacementIngredientId.Value;

		public ChefDeskGastropanInteractor GastropanInteractor => _gastropanInteractor;

		public byte Index { get; private set; }

		public event Action<ChefDeskGastroPan> ClientIngredientStateChanged;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ClearStaffStockSources()
		{
			StaffStockSources.Clear();
		}

		public override void OnNetworkSpawn()
		{
			if (base.IsServer)
			{
				StaffStockSources.Add(this);
				_lastEmptiedPlacementIngredientId.Value = default;
			}
			base.OnNetworkSpawn();
			NetworkVariable<FixedString64Bytes> ingredientID = _ingredientID;
			ingredientID.OnValueChanged = (NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate)Delegate.Combine(ingredientID.OnValueChanged, new NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate(OnIngredientIdChanged));
			NetworkVariable<byte> ingredientCount = _ingredientCount;
			ingredientCount.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Combine(ingredientCount.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnIngredientCountChanged));
			if (_chefDesk.StaffEmployeeId != 0)
			{
				_staffVisualDirty = true;
				return;
			}
			ClientRefreshPanFromNetworkState();
			ClientIngredientStateChanged?.Invoke(this);
		}

		public override void OnNetworkDespawn()
		{
			_staffVisualDirty = false;
			ClientClearPanVisualAndTooltip();
			foreach (PanIngredientGraphics value in _cachedGraphics.Values)
			{
				if (value != null)
				{
					UnityEngine.Object.Destroy(value.gameObject);
				}
			}
			_cachedGraphics.Clear();
			StaffStockSources.Remove(this);
			NetworkVariable<FixedString64Bytes> ingredientID = _ingredientID;
			ingredientID.OnValueChanged = (NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate)Delegate.Remove(ingredientID.OnValueChanged, new NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate(OnIngredientIdChanged));
			NetworkVariable<byte> ingredientCount = _ingredientCount;
			ingredientCount.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Remove(ingredientCount.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnIngredientCountChanged));
			base.OnNetworkDespawn();
		}

		private void OnIngredientIdChanged(FixedString64Bytes previousValue, FixedString64Bytes newValue)
		{
			if (_chefDesk.StaffEmployeeId != 0)
			{
				_staffVisualDirty = true;
				return;
			}
			ClientRefreshPanFromNetworkState();
			ClientIngredientStateChanged?.Invoke(this);
		}

		private void OnIngredientCountChanged(byte previousValue, byte newValue)
		{
			if (_chefDesk.StaffEmployeeId != 0)
			{
				_staffVisualDirty = true;
				return;
			}
			ClientRefreshPanFromNetworkState();
			ClientIngredientStateChanged?.Invoke(this);
		}

		private void LateUpdate()
		{
			if (_staffVisualDirty && base.IsSpawned)
			{
				string text = _ingredientID.Value.ToString();
				if (_ingredientCount.Value <= 0 || !IsValidIngredientId(text) || (!(_panIngredientGraphics == null) && !(_spawnedGraphicIngredientId != text)) || PanIngredientGraphics.TryBeginDeferredBuild())
				{
					_staffVisualDirty = false;
					ClientRefreshPanFromNetworkState();
					ClientIngredientStateChanged?.Invoke(this);
				}
			}
		}

		private void ClientRefreshPanFromNetworkState()
		{
			using (VisualMarker.Auto())
			{
				string text = _ingredientID.Value.ToString();
				byte value = _ingredientCount.Value;
				if (!IsValidIngredientId(text) || value == 0)
				{
					ClientClearPanVisualAndTooltip();
					return;
				}
				ClientSpawnGraphicLocal(_ingredientID.Value, value);
				_gastropanInteractor.ClientSetToolTipData(text, value);
			}
		}

		private void ClientClearPanVisualAndTooltip()
		{
			if (_panIngredientGraphics != null)
			{
				_panIngredientGraphics.ResetAll();
				PanIngredientGraphics panIngredientGraphics = _panIngredientGraphics;
				_panIngredientGraphics = null;
				panIngredientGraphics.gameObject.SetActive(value: false);
				if (!string.IsNullOrEmpty(_spawnedGraphicIngredientId))
				{
					if (_cachedGraphics.Count >= 2)
					{
						string key = null;
						using (Dictionary<string, PanIngredientGraphics>.KeyCollection.Enumerator enumerator = _cachedGraphics.Keys.GetEnumerator())
						{
							if (enumerator.MoveNext())
							{
								key = enumerator.Current;
							}
						}
						if (_cachedGraphics[key] != null)
						{
							UnityEngine.Object.Destroy(_cachedGraphics[key].gameObject);
						}
						_cachedGraphics.Remove(key);
					}
					_cachedGraphics[_spawnedGraphicIngredientId] = panIngredientGraphics;
				}
				else
				{
					UnityEngine.Object.Destroy(panIngredientGraphics.gameObject);
				}
			}
			_spawnedGraphicIngredientId = string.Empty;
			_gastropanInteractor.ClientClearToolTipData();
		}

		public void ClientInitialize(byte index)
		{
			Index = index;
			_gastropanInteractor.ClientInit(this);
		}

		private void ClientSpawnGraphicLocal(FixedString64Bytes processedIngredientID, byte totalInPan)
		{
			string text = processedIngredientID.ToString();
			if (!IsValidIngredientId(text))
			{
				ClientClearPanVisualAndTooltip();
				return;
			}
			if (_panIngredientGraphics != null && _spawnedGraphicIngredientId != text)
			{
				ClientClearPanVisualAndTooltip();
			}
			if (_panIngredientGraphics == null && _cachedGraphics.TryGetValue(text, out var value) && value != null)
			{
				_cachedGraphics.Remove(text);
				_panIngredientGraphics = value;
				_spawnedGraphicIngredientId = text;
				value.gameObject.SetActive(value: true);
			}
			if (_panIngredientGraphics == null && _chefDesk.ItemDatabaseSO.FindById(text) is ProcessedIngredientSO processedSO)
			{
				_panIngredientGraphics = ClientSpawnPanIngredientGraphics(processedSO, _graphic.transform);
				_spawnedGraphicIngredientId = text;
			}
			if (!(_panIngredientGraphics == null))
			{
				_panIngredientGraphics.SyncToCount(totalInPan);
			}
		}

		private PanIngredientGraphics ClientSpawnPanIngredientGraphics(ProcessedIngredientSO processedSO, Transform parent)
		{
			if (_panIngredientGraphics != null)
			{
				return null;
			}
			GameObject obj = UnityEngine.Object.Instantiate(processedSO.PanIngredientPrefab, parent);
			obj.transform.localPosition = Vector3.zero;
			obj.transform.localRotation = Quaternion.Euler(_chefDeskIngredientRotationX, 0f, 0f);
			PanIngredientGraphics component = obj.GetComponent<PanIngredientGraphics>();
			if (component == null)
			{
				UtilityMethods.Log("[ChefDeskGastroPan -  ClientSpawnPanIngredientGraphics] Prefab üzerinde PanIngredientGraphics eksik! Prefab'i kontrol et.");
				return null;
			}
			return component;
		}

		public bool ServerTryConsumeForPlacement()
		{
			if (_chefDesk.StaffLocked)
			{
				return false;
			}
			if (!base.IsServer || _ingredientCount.Value == 0)
			{
				return false;
			}
			FixedString64Bytes value = _ingredientID.Value;
			if (!IsValidIngredientId(value.ToString()))
			{
				UtilityMethods.Log("[ChefDeskGastroPan - ServerTryConsumeForPlacement] Ingredient ID is invalid.", LogType.Error);
				return false;
			}
			byte b = (byte)(_ingredientCount.Value - 1);
			ClientPlaceProductOnTableRpc(value, b);
			if (b == 0)
			{
				ServerResetPan();
				_lastEmptiedPlacementIngredientId.Value = value;
			}
			else
			{
				_ingredientCount.Value = b;
				ClientToolTipIngredientCountUpdateRpc(b);
			}
			return true;
		}

		[Rpc(SendTo.Everyone)]
		private void ClientPlaceProductOnTableRpc(FixedString64Bytes ingredientId, byte remainingCount)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3710719767u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in ingredientId, default(FastBufferWriter.ForFixedStrings));
				bufferWriter.WriteValueSafe(in remainingCount, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3710719767u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!IsValidIngredientId(ingredientId.ToString()))
			{
				UtilityMethods.Log("[ChefDeskGastroPan - ClientPlaceProductOnTableRpc] Ingredient ID is invalid.", LogType.Error);
				return;
			}
			GameObject gameObject = null;
			if (_panIngredientGraphics != null)
			{
				List<GameObject> ingredientObjects = _panIngredientGraphics.IngredientObjects;
				if (ingredientObjects != null)
				{
					GameObject gameObject2 = null;
					for (int num = ingredientObjects.Count - 1; num >= 0; num--)
					{
						GameObject gameObject3 = ingredientObjects[num];
						if (!(gameObject3 == null))
						{
							if ((object)gameObject2 == null)
							{
								gameObject2 = gameObject3;
							}
							if (gameObject3.activeInHierarchy)
							{
								gameObject = gameObject3;
								break;
							}
						}
					}
					if ((object)gameObject == null)
					{
						gameObject = gameObject2;
					}
				}
			}
			SushiType currentSushiType = _chefDesk.CurrentSushiType;
			GameObject gameObject4 = CreateProductClone(gameObject, ingredientId);
			if (gameObject4 == null)
			{
				UtilityMethods.Log("<color=red>[ChefDeskGastroPan - ClientPlaceProductOnTableRpc] Product clone could not be created.</color>");
				return;
			}
			_target = null;
			switch (currentSushiType)
			{
			case SushiType.Maki:
				HandleMakiStep(gameObject4, ingredientId);
				break;
			case SushiType.Uramaki:
				HandleUramakiStep(gameObject4, ingredientId);
				break;
			case SushiType.Nigiri:
				gameObject4.SetActive(value: true);
				HandleNigiriStep(gameObject4, ingredientId);
				break;
			case SushiType.Temaki:
				HandleTemakiStep(gameObject4, ingredientId);
				break;
			case SushiType.Sashimi:
				HandleSashimiStep(gameObject4);
				break;
			default:
				UnityEngine.Object.Destroy(gameObject4);
				return;
			}
			if (!(_target == null))
			{
				_panIngredientGraphics?.SyncToCount(remainingCount);
				ClientMoveObjectTo(gameObject4, _target, placeOnTable: true, currentSushiType, ingredientId);
				_chefDesk.ObjectsOnTable.Add(new TableObjectData(ingredientId, gameObject4));
			}
		}

		[Rpc(SendTo.Everyone)]
		public void ClientRetrieveProductFromTableRpc(FixedString64Bytes ingredientId, byte restoredCount, int placementIndex)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1714848277u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in ingredientId, default(FastBufferWriter.ForFixedStrings));
				bufferWriter.WriteValueSafe(in restoredCount, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(bufferWriter, placementIndex);
				__endSendRpc(ref bufferWriter, 1714848277u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (_chefDesk.ObjectsOnTable.Count == 0)
			{
				UtilityMethods.Log("[ChefDeskGastroPan - ClientRetrieveProductFromTableRpc] Masada ürün yok.");
				_panIngredientGraphics?.SyncToCount(restoredCount);
				return;
			}
			int num = _chefDesk.ObjectsOnTable.Count - 1;
			int num2 = ((placementIndex >= 0) ? placementIndex : num);
			if (num2 > num)
			{
				UtilityMethods.Log("[ChefDeskGastroPan - ClientRetrieveProductFromTableRpc] Placement index is outside table visuals.", LogType.Warning);
				_panIngredientGraphics?.SyncToCount(restoredCount);
				return;
			}
			TableObjectData tableObjectData = _chefDesk.ObjectsOnTable[num2];
			if (tableObjectData.IngredientId != ingredientId)
			{
				UtilityMethods.Log("[ChefDeskGastroPan - ClientRetrieveProductFromTableRpc] Top ingredient visual does not match server state.", LogType.Warning);
				_panIngredientGraphics?.SyncToCount(restoredCount);
				return;
			}
			_chefDesk.ObjectsOnTable.RemoveAt(num2);
			if (ingredientId != ILSpyHelper_AsRefReadOnly((FixedString32Bytes)_chefDesk.SingleNoriSO_ID) && ingredientId != ILSpyHelper_AsRefReadOnly((FixedString32Bytes)_chefDesk.CookedRiceSO_ID))
			{
				if (placementIndex >= 0)
				{
					_chefDesk.ClientCompactAssemblyIngredientAnchors();
				}
				else
				{
					_chefDesk.ClientReleaseLastAssemblyIngredientAnchor();
				}
			}
			ChefDesk.SushiVisualSet value;
			if (tableObjectData.Obj != null)
			{
				ClientMoveObjectTo(tableObjectData.Obj, _graphic.transform, placeOnTable: false, _chefDesk.CurrentSushiType, ingredientId);
			}
			else if ((ingredientId == ILSpyHelper_AsRefReadOnly((FixedString32Bytes)_chefDesk.SingleNoriSO_ID) || ingredientId == ILSpyHelper_AsRefReadOnly((FixedString32Bytes)_chefDesk.CookedRiceSO_ID)) && _chefDesk.VisualMap != null && _chefDesk.VisualMap.TryGetValue(_chefDesk.CurrentSushiType, out value) && value != null)
			{
				GameObject gameObject = ((ingredientId == ILSpyHelper_AsRefReadOnly((FixedString32Bytes)_chefDesk.SingleNoriSO_ID)) ? value.SingleNoriOBJ : value.SushiRiceOBJ);
				if (gameObject != null)
				{
					gameObject.SetActive(value: false);
				}
			}
			_panIngredientGraphics?.SyncToCount(restoredCount);
			static ref readonly T ILSpyHelper_AsRefReadOnly<T>(in T temp)
			{
				//ILSpy generated this function to help ensure overload resolution can pick the overload using 'in'
				return ref temp;
			}
		}

		private void ClientMoveObjectTo(GameObject obj, Transform target, bool placeOnTable, SushiType sushiType, FixedString64Bytes ingredientId, float duration = 0.3f)
		{
			if (!placeOnTable)
			{
				ChefDesk.SushiVisualSet value = null;
				_chefDesk.VisualMap?.TryGetValue(sushiType, out value);
				if (ingredientId == ILSpyHelper_AsRefReadOnly((FixedString32Bytes)_chefDesk.CookedRiceSO_ID))
				{
					if (value != null && value.SushiRiceOBJ != null)
					{
						value.SushiRiceOBJ.SetActive(value: false);
					}
					obj.SetActive(value: true);
				}
				if (ingredientId == ILSpyHelper_AsRefReadOnly((FixedString32Bytes)_chefDesk.SingleNoriSO_ID))
				{
					if (value != null && value.SingleNoriOBJ != null)
					{
						value.SingleNoriOBJ.SetActive(value: false);
					}
					obj.SetActive(value: true);
				}
				obj.transform.SetParent(null);
			}
			Vector3 endValue = obj.transform.position + Vector3.up * 0.25f;
			Sequence s = DOTween.Sequence();
			float duration2 = duration * 0.5f;
			s.Append(obj.transform.DOMove(endValue, duration2).SetEase(Ease.OutSine)).Append(obj.transform.DOMove(target.position, duration2).SetEase(Ease.InSine)).OnComplete(() =>
			{
				UtilityMethods.Log("[ChefDeskGastroPan - ClientMoveObjectTo] Tween tamamlandı");
				ChefDesk.SushiVisualSet value2 = null;
				_chefDesk.VisualMap?.TryGetValue(sushiType, out value2);
				if (ingredientId == ILSpyHelper_AsRefReadOnly((FixedString32Bytes)_chefDesk.CookedRiceSO_ID) && placeOnTable)
				{
					if (_chefDesk.CurrentSushiType == SushiType.Nigiri)
					{
						_chefDesk.PlayChefDeskSfxForActiveUser(ChefDesk.ChefDeskSfxId.PlaceIngredientOnCounter);
						return;
					}
					obj.SetActive(value: false);
					if (value2 != null && value2.SushiRiceOBJ != null)
					{
						value2.SushiRiceOBJ.SetActive(value: true);
					}
				}
				if (ingredientId == ILSpyHelper_AsRefReadOnly((FixedString32Bytes)_chefDesk.SingleNoriSO_ID) && placeOnTable)
				{
					obj.SetActive(value: false);
					if (value2 != null && value2.SingleNoriOBJ != null)
					{
						value2.SingleNoriOBJ.SetActive(value: true);
					}
				}
				if (!placeOnTable)
				{
					UnityEngine.Object.Destroy(obj);
				}
				_chefDesk.PlayChefDeskSfxForActiveUser(ChefDesk.ChefDeskSfxId.PlaceIngredientOnCounter);
				static ref readonly T ILSpyHelper_AsRefReadOnly<T>(in T temp)
				{
					//ILSpy generated this function to help ensure overload resolution can pick the overload using 'in'
					return ref temp;
				}
			});
			obj.transform.DORotateQuaternion(target.rotation, duration).SetEase(Ease.InOutSine);
			static ref readonly T ILSpyHelper_AsRefReadOnly<T>(in T temp)
			{
				//ILSpy generated this function to help ensure overload resolution can pick the overload using 'in'
				return ref temp;
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerDataUpdatedRpc(bool increase)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2637171926u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in increase, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2637171926u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!_chefDesk.StaffLocked)
				{
					ServerDataUpdated(increase);
				}
			}
		}

		private void ServerDataUpdated(bool increase)
		{
			if (base.IsServer)
			{
				if (increase)
				{
					_ingredientCount.Value++;
				}
				else
				{
					_ingredientCount.Value = (byte)Mathf.Max(0, _ingredientCount.Value - 1);
				}
				if (_ingredientCount.Value == 0)
				{
					ServerResetPan();
				}
				ClientToolTipIngredientCountUpdateRpc(_ingredientCount.Value);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientToolTipIngredientCountUpdateRpc(byte ingredientCount)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1829607464u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in ingredientCount, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1829607464u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				_gastropanInteractor.ClientToolTipIngredientCountUpdate(ingredientCount);
			}
		}

		public void ServerResetPan()
		{
			_lastEmptiedPlacementIngredientId.Value = default;
			_ingredientCount.Value = 0;
			_maxCapacity.Value = 0;
			_ingredientType.Value = 0;
			_ingredientID.Value = default;
			ClientResetPanRpc();
		}

		public bool ServerDebugFillToCapacity(ProcessedIngredientSO processedIngredientSO)
		{
			if (!base.IsServer || processedIngredientSO == null || processedIngredientSO.MaxPanCapacity <= 0)
			{
				return false;
			}
			_ingredientID.Value = processedIngredientSO.Id;
			_ingredientType.Value = (byte)processedIngredientSO.IngredientType;
			_maxCapacity.Value = processedIngredientSO.MaxPanCapacity;
			_ingredientCount.Value = processedIngredientSO.MaxPanCapacity;
			ClientToolTipIngredientCountUpdateRpc(_ingredientCount.Value);
			return true;
		}

		public bool ServerTryRestoreIngredientFromTable(FixedString64Bytes ingredientId)
		{
			if (!base.IsServer)
			{
				return false;
			}
			string text = ingredientId.ToString();
			if (!IsValidIngredientId(text))
			{
				return false;
			}
			ProcessedIngredientSO processedIngredientSO = ResolveProcessedIngredientSOForLoad(text);
			if (processedIngredientSO == null)
			{
				return false;
			}
			bool num = _ingredientCount.Value == 0 || !IsValidIngredientId(_ingredientID.Value.ToString());
			bool panHasSameIngredient = _ingredientID.Value.ToString() == processedIngredientSO.Id;
			int maxPanCapacity = processedIngredientSO.MaxPanCapacity;
			int num2 = ((!num) ? _ingredientCount.Value : 0);
			if (!CanRestoreIngredientToPan(num, panHasSameIngredient, num2, maxPanCapacity))
			{
				return false;
			}
			_ingredientID.Value = processedIngredientSO.Id;
			_ingredientType.Value = (byte)processedIngredientSO.IngredientType;
			_maxCapacity.Value = maxPanCapacity;
			_ingredientCount.Value = (byte)(num2 + 1);
			ClientToolTipIngredientCountUpdateRpc(_ingredientCount.Value);
			return true;
		}

		private static bool CanRestoreIngredientToPan(bool panIsEmpty, bool panHasSameIngredient, int currentCount, int maxCapacity)
		{
			if ((panIsEmpty | panHasSameIngredient) && maxCapacity > 0 && currentCount >= 0)
			{
				return currentCount < maxCapacity;
			}
			return false;
		}

		[Rpc(SendTo.Everyone)]
		private void ClientResetPanRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(525621338u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 525621338u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ClientClearPanVisualAndTooltip();
			}
		}

		private GameObject CreateProductClone(GameObject sourceObj, FixedString64Bytes ingredientId)
		{
			Transform transform = ((sourceObj != null) ? sourceObj.transform : _graphic?.transform);
			if (transform == null)
			{
				UtilityMethods.Log("<color=red>[ChefDeskGastroPan - CreateProductClone] Placement origin is null.</color>");
				return null;
			}
			if (ingredientId == ILSpyHelper_AsRefReadOnly((FixedString32Bytes)_chefDesk.CookedRiceSO_ID))
			{
				GameObject cookedRiceTravelVisualPrefab = _chefDesk.CookedRiceTravelVisualPrefab;
				if (cookedRiceTravelVisualPrefab == null)
				{
					UtilityMethods.Log("<color=red>[ChefDeskGastroPan - CreateProductClone] CookedRiceTravelVisualPrefab is missing on ChefDesk.</color>");
					return null;
				}
				GameObject obj = UnityEngine.Object.Instantiate(cookedRiceTravelVisualPrefab, transform.position, transform.rotation);
				obj.SetActive(value: true);
				return obj;
			}
			ProcessedIngredientSO processedIngredient = ResolveProcessedIngredientSOForLoad(ingredientId.ToString());
			GameObject gameObject = _chefDesk.ResolveTableIngredientVisualPrefab(processedIngredient);
			if (gameObject != null)
			{
				GameObject obj2 = UnityEngine.Object.Instantiate(gameObject, transform.position, transform.rotation);
				obj2.SetActive(value: true);
				return obj2;
			}
			UtilityMethods.Log($"[ChefDesk Assembly] Canonical visual is missing. IngredientId={ingredientId}. " + "Falling back to the pan presentation visual.", LogType.Warning);
			if (sourceObj != null)
			{
				return UnityEngine.Object.Instantiate(sourceObj, transform.position, transform.rotation);
			}
			UtilityMethods.Log($"[ChefDesk Assembly] No fallback visual is available. IngredientId={ingredientId}.", LogType.Error);
			return null;
			static ref readonly T ILSpyHelper_AsRefReadOnly<T>(in T temp)
			{
				//ILSpy generated this function to help ensure overload resolution can pick the overload using 'in'
				return ref temp;
			}
		}

		private bool ValidateOriginalSlot(out Transform slot)
		{
			slot = null;
			List<GameObject> ingredientObjects = _panIngredientGraphics.IngredientObjects;
			if (ingredientObjects == null || ingredientObjects.Count == 0)
			{
				return false;
			}
			int num = -1;
			for (int i = 0; i < ingredientObjects.Count; i++)
			{
				if (ingredientObjects[i].activeSelf)
				{
					num = i;
				}
			}
			int num2 = num + 1;
			if (num2 >= 0 && num2 < ingredientObjects.Count)
			{
				slot = ingredientObjects[num2].transform;
				return true;
			}
			UtilityMethods.Log("<color=yellow>[ChefDeskGastroPan] Pan içindeki aktif objelerin bir sonraki pozisyonu bulunamadı!</color>");
			return false;
		}

		private void RemoveFromTables(GameObject obj)
		{
			_chefDesk.ObjectsOnTable.RemoveAll((TableObjectData o) => o.Obj == obj);
		}

		private void HandleMakiStep(GameObject objClone, FixedString64Bytes ingredientId)
		{
			ChefDesk.SushiVisualSet value = null;
			_chefDesk.VisualMap?.TryGetValue(SushiType.Maki, out value);
			FixedString64Bytes a = ingredientId;
			Transform target;
			if (value != null && a == ILSpyHelper_AsRefReadOnly((FixedString32Bytes)_chefDesk.SingleNoriSO_ID))
			{
				target = value.SingleNoriOBJ?.transform;
			}
			else
			{
				FixedString64Bytes a2 = a;
				target = ((value == null || !(a2 == ILSpyHelper_AsRefReadOnly((FixedString32Bytes)_chefDesk.CookedRiceSO_ID))) ? _chefDesk.GetNextTableSpawnPoint() : value.SushiRiceOBJ?.transform);
			}
			_target = target;
			if (_target == null)
			{
				UtilityMethods.Log("<color=red>[ChefDeskGastroPan] TableSpawnPoints veya özel objeler null!</color>");
				UnityEngine.Object.Destroy(objClone);
			}
			else if (ingredientId != ILSpyHelper_AsRefReadOnly((FixedString32Bytes)_chefDesk.SingleNoriSO_ID) && ingredientId != ILSpyHelper_AsRefReadOnly((FixedString32Bytes)_chefDesk.CookedRiceSO_ID))
			{
				objClone.transform.SetParent(_target);
			}
			static ref readonly T ILSpyHelper_AsRefReadOnly<T>(in T temp)
			{
				//ILSpy generated this function to help ensure overload resolution can pick the overload using 'in'
				return ref temp;
			}
		}

		private void HandleUramakiStep(GameObject objClone, FixedString64Bytes ingredientId)
		{
			ChefDesk.SushiVisualSet value = null;
			_chefDesk.VisualMap?.TryGetValue(SushiType.Uramaki, out value);
			FixedString64Bytes a = ingredientId;
			Transform target;
			if (value != null && a == ILSpyHelper_AsRefReadOnly((FixedString32Bytes)_chefDesk.SingleNoriSO_ID))
			{
				target = value.SingleNoriOBJ?.transform;
			}
			else
			{
				FixedString64Bytes a2 = a;
				target = ((value == null || !(a2 == ILSpyHelper_AsRefReadOnly((FixedString32Bytes)_chefDesk.CookedRiceSO_ID))) ? _chefDesk.GetNextTableSpawnPoint() : value.SushiRiceOBJ?.transform);
			}
			_target = target;
			if (_target == null)
			{
				UtilityMethods.Log("<color=red>[ChefDeskGastroPan] TableSpawnPoints veya özel objeler null!</color>");
				UnityEngine.Object.Destroy(objClone);
			}
			else if (ingredientId != ILSpyHelper_AsRefReadOnly((FixedString32Bytes)_chefDesk.SingleNoriSO_ID) && ingredientId != ILSpyHelper_AsRefReadOnly((FixedString32Bytes)_chefDesk.CookedRiceSO_ID))
			{
				objClone.transform.SetParent(_target);
			}
			static ref readonly T ILSpyHelper_AsRefReadOnly<T>(in T temp)
			{
				//ILSpy generated this function to help ensure overload resolution can pick the overload using 'in'
				return ref temp;
			}
		}

		private void HandleTemakiStep(GameObject objClone, FixedString64Bytes ingredientId)
		{
			ChefDesk.SushiVisualSet value = null;
			_chefDesk.VisualMap?.TryGetValue(SushiType.Temaki, out value);
			FixedString64Bytes a = ingredientId;
			Transform target;
			if (value != null && a == ILSpyHelper_AsRefReadOnly((FixedString32Bytes)_chefDesk.SingleNoriSO_ID))
			{
				target = value.SingleNoriOBJ?.transform;
			}
			else
			{
				FixedString64Bytes a2 = a;
				target = ((value == null || !(a2 == ILSpyHelper_AsRefReadOnly((FixedString32Bytes)_chefDesk.CookedRiceSO_ID))) ? _chefDesk.GetNextTemakiSpawnPoint() : value.SushiRiceOBJ?.transform);
			}
			_target = target;
			if (_target == null)
			{
				UtilityMethods.Log("<color=red>[ChefDeskGastroPan] TableSpawnPoints veya özel objeler null!</color>");
				UnityEngine.Object.Destroy(objClone);
			}
			else if (ingredientId != ILSpyHelper_AsRefReadOnly((FixedString32Bytes)_chefDesk.SingleNoriSO_ID) && ingredientId != ILSpyHelper_AsRefReadOnly((FixedString32Bytes)_chefDesk.CookedRiceSO_ID))
			{
				objClone.transform.SetParent(_target);
			}
			static ref readonly T ILSpyHelper_AsRefReadOnly<T>(in T temp)
			{
				//ILSpy generated this function to help ensure overload resolution can pick the overload using 'in'
				return ref temp;
			}
		}

		private void HandleNigiriStep(GameObject objClone, FixedString64Bytes ingredientId)
		{
			_target = _chefDesk.GetNextNigiriSpawnPoint(ingredientId.ToString());
			if (_target == null)
			{
				UtilityMethods.Log("<color=red>[ChefDeskGastroPan] Nigiri spawn noktası bulunamadı!</color>");
				UnityEngine.Object.Destroy(objClone);
			}
		}

		private void HandleSashimiStep(GameObject objClone)
		{
			_target = _chefDesk.GetNextSashimiSpawnPoint();
			if (_target == null)
			{
				UtilityMethods.Log("<color=red>[ChefDeskGastroPan] Sashimi spawn noktası bulunamadı!</color>");
				UnityEngine.Object.Destroy(objClone);
			}
		}

		public override void EnableGazeHints()
		{
			_gastropanInteractor.ChefDeskToolTip(value: true);
			if (!(Singleton<HintsManager>.Instance == null))
			{
				Singleton<HintsManager>.Instance.ClearAllHints();
				if (((Singleton<QuickSlotHandler>.Instance != null) ? Singleton<QuickSlotHandler>.Instance.SelectedItem : null) != null)
				{
					Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_add");
				}
				if (!_chefDesk.StaffLocked)
				{
					Singleton<HintsManager>.Instance.AddHint("RightMouseMechanic", "key_take");
				}
			}
		}

		public override void DisableGazeHints()
		{
			_gastropanInteractor.ChefDeskToolTip(value: false);
			if (!(Singleton<HintsManager>.Instance == null))
			{
				Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
				Singleton<HintsManager>.Instance.RemoveHint("RightMouseMechanic");
			}
		}

		public ChefDeskGastropanData Save()
		{
			return new ChefDeskGastropanData
			{
				ItemID = _ingredientID.Value.ToString(),
				Amount = _ingredientCount.Value,
				Index = Index
			};
		}

		public void Load(ChefDeskGastropanData data)
		{
			if (!base.IsServer || data == null)
			{
				return;
			}
			_lastEmptiedPlacementIngredientId.Value = default;
			if (!IsValidIngredientId(data.ItemID) || data.Amount == 0)
			{
				_ingredientID.Value = default;
				_ingredientCount.Value = 0;
				_ingredientType.Value = 0;
				_maxCapacity.Value = 0;
				if (base.IsSpawned)
				{
					ClientRefreshPanFromNetworkState();
				}
				return;
			}
			ProcessedIngredientSO processedIngredientSO = ResolveProcessedIngredientSOForLoad(data.ItemID);
			if (!(processedIngredientSO == null))
			{
				_ingredientID.Value = processedIngredientSO.Id;
				_ingredientCount.Value = data.Amount;
				_ingredientType.Value = (byte)processedIngredientSO.IngredientType;
				_maxCapacity.Value = processedIngredientSO.MaxPanCapacity;
				if (base.IsSpawned)
				{
					ClientRefreshPanFromNetworkState();
				}
			}
		}

		private ProcessedIngredientSO ResolveProcessedIngredientSOForLoad(string itemId)
		{
			ItemSO itemSO = _chefDesk.ItemDatabaseSO.FindById(itemId);
			if (itemSO == null)
			{
				return null;
			}
			if (itemSO is ProcessedIngredientSO result)
			{
				return result;
			}
			if (itemSO is IngredientSO ingredientSO)
			{
				if (ingredientSO.ProcessedIngredients == null || ingredientSO.ProcessedIngredients.Count == 0)
				{
					UtilityMethods.Log("[ChefDeskGastroPan - Load] IngredientSO " + ingredientSO.name + " has no ProcessedIngredients.");
					return null;
				}
				return ingredientSO.ProcessedIngredients[0];
			}
			return null;
		}

		private bool IsValidIngredientId(string s)
		{
			if (!string.IsNullOrEmpty(s))
			{
				return s != "0";
			}
			return false;
		}

		public bool ServerStaffSet(int employeeId, ProcessedIngredientSO ingredient, int count, Vector3 source, float seconds = 0f)
		{
			if (!base.IsServer || !base.IsSpawned || _chefDesk.StaffEmployeeId != employeeId || ingredient == null || count < 0 || count > ingredient.MaxPanCapacity)
			{
				return false;
			}
			_maxCapacity.Value = ingredient.MaxPanCapacity;
			_ingredientType.Value = (byte)ingredient.IngredientType;
			_ingredientID.Value = ingredient.Id;
			_ingredientCount.Value = (byte)count;
			if (seconds > 0f)
			{
				ClientStaffSwapRpc(source, seconds);
			}
			return true;
		}

		[Rpc(SendTo.Everyone)]
		private void ClientStaffSwapRpc(Vector3 source, float seconds)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2752863329u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in source);
				bufferWriter.WriteValueSafe(in seconds, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2752863329u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!(_graphic == null))
				{
					Vector3 localPosition = _graphic.transform.localPosition;
					_graphic.transform.DOKill(complete: true);
					_graphic.transform.position = source;
					_graphic.transform.DOLocalMove(localPosition, seconds).SetEase(Ease.OutQuad);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerHandleGastroPanRpc(NetworkObjectReference interactorRef, NetworkObjectReference gastroPanRef, RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcParams rpcParams2 = rpcParams;
				RpcAttribute.RpcAttributeParams attributeParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(2387557919u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in gastroPanRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 2387557919u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			BaseRpcTarget baseRpcTarget = base.RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp);
			if (!interactorRef.TryGetComponent<Interactor>(out var component))
			{
				_chefDesk.ClientShowNotifyRpc("Error", "notification.chef.action_failed", baseRpcTarget);
				UtilityMethods.Log("[ChefDeskGastroPan - ServerHandleGastroPan] interactor not found");
				return;
			}
			if (!gastroPanRef.TryGetComponent<GastroPan>(out var component2))
			{
				UtilityMethods.Log("[ChefDeskGastroPan - ServerHandleGastroPan] gastroPan not found");
				_chefDesk.ClientShowNotifyRpc("Error", "notification.chef.action_failed", baseRpcTarget);
				return;
			}
			if (!ServerValidateHeldTransfer(component, component2, rpcParams.Receive.SenderClientId))
			{
				_chefDesk.ClientShowNotifyRpc("Error", "notification.chef.action_failed", baseRpcTarget);
				return;
			}
			if (component2.IngredientCount <= 0)
			{
				UtilityMethods.Log("[ChefDeskGastroPan - ServerHandleGastroPan] pan empty.");
				_chefDesk.ClientShowNotifyRpc("Info", "notification.chef.held_gastropan_empty", baseRpcTarget);
				return;
			}
			EnsureCapacityInitialized(component2.MaxCapacity);
			if (IsPanFull())
			{
				_chefDesk.ClientShowNotifyRpc("Info", "notification.chef.gastropan_full", baseRpcTarget);
			}
			else
			{
				ServerApplyIngredientTransfer(component, component2.IngredientType, component2.IngredientId, null, component2, null, baseRpcTarget);
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerHandleIngredientEntityRpc(NetworkObjectReference interactorRef, NetworkObjectReference ingredientEntityRef, RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcParams rpcParams2 = rpcParams;
				RpcAttribute.RpcAttributeParams attributeParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(4071394203u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in ingredientEntityRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 4071394203u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			BaseRpcTarget baseRpcTarget = base.RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp);
			if (!interactorRef.TryGetComponent<Interactor>(out var component))
			{
				_chefDesk.ClientShowNotifyRpc("Error", "notification.chef.action_failed", baseRpcTarget);
				UtilityMethods.Log("[ChefDeskGastroPan - ServerHandleIngredientEntityRpc] interactor not found");
				return;
			}
			if (!ingredientEntityRef.TryGetComponent<IngredientEntity>(out var component2))
			{
				UtilityMethods.Log("[ChefDeskGastroPan - ServerHandleIngredientEntityRpc] ingredientEntityRef not found");
				_chefDesk.ClientShowNotifyRpc("Error", "notification.chef.action_failed", baseRpcTarget);
				return;
			}
			if (!ServerValidateHeldTransfer(component, component2, rpcParams.Receive.SenderClientId))
			{
				_chefDesk.ClientShowNotifyRpc("Error", "notification.chef.action_failed", baseRpcTarget);
				return;
			}
			ProcessedIngredientSO processedIngredientSO = ResolveProcessedIngredientSO(component2);
			if (!component2.IsPlaceableOnChefDesk)
			{
				_chefDesk.ClientShowNotifyRpc("Info", RAW_PREPARATION_MESSAGE, baseRpcTarget);
				return;
			}
			if (processedIngredientSO == null)
			{
				_chefDesk.ClientShowNotifyRpc("Error", "notification.chef.action_failed", baseRpcTarget);
				return;
			}
			EnsureCapacityInitialized(processedIngredientSO.MaxPanCapacity);
			if (IsPanFull())
			{
				_chefDesk.ClientShowNotifyRpc("Info", "notification.chef.gastropan_full", baseRpcTarget);
			}
			else
			{
				ServerApplyIngredientTransfer(component, (byte)processedIngredientSO.IngredientType, processedIngredientSO.Id, component2, null, null, baseRpcTarget);
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerHandleWholesaleNoriRpc(NetworkObjectReference interactorRef, NetworkObjectReference wholesaleNoriRef, RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcParams rpcParams2 = rpcParams;
				RpcAttribute.RpcAttributeParams attributeParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(2338979809u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in wholesaleNoriRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 2338979809u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			BaseRpcTarget baseRpcTarget = base.RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp);
			if (!interactorRef.TryGetComponent<Interactor>(out var component))
			{
				_chefDesk.ClientShowNotifyRpc("Error", "notification.chef.action_failed", baseRpcTarget);
				UtilityMethods.Log("[ChefDeskGastroPan - ServerHandleIngredientEntityRpc] interactor not found");
				return;
			}
			if (!wholesaleNoriRef.TryGetComponent<WholesaleEntity>(out var component2))
			{
				UtilityMethods.Log("[ChefDeskGastroPan - ServerHandleIngredientEntityRpc] ingredientEntityRef not found");
				_chefDesk.ClientShowNotifyRpc("Error", "notification.chef.action_failed", baseRpcTarget);
				return;
			}
			if (!ServerValidateHeldTransfer(component, component2, rpcParams.Receive.SenderClientId))
			{
				_chefDesk.ClientShowNotifyRpc("Error", "notification.chef.action_failed", baseRpcTarget);
				return;
			}
			if (component2.CurrentCount <= 0)
			{
				_chefDesk.ClientShowNotifyRpc("Info", "notification.chef.pack_empty", baseRpcTarget);
				UtilityMethods.Log("[ChefDeskGastroPan - ServerHandleWholesaleNoriRpc] wholesale nori pack empty.");
				return;
			}
			ProcessedIngredientSO processedIngredientSO = ResolveProcessedNoriSO();
			if (processedIngredientSO == null)
			{
				_chefDesk.ClientShowNotifyRpc("Error", "notification.chef.action_failed", baseRpcTarget);
				return;
			}
			EnsureCapacityInitialized(processedIngredientSO.MaxPanCapacity);
			if (IsPanFull())
			{
				_chefDesk.ClientShowNotifyRpc("Info", "notification.chef.gastropan_full", baseRpcTarget);
			}
			else
			{
				ServerApplyIngredientTransfer(component, (byte)processedIngredientSO.IngredientType, processedIngredientSO.Id, null, null, component2, baseRpcTarget);
			}
		}

		private static bool ServerValidateHeldTransfer(Interactor interactor, BaseHoldable holdable, ulong senderClientId)
		{
			int num;
			if (interactor != null && holdable != null && interactor.OwnerClientId == senderClientId && holdable.OwnerClientId == senderClientId && !holdable.CanHold.Value)
			{
				num = (((object)interactor.CurrentHeld == holdable) ? 1 : 0);
				if (num != 0)
				{
					goto IL_004b;
				}
			}
			else
			{
				num = 0;
			}
			UtilityMethods.Log("[ChefDeskGastroPan] Held transfer validation failed.", LogType.Warning);
			goto IL_004b;
			IL_004b:
			return (byte)num != 0;
		}

		private void ServerApplyIngredientTransfer(Interactor interactor, byte type, FixedString64Bytes id, IngredientEntity entity = null, GastroPan gastroPan = null, WholesaleEntity wholesale = null, BaseRpcTarget notifyTarget = null)
		{
			if (_ingredientCount.Value > 0 && _ingredientID.Value != id)
			{
				_chefDesk.ClientShowNotifyRpc("Info", "The ingredients are different. The transfer cannot be completed.", notifyTarget);
				return;
			}
			int num = _maxCapacity.Value - _ingredientCount.Value;
			if (num <= 0)
			{
				_chefDesk.ClientShowNotifyRpc("Info", "notification.chef.gastropan_full", notifyTarget);
				return;
			}
			int num2 = 0;
			if (gastroPan != null)
			{
				num2 = gastroPan.IngredientCount;
			}
			else if (entity != null)
			{
				num2 = entity.IngredientCount;
			}
			else
			{
				if (!(wholesale != null))
				{
					return;
				}
				num2 = wholesale.CurrentCount;
			}
			int num3 = Mathf.Min(num2, num);
			if (num3 <= 0)
			{
				return;
			}
			byte b = (byte)num3;
			_ingredientType.Value = type;
			_ingredientID.Value = id;
			_ingredientCount.Value += b;
			if (gastroPan != null)
			{
				gastroPan.IngredientCount -= b;
				if (num2 - num3 <= 0)
				{
					interactor.Player.Interactor.ClientDestroyHeldItemRpc(gastroPan.NetworkObject.ToReference());
				}
			}
			else if (entity != null)
			{
				entity.IngredientCount -= b;
				if (num2 - num3 <= 0)
				{
					interactor.Player.Interactor.ClientDestroyHeldItemRpc(entity.NetworkObject.ToReference());
				}
			}
			else if (wholesale != null)
			{
				if (!wholesale.ServerTryConsumeAmount(b, out var consumedAmount))
				{
					_ingredientCount.Value -= b;
					return;
				}
				if (consumedAmount != b)
				{
					_ingredientCount.Value -= b;
					_ingredientCount.Value += consumedAmount;
					b = consumedAmount;
				}
			}
			_chefDesk.ClientGastropanSFXRpc(interactor.OwnerClientId, notifyTarget);
			if ((FixedString64Bytes)Singleton<GameProgressManager>.Instance.ItemDatabaseSO.ProcessedNoriSO.Id == _ingredientID.Value || (FixedString64Bytes)Singleton<GameProgressManager>.Instance.ItemDatabaseSO.WholesaleNori.Id == _ingredientID.Value)
			{
				QuestReferenceHub.Instance.QuestComplete(QuestId.AddNoriToChefDeskGastroPan);
			}
			else if ((FixedString64Bytes)Singleton<GameProgressManager>.Instance.ItemDatabaseSO.CookedRice.Id == _ingredientID.Value)
			{
				QuestReferenceHub.Instance.QuestComplete(QuestId.AddCookedRiceToChefDeskGastroPan);
			}
			else if ((FixedString64Bytes)Singleton<GameProgressManager>.Instance.ItemDatabaseSO.CucumberLongStripSO.Id == _ingredientID.Value)
			{
				QuestReferenceHub.Instance.QuestComplete(QuestId.AddSlicedCucumberToChefDeskGastroPan);
			}
		}

		private void EnsureCapacityInitialized(int capacity)
		{
			if (_ingredientCount.Value == 0)
			{
				_maxCapacity.Value = capacity;
			}
		}

		private bool IsPanFull()
		{
			return _ingredientCount.Value >= _maxCapacity.Value;
		}

		private ProcessedIngredientSO ResolveProcessedIngredientSO(IngredientEntity entity)
		{
			IngredientSO ingredientSO = entity.ItemSO as IngredientSO;
			if (ingredientSO != null)
			{
				if (ingredientSO.ProcessedIngredients.Count == 0)
				{
					UtilityMethods.Log("[ChefDeskGastroPan - ResolveProcessedIngredientSO] IngredientSO " + ingredientSO.name + " için ProcessedIngredients listesi boş!");
					return null;
				}
				return ingredientSO.ProcessedIngredients[0];
			}
			return entity.ItemSO as ProcessedIngredientSO;
		}

		private ProcessedIngredientSO ResolveProcessedNoriSO()
		{
			if (_chefDesk.ItemDatabaseSO.FindById(_chefDesk.SingleNoriSO_ID) is ProcessedIngredientSO result)
			{
				return result;
			}
			UtilityMethods.Log("[ChefDeskGastroPan - ResolveProcessedNoriSO] ProcessedNoriSO could not be resolved.");
			return null;
		}

		protected override void ClientMouse2(Interactor interactor, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started)
			{
				base.ClientMouse2(interactor, phase);
				ServerTryTakeInHandRpc(interactor.NetworkObject.ToReference());
			}
		}

		protected override void ClientCrossMouse2(Interactor interactor, BaseHoldable holdable, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started && !(interactor == null) && !(interactor.NetworkObject == null) && (!(holdable != null) || (!(Singleton<QuickSlotHandler>.Instance == null) && Singleton<QuickSlotHandler>.Instance.CanHold(holdable))))
			{
				ServerTryTakeInHandRpc(interactor.NetworkObject.ToReference());
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerTryTakeInHandRpc(NetworkObjectReference interactorRef, RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcParams rpcParams2 = rpcParams;
				RpcAttribute.RpcAttributeParams attributeParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(152869563u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 152869563u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (_chefDesk.StaffLocked)
			{
				return;
			}
			BaseRpcTarget baseRpcTarget = base.RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp);
			if (!interactorRef.TryGetComponent<Interactor>(out var component))
			{
				UtilityMethods.Log("[ChefDeskGastroPan] ServerTryTakeInHandRpc failed!");
				_chefDesk.ClientShowNotifyRpc("Error", "notification.chef.action_failed", baseRpcTarget);
				return;
			}
			if (_ingredientCount.Value <= 0)
			{
				UtilityMethods.Log("Küvet boş, işlem iptal edildi");
				_chefDesk.ClientShowNotifyRpc("Info", "notification.chef.cannot_pick_up_empty_pan", baseRpcTarget);
				return;
			}
			GameObject gameObject = UnityEngine.Object.Instantiate(_chefDesk.GastroPanPrefab);
			GastroPan component2 = gameObject.GetComponent<GastroPan>();
			if (component2 == null || component2.NetworkObject == null)
			{
				_chefDesk.ClientShowNotifyRpc("Error", "notification.chef.action_failed", baseRpcTarget);
				UnityEngine.Object.Destroy(gameObject);
				return;
			}
			Singleton<SoundManager>.Instance.PlaySFX(SoundType.Item_Pick, HoldableMaterialType.Plastic, FurnitureWeight.Medium);
			component2.NetworkObject.Spawn(destroyWithScene: true);
			component2.ServerNonChefDeskInitialize(CurrentIngredientID, _ingredientCount.Value);
			component.ServerHoldRpc(component2.NetworkObject.ToReference());
			_gastropanInteractor.ChefDeskToolTip(value: false);
			ServerResetPan();
		}

		protected override void ClientCrossMouse1(Interactor interactor, BaseHoldable holdable, InputActionPhase phase)
		{
			base.ClientCrossMouse1(interactor, holdable, phase);
			if (phase != InputActionPhase.Started || !CanPlaceHeldIntoGastropan(holdable, showNotify: true))
			{
				return;
			}
			if (!(holdable is GastroPan gastroPan))
			{
				if (!(holdable is IngredientEntity ingredientEntity))
				{
					if (holdable is WholesaleEntity wholesaleEntity)
					{
						ServerHandleWholesaleNoriRpc(interactor.NetworkObject.ToReference(), wholesaleEntity.NetworkObject.ToReference());
						return;
					}
					BaseRpcTarget baseRpcTarget = base.RpcTarget.Single(interactor.OwnerClientId, RpcTargetUse.Temp);
					_chefDesk.ClientShowNotifyRpc("Info", "notification.chef.ingredient_incompatible", baseRpcTarget);
					UtilityMethods.Log("[ChefDeskGastroPan - ClientCrossMouse1] <color=red>Unsupported holdable.</color>");
				}
				else
				{
					ServerHandleIngredientEntityRpc(interactor.NetworkObject.ToReference(), ingredientEntity.NetworkObject.ToReference());
				}
			}
			else
			{
				ServerHandleGastroPanRpc(interactor.NetworkObject.ToReference(), gastroPan.NetworkObject.ToReference());
			}
		}

		private bool CanPlaceHeldIntoGastropan(BaseHoldable holdable, bool showNotify)
		{
			if (holdable is GastroPan gastroPan)
			{
				if (_ingredientCount.Value == 0)
				{
					UtilityMethods.Log("[ChefDeskGastroPan - CanPlaceHeldIntoGastropan] <color=green>Pan empty → GastroPan accepted.</color>");
					return true;
				}
				if (gastroPan.IngredientId != _ingredientID.Value)
				{
					UtilityMethods.Log("[ChefDeskGastroPan - CanPlaceHeldIntoGastropan] <color=orange>Ingredient ID mismatch. Rejected.</color>");
					if (showNotify && Singleton<NotifyManager>.Instance != null)
					{
						Singleton<NotifyManager>.Instance.ShowToast("The ingredients are different. The transfer cannot be completed.");
					}
					return false;
				}
				UtilityMethods.Log("[ChefDeskGastroPan - CanPlaceHeldIntoGastropan] <color=green>GastroPan accepted.</color>");
				return true;
			}
			if (holdable is IngredientEntity ingredientEntity)
			{
				if (!ingredientEntity.IsPlaceableOnChefDesk)
				{
					UtilityMethods.Log("[ChefDeskGastroPan - CanPlaceHeldIntoGastropan] Ingredient cannot be used on ChefDesk.");
					if (showNotify && Singleton<NotifyManager>.Instance != null)
					{
						Singleton<NotifyManager>.Instance.ShowToast(RAW_PREPARATION_MESSAGE);
					}
					return false;
				}
				// Use the same item-data resolver as the server. A prepared item
				// may share a generic pickup component with its raw template.
				ProcessedIngredientSO prepared = ResolveProcessedIngredientSO(ingredientEntity);
				if (prepared == null || string.IsNullOrEmpty(prepared.Id))
				{
					return false;
				}
				string text = prepared.Id;
				if (_ingredientCount.Value == 0)
				{
					UtilityMethods.Log("[ChefDeskGastroPan - CanPlaceHeldIntoGastropan] <color=green>Pan empty → IngredientEntity accepted.</color>");
					return true;
				}
				if ((FixedString64Bytes)text != _ingredientID.Value)
				{
					UtilityMethods.Log("[ChefDeskGastroPan - CanPlaceHeldIntoGastropan] <color=orange>Ingredient ID mismatch. Rejected.</color>");
					if (showNotify && Singleton<NotifyManager>.Instance != null)
					{
						Singleton<NotifyManager>.Instance.ShowToast("The ingredients are different. The transfer cannot be completed.");
					}
					return false;
				}
				UtilityMethods.Log("[ChefDeskGastroPan - CanPlaceHeldIntoGastropan] <color=green>IngredientEntity accepted.</color>");
				return true;
			}
			if (holdable is WholesaleEntity wholesaleEntity && wholesaleEntity.ItemSO != null && wholesaleEntity.ItemSO.Id == Singleton<GameProgressManager>.Instance.ItemDatabaseSO.WholesaleNoriSO.Id)
			{
				if (_ingredientCount.Value == 0)
				{
					UtilityMethods.Log("[ChefDeskGastroPan - CanPlaceHeldIntoGastropan] <color=green>Pan empty → WholesaleNori accepted.</color>");
					return true;
				}
				if (_ingredientID.Value == (FixedString32Bytes)Singleton<GameProgressManager>.Instance.ItemDatabaseSO.ProcessedNoriSO.Id)
				{
					UtilityMethods.Log("[ChefDeskGastroPan - CanPlaceHeldIntoGastropan] <color=green>WholesaleNori accepted.</color>");
					return true;
				}
				if (showNotify && Singleton<NotifyManager>.Instance != null)
				{
					Singleton<NotifyManager>.Instance.ShowToast("The ingredients are different. The transfer cannot be completed.");
				}
				return false;
			}
			UtilityMethods.Log("[ChefDeskGastroPan - CanPlaceHeldIntoGastropan] <color=red>Unsupported holdable.</color>");
			if (showNotify && Singleton<NotifyManager>.Instance != null)
			{
				Singleton<NotifyManager>.Instance.ShowToast("It can’t be transferred to the gastropan you’re holding.");
			}
			return false;
		}

		protected override void __initializeVariables()
		{
			if (_maxCapacity == null)
			{
				throw new Exception("ChefDeskGastroPan._maxCapacity cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_maxCapacity.Initialize(this);
			__nameNetworkVariable(_maxCapacity, "_maxCapacity");
			NetworkVariableFields.Add(_maxCapacity);
			if (_ingredientType == null)
			{
				throw new Exception("ChefDeskGastroPan._ingredientType cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_ingredientType.Initialize(this);
			__nameNetworkVariable(_ingredientType, "_ingredientType");
			NetworkVariableFields.Add(_ingredientType);
			if (_ingredientCount == null)
			{
				throw new Exception("ChefDeskGastroPan._ingredientCount cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_ingredientCount.Initialize(this);
			__nameNetworkVariable(_ingredientCount, "_ingredientCount");
			NetworkVariableFields.Add(_ingredientCount);
			if (_ingredientID == null)
			{
				throw new Exception("ChefDeskGastroPan._ingredientID cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_ingredientID.Initialize(this);
			__nameNetworkVariable(_ingredientID, "_ingredientID");
			NetworkVariableFields.Add(_ingredientID);
			if (_lastEmptiedPlacementIngredientId == null)
			{
				throw new Exception("ChefDeskGastroPan._lastEmptiedPlacementIngredientId cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_lastEmptiedPlacementIngredientId.Initialize(this);
			__nameNetworkVariable(_lastEmptiedPlacementIngredientId, "_lastEmptiedPlacementIngredientId");
			NetworkVariableFields.Add(_lastEmptiedPlacementIngredientId);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(3710719767u, __rpc_handler_3710719767, "ClientPlaceProductOnTableRpc", RpcInvokePermission.Everyone);
			__registerRpc(1714848277u, __rpc_handler_1714848277, "ClientRetrieveProductFromTableRpc", RpcInvokePermission.Everyone);
			__registerRpc(2637171926u, __rpc_handler_2637171926, "ServerDataUpdatedRpc", RpcInvokePermission.Everyone);
			__registerRpc(1829607464u, __rpc_handler_1829607464, "ClientToolTipIngredientCountUpdateRpc", RpcInvokePermission.Everyone);
			__registerRpc(525621338u, __rpc_handler_525621338, "ClientResetPanRpc", RpcInvokePermission.Everyone);
			__registerRpc(2752863329u, __rpc_handler_2752863329, "ClientStaffSwapRpc", RpcInvokePermission.Everyone);
			__registerRpc(2387557919u, __rpc_handler_2387557919, "ServerHandleGastroPanRpc", RpcInvokePermission.Everyone);
			__registerRpc(4071394203u, __rpc_handler_4071394203, "ServerHandleIngredientEntityRpc", RpcInvokePermission.Everyone);
			__registerRpc(2338979809u, __rpc_handler_2338979809, "ServerHandleWholesaleNoriRpc", RpcInvokePermission.Everyone);
			__registerRpc(152869563u, __rpc_handler_152869563, "ServerTryTakeInHandRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_3710719767(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				reader.ReadValueSafe(out byte value2, default(FastBufferWriter.ForPrimitives));
				((ChefDeskGastroPan)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDeskGastroPan)target).ClientPlaceProductOnTableRpc(value, value2);
				((ChefDeskGastroPan)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1714848277(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				reader.ReadValueSafe(out byte value2, default(FastBufferWriter.ForPrimitives));
				ByteUnpacker.ReadValueBitPacked(reader, out int value3);
				((ChefDeskGastroPan)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDeskGastroPan)target).ClientRetrieveProductFromTableRpc(value, value2, value3);
				((ChefDeskGastroPan)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2637171926(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				((ChefDeskGastroPan)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDeskGastroPan)target).ServerDataUpdatedRpc(value);
				((ChefDeskGastroPan)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1829607464(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				((ChefDeskGastroPan)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDeskGastroPan)target).ClientToolTipIngredientCountUpdateRpc(value);
				((ChefDeskGastroPan)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_525621338(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ChefDeskGastroPan)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDeskGastroPan)target).ClientResetPanRpc();
				((ChefDeskGastroPan)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2752863329(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out float value2, default(FastBufferWriter.ForPrimitives));
				((ChefDeskGastroPan)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDeskGastroPan)target).ClientStaffSwapRpc(value, value2);
				((ChefDeskGastroPan)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2387557919(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((ChefDeskGastroPan)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDeskGastroPan)target).ServerHandleGastroPanRpc(value, value2, ext);
				((ChefDeskGastroPan)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4071394203(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((ChefDeskGastroPan)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDeskGastroPan)target).ServerHandleIngredientEntityRpc(value, value2, ext);
				((ChefDeskGastroPan)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2338979809(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((ChefDeskGastroPan)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDeskGastroPan)target).ServerHandleWholesaleNoriRpc(value, value2, ext);
				((ChefDeskGastroPan)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_152869563(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((ChefDeskGastroPan)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDeskGastroPan)target).ServerTryTakeInHandRpc(value, ext);
				((ChefDeskGastroPan)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "ChefDeskGastroPan";
		}
	}
}
