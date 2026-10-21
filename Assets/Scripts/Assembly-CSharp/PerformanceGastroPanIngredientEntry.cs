using System;

[Serializable]
public sealed class PerformanceGastroPanIngredientEntry
{
	public string ingredientId;

	public int panCount;

	public int emptyPanCount;

	public int ingredientUnitsTotal;

	public int capacityTotal;

	public int minimumIngredientCount;

	public int maximumIngredientCount;

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
}
