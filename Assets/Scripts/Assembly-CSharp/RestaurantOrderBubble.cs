using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Restaurant;
using PaperPixelGames.Framework.Enums;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public sealed class RestaurantOrderBubble : MonoBehaviour
{
	public enum DisplayState
	{
		Hidden = 0,
		Ordering = 1,
		WaitingForFood = 2
	}

	[Header("Root")]
	[Tooltip("The complete bubble canvas/root to toggle. This can be this object's Canvas.")]
	[SerializeField]
	private GameObject bubbleCanvas;

	[Header("Order UI")]
	[Tooltip("Image Type = Filled olmalı. Fill Method/Origin ayarını Inspector'dan Vertical yapabilirsin.")]
	[SerializeField]
	private Image timeFillImage;

	[SerializeField]
	private Image sushiImage;

	[SerializeField]
	private GameObject plusIcon;

	[SerializeField]
	private Image sideDishImage;

	[SerializeField]
	private Image drinkImage;

	[Header("Localized Item Names")]
	[Tooltip("Açıksa sushi, side dish ve içecek isimleri aktif oyun dilinde gösterilir.")]
	[SerializeField]
	private bool showItemNames = true;

	[SerializeField]
	private TextMeshProUGUI sushiNameText;

	[SerializeField]
	private TextMeshProUGUI sideDishNameText;

	[SerializeField]
	private TextMeshProUGUI drinkNameText;

	[Header("Timer")]
	[Tooltip("When enabled, the timer uses unscaled time and automatically hides the bubble at zero.")]
	[SerializeField]
	private bool runTimerAutomatically = true;

	[SerializeField]
	private bool hideWhenTimeExpires = true;

	[Header("Timer Fill Color")]
	[SerializeField]
	private bool useTimerColorTransition = true;

	[SerializeField]
	private Color fullTimeColor = new Color(0.18f, 0.82f, 0.56f, 1f);

	[SerializeField]
	private Color halfTimeColor = new Color(1f, 0.69f, 0.2f, 1f);

	[SerializeField]
	private Color lowTimeColor = new Color(0.96f, 0.3f, 0.38f, 1f);

	[Header("Automatic Restaurant Binding")]
	[Tooltip("BaseResident üzerindeyken siparişi ve masa durumunu otomatik okur. Özel kullanımda kapatıp public Show... metotlarını çağırabilirsin.")]
	[SerializeField]
	private bool followResidentRestaurantState = true;

	private ServedDishSO _sushi;

	private string _customSushiName = string.Empty;

	private ServedDishSO _sideDish;

	private ServedDishSO _drink;

	private ResidentController _resident;

	private RestaurantTable _table;

	private int _cachedTableId = -1;

	private RestaurantTableState _lastTableState;

	public DisplayState State { get; private set; }

	public float RemainingTime { get; private set; }

	public float TotalTime { get; private set; }

	public bool IsVisible => State != DisplayState.Hidden;

	private void Awake()
	{
		HideImmediate();
	}

	private void Update()
	{
		if (followResidentRestaurantState)
		{
			RefreshFromResidentRestaurantState();
		}
		if (IsVisible)
		{
			RefreshItemNameTexts();
		}
		if (runTimerAutomatically && State != DisplayState.Hidden && !(TotalTime <= 0f))
		{
			SetRemainingTime(RemainingTime - Time.unscaledDeltaTime);
			if (RemainingTime <= 0f && hideWhenTimeExpires)
			{
				Hide();
			}
		}
	}

	public void ShowOrdering(Sprite sushi, Sprite sideDish, Sprite drink, float duration)
	{
		Show(DisplayState.Ordering, sushi, sideDish, drink, duration);
	}

	public void ShowOrderingTimerOnly(float duration)
	{
		State = DisplayState.Ordering;
		ClearCachedItems();
		SetImage(sushiImage, null);
		SetImage(sideDishImage, null);
		SetImage(drinkImage, null);
		if (plusIcon != null)
		{
			plusIcon.SetActive(value: false);
		}
		SetBubbleVisible(visible: true);
		SetRemainingTime(Mathf.Max(0f, duration), Mathf.Max(0f, duration));
		if (duration <= 0f && hideWhenTimeExpires)
		{
			Hide();
		}
	}

	public void ShowWaitingForFood(Sprite sushi, Sprite sideDish, Sprite drink, float duration)
	{
		Show(DisplayState.WaitingForFood, sushi, sideDish, drink, duration);
	}

	public void ShowWaitingForFood(ServedDishSO sushi, ServedDishSO sideDish, ServedDishSO drink, float duration)
	{
		ShowWaitingForFood((sushi != null) ? sushi.Sprite : null, (sideDish != null) ? sideDish.Sprite : null, (drink != null) ? drink.Sprite : null, duration);
		_sushi = sushi;
		_sideDish = sideDish;
		_drink = drink;
		RefreshItemNameTexts();
	}

	public void ShowWaitingForFood(SushiRecipeDefinition sushi, ServedDishSO sideDish, ServedDishSO drink, float duration)
	{
		ShowWaitingForFood(sushi?.Icon, (sideDish != null) ? sideDish.Sprite : null, (drink != null) ? drink.Sprite : null, duration);
		_sushi = sushi?.BuiltInSushi;
		_customSushiName = ((!(sushi?.BuiltInSushi == null)) ? string.Empty : (sushi?.DisplayName ?? string.Empty));
		_sideDish = sideDish;
		_drink = drink;
		RefreshItemNameTexts();
	}

	public void SetShowItemNames(bool value)
	{
		showItemNames = value;
		RefreshItemNameTexts();
	}

	public void SetState(DisplayState state)
	{
		if (state == DisplayState.Hidden)
		{
			Hide();
			return;
		}
		State = state;
		SetBubbleVisible(visible: true);
	}

	public void SetRemainingTime(float remainingTime, float totalTime = -1f)
	{
		if (totalTime >= 0f)
		{
			TotalTime = Mathf.Max(0f, totalTime);
		}
		RemainingTime = Mathf.Clamp(remainingTime, 0f, TotalTime);
		UpdateTimerFillVisual((TotalTime > 0f) ? (RemainingTime / TotalTime) : 0f);
	}

	public void Hide()
	{
		State = DisplayState.Hidden;
		ClearCachedItems();
		RemainingTime = 0f;
		TotalTime = 0f;
		if (timeFillImage != null)
		{
			timeFillImage.fillAmount = 0f;
			UpdateTimerFillColor(0f);
		}
		SetBubbleVisible(visible: false);
	}

	private void Show(DisplayState state, Sprite sushi, Sprite sideDish, Sprite drink, float duration)
	{
		State = state;
		ClearCachedItems();
		SetImage(sushiImage, sushi);
		SetImage(sideDishImage, sideDish);
		SetImage(drinkImage, drink);
		bool active = sideDish != null || drink != null;
		if (plusIcon != null)
		{
			plusIcon.SetActive(active);
		}
		SetBubbleVisible(visible: true);
		SetRemainingTime(Mathf.Max(0f, duration), Mathf.Max(0f, duration));
		if (duration <= 0f && hideWhenTimeExpires)
		{
			Hide();
		}
	}

	private void HideImmediate()
	{
		State = DisplayState.Hidden;
		ClearCachedItems();
		RemainingTime = 0f;
		TotalTime = 0f;
		if (timeFillImage != null)
		{
			timeFillImage.fillAmount = 0f;
			UpdateTimerFillColor(0f);
		}
		if (plusIcon != null)
		{
			plusIcon.SetActive(value: false);
		}
		SetImage(sushiImage, null);
		SetImage(sideDishImage, null);
		SetImage(drinkImage, null);
		SetBubbleVisible(visible: false);
	}

	private static void SetImage(Image image, Sprite sprite)
	{
		if (!(image == null))
		{
			image.sprite = sprite;
			image.enabled = sprite != null;
		}
	}

	private void UpdateTimerFillVisual(float normalizedTimeRemaining)
	{
		if (!(timeFillImage == null))
		{
			float num = Mathf.Clamp01(normalizedTimeRemaining);
			timeFillImage.fillAmount = num;
			UpdateTimerFillColor(num);
		}
	}

	private void UpdateTimerFillColor(float normalizedTimeRemaining)
	{
		if (useTimerColorTransition && !(timeFillImage == null))
		{
			float num = Mathf.Clamp01(normalizedTimeRemaining);
			Color color;
			if (num >= 0.5f)
			{
				float t = Mathf.SmoothStep(0f, 1f, (num - 0.5f) * 2f);
				color = Color.Lerp(halfTimeColor, fullTimeColor, t);
			}
			else
			{
				float t2 = Mathf.SmoothStep(0f, 1f, num * 2f);
				color = Color.Lerp(lowTimeColor, halfTimeColor, t2);
			}
			timeFillImage.color = color;
		}
	}

	private void ClearCachedItems()
	{
		_sushi = null;
		_customSushiName = string.Empty;
		_sideDish = null;
		_drink = null;
		RefreshItemNameTexts();
	}

	private void RefreshItemNameTexts()
	{
		if (!string.IsNullOrWhiteSpace(_customSushiName))
		{
			if (sushiNameText != null)
			{
				sushiNameText.gameObject.SetActive(showItemNames);
				sushiNameText.text = _customSushiName;
			}
		}
		else
		{
			SetItemNameText(sushiNameText, _sushi);
		}
		SetItemNameText(sideDishNameText, _sideDish);
		SetItemNameText(drinkNameText, _drink);
	}

	private void SetItemNameText(TextMeshProUGUI label, ServedDishSO item)
	{
		if (!(label == null))
		{
			bool flag = showItemNames && item != null;
			label.gameObject.SetActive(flag);
			if (flag)
			{
				label.text = item.LocalizedName;
			}
		}
	}

	private void SetBubbleVisible(bool visible)
	{
		if (bubbleCanvas != null)
		{
			bubbleCanvas.SetActive(visible);
		}
		else
		{
			base.gameObject.SetActive(visible);
		}
	}

	private void RefreshFromResidentRestaurantState()
	{
		if (_resident == null)
		{
			_resident = GetComponent<ResidentController>();
		}
		ResidentRuntimeState residentRuntimeState = ((_resident != null) ? _resident.Runtime : null);
		if (residentRuntimeState == null || !residentRuntimeState.HasArrived || !residentRuntimeState.IsInRestaurant || residentRuntimeState.RestaurantTargetType != ResidentRestaurantTargetType.DiningTable || residentRuntimeState.RestaurantTableId < 0 || residentRuntimeState.RestaurantSeatIndex < 0)
		{
			if (IsVisible)
			{
				Hide();
			}
			return;
		}
		if (_table == null || _cachedTableId != residentRuntimeState.RestaurantTableId)
		{
			_table = FindTable(residentRuntimeState.RestaurantTableId);
			_cachedTableId = residentRuntimeState.RestaurantTableId;
			_lastTableState = RestaurantTableState.Empty;
		}
		if (_table == null || (_table.CurrentState != RestaurantTableState.TakingOrder && _table.CurrentState != RestaurantTableState.DeliveringOrder))
		{
			if (IsVisible)
			{
				Hide();
			}
			return;
		}
		double activeOrderExpireServerTime = _table.ActiveOrderExpireServerTime;
		double num = ((NetworkManager.Singleton != null) ? NetworkManager.Singleton.ServerTime.Time : Time.timeAsDouble);
		float num2 = Mathf.Max(0f, (float)(activeOrderExpireServerTime - num));
		float activeOrderTimerDurationSeconds = _table.ActiveOrderTimerDurationSeconds;
		if (activeOrderExpireServerTime <= 0.0 || activeOrderTimerDurationSeconds <= 0f || num2 <= 0f)
		{
			if (IsVisible)
			{
				Hide();
			}
			return;
		}
		RestaurantTableState currentState = _table.CurrentState;
		if (currentState == RestaurantTableState.TakingOrder)
		{
			if (!IsVisible || _lastTableState != currentState)
			{
				ShowOrderingTimerOnly(activeOrderTimerDurationSeconds);
				_lastTableState = currentState;
			}
			SetRemainingTime(num2, activeOrderTimerDurationSeconds);
			return;
		}
		if (!_table.TryGetSeatOrder(residentRuntimeState.RestaurantSeatIndex, out var order) || order == null || !order.HasMainDish)
		{
			if (IsVisible)
			{
				Hide();
			}
			return;
		}
		if (!IsVisible || _lastTableState != currentState)
		{
			if (order.MainDishRecipe != null)
			{
				ShowWaitingForFood(order.MainDishRecipe, order.SideDishSO, order.BeverageSO, activeOrderTimerDurationSeconds);
			}
			else
			{
				ShowWaitingForFood(order.MainDishSO, order.SideDishSO, order.BeverageSO, activeOrderTimerDurationSeconds);
			}
			_lastTableState = currentState;
		}
		SetRemainingTime(num2, activeOrderTimerDurationSeconds);
	}

	private static RestaurantTable FindTable(int tableId)
	{
		RestaurantTable[] array = Object.FindObjectsOfType<RestaurantTable>();
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i] != null && array[i].Id == tableId)
			{
				return array[i];
			}
		}
		return null;
	}
}
