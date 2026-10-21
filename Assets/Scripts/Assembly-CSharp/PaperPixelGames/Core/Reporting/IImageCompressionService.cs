using UnityEngine;

namespace PaperPixelGames.Core.Reporting
{
	public interface IImageCompressionService
	{
		byte[] CompressToJpg(Texture2D source, int maxLongestSide, int jpgQuality);
	}
}
