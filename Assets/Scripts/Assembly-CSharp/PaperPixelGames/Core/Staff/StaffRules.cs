using System;
using System.Collections.Generic;
using PaperPixelGames.Core.SaveSystem;

namespace PaperPixelGames.Core.Staff
{
	public static class StaffRules
	{
		public const int SaveVersion = 1;

		public const int RefreshCost = 100;

		public static bool IsRoleEnabled(StaffRole role)
		{
			if ((int)role >= 0)
			{
				return (int)role <= 3;
			}
			return false;
		}

		public static int DefaultUnlockLevel(StaffRole role)
		{
			return role switch
			{
				StaffRole.Stocker => 3, 
				StaffRole.Waitress => 5, 
				StaffRole.PrepCook => 7, 
				StaffRole.SushiChef => 10, 
				_ => int.MaxValue, 
			};
		}

		public static byte ClampSkill(int value)
		{
			return (byte)Math.Clamp(value, 1, 5);
		}

		public static float WorkSpeed(int skill)
		{
			return 1f + (float)(ClampSkill(skill) - 1) * 0.15f;
		}

		public static (int min, int max) SkillRange(int level)
		{
			if (level <= 8)
			{
				if (level > 2)
				{
					if (level <= 5)
					{
						return (min: 1, max: 2);
					}
					return (min: 1, max: 3);
				}
				return (min: 1, max: 1);
			}
			if (level <= 11)
			{
				return (min: 2, max: 4);
			}
			return (min: 3, max: 5);
		}

		public static int FireCharge(int wage, int lastPaidDay, int day, int balance)
		{
			if (lastPaidDay != day)
			{
				return Math.Min(Math.Max(0, balance), Math.Max(0, wage) * 100);
			}
			return 0;
		}

		public static void Migrate(StaffData data)
		{
			if (data.Version >= 1)
			{
				return;
			}
			List<StaffEmployeeData>[] array = new List<StaffEmployeeData>[2] { data.Team, data.Candidates };
			foreach (List<StaffEmployeeData> list in array)
			{
				if (list == null)
				{
					continue;
				}
				foreach (StaffEmployeeData item in list)
				{
					if (item != null)
					{
						item.SkillLevel = ((item.Role == StaffRole.SushiChef || item.Role == StaffRole.PrepCook) ? ClampSkill((int)Math.Round(((float)(int)item.PrimaryStatHundredths / 100f - 1f) / 0.15f, MidpointRounding.AwayFromZero) + 1) : ClampSkill(item.SecondaryStat));
						item.LastPaidDay = data.LastPayrollDay;
					}
				}
			}
			data.Version = 1;
		}
	}
}
