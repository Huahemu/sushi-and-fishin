using System;

[Serializable]
public sealed class PerformanceRenderHotspotEntry
{
	public string name;

	public string path;

	public string scene;

	public int rendererCount;

	public int lodSelectedRendererCount;

	public int nonLodRendererCount;

	public int materialSlots;

	public long triangles;

	public int shadowCasterCount;

	public int staticBatchedRendererCount;

	public float minDistance;

	public float maxDistance;
}
