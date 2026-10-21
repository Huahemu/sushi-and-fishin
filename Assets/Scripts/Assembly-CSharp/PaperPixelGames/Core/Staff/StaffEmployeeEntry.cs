using System;
using Unity.Collections;
using Unity.Netcode;

namespace PaperPixelGames.Core.Staff
{
	[Serializable]
	public struct StaffEmployeeEntry : INetworkSerializable, IEquatable<StaffEmployeeEntry>
	{
		public int EmployeeId;

		public FixedString64Bytes Name;

		public StaffRole Role;

		public StaffGender Gender;

		public int CustomizationId;

		public byte SkillLevel;

		public int LastPaidDay;

		public StaffStatus Status;

		public StaffDeparture Departure;

		public int DailyWageDollars;

		public int HireSequence;

		public bool BuyMissingStock;

		public bool GreenMarketEnabled;

		public bool BlueMarketEnabled;

		public bool StockingStopped;

		public bool RiceCookerEnabled;

		public bool FilletMachineEnabled;

		public bool PrepareOrdersEnabled;

		public bool StockConveyorEnabled;

		public bool TakeOrdersEnabled;

		public bool ServeOrdersEnabled;

		public bool CollectFilletsEnabled;

		public FixedString64Bytes StatusItemId;

		public float WorkSpeed => StaffRules.WorkSpeed(SkillLevel);

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref EmployeeId, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref Name, default(FastBufferWriter.ForFixedStrings));
			serializer.SerializeValue(ref Role, default(FastBufferWriter.ForEnums));
			serializer.SerializeValue(ref Gender, default(FastBufferWriter.ForEnums));
			serializer.SerializeValue(ref CustomizationId, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref SkillLevel);
			serializer.SerializeValue(ref LastPaidDay, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref Status, default(FastBufferWriter.ForEnums));
			serializer.SerializeValue(ref Departure, default(FastBufferWriter.ForEnums));
			serializer.SerializeValue(ref DailyWageDollars, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref HireSequence, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref BuyMissingStock, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref GreenMarketEnabled, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref BlueMarketEnabled, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref StockingStopped, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref RiceCookerEnabled, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref FilletMachineEnabled, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref PrepareOrdersEnabled, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref StockConveyorEnabled, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref TakeOrdersEnabled, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref ServeOrdersEnabled, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref CollectFilletsEnabled, default(FastBufferWriter.ForPrimitives));
			serializer.SerializeValue(ref StatusItemId, default(FastBufferWriter.ForFixedStrings));
		}

		public bool Equals(StaffEmployeeEntry other)
		{
			if (EmployeeId == other.EmployeeId && Name.Equals(other.Name) && Role == other.Role && Gender == other.Gender && CustomizationId == other.CustomizationId && SkillLevel == other.SkillLevel && LastPaidDay == other.LastPaidDay && Status == other.Status && Departure == other.Departure && DailyWageDollars == other.DailyWageDollars && HireSequence == other.HireSequence && BuyMissingStock == other.BuyMissingStock && GreenMarketEnabled == other.GreenMarketEnabled && BlueMarketEnabled == other.BlueMarketEnabled && StockingStopped == other.StockingStopped && RiceCookerEnabled == other.RiceCookerEnabled && FilletMachineEnabled == other.FilletMachineEnabled && PrepareOrdersEnabled == other.PrepareOrdersEnabled && StockConveyorEnabled == other.StockConveyorEnabled && TakeOrdersEnabled == other.TakeOrdersEnabled && ServeOrdersEnabled == other.ServeOrdersEnabled && CollectFilletsEnabled == other.CollectFilletsEnabled)
			{
				return StatusItemId.Equals(other.StatusItemId);
			}
			return false;
		}

		public override bool Equals(object obj)
		{
			if (obj is StaffEmployeeEntry other)
			{
				return Equals(other);
			}
			return false;
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(EmployeeId, Name, Role, Gender, CustomizationId, SkillLevel, LastPaidDay, Status);
		}
	}
}
