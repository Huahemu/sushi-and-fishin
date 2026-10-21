using System;
using Unity.Collections;
using UnityEngine;

namespace PaperPixelGames.Core.Kitchen.ChefDesk
{
	[Serializable]
	public struct TableObjectData
	{
		public FixedString64Bytes IngredientId;

		public GameObject Obj;

		public TableObjectData(FixedString64Bytes ingredientId, GameObject obj)
		{
			IngredientId = ingredientId;
			Obj = obj;
		}
	}
}
