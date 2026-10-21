using System;
using System.Collections.Generic;
using System.Linq;
using Nav3D.Common;
using Nav3D.Obstacles;
using UnityEngine;

namespace Nav3D.LocalAvoidance
{
	public class StaticTrianglesVOProvider
	{
		private readonly TimeSpan UPDATE_PERIOD = TimeSpan.FromMilliseconds(500.0);

		private readonly List<Triangle> m_NearestTriangles = new List<Triangle>();

		private Triangle m_ConsideringTriangle;

		private readonly IMovable m_Mover;

		private readonly float m_Radius;

		private readonly float m_MaxSpeed;

		private readonly float m_ORCATau;

		private readonly float m_VelocityRadiusProjected;

		private Vector3 m_LastUpdatePosition;

		private DateTime m_LastUpdateTime;

		private readonly bool m_ReturnIntersecting;

		public StaticTrianglesVOProvider(IMovable _Mover, float _Radius, float _MaxSpeed, float _ORCATau, bool _ReturnIntersecting)
		{
			m_Mover = _Mover;
			m_Radius = _Radius;
			m_MaxSpeed = _MaxSpeed;
			m_ORCATau = _ORCATau;
			m_ReturnIntersecting = _ReturnIntersecting;
			m_VelocityRadiusProjected = m_Radius + m_MaxSpeed * m_ORCATau;
		}

		public IVO GetVO()
		{
			Vector3 position = m_Mover.GetPosition();
			IVO result = null;
			TryUpdateNeighborTriangles();
			if (DateTime.Now - m_LastUpdateTime > UPDATE_PERIOD && (m_LastUpdatePosition - position).sqrMagnitude > UtilsMath.Sqr(m_MaxSpeed * 0.3f))
			{
				UpdateConsideredTriangles();
			}
			if (m_ConsideringTriangle != null)
			{
				if (m_ConsideringTriangle.Plane.GetDistanceToPoint(position) > m_Radius)
				{
					m_ConsideringTriangle = null;
					UpdateConsideredTriangles();
				}
				else
				{
					result = new VOObstacle(m_Mover, m_ConsideringTriangle, m_ORCATau);
				}
			}
			return result;
		}

		private void TryUpdateNeighborTriangles()
		{
			if (m_Mover.IsNeighborObstaclesDirty)
			{
				m_NearestTriangles.Clear();
				if (AgentManager.Instance.HasNearestObstacles(m_Mover, out var _DangerBounds))
				{
					m_NearestTriangles.AddRange(ObstacleManager.Instance.GetIntersectedObstaclesTriangles(_DangerBounds));
				}
				UpdateConsideredTriangles();
				m_Mover.SetNeighborObstaclesDirty(_Dirty: false);
			}
		}

		private void UpdateConsideredTriangles()
		{
			Vector3 moverPosition = m_Mover.GetPosition();
			if (!m_NearestTriangles.Any())
			{
				m_ConsideringTriangle = null;
				return;
			}
			Triangle triangle = m_NearestTriangles.MinBy((Triangle _Triangle) => (_Triangle.ClosestPointOnTriangle(moverPosition) - moverPosition).sqrMagnitude, out var _);
			if (m_ReturnIntersecting)
			{
				m_ConsideringTriangle = (triangle.Intersects(moverPosition, m_Radius) ? triangle : null);
			}
			else
			{
				m_ConsideringTriangle = (triangle.Intersects(moverPosition, m_VelocityRadiusProjected) ? triangle : null);
			}
			m_LastUpdatePosition = moverPosition;
			m_LastUpdateTime = DateTime.Now;
		}
	}
}
