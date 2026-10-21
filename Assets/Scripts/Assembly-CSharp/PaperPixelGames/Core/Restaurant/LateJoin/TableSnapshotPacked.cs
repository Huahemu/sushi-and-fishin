using System;
using Unity.Netcode;

namespace PaperPixelGames.Core.Restaurant.LateJoin
{
	[Serializable]
	public struct TableSnapshotPacked : INetworkSerializable
	{
		public byte TableId;

		public byte CurrentState;

		public byte SeatedCount;

		public byte ArrivedCustomerCount;

		public TableSnapFlags Flags;

		public int TakingOrderSeed;

		public int TotalPaymentFinal;

		public byte DeliveredSeatMask;

		public SeatOrderPacked Seat0;

		public SeatOrderPacked Seat1;

		public SeatOrderPacked Seat2;

		public SeatOrderPacked Seat3;

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref TableId);
			serializer.SerializeValue(ref CurrentState);
			serializer.SerializeValue(ref SeatedCount);
			serializer.SerializeValue(ref ArrivedCustomerCount);
			byte value = (byte)Flags;
			serializer.SerializeValue(ref value);
			if (serializer.IsReader)
			{
				Flags = (TableSnapFlags)value;
			}
			serializer.SerializeValue(ref TakingOrderSeed, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref TotalPaymentFinal, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref DeliveredSeatMask);
			serializer.SerializeValue(ref Seat0, default(FastBufferWriter.ForNetworkSerializable));
			serializer.SerializeValue(ref Seat1, default(FastBufferWriter.ForNetworkSerializable));
			serializer.SerializeValue(ref Seat2, default(FastBufferWriter.ForNetworkSerializable));
			serializer.SerializeValue(ref Seat3, default(FastBufferWriter.ForNetworkSerializable));
		}
	}
}
