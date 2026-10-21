using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Data;
using UnityEngine;

namespace PaperPixelGames.Core.Kitchen.ServedDishes
{
	[DisallowMultipleComponent]
	public sealed class CustomSushiIngredientVisuals : MonoBehaviour
	{
		[Serializable]
		public sealed class IngredientVisualBinding
		{
			public ProcessedIngredientSO Ingredient;

			public GameObject[] VisualRoots;
		}

		[Tooltip("Existing placeholder sushi graphics to hide for Custom Sushi. Do not assign the plate.")]
		[SerializeField]
		private GameObject[] _builtInOnlyVisualRoots;

		[Tooltip("Assign a processed ingredient asset and its visual objects. No runtime name lookup is used.")]
		[SerializeField]
		private List<IngredientVisualBinding> _ingredientVisuals = new List<IngredientVisualBinding>();

		public void Apply(SushiCompositionData composition, bool isCustomSushi)
		{
			HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
			if (isCustomSushi && composition?.IngredientIds != null)
			{
				foreach (string ingredientId in composition.IngredientIds)
				{
					if (!string.IsNullOrWhiteSpace(ingredientId))
					{
						hashSet.Add(ingredientId);
					}
				}
			}
			Dictionary<GameObject, bool> dictionary = new Dictionary<GameObject, bool>();
			if (_builtInOnlyVisualRoots != null)
			{
				GameObject[] builtInOnlyVisualRoots = _builtInOnlyVisualRoots;
				foreach (GameObject visual in builtInOnlyVisualRoots)
				{
					AddVisibility(dictionary, visual, !isCustomSushi);
				}
			}
			if (_ingredientVisuals != null)
			{
				foreach (IngredientVisualBinding ingredientVisual in _ingredientVisuals)
				{
					if (ingredientVisual?.VisualRoots != null)
					{
						bool show = isCustomSushi && ingredientVisual.Ingredient != null && hashSet.Contains(ingredientVisual.Ingredient.Id);
						GameObject[] builtInOnlyVisualRoots = ingredientVisual.VisualRoots;
						foreach (GameObject visual2 in builtInOnlyVisualRoots)
						{
							AddVisibility(dictionary, visual2, show);
						}
					}
				}
			}
			foreach (KeyValuePair<GameObject, bool> item in dictionary)
			{
				if (item.Key != null && item.Key.activeSelf != item.Value)
				{
					item.Key.SetActive(item.Value);
				}
			}
		}

		private void AddVisibility(Dictionary<GameObject, bool> visibility, GameObject visual, bool show)
		{
			if (!(visual == null) && !(visual == base.gameObject) && visual.transform.IsChildOf(base.transform))
			{
				visibility.TryGetValue(visual, out var value);
				visibility[visual] = value | show;
			}
		}
	}
}
