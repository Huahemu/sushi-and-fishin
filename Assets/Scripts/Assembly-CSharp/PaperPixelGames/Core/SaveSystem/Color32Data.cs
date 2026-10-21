using System;
using UnityEngine;

namespace PaperPixelGames.Core.SaveSystem
{
	[Serializable]
	public struct Color32Data
	{
		public byte R;

		public byte G;

		public byte B;

		public byte A;

		public Color32Data(Color color)
		{
			Color32 color2 = color;
			R = color2.r;
			G = color2.g;
			B = color2.b;
			A = color2.a;
		}

		public Color ToColor()
		{
			return new Color32(R, G, B, A);
		}
	}
}
