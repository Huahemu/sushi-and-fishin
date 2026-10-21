using UnityEngine;

namespace PaperPixelGames.Core.PlayerTools
{
	public sealed class CompassNeedle3D : MonoBehaviour
	{
		[Header("Refs")]
		[SerializeField]
		private Transform _needle;

		[SerializeField]
		private Animator _animator;

		[SerializeField]
		private AudioSource _sfxShimmer;

		[SerializeField]
		private AudioSource _sfxSpinning;

		[Header("Behavior")]
		[SerializeField]
		private bool _activeOnStart;

		[SerializeField]
		private bool _yawOnly;

		[Header("Offsets")]
		[Tooltip("If your needle/model forward is not +Z, add a correction here (Euler degrees).")]
		[SerializeField]
		private Vector3 _modelForwardOffsetEuler = Vector3.zero;

		[Tooltip("Optional world-space target offset.")]
		[SerializeField]
		private Vector3 _targetPositionOffset = Vector3.zero;

		[Header("Search Area")]
		[Tooltip("When distance to target is <= this value, the compass enters search mode.")]
		[Min(0f)]
		[SerializeField]
		private float _searchRadius = 10f;

		[Tooltip("Random yaw angle range used while searching.")]
		[Min(0f)]
		[SerializeField]
		private float _searchAngleRange = 90f;

		[Tooltip("How often a new random search direction is picked.")]
		[Min(0.01f)]
		[SerializeField]
		private float _searchDirectionChangeInterval = 0.2f;

		[Tooltip("Optional wobble speed while searching.")]
		[Min(0f)]
		[SerializeField]
		private float _searchWobbleSpeed = 8f;

		[Tooltip("Optional wobble amplitude while searching.")]
		[Min(0f)]
		[SerializeField]
		private float _searchWobbleAngle = 12f;

		[Header("Smoothing")]
		[Tooltip("Exponential damping. 8-16 feels natural. Used when Max Turn Speed = 0.")]
		[Range(1f, 30f)]
		[SerializeField]
		private float _damping = 12f;

		[Tooltip("Max turn speed (deg/sec). 0 => unlimited (damping only). If > 0, RotateTowards is used.")]
		[Min(0f)]
		[SerializeField]
		private float _maxTurnSpeedDegPerSec;

		[Header("Audio")]
		[Tooltip("If true, Deactivate() uses Pause() so Activate() resumes where it left off. If false, uses Stop().")]
		[SerializeField]
		private bool _pauseAudioOnDeactivate;

		private Transform _target;

		private bool _active;

		private Quaternion _modelFwdOffset;

		private Quaternion _desiredRot;

		private float _nextSearchDirectionTime;

		private float _currentSearchYawOffset;

		private const float kMinDirSqr = 1E-10f;

		private void Awake()
		{
			_modelFwdOffset = Quaternion.Euler(_modelForwardOffsetEuler);
			_active = _activeOnStart;
			SetAnimatorActive(_active);
			if (_active)
			{
				PlayAudio();
			}
			else
			{
				StopOrPauseAudio();
			}
		}

		private void OnValidate()
		{
			_modelFwdOffset = Quaternion.Euler(_modelForwardOffsetEuler);
			if (_damping < 1f)
			{
				_damping = 1f;
			}
			if (_maxTurnSpeedDegPerSec < 0f)
			{
				_maxTurnSpeedDegPerSec = 0f;
			}
			if (_searchRadius < 0f)
			{
				_searchRadius = 0f;
			}
			if (_searchAngleRange < 0f)
			{
				_searchAngleRange = 0f;
			}
			if (_searchDirectionChangeInterval < 0.01f)
			{
				_searchDirectionChangeInterval = 0.01f;
			}
			if (_searchWobbleSpeed < 0f)
			{
				_searchWobbleSpeed = 0f;
			}
			if (_searchWobbleAngle < 0f)
			{
				_searchWobbleAngle = 0f;
			}
		}

		private void Update()
		{
			if (_active && !(_needle == null) && !(_target == null) && TryComputeDesiredRotation(out _desiredRot))
			{
				if (_maxTurnSpeedDegPerSec > 0f)
				{
					float maxDegreesDelta = _maxTurnSpeedDegPerSec * Time.deltaTime;
					_needle.rotation = Quaternion.RotateTowards(_needle.rotation, _desiredRot, maxDegreesDelta);
				}
				else
				{
					float t = 1f - Mathf.Exp((0f - _damping) * Time.deltaTime);
					_needle.rotation = Quaternion.Slerp(_needle.rotation, _desiredRot, t);
				}
			}
		}

		public void SetTarget(Transform newTarget)
		{
			_target = newTarget;
		}

		public void SnapToTarget()
		{
			if (!(_needle == null) && !(_target == null) && TryComputeDesiredRotation(out var desired))
			{
				_needle.rotation = desired;
			}
		}

		public void Activate()
		{
			_active = true;
			SetAnimatorActive(value: true);
			PlayAudio();
		}

		public void Deactivate()
		{
			_active = false;
			SetAnimatorActive(value: false);
			StopOrPauseAudio();
		}

		private void SetAnimatorActive(bool value)
		{
			if (_animator != null)
			{
				_animator.SetBool("Active", value);
			}
		}

		private void PlayAudio()
		{
			if (_sfxShimmer != null && !_sfxShimmer.isPlaying && _sfxShimmer.gameObject.activeSelf)
			{
				_sfxShimmer.Play();
			}
			if (_sfxSpinning != null && !_sfxSpinning.isPlaying && _sfxSpinning.gameObject.activeSelf)
			{
				_sfxSpinning.Play();
			}
		}

		private void StopOrPauseAudio()
		{
			if (_pauseAudioOnDeactivate)
			{
				if (_sfxShimmer != null && _sfxShimmer.isPlaying)
				{
					_sfxShimmer.Pause();
				}
				if (_sfxSpinning != null && _sfxSpinning.isPlaying)
				{
					_sfxSpinning.Pause();
				}
			}
			else
			{
				if (_sfxShimmer != null)
				{
					_sfxShimmer.Stop();
				}
				if (_sfxSpinning != null)
				{
					_sfxSpinning.Stop();
				}
			}
		}

		private bool TryComputeDesiredRotation(out Quaternion desired)
		{
			desired = default;
			Vector3 vector = ((_needle != null) ? _needle.up : Vector3.up);
			Vector3 vector2 = _target.position + _targetPositionOffset - _needle.position;
			float magnitude = vector2.magnitude;
			if (magnitude <= Mathf.Epsilon)
			{
				return false;
			}
			Vector3 vector3;
			if (_searchRadius > 0f && magnitude <= _searchRadius)
			{
				vector3 = GetSearchDirection(vector2, vector);
			}
			else
			{
				vector3 = vector2;
				if (_yawOnly)
				{
					vector3 = Vector3.ProjectOnPlane(vector3, vector);
				}
				float sqrMagnitude = vector3.sqrMagnitude;
				if (sqrMagnitude < 1E-10f)
				{
					return false;
				}
				vector3 /= Mathf.Sqrt(sqrMagnitude);
			}
			desired = Quaternion.LookRotation(vector3, vector) * _modelFwdOffset;
			return true;
		}

		private Vector3 GetSearchDirection(Vector3 toTarget, Vector3 up)
		{
			Vector3 vector = (_yawOnly ? Vector3.ProjectOnPlane(toTarget, up) : toTarget);
			if (vector.sqrMagnitude < 1E-10f)
			{
				vector = _needle.forward;
			}
			vector.Normalize();
			if (Time.time >= _nextSearchDirectionTime)
			{
				_nextSearchDirectionTime = Time.time + _searchDirectionChangeInterval;
				_currentSearchYawOffset = Random.Range(0f - _searchAngleRange, _searchAngleRange);
			}
			float num = ((_searchWobbleAngle > 0f) ? (Mathf.Sin(Time.time * _searchWobbleSpeed) * _searchWobbleAngle) : 0f);
			Vector3 vector2 = Quaternion.AngleAxis(_currentSearchYawOffset + num, up) * vector;
			if (_yawOnly)
			{
				vector2 = Vector3.ProjectOnPlane(vector2, up);
			}
			if (vector2.sqrMagnitude < 1E-10f)
			{
				vector2 = _needle.forward;
			}
			return vector2.normalized;
		}

		public bool IsInSearchMode()
		{
			if (_needle == null || _target == null || _searchRadius <= 0f)
			{
				return false;
			}
			Vector3 b = _target.position + _targetPositionOffset;
			return Vector3.Distance(_needle.position, b) <= _searchRadius;
		}

		public float GetDistanceToTarget()
		{
			if (_needle == null || _target == null)
			{
				return float.PositiveInfinity;
			}
			Vector3 b = _target.position + _targetPositionOffset;
			return Vector3.Distance(_needle.position, b);
		}
	}
}
