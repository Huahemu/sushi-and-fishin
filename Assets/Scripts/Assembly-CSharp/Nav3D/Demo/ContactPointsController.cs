using System.Collections.Generic;
using System.Linq;
using Nav3D.Common;
using UnityEngine;

namespace Nav3D.Demo
{
	public class ContactPointsController : MonoBehaviour
	{
		private const int CONTACT_POINTS_COUNT = 15;

		private const float CONTACT_RADIUS = 0.65f;

		[Space]
		[Header("Values")]
		[SerializeField]
		private int m_ContactPointsCount = 15;

		[SerializeField]
		private float m_ContactRadius = 0.65f;

		[Space]
		[Header("Debug parameters")]
		[SerializeField]
		private bool m_DrawContactPoints;

		private List<Vector3> m_Points = new List<Vector3>(15);

		private List<Vector3> m_FreePoints = new List<Vector3>(15);

		private List<Vector3> m_OccupiedPoints = new List<Vector3>(15);

		public Vector3? GetClosestFreeTouchPoint(Vector3 _NearestPoint)
		{
			if (!m_FreePoints.Any())
			{
				return null;
			}
			return m_FreePoints.MinBy((Vector3 _Point) => Vector3.SqrMagnitude(_Point - _NearestPoint));
		}

		public Vector3? GetRandomFreeTouchPoint()
		{
			if (!m_FreePoints.Any())
			{
				return null;
			}
			return m_FreePoints[Random.Range(0, m_FreePoints.Count)];
		}

		public Vector3 GetClosestTouchPoint(Vector3 _NearestPoint)
		{
			return m_Points.MinBy((Vector3 _Point) => Vector3.SqrMagnitude(_Point - _NearestPoint));
		}

		public void OccupyTouchPoint(Vector3 _Point)
		{
			if (m_FreePoints.Contains(_Point))
			{
				m_FreePoints.Remove(_Point);
				m_OccupiedPoints.Add(_Point);
			}
		}

		public void ReleaseTouchPoint(Vector3 _Point)
		{
			if (m_OccupiedPoints.Contains(_Point))
			{
				m_OccupiedPoints.Remove(_Point);
				m_FreePoints.Add(_Point);
			}
		}

		private void Start()
		{
			m_FreePoints = UtilsMath.GetSphereSurfacePoints(base.transform.position, m_ContactRadius, m_ContactPointsCount).ToList();
			m_Points.AddRange(m_FreePoints);
		}
	}
}
