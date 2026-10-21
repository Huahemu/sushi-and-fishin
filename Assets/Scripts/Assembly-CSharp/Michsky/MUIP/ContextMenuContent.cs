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
	[AddComponentMenu("Modern UI Pack/Context Menu/Context Menu Content")]
	public class ContextMenuContent : MonoBehaviour, IPointerClickHandler, IEventSystemHandler
	{
		[Serializable]
		public class ContextItem
		{
			[Header("Information")]
			[Space(-5f)]
			public string itemText = "Item Text";

			public Sprite itemIcon;

			public ContextItemType contextItemType;

			[Header("Sub Menu")]
			public List<SubMenuItem> subMenuItems = new List<SubMenuItem>();

			[Header("Events")]
			public UnityEvent onClick;
		}

		[Serializable]
		public class SubMenuItem
		{
			public string itemText = "Item Text";

			public Sprite itemIcon;

			public ContextItemType contextItemType;

			public UnityEvent onClick;
		}

		public enum ContextItemType
		{
			Button = 0,
			Separator = 1
		}

		public ContextMenuManager contextManager;

		public Transform itemParent;

		public bool useIn3D;

		public List<ContextItem> contexItems = new List<ContextItem>();

		private GameObject selectedItem;

		private Image setItemImage;

		private TextMeshProUGUI setItemText;

		private Sprite imageHelper;

		private string textHelper;

		private void Awake()
		{
			if (contextManager == null)
			{
				try
				{
					contextManager = UnityEngine.Object.FindObjectsByType<ContextMenuManager>(FindObjectsSortMode.None)[0];
					itemParent = contextManager.transform.Find("Content/Item List").transform;
				}
				catch
				{
					Debug.LogError("<b>[Context Menu]</b> Context Manager is missing.", this);
					return;
				}
			}
			foreach (Transform item in itemParent)
			{
				UnityEngine.Object.Destroy(item.gameObject);
			}
		}

		public void ProcessContent()
		{
			foreach (Transform item in itemParent)
			{
				UnityEngine.Object.Destroy(item.gameObject);
			}
			for (int i = 0; i < contexItems.Count; i++)
			{
				bool flag = false;
				if (contexItems[i].contextItemType == ContextItemType.Button && contextManager.contextButton != null)
				{
					selectedItem = contextManager.contextButton;
				}
				else if (contexItems[i].contextItemType == ContextItemType.Separator && contextManager.contextSeparator != null)
				{
					selectedItem = contextManager.contextSeparator;
				}
				else
				{
					Debug.LogError("<b>[Context Menu]</b> At least one of the item presets is missing. You can assign a new variable in Resources (Context Menu) tab. All default presets can be found in <b>Modern UI Pack > Prefabs > Context Menu</b> folder.", this);
					flag = true;
				}
				if (flag)
				{
					continue;
				}
				if (contexItems[i].subMenuItems.Count == 0)
				{
					GameObject gameObject = UnityEngine.Object.Instantiate(selectedItem, new Vector3(0f, 0f, 0f), Quaternion.identity);
					gameObject.transform.SetParent(itemParent, worldPositionStays: false);
					if (contexItems[i].contextItemType == ContextItemType.Button)
					{
						setItemText = gameObject.GetComponentInChildren<TextMeshProUGUI>();
						textHelper = contexItems[i].itemText;
						setItemText.text = textHelper;
						Transform transform = gameObject.gameObject.transform.Find("Icon");
						setItemImage = transform.GetComponent<Image>();
						imageHelper = contexItems[i].itemIcon;
						setItemImage.sprite = imageHelper;
						if (imageHelper == null)
						{
							setItemImage.color = new Color(0f, 0f, 0f, 0f);
						}
						Button component = gameObject.GetComponent<Button>();
						component.onClick.AddListener(contexItems[i].onClick.Invoke);
						component.onClick.AddListener(contextManager.Close);
					}
				}
				else if (contextManager.contextSubMenu != null && contexItems[i].subMenuItems.Count != 0)
				{
					GameObject gameObject2 = UnityEngine.Object.Instantiate(contextManager.contextSubMenu, new Vector3(0f, 0f, 0f), Quaternion.identity);
					gameObject2.transform.SetParent(itemParent, worldPositionStays: false);
					ContextMenuSubMenu component2 = gameObject2.GetComponent<ContextMenuSubMenu>();
					component2.cmManager = contextManager;
					component2.cmContent = this;
					component2.subMenuIndex = i;
					setItemText = gameObject2.GetComponentInChildren<TextMeshProUGUI>();
					textHelper = contexItems[i].itemText;
					setItemText.text = textHelper;
					Transform transform2 = gameObject2.gameObject.transform.Find("Icon");
					setItemImage = transform2.GetComponent<Image>();
					imageHelper = contexItems[i].itemIcon;
					setItemImage.sprite = imageHelper;
				}
				StopCoroutine("ExecuteAfterTime");
				StartCoroutine("ExecuteAfterTime", 0.01f);
			}
			contextManager.SetContextMenuPosition();
			contextManager.Open();
		}

		public void OnPointerClick(PointerEventData eventData)
		{
			if (contextManager.isOn)
			{
				contextManager.Close();
			}
			else if (eventData.button == PointerEventData.InputButton.Right && !contextManager.isOn)
			{
				ProcessContent();
			}
		}

		private IEnumerator ExecuteAfterTime(float time)
		{
			yield return new WaitForSecondsRealtime(time);
			itemParent.gameObject.SetActive(value: false);
			itemParent.gameObject.SetActive(value: true);
		}

		public void OnMouseOver()
		{
			if (useIn3D && Input.GetMouseButtonDown(1))
			{
				ProcessContent();
			}
		}

		public void AddNewItem()
		{
			ContextItem item = new ContextItem();
			contexItems.Add(item);
		}
	}
}
