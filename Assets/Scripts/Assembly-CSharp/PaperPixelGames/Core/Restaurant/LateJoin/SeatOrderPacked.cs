using System;
using Unity.Collections;
using Unity.Netcode;

namespace PaperPixelGames.Core.Restaurant.LateJoin
{
	[Serializable]
	public struct SeatOrderPacked : INetworkSerializable
	{
		public FixedString64Bytes MainId;

		public FixedString64Bytes SideId;

		public FixedString64Bytes BevId;

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref MainId, default(FastBufferWriter.ForFixedStrings));
			serializer.SerializeValue(ref SideId, default(FastBufferWriter.ForFixedStrings));
			serializer.SerializeValue(ref BevId, default(FastBufferWriter.ForFixedStrings));
		}
	}
}
