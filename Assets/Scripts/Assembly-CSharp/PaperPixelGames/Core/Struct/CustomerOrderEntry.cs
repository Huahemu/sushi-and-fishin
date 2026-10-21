using System;
using PaperPixelGames.Core.Staff;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Struct
{
	public struct CustomerOrderEntry : INetworkSerializable, IEquatable<CustomerOrderEntry>
	{
		public int OrderId;

		public FixedString32Bytes TableNo;

		public int ClientOrderIndex;

		public FixedString64Bytes CustomerName;

		public FixedString64Bytes WaitressName;

		public bool IsStaffWaitress;

		public StaffGender StaffWaitressGender;

		public FixedList128Bytes<FixedString32Bytes> FoodIds;

		public FixedList128Bytes<FixedString32Bytes> DeliveredFoodIds;

		public double ExpireServerTime;

		public byte Status;

		public bool Equals(CustomerOrderEntry other)
		{
			if (OrderId != other.OrderId)
			{
				return false;
			}
			if (!TableNo.Equals(other.TableNo))
			{
				return false;
			}
			if (ClientOrderIndex != other.ClientOrderIndex)
			{
				return false;
			}
			if (!CustomerName.Equals(other.CustomerName))
			{
				return false;
			}
			if (!WaitressName.Equals(other.WaitressName))
			{
				return false;
			}
			if (IsStaffWaitress != other.IsStaffWaitress || StaffWaitressGender != other.StaffWaitressGender)
			{
				return false;
			}
			if (!ExpireServerTime.Equals(other.ExpireServerTime))
			{
				return false;
			}
			if (Status != other.Status)
			{
				return false;
			}
			if (!AreListsEqual(FoodIds, other.FoodIds))
			{
				return false;
			}
			if (!AreListsEqual(DeliveredFoodIds, other.DeliveredFoodIds))
			{
				return false;
			}
			return true;
		}

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref OrderId, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref TableNo, default(FastBufferWriter.ForFixedStrings));
			serializer.SerializeValue(ref ClientOrderIndex, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref CustomerName, default(FastBufferWriter.ForFixedStrings));
			serializer.SerializeValue(ref WaitressName, default(FastBufferWriter.ForFixedStrings));
			serializer.SerializeValue(ref IsStaffWaitress, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref StaffWaitressGender, default(FastBufferWriter.ForEnums));
			serializer.SerializeValue(ref ExpireServerTime, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref Status);
			SerializeFoodList(serializer, ref FoodIds);
			SerializeFoodList(serializer, ref DeliveredFoodIds);
		}

		public void ClearFoods()
		{
			FoodIds.Clear();
			DeliveredFoodIds.Clear();
		}

		public void AddFood(FixedString32Bytes foodId)
		{
			if (FoodIds.Length >= FoodIds.Capacity)
			{
				Debug.LogWarning($"OrderEntry ({OrderId}) FoodIds capacity reached.");
			}
			else
			{
				FoodIds.Add(in foodId);
			}
		}

		public bool TryRegisterDeliveredFood(FixedString32Bytes foodId)
		{
			int num = CountFood(FoodIds, foodId);
			int num2 = CountFood(DeliveredFoodIds, foodId);
			if (num <= 0)
			{
				return false;
			}
			if (num2 >= num)
			{
				return false;
			}
			if (DeliveredFoodIds.Length >= DeliveredFoodIds.Capacity)
			{
				Debug.LogWarning($"OrderEntry ({OrderId}) DeliveredFoodIds capacity reached.");
				return false;
			}
			DeliveredFoodIds.Add(in foodId);
			return true;
		}

		public bool TryRegisterDeliveredFoods(FixedList128Bytes<FixedString32Bytes> foodIds)
		{
			bool result = false;
			for (int i = 0; i < foodIds.Length; i++)
			{
				if (TryRegisterDeliveredFood(foodIds[i]))
				{
					result = true;
				}
			}
			return result;
		}

		public bool TryUnregisterDeliveredFood(FixedString32Bytes foodId)
		{
			for (int i = 0; i < DeliveredFoodIds.Length; i++)
			{
				if (!DeliveredFoodIds[i].Equals(foodId))
				{
					continue;
				}
				FixedList128Bytes<FixedString32Bytes> deliveredFoodIds = default;
				for (int j = 0; j < DeliveredFoodIds.Length; j++)
				{
					if (j != i)
					{
						deliveredFoodIds.Add(DeliveredFoodIds[j]);
					}
				}
				DeliveredFoodIds = deliveredFoodIds;
				return true;
			}
			return false;
		}

		public bool TryUnregisterDeliveredFoods(FixedList128Bytes<FixedString32Bytes> foodIds)
		{
			bool result = false;
			for (int i = 0; i < foodIds.Length; i++)
			{
				if (TryUnregisterDeliveredFood(foodIds[i]))
				{
					result = true;
				}
			}
			return result;
		}

		public bool IsFullyDelivered()
		{
			if (FoodIds.Length == 0)
			{
				return false;
			}
			for (int i = 0; i < FoodIds.Length; i++)
			{
				FixedString32Bytes foodId = FoodIds[i];
				int num = CountFood(FoodIds, foodId);
				if (CountFood(DeliveredFoodIds, foodId) < num)
				{
					return false;
				}
			}
			return true;
		}

		public void SetStatus(OrderReceiptStatus status)
		{
			Status = (byte)status;
		}

		public OrderReceiptStatus GetStatus()
		{
			return (OrderReceiptStatus)Status;
		}

		private static int CountFood(FixedList128Bytes<FixedString32Bytes> list, FixedString32Bytes foodId)
		{
			int num = 0;
			for (int i = 0; i < list.Length; i++)
			{
				if (list[i].Equals(foodId))
				{
					num++;
				}
			}
			return num;
		}

		private static bool AreListsEqual(FixedList128Bytes<FixedString32Bytes> a, FixedList128Bytes<FixedString32Bytes> b)
		{
			if (a.Length != b.Length)
			{
				return false;
			}
			for (int i = 0; i < a.Length; i++)
			{
				if (!a[i].Equals(b[i]))
				{
					return false;
				}
			}
			return true;
		}

		private static void SerializeFoodList<T>(BufferSerializer<T> serializer, ref FixedList128Bytes<FixedString32Bytes> list) where T : IReaderWriter
		{
			if (serializer.IsWriter)
			{
				int value = list.Length;
				serializer.SerializeValue(ref value, default(FastBufferWriter.ForPrimitives));
				for (int i = 0; i < value; i++)
				{
					FixedString32Bytes value2 = list[i];
					serializer.SerializeValue(ref value2, default(FastBufferWriter.ForFixedStrings));
				}
				return;
			}
			int value3 = 0;
			serializer.SerializeValue(ref value3, default(FastBufferWriter.ForPrimitives));
			list.Clear();
			value3 = Mathf.Min(value3, list.Capacity);
			for (int j = 0; j < value3; j++)
			{
				FixedString32Bytes value4 = default;
				serializer.SerializeValue(ref value4, default(FastBufferWriter.ForFixedStrings));
				list.Add(in value4);
			}
		}
	}
}
