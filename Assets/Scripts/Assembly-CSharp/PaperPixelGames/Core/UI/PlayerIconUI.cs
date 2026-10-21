using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.UI
{
	public class PlayerIconUI : MonoBehaviour
	{
		[SerializeField]
		private TextMeshProUGUI infoText;

		[SerializeField]
		private Image _iconImage;

		[SerializeField]
		private RectTransform _directionalArrow;

		public void Initialize(string info)
		{
			infoText.text = info;
		}

		public void SetInformationText(string info)
		{
			infoText.text = info;
		}

		public Image GetImage()
		{
			return _iconImage;
		}

		public RectTransform GetDirectionalArrow()
		{
			return _directionalArrow;
		}
	}
}
