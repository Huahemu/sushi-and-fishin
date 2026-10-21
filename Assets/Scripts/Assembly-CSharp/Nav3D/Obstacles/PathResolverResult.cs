using System.Collections.Generic;
using Nav3D.API;

namespace Nav3D.Obstacles
{
	public struct PathResolverResult
	{
		public List<Leaf> Path { get; private set; }

		public PathfindingResultCode ResultCode { get; private set; }

		public PathResolverResult(List<Leaf> _Path, PathfindingResultCode _Result)
		{
			Path = _Path;
			ResultCode = _Result;
		}
	}
}
