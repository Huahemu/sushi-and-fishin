using System;
using UnityEngine;

namespace Nav3D.Common
{
	public interface IMovable
	{
		bool NeedToBeAvoided { get; }

		bool Avoiding { get; }

		bool IsNeighborMoversDirty { get; }

		bool IsNeighborObstaclesDirty { get; }

		event Action<IMovable, Vector3> OnPositionChanged;

		Vector3 GetPosition();

		Vector3 GetLastFrameVelocity();

		Vector3 GetLastNonZeroVelocity();

		float GetRadius();

		float GetMaxSpeed();

		float GetORCATau();

		float GetStaticObstaclesDangerDistance();

		void SetNeighborMovablesDirty(bool _Dirty);

		void SetNeighborObstaclesDirty(bool _Dirty);
	}
}
