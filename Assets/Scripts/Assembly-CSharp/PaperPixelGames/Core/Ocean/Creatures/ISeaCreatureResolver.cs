using PaperPixelGames.Core.Data;

namespace PaperPixelGames.Core.Ocean.Creatures
{
	public interface ISeaCreatureResolver
	{
		bool TryGetLayer(byte layerIndex, out SeaLayer layer);

		bool TryGetCreature(string creatureId, out CreatureSO creatureSO);

		bool IsLocalLayerActive(byte layerIndex);

		float GetLocalCreaturePresentationProgress(SeaCreature creature);
	}
}
