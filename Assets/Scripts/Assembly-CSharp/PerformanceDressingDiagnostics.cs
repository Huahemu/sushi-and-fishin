using System;
using System.Collections.Generic;

[Serializable]
public sealed class PerformanceDressingDiagnostics
{
	public bool managerFound;

	public bool managerIsLoading;

	public bool managerIsStartupLoading;

	public bool activationQueueRunning;

	public int loadedSceneCount;

	public int pendingActivationGroupCount;

	public bool rendererCullerEnabled;

	public bool rendererCullerBuilt;

	public bool rendererCullerBuilding;

	public int rendererCullerBuiltSceneCount;

	public int rendererCullingCellCount;

	public int rendererCullingManagedRendererCount;

	public int rendererCullingCulledCellCount;

	public int rendererCullingCulledRendererCount;

	public int rendererCullingPendingTransitionCount;

	public int rendererCullingBuildProcessedTransformCount;

	public int rendererCullingBuildDiscoveredRendererCount;

	public string rendererCullingBuildCurrentGroup;

	public int sceneRootCount;

	public int totalGroupCount;

	public int activeGroupCount;

	public int inactiveGroupCount;

	public int totalGameObjectCount;

	public int activeGameObjectCount;

	public int totalRendererCount;

	public int visibleRendererCount;

	public int totalLodGroupCount;

	public int totalColliderCount;

	public List<PerformanceDressingGroupEntry> groups;
}
