using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Michsky.MUIP
{
	public class CustomDropdown : MonoBehaviour, IPointerExitHandler, IEventSystemHandler, IPointerEnterHandler, IPointerClickHandler, ISubmitHandler
	{
		[Serializable]
		public class DropdownEvent : UnityEvent<int>
		{
		}

		[Serializable]
		public class ItemTextChangedEvent : UnityEvent<TMP_Text>
		{
		}

		public enum AnimationType
		{
			Modular = 0,
			Custom = 1
		}

		public enum PanelDirection
		{
			Bottom = 0,
			Top = 1
		}

		[Serializable]
		public class Item
		{
			public string itemName = "Dropdown Item";

			public Sprite itemIcon;

			[HideInInspector]
			public int itemIndex;

			public UnityEvent OnItemSelection = new UnityEvent();
		}

		public Animator dropdownAnimator;

		public GameObject triggerObject;

		public TextMeshProUGUI selectedText;

		public Image selectedImage;

		public Transform itemParent;

		public GameObject itemObject;

		public GameObject scrollbar;

		public VerticalLayoutGroup itemList;

		public AudioSource soundSource;

		public RectTransform listRect;

		public CanvasGroup listCG;

		public CanvasGroup contentCG;

		public bool isInteractable = true;

		public bool enableIcon = true;

		public bool enableTrigger = true;

		public bool enableScrollbar = true;

		public bool updateOnEnable = true;

		public bool outOnPointerExit;

		public bool setHighPriority = true;

		public bool invokeAtStart;

		public bool initAtStart = true;

		public bool enableDropdownSounds;

		public bool useHoverSound = true;

		public bool useClickSound = true;

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

		public int selectedItemIndex;

		public AnimationType animationType;

		public PanelDirection panelDirection;

		[Range(25f, 1000f)]
		public float panelSize = 200f;

		[Range(0.5f, 10f)]
		public float curveSpeed = 3f;

		public AnimationCurve animationCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));

		public bool saveSelected;

		public string saveKey = "My Dropdown";

		[SerializeField]
		public List<Item> items = new List<Item>();

		public DropdownEvent onValueChanged = new DropdownEvent();

		public ItemTextChangedEvent onItemTextChanged = new ItemTextChangedEvent();

		public AudioClip hoverSound;

		public AudioClip clickSound;

		private bool isInitialized;

		[HideInInspector]
		public bool isOn;

		[HideInInspector]
		public int index;

		[HideInInspector]
		public int siblingIndex;

		[HideInInspector]
		public TextMeshProUGUI setItemText;

		[HideInInspector]
		public Image setItemImage;

		private EventTrigger triggerEvent;

		private Sprite imageHelper;

		private string textHelper;

		private void OnEnable()
		{
			if (!isInitialized)
			{
				Initialize();
			}
			if (updateOnEnable && index < items.Count)
			{
				SetDropdownIndex(selectedItemIndex, false);
			}
			listCG.alpha = 0f;
			listCG.interactable = false;
			listCG.blocksRaycasts = false;
			listRect.sizeDelta = new Vector2(listRect.sizeDelta.x, 0f);
		}

		private void Initialize()
		{
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
			dropdownAnimator = base.gameObject.GetComponent<Animator>();
			if (listCG == null)
			{
				listCG = base.gameObject.GetComponentInChildren<CanvasGroup>();
			}
			if (listRect == null)
			{
				listRect = listCG.GetComponent<RectTransform>();
			}
			if (initAtStart && items.Count != 0)
			{
				SetupDropdown();
			}
			if (animationType == AnimationType.Modular && dropdownAnimator != null)
			{
				UnityEngine.Object.Destroy(dropdownAnimator);
			}
			isInitialized = true;
		}

		public void SetupDropdown()
		{
			if (!enableScrollbar && scrollbar != null)
			{
				UnityEngine.Object.Destroy(scrollbar);
			}
			if (itemList == null)
			{
				itemList = itemParent.GetComponent<VerticalLayoutGroup>();
			}
			UpdateItemLayout();
			index = 0;
			foreach (Transform item in itemParent)
			{
				UnityEngine.Object.Destroy(item.gameObject);
			}
			for (int i = 0; i < items.Count; i++)
			{
				GameObject gameObject = UnityEngine.Object.Instantiate(itemObject, new Vector3(0f, 0f, 0f), Quaternion.identity);
				gameObject.transform.SetParent(itemParent, worldPositionStays: false);
				gameObject.name = items[i].itemName;
				setItemText = gameObject.GetComponentInChildren<TextMeshProUGUI>();
				textHelper = items[i].itemName;
				setItemText.text = textHelper;
				onItemTextChanged?.Invoke(setItemText);
				Transform transform = gameObject.gameObject.transform.Find("Icon");
				setItemImage = transform.GetComponent<Image>();
				if (items[i].itemIcon == null)
				{
					setItemImage.gameObject.SetActive(value: false);
				}
				else
				{
					imageHelper = items[i].itemIcon;
					setItemImage.sprite = imageHelper;
				}
				items[i].itemIndex = i;
				Item mainItem = items[i];
				Button component = gameObject.GetComponent<Button>();
				component.onClick.AddListener(Animate);
				component.onClick.AddListener(items[i].OnItemSelection.Invoke);
				component.onClick.AddListener(() =>
				{
					SetDropdownIndex(index = mainItem.itemIndex);
					onValueChanged.Invoke(index = mainItem.itemIndex);
					if (saveSelected)
					{
						PlayerPrefs.SetInt("Dropdown_" + saveKey, mainItem.itemIndex);
					}
				});
			}
			if (selectedImage != null && !enableIcon)
			{
				selectedImage.gameObject.SetActive(value: false);
			}
			else if (selectedImage != null)
			{
				selectedImage.sprite = items[selectedItemIndex].itemIcon;
			}
			if (selectedText != null)
			{
				selectedText.text = items[selectedItemIndex].itemName;
				onItemTextChanged?.Invoke(selectedText);
			}
			if (saveSelected)
			{
				if (invokeAtStart)
				{
					items[PlayerPrefs.GetInt("Dropdown_" + saveKey)].OnItemSelection.Invoke();
				}
				else
				{
					SetDropdownIndex(PlayerPrefs.GetInt("Dropdown_" + saveKey), false);
				}
			}
			else if (invokeAtStart)
			{
				items[selectedItemIndex].OnItemSelection.Invoke();
			}
		}

		public void ChangeDropdownInfo(int itemIndex)
		{
			SetDropdownIndex(itemIndex);
		}

		public void SetDropdownIndex(int itemIndex)
		{
			SetDropdownIndex(itemIndex, bypassSound: true);
		}

		public void SetDropdownIndex(int itemIndex, bool bypassSound = false)
		{
			if (selectedImage != null && enableIcon && items[itemIndex].itemIcon != null)
			{
				selectedImage.gameObject.SetActive(value: true);
				selectedImage.sprite = items[itemIndex].itemIcon;
			}
			else if (selectedImage != null && enableIcon && items[itemIndex].itemIcon == null)
			{
				selectedImage.gameObject.SetActive(value: false);
			}
			if (selectedText != null)
			{
				selectedText.text = items[itemIndex].itemName;
				onItemTextChanged?.Invoke(selectedText);
			}
			if (!bypassSound && enableDropdownSounds && useClickSound)
			{
				soundSource.PlayOneShot(clickSound);
			}
			selectedItemIndex = itemIndex;
		}

		public void Animate()
		{
			if (!isOn && animationType == AnimationType.Modular)
			{
				isOn = true;
				listCG.blocksRaycasts = true;
				listCG.interactable = true;
				listCG.gameObject.SetActive(value: true);
				StopCoroutine("StartMinimize");
				StopCoroutine("StartExpand");
				StartCoroutine("StartExpand");
			}
			else if (isOn && animationType == AnimationType.Modular)
			{
				isOn = false;
				listCG.blocksRaycasts = false;
				listCG.interactable = false;
				StopCoroutine("StartMinimize");
				StopCoroutine("StartExpand");
				StartCoroutine("StartMinimize");
			}
			else if (!isOn && animationType == AnimationType.Custom)
			{
				dropdownAnimator.Play("Stylish In");
				isOn = true;
			}
			else if (isOn && animationType == AnimationType.Custom)
			{
				dropdownAnimator.Play("Stylish Out");
				isOn = false;
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

		public void Interactable(bool value)
		{
			isInteractable = value;
		}

		public void CreateNewItem(string title, Sprite icon, bool notify = false)
		{
			Item item = new Item
			{
				itemName = title,
				itemIcon = icon
			};
			items.Add(item);
			if (selectedItemIndex > items.Count)
			{
				selectedItemIndex = 0;
			}
			if (notify)
			{
				SetupDropdown();
			}
		}

		public void CreateNewItem(string title, bool notify = false)
		{
			Item item = new Item
			{
				itemName = title
			};
			items.Add(item);
			if (selectedItemIndex > items.Count)
			{
				selectedItemIndex = 0;
			}
			if (notify)
			{
				SetupDropdown();
			}
		}

		public void RemoveItem(string itemTitle, bool notify = false)
		{
			Item item = items.Find((Item x) => x.itemName == itemTitle);
			items.Remove(item);
			if (selectedItemIndex > items.Count)
			{
				selectedItemIndex = 0;
			}
			if (notify)
			{
				SetupDropdown();
			}
		}

		public void UpdateItemLayout()
		{
			if (!(itemList == null))
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
				if (enableDropdownSounds && useClickSound)
				{
					soundSource.PlayOneShot(clickSound);
				}
				Animate();
			}
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			if (isInteractable && enableDropdownSounds && useHoverSound)
			{
				soundSource.PlayOneShot(hoverSound);
			}
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			if (isInteractable && outOnPointerExit && isOn)
			{
				Animate();
				isOn = false;
			}
		}

		public void OnSubmit(BaseEventData eventData)
		{
			if (isInteractable)
			{
				if (enableDropdownSounds && useClickSound)
				{
					soundSource.PlayOneShot(clickSound);
				}
				Animate();
			}
		}

		private IEnumerator StartExpand()
		{
			float elapsedTime = 0f;
			Vector2 startPos = listRect.sizeDelta;
			Vector2 endPos = new Vector2(listRect.sizeDelta.x, panelSize);
			while (listRect.sizeDelta.y <= panelSize - 0.1f)
			{
				elapsedTime += Time.unscaledDeltaTime;
				listCG.alpha += Time.unscaledDeltaTime * (curveSpeed * 2f);
				listRect.sizeDelta = Vector2.Lerp(startPos, endPos, animationCurve.Evaluate(elapsedTime * curveSpeed));
				yield return null;
			}
			listCG.alpha = 1f;
			listRect.sizeDelta = endPos;
		}

		private IEnumerator StartMinimize()
		{
			float elapsedTime = 0f;
			Vector2 startPos = listRect.sizeDelta;
			Vector2 endPos = new Vector2(listRect.sizeDelta.x, 0f);
			while (listRect.sizeDelta.y >= 0.1f)
			{
				elapsedTime += Time.unscaledDeltaTime;
				listCG.alpha -= Time.unscaledDeltaTime * (curveSpeed * 2f);
				listRect.sizeDelta = Vector2.Lerp(startPos, endPos, animationCurve.Evaluate(elapsedTime * curveSpeed));
				yield return null;
			}
			listCG.alpha = 0f;
			listRect.sizeDelta = endPos;
			listCG.gameObject.SetActive(value: false);
		}
	}
}
