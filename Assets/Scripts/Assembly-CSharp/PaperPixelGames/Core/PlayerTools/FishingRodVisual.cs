using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Data.Tools;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Utility;
using UnityEngine;

namespace PaperPixelGames.Core.PlayerTools
{
	[DefaultExecutionOrder(-200)]
	public sealed class FishingRodVisual : MonoBehaviour
	{
		private const string BendBonePrefix = "Rod_Bend_";

		private const string ReelCrankName = "Reel_Crank";

		private const string ReelCrankHandleName = "Reel_Crank_Handle";

		private const string ReelHandTargetName = "Reel_Hand_Target";

		private const string LineTipName = "Line_Tip";

		private static readonly AnimationCurve DefaultBendProfile = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.45f, 1f), new Keyframe(1f, 0f));

		private readonly List<Transform> _bendBones = new List<Transform>(8);

		private readonly Vector3[] _targetBoneEuler = new Vector3[8];

		private readonly Vector3[] _currentBoneEuler = new Vector3[8];

		private FishingRodData _data;

		private PPG_Player _player;

		private Quaternion[] _bendRestRotations = Array.Empty<Quaternion>();

		private Transform _reelCrank;

		private Transform _reelCrankHandle;

		private Transform _reelHandTarget;

		private Transform _lineTip;

		private Quaternion _reelCrankRestRotation;

		private Vector3 _reelHandTargetBaseLocalPosition;

		private Quaternion _reelHandTargetBaseLocalRotation = Quaternion.identity;

		private float _reelCrankAngle;

		private bool _reeling;

		private bool _useFpsAnimator;

		private bool _initialized;

		public Transform LineOrigin
		{
			get
			{
				if (!(_lineTip != null))
				{
					return base.transform;
				}
				return _lineTip;
			}
		}

		public Transform ReelHandTarget => _reelHandTarget;

		public void Initialize(FishingRodData data, PPG_Player player = null, bool useFpsAnimator = false)
		{
			_data = data;
			_player = player;
			_useFpsAnimator = useFpsAnimator;
			ResolveRigReferences();
			CacheRestPose();
			if (_player != null)
			{
				ConfigureReelHandTarget();
			}
			_initialized = true;
		}

		public void SetPresentation(float normalizedTension, bool reeling, bool challengeActive, float presentationStrength, FishingRodFishDirection fishDirection, float caughtFishWeightBend = 0f)
		{
			if (_initialized)
			{
				float num = Mathf.Max(0f, presentationStrength);
				float bendAmount;
				if (challengeActive)
				{
					bendAmount = Mathf.Clamp01(Mathf.Lerp(_data.MinimumChallengeBend, 1f, Mathf.Clamp01(normalizedTension)) * num);
				}
				else if (caughtFishWeightBend > 0f)
				{
					fishDirection = FishingRodFishDirection.Forward;
					bendAmount = Mathf.Clamp01(caughtFishWeightBend);
				}
				else
				{
					bendAmount = 0f;
				}
				PopulateTargetBoneEuler(fishDirection, reeling, bendAmount);
				_reeling = challengeActive & reeling;
			}
		}

		public void ResetPose()
		{
			if (_initialized)
			{
				Array.Clear(_targetBoneEuler, 0, _targetBoneEuler.Length);
				Array.Clear(_currentBoneEuler, 0, _currentBoneEuler.Length);
				_reeling = false;
				_reelCrankAngle = 0f;
				ApplyBendPose();
				ApplyReelPose();
			}
		}

		public static Vector3 ResolveRootDirectionEuler(FishingRodFishDirection fishDirection, bool challengeActive, float presentationStrength, Vector3 horizontalTipEuler, Vector3 forwardTipEuler)
		{
			if (!challengeActive)
			{
				return Vector3.zero;
			}
			return fishDirection switch
			{
				FishingRodFishDirection.Left => horizontalTipEuler, 
				FishingRodFishDirection.Right => MirrorHorizontalTipEuler(horizontalTipEuler), 
				FishingRodFishDirection.Forward => forwardTipEuler, 
				_ => Vector3.zero, 
			} * Mathf.Max(0f, presentationStrength);
		}

		private static Vector3 MirrorHorizontalTipEuler(Vector3 leftEuler)
		{
			return new Vector3(leftEuler.x, 0f - leftEuler.y, leftEuler.z);
		}

		private static Vector3 MirrorHorizontalBodyEuler(Vector3 leftEuler)
		{
			return new Vector3(leftEuler.x, 0f - leftEuler.y, 0f - leftEuler.z);
		}

		private void Update()
		{
			if (!_initialized || _data == null)
			{
				return;
			}
			UpdateReelHandTargetPose();
			if (_reeling)
			{
				if (_player != null && _player.TryGetFishingRodReelPhase(_useFpsAnimator, out var normalizedPhase))
				{
					_reelCrankAngle = normalizedPhase * 360f * _data.ReelCrankTurnsPerAnimationLoop + _data.ReelCrankPhaseOffsetDegrees;
				}
				else
				{
					_reelCrankAngle = Mathf.Repeat(_reelCrankAngle + _data.ReelCrankDegreesPerSecond * Time.deltaTime, 360f);
				}
			}
			ApplyReelPose();
		}

		private void LateUpdate()
		{
			if (_initialized && !(_data == null))
			{
				float t = 1f - Mathf.Exp((0f - _data.RodBendResponse) * Time.deltaTime);
				for (int i = 0; i < _currentBoneEuler.Length; i++)
				{
					_currentBoneEuler[i] = Vector3.Lerp(_currentBoneEuler[i], _targetBoneEuler[i], t);
				}
				ApplyBendPose();
			}
		}

		private void OnDisable()
		{
			ResetPose();
		}

		private void ResolveRigReferences()
		{
			_bendBones.Clear();
			_reelCrank = null;
			_reelCrankHandle = null;
			_reelHandTarget = null;
			_lineTip = null;
			Transform[] componentsInChildren = GetComponentsInChildren<Transform>(includeInactive: true);
			Dictionary<string, Transform> dictionary = new Dictionary<string, Transform>(StringComparer.Ordinal);
			foreach (Transform transform in componentsInChildren)
			{
				dictionary.TryAdd(transform.name, transform);
			}
			for (int j = 1; j <= 8; j++)
			{
				string key = string.Format("{0}{1:00}", "Rod_Bend_", j);
				if (dictionary.TryGetValue(key, out var value))
				{
					_bendBones.Add(value);
				}
			}
			dictionary.TryGetValue("Reel_Crank", out _reelCrank);
			dictionary.TryGetValue("Reel_Crank_Handle", out _reelCrankHandle);
			dictionary.TryGetValue("Line_Tip", out _lineTip);
			if (_bendBones.Count != 8)
			{
				UtilityMethods.Log($"[FishingRodVisual] Expected {8} bend bones but found {_bendBones.Count}.", LogType.Warning);
			}
			if (_reelCrank == null)
			{
				UtilityMethods.Log("[FishingRodVisual] Reel_Crank was not found.", LogType.Warning);
			}
			if (_reelCrankHandle == null)
			{
				UtilityMethods.Log("[FishingRodVisual] Reel_Crank_Handle was not found. Reel hand IK will be disabled.", LogType.Warning);
			}
			if (_lineTip == null)
			{
				UtilityMethods.Log("[FishingRodVisual] Line_Tip was not found. The visual root will be used as the line origin.", LogType.Warning);
			}
		}

		private void CacheRestPose()
		{
			_bendRestRotations = new Quaternion[_bendBones.Count];
			for (int i = 0; i < _bendBones.Count; i++)
			{
				_bendRestRotations[i] = _bendBones[i].localRotation;
			}
			if (_reelCrank != null)
			{
				_reelCrankRestRotation = _reelCrank.localRotation;
			}
		}

		private void ConfigureReelHandTarget()
		{
			if (!(_reelCrank == null) && !(_data == null))
			{
				ResolveReelHandTargetBasePose();
				Transform transform = _reelCrank.Find("Reel_Hand_Target");
				if (transform != null)
				{
					_reelHandTarget = transform;
				}
				else
				{
					GameObject gameObject = new GameObject("Reel_Hand_Target");
					_reelHandTarget = gameObject.transform;
					_reelHandTarget.SetParent(_reelCrank, worldPositionStays: false);
				}
				UpdateReelHandTargetPose();
			}
		}

		private void ResolveReelHandTargetBasePose()
		{
			_reelHandTargetBaseLocalPosition = Vector3.zero;
			_reelHandTargetBaseLocalRotation = Quaternion.identity;
			if (!(_reelCrank == null) && !(_reelCrankHandle == null))
			{
				Renderer renderer = _reelCrankHandle.GetComponent<Renderer>() ?? _reelCrankHandle.GetComponentInChildren<Renderer>();
				Vector3 position = ((renderer != null) ? renderer.bounds.center : _reelCrankHandle.position);
				_reelHandTargetBaseLocalPosition = _reelCrank.InverseTransformPoint(position);
				_reelHandTargetBaseLocalRotation = Quaternion.Inverse(_reelCrank.rotation) * _reelCrankHandle.rotation;
			}
		}

		private void UpdateReelHandTargetPose()
		{
			if (!(_reelHandTarget == null) && !(_data == null))
			{
				_reelHandTarget.SetLocalPositionAndRotation(_reelHandTargetBaseLocalPosition + _data.ReelHandTargetLocalPosition, _reelHandTargetBaseLocalRotation * Quaternion.Euler(_data.ReelHandTargetLocalEuler));
			}
		}

		private void ApplyBendPose()
		{
			if (!(_data == null) && _bendBones.Count != 0)
			{
				for (int i = 0; i < _bendBones.Count; i++)
				{
					_bendBones[i].localRotation = _bendRestRotations[i] * Quaternion.Euler(_currentBoneEuler[i]);
				}
			}
		}

		private void PopulateTargetBoneEuler(FishingRodFishDirection fishDirection, bool reeling, float bendAmount)
		{
			Array.Clear(_targetBoneEuler, 0, _targetBoneEuler.Length);
			switch (fishDirection)
			{
			case FishingRodFishDirection.Left:
			case FishingRodFishDirection.Right:
			{
				bool flag = fishDirection == FishingRodFishDirection.Right;
				for (int j = 0; j < _targetBoneEuler.Length; j++)
				{
					Vector3 leftBoneEulerOffset = GetLeftBoneEulerOffset(j);
					_targetBoneEuler[j] = (flag ? MirrorHorizontalBodyEuler(leftBoneEulerOffset) : leftBoneEulerOffset) * bendAmount;
				}
				return;
			}
			case FishingRodFishDirection.Forward:
			{
				for (int i = 0; i < _targetBoneEuler.Length; i++)
				{
					_targetBoneEuler[i] = GetForwardBoneEulerOffset(i) * bendAmount;
				}
				return;
			}
			}
			FishingRodFishDirection fishingRodFishDirection = fishDirection;
			Vector3 vector;
			if (fishingRodFishDirection != FishingRodFishDirection.Centered)
			{
				if (fishingRodFishDirection != FishingRodFishDirection.Tired || !reeling)
				{
					goto IL_00ed;
				}
				vector = _data.MaxRodBendEuler * _data.ReelCurveMultiplier;
			}
			else
			{
				if (!reeling)
				{
					goto IL_00ed;
				}
				vector = _data.MaxRodBendEuler * _data.ReelCurveMultiplier;
			}
			goto IL_00f4;
			IL_00f4:
			Vector3 vector2 = vector;
			AnimationCurve animationCurve = ((_data.RodBendProfile != null && _data.RodBendProfile.length >= 2) ? _data.RodBendProfile : DefaultBendProfile);
			float num = animationCurve.Evaluate(0f);
			for (int k = 0; k < _targetBoneEuler.Length; k++)
			{
				float time = ((float)k + 1f) / (float)_targetBoneEuler.Length;
				float num2 = animationCurve.Evaluate(time);
				float num3 = num2 - num;
				_targetBoneEuler[k] = vector2 * num3 * bendAmount;
				num = num2;
			}
			return;
			IL_00ed:
			vector = Vector3.zero;
			goto IL_00f4;
		}

		private Vector3 GetLeftBoneEulerOffset(int index)
		{
			return _data.GetLeftRodBoneEulerOffset(index);
		}

		private Vector3 GetForwardBoneEulerOffset(int index)
		{
			return _data.GetForwardRodBoneEulerOffset(index);
		}

		private void ApplyReelPose()
		{
			if (!(_reelCrank == null))
			{
				Vector3 axis = ((_data.ReelCrankLocalAxis.sqrMagnitude > 0.0001f) ? _data.ReelCrankLocalAxis.normalized : Vector3.forward);
				_reelCrank.localRotation = _reelCrankRestRotation * Quaternion.AngleAxis(_reelCrankAngle, axis);
			}
		}
	}
}
