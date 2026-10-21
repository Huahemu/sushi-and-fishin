using System;

namespace PaperPixelGames.Core.Data
{
	[Flags]
	public enum SharkAttackCapabilities : byte
	{
		None = 0,
		BiteDrag = 1,
		TailKnockback = 2,
		CageStrike = 4,
		All = BiteDrag | TailKnockback | CageStrike
	}
}
