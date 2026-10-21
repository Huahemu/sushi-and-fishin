using UnityEngine;

namespace PaperPixelGames.Core.Interaction
{
	public interface IQuickSnapPlacementZone
	{
		SurfaceTags Tags { get; }

		bool HasTag(SurfaceTags tags);

		Vector3 GetPlacementPoint(Vector3 hitPoint);

		Quaternion GetPlacementRotation(Quaternion defaultRotation);
	}
}
