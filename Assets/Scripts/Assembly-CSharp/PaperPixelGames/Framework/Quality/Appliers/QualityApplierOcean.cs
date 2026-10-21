using System.Collections.Generic;
using StylizedWater3;
using StylizedWater3.UnderwaterRendering;
using UnityEngine;

namespace PaperPixelGames.Framework.Quality.Appliers
{
	public class QualityApplierOcean : IQualityApplier
	{
		public OceanFollowBehaviour Ocean;

		public List<Material> OceanMaterial;

		public void Apply(int qualityLevel)
		{
			if (Ocean == null)
			{
				Debug.LogWarning("[QualityApplierOcean] Cannot apply ocean quality because Ocean is not assigned.");
				return;
			}
			Material materialForQuality = GetMaterialForQuality(qualityLevel);
			if (!(materialForQuality == null))
			{
				Ocean.material = materialForQuality;
				Ocean.ApplyMaterial();
                // The repaired town sea uses equal-height tiles with the same
                // water material. Keep the existing quality selector effective.
                foreach (var grid in Ocean.transform.parent.GetComponentsInChildren<WaterGrid>(true))
                {
                    if (grid.name != "TownOceanContinuousGrid") continue;
                    grid.material = materialForQuality;
                    foreach (var water in grid.GetComponentsInChildren<WaterObject>(true)) water.AssignMaterial(materialForQuality);
                }
				SyncUnderwaterAreas(materialForQuality);
			}
		}

		private Material GetMaterialForQuality(int qualityLevel)
		{
			if (OceanMaterial == null || OceanMaterial.Count == 0)
			{
				Debug.LogWarning("[QualityApplierOcean] Cannot apply ocean quality because no ocean materials are assigned.");
				return null;
			}
			int num = Mathf.Clamp(qualityLevel, 0, OceanMaterial.Count - 1);
			if (num != qualityLevel)
			{
				Debug.LogWarning($"[QualityApplierOcean] Quality level {qualityLevel} is outside the ocean material list. Using index {num}.");
			}
			Material material = OceanMaterial[num];
			if (material == null)
			{
				Debug.LogWarning($"[QualityApplierOcean] Ocean material at index {num} is not assigned.");
			}
			return material;
		}

		private static void SyncUnderwaterAreas(Material oceanMaterial)
		{
			for (int i = 0; i < UnderwaterArea.Instances.Count; i++)
			{
				UnderwaterArea underwaterArea = UnderwaterArea.Instances[i];
				if (!(underwaterArea == null) && underwaterArea.waterLevelSource == UnderwaterArea.WaterLevelSource.Ocean)
				{
					underwaterArea.waterMaterial = oceanMaterial;
				}
			}
		}
	}
}
