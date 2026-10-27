using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Deform;
using EPOOutline;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Ocean;
using PaperPixelGames.Core.Ocean.Creatures;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Core.Progression;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Core.Staff;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Wheel;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Kitchen
{
	public class FilletMachine : BaseInteractable
	{
		private static readonly float SAW_SPEED = 0.75f;

		private static readonly float MACHINE_SHAKE = 0.0035f;

		private const float OUTPUT_TWEEN_START_LOCAL_X = -0.125f;

		private const float OUTPUT_RECOVERY_RADIUS = 0.12f;

		private const float OUTPUT_FALLBACK_HORIZONTAL_OFFSET = 0.35f;

		private const float OUTPUT_FALLBACK_VERTICAL_OFFSET = 0.1f;

		[SerializeField]
		private float _filletDuration = 5f;

		[Space]
		[SerializeField]
		private Transform _inputSourceObject;

		[SerializeField]
		private Transform _inputSourceObjectBig;

		[SerializeField]
		private Transform _outputSourceObject;

		[SerializeField]
		private Transform _sawTransform;

		[SerializeField]
		private Transform _sawTransform2;

		[SerializeField]
		private Transform _machineRoot;

		[SerializeField]
		private GameObject _objToolTipGO;

		[Header("VFX")]
		[SerializeField]
		private ParticleSystem _exitFX;

		[SerializeField]
		private ParticleSystem _loopFX;

		[SerializeField]
		private ParticleSystem _brokenFX;

		[Header("SFX")]
		[SerializeField]
		private AudioSource _audioSource;

		[SerializeField]
		private AudioClip _insertClip;

		[SerializeField]
		private AudioClip _beginClip;

		[SerializeField]
		private AudioClip _doneClip;

		[SerializeField]
		private AudioClip _explosionClip;

		[SerializeField]
		private AudioClip _takeFilletClip;

		[Header("Button")]
		[SerializeField]
		private Transform _buttonTransform;

		[SerializeField]
		private Renderer _buttonRenderer;

		[SerializeField]
		private float _glowIntensity = 5f;

		[SerializeField]
		private float _defaultIntensity = 1f;

		[Header("Compass")]
		[SerializeField]
		private GameObject _compassParent;

		[SerializeField]
		private Outlinable _compassOutline;

		private bool _shouldShowCompassOnCurrentOperation;

		private bool _compassEnabled;

		private bool _compassEventInProgress;

		private bool _loadInProgress;

		private int _operationVersion;

		private Vector3 _machineRootDefaultLocalPosition;

		private Quaternion _machineRootDefaultLocalRotation;

		private Vector3 _buttonDefaultLocalPosition;

		private Quaternion _buttonDefaultLocalRotation;

		private Vector3 _outputDefaultLocalPosition;

		private Quaternion _outputDefaultLocalRotation;

		private bool _outputDefaultActive;

		private Quaternion _sawDefaultLocalRotation;

		private Quaternion _saw2DefaultLocalRotation;

		private Material _buttonMaterial;

		private Color _buttonDefaultColor;

		private DeadFish _loadedDeadFish;

		private GameObject _loadedFishGraphic;

		private Coroutine _timerCoroutine;

		private ulong _processingClientId = ulong.MaxValue;

		private Vector3 _defaultPosition;

		private Stack<GameObject> _spawnedFilletGraphics = new Stack<GameObject>();

		private readonly NetworkVariable<FilletState> _state = new NetworkVariable<FilletState>(FilletState.Empty);

		private readonly NetworkVariable<byte> _filletYield = new NetworkVariable<byte>(0);

		private readonly NetworkVariable<NetworkObjectReference> _loadedFishReference = new NetworkVariable<NetworkObjectReference>();

		private readonly NetworkVariable<int> _staffEmployee = new NetworkVariable<int>(0);

		private readonly NetworkVariable<FixedString64Bytes> _staffFish = new NetworkVariable<FixedString64Bytes>();

		private GameObject _staffFishVisual;

		public bool StaffLocked => _staffEmployee.Value != 0;

		public bool ServerStaffAvailable
		{
			get
			{
				if (base.IsServer && base.IsSpawned && !StaffLocked && _state.Value == FilletState.Empty && !_loadInProgress && !_compassEventInProgress)
				{
					return !_compassEnabled;
				}
				return false;
			}
		}

		protected override void Awake()
		{
			base.Awake();
			_defaultPosition = base.transform.position;
			_buttonMaterial = _buttonRenderer.material;
			_buttonDefaultColor = _buttonMaterial.color;
			_buttonMaterial.SetColor("_EmissionColor", _buttonMaterial.color * _defaultIntensity);
			CacheDefaultMachineState();
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			NetworkVariable<FixedString64Bytes> staffFish = _staffFish;
			staffFish.OnValueChanged = (NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate)Delegate.Combine(staffFish.OnValueChanged, new NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate(StaffFishChanged));
			StaffFishChanged(default, _staffFish.Value);
			NetworkVariable<FilletState> state = _state;
			state.OnValueChanged = (NetworkVariable<FilletState>.OnValueChangedDelegate)Delegate.Combine(state.OnValueChanged, new NetworkVariable<FilletState>.OnValueChangedDelegate(OnStateChanged));
			NetworkVariable<byte> filletYield = _filletYield;
			filletYield.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Combine(filletYield.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnFilletYieldChanged));
			_objToolTipGO.SetActive(value: false);
			StartCoroutine(ClientRestoreLateJoinState());
		}

		public override void OnNetworkDespawn()
		{
			NetworkVariable<FixedString64Bytes> staffFish = _staffFish;
			staffFish.OnValueChanged = (NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate)Delegate.Remove(staffFish.OnValueChanged, new NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate(StaffFishChanged));
			NetworkVariable<FilletState> state = _state;
			state.OnValueChanged = (NetworkVariable<FilletState>.OnValueChangedDelegate)Delegate.Remove(state.OnValueChanged, new NetworkVariable<FilletState>.OnValueChangedDelegate(OnStateChanged));
			NetworkVariable<byte> filletYield = _filletYield;
			filletYield.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Remove(filletYield.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnFilletYieldChanged));
			base.OnNetworkDespawn();
		}

		public override void EnableGazeHints()
		{
			Singleton<HintsManager>.Instance.ClearAllHints();
			switch (_state.Value)
			{
			case FilletState.Empty:
				AddEmptyStateHints();
				break;
			case FilletState.Loaded:
				Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_start");
				Singleton<HintsManager>.Instance.AddHint("RightMouseMechanic", "key_take");
				break;
			case FilletState.Completed:
				if (_filletYield.Value > 0)
				{
					Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_take");
				}
				break;
			}
			_objToolTipGO.SetActive(value: true);
		}

		public override void DisableGazeHints()
		{
			Singleton<HintsManager>.Instance.ClearAllHints();
			_objToolTipGO.SetActive(value: false);
		}

		private void AddEmptyStateHints()
		{
			if (!(Singleton<QuickSlotHandler>.Instance == null) && Singleton<QuickSlotHandler>.Instance.SelectedItem is DeadFish)
			{
				Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_place");
			}
		}

		private bool CompassShowCondition()
		{
			if (!Singleton<PPG_WheelManager>.Instance.IsCompassUnlocked)
			{
				return QuestReferenceHub.Instance.IsCurrentOrAlreadyDone(QuestId.UseTheFilletMachine);
			}
			return false;
		}

		[Rpc(SendTo.Server)]
		private void ServerEnableCompassRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(3609356508u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3609356508u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!StaffLocked)
				{
					TryAdvanceToStrangeCompassQuestServer();
					ClientEnableCompassRpc();
				}
			}
		}

		private void TryAdvanceToStrangeCompassQuestServer()
		{
			QuestReferenceHub instance = QuestReferenceHub.Instance;
			if (!(instance == null))
			{
				instance.CompleteIfActive(QuestId.UseTheFilletMachine);
				instance.CompleteCurrentMainGroupIfContains(QuestId.UseTheFilletMachine);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientEnableCompassRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(3737698339u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3737698339u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			int operationVersion = _operationVersion;
			_compassEventInProgress = true;
			_audioSource.Stop();
			_audioSource.PlayOneShot(_explosionClip);
			Singleton<DelayHelper>.Instance.DelayAction(0.7f, () =>
			{
				if (IsOperationVersionCurrent(operationVersion))
				{
					_loopFX.Stop();
					_sawTransform.DOKill();
					_sawTransform2.DOKill();
					_machineRoot.DOKill();
					_buttonMaterial.DOColor(Color.red * _glowIntensity, "_EmissionColor", 0.2f);
				}
			});
			Singleton<DelayHelper>.Instance.DelayAction(1.25f, () =>
			{
				if (IsOperationVersionCurrent(operationVersion))
				{
					PPG_Player pPG_Player = Singleton<PlayersManager>.Instance?.LocalPlayer;
					if (pPG_Player != null && Vector3.Distance(base.transform.position, pPG_Player.transform.position) < 10f)
					{
						pPG_Player.CameraShake(ShakeIntensity.Medium, ShakeDuration.Long);
					}
					_compassParent.SetActive(value: true);
					Singleton<HighlightManager>.Instance?.SetPersistent(_compassOutline);
					_brokenFX.Play();
					_compassEnabled = true;
				}
			});
		}

		[Rpc(SendTo.Server)]
		private void ServerDisableCompassRpc(NetworkObjectReference interactorRef)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2685536099u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 2685536099u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!StaffLocked)
				{
					QuestReferenceHub.Instance?.CompleteIfActive(QuestId.PickUpCompass);
					ClientDisableCompassRpc(interactorRef);
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientDisableCompassRpc(NetworkObjectReference interactorRef)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2100103264u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 2100103264u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				CleanupInputGraphics();
				_brokenFX.Stop();
				_audioSource.Stop();
				_compassParent.SetActive(value: false);
				Singleton<HighlightManager>.Instance.ClearPersistentHighlight();
				if (base.IsServer)
				{
					ServerUpdateStateRpc(1);
				}
				_compassEnabled = false;
				_compassEventInProgress = false;
				if (Singleton<PPG_WheelManager>.Instance != null)
				{
					Singleton<PPG_WheelManager>.Instance.UnlockCompass(status: true);
				}
				if (!interactorRef.TryGetComponent<Interactor>(out var _))
				{
					UtilityMethods.Log("[FilletMachine] interactor not found!");
				}
			}
		}

		private void TweenProcessing()
		{
			_sawTransform.DOKill();
			_sawTransform2.DOKill();
			_machineRoot.DOKill();
			_buttonTransform.DOKill();
			_outputSourceObject.gameObject.SetActive(value: false);
			_loopFX.Play();
			_audioSource.PlayOneShot(_beginClip);
			_buttonTransform.DOLocalMoveZ(-0.015f, 0.2f).OnComplete(() =>
			{
				_buttonTransform.DOLocalMoveZ(0f, 0.3f);
			});
			_buttonMaterial.DOColor(_buttonMaterial.color * _glowIntensity, "_EmissionColor", 0.2f);
			_sawTransform.DOLocalRotate(new Vector3(0f, 0f, 480f), SAW_SPEED, RotateMode.FastBeyond360).SetEase(Ease.InExpo).OnComplete(() =>
			{
				_sawTransform.DOLocalRotate(new Vector3(0f, 0f, 360f), SAW_SPEED * 0.5f, RotateMode.FastBeyond360).SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart);
			});
			_sawTransform2.DOLocalRotate(new Vector3(0f, 0f, 360f), SAW_SPEED, RotateMode.FastBeyond360).SetEase(Ease.InExpo).OnComplete(() =>
			{
				_sawTransform2.DOLocalRotate(new Vector3(0f, 0f, 360f), SAW_SPEED * 0.5f, RotateMode.FastBeyond360).SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart);
			});
			_machineRoot.DOShakePosition(0.5f, Vector3.right * MACHINE_SHAKE, 5, 90f, snapping: false, fadeOut: false).SetEase(Ease.InExpo);
			_machineRoot.DOShakePosition(0.5f, Vector3.right * MACHINE_SHAKE, 8, 90f, snapping: false, fadeOut: false).SetDelay(0.45f).SetLoops(-1, LoopType.Restart);
		}

		private void TweenCompleted()
		{
			_loopFX.Stop();
			_sawTransform.DOKill();
			_sawTransform2.DOKill();
			_sawTransform.DOLocalRotate(new Vector3(0f, 0f, 360f), SAW_SPEED * 0.5f, RotateMode.FastBeyond360).SetEase(Ease.OutExpo);
			_sawTransform2.DOLocalRotate(new Vector3(0f, 0f, 480f), SAW_SPEED * 0.5f, RotateMode.FastBeyond360).SetEase(Ease.OutExpo);
			_machineRoot.DOKill();
			_machineRoot.DOShakePosition(0.5f, Vector3.right * MACHINE_SHAKE, 12, 90f, snapping: false, fadeOut: false).SetEase(Ease.OutExpo).OnComplete(() =>
			{
				_machineRoot.position = _defaultPosition;
			});
			_buttonMaterial.DOColor(Color.yellow * _glowIntensity / 2f, "_EmissionColor", 0.5f);
			_audioSource.PlayOneShot(_doneClip);
			_exitFX.Play();
			_outputSourceObject.DOKill();
			_outputSourceObject.DOLocalMoveX(-0.125f, 0f).OnComplete(() =>
			{
				_outputSourceObject.gameObject.SetActive(value: true);
			});
			_outputSourceObject.DOLocalMoveX(-0.375f, 0.4f).SetDelay(1.1f);
		}

		private void TimerStart(int operationVersion)
		{
			StopTimerIfRunning();
			_shouldShowCompassOnCurrentOperation = ShouldStartCompassEventForCurrentOperation();
			QuestReferenceHub.Instance.CompleteIfActive(QuestId.StartTheFilletMachine);
			_timerCoroutine = StartCoroutine(TimerRoutine(_filletDuration, operationVersion));
		}

		private bool ShouldStartCompassEventForCurrentOperation()
		{
			if (Singleton<PPG_WheelManager>.Instance == null || Singleton<PPG_WheelManager>.Instance.IsCompassUnlocked)
			{
				return false;
			}
			if (QuestReferenceHub.Instance == null)
			{
				return false;
			}
			return QuestReferenceHub.Instance.IsActive(QuestId.UseTheFilletMachine);
		}

		private IEnumerator TimerRoutine(float duration, int operationVersion)
		{
			int total = Mathf.CeilToInt(duration);
			int halfTick = total / 2;
			for (int t = 0; t < total; t++)
			{
				yield return new WaitForSeconds(1f);
				if (!IsOperationVersionCurrent(operationVersion))
				{
					_timerCoroutine = null;
					yield break;
				}
				if (_state.Value != FilletState.Processing)
				{
					_timerCoroutine = null;
					yield break;
				}
				if (_shouldShowCompassOnCurrentOperation && t == halfTick)
				{
					_timerCoroutine = null;
					ServerEnableCompassRpc();
					yield break;
				}
			}
			_timerCoroutine = null;
			TimerDone(operationVersion);
		}

		private void TimerDone(int operationVersion)
		{
			if (!base.IsServer || !IsOperationVersionCurrent(operationVersion) || _state.Value != FilletState.Processing)
			{
				return;
			}
			if (!HasValidLoadedFish())
			{
				ServerRecoverToEmpty("TimerDone failed because loaded fish is invalid.", discardLoadedFish: true);
				return;
			}
			FishSO fishSo = _loadedDeadFish.FishSO;
			byte yield = (byte)fishSo.RollFilletYield();
			ServerUpdateYieldRpc(yield);
			if (Singleton<GameProgressManager>.Instance.RestaurantManager != null)
			{
				_loadedDeadFish.ServerDeregisterInventoryStock();
			}
			Singleton<DelayHelper>.Instance.DelayAction(0.25f, () =>
			{
				if (IsOperationVersionCurrent(operationVersion) && _state.Value == FilletState.Processing)
				{
					ServerUpdateStateRpc(4);
					Singleton<GameProgressManager>.Instance?.ServerGrantProgressionXp(ProgressionXpSource.FishFilleted, (fishSo != null) ? fishSo.Id : null, (_processingClientId == ulong.MaxValue) ? ((ulong?)null) : new ulong?(_processingClientId));
				}
			});
		}

		private void OnStateChanged(FilletState previous, FilletState current)
		{
			if (current == FilletState.Loaded || current == FilletState.Empty)
			{
				_loadInProgress = false;
			}
			switch (current)
			{
			case FilletState.Empty:
				_shouldShowCompassOnCurrentOperation = false;
				InvalidateOperationVersion();
				StopTimerIfRunning();
				KillMachineTweensAndRuntimeEffects();
				_loadedDeadFish = null;
				_buttonMaterial.DOColor(_buttonDefaultColor * _defaultIntensity, "_EmissionColor", 0.5f);
				CleanupInputGraphics();
				break;
			case FilletState.Loaded:
				if (base.IsServer)
				{
					if (!HasValidLoadedFish())
					{
						ServerRecoverToEmpty("Loaded state entered with invalid loaded fish.", discardLoadedFish: true);
						return;
					}
					if ((bool)Singleton<GameProgressManager>.Instance.ItemDatabaseSO.MackerelSO)
					{
						QuestReferenceHub.Instance.QuestComplete(QuestId.PlaceMackerelInFilletMachine);
					}
				}
				break;
			case FilletState.Processing:
			{
				int operationVersion = InvalidateOperationVersion();
				if (_loadedFishGraphic == null || !HasValidLoadedFish())
				{
					if (base.IsServer)
					{
						ServerRecoverToEmpty("Processing state entered without a valid loaded fish or input graphic.", discardLoadedFish: true);
					}
					CleanupInputGraphics();
					return;
				}
				_audioSource.PlayOneShot(_insertClip);
				Deformable componentInChildren = _loadedFishGraphic.GetComponentInChildren<Deformable>(includeInactive: true);
				if (componentInChildren != null)
				{
					componentInChildren.UpdateMode = UpdateMode.Auto;
					if (componentInChildren.DeformerElements == null || componentInChildren.DeformerElements.Count <= 0)
					{
						UtilityMethods.Log("[FilletMachine] DeformerElements missing!", LogType.Warning);
						ResetMachineSafe();
						return;
					}
					Deformer component = componentInChildren.DeformerElements[0].Component;
					MagnetDeformer magnetDeformer = component as MagnetDeformer;
					if ((object)magnetDeformer == null)
					{
						UtilityMethods.Log("[FilletMachine] MagnetDeformer not found!", LogType.Warning);
						ResetMachineSafe();
						return;
					}
					float factor = _loadedDeadFish.FishSO.StartMagnetFactor;
					DOTween.To(() => factor, (float x) =>
					{
						if (IsOperationVersionCurrent(operationVersion))
						{
							factor = x;
							magnetDeformer.Factor = x;
						}
					}, _loadedDeadFish.FishSO.EndMagnetFactor, 0.5f).OnComplete(() =>
					{
						if (IsOperationVersionCurrent(operationVersion) && !(_loadedFishGraphic == null))
						{
							_loadedFishGraphic.SetActive(value: false);
						}
					});
					TweenProcessing();
					if (base.IsServer)
					{
						TimerStart(operationVersion);
					}
				}
				else
				{
					if (!IsOperationVersionCurrent(operationVersion) || _loadedFishGraphic == null)
					{
						return;
					}
					_loadedFishGraphic.SetActive(value: false);
					TweenProcessing();
					if (base.IsServer)
					{
						TimerStart(operationVersion);
					}
					UtilityMethods.Log("[FilletMachine] Deformable not found in children!", LogType.Warning);
				}
				break;
			}
			case FilletState.Completed:
			{
				if ((_loadedDeadFish == null || _loadedFishGraphic == null) && _loadedFishReference.Value.TryGet(out var _))
				{
					StartCoroutine(ClientRestoreLateJoinState());
					return;
				}
				if (!CanUseCurrentOutputData())
				{
					if (base.IsServer)
					{
						ServerRecoverToEmpty("Completed state entered with invalid output data.", discardLoadedFish: true);
					}
					CleanupInputGraphics();
					return;
				}
				EnsureOutputGraphicCountMatchesYield();
				TweenCompleted();
				break;
			}
			}
			RefreshGazeFeedback();
		}

		private void OnFilletYieldChanged(byte previous, byte current)
		{
			UtilityMethods.Log($"[FilletMachine - OnFilletYieldChanged] {_state.Value} - {previous} - {current}!");
			if (_state.Value == FilletState.Processing)
			{
				int num = current - previous;
				if (num <= 0)
				{
					return;
				}
				if (!CanUseCurrentOutputData())
				{
					if (base.IsServer)
					{
						ServerRecoverToEmpty("Yield increased while output data was invalid.", discardLoadedFish: true);
					}
					CleanupInputGraphics();
					return;
				}
				SpawnOutputGraphics(previous, num);
			}
			else if (_state.Value == FilletState.Completed)
			{
				int num2 = previous - current;
				for (int i = 0; i < num2; i++)
				{
					if (!_spawnedFilletGraphics.TryPop(out var result))
					{
						UtilityMethods.Log("[FilletMachine] Output visual missing while reducing yield.", LogType.Warning);
					}
					else
					{
						UnityEngine.Object.Destroy(result);
					}
				}
				if (base.IsServer && current == 0)
				{
					int operationVersion = _operationVersion;
					Singleton<DelayHelper>.Instance.DelayAction(1f, () =>
					{
						if (IsOperationVersionCurrent(operationVersion) && _state.Value == FilletState.Completed && _filletYield.Value == 0)
						{
							ServerFinalizeCompletedFish();
						}
					});
				}
			}
			RefreshGazeFeedback();
		}

		protected override void ClientMouse1(Interactor interactor, InputActionPhase phase)
		{
			if (StaffLocked || phase != InputActionPhase.Started)
			{
				return;
			}
			if (_compassEnabled)
			{
				ServerDisableCompassRpc(interactor.NetworkObject.ToReference());
				return;
			}
			switch (_state.Value)
			{
			case FilletState.Empty:
				ShowMachineToast("notification.fillet_machine.place_fish_first", "Place a fish into the machine first.");
				break;
			case FilletState.Loaded:
				ServerUpdateStateRpc(3);
				break;
			case FilletState.Processing:
				ShowMachineToast("notification.fillet_machine.already_running", "The fillet machine is already running.");
				break;
			case FilletState.Completed:
				TryTakeFilletOutput(interactor);
				break;
			}
		}

		protected override void ClientMouse2(Interactor interactor, InputActionPhase phase)
		{
			if (StaffLocked || _compassEnabled || phase != InputActionPhase.Started)
			{
				return;
			}
			if (_loadedDeadFish == null)
			{
				UtilityMethods.Log("[FilletMachine] _loadedFishSO is null!");
				return;
			}
			switch (_state.Value)
			{
			case FilletState.Loaded:
				if (interactor.TryHold(_loadedDeadFish))
				{
					ServerDestroyLoadedFishRpc();
				}
				break;
			case FilletState.Processing:
				ShowMachineToast("notification.fillet_machine.already_running", "The fillet machine is already running.");
				break;
			case FilletState.Completed:
				ShowMachineToast("notification.fillet_machine.collect_fillets_first", "Collect the fillets before taking the fish back.");
				break;
			}
		}

		protected override bool CanCrossTrigger(Interactor interactor, BaseHoldable held)
		{
			if (held == null)
			{
				return false;
			}
			_ = _compassEnabled;
			return base.CanCrossTrigger(interactor, held);
		}

		protected override void ClientCrossMouse1(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			if (StaffLocked || phase != InputActionPhase.Started)
			{
				return;
			}
			if (_compassEnabled)
			{
				ServerDisableCompassRpc(interactor.NetworkObject.ToReference());
				return;
			}
			switch (_state.Value)
			{
			case FilletState.Empty:
				TryLoadHeldItemAsFish(interactor, held);
				break;
			case FilletState.Loaded:
				ServerUpdateStateRpc(3);
				break;
			case FilletState.Processing:
				ShowMachineToast("notification.fillet_machine.already_running", "The fillet machine is already running.");
				break;
			case FilletState.Completed:
				TryTakeFilletOutput(interactor);
				break;
			}
		}

		private void TryLoadHeldItemAsFish(Interactor interactor, BaseHoldable held)
		{
			if (_loadInProgress)
			{
				ShowMachineToast("notification.fillet_machine.fish_loading", "The fish is already being loaded.");
				return;
			}
			if (held == null)
			{
				ShowMachineToast("notification.fillet_machine.hold_fish_first", "You need to hold a fish first.");
				return;
			}
			if (!(held is DeadFish))
			{
				ShowMachineToast("notification.fillet_machine.only_fish", "Only fish can be placed into the fillet machine.");
				return;
			}
			if(held is DeadFish collectionFish&&collectionFish.FishSO!=null&&collectionFish.FishSO.IngredientSO==null){ShowMachineToast("fishing.collection_only","这类钓获物只能收藏、展示或出售，不能切片。");return;}
            if (held.NetworkObject == null || !held.NetworkObject.IsSpawned)
			{
				ShowMachineToast("notification.fillet_machine.fish_unavailable", "This fish cannot be loaded right now.");
				return;
			}
			_loadInProgress = true;
			ServerLoadFishRpc(held.NetworkObject.ToReference(), interactor.OwnerClientId);
		}

		private void ShowMachineToast(string localizationKey, string fallback)
		{
			if (!(Singleton<NotifyManager>.Instance == null))
			{
				string message = LocalizationDatabase.GetString("UiTable", localizationKey, fallback);
				Singleton<NotifyManager>.Instance.ShowToast(message);
			}
		}

		private void TryTakeFilletOutput(Interactor interactor)
		{
			if (_filletYield.Value <= 0)
			{
				return;
			}
			if (!CanUseCurrentOutputData())
			{
				ShowMachineToast("notification.fillet_machine.output_missing", "Fillet output is missing.");
				return;
			}
			IngredientSO ingredientSO = _loadedDeadFish.FishSO.IngredientSO;
			if (Singleton<QuickSlotHandler>.Instance == null || !Singleton<QuickSlotHandler>.Instance.CanHoldSlot(ingredientSO))
			{
				ShowMachineToast("notification.fillet_machine.inventory_full", "No space in inventory.");
			}
			else
			{
				ServerTakeFilletOutputRpc(interactor.NetworkObject.ToReference(), selectOutput: true);
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerTakeFilletOutputRpc(NetworkObjectReference interactorRef, bool selectOutput, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1533963084u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in selectOutput, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1533963084u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (StaffLocked || _state.Value != FilletState.Completed || _filletYield.Value <= 0)
			{
				return;
			}
			if (!CanUseCurrentOutputData())
			{
				ServerRecoverToEmpty("ServerTakeFilletOutputRpc failed because output data was invalid.", discardLoadedFish: true);
				return;
			}
			if (!interactorRef.TryGetComponent<Interactor>(out var component))
			{
				UtilityMethods.Log("[FilletMachine] ServerTakeFilletOutputRpc failed: interactor not found.", LogType.Warning);
				return;
			}
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (component.OwnerClientId != senderClientId)
			{
				UtilityMethods.Log($"[FilletMachine] ServerTakeFilletOutputRpc failed: interactor ownership mismatch. Sender:{senderClientId}, InteractorOwner:{component.OwnerClientId}", LogType.Warning);
				return;
			}
			IngredientSO ingredientSO = _loadedDeadFish.FishSO.IngredientSO;
			if (ingredientSO.Prefab == null)
			{
				UtilityMethods.Log("[FilletMachine] ServerTakeFilletOutputRpc failed: ingredient prefab missing.", LogType.Warning);
				return;
			}
			if (!ingredientSO.Prefab.TryGetComponent<NetworkObject>(out var component2))
			{
				UtilityMethods.Log("[FilletMachine] ServerTakeFilletOutputRpc failed: ingredient prefab has no NetworkObject.", LogType.Warning);
				return;
			}
			if (!component2.TryInstantiateAndSpawn(out var instance, _outputSourceObject.position, _outputSourceObject.rotation))
			{
				UtilityMethods.Log("[FilletMachine] ServerTakeFilletOutputRpc failed: spawn failed.", LogType.Warning);
				return;
			}
			if (!instance.TryGetComponent<BaseHoldable>(out var component3))
			{
				UtilityMethods.Log("[FilletMachine] ServerTakeFilletOutputRpc failed: spawned object has no BaseHoldable.", LogType.Warning);
				instance.Despawn();
				return;
			}
			component3.ServerLazyHold(component.OwnerClientId, component.NetworkObject.ToReference(), selectOutput, hold: true, useFallbackDropPosition: true, GetOutputFallbackDropPosition(), requireQuickSlot: true);
			((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null)?.ServerAddStockRpc(ingredientSO.Id, 1);
			ServerUpdateYieldRpc((byte)(_filletYield.Value - 1));
			QuestReferenceHub.Instance.QuestComplete(QuestId.TakeMackerelFillet);
			ClientPlayTakeFilletSfxRpc();
		}

		[Rpc(SendTo.Everyone)]
		private void ClientPlayTakeFilletSfxRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(1914686566u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1914686566u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (_takeFilletClip != null)
				{
					_audioSource.PlayOneShot(_takeFilletClip);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerUpdateStateRpc(byte state)
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
				FastBufferWriter bufferWriter = __beginSendRpc(4153021801u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in state, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 4153021801u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (StaffLocked)
			{
				return;
			}
			FilletState filletState = (FilletState)state;
			if (filletState == FilletState.Processing && !HasValidLoadedFish())
			{
				ServerRecoverToEmpty("Processing request rejected because loaded fish is invalid.", discardLoadedFish: true);
				return;
			}
			if (filletState == FilletState.Empty)
			{
				_loadedFishReference.Value = default;
			}
			_state.Value = filletState;
		}

		[Rpc(SendTo.Server)]
		private void ServerUpdateYieldRpc(byte yield)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2617503868u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in yield, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2617503868u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!StaffLocked)
				{
					_filletYield.Value = yield;
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerDestroyLoadedFishRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(2417250426u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 2417250426u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!StaffLocked)
				{
					ClientDestroyLoadedFishRpc();
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientDestroyLoadedFishRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(2767525284u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 2767525284u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				CleanupInputGraphics();
				if (base.IsServer)
				{
					ServerUpdateStateRpc(1);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerLoadFishRpc(NetworkObjectReference fishRef, ulong clientId)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2850751229u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in fishRef, default(FastBufferWriter.ForNetworkSerializable));
				BytePacker.WriteValueBitPacked(bufferWriter, clientId);
				__endSendRpc(ref bufferWriter, 2850751229u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!StaffLocked)
			{
				if (_state.Value != FilletState.Empty)
				{
					ClientLoadFishRejectedRpc(clientId);
					return;
				}
				if (!fishRef.TryGetComponent<DeadFish>(out var component) || component.NetworkObject == null || !component.NetworkObject.IsSpawned || component.FishSO == null || component.FishSO.IngredientSO == null || component.DeadGraphic == null)
				{
					UtilityMethods.Log("[FilletMachine] ServerLoadFishRpc rejected invalid fish.", LogType.Warning);
					ClientLoadFishRejectedRpc(clientId);
					return;
				}
				_loadedDeadFish = component;
				_processingClientId = clientId;
				_loadedFishReference.Value = fishRef;
				_state.Value = FilletState.Loaded;
				ClientLoadFishRpc(fishRef, clientId);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientLoadFishRpc(NetworkObjectReference fishRef, ulong clientId)
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
				FastBufferWriter bufferWriter = __beginSendRpc(4025622129u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in fishRef, default(FastBufferWriter.ForNetworkSerializable));
				BytePacker.WriteValueBitPacked(bufferWriter, clientId);
				__endSendRpc(ref bufferWriter, 4025622129u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			CleanupInputGraphics();
			if (!fishRef.TryGetComponent<DeadFish>(out var component) || component.FishSO == null || component.DeadGraphic == null)
			{
				UtilityMethods.Log("[FilletMachine] loadedDeadFish is invalid:" + fishRef.NetworkObjectId, LogType.Warning);
				_loadInProgress = false;
				if (base.IsServer)
				{
					ServerRecoverToEmpty("ClientLoadFishRpc failed to resolve loaded fish on server.", discardLoadedFish: true);
				}
				return;
			}
			component.ObjectsStatus(status: false);
			component.CollidersStatus(status: false);
			_loadedDeadFish = component;
			_audioSource.PlayOneShot(_insertClip);
			_loadedFishGraphic = UnityEngine.Object.Instantiate(component.DeadGraphic, _loadedDeadFish.FishSO.FilletBigInput ? _inputSourceObjectBig.transform : _inputSourceObject.transform);
			if (!component.FishSO.Name.Contains("Shrimp"))
			{
				Singleton<SeaCreatureManager>.Instance.ApplyDeadEyeShaderState(_loadedFishGraphic);
			}
			ItemOffset filletOffset = component.FishSO.FilletOffset;
			if (filletOffset.ScaleOffset != Vector3.zero)
			{
				_loadedFishGraphic.transform.localScale = filletOffset.ScaleOffset;
			}
			_loadedFishGraphic.transform.SetLocalPositionAndRotation(filletOffset.PositionOffset, Quaternion.Euler(filletOffset.RotationOffset));
			if (_loadedFishGraphic.TryGetComponent<Deformable>(out var component2))
			{
				component2.UpdateMode = UpdateMode.Auto;
				if (component2.DeformerElements != null && component2.DeformerElements.Count > 0)
				{
					Deformer component3 = component2.DeformerElements[0].Component;
					MagnetDeformer magnetDeform = component3 as MagnetDeformer;
					if ((object)magnetDeform != null)
					{
						float factor = 0f;
						DOTween.To(() => factor, (float x) =>
						{
							factor = x;
							magnetDeform.Factor = x;
						}, component.FishSO.StartMagnetFactor, 0.25f);
					}
					else
					{
						UtilityMethods.Log("[FilletMachine] ClientLoadFishRpc MagnetDeform not found!", LogType.Warning);
					}
				}
				else
				{
					UtilityMethods.Log("[FilletMachine] ClientLoadFishRpc DeformerElements missing!", LogType.Warning);
				}
			}
			if (Singleton<PlayersManager>.Instance.LocalPlayer.OwnerClientId == clientId)
			{
				Singleton<QuickSlotHandler>.Instance.RemoveSpecificItem(component);
				component.Disable(clearQuickSlotOnOwner: false);
				RefreshMachineHintsAfterInputConsumed(clientId);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientLoadFishRejectedRpc(ulong clientId)
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
				FastBufferWriter bufferWriter = __beginSendRpc(153628090u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, clientId);
				__endSendRpc(ref bufferWriter, 153628090u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!(Singleton<PlayersManager>.Instance == null) && !(Singleton<PlayersManager>.Instance.LocalPlayer == null) && Singleton<PlayersManager>.Instance.LocalPlayer.OwnerClientId == clientId)
				{
					_loadInProgress = false;
					RefreshGazeFeedback();
				}
			}
		}

		public void ServerEndDayCleanup()
		{
			if (!base.IsServer)
			{
				UtilityMethods.Log("[FilletMachine] ServerEndDayCleanup blocked. Only server can clean up the machine.", LogType.Warning);
				return;
			}
			if (_compassEnabled || _compassEventInProgress)
			{
				UtilityMethods.Log("[FilletMachine] ServerEndDayCleanup skipped because compass event is active.", LogType.Warning);
				return;
			}
			InvalidateOperationVersion();
			StopTimerIfRunning();
			ServerDiscardLoadedDeadFish();
			if (_filletYield.Value != 0)
			{
				_filletYield.Value = 0;
			}
			if (_state.Value != FilletState.Empty)
			{
				_state.Value = FilletState.Empty;
			}
			ClientEndDayCleanupRpc();
		}

		[Rpc(SendTo.Everyone)]
		private void ClientEndDayCleanupRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(2636773693u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 2636773693u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				InvalidateOperationVersion();
				ResetMachineLocalVisualState(restoreDefaultTransforms: true);
				RefreshGazeFeedback();
			}
		}

		private void ServerRecoverToEmpty(string reason, bool discardLoadedFish)
		{
			if (base.IsServer)
			{
				UtilityMethods.Log("[FilletMachine] " + reason + " Recovering to Empty state.", LogType.Warning);
				InvalidateOperationVersion();
				StopTimerIfRunning();
				if (discardLoadedFish)
				{
					ServerDiscardLoadedDeadFish();
				}
				if (_filletYield.Value != 0)
				{
					_filletYield.Value = 0;
				}
				if (_state.Value != FilletState.Empty)
				{
					_state.Value = FilletState.Empty;
				}
				_loadedFishReference.Value = default;
				ClientRecoverToEmptyRpc();
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientRecoverToEmptyRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(2194133395u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 2194133395u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ResetMachineLocalVisualState(restoreDefaultTransforms: false);
				RefreshGazeFeedback();
			}
		}

		private void ServerFinalizeCompletedFish()
		{
			if (base.IsServer)
			{
				ServerDiscardLoadedDeadFish();
				ClientDestroyLoadedFishRpc();
			}
		}

		private IEnumerator ClientRestoreLateJoinState()
		{
			yield return null;
			FilletState value = _state.Value;
			if (value != FilletState.Loaded && value != FilletState.Completed)
			{
				yield break;
			}
			for (int attempt = 0; attempt < 10; attempt++)
			{
				if (!(_loadedDeadFish == null) && !(_loadedFishGraphic == null))
				{
					break;
				}
				if (!_loadedFishReference.Value.TryGet(out var networkObject) || !networkObject.TryGetComponent<DeadFish>(out var _))
				{
					yield return null;
				}
				else
				{
					ClientLoadFishRpc(_loadedFishReference.Value, ulong.MaxValue);
				}
			}
			if (_loadedDeadFish == null || _loadedFishGraphic == null)
			{
				UtilityMethods.Log("[FilletMachine] Late-join restore could not resolve the loaded fish.", LogType.Warning);
				yield break;
			}
			if (_state.Value == FilletState.Completed)
			{
				if (_loadedFishGraphic != null)
				{
					_loadedFishGraphic.SetActive(value: false);
				}
				EnsureOutputGraphicCountMatchesYield();
				TweenCompleted();
			}
			RefreshGazeFeedback();
		}

		private void ServerDiscardLoadedDeadFish()
		{
			if (base.IsServer && !(_loadedDeadFish == null))
			{
				NetworkObject networkObject = _loadedDeadFish.NetworkObject;
				if (networkObject != null && networkObject.IsSpawned)
				{
					networkObject.Despawn();
				}
				else if (_loadedDeadFish.gameObject != null)
				{
					UnityEngine.Object.Destroy(_loadedDeadFish.gameObject);
				}
				_loadedDeadFish = null;
			}
		}

		private bool HasValidLoadedFish()
		{
			if (_loadedDeadFish != null && _loadedDeadFish.NetworkObject != null && _loadedDeadFish.NetworkObject.IsSpawned)
			{
				return _loadedDeadFish.FishSO != null;
			}
			return false;
		}

		private bool CanUseCurrentOutputData()
		{
			if (HasValidLoadedFish())
			{
				return _loadedDeadFish.FishSO.IngredientSO != null;
			}
			return false;
		}

		private bool CanUseCurrentOutputGraphicData()
		{
			if (CanUseCurrentOutputData())
			{
				return _loadedDeadFish.FishSO.IngredientSO.GraphicObjectPrefab != null;
			}
			return false;
		}

		public bool IsSavedIngredientAtOutput(Vector3 worldPosition)
		{
			if (_outputSourceObject == null)
			{
				return false;
			}
			Vector3 vector = base.transform.InverseTransformPoint(worldPosition);
			Vector3 outputDefaultLocalPosition = _outputDefaultLocalPosition;
			float x = Mathf.Clamp(min: Mathf.Min(-0.125f, outputDefaultLocalPosition.x), max: Mathf.Max(-0.125f, outputDefaultLocalPosition.x), value: vector.x);
			Vector3 vector2 = new Vector3(x, outputDefaultLocalPosition.y, outputDefaultLocalPosition.z);
			return (vector - vector2).sqrMagnitude <= 0.0144f;
		}

		private Vector3 GetOutputFallbackDropPosition()
		{
			Vector3 outputDefaultLocalPosition = _outputDefaultLocalPosition;
			float num = (Mathf.Approximately(outputDefaultLocalPosition.x, 0f) ? (-1f) : Mathf.Sign(outputDefaultLocalPosition.x));
			outputDefaultLocalPosition.x += num * 0.35f;
			outputDefaultLocalPosition.y += 0.1f;
			return base.transform.TransformPoint(outputDefaultLocalPosition);
		}

		private void SpawnOutputGraphics(int startIndex, int count)
		{
			if (!CanUseCurrentOutputGraphicData())
			{
				UtilityMethods.Log("[FilletMachine] Output graphic prefab is missing.", LogType.Warning);
				return;
			}
			for (int i = 0; i < count; i++)
			{
				int num = startIndex + i;
				Vector3 position = _outputSourceObject.position + (float)num * _loadedDeadFish.FishSO.StackOffset * Vector3.up;
				GameObject item = UnityEngine.Object.Instantiate(_loadedDeadFish.FishSO.IngredientSO.GraphicObjectPrefab, position, _outputSourceObject.rotation, _outputSourceObject);
				_spawnedFilletGraphics.Push(item);
			}
		}

		private void EnsureOutputGraphicCountMatchesYield()
		{
			if (CanUseCurrentOutputGraphicData() && _spawnedFilletGraphics.Count != _filletYield.Value)
			{
				DestroyChildren(_outputSourceObject);
				_spawnedFilletGraphics.Clear();
				SpawnOutputGraphics(0, _filletYield.Value);
			}
		}

		private int InvalidateOperationVersion()
		{
			_operationVersion++;
			return _operationVersion;
		}

		private bool IsOperationVersionCurrent(int operationVersion)
		{
			return _operationVersion == operationVersion;
		}

		private void CacheDefaultMachineState()
		{
			if (_machineRoot != null)
			{
				_machineRootDefaultLocalPosition = _machineRoot.localPosition;
				_machineRootDefaultLocalRotation = _machineRoot.localRotation;
			}
			if (_buttonTransform != null)
			{
				_buttonDefaultLocalPosition = _buttonTransform.localPosition;
				_buttonDefaultLocalRotation = _buttonTransform.localRotation;
			}
			if (_outputSourceObject != null)
			{
				_outputDefaultLocalPosition = _outputSourceObject.localPosition;
				_outputDefaultLocalRotation = _outputSourceObject.localRotation;
				_outputDefaultActive = _outputSourceObject.gameObject.activeSelf;
			}
			if (_sawTransform != null)
			{
				_sawDefaultLocalRotation = _sawTransform.localRotation;
			}
			if (_sawTransform2 != null)
			{
				_saw2DefaultLocalRotation = _sawTransform2.localRotation;
			}
		}

		private void ResetMachineLocalVisualState(bool restoreDefaultTransforms)
		{
			StopTimerIfRunning();
			KillMachineTweensAndRuntimeEffects();
			_loadInProgress = false;
			_loadedDeadFish = null;
			CleanupInputGraphics();
			if (restoreDefaultTransforms)
			{
				RestoreDefaultMachineTransforms();
			}
			if (_outputSourceObject != null)
			{
				_outputSourceObject.gameObject.SetActive(_outputDefaultActive);
			}
			if (_objToolTipGO != null)
			{
				_objToolTipGO.SetActive(value: false);
			}
			if (_buttonMaterial != null)
			{
				_buttonMaterial.color = _buttonDefaultColor;
				_buttonMaterial.SetColor("_EmissionColor", _buttonDefaultColor * _defaultIntensity);
			}
		}

		private void RestoreDefaultMachineTransforms()
		{
			if (_machineRoot != null)
			{
				_machineRoot.SetLocalPositionAndRotation(_machineRootDefaultLocalPosition, _machineRootDefaultLocalRotation);
			}
			if (_buttonTransform != null)
			{
				_buttonTransform.SetLocalPositionAndRotation(_buttonDefaultLocalPosition, _buttonDefaultLocalRotation);
			}
			if (_outputSourceObject != null)
			{
				_outputSourceObject.SetLocalPositionAndRotation(_outputDefaultLocalPosition, _outputDefaultLocalRotation);
			}
			if (_sawTransform != null)
			{
				_sawTransform.localRotation = _sawDefaultLocalRotation;
			}
			if (_sawTransform2 != null)
			{
				_sawTransform2.localRotation = _saw2DefaultLocalRotation;
			}
		}

		private void KillMachineTweensAndRuntimeEffects()
		{
			_sawTransform?.DOKill();
			_sawTransform2?.DOKill();
			_machineRoot?.DOKill();
			_buttonTransform?.DOKill();
			_outputSourceObject?.DOKill();
			if (_buttonMaterial != null)
			{
				DOTween.Kill(_buttonMaterial);
			}
			if (_loopFX != null)
			{
				_loopFX.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
			}
			if (_exitFX != null)
			{
				_exitFX.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
			}
			if (_audioSource != null)
			{
				_audioSource.Stop();
			}
		}

		private void StopTimerIfRunning()
		{
			if (_timerCoroutine != null)
			{
				StopCoroutine(_timerCoroutine);
				_timerCoroutine = null;
			}
		}

		private void CleanupInputGraphics()
		{
			if (_loadedFishGraphic != null)
			{
				UnityEngine.Object.Destroy(_loadedFishGraphic);
			}
			_loadedFishGraphic = null;
			DestroyChildren(_inputSourceObject);
			DestroyChildren(_inputSourceObjectBig);
			DestroyChildren(_outputSourceObject);
			_spawnedFilletGraphics.Clear();
		}

		private void DestroyChildren(Transform root)
		{
			if (!(root == null))
			{
				for (int num = root.childCount - 1; num >= 0; num--)
				{
					UnityEngine.Object.Destroy(root.GetChild(num).gameObject);
				}
			}
		}

		private void ResetMachineSafe()
		{
			InvalidateOperationVersion();
			ResetMachineLocalVisualState(restoreDefaultTransforms: false);
			if (base.IsServer)
			{
				ServerRecoverToEmpty("ResetMachineSafe was requested.", discardLoadedFish: true);
			}
		}

		private void RefreshMachineHintsAfterInputConsumed(ulong ownerClientId)
		{
			if (!(Singleton<PlayersManager>.Instance == null) && !(Singleton<PlayersManager>.Instance.LocalPlayer == null) && Singleton<PlayersManager>.Instance.LocalPlayer.OwnerClientId == ownerClientId)
			{
				RefreshGazeFeedback();
				Singleton<DelayHelper>.Instance.DelayActionUntilEndOfFrame(() =>
				{
					RefreshGazeFeedback();
				});
				Singleton<DelayHelper>.Instance.DelayAction(0.05f, () =>
				{
					RefreshGazeFeedback();
				});
			}
		}

		public bool ServerStaffClaim(int id)
		{
			if (id <= 0 || !ServerStaffAvailable)
			{
				return false;
			}
			_staffEmployee.Value = id;
			return true;
		}

		public void ServerStaffRelease(int id)
		{
			if (this.CanWriteServerState() && _staffEmployee.Value == id)
			{
				_staffFish.Value = default;
				_staffEmployee.Value = 0;
			}
		}

		public void ServerStaffBegin(int id, string fishId)
		{
			if (base.IsServer && _staffEmployee.Value == id)
			{
				_staffFish.Value = fishId;
				if (fishId == StaffWorkplaceRegistry.Instance?.Database?.MackerelSO?.Id)
				{
					StaffQuestProgress.Complete(QuestId.PlaceMackerelInFilletMachine);
				}
				StaffQuestProgress.Complete(QuestId.StartTheFilletMachine);
			}
		}

		public void ServerStaffComplete(int id)
		{
			if (this.CanWriteServerState() && id > 0 && _staffEmployee.Value == id && !_staffFish.Value.IsEmpty)
			{
				bool num = ShouldStartCompassEventForCurrentOperation();
				ServerStaffRelease(id);
				if (num)
				{
					ServerEnableCompassRpc();
				}
			}
		}

		private void StaffFishChanged(FixedString64Bytes before, FixedString64Bytes after)
		{
			if (_staffFishVisual != null)
			{
				UnityEngine.Object.Destroy(_staffFishVisual);
			}
			if (_loopFX != null)
			{
				if (after.IsEmpty)
				{
					_loopFX.Stop();
				}
				else
				{
					_loopFX.Play();
				}
			}
			if (!after.IsEmpty && !(_inputSourceObject == null) && StaffWorkplaceRegistry.Instance?.Database?.FindById(after.ToString()) is FishSO fishSO && fishSO.DeadGraphic != null)
			{
				Transform parent = ((fishSO.FilletBigInput && _inputSourceObjectBig != null) ? _inputSourceObjectBig : _inputSourceObject);
				_staffFishVisual = UnityEngine.Object.Instantiate(fishSO.DeadGraphic, parent);
				ItemOffset filletOffset = fishSO.FilletOffset;
				if (filletOffset.ScaleOffset != Vector3.zero)
				{
					_staffFishVisual.transform.localScale = filletOffset.ScaleOffset;
				}
				_staffFishVisual.transform.SetLocalPositionAndRotation(filletOffset.PositionOffset, Quaternion.Euler(filletOffset.RotationOffset));
				Collider[] componentsInChildren = _staffFishVisual.GetComponentsInChildren<Collider>();
				for (int i = 0; i < componentsInChildren.Length; i++)
				{
					componentsInChildren[i].enabled = false;
				}
			}
		}

		private void LateUpdate()
		{
			if (!_staffFish.Value.IsEmpty)
			{
				if (_sawTransform != null)
				{
					_sawTransform.Rotate(Vector3.forward, 360f * Time.deltaTime);
				}
				if (_sawTransform2 != null)
				{
					_sawTransform2.Rotate(Vector3.forward, 360f * Time.deltaTime);
				}
			}
		}

		protected override void __initializeVariables()
		{
			if (_state == null)
			{
				throw new Exception("FilletMachine._state cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_state.Initialize(this);
			__nameNetworkVariable(_state, "_state");
			NetworkVariableFields.Add(_state);
			if (_filletYield == null)
			{
				throw new Exception("FilletMachine._filletYield cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_filletYield.Initialize(this);
			__nameNetworkVariable(_filletYield, "_filletYield");
			NetworkVariableFields.Add(_filletYield);
			if (_loadedFishReference == null)
			{
				throw new Exception("FilletMachine._loadedFishReference cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_loadedFishReference.Initialize(this);
			__nameNetworkVariable(_loadedFishReference, "_loadedFishReference");
			NetworkVariableFields.Add(_loadedFishReference);
			if (_staffEmployee == null)
			{
				throw new Exception("FilletMachine._staffEmployee cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_staffEmployee.Initialize(this);
			__nameNetworkVariable(_staffEmployee, "_staffEmployee");
			NetworkVariableFields.Add(_staffEmployee);
			if (_staffFish == null)
			{
				throw new Exception("FilletMachine._staffFish cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_staffFish.Initialize(this);
			__nameNetworkVariable(_staffFish, "_staffFish");
			NetworkVariableFields.Add(_staffFish);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(3609356508u, __rpc_handler_3609356508, "ServerEnableCompassRpc", RpcInvokePermission.Everyone);
			__registerRpc(3737698339u, __rpc_handler_3737698339, "ClientEnableCompassRpc", RpcInvokePermission.Everyone);
			__registerRpc(2685536099u, __rpc_handler_2685536099, "ServerDisableCompassRpc", RpcInvokePermission.Everyone);
			__registerRpc(2100103264u, __rpc_handler_2100103264, "ClientDisableCompassRpc", RpcInvokePermission.Everyone);
			__registerRpc(1533963084u, __rpc_handler_1533963084, "ServerTakeFilletOutputRpc", RpcInvokePermission.Everyone);
			__registerRpc(1914686566u, __rpc_handler_1914686566, "ClientPlayTakeFilletSfxRpc", RpcInvokePermission.Everyone);
			__registerRpc(4153021801u, __rpc_handler_4153021801, "ServerUpdateStateRpc", RpcInvokePermission.Everyone);
			__registerRpc(2617503868u, __rpc_handler_2617503868, "ServerUpdateYieldRpc", RpcInvokePermission.Everyone);
			__registerRpc(2417250426u, __rpc_handler_2417250426, "ServerDestroyLoadedFishRpc", RpcInvokePermission.Everyone);
			__registerRpc(2767525284u, __rpc_handler_2767525284, "ClientDestroyLoadedFishRpc", RpcInvokePermission.Everyone);
			__registerRpc(2850751229u, __rpc_handler_2850751229, "ServerLoadFishRpc", RpcInvokePermission.Everyone);
			__registerRpc(4025622129u, __rpc_handler_4025622129, "ClientLoadFishRpc", RpcInvokePermission.Everyone);
			__registerRpc(153628090u, __rpc_handler_153628090, "ClientLoadFishRejectedRpc", RpcInvokePermission.Everyone);
			__registerRpc(2636773693u, __rpc_handler_2636773693, "ClientEndDayCleanupRpc", RpcInvokePermission.Everyone);
			__registerRpc(2194133395u, __rpc_handler_2194133395, "ClientRecoverToEmptyRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_3609356508(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((FilletMachine)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FilletMachine)target).ServerEnableCompassRpc();
				((FilletMachine)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3737698339(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((FilletMachine)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FilletMachine)target).ClientEnableCompassRpc();
				((FilletMachine)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2685536099(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				((FilletMachine)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FilletMachine)target).ServerDisableCompassRpc(value);
				((FilletMachine)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2100103264(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				((FilletMachine)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FilletMachine)target).ClientDisableCompassRpc(value);
				((FilletMachine)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1533963084(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out bool value2, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((FilletMachine)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FilletMachine)target).ServerTakeFilletOutputRpc(value, value2, ext);
				((FilletMachine)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1914686566(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((FilletMachine)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FilletMachine)target).ClientPlayTakeFilletSfxRpc();
				((FilletMachine)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4153021801(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				((FilletMachine)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FilletMachine)target).ServerUpdateStateRpc(value);
				((FilletMachine)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2617503868(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				((FilletMachine)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FilletMachine)target).ServerUpdateYieldRpc(value);
				((FilletMachine)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2417250426(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((FilletMachine)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FilletMachine)target).ServerDestroyLoadedFishRpc();
				((FilletMachine)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2767525284(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((FilletMachine)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FilletMachine)target).ClientDestroyLoadedFishRpc();
				((FilletMachine)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2850751229(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value2);
				((FilletMachine)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FilletMachine)target).ServerLoadFishRpc(value, value2);
				((FilletMachine)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4025622129(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value2);
				((FilletMachine)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FilletMachine)target).ClientLoadFishRpc(value, value2);
				((FilletMachine)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_153628090(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				((FilletMachine)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FilletMachine)target).ClientLoadFishRejectedRpc(value);
				((FilletMachine)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2636773693(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((FilletMachine)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FilletMachine)target).ClientEndDayCleanupRpc();
				((FilletMachine)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2194133395(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((FilletMachine)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FilletMachine)target).ClientRecoverToEmptyRpc();
				((FilletMachine)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "FilletMachine";
		}
	}
}
