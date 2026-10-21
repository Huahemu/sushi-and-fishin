using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Struct
{
	public struct FishPersonalBestEntry : INetworkSerializable, IEquatable<FishPersonalBestEntry>
	{
		public FixedString64Bytes FishId;

		public ulong SteamId;

		public float BestWeightKg;

		public bool Equals(FishPersonalBestEntry other)
		{
			if (FishId.Equals(other.FishId) && SteamId == other.SteamId)
			{
				return Mathf.Approximately(BestWeightKg, other.BestWeightKg);
			}
			return false;
		}

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref FishId, default(FastBufferWriter.ForFixedStrings));
			serializer.SerializeValue(ref SteamId, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref BestWeightKg, default(FastBufferWriter.ForPrimitives));
		}
	}
}
