using System;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Ai.NPCs
{
	[Serializable]
	public struct ResidentSnapshotPacked : INetworkSerializable, IEquatable<ResidentSnapshotPacked>
	{
		public byte ResidentId;

		public Vector3 Position;

		public ushort YawQ;

		public byte CurrentState;

		public byte PreviousState;

		public byte Flags;

		public byte Index0;

		public byte Index1;

		public ushort TableId;

		public byte SeatIndex;

		public byte RestaurantState;

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref ResidentId);
			serializer.SerializeValue(ref Position);
			serializer.SerializeValue(ref YawQ, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref CurrentState);
			serializer.SerializeValue(ref PreviousState);
			serializer.SerializeValue(ref Flags);
			serializer.SerializeValue(ref Index0);
			serializer.SerializeValue(ref Index1);
			serializer.SerializeValue(ref TableId, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref SeatIndex);
			serializer.SerializeValue(ref RestaurantState);
		}

		public bool Equals(ResidentSnapshotPacked other)
		{
			if (ResidentId == other.ResidentId && Position == other.Position && YawQ == other.YawQ && CurrentState == other.CurrentState && PreviousState == other.PreviousState && Flags == other.Flags && Index0 == other.Index0 && Index1 == other.Index1 && TableId == other.TableId && SeatIndex == other.SeatIndex)
			{
				return RestaurantState == other.RestaurantState;
			}
			return false;
		}

		public override bool Equals(object obj)
		{
			if (obj is ResidentSnapshotPacked other)
			{
				return Equals(other);
			}
			return false;
		}

		public override int GetHashCode()
		{
			return (((((((((((((((((((ResidentId.GetHashCode() * 397) ^ Position.GetHashCode()) * 397) ^ YawQ.GetHashCode()) * 397) ^ CurrentState.GetHashCode()) * 397) ^ PreviousState.GetHashCode()) * 397) ^ Flags.GetHashCode()) * 397) ^ Index0.GetHashCode()) * 397) ^ Index1.GetHashCode()) * 397) ^ TableId.GetHashCode()) * 397) ^ SeatIndex.GetHashCode()) * 397) ^ RestaurantState.GetHashCode();
		}
	}
}
