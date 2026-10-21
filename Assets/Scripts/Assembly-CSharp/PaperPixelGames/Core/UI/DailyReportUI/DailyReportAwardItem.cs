using PaperPixelGames.Core.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperPixelGames.Core.UI.DailyReportUI
{
	public class DailyReportAwardItem : MonoBehaviour, IPointerEnterHandler, IEventSystemHandler, IPointerExitHandler
	{
		[SerializeField]
		private TextMeshProUGUI _titleName;

		[SerializeField]
		private Image _titleImage;

		[SerializeField]
		private GameObject _tooltipRoot;

		private void Awake()
		{
			SetTooltipVisible(visible: false);
		}

		private void OnDisable()
		{
			SetTooltipVisible(visible: false);
		}

		public void Initialize(AwardSO awardSO)
		{
			BindAward(awardSO);
		}

		public void InitializeBadge(AwardSO awardSO)
		{
			BindAward(awardSO);
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			SetTooltipVisible(visible: true);
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			SetTooltipVisible(visible: false);
		}

		private void BindAward(AwardSO awardSO)
		{
			if (_titleImage != null)
			{
				_titleImage.sprite = ((awardSO != null) ? awardSO.Sprite : null);
			}
			if (_titleName != null)
			{
				_titleName.text = ((awardSO != null) ? awardSO.Title : "-");
			}
		}

		private void SetTooltipVisible(bool visible)
		{
			if (_tooltipRoot != null)
			{
				_tooltipRoot.SetActive(visible);
			}
		}
	}
}
