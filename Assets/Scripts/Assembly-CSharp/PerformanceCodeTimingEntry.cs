using System;

[Serializable]
public sealed class PerformanceCodeTimingEntry
{
	public string name;

	public long callCount;

	public int activeFrameCount;

	public float totalMs;

	public float averageCallMs;

	public float averageActiveFrameMs;

	public float maxCallMs;

	public float averageMsPerSecond;
}
