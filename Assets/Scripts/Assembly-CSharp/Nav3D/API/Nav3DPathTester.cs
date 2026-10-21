using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Nav3D.API
{
	public class Nav3DPathTester : MonoBehaviour
	{
		[SerializeField]
		private List<Transform> m_Targets = new List<Transform>(2);

		[SerializeField]
		private bool m_Loop;

		[SerializeField]
		private bool m_Smooth = true;

		[SerializeField]
		private int m_SmoothRatio = 3;

		[SerializeField]
		private bool m_SkipUnpassableTargets;

		[SerializeField]
		[Header("Debug")]
		private bool m_Draw;

		[SerializeField]
		[Header("External Points Debug")]
		private bool m_UseExternalPoints;

		private readonly Dictionary<Transform, Vector3> m_CachedTargetPositions = new Dictionary<Transform, Vector3>();

		private bool m_CachedLoop;

		private bool m_CachedSmooth;

		private int m_CachedSmoothRatio;

		private bool m_CachedSkipUnpassableTargets;

		private Vector3[] m_ExternalPoints;

		private Vector3[] m_CachedExternalPoints;

		private Nav3DPath m_Path;

		private bool m_NavInitHooked;

		private void Update()
		{
			if (!m_UseExternalPoints)
			{
				if (m_Targets != null && m_Targets.Count >= 2 && NeedsUpdateFromTransforms())
				{
					UpdatePathFromTransforms();
				}
			}
			else if (m_ExternalPoints != null && m_ExternalPoints.Length >= 2 && NeedsUpdateFromExternalPoints())
			{
				UpdatePathFromExternalPoints();
			}
		}

		private void OnDisable()
		{
			m_Path?.Dispose();
		}

		public void SetExternalPoints(Vector3[] points, bool loop, bool smooth, int smoothRatio, bool skipUnpassableTargets, bool draw = true)
		{
			m_UseExternalPoints = true;
			m_Draw = draw;
			m_Loop = loop;
			m_Smooth = smooth;
			m_SmoothRatio = Mathf.Max(1, smoothRatio);
			m_SkipUnpassableTargets = skipUnpassableTargets;
			m_ExternalPoints = points;
			UpdatePathFromExternalPoints();
		}

		public void ClearExternalPoints()
		{
			m_UseExternalPoints = false;
			m_ExternalPoints = null;
			m_CachedExternalPoints = null;
		}

		private bool NeedsUpdateFromTransforms()
		{
			if (m_CachedLoop != m_Loop || m_CachedSmooth != m_Smooth || m_CachedSmoothRatio != m_SmoothRatio || m_CachedSkipUnpassableTargets != m_SkipUnpassableTargets || m_Targets.Count != m_CachedTargetPositions.Count)
			{
				return true;
			}
			for (int i = 0; i < m_Targets.Count; i++)
			{
				Transform transform = m_Targets[i];
				if (transform == null)
				{
					return true;
				}
				if (!m_CachedTargetPositions.TryGetValue(transform, out var value))
				{
					return true;
				}
				if (value != transform.position)
				{
					return true;
				}
			}
			return false;
		}

		private bool NeedsUpdateFromExternalPoints()
		{
			if (m_CachedLoop != m_Loop || m_CachedSmooth != m_Smooth || m_CachedSmoothRatio != m_SmoothRatio || m_CachedSkipUnpassableTargets != m_SkipUnpassableTargets)
			{
				return true;
			}
			if (m_CachedExternalPoints == null || m_ExternalPoints == null)
			{
				return true;
			}
			if (m_CachedExternalPoints.Length != m_ExternalPoints.Length)
			{
				return true;
			}
			for (int i = 0; i < m_ExternalPoints.Length; i++)
			{
				if (m_CachedExternalPoints[i] != m_ExternalPoints[i])
				{
					return true;
				}
			}
			return false;
		}

		private void UpdatePathFromTransforms()
		{
			m_CachedTargetPositions.Clear();
			for (int i = 0; i < m_Targets.Count; i++)
			{
				Transform transform = m_Targets[i];
				if (!(transform == null))
				{
					m_CachedTargetPositions[transform] = transform.position;
				}
			}
			CacheCommonSettings();
			if (m_Path == null)
			{
				m_Path = new Nav3DPath();
			}
			Vector3[] points = (from t in m_Targets
				where t != null
				select t.position).ToArray();
			BuildPath(points);
		}

		private void UpdatePathFromExternalPoints()
		{
			if (m_ExternalPoints != null)
			{
				CacheCommonSettings();
				m_CachedExternalPoints = new Vector3[m_ExternalPoints.Length];
				Array.Copy(m_ExternalPoints, m_CachedExternalPoints, m_ExternalPoints.Length);
				if (m_Path == null)
				{
					m_Path = new Nav3DPath();
				}
				BuildPath(m_ExternalPoints);
			}
		}

		private void CacheCommonSettings()
		{
			m_CachedLoop = m_Loop;
			m_CachedSmooth = m_Smooth;
			m_CachedSmoothRatio = (m_SmoothRatio = Mathf.Max(m_SmoothRatio, 1));
			m_CachedSkipUnpassableTargets = m_SkipUnpassableTargets;
		}

		private void BuildPath(Vector3[] points)
		{
			if (points != null && points.Length >= 2)
			{
				if (!m_NavInitHooked)
				{
					m_NavInitHooked = true;
					Nav3DManager.OnNav3DInit += TryFindPath;
				}
				TryFindPath();
			}
			void TryFindPath()
			{
				m_Path.Find(new PathfindingRequest(points, m_CachedLoop, m_CachedSkipUnpassableTargets, _TryRepositionStartIfOccupied: false, _TryRepositionTargetIfOccupied: false, 2000, m_CachedSmooth, m_CachedSmoothRatio, $"{base.gameObject.name} [{GetInstanceID()}]"), null, OnPathfindingFail);
			}
		}

		private void OnPathfindingFail(PathfindingResult result)
		{
			Debug.LogError($"Pathfinding failed: {result.Result}. Message: {result.ErrorMessage}");
		}
	}
}
