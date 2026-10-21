using UnityEngine;

namespace NGS.SLO.MaterialsCombine
{
	public interface ITexturePlacer
	{
		int MaxWidth { get; }

		int MaxHeight { get; }

		bool CanInsert(int width, int height);

		bool TryInsert(int width, int height, out Rect resultRect);
	}
}
