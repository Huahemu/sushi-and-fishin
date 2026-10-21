using System;
using System.Collections.Generic;
using PaperPixelGames.Core.DailyReport;
using PaperPixelGames.Core.Staff;
using PaperPixelGames.Core.Tablet;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperPixelGames.Core.UI.DailyReportUI
{
	public sealed class StaffDailyReportPopup : MonoBehaviour
	{
		public TextMeshProUGUI Title;

		public TextMeshProUGUI Summary;

		public TextMeshProUGUI Empty;

		public StaffDailyReportCard CardTemplate;

		public StaffCatalogSO Catalog;

		public Transform CardsRoot;

		public ScrollRect Scroll;

		public Button Okay;

		public TextMeshProUGUI OkayLabel;

		private readonly List<StaffDailyReportCard> cards = new List<StaffDailyReportCard>();

		private Action acknowledged;

		public bool IsOpen => base.gameObject.activeSelf;

		private void Awake()
		{
			Okay.onClick.AddListener(Acknowledge);
		}

		private void OnDestroy()
		{
			Okay.onClick.RemoveListener(Acknowledge);
		}

		public void Show(CommonReportPayload report, Action onAcknowledged)
		{
			acknowledged = onAcknowledged;
			Title.text = string.Format(StaffText.Get("daily_title"), report.Day);
			OkayLabel.text = StaffText.Get("daily_okay");
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			List<StaffReportEntry> staff = report.Staff;
			if (staff != null)
			{
				foreach (StaffReportEntry item in staff)
				{
					if (item.HasLeft)
					{
						num++;
					}
					else if (item.Departure != StaffDeparture.None)
					{
						num4++;
					}
					if (item.Departure == StaffDeparture.Unpaid)
					{
						num2++;
					}
					if (item.Departure == StaffDeparture.Fired)
					{
						num3++;
					}
				}
			}
			Summary.text = string.Format(StaffText.Get("daily_summary"), report.StaffTeamCount, (float)report.StaffWagesStored / 100f, (float)report.StaffUnpaidWagesStored / 100f, num, num4, num2, num3, (float)report.StaffPurchasesStored / 100f);
			int num5 = staff?.Count ?? 0;
			Empty.text = StaffText.Get("daily_empty");
			Empty.gameObject.SetActive(num5 == 0);
			CardTemplate.gameObject.SetActive(value: false);
			for (int i = 0; i < num5; i++)
			{
				if (cards.Count <= i)
				{
					cards.Add(UnityEngine.Object.Instantiate(CardTemplate, CardsRoot));
				}
				StaffReportEntry staffReportEntry = staff[i];
				StaffCatalogSO staffCatalogSO = ((StaffManager.Instance != null) ? StaffManager.Instance.Catalog : Catalog);
				cards[i].Bind(staffReportEntry, (staffReportEntry.CustomizationId >= 0) ? staffCatalogSO?.ResolveCharacter(staffReportEntry.Role, staffReportEntry.CustomizationId)?.Portrait : null);
				cards[i].gameObject.SetActive(value: true);
			}
			for (int j = num5; j < cards.Count; j++)
			{
				cards[j].gameObject.SetActive(value: false);
			}
			base.transform.SetAsLastSibling();
			base.gameObject.SetActive(value: true);
			Canvas.ForceUpdateCanvases();
			for (int k = 0; k < num5; k++)
			{
				cards[k].RefreshLayout();
			}
			LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)CardsRoot);
			Canvas.ForceUpdateCanvases();
			Scroll.StopMovement();
			Scroll.verticalNormalizedPosition = 1f;
			Okay.Select();
		}

		public static string FormatCard(StaffReportEntry entry)
		{
			string key = entry.Role switch
			{
				StaffRole.Waitress => "waitress_report_card", 
				StaffRole.SushiChef => "chef_report_card", 
				StaffRole.PrepCook => "prep_report_card", 
				_ => "report_card", 
			};
			int num = entry.Role switch
			{
				StaffRole.Waitress => entry.CustomersServed, 
				StaffRole.SushiChef => entry.SushiProduced, 
				StaffRole.PrepCook => entry.IngredientsProduced, 
				_ => entry.Packages, 
			};
			string text = ((entry.Departure == StaffDeparture.None) ? "" : (StaffText.Get((entry.Departure == StaffDeparture.Unpaid) ? "daily_reason_unpaid" : "daily_reason_fired") + " · " + StaffText.Get(entry.HasLeft ? "daily_left" : "Leaving")));
			string text2 = string.Format(StaffText.Get(key), StaffNames.DisplayName(entry), TabletLocalization.GetStaffRoleName(entry.Role, entry.Role.ToString()), entry.Tasks, entry.WorkSeconds / 60.0, (float)entry.WageStored / 100f, num, (float)entry.SpentStored / 100f, entry.ProgressionXp, text, entry.PaymentsCollected, entry.TraysCleaned);
			if (entry.UnpaidWageStored > 0)
			{
				text2 = text2 + "\n" + string.Format(StaffText.Get("daily_unpaid_amount"), (float)entry.UnpaidWageStored / 100f);
			}
			return text2.TrimEnd();
		}

		public void Acknowledge()
		{
			if (IsOpen)
			{
				Action action = acknowledged;
				Close();
				action?.Invoke();
			}
		}

		public void Close()
		{
			acknowledged = null;
			if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(base.transform))
			{
				EventSystem.current.SetSelectedGameObject(null);
			}
			base.gameObject.SetActive(value: false);
		}
	}
}
