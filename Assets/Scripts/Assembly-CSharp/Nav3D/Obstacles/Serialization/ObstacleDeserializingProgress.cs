using System;
using System.Text;
using Nav3D.API;

namespace Nav3D.Obstacles.Serialization
{
	public class ObstacleDeserializingProgress : IObstacleAdditionProgress
	{
		public enum ObstacleDeserializingStatus
		{
			DECOMPRESSION = 0,
			DESERIALIZING = 1,
			UNPACKING = 2,
			FINISHED = 3,
			FAILED = 4
		}

		private const string DECOMPRESSION_INFO = "Decompressing obstacle data file..";

		private const string DESERIALIZING_INFO = "Deserializing obstacle data..";

		private const string UNPACKING_INFO = "Unpacking data..";

		private const string FINISHED_INFO = "Deserializing finished!";

		private const string FAILED_INFO = "Deserializing failed";

		private readonly string UNKNOWN_DESERIALIZING_STATUS_TYPE = "Unknown ObstacleDeserializingStatus value: {0}";

		private const string RESULT_STATS_HEADER = "Obstacle deserializing stats:";

		private const string RESULT_STATS_IN_PROGRESS_INFO = "Obstacle deserializing still in progress. Please stand by.";

		private readonly string RESULT_STATS_DECOMPRESSING_INFO = "Decompressing file takes: {0}";

		private readonly string RESULT_STATS_DESERIALIZING_INFO = "Deserializing takes: {0}";

		private readonly string RESULT_STATS_UNPACKING_INFO = "Unpacking data takes: {0}";

		private readonly string RESULT_STATS_TOTAL_PROCESSING_INFO = "Total processing takes: {0}";

		private ObstacleDeserializingStatus m_Status;

		private DateTime m_Start;

		private DateTime m_Finish;

		private TimeSpan m_DecompressingDuration;

		private TimeSpan m_DeserializingDuration;

		private TimeSpan m_UnpackingDataDuration;

		private TimeSpan m_TotalProcessingDuration;

		public static ObstacleDeserializingProgress FAILED => new ObstacleDeserializingProgress
		{
			m_Status = ObstacleDeserializingStatus.FAILED
		};

		public float Progress { get; private set; }

		public bool Finished => m_Status == ObstacleDeserializingStatus.FINISHED;

		public void Fail()
		{
			SetStatus(ObstacleDeserializingStatus.FAILED);
		}

		public void SetStatus(ObstacleDeserializingStatus _Status)
		{
			m_Status = _Status;
			switch (_Status)
			{
			case ObstacleDeserializingStatus.DECOMPRESSION:
				m_Start = DateTime.UtcNow;
				break;
			case ObstacleDeserializingStatus.DESERIALIZING:
				m_DecompressingDuration = DateTime.UtcNow - m_Start;
				break;
			case ObstacleDeserializingStatus.UNPACKING:
				m_DeserializingDuration = DateTime.UtcNow - m_Start - m_DecompressingDuration;
				break;
			case ObstacleDeserializingStatus.FINISHED:
				m_Finish = DateTime.UtcNow;
				m_UnpackingDataDuration = m_Finish - m_Start - m_DecompressingDuration - m_DeserializingDuration;
				m_TotalProcessingDuration = m_Finish - m_Start;
				break;
			}
		}

		public void SetProgress(float _Progrress)
		{
			Progress = _Progrress;
		}

		public string GetStatusInfo()
		{
			return m_Status switch
			{
				ObstacleDeserializingStatus.DECOMPRESSION => "Decompressing obstacle data file..", 
				ObstacleDeserializingStatus.DESERIALIZING => "Deserializing obstacle data..", 
				ObstacleDeserializingStatus.UNPACKING => "Unpacking data..", 
				ObstacleDeserializingStatus.FINISHED => "Deserializing finished!", 
				ObstacleDeserializingStatus.FAILED => "Deserializing failed", 
				_ => throw new Exception(string.Format(UNKNOWN_DESERIALIZING_STATUS_TYPE, m_Status)), 
			};
		}

		public string GetResultStats(bool _InMilliseconds = false)
		{
			if (m_Status == ObstacleDeserializingStatus.FAILED)
			{
				return "Deserializing failed";
			}
			if (m_Status != ObstacleDeserializingStatus.FINISHED)
			{
				return "Obstacle deserializing still in progress. Please stand by.";
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("Obstacle deserializing stats:");
			stringBuilder.AppendLine(string.Format(RESULT_STATS_DECOMPRESSING_INFO, _InMilliseconds ? m_DecompressingDuration.TotalMilliseconds.ToString() : m_DecompressingDuration.ToString()));
			stringBuilder.AppendLine(string.Format(RESULT_STATS_DESERIALIZING_INFO, _InMilliseconds ? m_DeserializingDuration.TotalMilliseconds.ToString() : m_DeserializingDuration.ToString()));
			stringBuilder.AppendLine(string.Format(RESULT_STATS_UNPACKING_INFO, _InMilliseconds ? m_UnpackingDataDuration.TotalMilliseconds.ToString() : m_UnpackingDataDuration.ToString()));
			stringBuilder.AppendLine(string.Format(RESULT_STATS_TOTAL_PROCESSING_INFO, _InMilliseconds ? m_TotalProcessingDuration.TotalMilliseconds.ToString() : m_TotalProcessingDuration.ToString()));
			return stringBuilder.ToString();
		}
	}
}
