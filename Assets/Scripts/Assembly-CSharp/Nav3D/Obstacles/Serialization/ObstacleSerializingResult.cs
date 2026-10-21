using System;

namespace Nav3D.Obstacles.Serialization
{
	public struct ObstacleSerializingResult
	{
		public DateTime Start { get; private set; }

		public DateTime Finish { get; private set; }

		public TimeSpan PreparingInfosDuration { get; private set; }

		public TimeSpan BakingObstaclesDuration { get; private set; }

		public TimeSpan PackingBakedDataDuration { get; private set; }

		public TimeSpan SerializingDuration { get; private set; }

		public TimeSpan CompressingDuration { get; private set; }

		public TimeSpan TotalDuration { get; private set; }

		public ObstacleSerializingResult(DateTime _Start, DateTime _Finish, TimeSpan _PreparingInfosDuration, TimeSpan _BakingObstaclesDuration, TimeSpan _PackingBakedDataDuration, TimeSpan _SerializingDuration, TimeSpan _CompressingDuration)
		{
			Start = _Start;
			Finish = _Finish;
			PreparingInfosDuration = _PreparingInfosDuration;
			BakingObstaclesDuration = _BakingObstaclesDuration;
			PackingBakedDataDuration = _PackingBakedDataDuration;
			SerializingDuration = _SerializingDuration;
			CompressingDuration = _CompressingDuration;
			TotalDuration = Finish - Start;
		}
	}
}
