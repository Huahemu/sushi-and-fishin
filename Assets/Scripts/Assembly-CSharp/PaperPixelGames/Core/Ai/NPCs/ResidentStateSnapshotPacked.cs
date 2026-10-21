using System;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Ai.NPCs
{
	[Serializable]
	public struct ResidentStateSnapshotPacked : INetworkSerializable, IEquatable<ResidentStateSnapshotPacked>
	{
		public byte ResidentId;

		public byte CurrentState;

		public byte PreviousState;

		public byte Flags;

		public int CurrentScheduleEntryIndex;

		public byte WaypointIndex;

		public int CurrentPatrolEntryPointIndex;

		public Vector3 TargetDestination;

		public ushort TargetYawQ;

		public Vector3 CurrentPosition;

		public ushort CurrentYawQ;

		public byte RestaurantTargetType;

		public int RestaurantTableId;

		public byte RestaurantSeatIndex;

		public byte RestaurantSubState;

		public byte RestaurantServiceState;

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref ResidentId);
			serializer.SerializeValue(ref CurrentState);
			serializer.SerializeValue(ref PreviousState);
			serializer.SerializeValue(ref Flags);
			serializer.SerializeValue(ref CurrentScheduleEntryIndex, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref WaypointIndex);
			serializer.SerializeValue(ref CurrentPatrolEntryPointIndex, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref TargetDestination);
			serializer.SerializeValue(ref TargetYawQ, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref CurrentPosition);
			serializer.SerializeValue(ref CurrentYawQ, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref RestaurantTargetType);
			serializer.SerializeValue(ref RestaurantTableId, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref RestaurantSeatIndex);
			serializer.SerializeValue(ref RestaurantSubState);
			serializer.SerializeValue(ref RestaurantServiceState);
		}

		public bool Equals(ResidentStateSnapshotPacked other)
		{
			if (ResidentId == other.ResidentId && CurrentState == other.CurrentState && PreviousState == other.PreviousState && Flags == other.Flags && CurrentScheduleEntryIndex == other.CurrentScheduleEntryIndex && WaypointIndex == other.WaypointIndex && CurrentPatrolEntryPointIndex == other.CurrentPatrolEntryPointIndex && TargetDestination == other.TargetDestination && TargetYawQ == other.TargetYawQ && CurrentPosition == other.CurrentPosition && CurrentYawQ == other.CurrentYawQ && RestaurantTargetType == other.RestaurantTargetType && RestaurantTableId == other.RestaurantTableId && RestaurantSeatIndex == other.RestaurantSeatIndex && RestaurantSubState == other.RestaurantSubState)
			{
				return RestaurantServiceState == other.RestaurantServiceState;
			}
			return false;
		}

		public bool EqualsSemantic(ResidentStateSnapshotPacked other)
		{
			if (ResidentId == other.ResidentId && CurrentState == other.CurrentState && PreviousState == other.PreviousState && Flags == other.Flags && CurrentScheduleEntryIndex == other.CurrentScheduleEntryIndex && WaypointIndex == other.WaypointIndex && CurrentPatrolEntryPointIndex == other.CurrentPatrolEntryPointIndex && TargetDestination == other.TargetDestination && TargetYawQ == other.TargetYawQ && RestaurantTargetType == other.RestaurantTargetType && RestaurantTableId == other.RestaurantTableId && RestaurantSeatIndex == other.RestaurantSeatIndex && RestaurantSubState == other.RestaurantSubState)
			{
				return RestaurantServiceState == other.RestaurantServiceState;
			}
			return false;
		}

		public override bool Equals(object obj)
		{
			if (obj is ResidentStateSnapshotPacked other)
			{
				return Equals(other);
			}
			return false;
		}

		public override int GetHashCode()
		{
			return (((((((((((((((((((((((((((((ResidentId.GetHashCode() * 397) ^ CurrentState.GetHashCode()) * 397) ^ PreviousState.GetHashCode()) * 397) ^ Flags.GetHashCode()) * 397) ^ CurrentScheduleEntryIndex) * 397) ^ WaypointIndex.GetHashCode()) * 397) ^ CurrentPatrolEntryPointIndex) * 397) ^ TargetDestination.GetHashCode()) * 397) ^ TargetYawQ.GetHashCode()) * 397) ^ CurrentPosition.GetHashCode()) * 397) ^ CurrentYawQ.GetHashCode()) * 397) ^ RestaurantTargetType.GetHashCode()) * 397) ^ RestaurantTableId) * 397) ^ RestaurantSeatIndex.GetHashCode()) * 397) ^ RestaurantSubState.GetHashCode()) * 397) ^ RestaurantServiceState.GetHashCode();
		}
	}
}
