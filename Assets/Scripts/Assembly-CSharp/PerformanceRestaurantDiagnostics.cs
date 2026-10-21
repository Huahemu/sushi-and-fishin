using System;
using System.Collections.Generic;

[Serializable]
public sealed class PerformanceRestaurantDiagnostics
{
	public int restaurantTableCount;

	public int activeRestaurantTableCount;

	public int serverRestaurantTableCount;

	public int activeTakeOrderTimerCount;

	public int activeDeliverOrderTimerCount;

	public List<PerformanceStateCountEntry> restaurantTableStates;

	public int conveyorTableCount;

	public int activeConveyorTableCount;

	public int serverConveyorTableCount;

	public int activeConveyorMealTimerCount;

	public int activePaymentScanCount;

	public List<PerformanceStateCountEntry> conveyorTableStates;

	public int conveyorBeltCount;

	public int activeConveyorBeltCount;

	public int runningConveyorBeltCount;

	public int configuredConveyorSlotCount;

	public int networkConveyorSlotCount;

	public int offscreenIndicatorHandlerCount;

	public int registeredOffscreenIndicatorCount;

	public int offscreenRegisteredEntryCount;

	public int offscreenUpdatedEntryCount;

	public int questIndicatorBridgeCount;

	public int trackedQuestIndicatorGroupCount;

	public int questHighlightCandidateCount;

	public int worldUiManagerCount;

	public int registeredWorldUiAnchorCount;

	public int worldUiRegisteredAnchorCount;

	public int worldUiUpdatedAnchorCount;
}
