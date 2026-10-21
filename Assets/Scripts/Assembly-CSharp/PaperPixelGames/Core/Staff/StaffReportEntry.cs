using System;

namespace PaperPixelGames.Core.Staff
{
	[Serializable]
	public sealed class StaffReportEntry
	{
		public int EmployeeId;

		public int CustomizationId = -1;

		public string Name;

		public StaffGender Gender;

		public bool HasGender;

		public StaffRole Role;

		public int Tasks;

		public double WorkSeconds;

		public int WageStored;

		public int UnpaidWageStored;

		public bool HasLeft;

		public double ProgressionXp;

		public int Packages;

		public int SpentStored;

		public int IngredientsProduced;

		public int SushiProduced;

		public int CustomersServed;

		public int PaymentsCollected;

		public int TraysCleaned;

		public StaffDeparture Departure;

		public StaffReportEntry Copy()
		{
			return (StaffReportEntry)MemberwiseClone();
		}
	}
}
