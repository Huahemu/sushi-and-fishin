namespace PaperPixelGames.Core.Ocean.Creatures
{
	public enum CatchableCreatureState : byte
	{
		Free = 0,
		Hooked = 1,
		CatchPending = 2,
		Caught = 3,
		Released = 4
	}
}
