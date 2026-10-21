using System;
using System.Linq;
using Nav3D.API;
using UnityEngine;

namespace Nav3D.Demo
{
	public class WaypointAgentController : MonoBehaviour
	{
		[SerializeField]
		private Transform[] m_WaypointsTransforms;

		[SerializeField]
		private bool m_Loop;

		[SerializeField]
		private Material m_PathRendererMaterial;

		private Nav3DAgent m_Agent;

		private LineRenderer m_PathRenderer;

		private Vector3[] m_Waypoints;

		private void Awake()
		{
			m_Agent = GetComponent<Nav3DAgent>();
			m_PathRenderer = GetComponent<LineRenderer>();
			m_PathRenderer.material = m_PathRendererMaterial;
			LineRenderer pathRenderer = m_PathRenderer;
			float startWidth = (m_PathRenderer.endWidth = 0.05f);
			pathRenderer.startWidth = startWidth;
			m_Agent.OnPathUpdated += UpdatePathRenderer;
			m_Waypoints = m_WaypointsTransforms.Select((Transform _Transform) => _Transform.position).ToArray();
			Nav3DManager.OnNav3DInit += MoveAlongWaypoints;
		}

		private void MoveAlongWaypoints()
		{
			Nav3DAgent agent = m_Agent;
			Vector3[] waypoints = m_Waypoints;
			bool loop = m_Loop;
			Action<Vector3> onTargetPassed = OnTargetPassed;
			Action<PathfindingResult> onPathfindingFail = OnPathfindingFail;
			Action onFinished = PointsFollowingFinished;
			agent.MoveToPoints(waypoints, loop, _StartFromClosest: false, _SkipUnpassableTargets: false, null, onTargetPassed, onFinished, null, onPathfindingFail);
		}

		private void UpdatePathRenderer(PathfindingResult _Result)
		{
			m_PathRenderer.positionCount = _Result.PathFinal.Length;
			m_PathRenderer.SetPositions(_Result.PathFinal);
		}

		private void OnTargetPassed(Vector3 _Target)
		{
			Debug.Log($"{base.name} passed target: {_Target}");
		}

		private void OnPathfindingFail(PathfindingResult _Result)
		{
			Debug.LogError(_Result.ErrorMessage);
		}

		private void PointsFollowingFinished()
		{
			Debug.Log(base.name + " finished points following.");
		}
	}
}
