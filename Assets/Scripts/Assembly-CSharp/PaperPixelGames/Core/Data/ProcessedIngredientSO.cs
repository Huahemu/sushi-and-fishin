using PaperPixelGames.Core.Interaction;
using UnityEngine;

namespace PaperPixelGames.Core.Data
{
	[CreateAssetMenu(fileName = "ProcessedIngredient", menuName = "Data/ProcessedIngredientSO")]
	public class ProcessedIngredientSO : HoldableSO
	{
		private enum ProcessedIngredientAutoFillType
		{
			Nigiri = 0,
			LongStrip = 1
		}

		public IngredientType IngredientType;

		public ProcessedType ProcessedType;

		public byte OutputYield;

		public byte MaxPanCapacity;

		public float CustomSushiPlacementValue = 1f;

		public GameObject PanIngredientPrefab;

		[SerializeField]
		private ProcessedIngredientAutoFillType _autoFillType;

		[SerializeField]
		private bool _setActiveOnAutoFill = true;

		[SerializeField]
		private bool _clearRuntimePrefab = true;

		[SerializeField]
		private bool _clearPlacementAndGraphicPrefabs = true;

		private void OnValidate()
		{
			if (OutputYield < 1)
			{
				OutputYield = 1;
			}
			if (MaxPanCapacity < 1)
			{
				MaxPanCapacity = 1;
			}
		}
	}
}
