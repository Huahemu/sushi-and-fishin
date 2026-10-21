using System;

[Serializable]
public sealed class PerformanceRendererEntry
{
	public string name;

	public string path;

	public string scene;

	public string layer;

	public string rendererType;

	public long triangles;

	public int materialSlots;

	public bool isPartOfStaticBatch;

	public int meshSubMeshCount;

	public int renderedSubMeshStart;

	public int estimatedRenderedSubMeshCount;

	public float distance;

	public bool castsShadows;

	public bool selectedByLod;
}
