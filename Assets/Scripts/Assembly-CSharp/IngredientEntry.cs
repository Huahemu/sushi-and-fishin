using System;
using Unity.Collections;
using Unity.Netcode;

public struct IngredientEntry : INetworkSerializable, IEquatable<IngredientEntry>
{
	public FixedString64Bytes IngredientID;

	public byte Count;

	public IngredientEntry(FixedString64Bytes id, byte count)
	{
		IngredientID = id;
		Count = count;
	}

	public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
	{
		serializer.SerializeValue(ref IngredientID, default(FastBufferWriter.ForFixedStrings));
		serializer.SerializeValue(ref Count);
	}

	public bool Equals(IngredientEntry other)
	{
		if (IngredientID.Equals(other.IngredientID))
		{
			return Count == other.Count;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return (IngredientID.GetHashCode() * 397) ^ Count.GetHashCode();
	}
}
