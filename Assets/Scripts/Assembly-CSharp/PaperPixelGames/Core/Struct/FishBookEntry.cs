using System;
using Unity.Collections;
using Unity.Netcode;

namespace PaperPixelGames.Core.Struct
{
	public struct FishBookEntry : INetworkSerializable, IEquatable<FishBookEntry>
	{
		public FixedString64Bytes FishId;

		public bool IsUnlocked;

		public int TotalCaughtAmount;

		public FishLeaderboardEntry Score1;

		public FishLeaderboardEntry Score2;

		public FishLeaderboardEntry Score3;

		public bool Equals(FishBookEntry other)
		{
			if (FishId.Equals(other.FishId) && IsUnlocked == other.IsUnlocked && TotalCaughtAmount == other.TotalCaughtAmount && Score1.Equals(other.Score1) && Score2.Equals(other.Score2))
			{
				return Score3.Equals(other.Score3);
			}
			return false;
		}

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref FishId, default(FastBufferWriter.ForFixedStrings));
			serializer.SerializeValue(ref IsUnlocked, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref TotalCaughtAmount, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref Score1, default(FastBufferWriter.ForNetworkSerializable));
			serializer.SerializeValue(ref Score2, default(FastBufferWriter.ForNetworkSerializable));
			serializer.SerializeValue(ref Score3, default(FastBufferWriter.ForNetworkSerializable));
		}
	}
}
