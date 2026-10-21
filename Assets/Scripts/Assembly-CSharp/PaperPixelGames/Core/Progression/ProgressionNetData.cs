using System;
using Unity.Netcode;

namespace PaperPixelGames.Core.Progression
{
	[Serializable]
	public struct ProgressionNetData : INetworkSerializable, IEquatable<ProgressionNetData>
	{
		public ushort SyncRevision;

		public int RestaurantLevel;

		public double CurrentXp;

		public double TotalXp;

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref SyncRevision, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref RestaurantLevel, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref CurrentXp, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref TotalXp, default(FastBufferWriter.ForPrimitives));
		}

		public bool Equals(ProgressionNetData other)
		{
			if (SyncRevision == other.SyncRevision && RestaurantLevel == other.RestaurantLevel && CurrentXp == other.CurrentXp)
			{
				return TotalXp == other.TotalXp;
			}
			return false;
		}

		public override bool Equals(object obj)
		{
			if (obj is ProgressionNetData other)
			{
				return Equals(other);
			}
			return false;
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(SyncRevision, RestaurantLevel, CurrentXp, TotalXp);
		}

		public static bool operator ==(ProgressionNetData left, ProgressionNetData right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(ProgressionNetData left, ProgressionNetData right)
		{
			return !left.Equals(right);
		}
	}
}
