using UnityEngine;

namespace PaperPixelGames.Core.Reporting
{
	public class ImageCompressionService : IImageCompressionService
	{
		public byte[] CompressToJpg(Texture2D source, int maxLongestSide, int quality)
		{
			int width = source.width;
			int height = source.height;
			int num = Mathf.Max(width, height);
			Texture2D texture2D = source;
			if (num > maxLongestSide)
			{
				float num2 = (float)maxLongestSide / (float)num;
				int w = Mathf.RoundToInt((float)width * num2);
				int h = Mathf.RoundToInt((float)height * num2);
				texture2D = Resize(source, w, h);
			}
			try
			{
				return texture2D.EncodeToJPG(quality);
			}
			finally
			{
				if (texture2D != source)
				{
					Object.Destroy(texture2D);
				}
			}
		}

		private Texture2D Resize(Texture2D src, int w, int h)
		{
			RenderTexture temporary = RenderTexture.GetTemporary(w, h);
			RenderTexture active = RenderTexture.active;
			Texture2D texture2D = null;
			try
			{
				Graphics.Blit(src, temporary);
				RenderTexture.active = temporary;
				texture2D = new Texture2D(w, h, TextureFormat.RGB24, mipChain: false);
				texture2D.ReadPixels(new Rect(0f, 0f, w, h), 0, 0);
				texture2D.Apply();
				return texture2D;
			}
			catch
			{
				if (texture2D != null)
				{
					Object.Destroy(texture2D);
				}
				throw;
			}
			finally
			{
				RenderTexture.active = active;
				RenderTexture.ReleaseTemporary(temporary);
			}
		}
	}
}
