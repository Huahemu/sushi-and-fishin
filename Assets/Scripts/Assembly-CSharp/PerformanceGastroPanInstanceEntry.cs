using System;

[Serializable]
public sealed class PerformanceGastroPanInstanceEntry
{
	public string name;

	public string path;

	public string scene;

	public string ingredientId;

	public int ingredientCount;

	public int maxCapacity;

	public bool activeInHierarchy;

	public bool networkSpawned;

	public bool hasWorldVisual;

	public bool hasFpsVisual;

	public bool hasTpsVisual;

	public int visualCopyCount;

	public int totalVisualGameObjectCount;

	public int totalIngredientSlotCount;

	public int selectedIngredientObjectCount;

	public int visibleIngredientObjectCount;

	public int totalRendererCount;

	public int visibleRendererCount;
}
