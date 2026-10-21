using PaperPixelGames.Core.Staff;
using PaperPixelGames.Core.Tablet;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.UI.DailyReportUI
{
	public sealed class StaffDailyReportCard : MonoBehaviour
	{
		public Image Icon;

		public Image Accent;

		public Image IconWell;

		public TextMeshProUGUI Name;

		public TextMeshProUGUI Role;

		public TextMeshProUGUI Work;

		public TextMeshProUGUI Output;

		public TextMeshProUGUI Departure;

		public LayoutElement Layout;

		public void Bind(StaffReportEntry entry, Sprite icon)
		{
			Color color = StaffCardFeedback.RoleColor(entry.Role);
			Icon.sprite = icon;
			Icon.enabled = icon != null;
			Accent.color = color;
			IconWell.color = Color.Lerp(Color.white, color, 0.16f);
			Name.text = StaffNames.DisplayName(entry);
			Role.text = TabletLocalization.GetStaffRoleName(entry.Role, entry.Role.ToString());
			Role.color = color;
			string[] array = StaffDailyReportPopup.FormatCard(entry).Split('\n');
			Work.text = ((array.Length > 1) ? array[1] : "");
			Output.text = ((array.Length > 2) ? array[2] : "");
			Departure.text = ((array.Length > 3) ? string.Join("\n", array, 3, array.Length - 3).Trim() : "");
			bool flag = Departure.text.Length > 0;
			Departure.gameObject.SetActive(flag);
			Layout.preferredHeight = (flag ? 188 : 142);
		}

		public void RefreshLayout()
		{
			float top = 22f;
			top = FitLine(Work, top, 42f) + 8f;
			top = FitLine(Output, top, 48f);
			if (Departure.gameObject.activeSelf)
			{
				top = FitLine(Departure, top + 12f, 30f);
			}
			Layout.preferredHeight = Mathf.Max(142f, top + 20f);
		}

		private static float FitLine(TextMeshProUGUI label, float top, float minimumHeight)
		{
			label.fontSize = label.fontSizeMax;
			float num = Mathf.Max(minimumHeight, label.GetPreferredValues(label.text, label.rectTransform.rect.width, float.PositiveInfinity).y + 6f);
			RectTransform rectTransform = label.rectTransform;
			rectTransform.offsetMin = new Vector2(rectTransform.offsetMin.x, 0f - top - num);
			rectTransform.offsetMax = new Vector2(rectTransform.offsetMax.x, 0f - top);
			return top + num;
		}
	}
}
