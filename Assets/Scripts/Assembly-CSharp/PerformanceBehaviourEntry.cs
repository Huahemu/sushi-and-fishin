using System;

[Serializable]
public sealed class PerformanceBehaviourEntry
{
	public string name;

	public int count;

	public int enabledCount;

	public int disabledCount;

	public int activeInHierarchyCount;

	public int inactiveInHierarchyCount;

	public bool hasUpdate;

	public bool hasLateUpdate;

	public bool hasFixedUpdate;
}
