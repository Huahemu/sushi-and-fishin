using PaperPixelGames.Core.Staff;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Tablet
{
	public sealed class StaffCardFeedback : MonoBehaviour
	{
		public Image Accent;

		public Image RoleBadge;

		public TextMeshProUGUI RoleLabel;

		public TextMeshProUGUI SkillLabel;

		public Image[] SkillPips;

		public Image StatusBackground;

		public Image StatusDot;

		public static Color RoleColor(StaffRole role)
		{
			return role switch
			{
				StaffRole.SushiChef => new Color32(53, 112, 137, byte.MaxValue), 
				StaffRole.PrepCook => new Color32(78, 123, 65, byte.MaxValue), 
				StaffRole.Waitress => new Color32(131, 86, 135, byte.MaxValue), 
				_ => new Color32(163, 104, 30, byte.MaxValue), 
			};
		}

		public void Bind(StaffEmployeeEntry employee)
		{
			Color color = RoleColor(employee.Role);
			if (Accent != null)
			{
				Accent.color = color;
			}
			if (RoleBadge != null)
			{
				RoleBadge.color = Color.Lerp(Color.white, color, 0.2f);
			}
			if (RoleLabel != null)
			{
				RoleLabel.color = color * new Color(0.8f, 0.8f, 0.8f, 1f);
			}
			if (SkillLabel != null)
			{
				SkillLabel.text = string.Format(StaffText.Get("skill_level"), employee.SkillLevel);
			}
			if (SkillPips != null)
			{
				for (int i = 0; i < SkillPips.Length; i++)
				{
					if (SkillPips[i] != null)
					{
						SkillPips[i].color = ((i < employee.SkillLevel) ? color : new Color(0.68f, 0.63f, 0.52f, 0.35f));
					}
				}
			}
			Color color2 = ((employee.Departure != StaffDeparture.None) ? new Color32(166, 65, 49, byte.MaxValue) : ((employee.StockingStopped || employee.Status == StaffStatus.Stopped) ? new Color32(106, 102, 91, byte.MaxValue) : (IsWaiting(employee.Status) ? new Color32(170, 119, 35, byte.MaxValue) : new Color32(63, 122, 85, byte.MaxValue))));
			if (StatusDot != null)
			{
				StatusDot.color = color2;
			}
			if (StatusBackground != null)
			{
				StatusBackground.color = Color.Lerp(Color.white, color2, 0.17f);
			}
		}

		private static bool IsWaiting(StaffStatus status)
		{
			if (status != StaffStatus.Waiting && status != StaffStatus.WaitingForIngredients && status != StaffStatus.WaitingForMachine && status != StaffStatus.WaitingForDishes && status != StaffStatus.WaitingForConveyorSlot && status != StaffStatus.InsufficientMoney && status != StaffStatus.ProductsLocked && status != StaffStatus.MissingWorkplace && status != StaffStatus.NoTray)
			{
				return status == StaffStatus.ConveyorDisabled;
			}
			return true;
		}
	}
}
