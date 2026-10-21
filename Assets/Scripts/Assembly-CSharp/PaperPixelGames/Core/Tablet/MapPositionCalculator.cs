using UnityEngine;

namespace PaperPixelGames.Core.Tablet
{
	public sealed class MapPositionCalculator : MonoBehaviour
	{
		[SerializeField]
		private MapDefinitionSO _surfaceMap;

		[SerializeField]
		private MapDefinitionSO _underwaterMap;

		private void ValidateDefinitions()
		{
			ValidateDefinition(_surfaceMap);
			ValidateDefinition(_underwaterMap);
		}

		private static void ValidateDefinition(MapDefinitionSO definition)
		{
			if (!(definition == null))
			{
				MapProjection mapProjection = new MapProjection();
				if (!mapProjection.Fit(definition))
				{
					Debug.LogError("[MapCalibration] Invalid definition: " + definition.name, definition);
				}
				else
				{
					Debug.Log($"[MapCalibration] Definition={definition.name} RMS={mapProjection.RootMeanSquareErrorPixels:F1}px Max={mapProjection.MaximumErrorPixels:F1}px", definition);
				}
			}
		}
	}
}
