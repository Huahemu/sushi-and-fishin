using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Struct
{
	public struct CustomerOrderFoodPayload : INetworkSerializable
	{
		public FixedList128Bytes<FixedString32Bytes> FoodIds;

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			if (serializer.IsWriter)
			{
				int value = FoodIds.Length;
				serializer.SerializeValue(ref value, default(FastBufferWriter.ForPrimitives));
				for (int i = 0; i < value; i++)
				{
					FixedString32Bytes value2 = FoodIds[i];
					serializer.SerializeValue(ref value2, default(FastBufferWriter.ForFixedStrings));
				}
				return;
			}
			int value3 = 0;
			serializer.SerializeValue(ref value3, default(FastBufferWriter.ForPrimitives));
			FoodIds.Clear();
			value3 = Mathf.Min(value3, FoodIds.Capacity);
			for (int j = 0; j < value3; j++)
			{
				FixedString32Bytes value4 = default;
				serializer.SerializeValue(ref value4, default(FastBufferWriter.ForFixedStrings));
				FoodIds.Add(in value4);
			}
		}
	}
}
