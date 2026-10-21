using UnityEngine;

namespace PaperPixelGames.Framework.Indicator
{
	public interface IOffscreenIndicatorTarget
	{
		Sprite Icon { get; }

		bool ShowDistance { get; }

		bool IsValid { get; }

		Vector3 GetWorldPosition();
	}
}
