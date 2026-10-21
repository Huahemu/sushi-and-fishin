using System;
using System.Linq;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;
using UnityEngine.Events;

namespace PaperPixelGames.Core.PlayerTools
{
	[DisallowMultipleComponent]
	[RequireComponent(typeof(LineRenderer))]
	public class GrappleEffect : MonoBehaviour
	{
		private enum Phase
		{
			Idle = 0,
			Extending = 1,
			Taut = 2,
			Retracting = 3,
			Snap = 4
		}

		private const float TIME_CYCLE = 1024f;

		public UnityEvent OnReachTarget;

		public UnityEvent OnRetractComplete;

		[SerializeField]
		private LineRenderer _line;

		[SerializeField]
		private Transform _visualTip;

		[SerializeField]
		private Transform _logicTip;

		[SerializeField]
		private Transform Target;

		[SerializeField]
		private Transform _visualTargetOverride;

		[Min(2f)]
		[SerializeField]
		private int _segments = 64;

		[Min(0f)]
		[SerializeField]
		private float _extendSpeed = 25f;

		[Min(0f)]
		[SerializeField]
		private float _retractSpeed = 30f;

		[Range(0f, 1f)]
		[SerializeField]
		private float _tensionStart = 0.75f;

		public bool spiralEnabled = true;

		[Min(0f)]
		public float spiralAmplitude = 0.06f;

		[Min(0f)]
		public float spiralCycles = 2.5f;

		public float spiralScrollSpeed = 0.65f;

		public AnimationCurve spiralAmplitudeProfile = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

		[Range(0f, 2f)]
		public float tensionFadeMultiplier = 1f;

		[Range(0f, 1f)]
		public float retractSpiralBlend = 0.5f;

		private float _modifiedRetractSpeed;

		[Tooltip("Mask along rope length (0=start, 1=end). 0 disables spiral, 1 enables.")]
		public AnimationCurve spiralLengthMask = new AnimationCurve(new Keyframe(0f, 1f, 0f, 0f), new Keyframe(0.2f, 1f, 0f, 0f), new Keyframe(0.3f, 0f, 0f, 0f), new Keyframe(0.7f, 0f, 0f, 0f), new Keyframe(0.8f, 1f, 0f, 0f), new Keyframe(1f, 1f, 0f, 0f));

		[Tooltip("Mask over extend progress/time (0..1 of extendCurve input). 0 disables spiral, 1 enables.")]
		public AnimationCurve spiralTimeMask = new AnimationCurve(new Keyframe(0f, 1f, 0f, 0f), new Keyframe(0.25f, 1f, 0f, 0f), new Keyframe(0.4f, 0f, 0f, 0f), new Keyframe(1f, 0f, 0f, 0f));

		[Tooltip("If target distance is below this, spiral is disabled entirely.")]
		[Min(0f)]
		public float spiralDisableBelowDistance = 2f;

		[Tooltip("Controls rope extend progression over normalized time (0-1).")]
		public AnimationCurve extendCurve = new AnimationCurve(new Keyframe(0f, 0f, 1.5f, 1.5f), new Keyframe(0.2f, 0.05f, 0f, 2.5f), new Keyframe(1f, 1f, 0f, 0f));

		private Phase _phase;

		private bool _active;

		private bool _eventInvoked;

		private float _tipCurrentDist;

		private float _tipTargetDist;

		private float _tension01;

		private float _time;

		private float _extendProgress01;

		private Vector3[] _positions;

		private bool _suppressReachTargetEvent;

		private AudioSource _retractSFX;

		public Transform TipTransform => _visualTip;

		private void Awake()
		{
			if (!_line)
			{
				_line = GetComponent<LineRenderer>();
			}
			EnsureBuffers();
			_line.enabled = false;
			_line.useWorldSpace = true;
			if (_line.widthMultiplier <= 0f)
			{
				_line.widthMultiplier = 0.02f;
			}
			if (!_visualTip)
			{
				GameObject gameObject = new GameObject("RopeTipAnchor");
				_visualTip = gameObject.transform;
				_visualTip.SetParent(base.transform, worldPositionStays: false);
			}
		}

		private void OnValidate()
		{
			if ((bool)_line && base.enabled)
			{
				EnsureBuffers();
			}
		}

		private void OnDisable()
		{
			HardStop();
		}

		private void Update()
		{
			_time = Mathf.Repeat(_time + Time.deltaTime, 1024f);
		}

		private void LateUpdate()
		{
			Transform transform = ((_visualTargetOverride != null) ? _visualTargetOverride : Target);
			if (!_active || transform == null)
			{
				return;
			}
			Vector3 position = base.transform.position;
			Vector3 position2 = transform.position;
			float num = (_tipTargetDist = Vector3.Distance(position, position2));
			switch (_phase)
			{
			case Phase.Extending:
			{
				_extendProgress01 = Mathf.Clamp01(_extendProgress01 + Time.deltaTime * _extendSpeed / Mathf.Max(num, 0.001f));
				float t = Mathf.Clamp01(extendCurve.Evaluate(_extendProgress01));
				_tipCurrentDist = Mathf.Lerp(0f, _tipTargetDist, t);
				if (_extendProgress01 >= 1f)
				{
					_phase = Phase.Taut;
					_tension01 = 1f;
				}
				break;
			}
			case Phase.Snap:
				_tipCurrentDist = _tipTargetDist;
				_phase = Phase.Taut;
				_tension01 = 1f;
				break;
			case Phase.Retracting:
				_tipCurrentDist = Mathf.Max(0f, _tipCurrentDist - _modifiedRetractSpeed * Time.deltaTime);
				_tension01 = 0f;
				if (_tipCurrentDist <= 0.175f)
				{
					OnRetractComplete?.Invoke();
					HardStop();
					return;
				}
				break;
			case Phase.Taut:
				if (!_eventInvoked)
				{
					_eventInvoked = true;
					if (!_suppressReachTargetEvent)
					{
						OnReachTarget?.Invoke();
					}
				}
				_tipCurrentDist = _tipTargetDist;
				_tension01 = 1f;
				break;
			}
			float num2 = ((num > 0.1f) ? Mathf.Clamp01(_tipCurrentDist / Mathf.Max(0.0001f, num)) : 0f);
			float tension = ((_phase == Phase.Retracting) ? 0f : ((_phase == Phase.Taut || _phase == Phase.Snap) ? 1f : Mathf.InverseLerp(_tensionStart, 1f, num2)));
			_tension01 = tension;
			BuildRope(position, position2, num, num2, out var linePos);
			_line.SetPositions(_positions);
			int num3 = _positions.Length - 1;
			Vector3 vector = _positions[num3];
			Vector3 vector2 = ((num3 > 0) ? (_positions[num3] - _positions[num3 - 1]) : (position2 - position));
			if (vector2.sqrMagnitude < 1E-08f)
			{
				vector2 = position2 - vector;
			}
			if (vector2.sqrMagnitude < 1E-08f)
			{
				vector2 = base.transform.forward;
			}
			_visualTip.position = vector;
			if (transform != null)
			{
				Vector3 vector3 = transform.position - vector;
				if (vector3.sqrMagnitude < 1E-08f)
				{
					vector3 = vector2;
				}
				_visualTip.rotation = Quaternion.LookRotation(vector3.normalized, Vector3.up);
			}
			else
			{
				_visualTip.rotation = Quaternion.LookRotation(vector2.normalized, Vector3.up);
			}
			_logicTip.position = linePos;
		}

		public void SetVisualTarget(Transform target)
		{
			_visualTargetOverride = target;
		}

		public void ClearVisualTarget()
		{
			_visualTargetOverride = null;
		}

		public void StartForward(float speed)
		{
			HardStop();
			_suppressReachTargetEvent = false;
			_active = true;
			_extendSpeed = speed;
			_modifiedRetractSpeed = _retractSpeed + _extendSpeed / 2f;
			EnsureBuffers();
			Vector3 position = base.transform.position;
			for (int i = 0; i < _positions.Length; i++)
			{
				_positions[i] = position;
			}
			_line.SetPositions(_positions);
			_line.enabled = true;
			_phase = Phase.Extending;
		}

		public void EndForward(bool invokeReachTargetEvent = true)
		{
			_suppressReachTargetEvent = !invokeReachTargetEvent;
			_phase = Phase.Snap;
			_tension01 = 1f;
		}

		public bool StartRetract(bool instant)
		{
			if (instant)
			{
				HardStop();
				OnRetractComplete?.Invoke();
				return true;
			}
			if (!_active)
			{
				return false;
			}
			_phase = Phase.Retracting;
			_tension01 = 0f;
			SoundManager instance = Singleton<SoundManager>.Instance;
			Transform parent = base.transform;
			_retractSFX = instance.PlaySFX(SoundType.Harpoon_Retract, default, parent);
			_retractSFX.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			return true;
		}

		public void HardStop()
		{
			if (_retractSFX != null)
			{
				_retractSFX.Stop();
			}
			_active = false;
			_eventInvoked = false;
			_suppressReachTargetEvent = false;
			_line.enabled = false;
			_time = 0f;
			_tension01 = 0f;
			_tipCurrentDist = 0f;
			_extendProgress01 = 0f;
			_phase = Phase.Idle;
			if (_positions != null)
			{
				Vector3 position = base.transform.position;
				for (int i = 0; i < _positions.Length; i++)
				{
					_positions[i] = position;
				}
				if ((bool)_line)
				{
					_line.SetPositions(_positions);
				}
			}
		}

		public void SetTipVisible(bool visible)
		{
			if ((bool)_visualTip)
			{
				_visualTip.gameObject.SetActive(visible);
			}
		}

		public void ParentToTip(Transform child, bool worldPositionStays = true)
		{
			if ((bool)_visualTip && (bool)child)
			{
				child.SetParent(_visualTip, worldPositionStays);
			}
		}

		private void EnsureBuffers()
		{
			int num = Mathf.Max(2, _segments) + 1;
			if (_positions == null || _positions.Length != num)
			{
				_positions = new Vector3[num];
				if ((bool)_line)
				{
					_line.positionCount = num;
				}
			}
		}

		private void BuildRope(Vector3 start, Vector3 end, float dist, float extend01, out Vector3 linePos)
		{
			if (dist < 0.1f)
			{
				for (int i = 0; i < _positions.Length; i++)
				{
					_positions[i] = start;
				}
				linePos = _positions.Last();
				return;
			}
			Vector3 normalized = (end - start).normalized;
			Vector3 up = Vector3.up;
			Vector3 vector = Vector3.Cross(normalized, up);
			if (vector.sqrMagnitude < 1E-06f)
			{
				vector = Vector3.Cross((Mathf.Abs(Vector3.Dot(normalized, Vector3.up)) > 0.9f) ? Vector3.forward : Vector3.up, normalized).normalized;
				up = Vector3.Cross(normalized, vector);
			}
			else
			{
				vector.Normalize();
				up = Vector3.Cross(normalized, vector);
			}
			bool num = spiralEnabled && spiralAmplitude > 0f && spiralCycles > 0f;
			bool flag = dist > spiralDisableBelowDistance;
			bool flag2 = num & flag;
			float num2 = 1f - _tension01;
			Vector3 vector2 = Vector3.LerpUnclamped(start, end, Mathf.Min(1f, extend01));
			_positions[0] = start;
			int num3 = _positions.Length;
			for (int j = 1; j < num3; j++)
			{
				float num4 = Mathf.Min((float)j / (float)(num3 - 1), extend01);
				Vector3 vector3 = Vector3.LerpUnclamped(start, end, num4);
				vector2 = vector3;
				if (flag2 && num2 > 0f && num4 > 0f)
				{
					float num5 = Mathf.Clamp01(spiralLengthMask.Evaluate(num4));
					float num6 = Mathf.Clamp01(spiralTimeMask.Evaluate(_extendProgress01));
					float num7 = num5 * num6;
					if (num7 <= 0f)
					{
						_positions[j] = vector3;
						continue;
					}
					float num8 = Mathf.Clamp01(spiralAmplitudeProfile.Evaluate(num4));
					float num9 = Mathf.Clamp01((_phase == Phase.Retracting) ? retractSpiralBlend : (1f - _tension01 * tensionFadeMultiplier));
					float f = (num4 * spiralCycles + _time * spiralScrollSpeed) * MathF.PI * 2f;
					float num10 = spiralAmplitude * num8 * num2 * num7;
					vector3 += num10 * num9 * (vector * Mathf.Cos(f) + up * Mathf.Sin(f));
				}
				_positions[j] = vector3;
			}
			linePos = vector2;
		}
	}
}
