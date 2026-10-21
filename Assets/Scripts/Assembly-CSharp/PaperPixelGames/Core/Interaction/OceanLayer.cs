using System;

namespace PaperPixelGames.Core.Interaction
{
	[Flags]
	public enum OceanLayer : ushort
	{
		None = 0,
		SunlightZone = 1,
		MidZone = 2,
		DeepZone = 4,
		AbyssalRift = 8,
		ForgottenDepth = 0x10
	}
}
