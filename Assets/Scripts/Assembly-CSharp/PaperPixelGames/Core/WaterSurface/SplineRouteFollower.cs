using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace PaperPixelGames.Core.WaterSurface
{
	public class SplineRouteFollower : MonoBehaviour
	{
		[Header("Runtime")]
		private SplineContainer _container;

		private int _splineIndex;

		[SerializeField]
		private AudioSource _audioSource;

		private float _hornMinDelay = 1f;

		private float _hornMaxDelay = 8f;

		private float _hornPlayChance = 0.35f;

		private bool _hornAttemptDone;

		private float _hornAttemptAtTime;

		private float _elapsed;

		[Header("Movement")]
		[SerializeField]
		[Min(0.01f)]
		private float _speedUnitsPerSec = 6f;

		[SerializeField]
		private bool _alignRotation = true;

		private float _distance;

		private float _splineLength;

		private bool _isRunning;

		private bool _startFromEnd;

		public bool IsRunning => _isRunning;

		public event Action<SplineRouteFollower> Completed;

		public void Begin(SplineContainer container, int splineIndex, float speedUnitsPerSec, bool startFromEnd)
		{
			_container = container;
			_splineIndex = splineIndex;
			_speedUnitsPerSec = Mathf.Max(0.01f, speedUnitsPerSec);
			_startFromEnd = startFromEnd;
			_distance = 0f;
			_isRunning = false;
			_elapsed = 0f;
			_hornAttemptDone = false;
			float max = _splineLength / _speedUnitsPerSec;
			float num = Mathf.Clamp(_hornMinDelay, 0f, max);
			float maxInclusive = Mathf.Clamp(_hornMaxDelay, num, max);
			_hornAttemptAtTime = UnityEngine.Random.Range(num, maxInclusive);
			if (_container == null)
			{
				Debug.LogWarning("[SplineRouteFollower] Begin failed: container is null");
				return;
			}
			if (_container.Splines == null || _container.Splines.Count == 0)
			{
				Debug.LogWarning("[SplineRouteFollower] Begin failed: container has no splines");
				return;
			}
			if (_splineIndex < 0 || _splineIndex >= _container.Splines.Count)
			{
				Debug.LogWarning($"[SplineRouteFollower] Begin failed: splineIndex out of range ({_splineIndex})");
				return;
			}
			Spline spline = _container.Splines[_splineIndex];
			_splineLength = spline.GetLength();
			if (_splineLength <= 0.0001f)
			{
				Debug.LogWarning("[SplineRouteFollower] Begin failed: spline length is ~0");
				return;
			}
			ApplyPoseAtDistance(0f);
			_isRunning = true;
			base.gameObject.SetActive(value: true);
		}

		public void StopAndHide()
		{
			_isRunning = false;
			_distance = 0f;
			_hornAttemptDone = false;
			_elapsed = 0f;
			base.gameObject.SetActive(value: false);
		}

		private void FixedUpdate()
		{
			if (!_isRunning)
			{
				return;
			}
			if (_container == null)
			{
				StopAndHide();
				return;
			}
			_distance += _speedUnitsPerSec * Time.deltaTime;
			if (!_hornAttemptDone && _elapsed >= _hornAttemptAtTime)
			{
				_hornAttemptDone = true;
				if (_audioSource != null && !_audioSource.isPlaying && UnityEngine.Random.value <= _hornPlayChance)
				{
					_audioSource.Play();
				}
			}
			if (_distance >= _splineLength)
			{
				ApplyPoseAtDistance(_splineLength);
				_isRunning = false;
				Completed?.Invoke(this);
			}
			else
			{
				ApplyPoseAtDistance(_distance);
			}
		}

		private void ApplyPoseAtDistance(float distance)
		{
			if (_container == null || _container.Splines == null || _container.Splines.Count == 0 || _splineIndex < 0 || _splineIndex >= _container.Splines.Count)
			{
				return;
			}
			float normalizedInterpolation = SplineUtility.GetNormalizedInterpolation(_container.Splines[_splineIndex], distance, PathIndexUnit.Distance);
			normalizedInterpolation = Mathf.Clamp01(normalizedInterpolation);
			if (_startFromEnd)
			{
				normalizedInterpolation = 1f - normalizedInterpolation;
			}
			if (!_container.Evaluate(_splineIndex, normalizedInterpolation, out var position, out var tangent, out var upVector))
			{
				return;
			}
			base.transform.position = position;
			if (_alignRotation && math.lengthsq(tangent) > 0.0001f)
			{
				if (_startFromEnd)
				{
					tangent = -tangent;
				}
				base.transform.rotation = Quaternion.LookRotation(tangent, upVector);
			}
		}
	}
}
