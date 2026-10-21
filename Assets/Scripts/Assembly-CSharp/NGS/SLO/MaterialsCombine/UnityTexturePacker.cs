using UnityEngine;

namespace NGS.SLO.MaterialsCombine
{
	public class UnityTexturePacker : ITexturePacker
	{
		public Texture2D PackTextures(Texture2D[] textures, int padding, int maxAtlasSize, out Rect[] rects)
		{
			Texture2D texture2D = new Texture2D(0, 0);
			rects = texture2D.PackTextures(textures, padding, maxAtlasSize);
			texture2D.Apply();
			return texture2D;
		}
	}
}
