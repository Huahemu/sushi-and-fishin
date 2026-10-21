namespace Nav3D.API
{
	public interface IObstacleAdditionProgress
	{
		float Progress { get; }

		bool Finished { get; }

		string GetStatusInfo();

		string GetResultStats(bool _InMilliseconds = false);
	}
}
