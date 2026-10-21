using PaperPixelGames.Core.Data;
using Unity.Collections;
using Unity.Netcode;

namespace PaperPixelGames.Core.Player.QuickSlot
{
	public struct QuickSlotItemNet : INetworkSerializable
	{
		public FixedString64Bytes ItemId;

		public FixedString128Bytes CustomData;

		public bool HasCustomSushi;

		public CustomSushiPhysicalNetworkData CustomSushi;

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref ItemId, default(FastBufferWriter.ForFixedStrings));
			serializer.SerializeValue(ref CustomData, default(FastBufferWriter.ForFixedStrings));
			serializer.SerializeValue(ref HasCustomSushi, default(FastBufferWriter.ForPrimitives));
			if (HasCustomSushi)
			{
				CustomSushi.NetworkSerialize(serializer);
			}
		}
	}
}
