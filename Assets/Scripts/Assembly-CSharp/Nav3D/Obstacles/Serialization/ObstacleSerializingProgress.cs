using System;
using System.Text;
using System.Threading;
using Nav3D.API;

namespace Nav3D.Obstacles.Serialization
{
	public class ObstacleSerializingProgress
	{
		public enum ObstacleSerializingStatus
		{
			PREPARING_OBSTACLE_INFOS = 0,
			BAKING_OBSTACLES = 1,
			PACKING_BAKED_DATA = 2,
			SERIALIZING_DATA = 3,
			COMPRESSING_DATA = 4,
			FINISHED = 5
		}

		private const string SERIALIZING_GENERAL = "Serializing in progress..";

		private const string TITLE = "Obstacle serializing progress";

		private const string PREPARING_OBSTACLE_INFOS = "Information about obstacles is being prepared..";

		private readonly string PACKING_BACKED_OBSTACLES_DATA = "Packing data: {0} / {1}";

		private const string SERIALIZING = "Serializing data..";

		private const string COMPRESSING = "Compressing packed data..";

		private const string RESULT_STATS_HEADER = "Obstacle serializing stats:";

		private const string RESULT_STATS_IN_PROGRESS_INFO = "Obstacle serializing still in progress. Please stand by.";

		private readonly string RESULT_STATS_PREPARING_INFOS_INFO = "Preparing obstacle infos takes: {0}";

		private readonly string RESULT_STATS_BACKING_INFO = "Backing obstacles takes: {0}";

		private readonly string RESULT_STATS_PACKING_INFO = "Packing data takes: {0}";

		private readonly string RESULT_STATS_SERIALIZING_INFO = "Serializing takes: {0}";

		private readonly string RESULT_STATS_COMPRESSING_INFO = "Compressing takes: {0}";

		private readonly string RESULT_STATS_TOTAL_PROCESSING_INFO = "Total processing takes: {0}";

		private ObstacleSerializingStatus m_Status;

		private DateTime m_Start;

		private DateTime m_Finish;

		private TimeSpan m_PreparingInfosDuration;

		private TimeSpan m_BakingObstaclesDuration;

		private TimeSpan m_PackingBakedDataDuration;

		private TimeSpan m_SerializingDuration;

		private TimeSpan m_CompressingDuration;

		private TimeSpan m_TotalProcessingDuration;

		private CancellationTokenSource m_CTS = new CancellationTokenSource();

		private int m_TotalNodesToPackCount;

		private int m_PackedNodesCount;

		public static ObstacleSerializingProgress INITIAL
		{
			get
			{
				ObstacleSerializingProgress obstacleSerializingProgress = new ObstacleSerializingProgress();
				obstacleSerializingProgress.SetStatus(ObstacleSerializingStatus.PREPARING_OBSTACLE_INFOS);
				return obstacleSerializingProgress;
			}
		}

		public ObstacleAdditionProgress ObstacleAdditionProgress { get; private set; }

		public ObstacleSerializingResult Result { get; private set; }

		public CancellationToken CancellationToken => m_CTS.Token;

		public float Progress { get; private set; }

		public void SetObstacleAdditionProgress(ObstacleAdditionProgress _ObstacleAdditionProgress)
		{
			ObstacleAdditionProgress = _ObstacleAdditionProgress;
		}

		public void SetNodesPackingProgress(int _Counter, int _TotalCount)
		{
			m_PackedNodesCount = _Counter;
			m_TotalNodesToPackCount = _TotalCount;
			Progress = (float)m_PackedNodesCount / (float)m_TotalNodesToPackCount;
		}

		public void SetStatus(ObstacleSerializingStatus _Status)
		{
			m_Status = _Status;
			switch (_Status)
			{
			case ObstacleSerializingStatus.PREPARING_OBSTACLE_INFOS:
				m_Start = DateTime.UtcNow;
				break;
			case ObstacleSerializingStatus.BAKING_OBSTACLES:
				m_PreparingInfosDuration = DateTime.UtcNow - m_Start;
				break;
			case ObstacleSerializingStatus.PACKING_BAKED_DATA:
				m_BakingObstaclesDuration = DateTime.UtcNow - m_Start - m_PreparingInfosDuration;
				break;
			case ObstacleSerializingStatus.SERIALIZING_DATA:
				m_PackingBakedDataDuration = DateTime.UtcNow - m_Start - m_PreparingInfosDuration - m_BakingObstaclesDuration;
				break;
			case ObstacleSerializingStatus.COMPRESSING_DATA:
				m_SerializingDuration = DateTime.UtcNow - m_Start - m_PreparingInfosDuration - m_BakingObstaclesDuration - m_PackingBakedDataDuration;
				break;
			case ObstacleSerializingStatus.FINISHED:
				m_Finish = DateTime.UtcNow;
				m_CompressingDuration = m_Finish - m_Start - m_PreparingInfosDuration - m_BakingObstaclesDuration - m_PackingBakedDataDuration - m_SerializingDuration;
				m_TotalProcessingDuration = m_Finish - m_Start;
				Result = new ObstacleSerializingResult(m_Start, m_Finish, m_PreparingInfosDuration, m_BakingObstaclesDuration, m_PackingBakedDataDuration, m_SerializingDuration, m_CompressingDuration);
				break;
			}
		}

		public float GetProgress()
		{
			if (m_Status == ObstacleSerializingStatus.BAKING_OBSTACLES)
			{
				return ObstacleAdditionProgress?.GetProgress() ?? Progress;
			}
			return Progress;
		}

		public string GetTitle()
		{
			return "Obstacle serializing progress";
		}

		public string GetInfo()
		{
			return m_Status switch
			{
				ObstacleSerializingStatus.PREPARING_OBSTACLE_INFOS => "Information about obstacles is being prepared..", 
				ObstacleSerializingStatus.BAKING_OBSTACLES => ObstacleAdditionProgress?.GetStatusInfo() ?? "Serializing in progress..", 
				ObstacleSerializingStatus.PACKING_BAKED_DATA => string.Format(PACKING_BACKED_OBSTACLES_DATA, m_PackedNodesCount, m_TotalNodesToPackCount), 
				ObstacleSerializingStatus.COMPRESSING_DATA => "Compressing packed data..", 
				ObstacleSerializingStatus.SERIALIZING_DATA => "Serializing data..", 
				_ => "Serializing in progress..", 
			};
		}

		public string GetResultStats(bool _InMilliseconds = false)
		{
			if (m_Status != ObstacleSerializingStatus.FINISHED)
			{
				return "Obstacle serializing still in progress. Please stand by.";
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("Obstacle serializing stats:");
			stringBuilder.AppendLine(string.Format(RESULT_STATS_PREPARING_INFOS_INFO, _InMilliseconds ? m_PreparingInfosDuration.Milliseconds.ToString() : m_PreparingInfosDuration.ToString()));
			stringBuilder.AppendLine(string.Format(RESULT_STATS_BACKING_INFO, _InMilliseconds ? m_BakingObstaclesDuration.Milliseconds.ToString() : m_BakingObstaclesDuration.ToString()));
			stringBuilder.AppendLine(string.Format(RESULT_STATS_PACKING_INFO, _InMilliseconds ? m_PackingBakedDataDuration.Milliseconds.ToString() : m_PackingBakedDataDuration.ToString()));
			stringBuilder.AppendLine(string.Format(RESULT_STATS_SERIALIZING_INFO, _InMilliseconds ? m_SerializingDuration.Milliseconds.ToString() : m_SerializingDuration.ToString()));
			stringBuilder.AppendLine(string.Format(RESULT_STATS_COMPRESSING_INFO, _InMilliseconds ? m_CompressingDuration.Milliseconds.ToString() : m_CompressingDuration.ToString()));
			stringBuilder.AppendLine(string.Format(RESULT_STATS_TOTAL_PROCESSING_INFO, _InMilliseconds ? m_TotalProcessingDuration.Milliseconds.ToString() : m_TotalProcessingDuration.ToString()));
			return stringBuilder.ToString();
		}

		public void CancelSerialization()
		{
			if (m_Status != ObstacleSerializingStatus.FINISHED)
			{
				m_CTS.Cancel();
				ObstacleAdditionProgress?.CancelAddition();
			}
		}
	}
}
