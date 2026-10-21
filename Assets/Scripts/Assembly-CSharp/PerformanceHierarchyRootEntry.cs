using System;

[Serializable]
public sealed class PerformanceHierarchyRootEntry
{
	public string name;

	public string path;

	public string scene;

	public int gameObjectCount;

	public int activeGameObjectCount;

	public int inactiveGameObjectCount;

	public int componentCount;

	public int missingScriptCount;

	public int monoBehaviourCount;

	public int enabledMonoBehaviourCount;

	public int disabledMonoBehaviourCount;

	public int rendererCount;

	public int colliderCount;

	public int rigidbodyCount;

	public int animatorCount;

	public int canvasCount;

	public int particleSystemCount;

	public int cloneCount;
}
