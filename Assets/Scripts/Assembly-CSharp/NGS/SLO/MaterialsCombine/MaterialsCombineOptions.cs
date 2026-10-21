using System;

namespace NGS.SLO.MaterialsCombine
{
	[Serializable]
	public struct MaterialsCombineOptions
	{
		public int maxAtlasSize;

		public int maxTileTextureUpscaledSize;

		public int maxTileTextureDownscale;

		public int padding;

		public bool fillEmptyTextures;

		public static MaterialsCombineOptions Default => new MaterialsCombineOptions
		{
			maxAtlasSize = 2048,
			maxTileTextureUpscaledSize = 1024,
			maxTileTextureDownscale = 4,
			padding = 4,
			fillEmptyTextures = true
		};
	}
}
