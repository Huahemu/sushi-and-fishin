using System.Collections.Generic;
using UnityEngine;

namespace PaperPixelGames.Core.Ocean.Creatures
{
	public interface ICreaturePathProvider
	{
		bool TryGetPatrolPath(int pointCount, bool allowRepeats, out List<Vector3> path);

		bool TryGetRandomPoint(out Vector3 point);

		bool TryGetFarthestPointFrom(Vector3 referencePosition, out Vector3 point);

		bool TryGetPatrolLoop(out bool loop);

		bool TryGetPatrolStartFromClosest(out bool startFromClosest);

		bool TryGetPatrolReachDistance(out float reachDistance);
	}
}
