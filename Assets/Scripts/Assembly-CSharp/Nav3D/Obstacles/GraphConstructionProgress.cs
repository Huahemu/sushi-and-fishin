using System;
using System.Threading;
using Nav3D.API;

namespace Nav3D.Obstacles
{
	public class GraphConstructionProgress
	{
		public enum ConstructionStatus
		{
			WAITING_FOR_CONSTRUCTION = 0,
			FILLING_TRIANGLE_STORAGE = 1,
			TREE_CONSTRUCTION_PREPARATION = 2,
			TREE_CONSTRUCTION = 3,
			GRAPH_CONNECTIONS_BUILDING = 4,
			FINISHED = 5
		}

		private const string PROCESSING_GENERAL_INFO = "Processing..";

		private const string TREE_CONSTRUCTION_PREPARATION_INFO = "Preparing to build a tree..";

		private readonly string TIRIANGLE_STORAGE_PROGRESS_INFO = "{0} of {1} triangles registered.";

		private readonly string TREE_CONSTRUCTION = "Octree construction: {0}";

		private readonly string GRAPH_CONNECTIONS_BUILDING_INFO = "Forming leaves connections. {0} of {1} layers processed.";

		private int m_LeavesTotalCount;

		private int m_LeavesCounterCurrent;

		private int m_TrianglesCounterCurrent;

		private int m_TrianglesCounterTotal;

		private int m_LayersProcessedTotalCount;

		private int m_LayersProcessedCounterCurrent;

		private DateTime m_Start;

		private DateTime m_Finish;

		private TimeSpan m_TriangleStorageBuildDuration;

		private TimeSpan m_OctreeBulidPreparationsDuration;

		private TimeSpan m_OctreeBulidDuration;

		private TimeSpan m_GraphBuildDuration;

		private CancellationTokenSource m_CTS = new CancellationTokenSource();

		public static GraphConstructionProgress INITIAL => new GraphConstructionProgress
		{
			Status = ConstructionStatus.WAITING_FOR_CONSTRUCTION
		};

		public static GraphConstructionProgress COMPLETED => new GraphConstructionProgress
		{
			Status = ConstructionStatus.FINISHED
		};

		public ConstructionStatus Status { get; private set; }

		public float Progress { get; private set; }

		public ObstacleAdditionResult Result { get; private set; }

		public CancellationToken CancellationToken => m_CTS.Token;

		public void SetStatus(ConstructionStatus _Status)
		{
			Status = _Status;
			switch (_Status)
			{
			case ConstructionStatus.FILLING_TRIANGLE_STORAGE:
				m_Start = DateTime.UtcNow;
				break;
			case ConstructionStatus.TREE_CONSTRUCTION_PREPARATION:
				m_TriangleStorageBuildDuration = DateTime.UtcNow - m_Start;
				break;
			case ConstructionStatus.TREE_CONSTRUCTION:
				m_OctreeBulidPreparationsDuration = DateTime.UtcNow - m_Start - m_TriangleStorageBuildDuration;
				break;
			case ConstructionStatus.GRAPH_CONNECTIONS_BUILDING:
				m_OctreeBulidDuration = DateTime.UtcNow - m_Start - m_TriangleStorageBuildDuration - m_OctreeBulidPreparationsDuration;
				break;
			case ConstructionStatus.FINISHED:
				m_Finish = DateTime.UtcNow;
				m_GraphBuildDuration = m_Finish - m_Start - m_TriangleStorageBuildDuration - m_OctreeBulidPreparationsDuration - m_OctreeBulidDuration;
				Result = new ObstacleAdditionResult(m_Start, m_Finish, m_TriangleStorageBuildDuration, m_OctreeBulidPreparationsDuration, m_OctreeBulidDuration, m_GraphBuildDuration);
				break;
			}
		}

		public string GetInfo()
		{
			return Status switch
			{
				ConstructionStatus.FILLING_TRIANGLE_STORAGE => string.Format(TIRIANGLE_STORAGE_PROGRESS_INFO, m_TrianglesCounterCurrent, m_TrianglesCounterTotal), 
				ConstructionStatus.TREE_CONSTRUCTION_PREPARATION => "Preparing to build a tree..", 
				ConstructionStatus.TREE_CONSTRUCTION => string.Format(TREE_CONSTRUCTION, $"{Progress:P2}"), 
				ConstructionStatus.GRAPH_CONNECTIONS_BUILDING => string.Format(GRAPH_CONNECTIONS_BUILDING_INFO, m_LayersProcessedCounterCurrent, m_LayersProcessedTotalCount), 
				_ => "Processing..", 
			};
		}

		public void SetTrianglesStorageProgress(int _CurrentCounter, int _TotalCounter)
		{
			m_TrianglesCounterCurrent = _CurrentCounter;
			m_TrianglesCounterTotal = _TotalCounter;
			Progress = (float)m_TrianglesCounterCurrent / (float)m_TrianglesCounterTotal;
		}

		public void SetLayersProcessignProgress(int _CurrentCounter, int _TotalCounter)
		{
			m_LayersProcessedCounterCurrent = _CurrentCounter;
			m_LayersProcessedTotalCount = _TotalCounter;
			Progress = (float)m_LayersProcessedCounterCurrent / (float)m_LayersProcessedTotalCount;
		}

		public void SetTotalRootsCount(int _RootsCount)
		{
			m_LeavesTotalCount = _RootsCount;
		}

		public void AddTotalLeavesCount(int _AddValue)
		{
			Interlocked.Add(ref m_LeavesTotalCount, _AddValue);
			Progress = (float)m_LeavesCounterCurrent / (float)m_LeavesTotalCount;
		}

		public void AddProcessedLeavesCount(int _AddValue)
		{
			Interlocked.Add(ref m_LeavesCounterCurrent, _AddValue);
			Progress = (float)m_LeavesCounterCurrent / (float)m_LeavesTotalCount;
		}

		public void CancelConstruction()
		{
			m_CTS.Cancel();
		}
	}
}
