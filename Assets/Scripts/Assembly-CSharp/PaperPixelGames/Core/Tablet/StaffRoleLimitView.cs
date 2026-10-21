using PaperPixelGames.Core.Staff;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Tablet
{
	public sealed class StaffRoleLimitView : MonoBehaviour
	{
		[SerializeField]
		private TextMeshProUGUI _roleText;

		[SerializeField]
		private TextMeshProUGUI _countText;

		[SerializeField]
		private Image _badge;

		public void Bind(StaffRoleDefinition definition, int currentCount, int maximumCount)
		{
			_roleText.text = TabletLocalization.GetStaffRoleName(definition.Role, definition.DisplayName);
			_countText.text = $"{currentCount} / {maximumCount}";
			Color color = StaffCardFeedback.RoleColor(definition.Role);
			_roleText.color = color;
			if (_badge != null)
			{
				_badge.color = Color.Lerp(Color.white, color, 0.16f);
			}
		}
	}
}
