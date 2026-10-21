using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Economy;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;

namespace PaperPixelGames.Core.Staff
{
	public static class StaffGameplayApi
	{
		public static bool TryPurchase(StaffEmployeeEntry employee, WholesaleSO product, bool blue, StaffWorkplaceRegistry world, out BaseHoldable item, out double xp, out StaffStatus failure)
		{
			item = null;
			xp = 0.0;
			failure = StaffStatus.PurchaseFailed;
			StaffManager instance = StaffManager.Instance;
			EconomyHandler economyHandler = Singleton<GameProgressManager>.Instance?.EconomyHandler;
			if (instance == null || !instance.IsServer || economyHandler == null || product == null || world == null)
			{
				return false;
			}
			if (!world.MarketOpen(blue))
			{
				failure = StaffStatus.MarketClosed;
				return false;
			}
			bool flag = false;
			foreach (WholesaleSO item2 in world.Products(blue))
			{
				if (item2 == product)
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				failure = StaffStatus.ProductUnavailable;
				return false;
			}
			if (!Singleton<GameProgressManager>.Instance.IsWholesaleProgressionUnlocked(product))
			{
				failure = StaffStatus.ProductsLocked;
				return false;
			}
			int num = StaffStockPolicy.PackagePrice(product);
			if (num <= 0)
			{
				return false;
			}
			if (economyHandler.ShopBalance.Value < num)
			{
				failure = StaffStatus.InsufficientMoney;
				return false;
			}
			Transform transform = (blue ? world.BlueMarketPoint : world.GreenMarketPoint);
			if (transform == null)
			{
				failure = StaffStatus.MissingWorkplace;
				return false;
			}
			if (product.Prefab == null)
			{
				return false;
			}
			if (!product.Prefab.TryInstantiateAndSpawn(out var instance2, transform.position, Quaternion.identity))
			{
				return false;
			}
			if (instance2 == null || !instance2.TryGetComponent<BaseHoldable>(out item) || !item.ServerTryStaffPickup(employee.EmployeeId))
			{
				if (instance2 != null && instance2.IsSpawned)
				{
					instance2.Despawn();
				}
				item = null;
				return false;
			}
			if (!economyHandler.ServerTryStaffSpend(num, purchase: true, out xp))
			{
				instance2.Despawn();
				item = null;
				failure = StaffStatus.InsufficientMoney;
				return false;
			}
			instance.Ledger.Purchase(employee, num, xp);
			StaffQuestProgress.Purchased(product, world.Database);
			failure = StaffStatus.Waiting;
			return true;
		}
	}
}
