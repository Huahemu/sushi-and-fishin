using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Kitchen
{
	public class DishWashingStation : BaseUsable
	{
		public override void ClientUseEnter(Interactor interactor)
		{
			base.ClientUseEnter(interactor);
		}

		public override void ClientUseExit(Interactor interactor)
		{
			base.ClientUseExit(interactor);
		}

		public override void ServerUseEnter(Interactor interactor)
		{
			base.ServerUseEnter(interactor);
			if (base.NetworkObject.OwnerClientId != interactor.OwnerClientId)
			{
				base.NetworkObject.ChangeOwnership(interactor.OwnerClientId);
			}
		}

		public override void ServerUseExit(Interactor interactor)
		{
			base.ServerUseExit(interactor);
			base.NetworkObject.RemoveOwnership();
		}

		protected override bool CanCrossTrigger(Interactor interactor, BaseHoldable held)
		{
			bool num = held.ItemSO.ItemType == ItemType.Plate;
			UtilityMethods.Log(num ? "[DishWashingStation - CanCrossTrigger] islem basarili" : "[DishWashingStation - CanCrossTrigger] elindeki ürün buraya konulamaz");
			return num;
		}

		protected override void ClientCrossMouse1(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			base.ClientCrossMouse1(interactor, held, phase);
			Singleton<QuickSlotHandler>.Instance.DestroyCurrentItem();
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
			return "DishWashingStation";
		}
	}
}
