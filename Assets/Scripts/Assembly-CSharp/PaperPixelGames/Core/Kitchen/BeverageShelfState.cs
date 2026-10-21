using System;
using Unity.Netcode;

namespace PaperPixelGames.Core.Kitchen
{
	public struct BeverageShelfState : INetworkSerializable, IEquatable<BeverageShelfState>
	{
		public byte Type;

		public byte Count;

		public BeverageShelfState(byte type, byte count)
		{
			Type = type;
			Count = count;
		}

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref Type);
			serializer.SerializeValue(ref Count);
		}

		public bool Equals(BeverageShelfState other)
		{
			if (Type == other.Type)
			{
				return Count == other.Count;
			}
			return false;
		}
	}
}
