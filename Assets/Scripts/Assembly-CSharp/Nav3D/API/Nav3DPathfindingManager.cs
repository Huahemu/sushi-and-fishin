using Nav3D.Pathfinding;

namespace Nav3D.API
{
	internal static class Nav3DPathfindingManager
	{
		public static int MaxPathfindingTasks
		{
			get
			{
				Nav3DManager.CheckInitedHard();
				return PathfindingManager.Instance.PathFindingTasksMaxCount;
			}
			set
			{
				Nav3DManager.CheckInitedHard();
				PathfindingManager.Instance.PathFindingTasksMaxCount = value;
			}
		}

		public static int CurrentPathfindingTasksCount
		{
			get
			{
				if (!Nav3DManager.Inited)
				{
					throw new Nav3DManager.Nav3DManagerNotInitializedException();
				}
				return PathfindingManager.Instance.PathFindingTasksOperatingCount;
			}
		}
	}
}
