using System;
using System.Collections.Generic;

[Serializable]
public sealed class PerformanceResidentMergeDiagnostics
{
	public bool managerFound;

	public bool queueEnabled;

	public float effectiveInitialDelay;

	public float effectiveInterval;

	public int queueCount;

	public int queuedResidentCount;

	public int requestCount;

	public int duplicateRequestCount;

	public int startedCount;

	public int deferredCount;

	public int discardedCount;

	public float nextAttemptInSeconds;

	public string lastResident;

	public string lastStatus;

	public int residentCount;

	public int restaurantQueueCount;

	public int restaurantQueuedResidentCount;

	public int residentControllerCount;

	public int residentBrainCount;

	public int skinnedMeshRendererCount;

	public int bakedResidentVisualCount;

	public int runtimeCustomizedResidentCount;

	public int activeResidentAnimatorCount;

	public int visibleResidentSkinnedRendererCount;

	public int bozoOutfitCount;

	public int bozoBodyShapeModifierCount;

	public int bozoClothSupportCount;

	public int magicaClothCount;

	public int activeResidentMagicaClothCount;

	public int magicaCapsuleColliderCount;

	public int activeResidentMagicaColliderCount;

	public List<PerformanceResidentMergeReasonEntry> deferredReasons;

	public List<PerformanceResidentMergeReasonEntry> discardedReasons;
}
