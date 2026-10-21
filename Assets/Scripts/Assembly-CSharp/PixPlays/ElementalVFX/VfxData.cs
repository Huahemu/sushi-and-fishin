using UnityEngine;

namespace PixPlays.ElementalVFX
{
	public class VfxData
	{
		private float _duration;

		private float _radius;

		private Transform _sourceTransform;

		private Transform _targetTransform;

		private Transform _groundTransform;

		private Vector3 _sourcePos;

		private Vector3 _targetPos;

		private Vector3 _groundPos;

		public Vector3 Source
		{
			get
			{
				if (!(_sourceTransform != null))
				{
					return _sourcePos;
				}
				return _sourceTransform.position;
			}
		}

		public Vector3 Target
		{
			get
			{
				if (!(_targetTransform != null))
				{
					return _targetPos;
				}
				return _targetTransform.position;
			}
		}

		public Vector3 Ground
		{
			get
			{
				if (!(_groundTransform != null))
				{
					return _groundPos;
				}
				return _groundTransform.position;
			}
		}

		public float Duration => _duration;

		public float Radius => _radius;

		public VfxData(Vector3 source, Vector3 target, float duration, float radius)
		{
			_radius = radius;
			_sourcePos = source;
			_targetPos = target;
			_duration = duration;
		}

		public VfxData(Transform source, Vector3 target, float duration, float radius)
		{
			_radius = radius;
			_sourceTransform = source;
			_sourcePos = source.position;
			_targetPos = target;
			_duration = duration;
		}

		public VfxData(Transform source, Transform target, float duration, float radius)
		{
			_radius = radius;
			_sourceTransform = source;
			_targetTransform = target;
			_sourcePos = source.position;
			_targetPos = target.position;
			_duration = duration;
		}

		public void SetGround(Transform groundPoint)
		{
			_groundTransform = groundPoint;
			_groundPos = _groundTransform.position;
		}

		public void SetGround(Vector3 groundPos)
		{
			_groundPos = groundPos;
		}
	}
}
