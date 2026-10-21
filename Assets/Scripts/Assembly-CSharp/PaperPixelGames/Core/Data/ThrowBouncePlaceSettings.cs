using System;

namespace PaperPixelGames.Core.Data
{
	[Serializable]
	public struct ThrowBouncePlaceSettings
	{
		public bool EnableBouncePlace;

		public float BounceHeight;

		public float BounceUpDuration;

		public float SettleDuration;

		public static ThrowBouncePlaceSettings Default => new ThrowBouncePlaceSettings
		{
			EnableBouncePlace = true,
			BounceHeight = 0.12f,
			BounceUpDuration = 0.08f,
			SettleDuration = 0.12f
		};

		public static ThrowBouncePlaceSettings Disabled => new ThrowBouncePlaceSettings
		{
			EnableBouncePlace = false,
			BounceHeight = 0.12f,
			BounceUpDuration = 0.08f,
			SettleDuration = 0.12f
		};
	}
}
