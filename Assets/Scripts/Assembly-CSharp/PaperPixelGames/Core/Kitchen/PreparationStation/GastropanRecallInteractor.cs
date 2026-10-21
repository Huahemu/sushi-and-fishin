using PaperPixelGames.Core.Data;
using TMPro;
using UnityEngine;

namespace PaperPixelGames.Core.Kitchen.PreparationStation
{
	public class GastropanRecallInteractor : PrepStationUIHandlerBase
	{
		[SerializeField]
		private int _gastroPanTargetPointsIndex;

		[Header("ToolTip")]
		[SerializeField]
		private GameObject _toolTipGO;

		[SerializeField]
		private TextMeshProUGUI _ingredientNameText;

		[SerializeField]
		private TextMeshProUGUI _ingredientCount;

		private ProcessedIngredientSO _processedIngredientSO;

		private bool _hasTargetObject;

		protected override bool RequiresValidStateForClick => false;

		public GameObject ToolTipGO => _toolTipGO;

		public bool HasTargetObject
		{
			get
			{
				return _hasTargetObject;
			}
			set
			{
				_hasTargetObject = value;
				if (!_hasTargetObject && _toolTipGO != null)
				{
					_toolTipGO.SetActive(value: false);
				}
			}
		}

		protected override void OnClick()
		{
			_gastroPanSpawner.ServerTransferPendingIngredientsToTargetPanRpc(_gastroPanTargetPointsIndex);
		}

		protected override void OnHoverEnter()
		{
			base.OnHoverEnter();
			if (HasTargetObject)
			{
				if (_toolTipGO != null)
				{
					_toolTipGO.SetActive(value: true);
				}
				_gastroPanSpawner.ClientHoverEnterTargetPan(_gastroPanTargetPointsIndex);
			}
		}

		protected override void OnHoverExit()
		{
			base.OnHoverExit();
			if (_toolTipGO != null)
			{
				_toolTipGO.SetActive(value: false);
			}
			if (HasTargetObject)
			{
				_gastroPanSpawner.ClientHoverExitTargetPan(_gastroPanTargetPointsIndex);
			}
		}

		public void ClientSetToolTipData(ProcessedIngredientSO processedIngredientSO, byte ingredientCount)
		{
			if (!(processedIngredientSO == null))
			{
				_processedIngredientSO = processedIngredientSO;
				if (_ingredientNameText != null)
				{
					_ingredientNameText.text = processedIngredientSO.LocalizedName;
				}
				if (_ingredientCount != null)
				{
					_ingredientCount.text = $"{ingredientCount}/{processedIngredientSO.MaxPanCapacity}";
				}
			}
		}

		public void ClientToolTipIngredientCountUpdate(byte ingredientCount)
		{
			if (!(_processedIngredientSO == null) && _ingredientCount != null)
			{
				_ingredientCount.text = $"{ingredientCount}/{_processedIngredientSO.MaxPanCapacity}";
			}
		}
	}
}
