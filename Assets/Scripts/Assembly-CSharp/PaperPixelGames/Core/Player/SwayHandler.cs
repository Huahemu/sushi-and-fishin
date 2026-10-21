using System;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Settings;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;

namespace PaperPixelGames.Core.Player
{
	public sealed class SwayHandler : MonoBehaviour
	{
		[Header("Target")]
		[SerializeField]
		private Transform _target;

		[Header("Time")]
		[SerializeField]
		private bool _useUnscaledTime;

		[Header("Look Input")]
		[SerializeField]
		[Min(0f)]
		private float _deadZone = 0.01f;

		[Header("Sway Position")]
		[SerializeField]
		[Min(0f)]
		private float _posStep = 0.0025f;

		[SerializeField]
		[Min(0f)]
		private float _maxPos = 0.04f;

		[Header("Sway Rotation")]
		[SerializeField]
		[Min(0f)]
		private float _rotStep = 1.8f;

		[SerializeField]
		[Min(0f)]
		private float _maxRot = 6f;

		[Header("Bob (Viewmodel)")]
		[SerializeField]
		private bool _enableBob = true;

		[SerializeField]
		[Min(0f)]
		private float _walkBobFrequency = 1.65f;

		[SerializeField]
		[Min(0f)]
		private float _runBobFrequency = 2.35f;

		[SerializeField]
		[Min(0f)]
		private float _bobPosAmount = 0.02f;

		[SerializeField]
		[Min(0f)]
		private float _bobRotAmount = 0.85f;

		[SerializeField]
		[Range(0f, 1f)]
		private float _strafeBobBias = 0.65f;

		[Header("Idle (Viewmodel)")]
		[SerializeField]
		private bool _enableIdle = true;

		[SerializeField]
		[Min(0f)]
		private float _idleFrequency = 0.25f;

		[SerializeField]
		[Min(0f)]
		private float _idlePosAmount = 0.004f;

		[SerializeField]
		[Min(0f)]
		private float _idleRotAmount = 0.25f;

		[SerializeField]
		[Range(0f, 1f)]
		private float _idleWeight = 1f;

		[Header("Scripted Sway")]
		[SerializeField]
		private bool _enableScriptedSway = true;

		[SerializeField]
		[Min(0f)]
		private float _scriptedSmoothTime = 0.12f;

		[Header("Smoothing")]
		[SerializeField]
		[Min(0.001f)]
		private float _smoothTime = 0.05f;

		[Header("Damage Hit Feedback")]
		[SerializeField]
		private bool _enableDamageHit = true;

		[SerializeField]
		[Min(0f)]
		private float _damageHitPitch = 6.5f;

		[SerializeField]
		[Min(0f)]
		private float _damageHitYaw = 4f;

		[SerializeField]
		[Min(0f)]
		private float _damageHitRoll = 7.5f;

		[SerializeField]
		[Min(0.001f)]
		private float _damageHitReturnTime = 0.12f;

		[SerializeField]
		[Min(0.001f)]
		private float _damageHitRecoverTime = 0.2f;

		[Header("UI Overlay Sway")]
		[SerializeField]
		private bool _enableUiOverlaySway;

		[SerializeField]
		private RectTransform _uiOverlayTarget;

		[SerializeField]
		[Min(0f)]
		private float _uiPositionMultiplier = 900f;

		[SerializeField]
		[Min(0f)]
		private float _uiRotationMultiplier = 1f;

		[SerializeField]
		private Vector2 _uiMaxOffset = new Vector2(36f, 24f);

		[SerializeField]
		[Min(0f)]
		private float _uiExtraIdleAmount = 4f;

		[SerializeField]
		[Min(0f)]
		private float _uiExtraIdleFrequency = 0.35f;

		private Vector2 _uiBaseAnchoredPos;

		private Quaternion _uiBaseLocalRot;

		private Vector3 _damageRotCurrent;

		private Vector3 _damageRotVel;

		private Vector3 _damageRotTarget;

		private Vector3 _baseLocalPos;

		private Quaternion _baseLocalRot;

		private Vector2 _lookDelta;

		private bool _hasLookThisFrame;

		private Vector3 _swayPosCurrent;

		private Vector3 _swayPosVel;

		private Vector3 _swayRotEulerCurrent;

		private Vector3 _swayRotVelEuler;

		private bool _isMoving;

		private bool _isRunning;

		private float _speed01;

		private float _strafe01;

		private bool _movementSwaySuppressed;

		private float _bobPhase;

		private Vector3 _bobPosCurrent;

		private Vector3 _bobPosVel;

		private Vector3 _bobRotEulerCurrent;

		private Vector3 _bobRotVelEuler;

		private float _idlePhase;

		private Vector3 _idlePosCurrent;

		private Vector3 _idlePosVel;

		private Vector3 _idleRotEulerCurrent;

		private Vector3 _idleRotVelEuler;

		private float _idleWeightRuntime = 1f;

		private bool _isGrounded = true;

		private bool _scriptedActive;

		private float _scriptedWeight = 1f;

		private float _scriptedFrequency = 1f;

		private Vector3 _scriptedPosAmplitude;

		private Vector3 _scriptedRotAmplitude;

		private float _scriptedPhaseOffset;

		private float _scriptedTime;

		private bool _scriptedUsesDirectTarget;

		private Vector3 _scriptedDirectPosTarget;

		private Vector3 _scriptedDirectRotTarget;

		private float _scriptedDirectSmoothTime = 0.12f;

		private bool _scriptedIdleWeightOverrideActive;

		private float _scriptedPreviousIdleWeightRuntime = 1f;

		private Vector3 _scriptedPosCurrent;

		private Vector3 _scriptedPosVel;

		private Vector3 _scriptedRotEulerCurrent;

		private Vector3 _scriptedRotVelEuler;

		private Transform Target
		{
			get
			{
				if (!(_target != null))
				{
					return base.transform;
				}
				return _target;
			}
		}

		private void Awake()
		{
			if (_target == null)
			{
				_target = base.transform;
			}
			CacheBasePose();
			_uiOverlayTarget = Singleton<GameUIManager>.Instance.UIGogglesTarget;
			CacheUI();
			EnableGogglesUI(s: false);
		}

		private void OnEnable()
		{
			CacheBasePose();
			_lookDelta = Vector2.zero;
			_hasLookThisFrame = false;
			_swayPosCurrent = Vector3.zero;
			_swayPosVel = Vector3.zero;
			_swayRotEulerCurrent = Vector3.zero;
			_swayRotVelEuler = Vector3.zero;
			_bobPosCurrent = Vector3.zero;
			_bobPosVel = Vector3.zero;
			_bobRotEulerCurrent = Vector3.zero;
			_bobRotVelEuler = Vector3.zero;
			_bobPhase = 0f;
			_idlePhase = 0f;
			_idlePosCurrent = Vector3.zero;
			_idlePosVel = Vector3.zero;
			_idleRotEulerCurrent = Vector3.zero;
			_idleRotVelEuler = Vector3.zero;
			_scriptedTime = 0f;
			_scriptedUsesDirectTarget = false;
			_scriptedDirectPosTarget = Vector3.zero;
			_scriptedDirectRotTarget = Vector3.zero;
			_scriptedPosCurrent = Vector3.zero;
			_scriptedPosVel = Vector3.zero;
			_scriptedRotEulerCurrent = Vector3.zero;
			_scriptedRotVelEuler = Vector3.zero;
			_damageRotCurrent = Vector3.zero;
			_damageRotVel = Vector3.zero;
			_damageRotTarget = Vector3.zero;
			if (_uiOverlayTarget != null)
			{
				_uiOverlayTarget.anchoredPosition = _uiBaseAnchoredPos;
				_uiOverlayTarget.localRotation = _uiBaseLocalRot;
			}
		}

		private void OnDisable()
		{
			StopScriptedSway();
		}

		private void LateUpdate()
		{
			float num = (_useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
			if (num <= 0f)
			{
				return;
			}
			bool viewmodelMotionEnabled = RuntimeSettings.ViewmodelMotionEnabled;
			Vector2 vector = ((viewmodelMotionEnabled && _hasLookThisFrame) ? _lookDelta : Vector2.zero);
			_lookDelta = Vector2.zero;
			_hasLookThisFrame = false;
			if (vector.sqrMagnitude < _deadZone * _deadZone)
			{
				vector = Vector2.zero;
			}
			float value = (0f - vector.x) * _posStep;
			float value2 = (0f - vector.y) * _posStep;
			value = Mathf.Clamp(value, 0f - _maxPos, _maxPos);
			value2 = Mathf.Clamp(value2, 0f - _maxPos, _maxPos);
			Vector3 target = new Vector3(value, value2, 0f);
			float value3 = (0f - vector.y) * _rotStep;
			float value4 = vector.x * _rotStep;
			float value5 = (0f - vector.x) * (_rotStep * 0.55f);
			value3 = Mathf.Clamp(value3, 0f - _maxRot, _maxRot);
			value4 = Mathf.Clamp(value4, 0f - _maxRot, _maxRot);
			value5 = Mathf.Clamp(value5, 0f - _maxRot, _maxRot);
			Vector3 vector2 = new Vector3(value3, value4, value5);
			_swayPosCurrent = Vector3.SmoothDamp(_swayPosCurrent, target, ref _swayPosVel, _smoothTime, float.PositiveInfinity, num);
			_swayRotEulerCurrent.x = Mathf.SmoothDampAngle(_swayRotEulerCurrent.x, vector2.x, ref _swayRotVelEuler.x, _smoothTime, float.PositiveInfinity, num);
			_swayRotEulerCurrent.y = Mathf.SmoothDampAngle(_swayRotEulerCurrent.y, vector2.y, ref _swayRotVelEuler.y, _smoothTime, float.PositiveInfinity, num);
			_swayRotEulerCurrent.z = Mathf.SmoothDampAngle(_swayRotEulerCurrent.z, vector2.z, ref _swayRotVelEuler.z, _smoothTime, float.PositiveInfinity, num);
			Vector3 target2 = Vector3.zero;
			Vector3 vector3 = Vector3.zero;
			if (viewmodelMotionEnabled && _enableBob && _isGrounded && _isMoving && _speed01 > 0.001f)
			{
				float b = (_isRunning ? _runBobFrequency : _walkBobFrequency);
				b = Mathf.Max(0.01f, b);
				_bobPhase += num * b * Mathf.Lerp(0.2f, 1f, _speed01);
				float num2 = Mathf.Sin(_bobPhase * MathF.PI * 2f);
				float num3 = Mathf.Cos(_bobPhase * MathF.PI * 2f);
				float num4 = Mathf.Clamp(_strafe01, -1f, 1f) * _strafeBobBias;
				target2 = new Vector3((num3 * 0.55f + num2 * 0.2f) * _bobPosAmount * 0.55f * num4, Mathf.Abs(num2) * _bobPosAmount, num3 * 0.35f * _bobPosAmount * 0.35f) * _speed01;
				vector3 = new Vector3((0f - num3) * 0.85f * _bobRotAmount, num2 * 0.35f * _bobRotAmount * 0.25f * num4, (0f - num2) * 0.95f * _bobRotAmount * 0.55f * num4) * _speed01;
			}
			_bobPosCurrent = Vector3.SmoothDamp(_bobPosCurrent, target2, ref _bobPosVel, _smoothTime, float.PositiveInfinity, num);
			_bobRotEulerCurrent.x = Mathf.SmoothDampAngle(_bobRotEulerCurrent.x, vector3.x, ref _bobRotVelEuler.x, _smoothTime, float.PositiveInfinity, num);
			_bobRotEulerCurrent.y = Mathf.SmoothDampAngle(_bobRotEulerCurrent.y, vector3.y, ref _bobRotVelEuler.y, _smoothTime, float.PositiveInfinity, num);
			_bobRotEulerCurrent.z = Mathf.SmoothDampAngle(_bobRotEulerCurrent.z, vector3.z, ref _bobRotVelEuler.z, _smoothTime, float.PositiveInfinity, num);
			Vector3 target3 = Vector3.zero;
			Vector3 vector4 = Vector3.zero;
			if (viewmodelMotionEnabled && _enableIdle && (!_isMoving || _speed01 <= 0.001f))
			{
				float num5 = Mathf.Max(0.01f, _idleFrequency);
				_idlePhase += num * num5 * 2f;
				float num6 = Mathf.Sin(_idlePhase * MathF.PI * 2f);
				float num7 = Mathf.Cos(_idlePhase * MathF.PI * 2f);
				target3 = new Vector3(num7 * _idlePosAmount * 0.35f, Mathf.Abs(num6) * _idlePosAmount, num6 * _idlePosAmount * 0.25f) * _idleWeight;
				vector4 = new Vector3((0f - num7) * _idleRotAmount, num6 * _idleRotAmount * 0.15f, (0f - num6) * _idleRotAmount * 0.35f) * _idleWeight;
			}
			float num8 = _idleWeight * _idleWeightRuntime;
			target3 *= num8;
			vector4 *= num8;
			_idlePosCurrent = Vector3.SmoothDamp(_idlePosCurrent, target3, ref _idlePosVel, _smoothTime, float.PositiveInfinity, num);
			_idleRotEulerCurrent.x = Mathf.SmoothDampAngle(_idleRotEulerCurrent.x, vector4.x, ref _idleRotVelEuler.x, _smoothTime, float.PositiveInfinity, num);
			_idleRotEulerCurrent.y = Mathf.SmoothDampAngle(_idleRotEulerCurrent.y, vector4.y, ref _idleRotVelEuler.y, _smoothTime, float.PositiveInfinity, num);
			_idleRotEulerCurrent.z = Mathf.SmoothDampAngle(_idleRotEulerCurrent.z, vector4.z, ref _idleRotVelEuler.z, _smoothTime, float.PositiveInfinity, num);
			Vector3 target4 = Vector3.zero;
			Vector3 vector5 = Vector3.zero;
			if (viewmodelMotionEnabled && _enableScriptedSway && _scriptedActive && _scriptedWeight > 0.001f)
			{
				if (_scriptedUsesDirectTarget)
				{
					target4 = _scriptedDirectPosTarget * _scriptedWeight;
					vector5 = _scriptedDirectRotTarget * _scriptedWeight;
				}
				else
				{
					_scriptedTime += num * Mathf.Max(0.01f, _scriptedFrequency);
					float num9 = (_scriptedTime + _scriptedPhaseOffset) * MathF.PI * 2f;
					float num10 = Mathf.Sin(num9);
					float num11 = Mathf.Cos(num9);
					float num12 = Mathf.Sin(num9 * 0.5f + 0.7f);
					float num13 = Mathf.Sin(num9 * 1.35f + 1.2f);
					target4 = new Vector3(num10 * _scriptedPosAmplitude.x, num12 * _scriptedPosAmplitude.y, num11 * _scriptedPosAmplitude.z) * _scriptedWeight;
					vector5 = new Vector3(num12 * _scriptedRotAmplitude.x, num10 * _scriptedRotAmplitude.y, num13 * _scriptedRotAmplitude.z) * _scriptedWeight;
				}
			}
			float smoothTime = (_scriptedUsesDirectTarget ? _scriptedDirectSmoothTime : _scriptedSmoothTime);
			_scriptedPosCurrent = Vector3.SmoothDamp(_scriptedPosCurrent, target4, ref _scriptedPosVel, smoothTime, float.PositiveInfinity, num);
			_scriptedRotEulerCurrent.x = Mathf.SmoothDampAngle(_scriptedRotEulerCurrent.x, vector5.x, ref _scriptedRotVelEuler.x, smoothTime, float.PositiveInfinity, num);
			_scriptedRotEulerCurrent.y = Mathf.SmoothDampAngle(_scriptedRotEulerCurrent.y, vector5.y, ref _scriptedRotVelEuler.y, smoothTime, float.PositiveInfinity, num);
			_scriptedRotEulerCurrent.z = Mathf.SmoothDampAngle(_scriptedRotEulerCurrent.z, vector5.z, ref _scriptedRotVelEuler.z, smoothTime, float.PositiveInfinity, num);
			if (_enableDamageHit && RuntimeSettings.DamageCameraMotionEnabled)
			{
				float smoothTime2 = ((_damageRotTarget.sqrMagnitude > _damageRotCurrent.sqrMagnitude) ? _damageHitReturnTime : _damageHitRecoverTime);
				_damageRotCurrent.x = Mathf.SmoothDampAngle(_damageRotCurrent.x, _damageRotTarget.x, ref _damageRotVel.x, smoothTime2, float.PositiveInfinity, num);
				_damageRotCurrent.y = Mathf.SmoothDampAngle(_damageRotCurrent.y, _damageRotTarget.y, ref _damageRotVel.y, smoothTime2, float.PositiveInfinity, num);
				_damageRotCurrent.z = Mathf.SmoothDampAngle(_damageRotCurrent.z, _damageRotTarget.z, ref _damageRotVel.z, smoothTime2, float.PositiveInfinity, num);
				_damageRotTarget = Vector3.Lerp(_damageRotTarget, Vector3.zero, num / Mathf.Max(0.0001f, _damageHitRecoverTime));
			}
			else
			{
				_damageRotCurrent = Vector3.zero;
				_damageRotTarget = Vector3.zero;
				_damageRotVel = Vector3.zero;
			}
			Transform target5 = Target;
			Vector3 localPosition = _baseLocalPos + _swayPosCurrent + _bobPosCurrent + _idlePosCurrent + _scriptedPosCurrent;
			Quaternion localRotation = _baseLocalRot * Quaternion.Euler(_swayRotEulerCurrent + _bobRotEulerCurrent + _idleRotEulerCurrent + _scriptedRotEulerCurrent + _damageRotCurrent);
			target5.SetLocalPositionAndRotation(localPosition, localRotation);
			ApplyUiOverlaySway();
		}

		public void AddLookDelta(Vector2 delta)
		{
			_lookDelta += delta;
			_hasLookThisFrame = true;
		}

		public void SetMoveState(bool isMoving, float speed01, bool isRunning, float strafe01)
		{
			if (_movementSwaySuppressed)
			{
				_isMoving = false;
				_speed01 = 0f;
				_isRunning = false;
				_strafe01 = 0f;
			}
			else
			{
				_isMoving = isMoving;
				_speed01 = Mathf.Clamp01(speed01);
				_isRunning = isRunning;
				_strafe01 = Mathf.Clamp(strafe01, -1f, 1f);
			}
		}

		public void SetMovementSwaySuppressed(bool suppressed)
		{
			if (_movementSwaySuppressed != suppressed)
			{
				_movementSwaySuppressed = suppressed;
				if (suppressed)
				{
					SetMoveState(isMoving: false, 0f, isRunning: false, 0f);
				}
			}
		}

		public void SetIdleWeight(float weight01)
		{
			_idleWeightRuntime = Mathf.Clamp01(weight01);
		}

		public void SetGrounded(bool isGrounded)
		{
			_isGrounded = isGrounded;
		}

		public void StopScriptedSway()
		{
			_scriptedActive = false;
			_scriptedWeight = 0f;
			_scriptedDirectPosTarget = Vector3.zero;
			_scriptedDirectRotTarget = Vector3.zero;
			if (_scriptedIdleWeightOverrideActive)
			{
				_idleWeightRuntime = _scriptedPreviousIdleWeightRuntime;
				_scriptedIdleWeightOverrideActive = false;
			}
		}

		public void StartFishingSway(float weight = 1f)
		{
			RestoreScriptedIdleWeightOverride();
			_scriptedActive = true;
			_scriptedWeight = Mathf.Clamp01(weight);
			_scriptedUsesDirectTarget = false;
			_scriptedFrequency = 1f;
			_scriptedPosAmplitude = new Vector3(0.015f, 0.0025f, 0.0015f);
			_scriptedRotAmplitude = new Vector3(0.45f, 2.3f, 3.2f);
			_scriptedPhaseOffset = 0f;
		}

		public void StartHoldingSway(float weight = 1f)
		{
			RestoreScriptedIdleWeightOverride();
			_scriptedActive = true;
			_scriptedWeight = Mathf.Clamp01(weight);
			_scriptedUsesDirectTarget = false;
			_scriptedFrequency = 0.35f;
			_scriptedPosAmplitude = new Vector3(0.0035f, 0.002f, 0.0015f);
			_scriptedRotAmplitude = new Vector3(0.2f, 0.3f, 0.35f);
			_scriptedPhaseOffset = 0.23f;
		}

		public void SetScriptedSway(bool active, float weight, float frequency, Vector3 posAmplitude, Vector3 rotAmplitude, float phaseOffset = 0f)
		{
			RestoreScriptedIdleWeightOverride();
			_scriptedActive = active;
			_scriptedWeight = Mathf.Clamp01(weight);
			_scriptedUsesDirectTarget = false;
			_scriptedFrequency = Mathf.Max(0.01f, frequency);
			_scriptedPosAmplitude = posAmplitude;
			_scriptedRotAmplitude = rotAmplitude;
			_scriptedPhaseOffset = phaseOffset;
		}

		public void StartDirectionalSway(float weight, float idleWeight)
		{
			if (!_scriptedIdleWeightOverrideActive)
			{
				_scriptedPreviousIdleWeightRuntime = _idleWeightRuntime;
				_scriptedIdleWeightOverrideActive = true;
			}
			_idleWeightRuntime = Mathf.Clamp01(idleWeight);
			_scriptedActive = true;
			_scriptedWeight = Mathf.Clamp01(weight);
			_scriptedUsesDirectTarget = true;
			_scriptedDirectPosTarget = Vector3.zero;
			_scriptedDirectRotTarget = Vector3.zero;
		}

		public void SetDirectionalSwayTarget(Vector3 localPosition, Vector3 localEulerAngles, float smoothTime)
		{
			if (_scriptedActive && _scriptedUsesDirectTarget)
			{
				_scriptedDirectPosTarget = localPosition;
				_scriptedDirectRotTarget = localEulerAngles;
				_scriptedDirectSmoothTime = Mathf.Max(0.001f, smoothTime);
			}
		}

		private void RestoreScriptedIdleWeightOverride()
		{
			if (_scriptedIdleWeightOverrideActive)
			{
				_idleWeightRuntime = _scriptedPreviousIdleWeightRuntime;
				_scriptedIdleWeightOverrideActive = false;
			}
		}

		private void CacheBasePose()
		{
			Transform target = Target;
			_baseLocalPos = target.localPosition;
			_baseLocalRot = target.localRotation;
		}

		private void CacheUI()
		{
			if (_uiOverlayTarget != null)
			{
				_uiBaseAnchoredPos = _uiOverlayTarget.anchoredPosition;
				_uiBaseLocalRot = _uiOverlayTarget.localRotation;
			}
		}

		public void PlayDamageHitFeedback()
		{
			if (_enableDamageHit)
			{
				float num = ((!(UnityEngine.Random.value > 0.5f)) ? 1 : (-1));
				float num2 = Mathf.Clamp((!(UnityEngine.Random.value > 0.5f)) ? 1 : (-1), -1f, 1f);
				float x = (0f - _damageHitPitch) * num;
				float y = num2 * _damageHitYaw * num;
				float z = (0f - num2) * _damageHitRoll * num;
				_damageRotTarget += new Vector3(x, y, z);
				_damageRotTarget.x = Mathf.Clamp(_damageRotTarget.x, 0f - _damageHitPitch, _damageHitPitch);
				_damageRotTarget.y = Mathf.Clamp(_damageRotTarget.y, 0f - _damageHitYaw, _damageHitYaw);
				_damageRotTarget.z = Mathf.Clamp(_damageRotTarget.z, 0f - _damageHitRoll, _damageHitRoll);
			}
		}

		private void ApplyUiOverlaySway()
		{
			if (_enableUiOverlaySway && !(_uiOverlayTarget == null))
			{
				float num = (_useUnscaledTime ? Time.unscaledTime : Time.time);
				Vector2 vector = new Vector2((_swayPosCurrent.x + _idlePosCurrent.x + _scriptedPosCurrent.x) * _uiPositionMultiplier, (_swayPosCurrent.y + _idlePosCurrent.y + _scriptedPosCurrent.y) * _uiPositionMultiplier);
				Vector2 vector2 = new Vector2(Mathf.Sin(num * _uiExtraIdleFrequency * MathF.PI * 2f) * _uiExtraIdleAmount, Mathf.Sin((num * _uiExtraIdleFrequency * 1.37f + 0.25f) * MathF.PI * 2f) * _uiExtraIdleAmount);
				vector += vector2;
				vector.x = Mathf.Clamp(vector.x, 0f - _uiMaxOffset.x, _uiMaxOffset.x);
				vector.y = Mathf.Clamp(vector.y, 0f - _uiMaxOffset.y, _uiMaxOffset.y);
				float value = (_swayRotEulerCurrent.z + _idleRotEulerCurrent.z + _scriptedRotEulerCurrent.z) * _uiRotationMultiplier;
				value = Mathf.Clamp(value, -2.5f, 2.5f);
				_uiOverlayTarget.anchoredPosition = _uiBaseAnchoredPos + vector;
				_uiOverlayTarget.localRotation = _uiBaseLocalRot * Quaternion.Euler(0f, 0f, value);
			}
		}

		public void EnableGogglesUI(bool s)
		{
			if (!(_uiOverlayTarget == null))
			{
				_enableUiOverlaySway = s;
				_uiOverlayTarget.gameObject.SetActive(s);
			}
		}
	}
}
