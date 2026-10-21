using System;
using System.Collections.Generic;

[Serializable]
public sealed class PerformanceGastroPanDiagnostics
{
	public int totalPanCount;

	public int activePanCount;

	public int networkSpawnedPanCount;

	public int emptyPanCount;

	public int ingredientUnitsTotal;

	public int capacityTotal;

	public int visualCopyCount;

	public int worldVisualCopyCount;

	public int fpsVisualCopyCount;

	public int tpsVisualCopyCount;

	public int totalVisualGameObjectCount;

	public int totalIngredientSlotCount;

	public int selectedIngredientObjectCount;

	public int visibleIngredientObjectCount;

	public int totalRendererCount;

	public int visibleRendererCount;

	public List<PerformanceGastroPanIngredientEntry> ingredientGroups;

	public List<PerformanceGastroPanInstanceEntry> topInstances;
}
