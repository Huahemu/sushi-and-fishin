using System;
using Unity.Collections;
using Unity.Netcode;

namespace PaperPixelGames.Core.Struct
{
	[Serializable]
	public struct DailySushiSpecialEntry : INetworkSerializable, IEquatable<DailySushiSpecialEntry>
	{
		public FixedString64Bytes SushiId;

		public byte BonusPercentage;

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref SushiId, default(FastBufferWriter.ForFixedStrings));
			serializer.SerializeValue(ref BonusPercentage);
		}

		public bool Equals(DailySushiSpecialEntry other)
		{
			if (SushiId.Equals(other.SushiId))
			{
				return BonusPercentage == other.BonusPercentage;
			}
			return false;
		}

		public override bool Equals(object obj)
		{
			if (obj is DailySushiSpecialEntry other)
			{
				return Equals(other);
			}
			return false;
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(SushiId, BonusPercentage);
		}
	}
}
