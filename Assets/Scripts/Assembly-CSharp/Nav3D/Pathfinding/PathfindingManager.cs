using System;
using System.Threading;
using System.Threading.Tasks;
using Nav3D.API;
using Nav3D.Common;
using UnityEngine;

namespace Nav3D.Pathfinding
{
	public class PathfindingManager : MonoBehaviour
	{
		private readonly string LOG_REQUEST_PATHFINDING = "PathfindingManager.RequestPathfinding: OrderID: {0}, Points: {1}, Looped {2}";

		private int m_PathFindingTasksMaxCount = Environment.ProcessorCount - 1;

		private float m_StorageBucketSize;

		private CurvesSpatialHashMap<Path> m_PathsStorage;

		private OrdersExecutor<IExecutable> m_PathfindingOrdersExecutor;

		[SerializeField]
		private bool m_DrawPathsStorage;

		[SerializeField]
		private bool m_DisplayExecutorStats;

		public static bool Doomed { get; private set; }

		public static PathfindingManager Instance => Singleton<PathfindingManager>.Instance;

		public int PathFindingTasksMaxCount
		{
			get
			{
				return m_PathFindingTasksMaxCount;
			}
			set
			{
				if (value != m_PathFindingTasksMaxCount)
				{
					m_PathFindingTasksMaxCount = Mathf.Max(value, 1);
					RecreateOrdersExecutor(m_PathFindingTasksMaxCount);
				}
			}
		}

		public int PathFindingTasksOperatingCount => m_PathfindingOrdersExecutor?.CurrentAliveOrders ?? 0;

		public void Initialize(float _StorageBucketSize)
		{
			RecreateOrdersExecutor(m_PathFindingTasksMaxCount);
			m_StorageBucketSize = _StorageBucketSize;
			m_PathsStorage = new CurvesSpatialHashMap<Path>(m_StorageBucketSize);
		}

		public void Uninitialize(bool _NeedDestroy = true)
		{
			if (_NeedDestroy)
			{
				UtilsCommon.SmartDestroy(this);
			}
		}

		public Task<PathfindingResult> RequestPathfinding(Path _Requester, PathfindingRequest _Request, CancellationToken _CancellationToken, Log _Log = null)
		{
			_Log?.WriteFormat(LOG_REQUEST_PATHFINDING, _Request.RequesterID, UtilsCommon.GetPointsString(_Request.Points), _Request.Loop);
			PathfindingOrder pathfindingOrder = new PathfindingOrder(_Request, _CancellationToken, Pathfinder.FindPath, _Log);
			m_PathfindingOrdersExecutor.EnqueueOrder(pathfindingOrder, _Log);
			return pathfindingOrder.Task;
		}

		public void UpdateAllBoundsCrossingPaths(Bounds _Bounds)
		{
			if (m_PathsStorage.TryGetIntersectingCurves(_Bounds, out var _IntersectingCurves))
			{
				_IntersectingCurves.ForEach((Path _Path) =>
				{
					_Path.Update();
				});
			}
		}

		public void UpdatePathInStorage(Path _Path)
		{
			m_PathsStorage.Update(_Path);
		}

		public void RemovePathFromStorage(Path _Path)
		{
			m_PathsStorage.Unregister(_Path);
		}

		public void DisposePath(Path _Path)
		{
			m_PathsStorage.Unregister(_Path);
		}

		private void RecreateOrdersExecutor(int _MaxAliveOrders)
		{
			m_PathfindingOrdersExecutor = new OrdersExecutor<IExecutable>(_MaxAliveOrders);
		}

		private void DrawStorage()
		{
			if (m_DrawPathsStorage)
			{
				m_PathsStorage.Draw();
			}
		}

		private void DisplayOrdersExecutorStats()
		{
			GUILayout.Label($"Executor alive orders: {m_PathfindingOrdersExecutor.CurrentAliveOrders}/{m_PathfindingOrdersExecutor.MaxConcurrencyLevel}");
			GUILayout.Label(m_PathfindingOrdersExecutor.GetRunningOrdersInfo());
		}

		private void Awake()
		{
			Doomed = false;
		}

		private void OnDestroy()
		{
			Doomed = true;
			Uninitialize(_NeedDestroy: false);
		}

		private void OnDrawGizmos()
		{
			if (Application.isPlaying && base.enabled)
			{
				DrawStorage();
			}
		}

		private void OnGUI()
		{
			if (Application.isPlaying && base.enabled && m_DisplayExecutorStats)
			{
				DisplayOrdersExecutorStats();
			}
		}
	}
}
