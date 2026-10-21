using System;
using System.Collections.Generic;
using Nav3D.Common;
using Nav3D.LocalAvoidance.SupportingMath;
using UnityEngine;

namespace Nav3D.Pathfinding
{
	public class PathFollowData
	{
		private readonly string LOG_CTOR = "PathFollowData.ctor: {0}";

		private readonly string LOG_INVALIDATE = "PathFollowData.Invalidate: {0}";

		private readonly Vector3[] m_Path;

		private readonly int[] m_TargetIndices;

		private readonly Action<Vector3> m_OnTargetPassed;

		private readonly Action m_OnLastTargetReached;

		private readonly float m_ReachDistance;

		private readonly Log m_Log;

		private int m_NextIndex;

		private Vector3 m_NextPoint;

		private int m_NextTargetInTargetsIndex;

		private Vector3 m_NextTarget;

		public bool IsValid { get; private set; } = true;

		public PathFollowData(Vector3[] _Trajectory, int[] _TargetIndices, Vector3 _FollowerPoint, float _ReachDist, Action<Vector3> _OnTargetPassed, Action _OnLastTargetReached, Log _Log)
		{
			m_Log = _Log;
			m_Log?.WriteFormat(LOG_CTOR, GetHashCode());
			m_Path = _Trajectory;
			m_TargetIndices = _TargetIndices;
			m_OnTargetPassed = _OnTargetPassed;
			m_OnLastTargetReached = _OnLastTargetReached;
			m_ReachDistance = _ReachDist;
			GetClosestPoint(_FollowerPoint, _ReachDist, out m_NextPoint, out m_NextIndex, out m_NextTargetInTargetsIndex);
			m_NextTarget = m_Path[m_TargetIndices[m_NextTargetInTargetsIndex]];
		}

		public string GetNextTargetInfo()
		{
			return $"Next target index: {m_NextTargetInTargetsIndex}, next target index on path: {m_TargetIndices[m_NextTargetInTargetsIndex]}, next target position: {m_Path[m_TargetIndices[m_NextTargetInTargetsIndex]]}";
		}

		public Vector3 GetMovePoint(float _Speed, float _Radius, Vector3 _CurrentFollowerPosition)
		{
			m_NextPoint = m_Path[m_NextIndex];
			float magnitude = (m_NextPoint - _CurrentFollowerPosition).magnitude;
			while (magnitude <= _Speed + m_ReachDistance && m_NextIndex < m_Path.Length - 1)
			{
				_Speed -= magnitude;
				_CurrentFollowerPosition = m_NextPoint;
				CheckTargetPoints();
				m_NextIndex++;
				m_NextPoint = m_Path[m_NextIndex];
				magnitude = (m_NextPoint - _CurrentFollowerPosition).magnitude;
				if (m_NextIndex == m_Path.Length - 1)
				{
					CheckTargetPoints();
				}
			}
			if (m_NextTargetInTargetsIndex == m_TargetIndices.Length - 1 && m_TargetIndices[m_NextTargetInTargetsIndex] == m_NextIndex && (m_NextTarget - _CurrentFollowerPosition).sqrMagnitude <= m_ReachDistance)
			{
				m_OnTargetPassed?.Invoke(m_Path[m_TargetIndices[m_NextTargetInTargetsIndex]]);
				m_OnLastTargetReached?.Invoke();
			}
			return m_NextPoint;
			void CheckTargetPoints()
			{
				if (m_NextTargetInTargetsIndex < m_TargetIndices.Length && m_TargetIndices[m_NextTargetInTargetsIndex] < m_NextIndex)
				{
					m_OnTargetPassed?.Invoke(m_Path[m_TargetIndices[m_NextTargetInTargetsIndex]]);
					if (m_NextTargetInTargetsIndex < m_TargetIndices.Length - 1)
					{
						m_NextTargetInTargetsIndex++;
						m_NextTarget = m_Path[m_TargetIndices[m_NextTargetInTargetsIndex]];
					}
				}
			}
		}

		public Vector3[] GetUnpassedTargets(bool _Loop)
		{
			List<Vector3> list = new List<Vector3>(m_TargetIndices.Length);
			for (int i = m_NextTargetInTargetsIndex; i < m_TargetIndices.Length; i++)
			{
				list.Add(m_Path[m_TargetIndices[i]]);
			}
			if (!_Loop)
			{
				return list.ToArray();
			}
			for (int j = 0; j < m_NextTargetInTargetsIndex; j++)
			{
				list.Add(m_Path[m_TargetIndices[j]]);
			}
			return list.ToArray();
		}

		public void GetDistToClosestOnPath(Vector3 _Point, out Vector3 _Closest, out float _Magnitude)
		{
			int num = m_NextIndex;
			if (m_NextIndex == m_Path.Length)
			{
				num--;
			}
			int num2 = num - 1;
			if (num == 0)
			{
				_Closest = m_Path[num];
				_Magnitude = (_Closest - _Point).magnitude;
				return;
			}
			Vector3 vector = m_Path[num];
			Vector3 vector2 = m_Path[num2];
			if (vector == vector2)
			{
				_Closest = vector;
				_Magnitude = 0f;
				return;
			}
			Vector3 vector3 = new Straight(vector - vector2, vector2).ClosestPoint(_Point);
			float magnitude = (vector2 - _Point).magnitude;
			float magnitude2 = (vector - _Point).magnitude;
			float magnitude3 = (vector3 - _Point).magnitude;
			if (magnitude < magnitude2 && magnitude < magnitude3)
			{
				_Closest = vector2;
				_Magnitude = magnitude;
			}
			else if (magnitude2 < magnitude && magnitude2 < magnitude3)
			{
				_Closest = vector;
				_Magnitude = magnitude2;
			}
			else
			{
				_Closest = vector3;
				_Magnitude = magnitude3;
			}
		}

		public void RestartFollowing(Vector3 _FollowerPoint, float _ReachDistSqr)
		{
			GetClosestPoint(_FollowerPoint, _ReachDistSqr, out m_NextPoint, out m_NextIndex, out m_NextTargetInTargetsIndex);
			m_NextTarget = m_Path[m_TargetIndices[m_NextTargetInTargetsIndex]];
		}

		public void Invalidate()
		{
			m_Log?.WriteFormat(LOG_INVALIDATE, GetHashCode());
			IsValid = false;
		}

		private void GetClosestPoint(Vector3 _Point, float _ReachDistSqr, out Vector3 _ClosestPoint, out int _Index, out int _NextTargetInTargetsIndex)
		{
			Vector3 vector = Vector3.positiveInfinity;
			int minIndex = int.MaxValue;
			if ((m_Path[0] - _Point).sqrMagnitude < _ReachDistSqr && (m_Path[m_Path.Length - 1] - _Point).sqrMagnitude < _ReachDistSqr)
			{
				_ClosestPoint = m_Path[0];
				_Index = 0;
				_NextTargetInTargetsIndex = 0;
				return;
			}
			_NextTargetInTargetsIndex = 0;
			float num = float.MaxValue;
			for (int i = 0; i < m_Path.Length; i++)
			{
				Vector3 vector2 = m_Path[i];
				float sqrMagnitude = (vector2 - _Point).sqrMagnitude;
				if (sqrMagnitude < num)
				{
					minIndex = i;
					vector = vector2;
					num = sqrMagnitude;
				}
			}
			_NextTargetInTargetsIndex = m_TargetIndices.FindIndex((int _TargetIndex) => _TargetIndex >= minIndex);
			if (_NextTargetInTargetsIndex < 0)
			{
				_NextTargetInTargetsIndex = m_TargetIndices.Length - 1;
			}
			_ClosestPoint = vector;
			_Index = minIndex;
		}
	}
}
