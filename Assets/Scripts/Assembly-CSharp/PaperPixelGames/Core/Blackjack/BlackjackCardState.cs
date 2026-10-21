using System;
using Unity.Netcode;

namespace PaperPixelGames.Core.Blackjack
{
	[Serializable]
	public struct BlackjackCardState : INetworkSerializable, IEquatable<BlackjackCardState>
	{
		public int SeatIndex;

		public byte Order;

		public byte Rank;

		public BlackjackSuit Suit;

		public bool Hidden;

		public bool Equals(BlackjackCardState other)
		{
			if (SeatIndex == other.SeatIndex && Order == other.Order && Rank == other.Rank && Suit == other.Suit)
			{
				return Hidden == other.Hidden;
			}
			return false;
		}

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref SeatIndex, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref Order);
			serializer.SerializeValue(ref Rank);
			serializer.SerializeValue(ref Suit, default(FastBufferWriter.ForEnums));
			serializer.SerializeValue(ref Hidden, default(FastBufferWriter.ForPrimitives));
		}
	}
}
