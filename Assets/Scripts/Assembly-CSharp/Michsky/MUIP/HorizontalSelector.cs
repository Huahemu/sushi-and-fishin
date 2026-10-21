using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Michsky.MUIP
{
	[RequireComponent(typeof(Animator))]
	public class HorizontalSelector : MonoBehaviour
	{
		[Serializable]
		public class SelectorEvent : UnityEvent<int>
		{
		}

		[Serializable]
		public class ItemTextChangedEvent : UnityEvent<TMP_Text>
		{
		}

		[Serializable]
		public class Item
		{
			public string itemTitle = "Item Title";

			public Sprite itemIcon;

			public UnityEvent onItemSelect = new UnityEvent();
		}

		public TextMeshProUGUI label;

		public TextMeshProUGUI labelHelper;

		public Image labelIcon;

		public Image labelIconHelper;

		public Transform indicatorParent;

		public GameObject indicatorObject;

		public Animator selectorAnimator;

		public HorizontalLayoutGroup contentLayout;

		public HorizontalLayoutGroup contentLayoutHelper;

		private string newItemTitle;

		public bool enableIcon = true;

		public bool saveSelected;

		public string saveKey = "My Selector";

		public bool enableIndicators = true;

		public bool invokeAtStart;

		public bool invertAnimation;

		public bool loopSelection;

		[Range(0.25f, 2.5f)]
		public float iconScale = 1f;

		[Range(1f, 50f)]
		public int contentSpacing = 15;

		public int defaultIndex;

		[HideInInspector]
		public int index;

		public List<Item> items = new List<Item>();

		public SelectorEvent onValueChanged;

		public ItemTextChangedEvent onItemTextChanged;

		private void Awake()
		{
			if (selectorAnimator == null)
			{
				selectorAnimator = base.gameObject.GetComponent<Animator>();
			}
			if (label == null || labelHelper == null)
			{
				Debug.LogError("<b>[Horizontal Selector]</b> Cannot initalize the object due to missing resources.", this);
				return;
			}
			SetupSelector();
			UpdateContentLayout();
			if (invokeAtStart)
			{
				items[index].onItemSelect.Invoke();
				onValueChanged.Invoke(index);
			}
		}

		private void OnEnable()
		{
			if (base.gameObject.activeInHierarchy)
			{
				StartCoroutine("DisableAnimator");
			}
		}

		public void SetupSelector()
		{
			if (items.Count == 0)
			{
				return;
			}
			if (saveSelected)
			{
				if (PlayerPrefs.HasKey("HorizontalSelector_" + saveKey))
				{
					defaultIndex = PlayerPrefs.GetInt("HorizontalSelector_" + saveKey);
				}
				else
				{
					PlayerPrefs.SetInt("HorizontalSelector_" + saveKey, defaultIndex);
				}
			}
			label.text = items[defaultIndex].itemTitle;
			labelHelper.text = label.text;
			onItemTextChanged?.Invoke(label);
			if (labelIcon != null && enableIcon)
			{
				labelIcon.sprite = items[defaultIndex].itemIcon;
				labelIconHelper.sprite = labelIcon.sprite;
			}
			else if (!enableIcon)
			{
				if (labelIcon != null)
				{
					labelIcon.gameObject.SetActive(value: false);
				}
				if (labelIconHelper != null)
				{
					labelIconHelper.gameObject.SetActive(value: false);
				}
			}
			index = defaultIndex;
			if (enableIndicators)
			{
				UpdateIndicators();
			}
			else if (indicatorParent != null)
			{
				UnityEngine.Object.Destroy(indicatorParent.gameObject);
			}
		}

		public void PreviousItem()
		{
			if (items.Count == 0)
			{
				return;
			}
			StopCoroutine("DisableAnimator");
			selectorAnimator.enabled = true;
			if (!loopSelection)
			{
				if (index != 0)
				{
					labelHelper.text = label.text;
					if (labelIcon != null && enableIcon)
					{
						labelIconHelper.sprite = labelIcon.sprite;
					}
					if (index == 0)
					{
						index = items.Count - 1;
					}
					else
					{
						index--;
					}
					label.text = items[index].itemTitle;
					onItemTextChanged?.Invoke(label);
					if (labelIcon != null && enableIcon)
					{
						labelIcon.sprite = items[index].itemIcon;
					}
					items[index].onItemSelect.Invoke();
					onValueChanged.Invoke(index);
					selectorAnimator.Play(null);
					selectorAnimator.StopPlayback();
					if (invertAnimation)
					{
						selectorAnimator.Play("Forward");
					}
					else
					{
						selectorAnimator.Play("Previous");
					}
				}
			}
			else
			{
				labelHelper.text = label.text;
				if (labelIcon != null && enableIcon)
				{
					labelIconHelper.sprite = labelIcon.sprite;
				}
				if (index == 0)
				{
					index = items.Count - 1;
				}
				else
				{
					index--;
				}
				label.text = items[index].itemTitle;
				onItemTextChanged?.Invoke(label);
				if (labelIcon != null && enableIcon)
				{
					labelIcon.sprite = items[index].itemIcon;
				}
				items[index].onItemSelect.Invoke();
				onValueChanged.Invoke(index);
				selectorAnimator.Play(null);
				selectorAnimator.StopPlayback();
				if (invertAnimation)
				{
					selectorAnimator.Play("Forward");
				}
				else
				{
					selectorAnimator.Play("Previous");
				}
			}
			if (saveSelected)
			{
				PlayerPrefs.SetInt("HorizontalSelector_" + saveKey, index);
			}
			if (base.gameObject.activeInHierarchy)
			{
				StartCoroutine("DisableAnimator");
			}
			if (!enableIndicators)
			{
				return;
			}
			for (int i = 0; i < items.Count; i++)
			{
				GameObject obj = indicatorParent.GetChild(i).gameObject;
				Transform transform = obj.transform.Find("On");
				Transform transform2 = obj.transform.Find("Off");
				if (i == index)
				{
					transform.gameObject.SetActive(value: true);
					transform2.gameObject.SetActive(value: false);
				}
				else
				{
					transform.gameObject.SetActive(value: false);
					transform2.gameObject.SetActive(value: true);
				}
			}
		}

		public void NextItem()
		{
			if (items.Count == 0)
			{
				return;
			}
			StopCoroutine("DisableAnimator");
			selectorAnimator.enabled = true;
			if (!loopSelection)
			{
				if (index != items.Count - 1)
				{
					labelHelper.text = label.text;
					if (labelIcon != null && enableIcon)
					{
						labelIconHelper.sprite = labelIcon.sprite;
					}
					if (index + 1 >= items.Count)
					{
						index = 0;
					}
					else
					{
						index++;
					}
					label.text = items[index].itemTitle;
					onItemTextChanged?.Invoke(label);
					if (labelIcon != null && enableIcon)
					{
						labelIcon.sprite = items[index].itemIcon;
					}
					items[index].onItemSelect.Invoke();
					onValueChanged.Invoke(index);
					selectorAnimator.Play(null);
					selectorAnimator.StopPlayback();
					if (invertAnimation)
					{
						selectorAnimator.Play("Previous");
					}
					else
					{
						selectorAnimator.Play("Forward");
					}
				}
			}
			else
			{
				labelHelper.text = label.text;
				if (labelIcon != null && enableIcon)
				{
					labelIconHelper.sprite = labelIcon.sprite;
				}
				if (index + 1 >= items.Count)
				{
					index = 0;
				}
				else
				{
					index++;
				}
				label.text = items[index].itemTitle;
				onItemTextChanged?.Invoke(label);
				if (labelIcon != null && enableIcon)
				{
					labelIcon.sprite = items[index].itemIcon;
				}
				items[index].onItemSelect.Invoke();
				onValueChanged.Invoke(index);
				selectorAnimator.Play(null);
				selectorAnimator.StopPlayback();
				if (invertAnimation)
				{
					selectorAnimator.Play("Previous");
				}
				else
				{
					selectorAnimator.Play("Forward");
				}
			}
			if (saveSelected)
			{
				PlayerPrefs.SetInt("HorizontalSelector_" + saveKey, index);
			}
			if (enableIndicators)
			{
				for (int i = 0; i < items.Count; i++)
				{
					GameObject obj = indicatorParent.GetChild(i).gameObject;
					Transform transform = obj.transform.Find("On");
					Transform transform2 = obj.transform.Find("Off");
					if (i == index)
					{
						transform.gameObject.SetActive(value: true);
						transform2.gameObject.SetActive(value: false);
					}
					else
					{
						transform.gameObject.SetActive(value: false);
						transform2.gameObject.SetActive(value: true);
					}
				}
			}
			if (base.gameObject.activeInHierarchy)
			{
				StartCoroutine("DisableAnimator");
			}
		}

		public void PreviousClick()
		{
			PreviousItem();
		}

		public void ForwardClick()
		{
			NextItem();
		}

		public void CreateNewItem(string title)
		{
			Item item = new Item();
			newItemTitle = title;
			item.itemTitle = newItemTitle;
			items.Add(item);
		}

		public void CreateNewItem(string title, Sprite icon)
		{
			Item item = new Item();
			newItemTitle = title;
			item.itemTitle = newItemTitle;
			item.itemIcon = icon;
			items.Add(item);
		}

		public void RemoveItem(string itemTitle)
		{
			Item item = items.Find((Item x) => x.itemTitle == itemTitle);
			items.Remove(item);
			SetupSelector();
		}

		public void UpdateUI()
		{
			selectorAnimator.enabled = true;
			label.text = items[index].itemTitle;
			onItemTextChanged?.Invoke(label);
			if (labelIcon != null && enableIcon)
			{
				labelIcon.sprite = items[index].itemIcon;
			}
			if (base.gameObject.activeInHierarchy)
			{
				StartCoroutine("DisableAnimator");
			}
			UpdateContentLayout();
			UpdateIndicators();
		}

		public void UpdateIndicators()
		{
			if (!enableIndicators)
			{
				return;
			}
			foreach (Transform item in indicatorParent)
			{
				UnityEngine.Object.Destroy(item.gameObject);
			}
			for (int i = 0; i < items.Count; i++)
			{
				GameObject obj = UnityEngine.Object.Instantiate(indicatorObject, new Vector3(0f, 0f, 0f), Quaternion.identity);
				obj.transform.SetParent(indicatorParent, worldPositionStays: false);
				obj.name = items[i].itemTitle;
				Transform transform = obj.transform.Find("On");
				Transform transform2 = obj.transform.Find("Off");
				if (i == index)
				{
					transform.gameObject.SetActive(value: true);
					transform2.gameObject.SetActive(value: false);
				}
				else
				{
					transform.gameObject.SetActive(value: false);
					transform2.gameObject.SetActive(value: true);
				}
			}
		}

		public void UpdateContentLayout()
		{
			if (contentLayout != null)
			{
				contentLayout.spacing = contentSpacing;
			}
			if (contentLayoutHelper != null)
			{
				contentLayoutHelper.spacing = contentSpacing;
			}
			if (labelIcon != null)
			{
				labelIcon.transform.localScale = new Vector3(iconScale, iconScale, iconScale);
				labelIconHelper.transform.localScale = new Vector3(iconScale, iconScale, iconScale);
			}
			LayoutRebuilder.ForceRebuildLayoutImmediate(label.transform.GetComponent<RectTransform>());
			LayoutRebuilder.ForceRebuildLayoutImmediate(label.transform.parent.GetComponent<RectTransform>());
		}

		private IEnumerator DisableAnimator()
		{
			yield return new WaitForSecondsRealtime(0.5f);
			selectorAnimator.enabled = false;
		}
	}
}
