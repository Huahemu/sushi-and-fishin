using System;
using System.Text;
using PaperPixelGames.Core.Interaction;
using Unity.Collections;
using Unity.Netcode;

namespace PaperPixelGames.Core.Data
{
	public struct SushiCompositionNetworkData : INetworkSerializable, IEquatable<SushiCompositionNetworkData>
	{
		public SushiType SushiType;

		public FixedList4096Bytes<FixedString64Bytes> IngredientIds;

		public bool TrySet(SushiCompositionData composition)
		{
			IngredientIds.Clear();
			SushiType = composition?.SushiType ?? SushiType.Empty;
			if (composition?.IngredientIds == null)
			{
				return false;
			}
			for (int i = 0; i < composition.IngredientIds.Count; i++)
			{
				string text = composition.IngredientIds[i];
				if (string.IsNullOrWhiteSpace(text) || Encoding.UTF8.GetByteCount(text) > FixedString64Bytes.UTF8MaxLengthInBytes)
				{
					IngredientIds.Clear();
					SushiType = SushiType.Empty;
					return false;
				}
				if (IngredientIds.Length >= IngredientIds.Capacity)
				{
					IngredientIds.Clear();
					SushiType = SushiType.Empty;
					return false;
				}
				IngredientIds.Add(new FixedString64Bytes(text));
			}
			if (SushiType != SushiType.Empty)
			{
				return IngredientIds.Length > 0;
			}
			return false;
		}

		public SushiCompositionData ToData()
		{
			SushiCompositionData sushiCompositionData = new SushiCompositionData
			{
				SushiType = SushiType
			};
			for (int i = 0; i < IngredientIds.Length; i++)
			{
				sushiCompositionData.IngredientIds.Add(IngredientIds[i].ToString());
			}
			return sushiCompositionData;
		}

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			ushort value = (ushort)SushiType;
			serializer.SerializeValue(ref value, default(FastBufferWriter.ForPrimitives));
			if (serializer.IsReader)
			{
				SushiType = (SushiType)value;
			}
			int value2 = IngredientIds.Length;
			serializer.SerializeValue(ref value2, default(FastBufferWriter.ForPrimitives));
			if (serializer.IsReader)
			{
				IngredientIds.Clear();
				int num = Math.Min(Math.Max(0, value2), IngredientIds.Capacity);
				for (int i = 0; i < num; i++)
				{
					FixedString64Bytes value3 = default;
					serializer.SerializeValue(ref value3, default(FastBufferWriter.ForFixedStrings));
					IngredientIds.Add(in value3);
				}
			}
			else
			{
				for (int j = 0; j < value2; j++)
				{
					FixedString64Bytes value4 = IngredientIds[j];
					serializer.SerializeValue(ref value4, default(FastBufferWriter.ForFixedStrings));
				}
			}
		}

		public bool Equals(SushiCompositionNetworkData other)
		{
			return SushiCompositionComparer.Equals(ToData(), other.ToData());
		}
	}
}
