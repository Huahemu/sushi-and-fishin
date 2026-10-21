using UnityEngine;

namespace NGS.SLO.MaterialsCombine
{
	public interface ITexturePacker
	{
		Texture2D PackTextures(Texture2D[] textures, int padding, int maxAtlasSize, out Rect[] rects);
	}
}
