using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Michsky.MUIP
{
	public class DropdownMultiSelect : MonoBehaviour, IPointerExitHandler, IEventSystemHandler, IPointerClickHandler
	{
		[Serializable]
		public class ToggleEvent : UnityEvent<bool>
		{
		}

		public enum AnimationType
		{
			Modular = 0,
			Stylish = 1
		}

		[Serializable]
		public class Item
		{
			public string itemName = "Dropdown Item";

			public bool isOn;

			[HideInInspector]
			public int itemIndex;

			[SerializeField]
			public ToggleEvent onValueChanged = new ToggleEvent();
		}

		public GameObject triggerObject;

		public Transform itemParent;

		public GameObject itemObject;

		public GameObject scrollbar;

		private VerticalLayoutGroup itemList;

		private Transform currentListParent;

		public Transform listParent;

		private Animator dropdownAnimator;

		public TextMeshProUGUI setItemText;

		public CanvasGroup contentCG;

		public bool isInteractable = true;

		public bool initAtStart = true;

		public bool enableIcon = true;

		public bool enableTrigger = true;

		public bool enableScrollbar = true;

		public bool setHighPriority = true;

		public bool outOnPointerExit;

		public bool isListItem;

		public bool invokeAtStart;

		[Range(1f, 50f)]
		public int itemPaddingTop = 8;

		[Range(1f, 50f)]
		public int itemPaddingBottom = 8;

		[Range(1f, 50f)]
		public int itemPaddingLeft = 8;

		[Range(1f, 50f)]
		public int itemPaddingRight = 25;

		[Range(1f, 50f)]
		public int itemSpacing = 8;

		public AnimationType animationType;

		[Range(1f, 25f)]
		public float transitionSmoothness = 10f;

		[Range(1f, 25f)]
		public float sizeSmoothness = 15f;

		public float panelSize = 200f;

		public RectTransform listRect;

		public CanvasGroup listCG;

		private bool isInTransition;

		private float closeOn;

		[SerializeField]
		public List<Item> items = new List<Item>();

		private bool isInitialized;

		private int currentIndex;

		private Toggle currentToggle;

		private string textHelper;

		private bool isOn;

		public int siblingIndex;

		private EventTrigger triggerEvent;

		private void OnEnable()
		{
			if (!isInitialized)
			{
				Initialize();
			}
			listCG.alpha = 0f;
			listCG.interactable = false;
			listCG.blocksRaycasts = false;
			listRect.sizeDelta = new Vector2(listRect.sizeDelta.x, closeOn);
		}

		private void Initialize()
		{
			if (listCG == null)
			{
				listCG = base.gameObject.GetComponentInChildren<CanvasGroup>();
			}
			if (listRect == null)
			{
				listRect = listCG.GetComponent<RectTransform>();
			}
			if (initAtStart)
			{
				SetupDropdown();
			}
			if (animationType == AnimationType.Modular && dropdownAnimator != null)
			{
				UnityEngine.Object.Destroy(dropdownAnimator);
			}
			if (enableTrigger && triggerObject != null)
			{
				triggerEvent = triggerObject.AddComponent<EventTrigger>();
				EventTrigger.Entry entry = new EventTrigger.Entry();
				entry.eventID = EventTriggerType.PointerClick;
				entry.callback.AddListener((BaseEventData eventData) =>
				{
					Animate();
				});
				triggerEvent.GetComponent<EventTrigger>().triggers.Add(entry);
			}
			if (setHighPriority)
			{
				if (contentCG == null)
				{
					contentCG = base.transform.Find("Content/Item List").GetComponent<CanvasGroup>();
				}
				contentCG.alpha = 1f;
				Canvas canvas = contentCG.gameObject.AddComponent<Canvas>();
				canvas.overrideSorting = true;
				canvas.sortingOrder = 30000;
				contentCG.gameObject.AddComponent<GraphicRaycaster>();
			}
			currentListParent = base.transform.parent;
			closeOn = base.gameObject.GetComponent<RectTransform>().sizeDelta.y;
			isInitialized = true;
		}

		private void Update()
		{
			if (isInTransition)
			{
				ProcessModularAnimation();
			}
		}

		private void ProcessModularAnimation()
		{
			if (isOn)
			{
				listCG.alpha += Time.unscaledDeltaTime * transitionSmoothness;
				listRect.sizeDelta = Vector2.Lerp(listRect.sizeDelta, new Vector2(listRect.sizeDelta.x, panelSize), Time.unscaledDeltaTime * sizeSmoothness);
				if (listRect.sizeDelta.y >= panelSize - 0.1f && listCG.alpha >= 1f)
				{
					isInTransition = false;
				}
			}
			else
			{
				listCG.alpha -= Time.unscaledDeltaTime * transitionSmoothness;
				listRect.sizeDelta = Vector2.Lerp(listRect.sizeDelta, new Vector2(listRect.sizeDelta.x, closeOn), Time.unscaledDeltaTime * sizeSmoothness);
				if (listRect.sizeDelta.y <= closeOn + 0.1f && listCG.alpha <= 0f)
				{
					isInTransition = false;
				}
			}
		}

		public void SetupDropdown()
		{
			if (dropdownAnimator == null)
			{
				dropdownAnimator = base.gameObject.GetComponent<Animator>();
			}
			if (!enableScrollbar && scrollbar != null)
			{
				UnityEngine.Object.Destroy(scrollbar);
			}
			if (itemList == null)
			{
				itemList = itemParent.GetComponent<VerticalLayoutGroup>();
			}
			UpdateItemLayout();
			foreach (Transform item in itemParent)
			{
				UnityEngine.Object.Destroy(item.gameObject);
			}
			for (int i = 0; i < items.Count; i++)
			{
				GameObject gameObject = UnityEngine.Object.Instantiate(itemObject, new Vector3(0f, 0f, 0f), Quaternion.identity);
				gameObject.transform.SetParent(itemParent, worldPositionStays: false);
				setItemText = gameObject.GetComponentInChildren<TextMeshProUGUI>();
				textHelper = items[i].itemName;
				setItemText.text = textHelper;
				items[i].itemIndex = i;
				Item mainItem = items[i];
				Toggle component = gameObject.GetComponent<Toggle>();
				component.onValueChanged.AddListener(delegate
				{
					UpdateToggleData(mainItem.itemIndex);
				});
				component.onValueChanged.AddListener(UpdateToggle);
				component.onValueChanged.AddListener(items[i].onValueChanged.Invoke);
				if (items[i].isOn)
				{
					component.isOn = true;
				}
				else
				{
					component.isOn = false;
				}
				if (invokeAtStart)
				{
					if (items[i].isOn)
					{
						items[i].onValueChanged.Invoke(arg0: true);
					}
					else
					{
						items[i].onValueChanged.Invoke(arg0: false);
					}
				}
			}
			currentListParent = base.transform.parent;
		}

		private void UpdateToggle(bool value)
		{
			if (value)
			{
				currentToggle.isOn = true;
				items[currentIndex].isOn = true;
			}
			else
			{
				currentToggle.isOn = false;
				items[currentIndex].isOn = false;
			}
		}

		private void UpdateToggleData(int itemIndex)
		{
			currentIndex = itemIndex;
			currentToggle = itemParent.GetChild(currentIndex).GetComponent<Toggle>();
		}

		public void Animate()
		{
			if (!isOn && animationType == AnimationType.Modular)
			{
				isOn = true;
				isInTransition = true;
				base.enabled = true;
				listCG.blocksRaycasts = true;
				listCG.interactable = true;
				if (isListItem)
				{
					siblingIndex = base.transform.GetSiblingIndex();
					base.gameObject.transform.SetParent(listParent, worldPositionStays: true);
				}
			}
			else if (isOn && animationType == AnimationType.Modular)
			{
				isOn = false;
				isInTransition = true;
				base.enabled = true;
				listCG.blocksRaycasts = false;
				listCG.interactable = false;
				if (isListItem)
				{
					base.gameObject.transform.SetParent(currentListParent, worldPositionStays: true);
					base.gameObject.transform.SetSiblingIndex(siblingIndex);
				}
			}
			else if (!isOn && animationType == AnimationType.Stylish)
			{
				dropdownAnimator.Play("Stylish In");
				isOn = true;
				if (isListItem)
				{
					siblingIndex = base.transform.GetSiblingIndex();
					base.gameObject.transform.SetParent(listParent, worldPositionStays: true);
				}
			}
			else if (isOn && animationType == AnimationType.Stylish)
			{
				dropdownAnimator.Play("Stylish Out");
				isOn = false;
				if (isListItem)
				{
					base.gameObject.transform.SetParent(currentListParent, worldPositionStays: true);
					base.gameObject.transform.SetSiblingIndex(siblingIndex);
				}
			}
			if (enableTrigger && !isOn)
			{
				triggerObject.SetActive(value: false);
			}
			else if (enableTrigger && isOn)
			{
				triggerObject.SetActive(value: true);
			}
			if (enableTrigger && outOnPointerExit)
			{
				triggerObject.SetActive(value: false);
			}
		}

		public void CreateNewItem(string title, bool value, bool notify)
		{
			Item item = new Item();
			item.itemName = title;
			item.isOn = value;
			items.Add(item);
			if (notify)
			{
				SetupDropdown();
			}
		}

		public void CreateNewItem(string title, bool value)
		{
			Item item = new Item();
			item.itemName = title;
			item.isOn = value;
			items.Add(item);
			SetupDropdown();
		}

		public void CreateNewItem(string title)
		{
			Item item = new Item();
			item.itemName = title;
			items.Add(item);
		}

		public void RemoveItem(string itemTitle)
		{
			Item item = items.Find((Item x) => x.itemName == itemTitle);
			items.Remove(item);
			SetupDropdown();
		}

		public void UpdateItemLayout()
		{
			if (itemList != null)
			{
				itemList.spacing = itemSpacing;
				itemList.padding.top = itemPaddingTop;
				itemList.padding.bottom = itemPaddingBottom;
				itemList.padding.left = itemPaddingLeft;
				itemList.padding.right = itemPaddingRight;
			}
		}

		public void OnPointerClick(PointerEventData eventData)
		{
			if (isInteractable)
			{
				Animate();
			}
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			if (outOnPointerExit && isOn)
			{
				Animate();
				isOn = false;
				if (isListItem)
				{
					base.gameObject.transform.SetParent(currentListParent, worldPositionStays: true);
				}
			}
		}
	}
}
