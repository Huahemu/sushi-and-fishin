using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Struct
{
	[Serializable]
	public struct FishLeaderboardEntry : INetworkSerializable, IEquatable<FishLeaderboardEntry>
	{
		public FixedString64Bytes PlayerName;

		public float BestWeightKg;

		public ulong SteamId;

		public bool Equals(FishLeaderboardEntry other)
		{
			if (PlayerName.Equals(other.PlayerName) && Mathf.Approximately(BestWeightKg, other.BestWeightKg))
			{
				return SteamId == other.SteamId;
			}
			return false;
		}

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref PlayerName, default(FastBufferWriter.ForFixedStrings));
			serializer.SerializeValue(ref BestWeightKg, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref SteamId, default(FastBufferWriter.ForPrimitives));
		}

		public void Clear()
		{
			PlayerName = default;
			BestWeightKg = 0f;
			SteamId = 0uL;
		}
	}
}
