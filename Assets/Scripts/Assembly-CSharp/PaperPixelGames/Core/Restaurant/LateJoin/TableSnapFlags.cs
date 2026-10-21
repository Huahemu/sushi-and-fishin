using System;

namespace PaperPixelGames.Core.Restaurant.LateJoin
{
	[Flags]
	public enum TableSnapFlags : byte
	{
		None = 0,
		NeedsClean = 2,
		IsUsable = 4,
		HasOrders = 8
	}
}
