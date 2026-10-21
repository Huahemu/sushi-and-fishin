using System;

namespace PaperPixelGames.Core.Interaction
{
	[Flags]
	public enum BaitType : ushort
	{
		None = 0,
		Worm = 1,
		Bread = 2,
		Corn = 4,
		Insect = 8,
		Artificial = 0x10,
		Shrimp = 0x20,
		FishBits = 0x40,
		GoldenLure = 0x80
	}
}
