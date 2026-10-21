using PaperPixelGames.Core.Data;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Kitchen.Yaki3000Machine
{
	public class RecipeIngredientUI : NetworkBehaviour
	{
		[SerializeField]
		private Image _iconImage;

		[SerializeField]
		private TextMeshProUGUI _ingredientNameText;

		[SerializeField]
		private TextMeshProUGUI _currentAmountText;

		[SerializeField]
		private TextMeshProUGUI _requiredAmountText;

		[SerializeField]
		private Image _checkMarkImage;

		private Yaki3000Canvas _yaki3000StatusDisplay;

		private int _currentAmount;

		private int _requiredAmount;

		public FixedString64Bytes IngredientID { get; private set; }

		public void BindToYakiStatus(Yaki3000Canvas yaki3000StatusDisplay)
		{
			_yaki3000StatusDisplay = yaki3000StatusDisplay;
		}

		public void Initialize(FixedString64Bytes ingredientID, int requiredAmount)
		{
			IngredientID = ingredientID;
			_requiredAmount = requiredAmount;
			_requiredAmountText.text = requiredAmount.ToString();
			ItemSO itemSO = _yaki3000StatusDisplay.ItemDatabaseSO.FindById(ingredientID.ToString());
			if (itemSO is IngredientSO ingredientSO)
			{
				_iconImage.sprite = ingredientSO.Sprite;
				_ingredientNameText.text = ingredientSO.LocalizedName;
			}
			else if (itemSO is ProcessedIngredientSO processedIngredientSO)
			{
				_iconImage.sprite = processedIngredientSO.Sprite;
				_ingredientNameText.text = processedIngredientSO.LocalizedName;
			}
			UpdateAmount(0);
		}

		public void UpdateAmount(int currentAmount)
		{
			_currentAmount = currentAmount;
			_currentAmountText.text = currentAmount.ToString();
			if (_currentAmount >= _requiredAmount)
			{
				_checkMarkImage.enabled = true;
			}
			else
			{
				_checkMarkImage.enabled = false;
			}
		}

		public void ResetData()
		{
			_currentAmount = 0;
			_requiredAmount = 0;
			IngredientID = default;
			_iconImage.sprite = null;
			_ingredientNameText.text = string.Empty;
			_currentAmountText.text = "0";
			_requiredAmountText.text = "0";
			_checkMarkImage.enabled = false;
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			base.__initializeRpcs();
		}

		protected override string __getTypeName()
		{
			return "RecipeIngredientUI";
		}
	}
}
