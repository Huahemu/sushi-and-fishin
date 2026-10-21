using System;
using PaperPixelGames.Core.Staff;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Tablet
{
	public sealed class StaffTeamCardView : MonoBehaviour
	{
		[SerializeField]
		private Image _portrait;

		[SerializeField]
		private TextMeshProUGUI _nameText;

		[SerializeField]
		private TextMeshProUGUI _roleText;

		[SerializeField]
		private TextMeshProUGUI _statusText;

		[SerializeField]
		private TextMeshProUGUI _primaryStatLabelText;

		[SerializeField]
		private TextMeshProUGUI _primaryStatValueText;

		[SerializeField]
		private TextMeshProUGUI _secondaryStatLabelText;

		[SerializeField]
		private TextMeshProUGUI _secondaryStatValueText;

		[SerializeField]
		private TextMeshProUGUI _wageValueText;

		[SerializeField]
		private GameObject _stockerControls;

		[SerializeField]
		private Toggle _buyMissingStockToggle;

		[SerializeField]
		private Toggle _greenMarketToggle;

		[SerializeField]
		private Toggle _blueMarketToggle;

		[SerializeField]
		private Toggle _collectFilletsToggle;

		[SerializeField]
		private BaseButton _stopStockingButton;

		[SerializeField]
		private TextMeshProUGUI _stopStockingButtonText;

		[SerializeField]
		private BaseButton _fireButton;

		[SerializeField]
		private StaffCardFeedback _feedback;

		private int _employeeId;

		private bool _stockingStopped;

		private bool _prep;

		private bool _chef;

		private bool _waitress;

		private Action<int> _fireRequested;

		private Action<int, StockerSetting, bool> _settingChanged;

		public void Bind(StaffEmployeeEntry employee, StaffRoleDefinition definition, Action<int> fireRequested, Action<int, StockerSetting, bool> settingChanged)
		{
			_employeeId = employee.EmployeeId;
			_prep = employee.Role == StaffRole.PrepCook;
			_chef = employee.Role == StaffRole.SushiChef;
			_waitress = employee.Role == StaffRole.Waitress;
			_stockingStopped = employee.StockingStopped;
			_fireRequested = fireRequested;
			_settingChanged = settingChanged;
			_nameText.text = StaffNames.DisplayName(employee);
			_roleText.text = TabletLocalization.GetStaffRoleName(employee.Role, definition.DisplayName);
			_statusText.text = StaffText.Status(employee);
			_primaryStatLabelText.text = StaffText.SkillLabel(employee.Role);
			_primaryStatValueText.text = StaffText.SkillValue(employee);
			_secondaryStatLabelText.gameObject.SetActive(value: false);
			_secondaryStatValueText.gameObject.SetActive(value: false);
			if (_portrait != null)
			{
				_portrait.sprite = StaffManager.Instance?.Catalog?.ResolveCharacter(employee.Role, employee.CustomizationId)?.Portrait;
				_portrait.enabled = _portrait.sprite != null;
			}
			_wageValueText.text = (employee.DailyWageDollars * 100).ConvertMoneyText();
			bool flag = StaffRules.IsRoleEnabled(employee.Role) && employee.Departure == StaffDeparture.None;
			_fireButton.interactable = employee.Departure == StaffDeparture.None;
			_stockerControls.SetActive(flag);
			if (flag)
			{
				_buyMissingStockToggle.SetIsOnWithoutNotify(employee.BuyMissingStock);
				_greenMarketToggle.SetIsOnWithoutNotify(_waitress ? employee.TakeOrdersEnabled : (_chef ? employee.PrepareOrdersEnabled : (_prep ? employee.RiceCookerEnabled : employee.GreenMarketEnabled)));
				_blueMarketToggle.SetIsOnWithoutNotify(_waitress ? employee.ServeOrdersEnabled : (_chef ? employee.StockConveyorEnabled : (_prep ? employee.FilletMachineEnabled : employee.BlueMarketEnabled)));
				_buyMissingStockToggle.transform.parent.gameObject.SetActive(!_prep && !_chef && !_waitress);
				_collectFilletsToggle.transform.parent.gameObject.SetActive(employee.Role == StaffRole.Stocker);
				_collectFilletsToggle.SetIsOnWithoutNotify(employee.CollectFilletsEnabled);
				SetToggleLabel(_collectFilletsToggle, "tablet.staff.v1.collect_fillets");
				SetToggleLabel(_greenMarketToggle, _waitress ? "tablet.staff.v1.take_orders" : (_chef ? "tablet.staff.v1.prepare_orders" : (_prep ? "tablet.staff.v1.rice_cooker" : "tablet.staff.green_market")));
				SetToggleLabel(_blueMarketToggle, _waitress ? "tablet.staff.v1.serve_orders" : (_chef ? "tablet.staff.v1.stock_conveyor" : (_prep ? "tablet.staff.v1.fillet_machine" : "tablet.staff.blue_market")));
				_greenMarketToggle.interactable = _waitress || _chef || _prep || employee.BuyMissingStock;
				_blueMarketToggle.interactable = _waitress || _chef || _prep || employee.BuyMissingStock;
				SetLocalizedLabel(_stopStockingButtonText, (!employee.StockingStopped) ? ((_waitress || _chef || _prep) ? "tablet.staff.v1.stop_work" : "tablet.staff.stop_stocking") : ((_waitress || _chef || _prep) ? "tablet.staff.v1.resume_work" : "tablet.staff.resume_stocking"));
			}
			RebindEvents();
			if (_feedback != null)
			{
				_feedback.Bind(employee);
			}
		}

		private static void SetToggleLabel(Toggle toggle, string key)
		{
			SetLocalizedLabel(toggle.transform.parent.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true), key);
		}

		private static void SetLocalizedLabel(TextMeshProUGUI label, string key)
		{
			if (label == null)
			{
				return;
			}
			string text = ((!key.StartsWith("tablet.staff.v1.", StringComparison.Ordinal)) ? (key switch
			{
				"tablet.staff.green_market" => "Green Market", 
				"tablet.staff.blue_market" => "Blue Market", 
				"tablet.staff.stop_stocking" => "Stop Stocking", 
				"tablet.staff.resume_stocking" => "Resume Stocking", 
				_ => key, 
			}) : StaffText.Fallback(key.Substring("tablet.staff.v1.".Length)));
			string fallback = text;
			label.text = TabletLocalization.Get(key, fallback);
			LocalizeStringEvent component = label.GetComponent<LocalizeStringEvent>();
			if (component != null)
			{
				component.StringReference.SetReference("UiTable", key);
				if (component.isActiveAndEnabled)
				{
					component.RefreshString();
				}
			}
		}

		private void RebindEvents()
		{
			_fireButton.Clicked -= OnFireClicked;
			_fireButton.Clicked += OnFireClicked;
			_stopStockingButton.Clicked -= OnStopStockingClicked;
			_stopStockingButton.Clicked += OnStopStockingClicked;
			_buyMissingStockToggle.onValueChanged.RemoveListener(OnBuyMissingStockChanged);
			_buyMissingStockToggle.onValueChanged.AddListener(OnBuyMissingStockChanged);
			_greenMarketToggle.onValueChanged.RemoveListener(OnGreenMarketChanged);
			_greenMarketToggle.onValueChanged.AddListener(OnGreenMarketChanged);
			_blueMarketToggle.onValueChanged.RemoveListener(OnBlueMarketChanged);
			_blueMarketToggle.onValueChanged.AddListener(OnBlueMarketChanged);
			_collectFilletsToggle.onValueChanged.RemoveListener(OnCollectFilletsChanged);
			_collectFilletsToggle.onValueChanged.AddListener(OnCollectFilletsChanged);
		}

		private void OnDestroy()
		{
			if (_fireButton != null)
			{
				_fireButton.Clicked -= OnFireClicked;
			}
			if (_stopStockingButton != null)
			{
				_stopStockingButton.Clicked -= OnStopStockingClicked;
			}
			if (_buyMissingStockToggle != null)
			{
				_buyMissingStockToggle.onValueChanged.RemoveListener(OnBuyMissingStockChanged);
			}
			if (_greenMarketToggle != null)
			{
				_greenMarketToggle.onValueChanged.RemoveListener(OnGreenMarketChanged);
			}
			if (_blueMarketToggle != null)
			{
				_blueMarketToggle.onValueChanged.RemoveListener(OnBlueMarketChanged);
			}
			if (_collectFilletsToggle != null)
			{
				_collectFilletsToggle.onValueChanged.RemoveListener(OnCollectFilletsChanged);
			}
		}

		private void OnFireClicked()
		{
			_fireRequested?.Invoke(_employeeId);
		}

		private void OnCollectFilletsChanged(bool enabled)
		{
			_settingChanged?.Invoke(_employeeId, StockerSetting.CollectFillets, enabled);
		}

		private void OnStopStockingClicked()
		{
			_settingChanged?.Invoke(_employeeId, StockerSetting.StockingStopped, !_stockingStopped);
		}

		private void OnBuyMissingStockChanged(bool enabled)
		{
			_greenMarketToggle.interactable = enabled;
			_blueMarketToggle.interactable = enabled;
			_settingChanged?.Invoke(_employeeId, StockerSetting.BuyMissingStock, enabled);
		}

		private void OnGreenMarketChanged(bool enabled)
		{
			_settingChanged?.Invoke(_employeeId, _waitress ? StockerSetting.TakeOrders : (_chef ? StockerSetting.PrepareOrders : ((!_prep) ? StockerSetting.GreenMarket : StockerSetting.RiceCooker)), enabled);
		}

		private void OnBlueMarketChanged(bool enabled)
		{
			_settingChanged?.Invoke(_employeeId, _waitress ? StockerSetting.ServeOrders : (_chef ? StockerSetting.StockConveyor : (_prep ? StockerSetting.FilletMachine : StockerSetting.BlueMarket)), enabled);
		}
	}
}
