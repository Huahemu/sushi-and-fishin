using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Nav3D.Obstacles;

namespace Nav3D.API
{
	public class ObstacleAdditionProgress : IObstacleAdditionProgress
	{
		public enum ObstacleAdditionStatus
		{
			QUEUE_WAITING = 0,
			OBSTACLES_CLUSTERIZATION = 1,
			OBSTACLES_GRAPH_CONSTRUCTION = 2,
			REMOVING_OBSOLETE_OBSTACLES = 3,
			FINISHED = 4
		}

		private const string WAITING_FOR_BEGIN_INFO = "Waiting for processing to begin..";

		private const string OBSTACLES_CLUSTERING_INFO = "Obstacle clustering..";

		private const string OBSOLETE_OBSTACLES_CLEARING_INFO = "Clearing obsolete obstacles..";

		private const string FINISHED_INFO = "Obstacle addition finished!";

		private const string PROCESSING_INFO = "Processing";

		private const string RESULT_STATS_HEADER = "Obstacle addition stats:";

		private const string RESULT_STATS_IN_PROGRESS_INFO = "Obstacle adding still in progress. Please stand by.";

		private readonly string RESULT_STATS_TRIANGLE_STORAGE_CONSTRUCTION_INFO = "Building a triangle storage takes: {0}";

		private readonly string RESULT_STATS_OCTREE_PREPARATION_INFO = "Octree construction preparations takes: {0}";

		private readonly string RESULT_STATS_OCTREE_CONSTRUCTION_INFO = "Octree construction takes: {0}";

		private readonly string RESULT_STATS_GRAPH_CONSTRUCTION_INFO = "Graph construction takes: {0}";

		private readonly string RESULT_STATS_TOTAL_PROCESSING_INFO = "Total processing takes: {0}";

		private List<GraphConstructionProgress> m_Constructions = new List<GraphConstructionProgress>();

		private GraphConstructionProgress m_CurrentConstructionProgress;

		private ObstacleAdditionStatus m_Status;

		private CancellationTokenSource m_CTS = new CancellationTokenSource();

		public static ObstacleAdditionProgress INITIAL => new ObstacleAdditionProgress
		{
			m_Status = ObstacleAdditionStatus.QUEUE_WAITING
		};

		public float Progress { get; private set; }

		public bool Finished => m_Status == ObstacleAdditionStatus.FINISHED;

		public ObstacleAdditionResult Result { get; private set; }

		public CancellationToken CancellationToken => m_CTS.Token;

		public string GetStatusInfo()
		{
			return m_Status switch
			{
				ObstacleAdditionStatus.QUEUE_WAITING => "Waiting for processing to begin..", 
				ObstacleAdditionStatus.OBSTACLES_CLUSTERIZATION => "Obstacle clustering..", 
				ObstacleAdditionStatus.OBSTACLES_GRAPH_CONSTRUCTION => m_CurrentConstructionProgress?.GetInfo() ?? "Processing", 
				ObstacleAdditionStatus.REMOVING_OBSOLETE_OBSTACLES => "Clearing obsolete obstacles..", 
				ObstacleAdditionStatus.FINISHED => "Obstacle addition finished!", 
				_ => "Processing", 
			};
		}

		public string GetResultStats(bool _InMilliseconds = false)
		{
			if (m_Status != ObstacleAdditionStatus.FINISHED)
			{
				return "Obstacle adding still in progress. Please stand by.";
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("Obstacle addition stats:");
			stringBuilder.AppendLine(string.Format(RESULT_STATS_TRIANGLE_STORAGE_CONSTRUCTION_INFO, _InMilliseconds ? Result.TriangleStorageBuildDuration.TotalMilliseconds.ToString() : Result.TriangleStorageBuildDuration.ToString()));
			stringBuilder.AppendLine(string.Format(RESULT_STATS_OCTREE_PREPARATION_INFO, _InMilliseconds ? Result.OctreeBulidPreparationsDuration.TotalMilliseconds.ToString() : Result.OctreeBulidPreparationsDuration.ToString()));
			stringBuilder.AppendLine(string.Format(RESULT_STATS_OCTREE_CONSTRUCTION_INFO, _InMilliseconds ? Result.OctreeBulidDuration.TotalMilliseconds.ToString() : Result.OctreeBulidDuration.ToString()));
			stringBuilder.AppendLine(string.Format(RESULT_STATS_GRAPH_CONSTRUCTION_INFO, _InMilliseconds ? Result.GraphBuildDuration.TotalMilliseconds.ToString() : Result.GraphBuildDuration.ToString()));
			stringBuilder.AppendLine(string.Format(RESULT_STATS_TOTAL_PROCESSING_INFO, _InMilliseconds ? Result.TotalProcessingDuration.TotalMilliseconds.ToString() : Result.TotalProcessingDuration.ToString()));
			return stringBuilder.ToString();
		}

		public void SetCurrentConstructionProgress(GraphConstructionProgress _ConstructionProgress)
		{
			m_CurrentConstructionProgress = _ConstructionProgress;
			m_Constructions.Add(_ConstructionProgress);
		}

		public void SetStatus(ObstacleAdditionStatus _Status)
		{
			m_Status = _Status;
			if (_Status == ObstacleAdditionStatus.FINISHED)
			{
				Result = ObstacleAdditionResult.GetCombinedResult(m_Constructions.Select((GraphConstructionProgress _ConstructionProgress) => _ConstructionProgress.Result).ToArray());
			}
		}

		public float GetProgress()
		{
			if (m_Status == ObstacleAdditionStatus.OBSTACLES_GRAPH_CONSTRUCTION)
			{
				return m_CurrentConstructionProgress?.Progress ?? Progress;
			}
			return Progress;
		}

		public void SetProgress(float _Progress)
		{
			Progress = _Progress;
		}

		public void CancelAddition()
		{
			if (m_Status != ObstacleAdditionStatus.FINISHED)
			{
				m_CTS.Cancel();
				m_CurrentConstructionProgress?.CancelConstruction();
			}
		}
	}
}
