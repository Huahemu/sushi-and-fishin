using System;
using System.Linq;

namespace Nav3D.API
{
	public struct ObstacleAdditionResult
	{
		public DateTime Start { get; private set; }

		public DateTime Finish { get; private set; }

		public TimeSpan TriangleStorageBuildDuration { get; private set; }

		public TimeSpan OctreeBulidPreparationsDuration { get; private set; }

		public TimeSpan OctreeBulidDuration { get; private set; }

		public TimeSpan GraphBuildDuration { get; private set; }

		public TimeSpan TotalProcessingDuration { get; private set; }

		public ObstacleAdditionResult(DateTime _Start, DateTime _Finish, TimeSpan _TriangleStorageBuildingDuration, TimeSpan _OctreeBulidPreparationsDuration, TimeSpan _OctreeBulidDuration, TimeSpan _GraphBuildDuration)
		{
			Start = _Start;
			Finish = _Finish;
			TriangleStorageBuildDuration = _TriangleStorageBuildingDuration;
			OctreeBulidPreparationsDuration = _OctreeBulidPreparationsDuration;
			OctreeBulidDuration = _OctreeBulidDuration;
			GraphBuildDuration = _GraphBuildDuration;
			TotalProcessingDuration = _Finish - _Start;
		}

		public static ObstacleAdditionResult GetCombinedResult(ObstacleAdditionResult[] _Results)
		{
			DateTime start = _Results.Min((ObstacleAdditionResult _Result) => _Result.Start);
			DateTime finish = _Results.Max((ObstacleAdditionResult _Result) => _Result.Finish);
			return new ObstacleAdditionResult(start, finish, TimeSpan.FromMilliseconds(_Results.Sum((ObstacleAdditionResult _Result) => _Result.TriangleStorageBuildDuration.TotalMilliseconds)), TimeSpan.FromMilliseconds(_Results.Sum((ObstacleAdditionResult _Result) => _Result.OctreeBulidPreparationsDuration.TotalMilliseconds)), TimeSpan.FromMilliseconds(_Results.Sum((ObstacleAdditionResult _Result) => _Result.OctreeBulidDuration.TotalMilliseconds)), TimeSpan.FromMilliseconds(_Results.Sum((ObstacleAdditionResult _Result) => _Result.GraphBuildDuration.TotalMilliseconds)));
		}
	}
}
