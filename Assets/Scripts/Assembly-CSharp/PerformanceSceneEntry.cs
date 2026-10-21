using System;

[Serializable]
public sealed class PerformanceSceneEntry
{
	public string name;

	public string path;

	public bool isActiveScene;

	public int rootObjectCount;

	public int rendererCount;

	public int lodGroupCount;

	public int cameraCount;

	public int lightCount;
}
