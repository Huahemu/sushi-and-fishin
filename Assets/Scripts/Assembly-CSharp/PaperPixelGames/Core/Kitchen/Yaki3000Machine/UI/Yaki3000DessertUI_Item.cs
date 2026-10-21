using System;
using PaperPixelGames.Core.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Kitchen.Yaki3000Machine.UI
{
	public class Yaki3000DessertUI_Item : MonoBehaviour, IPointerEnterHandler, IEventSystemHandler, IPointerExitHandler, IPointerClickHandler
	{
		public Action<byte> OnClicked;

		[SerializeField]
		private Image _dessertImage;

		[SerializeField]
		private TextMeshProUGUI _dessertNameText;

		[SerializeField]
		private Image _selectedBorder;

		[SerializeField]
		private Image _defaultBorder;

		[SerializeField]
		private Image _hoverBorder;

		public Image SelectedBorder => _selectedBorder;

		public Image DefaultBorder => _defaultBorder;

		public Yaki3000IngredientSO DessertSO { get; private set; }

		public Yaki3000Product Yaki3000Product
		{
			get
			{
				if (DessertSO == null)
				{
					return Yaki3000Product.None;
				}
				return DessertSO.Yaki3000ProductType;
			}
		}

		private void Start()
		{
			_defaultBorder.enabled = true;
			_selectedBorder.enabled = false;
			_hoverBorder.enabled = false;
		}

		public void Initiazlie(Yaki3000IngredientSO dessertSO)
		{
			DessertSO = dessertSO;
			_dessertImage.sprite = dessertSO.Sprite;
			_dessertNameText.text = dessertSO.LocalizedName;
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			_hoverBorder.enabled = true;
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			_hoverBorder.enabled = false;
		}

		public void OnPointerClick(PointerEventData eventData)
		{
			Yaki3000Product obj = ((!(DessertSO == null)) ? DessertSO.Yaki3000ProductType : Yaki3000Product.None);
			OnClicked?.Invoke((byte)obj);
			_selectedBorder.enabled = true;
			_defaultBorder.enabled = false;
		}
	}
}
