using System;
using System.Collections.Generic;
using DG.Tweening;
using Deform;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PaperPixelGames.Core.Kitchen.ChefDesk
{
	public class ChefDeskSushiRoll : NetworkBehaviour, IPointerDownHandler, IEventSystemHandler, IPointerUpHandler, IDragHandler
	{
		private readonly struct SynchronousDeformableState
		{
			public readonly Deformable Deformable;

			public readonly UpdateMode OriginalUpdateMode;

			public readonly bool RestoreUpdateMode;

			public SynchronousDeformableState(Deformable deformable, UpdateMode originalUpdateMode, bool restoreUpdateMode)
			{
				Deformable = deformable;
				OriginalUpdateMode = originalUpdateMode;
				RestoreUpdateMode = restoreUpdateMode;
			}
		}

		internal enum RollVisualAxis : byte
		{
			None = 0,
			Vertical = 1,
			Horizontal = 2
		}

		internal struct RollNetworkState : INetworkSerializable, IEquatable<RollNetworkState>
		{
			public byte Axis;

			public ushort QuantizedProgress;

			public bool Completed;

			public float Progress => (float)(int)QuantizedProgress / 65535f;

			public RollNetworkState(RollVisualAxis axis, float progress, bool completed = false)
			{
				Axis = (byte)axis;
				QuantizedProgress = (ushort)Mathf.RoundToInt(Mathf.Clamp01(progress) * 65535f);
				Completed = completed;
			}

			public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
			{
				serializer.SerializeValue(ref Axis);
				serializer.SerializeValue(ref QuantizedProgress, default(FastBufferWriter.ForPrimitives));
				serializer.SerializeValue(ref Completed, default(FastBufferWriter.ForPrimitives));
			}

			public bool Equals(RollNetworkState other)
			{
				if (Axis == other.Axis && QuantizedProgress == other.QuantizedProgress)
				{
					return Completed == other.Completed;
				}
				return false;
			}
		}

		[Header("References")]
		[SerializeField]
		private ChefDesk _chefDesk;

		[Header("Mat Target (3D Object)")]
		[SerializeField]
		private Transform sushiMatBend;

		[Header("UI Track (Pointer Area)")]
		[SerializeField]
		private RectTransform track;

		[Header("Vertical Roll Limits (Z Axis)")]
		[SerializeField]
		private float minZ;

		[SerializeField]
		private float maxZ;

		[Header("Horizontal Roll Limits (X Axis)")]
		[SerializeField]
		private float minX;

		[SerializeField]
		private float maxX;

		[Header("Settings")]
		[SerializeField]
		[Min(0.001f)]
		private float _rollDragMultiplier = 4f;

		[Header("Temaki Ingredient Presentation")]
		[SerializeField]
		[Range(0.1f, 1f)]
		private float _temakiIngredientFinalScale = 0.4f;

		[SerializeField]
		[Min(0f)]
		private float _temakiIngredientUpwardOffset;

		[SerializeField]
		private Vector3 _temakiIngredientRolledLocalPositionOffset = new Vector3(0f, -0.01f, 0f);

		[SerializeField]
		private Vector3 _temakiIngredientRolledLocalRotationOffset = new Vector3(0f, 15f, 0f);

		[SerializeField]
		private Vector3 _temakiIngredientFinalRootLocalPosition = new Vector3(0f, 0.8f, -0.015f);

		[SerializeField]
		private Vector3 _temakiIngredientFinalRootLocalEulerAngles = new Vector3(-72f, 18f, 0f);

		[Header("Thresholds")]
		private const float FinishThresholdT = 0.98f;

		private const float SnapThresholdT = 0.995f;

		[Header("Network Visual Sync")]
		[SerializeField]
		[Min(0f)]
		private float _rollVisualBroadcastInterval = 0.08f;

		[SerializeField]
		[Min(0f)]
		private float _rollVisualBroadcastMinDelta = 0.01f;

		[SerializeField]
		[Min(0.1f)]
		private float _rollReliableCheckpointInterval = 0.5f;

		[Header("Roll Loop Audio")]
		[SerializeField]
		private AudioSource _rollLoopAudioSource;

		[SerializeField]
		[Range(0f, 1f)]
		private float _rollLoopMinVolume = 0.05f;

		[SerializeField]
		[Range(0f, 1f)]
		private float _rollLoopMaxVolume = 0.75f;

		[SerializeField]
		[Min(0.001f)]
		private float _rollLoopInputSpeedForMaxVolume = 1.5f;

		[SerializeField]
		[Min(0f)]
		private float _rollLoopVolumeRiseSpeed = 12f;

		[SerializeField]
		[Min(0f)]
		private float _rollLoopVolumeFallSpeed = 8f;

		[SerializeField]
		[Min(0f)]
		private float _rollLoopInputDecaySpeed = 4f;

		private bool _dragging;

		private float _grabT;

		private float _grabPointerT;

		private float _lastZ;

		private float _lastX;

		private float _defaultMatZ;

		private float _defaultMatX;

		private float _nextRollVisualBroadcastTime;

		private float _nextReliableRollCheckpointTime;

		private float _lastBroadcastRollValue;

		private RollVisualAxis _lastBroadcastRollAxis;

		private float _rollLoopTargetVolume;

		private float _rollLoopCurrentVolume;

		private float _lastRejectedRollInputLogTime = float.NegativeInfinity;

		private readonly List<SynchronousDeformableState> _synchronousDeformables = new List<SynchronousDeformableState>();

		private SushiType _synchronousDeformableType;

		private bool _deformRefreshPending;

		private bool _rollPresentationCompleted;

		private bool _temakiIngredientPresentationPrepared;

		private Vector3 _temakiIngredientBaseLocalPosition;

		private Quaternion _temakiIngredientBaseLocalRotation = Quaternion.identity;

		private Vector3 _temakiIngredientBaseLocalScale = Vector3.one;

		private const float BendPoseSampleDistance = 0.001f;

		private readonly NetworkVariable<RollNetworkState> _networkRollState = new NetworkVariable<RollNetworkState>(new RollNetworkState(RollVisualAxis.None, 0f));

		private float NormalizedZ(float z)
		{
			return Mathf.InverseLerp(minZ, maxZ, z);
		}

		private float NormalizedX(float x)
		{
			return Mathf.InverseLerp(minX, maxX, x);
		}

		private void Awake()
		{
			CacheDefaults();
			InitializeRollLoopAudio();
		}

		private void OnEnable()
		{
			CacheDefaults();
			ResetInternalState();
			StopRollLoopAudioImmediate();
		}

		private void OnDisable()
		{
			RestoreSynchronousDeformables();
			StopRollLoopAudioImmediate();
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			NetworkVariable<RollNetworkState> networkRollState = _networkRollState;
			networkRollState.OnValueChanged = (NetworkVariable<RollNetworkState>.OnValueChangedDelegate)Delegate.Combine(networkRollState.OnValueChanged, new NetworkVariable<RollNetworkState>.OnValueChangedDelegate(OnNetworkRollStateChanged));
			ClientApplyNetworkRollState(_networkRollState.Value);
		}

		public override void OnNetworkDespawn()
		{
			NetworkVariable<RollNetworkState> networkRollState = _networkRollState;
			networkRollState.OnValueChanged = (NetworkVariable<RollNetworkState>.OnValueChangedDelegate)Delegate.Remove(networkRollState.OnValueChanged, new NetworkVariable<RollNetworkState>.OnValueChangedDelegate(OnNetworkRollStateChanged));
			RestoreSynchronousDeformables();
			base.OnNetworkDespawn();
		}

		private void Update()
		{
			UpdateRollLoopAudio();
		}

		private void LateUpdate()
		{
			ApplyPendingDeformationNow();
		}

		private void CacheDefaults()
		{
			if (sushiMatBend != null)
			{
				_defaultMatZ = sushiMatBend.localPosition.z;
				_defaultMatX = sushiMatBend.localPosition.x;
			}
		}

		private void ResetInternalState()
		{
			_dragging = false;
			_rollPresentationCompleted = false;
			_temakiIngredientPresentationPrepared = false;
			_temakiIngredientBaseLocalPosition = Vector3.zero;
			_temakiIngredientBaseLocalRotation = Quaternion.identity;
			_temakiIngredientBaseLocalScale = Vector3.one;
			_lastZ = _defaultMatZ;
			_lastX = _defaultMatX;
		}

		private void InitializeRollLoopAudio()
		{
			if (!(_rollLoopAudioSource == null))
			{
				_rollLoopAudioSource.loop = true;
				_rollLoopAudioSource.playOnAwake = false;
				_rollLoopAudioSource.volume = 0f;
			}
		}

		public void OnPointerDown(PointerEventData e)
		{
			if (CanInteract())
			{
				if (sushiMatBend != null)
				{
					sushiMatBend.DOKill();
				}
				switch (_chefDesk.CurrentSushiType)
				{
				case SushiType.Maki:
				case SushiType.Uramaki:
					BeginVertical(e);
					break;
				case SushiType.Temaki:
					BeginHorizontal(e);
					break;
				}
				if (_dragging)
				{
					StartRollLoopAudio();
				}
			}
		}

		public void OnDrag(PointerEventData e)
		{
			if (CanInteract() && _dragging)
			{
				switch (_chefDesk.CurrentSushiType)
				{
				case SushiType.Maki:
				case SushiType.Uramaki:
					DragVertical(e);
					break;
				case SushiType.Temaki:
					DragHorizontal(e);
					break;
				}
			}
		}

		public void OnPointerUp(PointerEventData e)
		{
			if (CanInteract() && _dragging)
			{
				switch (_chefDesk.CurrentSushiType)
				{
				case SushiType.Maki:
				case SushiType.Uramaki:
					EndVertical();
					break;
				case SushiType.Temaki:
					EndHorizontal();
					break;
				}
				StopRollLoopAudio();
			}
		}

		private bool CanInteract()
		{
			if (_chefDesk == null || sushiMatBend == null || track == null)
			{
				return false;
			}
			if (_chefDesk.IsOwner)
			{
				return _chefDesk.CurrentState == ChefDeskState.WaitingForRoll;
			}
			return false;
		}

		private void BeginVertical(PointerEventData e)
		{
			SushiType currentSushiType = _chefDesk.CurrentSushiType;
			ChefDesk.SushiVisualSet value = null;
			_chefDesk.VisualMap?.TryGetValue(currentSushiType, out value);
			PrepareSynchronousDeformables(currentSushiType, value);
			_grabT = Mathf.InverseLerp(minZ, maxZ, sushiMatBend.localPosition.z);
			_grabPointerT = PointerT(e, horizontal: false);
			_dragging = true;
			_lastZ = sushiMatBend.localPosition.z;
		}

		private void DragVertical(PointerEventData e)
		{
			float num = PointerT(e, horizontal: false);
			float num2 = Mathf.Clamp01(_grabT + (num - _grabPointerT));
			float num3 = Mathf.Lerp(minZ, maxZ, num2);
			float normalizedInputDelta = GetNormalizedInputDelta(_lastZ, num3, minZ, maxZ);
			ClientApplyVerticalLocal(num3);
			UpdateRollLoopInputVolume(normalizedInputDelta);
			BroadcastRollProgress(RollVisualAxis.Vertical, num2);
		}

		private void EndVertical()
		{
			_dragging = false;
			StopRollLoopAudio();
			if (Mathf.Clamp01(NormalizedZ(_lastZ)) >= 0.98f)
			{
				ClientApplyVerticalLocal(maxZ);
				ApplyPendingDeformationNow();
				ServerRequestCompleteRollRpc(1, 1f);
			}
		}

		private void ClientApplyVerticalLocal(float z)
		{
			float num = NormalizedZ(z);
			if (num >= 0.995f)
			{
				z = maxZ;
			}
			ChefDesk.SushiVisualSet value = null;
			_chefDesk.VisualMap?.TryGetValue(_chefDesk.CurrentSushiType, out value);
			PrepareSynchronousDeformables(_chefDesk.CurrentSushiType, value);
			Vector3 localPosition = sushiMatBend.localPosition;
			localPosition.z = z;
			sushiMatBend.localPosition = localPosition;
			if (value != null)
			{
				if (value.SushiRiceBend != null)
				{
					SetVerticalBendAxisPosition(value.SushiRiceBend, z);
				}
				if (value.SingleNoriBend != null)
				{
					SetVerticalBendAxisPosition(value.SingleNoriBend, z);
				}
				AlignVerticalShellBendCoresToMat(value);
				if (value.SushiRiceBend != null)
				{
					value.SushiRiceBend.localPosition += value.RiceBendRollOffset;
				}
				if (value.SingleNoriBend != null)
				{
					value.SingleNoriBend.localPosition += value.NoriBendRollOffset;
				}
			}
			ApplyVerticalIngredientAnchors(value, num);
			_deformRefreshPending = true;
			_lastZ = z;
		}

		private void ApplyVerticalIngredientAnchors(ChefDesk.SushiVisualSet visual, float rollProgress)
		{
			_chefDesk.ClientEnsureAssemblyLayout();
			if (_chefDesk.AssemblyIngredientAnchors == null || _chefDesk.AssemblyIngredientDefaultPositions == null)
			{
				return;
			}
			Transform transform = _chefDesk.ClientGetAssemblyIngredientRoot();
			BendDeformer bendDeformer = ResolveVerticalIngredientBend(_chefDesk.CurrentSushiType, visual);
			if (transform == null || bendDeformer == null || bendDeformer.Axis == null)
			{
				return;
			}
			float num = Mathf.Clamp01(rollProgress);
			bool flag = TryGetBendCoreWorldPosition(bendDeformer, out var center);
			for (int i = 0; i < _chefDesk.AssemblyIngredientAnchors.Count; i++)
			{
				Transform transform2 = _chefDesk.AssemblyIngredientAnchors[i];
				if (!(transform2 == null) && i < _chefDesk.AssemblyIngredientDefaultPositions.Count)
				{
					Vector3 position = _chefDesk.AssemblyIngredientDefaultPositions[i];
					Quaternion quaternion = ((i < _chefDesk.AssemblyIngredientDefaultRotations.Count) ? _chefDesk.AssemblyIngredientDefaultRotations[i] : Quaternion.identity);
					Vector3 flatWorldPosition = transform.TransformPoint(position);
					Quaternion flatWorldRotation = transform.rotation * quaternion;
					EvaluateBendPose(bendDeformer, flatWorldPosition, flatWorldRotation, out var deformedWorldPosition, out var deformedWorldRotation);
					transform2.SetPositionAndRotation(deformedWorldPosition, deformedWorldRotation);
					if (flag && ChefDesk.ClientTryGetRendererBoundsCenter(transform2, out var center2))
					{
						transform2.position += (center - center2) * num;
					}
				}
			}
		}

		private void BeginHorizontal(PointerEventData e)
		{
			ChefDesk.SushiVisualSet value = null;
			_chefDesk.VisualMap?.TryGetValue(SushiType.Temaki, out value);
			if (value == null || value.SingleNoriBend == null)
			{
				UtilityMethods.Log("<color=red>[ChefDeskSushiRoll] Temaki SingleNoriBend missing.</color>");
				return;
			}
			_defaultMatX = value.SingleNoriBend.localPosition.x;
			_grabT = Mathf.InverseLerp(minX, maxX, value.SingleNoriBend.localPosition.x);
			_grabPointerT = PointerT(e, horizontal: true);
			_dragging = true;
			_lastX = value.SingleNoriBend.localPosition.x;
		}

		private void DragHorizontal(PointerEventData e)
		{
			float num = PointerT(e, horizontal: true);
			float num2 = Mathf.Clamp01(_grabT - (num - _grabPointerT));
			float num3 = Mathf.Lerp(minX, maxX, num2);
			float normalizedInputDelta = GetNormalizedInputDelta(_lastX, num3, minX, maxX);
			ClientApplyHorizontalLocal(num3);
			UpdateRollLoopInputVolume(normalizedInputDelta);
			BroadcastRollProgress(RollVisualAxis.Horizontal, num2);
		}

		private void EndHorizontal()
		{
			_dragging = false;
			StopRollLoopAudio();
			if (Mathf.Clamp01(NormalizedX(_lastX)) >= 0.98f)
			{
				ClientApplyHorizontalLocal(maxX);
				ApplyPendingDeformationNow();
				ServerRequestCompleteRollRpc(2, 1f);
			}
		}

		private void ClientApplyHorizontalLocal(float x)
		{
			float num = Mathf.Clamp01(NormalizedX(x));
			if (num >= 0.995f)
			{
				x = maxX;
				num = 1f;
			}
			ChefDesk.SushiVisualSet value = null;
			_chefDesk.VisualMap?.TryGetValue(SushiType.Temaki, out value);
			PrepareSynchronousDeformables(SushiType.Temaki, value);
			Vector3 localPosition = sushiMatBend.localPosition;
			localPosition.x = x;
			sushiMatBend.localPosition = localPosition;
			if (value != null)
			{
				if (value.SushiRiceBend != null)
				{
					Vector3 localPosition2 = value.SushiRiceBend.localPosition - value.RiceBendRollOffset;
					localPosition2.x = x;
					localPosition2 += value.RiceBendRollOffset;
					value.SushiRiceBend.localPosition = localPosition2;
				}
				if (value.SingleNoriBend != null)
				{
					Vector3 localPosition3 = value.SingleNoriBend.localPosition - value.NoriBendRollOffset;
					localPosition3.x = x;
					localPosition3 += value.NoriBendRollOffset;
					value.SingleNoriBend.localPosition = localPosition3;
				}
				AlignHorizontalShellBendCores(value);
			}
			ApplyHorizontalIngredientAnchors(value, num);
			_deformRefreshPending = true;
			_lastX = x;
		}

		private void ApplyHorizontalIngredientAnchors(ChefDesk.SushiVisualSet visual, float rollProgress)
		{
			PrepareTemakiIngredientPresentation();
			if (!_temakiIngredientPresentationPrepared || _chefDesk.AssemblyIngredientAnchors == null || _chefDesk.AssemblyIngredientDefaultPositions == null)
			{
				return;
			}
			Transform transform = _chefDesk.ClientGetAssemblyIngredientRoot();
			BendDeformer bendDeformer = ResolveHorizontalIngredientBend(visual);
			if (transform == null || bendDeformer == null || bendDeformer.Axis == null)
			{
				return;
			}
			float num = Mathf.Clamp01(rollProgress);
			float num2 = Mathf.Clamp(_temakiIngredientFinalScale, 0.1f, 1f);
			transform.localPosition = Vector3.Lerp(_temakiIngredientBaseLocalPosition, _temakiIngredientFinalRootLocalPosition, num);
			transform.localRotation = Quaternion.Slerp(_temakiIngredientBaseLocalRotation, Quaternion.Euler(_temakiIngredientFinalRootLocalEulerAngles), num);
			transform.localScale = Vector3.Lerp(_temakiIngredientBaseLocalScale, _temakiIngredientBaseLocalScale * num2, num);
			Vector3 vector = Vector3.up * (_temakiIngredientUpwardOffset * num);
			Vector3 vector2 = Vector3.Lerp(Vector3.zero, _temakiIngredientRolledLocalPositionOffset, num);
			Quaternion quaternion = Quaternion.Euler(Vector3.Lerp(Vector3.zero, _temakiIngredientRolledLocalRotationOffset, num));
			if (_chefDesk.AssemblyIngredientDefaultPositions.Count == 0)
			{
				return;
			}
			Vector3 position = _chefDesk.AssemblyIngredientDefaultPositions[0];
			Quaternion quaternion2 = ((_chefDesk.AssemblyIngredientDefaultRotations.Count > 0) ? _chefDesk.AssemblyIngredientDefaultRotations[0] : Quaternion.identity);
			Vector3 flatWorldPosition = transform.TransformPoint(position);
			Quaternion flatWorldRotation = transform.rotation * quaternion2;
			EvaluateBendPose(bendDeformer, flatWorldPosition, flatWorldRotation, out var deformedWorldPosition, out var deformedWorldRotation);
			Vector3 b = deformedWorldPosition + vector + deformedWorldRotation * vector2;
			for (int i = 0; i < _chefDesk.AssemblyIngredientAnchors.Count; i++)
			{
				Transform transform2 = _chefDesk.AssemblyIngredientAnchors[i];
				if (!(transform2 == null) && i < _chefDesk.AssemblyIngredientDefaultPositions.Count)
				{
					Vector3 position2 = _chefDesk.AssemblyIngredientDefaultPositions[i];
					Quaternion quaternion3 = ((i < _chefDesk.AssemblyIngredientDefaultRotations.Count) ? _chefDesk.AssemblyIngredientDefaultRotations[i] : Quaternion.identity);
					Vector3 flatWorldPosition2 = transform.TransformPoint(position2);
					Quaternion flatWorldRotation2 = transform.rotation * quaternion3;
					EvaluateBendPose(bendDeformer, flatWorldPosition2, flatWorldRotation2, out var deformedWorldPosition2, out var deformedWorldRotation2);
					Vector3 a = deformedWorldPosition2 + vector + deformedWorldRotation2 * vector2;
					transform2.SetPositionAndRotation(Vector3.Lerp(a, b, num), deformedWorldRotation2 * quaternion);
				}
			}
		}

		private void PrepareTemakiIngredientPresentation()
		{
			if (!_temakiIngredientPresentationPrepared)
			{
				_chefDesk.ClientEnsureAssemblyLayout();
				Transform transform = _chefDesk.ClientGetAssemblyIngredientRoot();
				if (!(transform == null) && _chefDesk.AssemblyIngredientAnchors != null)
				{
					_chefDesk.ClientResetAssemblyAnchorTransforms();
					_temakiIngredientBaseLocalPosition = transform.localPosition;
					_temakiIngredientBaseLocalRotation = transform.localRotation;
					_temakiIngredientBaseLocalScale = transform.localScale;
					_temakiIngredientPresentationPrepared = true;
				}
			}
		}

		internal void ClientPrepareRollPresentation()
		{
			if (!(_chefDesk == null) && _chefDesk.CurrentSushiType == SushiType.Temaki)
			{
				RollNetworkState value = _networkRollState.Value;
				float t = ((value.Axis == 2) ? value.Progress : 0f);
				ClientApplyHorizontalLocal(Mathf.Lerp(minX, maxX, t));
				ApplyPendingDeformationNow();
			}
		}

		private void PrepareSynchronousDeformables(SushiType sushiType, ChefDesk.SushiVisualSet visual)
		{
			if (_synchronousDeformableType != sushiType)
			{
				RestoreSynchronousDeformables();
				_synchronousDeformableType = sushiType;
			}
			RegisterSynchronousDeformable((sushiMatBend != null) ? sushiMatBend.GetComponentInParent<Deformable>() : null);
			RegisterSynchronousDeformable((visual?.SingleNoriOBJ != null) ? visual.SingleNoriOBJ.GetComponent<Deformable>() : null);
			RegisterSynchronousDeformable((visual?.SushiRiceOBJ != null) ? visual.SushiRiceOBJ.GetComponent<Deformable>() : null);
		}

		private void RegisterSynchronousDeformable(Deformable deformable, bool restoreUpdateMode = true)
		{
			if (deformable == null || !deformable.isActiveAndEnabled)
			{
				return;
			}
			for (int i = 0; i < _synchronousDeformables.Count; i++)
			{
				if (_synchronousDeformables[i].Deformable == deformable)
				{
					return;
				}
			}
			UpdateMode updateMode = deformable.UpdateMode;
			switch (updateMode)
			{
			case UpdateMode.Pause:
			case UpdateMode.Stop:
				return;
			default:
				deformable.UpdateMode = UpdateMode.Custom;
				break;
			case UpdateMode.Custom:
				break;
			}
			_synchronousDeformables.Add(new SynchronousDeformableState(deformable, updateMode, restoreUpdateMode));
		}

		private void RestoreSynchronousDeformables()
		{
			_deformRefreshPending = false;
			for (int i = 0; i < _synchronousDeformables.Count; i++)
			{
				SynchronousDeformableState synchronousDeformableState = _synchronousDeformables[i];
				if (synchronousDeformableState.RestoreUpdateMode && !(synchronousDeformableState.Deformable == null) && synchronousDeformableState.Deformable.UpdateMode != synchronousDeformableState.OriginalUpdateMode)
				{
					synchronousDeformableState.Deformable.UpdateMode = synchronousDeformableState.OriginalUpdateMode;
				}
			}
			_synchronousDeformables.Clear();
			_synchronousDeformableType = SushiType.Empty;
		}

		private void ResetActiveRollMeshesToOriginal()
		{
			ResetActiveDeformableMesh((sushiMatBend != null) ? sushiMatBend.GetComponentInParent<Deformable>() : null);
			if (_chefDesk?.VisualMap == null)
			{
				return;
			}
			foreach (KeyValuePair<SushiType, ChefDesk.SushiVisualSet> item in _chefDesk.VisualMap)
			{
				ChefDesk.SushiVisualSet value = item.Value;
				if (value != null)
				{
					ResetActiveDeformableMesh((value.SingleNoriOBJ != null) ? value.SingleNoriOBJ.GetComponent<Deformable>() : null);
					ResetActiveDeformableMesh((value.SushiRiceOBJ != null) ? value.SushiRiceOBJ.GetComponent<Deformable>() : null);
				}
			}
		}

		private static void ResetActiveDeformableMesh(Deformable deformable)
		{
			if (!(deformable == null) && deformable.isActiveAndEnabled)
			{
				UpdateMode updateMode = deformable.UpdateMode;
				deformable.UpdateMode = UpdateMode.Stop;
				if (updateMode != UpdateMode.Stop)
				{
					deformable.UpdateMode = updateMode;
				}
			}
		}

		private void ApplyPendingDeformationNow()
		{
			if (!_deformRefreshPending)
			{
				return;
			}
			_deformRefreshPending = false;
			for (int i = 0; i < _synchronousDeformables.Count; i++)
			{
				Deformable deformable = _synchronousDeformables[i].Deformable;
				if (!(deformable == null) && deformable.isActiveAndEnabled)
				{
					deformable.ForceImmediateUpdate();
				}
			}
		}

		private static void SetVerticalBendAxisPosition(Transform bendTransform, float z)
		{
			bendTransform.localPosition = new Vector3(0f, 0f, z);
		}

		private void AlignVerticalShellBendCoresToMat(ChefDesk.SushiVisualSet visual)
		{
			if (TryGetBendCoreWorldPosition((sushiMatBend != null) ? sushiMatBend.GetComponent<BendDeformer>() : null, out var center))
			{
				AlignVerticalBendCore(visual.SingleNoriBend, center);
				AlignVerticalBendCore(visual.SushiRiceBend, center);
			}
		}

		private static void AlignVerticalBendCore(Transform bendTransform, Vector3 targetCore)
		{
			if (!(bendTransform == null))
			{
				BendDeformer component = bendTransform.GetComponent<BendDeformer>();
				if (TryGetBendCoreWorldPosition(component, out var center))
				{
					Vector3 vector = targetCore - center;
					vector.x = 0f;
					component.Axis.position += vector;
				}
			}
		}

		private static void AlignHorizontalShellBendCores(ChefDesk.SushiVisualSet visual)
		{
			if (!TryGetBendCoreWorldPosition(ResolveHorizontalIngredientBend(visual), out var center))
			{
				return;
			}
			Transform transform = visual?.SushiRiceBend;
			if (!(transform == null))
			{
				BendDeformer component = transform.GetComponent<BendDeformer>();
				if (TryGetBendCoreWorldPosition(component, out var center2))
				{
					Vector3 vector = center - center2;
					vector.z = 0f;
					component.Axis.position += vector;
				}
			}
		}

		private static BendDeformer ResolveVerticalIngredientBend(SushiType sushiType, ChefDesk.SushiVisualSet visual)
		{
			Transform transform = ((sushiType != SushiType.Uramaki) ? visual?.SingleNoriBend : visual?.SushiRiceBend);
			if (!(transform != null))
			{
				return null;
			}
			return transform.GetComponent<BendDeformer>();
		}

		private static BendDeformer ResolveHorizontalIngredientBend(ChefDesk.SushiVisualSet visual)
		{
			if (!(visual?.SingleNoriBend != null))
			{
				return null;
			}
			return visual.SingleNoriBend.GetComponent<BendDeformer>();
		}

		internal bool ClientTryGetVerticalRollCore(out Vector3 center)
		{
			center = Vector3.zero;
			if (_chefDesk == null || _chefDesk.VisualMap == null)
			{
				return false;
			}
			SushiType currentSushiType = _chefDesk.CurrentSushiType;
			if ((currentSushiType != SushiType.Maki && currentSushiType != SushiType.Uramaki) || !_chefDesk.VisualMap.TryGetValue(currentSushiType, out var value))
			{
				return false;
			}
			return TryGetBendCoreWorldPosition(ResolveVerticalIngredientBend(currentSushiType, value), out center);
		}

		private static bool TryGetBendCoreWorldPosition(BendDeformer bend, out Vector3 center)
		{
			center = Vector3.zero;
			if (bend == null || bend.Axis == null)
			{
				return false;
			}
			float num = bend.Angle * bend.Factor;
			float num2 = bend.Top - bend.Bottom;
			if (Mathf.Abs(num) < bend.minValidBendAngle || Mathf.Approximately(num2, 0f))
			{
				return false;
			}
			float num3 = num * (MathF.PI / 180f);
			float x = num2 / num3;
			center = bend.Axis.TransformPoint(new Vector3(x, bend.Bottom, 0f));
			return true;
		}

		private static void EvaluateBendPose(BendDeformer bend, Vector3 flatWorldPosition, Quaternion flatWorldRotation, out Vector3 deformedWorldPosition, out Quaternion deformedWorldRotation)
		{
			deformedWorldPosition = EvaluateBendPoint(bend, flatWorldPosition);
			Vector3 worldPoint = flatWorldPosition + flatWorldRotation * Vector3.forward * 0.001f;
			Vector3 worldPoint2 = flatWorldPosition + flatWorldRotation * Vector3.up * 0.001f;
			Vector3 vector = EvaluateBendPoint(bend, worldPoint) - deformedWorldPosition;
			Vector3 vector2 = EvaluateBendPoint(bend, worldPoint2) - deformedWorldPosition;
			if (vector.sqrMagnitude < 1E-10f || vector2.sqrMagnitude < 1E-10f)
			{
				deformedWorldRotation = flatWorldRotation;
				return;
			}
			vector.Normalize();
			vector2.Normalize();
			if (Vector3.Cross(vector, vector2).sqrMagnitude < 1E-06f)
			{
				deformedWorldRotation = flatWorldRotation;
			}
			else
			{
				deformedWorldRotation = Quaternion.LookRotation(vector, vector2);
			}
		}

		private static Vector3 EvaluateBendPoint(BendDeformer bend, Vector3 worldPoint)
		{
			if (bend == null || bend.Axis == null)
			{
				return worldPoint;
			}
			float num = bend.Angle * bend.Factor;
			float num2 = bend.Top - bend.Bottom;
			if (Mathf.Abs(num) < bend.minValidBendAngle || Mathf.Approximately(num2, 0f))
			{
				return worldPoint;
			}
			float num3 = num * (MathF.PI / 180f);
			Transform axis = bend.Axis;
			Vector3 vector = axis.InverseTransformPoint(worldPoint);
			Vector3 vector2 = vector;
			float num4 = vector.y;
			if (bend.TopMode == BoundsMode.Limited)
			{
				num4 = Mathf.Min(bend.Top, num4);
			}
			if (bend.BottomMode == BoundsMode.Limited)
			{
				num4 = Mathf.Max(bend.Bottom, num4);
			}
			float num5 = (num4 - bend.Bottom) / num2 * num3;
			float num6 = Mathf.Cos(MathF.PI - num5);
			float num7 = Mathf.Sin(MathF.PI - num5);
			float num8 = num2 / num3;
			float x = vector.x;
			vector.x = num8 * num6 + num8 - x * num6;
			vector.y = num8 * num7 - x * num7;
			if (bend.TopMode == BoundsMode.Limited && vector2.y > bend.Top)
			{
				vector.y += (0f - num6) * (vector2.y - bend.Top);
				vector.x += num7 * (vector2.y - bend.Top);
			}
			else if (bend.BottomMode == BoundsMode.Limited && vector2.y < bend.Bottom)
			{
				vector.y += (0f - num6) * (vector2.y - bend.Bottom);
				vector.x += num7 * (vector2.y - bend.Bottom);
			}
			vector.y += bend.Bottom;
			return axis.TransformPoint(vector);
		}

		private void BroadcastRollProgress(RollVisualAxis axis, float progress, bool force = false)
		{
			progress = Mathf.Clamp01(progress);
			if (force || ShouldBroadcastRollProgress(axis, progress))
			{
				_lastBroadcastRollAxis = axis;
				_lastBroadcastRollValue = progress;
				_nextRollVisualBroadcastTime = Time.unscaledTime + _rollVisualBroadcastInterval;
				ServerSubmitRollProgressRpc((byte)axis, progress);
			}
		}

		private bool ShouldBroadcastRollProgress(RollVisualAxis axis, float progress)
		{
			if (Time.unscaledTime < _nextRollVisualBroadcastTime)
			{
				return false;
			}
			if (_lastBroadcastRollAxis != axis)
			{
				return true;
			}
			return Mathf.Abs(progress - _lastBroadcastRollValue) >= _rollVisualBroadcastMinDelta;
		}

		[Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
		private void ServerSubmitRollProgressRpc(byte axisValue, float progress, RpcParams rpcParams = default(RpcParams))
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
				RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
				{
					Delivery = RpcDelivery.Unreliable
				};
				FastBufferWriter bufferWriter = __beginSendRpc(370355440u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Unreliable);
				bufferWriter.WriteValueSafe(in axisValue, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in progress, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 370355440u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Unreliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (_chefDesk != null && _chefDesk.StaffLocked)
			{
				return;
			}
			RollVisualAxis axis = (RollVisualAxis)axisValue;
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (ServerCanAcceptRollInput(senderClientId, axis))
			{
				RollNetworkState rollNetworkState = new RollNetworkState(axis, progress);
				ClientPreviewRollProgressRpc(rollNetworkState);
				if (Time.unscaledTime >= _nextReliableRollCheckpointTime)
				{
					_networkRollState.Value = rollNetworkState;
					_nextReliableRollCheckpointTime = Time.unscaledTime + Mathf.Max(0.1f, _rollReliableCheckpointInterval);
				}
			}
		}

		[Rpc(SendTo.Everyone, Delivery = RpcDelivery.Unreliable)]
		private void ClientPreviewRollProgressRpc(RollNetworkState state)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
				{
					Delivery = RpcDelivery.Unreliable
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(2030098834u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Unreliable);
				bufferWriter.WriteValueSafe(in state, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 2030098834u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Unreliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ClientApplyNetworkRollState(state);
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerRequestCompleteRollRpc(byte axisValue, float finalProgress, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3854723588u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in axisValue, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in finalProgress, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3854723588u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (_chefDesk != null && _chefDesk.StaffLocked)
			{
				return;
			}
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			RollVisualAxis rollAxis = GetRollAxis((_chefDesk != null) ? _chefDesk.CurrentSushiType : SushiType.Empty);
			RollVisualAxis rollVisualAxis = (RollVisualAxis)axisValue;
			if (rollVisualAxis == rollAxis && ServerCanAcceptRollInput(senderClientId, rollAxis))
			{
				float num = Mathf.Clamp01(finalProgress);
				if (!float.IsFinite(finalProgress) || num < 0.98f)
				{
					UtilityMethods.Log($"[ChefDesk Roll] Completion rejected. SenderClientId={senderClientId}, " + $"ExpectedAxis={rollAxis}, ReceivedAxis={rollVisualAxis}, " + $"Progress={finalProgress:0.000}, Required={0.98f:0.000}", LogType.Warning);
				}
				else if (_chefDesk.ServerTryBeginCenterAssembly(senderClientId, num))
				{
					_networkRollState.Value = new RollNetworkState(rollAxis, 1f, completed: true);
				}
			}
		}

		internal void ServerPrepareRollSession(SushiType sushiType)
		{
			if (base.IsServer)
			{
				_nextReliableRollCheckpointTime = 0f;
				_networkRollState.Value = new RollNetworkState(GetRollAxis(sushiType), 0f);
			}
		}

		internal void ServerResetRollSession()
		{
			if (base.IsServer)
			{
				_nextReliableRollCheckpointTime = 0f;
				_networkRollState.Value = new RollNetworkState(RollVisualAxis.None, 0f);
			}
		}

		private bool ServerCanAcceptRollInput(ulong senderClientId, RollVisualAxis axis)
		{
			RollVisualAxis rollAxis = GetRollAxis((_chefDesk != null) ? _chefDesk.CurrentSushiType : SushiType.Empty);
			string text = null;
			if (_chefDesk == null)
			{
				text = "ChefDeskMissing";
			}
			else if (!_chefDesk.ServerIsActiveChefDeskUser(senderClientId))
			{
				text = "SenderIsNotActiveUser";
			}
			else if (_chefDesk.CurrentState != ChefDeskState.WaitingForRoll)
			{
				text = $"InvalidState:{_chefDesk.CurrentState}";
			}
			else if (axis == RollVisualAxis.None || axis != rollAxis)
			{
				text = $"InvalidAxis:{axis},Expected:{rollAxis}";
			}
			if (text == null)
			{
				return true;
			}
			if (Time.unscaledTime - _lastRejectedRollInputLogTime >= 1f)
			{
				_lastRejectedRollInputLogTime = Time.unscaledTime;
				UtilityMethods.Log($"[ChefDesk Roll] Input rejected. SenderClientId={senderClientId}, Reason={text}", LogType.Warning);
			}
			return false;
		}

		private void OnNetworkRollStateChanged(RollNetworkState previous, RollNetworkState current)
		{
			ClientApplyNetworkRollState(current);
		}

		private void ClientApplyNetworkRollState(RollNetworkState state)
		{
			if (_chefDesk == null || _dragging)
			{
				return;
			}
			RollVisualAxis axis = (RollVisualAxis)state.Axis;
			if (axis == RollVisualAxis.None)
			{
				return;
			}
			if (state.Completed)
			{
				ClientCompleteRollPresentation();
				return;
			}
			_rollPresentationCompleted = false;
			_chefDesk.ClientEnsureAssemblyLayout();
			switch (axis)
			{
			case RollVisualAxis.Vertical:
				ClientApplyVerticalLocal(Mathf.Lerp(minZ, maxZ, state.Progress));
				break;
			case RollVisualAxis.Horizontal:
				ClientApplyHorizontalLocal(Mathf.Lerp(minX, maxX, state.Progress));
				break;
			}
		}

		private static RollVisualAxis GetRollAxis(SushiType sushiType)
		{
			return sushiType switch
			{
				SushiType.Maki => RollVisualAxis.Vertical, 
				SushiType.Uramaki => RollVisualAxis.Vertical, 
				SushiType.Temaki => RollVisualAxis.Horizontal, 
				_ => RollVisualAxis.None, 
			};
		}

		internal void ClientReleaseRollMat(float duration = 0.35f)
		{
			RestoreSynchronousDeformables();
			ClientMoveRollMatToDefault(duration);
		}

		internal void ClientCompleteRollPresentation()
		{
			if (!_rollPresentationCompleted && !(_chefDesk == null))
			{
				switch (GetRollAxis(_chefDesk.CurrentSushiType))
				{
				default:
					return;
				case RollVisualAxis.Vertical:
					ClientApplyVerticalLocal(maxZ);
					break;
				case RollVisualAxis.Horizontal:
					ClientApplyHorizontalLocal(maxX);
					break;
				}
				ApplyPendingDeformationNow();
				_rollPresentationCompleted = true;
				ClientReleaseRollMat();
			}
		}

		internal void ClientResetRollPresentation()
		{
			_dragging = false;
			_rollPresentationCompleted = false;
			_temakiIngredientPresentationPrepared = false;
			_temakiIngredientBaseLocalPosition = Vector3.zero;
			_temakiIngredientBaseLocalRotation = Quaternion.identity;
			_temakiIngredientBaseLocalScale = Vector3.one;
			StopRollLoopAudioImmediate();
			ClientMoveRollMatToDefault(0f);
			ResetActiveRollMeshesToOriginal();
			RestoreSynchronousDeformables();
			_lastZ = _defaultMatZ;
			_lastX = _defaultMatX;
		}

		private void ClientMoveRollMatToDefault(float duration)
		{
			if (!(sushiMatBend == null))
			{
				sushiMatBend.DOKill();
				Vector3 localPosition = sushiMatBend.localPosition;
				localPosition.z = _defaultMatZ;
				localPosition.x = _defaultMatX;
				if (duration <= 0f)
				{
					sushiMatBend.localPosition = localPosition;
				}
				else
				{
					sushiMatBend.DOLocalMove(localPosition, duration).SetEase(Ease.OutSine).SetLink(base.gameObject, LinkBehaviour.KillOnDestroy);
				}
			}
		}

		private float PointerT(PointerEventData e, bool horizontal)
		{
			RectTransformUtility.ScreenPointToLocalPointInRectangle(track, e.position, e.pressEventCamera, out var localPoint);
			if (horizontal)
			{
				float num = track.rect.width * 0.5f;
				float value = Mathf.Clamp(localPoint.x, 0f - num, num);
				return Mathf.Clamp01((Mathf.InverseLerp(num, 0f - num, value) - 0.5f) * _rollDragMultiplier + 0.5f);
			}
			float num2 = track.rect.height * 0.5f;
			float value2 = Mathf.Clamp(localPoint.y, 0f - num2, num2);
			return Mathf.Clamp01((Mathf.InverseLerp(0f - num2, num2, value2) - 0.5f) * _rollDragMultiplier + 0.5f);
		}

		private void StartRollLoopAudio()
		{
			if (!(_rollLoopAudioSource == null) && !(_rollLoopAudioSource.clip == null))
			{
				_rollLoopTargetVolume = _rollLoopMinVolume;
				if (!_rollLoopAudioSource.isPlaying)
				{
					_rollLoopAudioSource.Play();
				}
			}
		}

		private void StopRollLoopAudio()
		{
			_rollLoopTargetVolume = 0f;
		}

		private void StopRollLoopAudioImmediate()
		{
			_rollLoopTargetVolume = 0f;
			_rollLoopCurrentVolume = 0f;
			if (!(_rollLoopAudioSource == null))
			{
				_rollLoopAudioSource.volume = 0f;
				if (_rollLoopAudioSource.isPlaying)
				{
					_rollLoopAudioSource.Stop();
				}
			}
		}

		private void UpdateRollLoopAudio()
		{
			if (!(_rollLoopAudioSource == null))
			{
				if (_dragging)
				{
					_rollLoopTargetVolume = Mathf.MoveTowards(_rollLoopTargetVolume, _rollLoopMinVolume, _rollLoopInputDecaySpeed * Time.unscaledDeltaTime);
				}
				float num = ((_rollLoopTargetVolume > _rollLoopCurrentVolume) ? _rollLoopVolumeRiseSpeed : _rollLoopVolumeFallSpeed);
				_rollLoopCurrentVolume = Mathf.MoveTowards(_rollLoopCurrentVolume, _rollLoopTargetVolume, num * Time.unscaledDeltaTime);
				_rollLoopAudioSource.volume = _rollLoopCurrentVolume;
				if (!_dragging && _rollLoopCurrentVolume <= 0.001f && _rollLoopAudioSource.isPlaying)
				{
					_rollLoopAudioSource.Stop();
				}
			}
		}

		private void UpdateRollLoopInputVolume(float normalizedDelta)
		{
			if (!(_rollLoopAudioSource == null))
			{
				if (!_rollLoopAudioSource.isPlaying && _rollLoopAudioSource.clip != null)
				{
					_rollLoopAudioSource.Play();
				}
				float num = Mathf.Max(Time.unscaledDeltaTime, 0.001f);
				float value = normalizedDelta / num;
				float t = Mathf.InverseLerp(0f, _rollLoopInputSpeedForMaxVolume, value);
				_rollLoopTargetVolume = Mathf.Lerp(_rollLoopMinVolume, _rollLoopMaxVolume, t);
			}
		}

		private static float GetNormalizedInputDelta(float previousValue, float currentValue, float minValue, float maxValue)
		{
			float num = Mathf.Abs(maxValue - minValue);
			if (num <= 0.0001f)
			{
				return 0f;
			}
			return Mathf.Clamp01(Mathf.Abs(currentValue - previousValue) / num);
		}

		protected override void __initializeVariables()
		{
			if (_networkRollState == null)
			{
				throw new Exception("ChefDeskSushiRoll._networkRollState cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_networkRollState.Initialize(this);
			__nameNetworkVariable(_networkRollState, "_networkRollState");
			NetworkVariableFields.Add(_networkRollState);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(370355440u, __rpc_handler_370355440, "ServerSubmitRollProgressRpc", RpcInvokePermission.Everyone);
			__registerRpc(2030098834u, __rpc_handler_2030098834, "ClientPreviewRollProgressRpc", RpcInvokePermission.Everyone);
			__registerRpc(3854723588u, __rpc_handler_3854723588, "ServerRequestCompleteRollRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_370355440(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value2, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((ChefDeskSushiRoll)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDeskSushiRoll)target).ServerSubmitRollProgressRpc(value, value2, ext);
				((ChefDeskSushiRoll)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2030098834(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out RollNetworkState value, default(FastBufferWriter.ForNetworkSerializable));
				((ChefDeskSushiRoll)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDeskSushiRoll)target).ClientPreviewRollProgressRpc(value);
				((ChefDeskSushiRoll)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3854723588(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value2, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((ChefDeskSushiRoll)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDeskSushiRoll)target).ServerRequestCompleteRollRpc(value, value2, ext);
				((ChefDeskSushiRoll)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "ChefDeskSushiRoll";
		}
	}
}
