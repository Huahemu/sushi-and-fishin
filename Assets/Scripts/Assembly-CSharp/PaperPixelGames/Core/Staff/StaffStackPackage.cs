using Assets.Scripts.Core.ProcessedIngredient;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Kitchen.Wholasale;

namespace PaperPixelGames.Core.Staff
{
	public static class StaffStackPackage
	{
		public static bool IsPackage(BaseHoldable item)
		{
			if (!(item is WholesaleEntity))
			{
				return item is SideDishIngredientEntity;
			}
			return true;
		}

		public static HoldableSO Output(BaseHoldable item)
		{
			if (!(item is WholesaleEntity wholesaleEntity))
			{
				if (item is SideDishIngredientEntity sideDishIngredientEntity)
				{
					return sideDishIngredientEntity.StaffOutputItem;
				}
				return null;
			}
			return wholesaleEntity.WholesaleSO?.OutputItem;
		}

		public static int Capacity(BaseHoldable item)
		{
			if (!(item is WholesaleEntity wholesaleEntity))
			{
				if (!(item is SideDishIngredientEntity { StaffPackageQuantity: var staffPackageQuantity }))
				{
					return 0;
				}
				return staffPackageQuantity;
			}
			return (wholesaleEntity.WholesaleSO != null) ? wholesaleEntity.WholesaleSO.Quantity : 0;
		}

		public static bool Matches(BaseHoldable a, BaseHoldable b)
		{
			if (IsPackage(a) && IsPackage(b) && a.ItemSO != null && b.ItemSO != null)
			{
				return StaffWorkplaceRegistry.StackType(a) == StaffWorkplaceRegistry.StackType(b);
			}
			return false;
		}

		public static bool ServerCanAccessContents(BaseHoldable item, Interactor interactor)
		{
			if (item == null || !item.IsServer || !item.IsSpawned || interactor == null || !item.CanHold.Value || item.IsStaffCarried || item.IsPlayerInventoryOwnedForPersistence)
			{
				return false;
			}
			if (item.StaffStackPoint < 0)
			{
				return true;
			}
			float num = interactor.InteractRange + 2f;
			if ((interactor.transform.position - item.transform.position).sqrMagnitude <= num * num)
			{
				StaffManager instance = StaffManager.Instance;
				if ((object)instance == null)
				{
					return true;
				}
				return !instance.IsStackItemReserved(item.NetworkObjectId);
			}
			return false;
		}
	}
}
