using PaperPixelGames.Framework.Enums;
using UnityEngine;

namespace PaperPixelGames.Core.Ai.NPCs
{
	public sealed class ResidentRuntimeState
	{
		public byte ResidentId;

		public ResidentActivityType CurrentActivity;

		public ResidentActivityType PreviousActivity;

		public ResidentSystemState SystemState;

		public int CurrentScheduleEntryIndex = -1;

		public int PendingScheduleEntryIndex = -1;

		public Vector3 TargetDestination;

		public Vector3 TargetRotation;

		public WayPointIndex CurrentWaypointIndex;

		public int CurrentPatrolEntryPointIndex = -1;

		public bool IsAvailable;

		public bool IsWorking;

		public bool IsSwimming;

		public bool IsInRestaurant;

		public bool IsInteracting;

		public bool IsSnapshotApplying;

		public bool HasValidTarget;

		public bool HasArrived;

		public bool HasNewEntry;

		public int RestaurantTableId = -1;

		public int RestaurantSeatIndex = -1;

		public ResidentRestaurantTargetType RestaurantTargetType;

		public ResidentRestaurantSubState RestaurantSubState;

		public RestaurantActivityType RestaurantServiceState;

		public ulong ActiveInteractorClientId = ulong.MaxValue;

		public float LastStateChangeTime;

		public bool HasRestaurantAssignment
		{
			get
			{
				if (RestaurantTargetType == ResidentRestaurantTargetType.None)
				{
					return RestaurantTableId >= 0;
				}
				return true;
			}
		}

		public void SetActivity(ResidentActivityType activity)
		{
			if (CurrentActivity != activity)
			{
				PreviousActivity = CurrentActivity;
				CurrentActivity = activity;
				LastStateChangeTime = Time.time;
			}
		}

		public void ClearRestaurantAssignment()
		{
			RestaurantTableId = -1;
			RestaurantSeatIndex = -1;
			RestaurantTargetType = ResidentRestaurantTargetType.None;
			RestaurantSubState = ResidentRestaurantSubState.None;
			RestaurantServiceState = RestaurantActivityType.Idle;
			IsInRestaurant = false;
		}
	}
}
