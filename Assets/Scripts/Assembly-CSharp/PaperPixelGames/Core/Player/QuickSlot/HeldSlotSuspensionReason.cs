using System;

namespace PaperPixelGames.Core.Player.QuickSlot
{
	[Flags]
	public enum HeldSlotSuspensionReason : byte
	{
		None = 0,
		EmoteArea = 1,
		Attachment = 2,
		JellyfishTrap = 4,
		OctopusTrap = 8
	}
}
