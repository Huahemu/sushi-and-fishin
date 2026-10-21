using System;

[Serializable]
public sealed class PerformanceDressingGroupEntry
{
	public string name;

	public string path;

	public string scene;

	public bool activeSelf;

	public bool activeInHierarchy;

	public bool activateOnPrepare;

	public int activationWeight;

	public int gameObjectCount;

	public int selfEnabledGameObjectCount;

	public int activeGameObjectCount;

	public int rendererCount;

	public int enabledRendererCount;

	public int visibleRendererCount;

	public int lodGroupCount;

	public int colliderCount;

	public float boundsCenterX;

	public float boundsCenterY;

	public float boundsCenterZ;

	public float boundsSizeX;

	public float boundsSizeY;

	public float boundsSizeZ;

	public float distanceToCameraBounds;
}
