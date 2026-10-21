using PaperPixelGames.Core.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Kitchen.ChefDesk
{
	public class ChefDeskIngredientItem : MonoBehaviour
	{
		[SerializeField]
		private TextMeshProUGUI _ingredientName;

		[SerializeField]
		private TextMeshProUGUI _countText;

		[SerializeField]
		private Image _ingredientAvailable;

		private ChefDesk _chefDesk;

		private BaseIngredientNeed _baseIngredientNeed;

		private ItemSO _ingredient;

		private string _ingredientId = string.Empty;

		private int _requiredAmount;

		private void OnEnable()
		{
			RefreshLocalization();
			RefreshAvailability();
		}

		public void Init(BaseIngredientNeed baseIngredientNeed, ChefDesk chefDesk)
		{
			if (baseIngredientNeed != null)
			{
				_baseIngredientNeed = baseIngredientNeed;
				_chefDesk = chefDesk;
				_ingredient = chefDesk?.ItemDatabaseSO?.FindById(baseIngredientNeed.IngredientId);
				_ingredientId = baseIngredientNeed.IngredientId;
				_requiredAmount = Mathf.Max(0, baseIngredientNeed.RequiredAmount);
				RefreshLocalization();
				RefreshCountText();
				RefreshAvailability();
			}
		}

		public void SetCount(int count)
		{
			_requiredAmount = Mathf.Max(0, count);
			RefreshCountText();
			RefreshAvailability();
		}

		public void Init(string ingredientId, int requiredAmount, ChefDesk chefDesk)
		{
			_baseIngredientNeed = null;
			_chefDesk = chefDesk;
			_ingredientId = ingredientId ?? string.Empty;
			_ingredient = chefDesk?.ItemDatabaseSO?.FindById(_ingredientId);
			_requiredAmount = Mathf.Max(0, requiredAmount);
			RefreshLocalization();
			RefreshCountText();
			RefreshAvailability();
		}

		public void RefreshLocalization()
		{
			if (_ingredientName != null)
			{
				_ingredientName.text = ((_ingredient != null) ? _ingredient.LocalizedName : (_baseIngredientNeed?.LocalizedIngredientName ?? _ingredientId));
			}
		}

		public void RefreshAvailability()
		{
			if (!string.IsNullOrWhiteSpace(_ingredientId))
			{
				if (_chefDesk == null)
				{
					UpdateIngredientStatusUI(iconAvailable: false);
					return;
				}
				bool iconAvailable = _chefDesk.ClientGetIngredientAmountInGastroPans(_ingredientId) >= _requiredAmount;
				UpdateIngredientStatusUI(iconAvailable);
			}
		}

		private void RefreshCountText()
		{
			_countText.text = $"x{_requiredAmount}";
		}

		private void UpdateIngredientStatusUI(bool iconAvailable)
		{
			_ingredientAvailable.enabled = iconAvailable;
		}
	}
}
