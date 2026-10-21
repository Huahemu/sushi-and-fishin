using System;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Ai.NPCs
{
	[Serializable]
	public struct ResidentMovementSnapshotPacked : INetworkSerializable, IEquatable<ResidentMovementSnapshotPacked>
	{
		public byte ResidentId;

		public Vector3 Position;

		public ushort YawQ;

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref ResidentId);
			serializer.SerializeValue(ref Position);
			serializer.SerializeValue(ref YawQ, default(FastBufferWriter.ForPrimitives));
		}

		public bool Equals(ResidentMovementSnapshotPacked other)
		{
			if (ResidentId == other.ResidentId && Position == other.Position)
			{
				return YawQ == other.YawQ;
			}
			return false;
		}

		public override bool Equals(object obj)
		{
			if (obj is ResidentMovementSnapshotPacked other)
			{
				return Equals(other);
			}
			return false;
		}

		public override int GetHashCode()
		{
			return (((ResidentId.GetHashCode() * 397) ^ Position.GetHashCode()) * 397) ^ YawQ.GetHashCode();
		}
	}
}
