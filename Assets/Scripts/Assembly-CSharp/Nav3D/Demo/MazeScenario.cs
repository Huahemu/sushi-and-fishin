using System.Collections.Generic;
using System.Linq;
using Nav3D.API;
using UnityEngine;
using UnityEngine.UI;

namespace Nav3D.Demo
{
	public class MazeScenario : MonoBehaviour
	{
		[Space]
		[Header("GameObjects")]
		[SerializeField]
		private Nav3DObstacle m_Obstacle;

		[SerializeField]
		private Transform m_PointATransform;

		[SerializeField]
		private Transform m_PointBTransform;

		[Space]
		[Header("Positions")]
		[SerializeField]
		private List<Transform> m_APositions = new List<Transform>();

		[SerializeField]
		private List<Transform> m_BPositions = new List<Transform>();

		[Space]
		[Header("Debug drawing")]
		[SerializeField]
		private bool m_DrawResolutionRegions;

		[Space]
		[Header("UI")]
		[SerializeField]
		private Text m_LoaderText;

		[SerializeField]
		private Button m_MoveRedButton;

		[SerializeField]
		private Button m_MoveGreenButton;

		private bool m_PathInited;

		private int m_APosIndex;

		private int m_BPosIndex;

		private Vector3 m_PointAPrePos;

		private Vector3 m_PointBPrePos;

		private LineRenderer m_PathRenderer;

		private Nav3DPath m_Path;

		private void Start()
		{
			m_PointATransform.position = m_APositions.First().position;
			m_PointBTransform.position = m_BPositions.First().position;
			BindUIHandlers();
			m_PathRenderer = GetComponent<LineRenderer>();
			m_PointAPrePos = m_PointATransform.position;
			m_PointBPrePos = m_PointBTransform.position;
			m_Obstacle.OnObstacleAdded += () =>
			{
				m_LoaderText.gameObject.SetActive(value: false);
				Debug.Log(m_Obstacle.AdditionProgress.GetResultStats());
			};
			Nav3DManager.OnNav3DInit += () =>
			{
				InitPath();
				UpdatePath();
			};
		}

		private void Update()
		{
			CheckPointsMoved();
		}

		private void InitPath()
		{
			if (!m_PathInited)
			{
				m_Path = new Nav3DPath();
				m_Path.OnPathfindingSuccess += OnPathUpdated;
				m_PathInited = true;
			}
		}

		private void OnPathUpdated(PathfindingResult _Result)
		{
			m_PathRenderer.positionCount = _Result.PathFinal.Length;
			m_PathRenderer.SetPositions(_Result.PathFinal);
		}

		private void UpdatePath()
		{
			m_Path?.Find(new PathfindingRequest(new Vector3[2] { m_PointAPrePos, m_PointBPrePos }, _Loop: false, _SkipUnpassableTargets: false, _TryRepositionStartIfOccupied: false, _TryRepositionTargetIfOccupied: false, 9000, _Smooth: true), (PathfindingResult _Result) =>
			{
				m_PathRenderer.positionCount = _Result.PathFinal.Length;
				m_PathRenderer.SetPositions(_Result.PathFinal);
			}, (PathfindingResult _Result) =>
			{
				Debug.LogError(_Result.ErrorMessage);
			});
		}

		private void CheckPointsMoved()
		{
			if (m_PointAPrePos != m_PointATransform.position || m_PointBPrePos != m_PointBTransform.position)
			{
				m_PointAPrePos = m_PointATransform.position;
				m_PointBPrePos = m_PointBTransform.position;
				UpdatePath();
			}
		}

		private void BindUIHandlers()
		{
			m_MoveRedButton.onClick.AddListener(MoveRed);
			m_MoveGreenButton.onClick.AddListener(MoveGreen);
		}

		private void MoveRed()
		{
			m_APosIndex++;
			if (m_APosIndex >= m_APositions.Count)
			{
				m_APosIndex = 0;
			}
			m_PointATransform.position = m_APositions[m_APosIndex].position;
			CheckPointsMoved();
		}

		private void MoveGreen()
		{
			m_BPosIndex++;
			if (m_BPosIndex >= m_BPositions.Count)
			{
				m_BPosIndex = 0;
			}
			m_PointBTransform.position = m_BPositions[m_BPosIndex].position;
			CheckPointsMoved();
		}
	}
}
