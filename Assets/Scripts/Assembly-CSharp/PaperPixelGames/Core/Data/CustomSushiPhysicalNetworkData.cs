using System;
using System.Text;
using Unity.Collections;
using Unity.Netcode;

namespace PaperPixelGames.Core.Data
{
	public struct CustomSushiPhysicalNetworkData : INetworkSerializable
	{
		public byte IdentityKind;

		public FixedString64Bytes RecipeId;

		public FixedString128Bytes DisplayName;

		public SushiCompositionNetworkData Composition;

		public int Price;

		public static bool TryCreate(CustomSushiPhysicalData source, out CustomSushiPhysicalNetworkData result)
		{
			result = default;
			if (source == null || !result.Composition.TrySet(source.Composition) || Encoding.UTF8.GetByteCount(source.RecipeId ?? string.Empty) > FixedString64Bytes.UTF8MaxLengthInBytes || Encoding.UTF8.GetByteCount(source.DisplayName ?? string.Empty) > FixedString128Bytes.UTF8MaxLengthInBytes)
			{
				return false;
			}
			result.IdentityKind = (byte)source.IdentityKind;
			result.RecipeId = source.RecipeId ?? string.Empty;
			result.DisplayName = source.DisplayName ?? string.Empty;
			result.Price = Math.Max(0, source.Price);
			return true;
		}

		public readonly CustomSushiPhysicalData ToData()
		{
			return new CustomSushiPhysicalData
			{
				IdentityKind = (SushiRecipeIdentityKind)IdentityKind,
				RecipeId = RecipeId.ToString(),
				DisplayName = DisplayName.ToString(),
				Composition = Composition.ToData(),
				Price = Math.Max(0, Price)
			};
		}

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref IdentityKind);
			serializer.SerializeValue(ref RecipeId, default(FastBufferWriter.ForFixedStrings));
			serializer.SerializeValue(ref DisplayName, default(FastBufferWriter.ForFixedStrings));
			Composition.NetworkSerialize(serializer);
			serializer.SerializeValue(ref Price, default(FastBufferWriter.ForPrimitives));
		}
	}
}
