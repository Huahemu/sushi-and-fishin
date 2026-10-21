using System;
using System.Collections.Generic;
using UnityEngine;

namespace PaperPixelGames.Core.Staff
{
	[Serializable]
	public sealed class StaffRoleDefinition
	{
		[Range(0f, 10f)]
		public int UnlockLevel;

		public int ProgressionPremium;

		public StaffRole Role;

		public string DisplayName;

		public string PrimaryStatLabel;

		public string SecondaryStatLabel;

		public float MinimumPrimaryStat = 0.9f;

		public float MaximumPrimaryStat = 1.6f;

		public int MinimumSecondaryStat = 1;

		public int MaximumSecondaryStat = 5;

		public int BaseDailyWage = 54;

		public int PrimaryStatWageRange = 50;

		public int SecondaryStatWagePerPoint = 10;

		public List<StaffCustomizationOption> Customizations = new List<StaffCustomizationOption>();
	}
}
