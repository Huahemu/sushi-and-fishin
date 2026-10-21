using System;
using Unity.Netcode;

namespace PaperPixelGames.Core.Player.QuickSlot
{
	public struct QuickSlotLoadPayload : INetworkSerializable
	{
		public int Count;

		public QuickSlotItemNet[] Items;

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref Count, default(FastBufferWriter.ForPrimitives));
			if (serializer.IsReader)
			{
				Items = ((Count > 0) ? new QuickSlotItemNet[Count] : Array.Empty<QuickSlotItemNet>());
			}
			for (int i = 0; i < Count; i++)
			{
				serializer.SerializeValue(ref Items[i], default(FastBufferWriter.ForNetworkSerializable));
			}
		}
	}
}
