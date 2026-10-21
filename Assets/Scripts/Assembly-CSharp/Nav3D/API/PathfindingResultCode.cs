namespace Nav3D.API
{
	public enum PathfindingResultCode
	{
		SUCCEEDED = 0,
		PATH_DOES_NOT_EXIST = 1,
		TIMEOUT = 2,
		CANCELLED = 3,
		START_POINT_INSIDE_OBSTACLE = 4,
		TARGET_POINT_INSIDE_OBSTACLE = 5,
		UNKNOWN = 6
	}
}
