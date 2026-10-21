using System;
using PaperPixelGames.Core.Staff;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Tablet
{
	public sealed class StaffCandidateCardView : MonoBehaviour
	{
		[SerializeField]
		private Image _portrait;

		[SerializeField]
		private TextMeshProUGUI _nameText;

		[SerializeField]
		private TextMeshProUGUI _roleText;

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
		private BaseButton _hireButton;

		[SerializeField]
		private TextMeshProUGUI _hireButtonText;

		[SerializeField]
		private StaffCardFeedback _feedback;

		private int _employeeId;

		private Action<int> _hireRequested;

		public void Bind(StaffEmployeeEntry employee, StaffRoleDefinition definition, bool roleFull, Action<int> hireRequested, int progressionLevel)
		{
			_employeeId = employee.EmployeeId;
			_hireRequested = hireRequested;
			_nameText.text = StaffNames.DisplayName(employee);
			_roleText.text = TabletLocalization.GetStaffRoleName(employee.Role, definition.DisplayName);
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
			bool flag = progressionLevel >= definition.UnlockLevel;
			_hireButton.interactable = flag && !roleFull && StaffRules.IsRoleEnabled(employee.Role);
			_hireButtonText.text = ((!StaffRules.IsRoleEnabled(employee.Role)) ? StaffText.Get("Unavailable") : ((!flag) ? StaffText.UnlockLevel(definition.UnlockLevel) : (roleFull ? TabletLocalization.Get("tablet.staff.role_full", "Role Full") : TabletLocalization.Get("tablet.staff.hire", "Hire"))));
			_hireButton.Clicked -= OnHireClicked;
			_hireButton.Clicked += OnHireClicked;
			if (_feedback != null)
			{
				_feedback.Bind(employee);
			}
		}

		private void OnDestroy()
		{
			if (_hireButton != null)
			{
				_hireButton.Clicked -= OnHireClicked;
			}
		}

		private void OnHireClicked()
		{
			_hireRequested?.Invoke(_employeeId);
		}
	}
}
