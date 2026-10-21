using UnityEngine;

namespace PaperPixelGames.Core.Ai.NPCs
{
	public static class ResidentSnapshotController
	{
		public static ResidentSnapshotPacked Build(ResidentController resident)
		{
			ResidentRuntimeState runtime = resident.Runtime;
			byte b = 0;
			if (runtime.IsWorking)
			{
				b |= 1;
			}
			if (runtime.IsAvailable)
			{
				b |= 2;
			}
			if (runtime.IsSwimming)
			{
				b |= 4;
			}
			if (runtime.HasNewEntry)
			{
				b |= 8;
			}
			if (runtime.HasArrived)
			{
				b |= 0x20;
			}
			if (runtime.HasValidTarget)
			{
				b |= 0x40;
			}
			return new ResidentSnapshotPacked
			{
				ResidentId = resident.ResidentId,
				Position = resident.transform.position,
				YawQ = ResidentYawQuantize.EncodeYaw(resident.transform.eulerAngles.y),
				CurrentState = (byte)runtime.CurrentActivity,
				PreviousState = (byte)runtime.PreviousActivity,
				Flags = b,
				Index0 = ((runtime.CurrentScheduleEntryIndex < 0) ? byte.MaxValue : ((byte)Mathf.Clamp(runtime.CurrentScheduleEntryIndex, 0, 254))),
				Index1 = (byte)runtime.RestaurantTargetType,
				TableId = (ushort)((runtime.RestaurantTableId >= 0) ? ((ushort)runtime.RestaurantTableId) : 0),
				SeatIndex = (byte)((runtime.RestaurantSeatIndex >= 0) ? ((byte)runtime.RestaurantSeatIndex) : 0),
				RestaurantState = (byte)runtime.RestaurantServiceState
			};
		}

		public static ResidentStateSnapshotPacked BuildState(ResidentController resident)
		{
			ResidentRuntimeState runtime = resident.Runtime;
			byte flags = BuildFlags(runtime);
			return new ResidentStateSnapshotPacked
			{
				ResidentId = resident.ResidentId,
				CurrentState = (byte)runtime.CurrentActivity,
				PreviousState = (byte)runtime.PreviousActivity,
				Flags = flags,
				CurrentScheduleEntryIndex = runtime.CurrentScheduleEntryIndex,
				WaypointIndex = (byte)runtime.CurrentWaypointIndex,
				CurrentPatrolEntryPointIndex = runtime.CurrentPatrolEntryPointIndex,
				TargetDestination = runtime.TargetDestination,
				TargetYawQ = ResidentYawQuantize.EncodeYaw(runtime.TargetRotation.y),
				CurrentPosition = resident.transform.position,
				CurrentYawQ = ResidentYawQuantize.EncodeYaw(resident.transform.eulerAngles.y),
				RestaurantTargetType = (byte)runtime.RestaurantTargetType,
				RestaurantTableId = runtime.RestaurantTableId,
				RestaurantSeatIndex = (byte)((runtime.RestaurantSeatIndex >= 0) ? ((byte)runtime.RestaurantSeatIndex) : 0),
				RestaurantSubState = (byte)runtime.RestaurantSubState,
				RestaurantServiceState = (byte)runtime.RestaurantServiceState
			};
		}

		public static ResidentMovementSnapshotPacked BuildMovement(ResidentController resident)
		{
			return new ResidentMovementSnapshotPacked
			{
				ResidentId = resident.ResidentId,
				Position = resident.transform.position,
				YawQ = ResidentYawQuantize.EncodeYaw(resident.transform.eulerAngles.y)
			};
		}

		private static byte BuildFlags(ResidentRuntimeState runtime)
		{
			byte b = 0;
			if (runtime.IsWorking)
			{
				b |= 1;
			}
			if (runtime.IsAvailable)
			{
				b |= 2;
			}
			if (runtime.IsSwimming)
			{
				b |= 4;
			}
			if (runtime.HasNewEntry)
			{
				b |= 8;
			}
			if (runtime.HasArrived)
			{
				b |= 0x20;
			}
			if (runtime.HasValidTarget)
			{
				b |= 0x40;
			}
			return b;
		}
	}
}
