using System;

[Serializable]
public sealed class PerformanceLodGroupEntry
{
	public string name;

	public string path;

	public string scene;

	public float distance;

	public float worldSize;

	public int lodCount;

	public int estimatedLod;

	public float relativeScreenHeight;

	public long selectedTriangles;

	public int selectedRendererCount;

	public int selectedMaterialSlots;

	public float lastTransitionHeight;
}
