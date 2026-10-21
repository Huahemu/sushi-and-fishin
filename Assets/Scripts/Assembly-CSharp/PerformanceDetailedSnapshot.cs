using System;
using System.Collections.Generic;

[Serializable]
public sealed class PerformanceDetailedSnapshot
{
	public string cameraName;

	public PerformancePlayerMovementSnapshot playerMovement;

	public int enabledCameraCount;

	public int cameraRenderCount;

	public string sunlightDiagnosticMode;

	public int sunlightDiagnosticTargetSceneCount;

	public int sunlightDiagnosticTargetRootCount;

	public int sunlightDiagnosticTargetRendererCount;

	public int sunlightDiagnosticTargetLodGroupCount;

	public int loadedSceneCount;

	public float lodBias;

	public float shadowDistance;

	public float cameraFarClip;

	public int enabledRendererCount;

	public int frustumRendererCount;

	public long frustumTriangles;

	public int frustumMaterialSlots;

	public int shadowCasterEnabledCount;

	public int potentialShadowDistanceCasterCount;

	public int totalLodGroupCount;

	public int frustumLodGroupCount;

	public int estimatedCulledLodGroupCount;

	public int estimatedLod0GroupCount;

	public int singleLevelLodGroupCount;

	public int lateCullLodGroupCount;

	public int estimatedActiveLodRenderers;

	public long estimatedActiveLodTriangles;

	public int estimatedActiveLodMaterialSlots;

	public int nonLodFrustumRendererCount;

	public long nonLodFrustumTriangles;

	public int nonLodFrustumMaterialSlots;

	public int staticBatchedFrustumRendererCount;

	public int loadedGameObjectCount;

	public int activeGameObjectCount;

	public int inactiveGameObjectCount;

	public int componentSlotCount;

	public int missingScriptCount;

	public int monoBehaviourCount;

	public int enabledMonoBehaviourCount;

	public int disabledMonoBehaviourCount;

	public int updateCandidateInstanceCount;

	public int colliderCount;

	public int triggerColliderCount;

	public int rigidbodyCount;

	public int awakeRigidbodyCount;

	public int animatorCount;

	public int canvasCount;

	public int worldSpaceCanvasCount;

	public int particleSystemCount;

	public int playingParticleSystemCount;

	public int enabledLightCount;

	public bool hasStaticCullingCamera;

	public bool hasDynamicCullingCamera;

	public List<string> cameraCullingComponents = new List<string>();

	public List<PerformanceLayerCullDistance> cameraLayerCullDistances;

	public List<PerformanceRankedEntry> topLayers;

	public List<PerformanceRankedEntry> topShaders;

	public List<PerformanceRankedEntry> topComponents;

	public List<PerformanceBehaviourEntry> topMonoBehaviours;

	public List<PerformanceBehaviourEntry> topUpdateCandidates;

	public List<PerformanceRankedEntry> topCloneGroups;

	public List<PerformanceRendererEntry> topRenderers;

	public List<PerformanceLodGroupEntry> topLodGroups;

	public List<PerformanceHierarchyRootEntry> topHierarchyRoots;

	public float midDistanceMin;

	public float midDistanceMax;

	public List<PerformanceRenderHotspotEntry> midDistanceRootHotspots;

	public List<PerformanceRenderHotspotEntry> midDistanceShaderHotspots;

	public List<PerformanceDistanceBand> distanceBands;

	public List<PerformanceSceneEntry> scenes;

	public List<PerformanceCameraEntry> cameras;

	public PerformanceResidentMergeDiagnostics residentMergeDiagnostics;

	public PerformanceGastroPanDiagnostics gastroPanDiagnostics;

	public PerformanceRestaurantDiagnostics restaurantDiagnostics;

	public List<PerformanceCodeTimingEntry> codeTimings;

	public PerformanceDressingDiagnostics dressingDiagnostics;

	public PerformanceUnderwaterChunkDiagnostics underwaterChunkDiagnostics;
}
