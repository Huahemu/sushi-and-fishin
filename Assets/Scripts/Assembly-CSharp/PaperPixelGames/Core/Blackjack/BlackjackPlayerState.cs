using System;
using Unity.Netcode;

namespace PaperPixelGames.Core.Blackjack
{
	[Serializable]
	public struct BlackjackPlayerState : INetworkSerializable, IEquatable<BlackjackPlayerState>
	{
		public int SeatIndex;

		public ulong ClientId;

		public int BetStored;

		public int HandValue;

		public BlackjackHandState HandState;

		public bool Equals(BlackjackPlayerState other)
		{
			if (SeatIndex == other.SeatIndex && ClientId == other.ClientId && BetStored == other.BetStored && HandValue == other.HandValue)
			{
				return HandState == other.HandState;
			}
			return false;
		}

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref SeatIndex, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref ClientId, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref BetStored, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref HandValue, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref HandState, default(FastBufferWriter.ForEnums));
		}
	}
}
