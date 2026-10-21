namespace PaperPixelGames.Core.Ocean.Creatures
{
	public enum HostileCreatureState : byte
	{
		Inactive = 0,
		Patrol = 1,
		Chase = 2,
		Attack = 3,
		Escape = 4,
		Dead = 5,
		LayerRetreat = 6
	}
}
