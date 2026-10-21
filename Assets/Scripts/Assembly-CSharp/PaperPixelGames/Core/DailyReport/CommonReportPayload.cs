using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Staff;

namespace PaperPixelGames.Core.DailyReport
{
	[Serializable]
	public sealed class CommonReportPayload
	{
		public int Day;

		public string RestaurantName;

		public List<StaffReportEntry> Staff = new List<StaffReportEntry>();

		public int StaffWagesStored;

		public int StaffPurchasesStored;

		public int StaffUnpaidWagesStored;

		public int StaffTeamCount;

		public int TotalEarned;

		public int TotalCustomerCount;

		public int TotalFishCaught;

		public float DeepestDepthMeters;

		public BiggestCatchPayload BiggestCatch;

		public MvpPayload MVP;
	}
}
