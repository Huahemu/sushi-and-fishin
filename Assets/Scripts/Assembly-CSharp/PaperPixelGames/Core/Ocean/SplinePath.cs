using UnityEngine;

namespace PaperPixelGames.Core.Ocean
{
	public class SplinePath : MonoBehaviour
	{
		[SerializeField]
		private Transform _pathParent;

		private int _samplesPerSegment = 20;

		private Color _curveColor = Color.cyan;

		private Color _handleColor = Color.gray;

		private Transform[] _pathPoints;

		public int SegmentCount
		{
			get
			{
				int num = ((_pathPoints != null) ? _pathPoints.Length : 0);
				if (num < 4)
				{
					return 0;
				}
				return (num - 1) / 3;
			}
		}

		private void Awake()
		{
			RefreshPathPoints();
		}

		private void OnValidate()
		{
			RefreshPathPoints();
		}

		public bool TryGetSegmentPoints(int segmentIndex, out Vector3 p0, out Vector3 p1, out Vector3 p2, out Vector3 p3)
		{
			p0 = (p1 = (p2 = (p3 = default)));
			if (_pathPoints == null)
			{
				return false;
			}
			if (segmentIndex < 0 || segmentIndex >= SegmentCount)
			{
				return false;
			}
			int num = segmentIndex * 3;
			p0 = _pathPoints[num].position;
			p1 = _pathPoints[num + 1].position;
			p2 = _pathPoints[num + 2].position;
			p3 = _pathPoints[num + 3].position;
			return true;
		}

		public Vector3 Evaluate(float tGlobal)
		{
			int num = Mathf.Clamp(Mathf.FloorToInt(tGlobal), 0, Mathf.Max(0, SegmentCount - 1));
			float t = Mathf.Clamp01(tGlobal - (float)num);
			TryGetSegmentPoints(num, out var p, out var p2, out var p3, out var p4);
			return Cubic(in p, in p2, in p3, in p4, t);
		}

		public static Vector3 Cubic(in Vector3 p0, in Vector3 p1, in Vector3 p2, in Vector3 p3, float t)
		{
			float num = 1f - t;
			return num * num * num * p0 + 3f * (num * num) * t * p1 + 3f * num * (t * t) * p2 + t * t * t * p3;
		}

		private void RefreshPathPoints()
		{
			if (_pathParent == null)
			{
				_pathPoints = null;
				return;
			}
			int childCount = _pathParent.childCount;
			_pathPoints = new Transform[childCount];
			for (int i = 0; i < childCount; i++)
			{
				_pathPoints[i] = _pathParent.GetChild(i);
			}
		}

		private void OnDrawGizmos()
		{
			if (_pathPoints == null || _pathPoints.Length < 4)
			{
				return;
			}
			int segmentCount = SegmentCount;
			if (segmentCount <= 0)
			{
				return;
			}
			Gizmos.color = _handleColor;
			for (int i = 0; i < segmentCount; i++)
			{
				int num = i * 3;
				Gizmos.DrawLine(_pathPoints[num].position, _pathPoints[num + 1].position);
				Gizmos.DrawLine(_pathPoints[num + 2].position, _pathPoints[num + 3].position);
			}
			Gizmos.color = _curveColor;
			for (int j = 0; j < segmentCount; j++)
			{
				int num2 = j * 3;
				Vector3 p = _pathPoints[num2].position;
				Vector3 p2 = _pathPoints[num2 + 1].position;
				Vector3 p3 = _pathPoints[num2 + 2].position;
				Vector3 p4 = _pathPoints[num2 + 3].position;
				Vector3 vector = p;
				for (int k = 1; k <= _samplesPerSegment; k++)
				{
					float t = (float)k / (float)_samplesPerSegment;
					Vector3 vector2 = Cubic(in p, in p2, in p3, in p4, t);
					Gizmos.DrawLine(vector, vector2);
					vector = vector2;
				}
			}
			for (int l = 0; l < _pathPoints.Length; l++)
			{
				Vector3 position = _pathPoints[l].position;
				bool flag = l % 3 == 0;
				Gizmos.color = (flag ? Color.yellow : Color.cyan);
				Gizmos.DrawSphere(position, flag ? 0.15f : 0.1f);
			}
		}
	}
}
