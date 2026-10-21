using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.UI.DailyReportUI
{
	public class DiveLogCatchedItem : MonoBehaviour
	{
		[SerializeField]
		private TextMeshProUGUI _itemAmount;

		[SerializeField]
		private Image _itemImage;

		public void Initialize(Sprite itemSprite, int amount)
		{
			if (_itemAmount != null)
			{
				_itemAmount.text = amount.ToString();
			}
			if (!(_itemImage == null))
			{
				_itemImage.sprite = itemSprite;
				_itemImage.enabled = itemSprite != null;
			}
		}
	}
}
