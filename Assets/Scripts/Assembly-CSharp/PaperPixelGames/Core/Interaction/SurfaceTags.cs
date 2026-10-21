using System;

namespace PaperPixelGames.Core.Interaction
{
	[Flags]
	public enum SurfaceTags
	{
		None = 0,
		Ground = 1,
		RestaurantFloor = 2,
		Counter = 4,
		Shelf = 8,
		PrepSurface = 0x10,
		ChefSurface = 0x20,
		MainDish = 0x40,
		SideDish = 0x80,
		Beverage = 0x100,
		WallMount = 0x200,
		CeilingMount = 0x400,
		Tray = 0x800,
		AnySurface = Ground | RestaurantFloor | Counter | Shelf | PrepSurface | ChefSurface | MainDish | SideDish | Beverage | Tray
	}
}
