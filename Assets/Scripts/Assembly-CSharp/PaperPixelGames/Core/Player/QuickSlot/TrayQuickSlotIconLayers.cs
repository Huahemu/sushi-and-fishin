using PaperPixelGames.Core.Restaurant;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Player.QuickSlot
{
	public sealed class TrayQuickSlotIconLayers : MonoBehaviour
	{
		private Image _mainDish;

		private Image _sideDish;

		private Image _beverage;

		public void Set(Tray tray)
		{
			EnsureLayers();
			tray.TryGetQuickSlotContentSprites(out var mainDishSprite, out var sideDishSprite, out var beverageSprite);
			SetLayer(_mainDish, mainDishSprite);
			SetLayer(_sideDish, sideDishSprite);
			SetLayer(_beverage, beverageSprite);
		}

		public void Clear()
		{
			if (_mainDish != null)
			{
				_mainDish.gameObject.SetActive(value: false);
			}
			if (_sideDish != null)
			{
				_sideDish.gameObject.SetActive(value: false);
			}
			if (_beverage != null)
			{
				_beverage.gameObject.SetActive(value: false);
			}
		}

		private void EnsureLayers()
		{
			if ((object)_mainDish == null)
			{
				_mainDish = CreateLayer("TrayMainDish", new Vector2(0.5f, 0.58f), new Vector2(0.58f, 0.58f));
			}
			if ((object)_sideDish == null)
			{
				_sideDish = CreateLayer("TraySideDish", new Vector2(0.25f, 0.23f), new Vector2(0.38f, 0.38f));
			}
			if ((object)_beverage == null)
			{
				_beverage = CreateLayer("TrayBeverage", new Vector2(0.76f, 0.25f), new Vector2(0.34f, 0.42f));
			}
		}

		private Image CreateLayer(string layerName, Vector2 anchor, Vector2 size)
		{
			GameObject gameObject = new GameObject(layerName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
			gameObject.transform.SetParent(base.transform, worldPositionStays: false);
			RectTransform obj = (RectTransform)gameObject.transform;
			obj.anchorMin = anchor;
			obj.anchorMax = anchor;
			obj.pivot = new Vector2(0.5f, 0.5f);
			obj.sizeDelta = size * 100f;
			Image component = gameObject.GetComponent<Image>();
			component.raycastTarget = false;
			component.preserveAspect = true;
			gameObject.SetActive(value: false);
			return component;
		}

		private static void SetLayer(Image image, Sprite sprite)
		{
			image.sprite = sprite;
			image.gameObject.SetActive(sprite != null);
		}
	}
}
