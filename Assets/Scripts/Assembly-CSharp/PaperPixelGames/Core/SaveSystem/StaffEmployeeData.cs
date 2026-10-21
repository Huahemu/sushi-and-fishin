using System;
using PaperPixelGames.Core.Staff;

namespace PaperPixelGames.Core.SaveSystem
{
	[Serializable]
	public sealed class StaffEmployeeData
	{
		public int EmployeeId;

		public string Name;

		public StaffRole Role;

		public StaffGender Gender;

		public int CustomizationId = -1;

		public byte SkillLevel;

		public int LastPaidDay = -1;

		public byte QualityTier;

		public ushort PrimaryStatHundredths;

		public byte SecondaryStat;

		public int DailyWageDollars;

		public int HireSequence;

		public bool BuyMissingStock = true;

		public bool GreenMarketEnabled = true;

		public bool BlueMarketEnabled = true;

		public bool StockingStopped;

		public bool RiceCookerEnabled = true;

		public bool FilletMachineEnabled = true;

		public bool PrepareOrdersEnabled = true;

		public bool StockConveyorEnabled = true;

		public bool TakeOrdersEnabled = true;

		public bool ServeOrdersEnabled = true;

		public bool CollectFilletsEnabled;
	}
}
