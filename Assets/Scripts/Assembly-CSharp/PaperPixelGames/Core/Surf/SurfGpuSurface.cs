using System;
using StylizedWater3;
using UnityEngine;

namespace PaperPixelGames.Core.Surf
{
	public sealed class SurfGpuSurface : IDisposable
	{
		private readonly HeightQuerySystem.Sampler _sampler;

		private readonly HeightQuerySystem.AsyncRequest _request;

		private Vector3 _wanted;

		private Vector3 _requested;

		private Vector3 _sampleCenter;

		private Vector3 _normal = Vector3.up;

		private float _height;

		private float _slopeX;

		private float _slopeZ;

		private float _completedAt;

		private bool _valid;

		private bool _disposed;

		public static bool Supported
		{
			get
			{
				if (HeightQuerySystem.IsSupported())
				{
					return SystemInfo.supportsAsyncGPUReadback;
				}
				return false;
			}
		}

		public SurfGpuSurface(int id, Vector3 position)
		{
			_wanted = position;
			_sampler = new HeightQuerySystem.Sampler();
			_sampler.SetSampleCount(5);
			SetSamplePositions();
			_request = new HeightQuerySystem.AsyncRequest(id, _sampler, "Surfboard rendered surface")
			{
				invalidateMisses = false
			};
			_request.onCompleted += OnCompleted;
			_request.Issue();
		}

		public void SetPosition(Vector3 position)
		{
			_wanted = position;
		}

		private void SetSamplePositions()
		{
			_requested = _wanted;
			_sampler.SetSamplePosition(0, _requested);
			_sampler.SetSamplePosition(1, _requested - Vector3.right);
			_sampler.SetSamplePosition(2, _requested + Vector3.right);
			_sampler.SetSamplePosition(3, _requested - Vector3.forward);
			_sampler.SetSamplePosition(4, _requested + Vector3.forward);
		}

		private void OnCompleted()
		{
			if (!_disposed)
			{
				bool flag = true;
				for (int i = 0; i < 5; i++)
				{
					flag &= float.IsFinite(_sampler.heightValues[i]) && !HeightQuerySystem.EqualsVoid(_sampler.heightValues[i]);
				}
				if (flag)
				{
					_valid = true;
					_sampleCenter = _requested;
					_height = _sampler.heightValues[0];
					_slopeX = (_sampler.heightValues[2] - _sampler.heightValues[1]) * 0.5f;
					_slopeZ = (_sampler.heightValues[4] - _sampler.heightValues[3]) * 0.5f;
					_normal = new Vector3(0f - _slopeX, 1f, 0f - _slopeZ).normalized;
					_completedAt = Time.unscaledTime;
				}
				SetSamplePositions();
			}
		}

		public bool TrySample(Vector3 position, out float height, out Vector3 normal)
		{
			Vector3 vector = Vector3.ProjectOnPlane(position - _sampleCenter, Vector3.up);
			height = _height + _slopeX * vector.x + _slopeZ * vector.z;
			normal = _normal;
			if (!_disposed && _valid && Time.unscaledTime - _completedAt <= 0.2f)
			{
				return vector.sqrMagnitude <= 9f;
			}
			return false;
		}

		public void Dispose()
		{
			if (!_disposed)
			{
				_disposed = true;
				_request.onCompleted -= OnCompleted;
				_request.Dispose();
			}
		}
	}
}
