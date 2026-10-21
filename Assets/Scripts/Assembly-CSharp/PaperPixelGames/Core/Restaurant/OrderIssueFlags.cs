using System;

namespace PaperPixelGames.Core.Restaurant
{
	[Flags]
	public enum OrderIssueFlags : ushort
	{
		None = 0,
		SideMissing = 1,
		SideWrong = 2,
		SideExtra = 4,
		BeverageMissing = 8,
		BeverageWrong = 0x10,
		BeverageExtra = 0x20
	}
}
