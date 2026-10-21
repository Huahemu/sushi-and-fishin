using PaperPixelGames.Core.Interaction.ToolTip;
using UnityEngine;

namespace PaperPixelGames.Core.ProcessedIngredient
{
	public class DispenserIngredientEntity : IngredientEntity
	{
		[SerializeField]
		private byte _maxEntityCount;

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			if (base.IsServer)
			{
				base.IngredientCount = _maxEntityCount;
			}
		}

		public void ServerDispense(byte amount = 1)
		{
			if (ServerCanDispense(amount))
			{
				base.IngredientCount = (byte)Mathf.Max(0, base.IngredientCount - amount);
			}
		}

		public bool ServerCanDispense(byte amount = 1)
		{
			if (base.IsServer && amount > 0)
			{
				return base.IngredientCount >= amount;
			}
			return false;
		}

		public override TooltipData GetTooltip()
		{
			TooltipData data = base.ItemSO.Tooltip.Data;
			data.Title = base.ItemSO.LocalizedName;
			data.Body = $"{base.IngredientCount}/{_maxEntityCount}";
			return data;
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
			return "DispenserIngredientEntity";
		}
	}
}
