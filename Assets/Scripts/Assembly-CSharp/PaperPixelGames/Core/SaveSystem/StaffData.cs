using System;
using System.Collections.Generic;

namespace PaperPixelGames.Core.SaveSystem
{
	[Serializable]
	public sealed class StaffData
	{
		public int Version;

		public int NextEmployeeId = 1;

		public int NextHireSequence = 1;

		public int LastPayrollDay = -1;

		public bool ProtectKitchenShelf = true;

		public bool CollectStationStorage;

		public List<StaffEmployeeData> Candidates = new List<StaffEmployeeData>();

		public List<StaffEmployeeData> Team = new List<StaffEmployeeData>();
	}
}
