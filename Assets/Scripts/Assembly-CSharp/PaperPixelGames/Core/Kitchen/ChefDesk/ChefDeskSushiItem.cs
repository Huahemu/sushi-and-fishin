using PaperPixelGames.Core.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Kitchen.ChefDesk
{
	public class ChefDeskSushiItem : MonoBehaviour, IPointerEnterHandler, IEventSystemHandler, IPointerExitHandler, IPointerClickHandler
	{
		[SerializeField]
		private Image _sushiImage;

		[SerializeField]
		private TextMeshProUGUI _sushiNameText;

		[Header("Borders")]
		[SerializeField]
		private Image _hoverBorder;

		[SerializeField]
		private Image _selectedBorder;

		private ChefDeskBookCanvas _chefDeskBook;

		public SushiRecipeDefinition Recipe { get; private set; }

		public SushiSO SushiSO => Recipe?.BuiltInSushi;

		public string RecipeId => Recipe?.RecipeId ?? string.Empty;

		public bool IsSelected { get; private set; }

		public void Prepare(SushiSO sushiSO, ChefDeskBookCanvas chefDeskBook)
		{
			Prepare(SushiRecipeResolver.CreateBuiltInDefinition(sushiSO), chefDeskBook);
		}

		public void Prepare(SushiRecipeDefinition recipe, ChefDeskBookCanvas chefDeskBook)
		{
			Recipe = recipe;
			_chefDeskBook = chefDeskBook;
			IsSelected = false;
			_sushiImage.sprite = Recipe?.Icon;
			_sushiImage.enabled = Recipe?.Icon != null;
			_sushiNameText.text = GetDisplayName();
			RefreshBorders(hover: false);
		}

		public void SetSelected(bool selected)
		{
			IsSelected = selected;
			RefreshBorders(hover: false);
		}

		public void Initialize()
		{
			if (Recipe != null && !(_chefDeskBook == null))
			{
				_chefDeskBook.PopulateRecipeIngredientUI(Recipe);
				_chefDeskBook.SelectedSushiNameText.text = GetDisplayName();
				_chefDeskBook.SelectedSushiImage.sprite = Recipe.Icon;
				_chefDeskBook.SelectedSushiImage.enabled = Recipe.Icon != null;
				SetSelected(selected: true);
			}
		}

		public void RefreshLocalization()
		{
			if (Recipe != null)
			{
				if (_sushiNameText != null)
				{
					_sushiNameText.text = GetDisplayName();
				}
				if (IsSelected && _chefDeskBook != null && _chefDeskBook.SelectedSushiNameText != null)
				{
					_chefDeskBook.SelectedSushiNameText.text = GetDisplayName();
				}
			}
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			RefreshBorders(hover: true);
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			RefreshBorders(hover: false);
		}

		public void OnPointerClick(PointerEventData eventData)
		{
			if (Recipe != null && !(_chefDeskBook == null))
			{
				_chefDeskBook.ClientSelectSushiPreview(Recipe.RecipeId);
			}
		}

		public void BorderChange(bool defaultBorder = true, bool hoverBorder = false, bool selectedBorder = false)
		{
			if (selectedBorder)
			{
				SetSelected(selected: true);
				return;
			}
			IsSelected = false;
			_hoverBorder.enabled = hoverBorder;
			_selectedBorder.enabled = false;
		}

		private void RefreshBorders(bool hover)
		{
			if (IsSelected)
			{
				_hoverBorder.enabled = false;
				_selectedBorder.enabled = true;
			}
			else
			{
				_hoverBorder.enabled = hover;
				_selectedBorder.enabled = false;
			}
		}

		private string GetDisplayName()
		{
			object obj;
			if (!(Recipe?.BuiltInSushi != null))
			{
				obj = Recipe?.DisplayName;
				if (obj == null)
				{
					return string.Empty;
				}
			}
			else
			{
				obj = Recipe.BuiltInSushi.LocalizedName;
			}
			return (string)obj;
		}
	}
}
