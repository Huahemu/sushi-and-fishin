using System;

[Serializable]
public sealed class PerformanceDistanceBand
{
	public string label;

	public float minDistance;

	public float maxDistance;

	public int rendererCount;

	public long triangles;

	public int materialSlots;
}
