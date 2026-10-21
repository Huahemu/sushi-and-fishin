using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Tablet
{
	public class CustomerOrderFoodItem : MonoBehaviour
	{
		[SerializeField]
		private Image _foodImage;

		[SerializeField]
		private TextMeshProUGUI _foodName;

		[SerializeField]
		private Image _completeIcon;

		[SerializeField]
		private Image _incompleteIcon;

		public void Initialize(Sprite foodSprite, string foodName, int remainingCount, bool showMissingIcon)
		{
			bool flag = remainingCount <= 0;
			if (_foodImage != null)
			{
				_foodImage.sprite = foodSprite;
				_foodImage.enabled = foodSprite != null;
			}
			if (_foodName != null)
			{
				_foodName.text = foodName;
			}
			if (_completeIcon != null)
			{
				_completeIcon.enabled = flag;
			}
			if (_incompleteIcon != null)
			{
				_incompleteIcon.enabled = !flag & showMissingIcon;
			}
		}
	}
}
