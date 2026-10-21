using System;
using System.Text;
using Unity.Collections;
using Unity.Netcode;

namespace PaperPixelGames.Core.Data
{
	public struct CustomSushiRecipeNetworkData : INetworkSerializable, IEquatable<CustomSushiRecipeNetworkData>
	{
		public FixedString64Bytes RecipeId;

		public FixedString128Bytes DisplayName;

		public SushiCompositionNetworkData Composition;

		public int Price;

		public FixedString64Bytes IconId;

		public static bool TryCreate(CustomSushiRecipeData source, out CustomSushiRecipeNetworkData result)
		{
			result = default;
			if (source == null || string.IsNullOrWhiteSpace(source.RecipeId) || string.IsNullOrWhiteSpace(source.DisplayName) || !result.Composition.TrySet(source.Composition))
			{
				return false;
			}
			if (Encoding.UTF8.GetByteCount(source.RecipeId) > FixedString64Bytes.UTF8MaxLengthInBytes || Encoding.UTF8.GetByteCount(source.DisplayName) > FixedString128Bytes.UTF8MaxLengthInBytes || Encoding.UTF8.GetByteCount(source.IconId ?? string.Empty) > FixedString64Bytes.UTF8MaxLengthInBytes)
			{
				return false;
			}
			result.RecipeId = source.RecipeId;
			result.DisplayName = source.DisplayName;
			result.Price = Math.Max(0, source.Price);
			result.IconId = source.IconId ?? string.Empty;
			return true;
		}

		public readonly CustomSushiRecipeData ToData()
		{
			return new CustomSushiRecipeData
			{
				RecipeId = RecipeId.ToString(),
				DisplayName = DisplayName.ToString(),
				Composition = Composition.ToData(),
				Price = Math.Max(0, Price),
				IconId = IconId.ToString()
			};
		}

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref RecipeId, default(FastBufferWriter.ForFixedStrings));
			serializer.SerializeValue(ref DisplayName, default(FastBufferWriter.ForFixedStrings));
			Composition.NetworkSerialize(serializer);
			serializer.SerializeValue(ref Price, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref IconId, default(FastBufferWriter.ForFixedStrings));
		}

		public readonly bool Equals(CustomSushiRecipeNetworkData other)
		{
			if (RecipeId.Equals(other.RecipeId) && DisplayName.Equals(other.DisplayName) && Composition.Equals(other.Composition) && Price == other.Price)
			{
				return IconId.Equals(other.IconId);
			}
			return false;
		}
	}
}
