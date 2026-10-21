namespace PaperPixelGames.Core.Ocean.Creatures
{
	public interface ISeaCreatureRegistry
	{
		void RegisterCreature(SeaCreature creature);

		void UnregisterCreature(SeaCreature creature);
	}
}
