using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Restaurant;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Player.QuickSlot
{
	public class HeldSlotElement : MonoBehaviour
	{
		[SerializeField]
		private GameObject _visualParent;

		[SerializeField]
		private Image _iconImage;

		public BaseHoldable HeldItem { get; private set; }

		public bool IsEmpty => !_visualParent.activeSelf;

		private QuickSlotHandler _quickSlotHandler => Singleton<QuickSlotHandler>.Instance;

		public void Set(BaseHoldable item)
		{
			HeldItem = item;
			Set(item.QuickSlotDisplayName, item.QuickSlotDisplaySprite);
            SushiAndFish.FishingIntegration.FishingTablet.PaintHeld(item,_iconImage);
			TrayQuickSlotIconLayers trayQuickSlotIconLayers = _iconImage.GetComponent<TrayQuickSlotIconLayers>();
			if (item is Tray tray)
			{
				if ((object)trayQuickSlotIconLayers == null)
				{
					trayQuickSlotIconLayers = _iconImage.gameObject.AddComponent<TrayQuickSlotIconLayers>();
				}
				trayQuickSlotIconLayers.Set(tray);
			}
			else if (trayQuickSlotIconLayers != null)
			{
				trayQuickSlotIconLayers.Clear();
			}
		}

		public void Set(string name, Sprite sprite)
		{
			_quickSlotHandler.UpdateInfoText(name);
			_iconImage.sprite = sprite;_iconImage.material=null;_iconImage.useSpriteMesh=false;
			_visualParent.SetActive(value: true);
		}

		public void Clear()
		{
			_quickSlotHandler.UpdateInfoText("");
			HeldItem = null;
			TrayQuickSlotIconLayers component = _iconImage.GetComponent<TrayQuickSlotIconLayers>();
			if (component != null)
			{
				component.Clear();
			}
			_visualParent.SetActive(value: false);
		}

		public void RefreshDisplay()
		{
			if (!(HeldItem == null))
			{
				Set(HeldItem);
			}
		}

		public BaseHoldable ExtractItem()
		{
			BaseHoldable heldItem = HeldItem;
			Clear();
			return heldItem;
		}

		public void DropItem(Vector3 pos)
		{
			if (HeldItem != null)
			{
				HeldItem.Drop(pos, Quaternion.identity);
			}
			Clear();
		}

		public void DisableItem()
		{
			if (HeldItem != null)
			{
				HeldItem.Disable(clearQuickSlotOnOwner: false);
			}
			Clear();
		}

		public void DestroyItem()
		{
			if (HeldItem != null)
			{
				HeldItem.Destroy();
			}
			Clear();
		}
	}
}
