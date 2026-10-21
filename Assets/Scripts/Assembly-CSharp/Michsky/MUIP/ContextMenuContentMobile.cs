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
	[AddComponentMenu("Modern UI Pack/Context Menu/Context Menu Content (Mobile)")]
	public class ContextMenuContentMobile : MonoBehaviour, IPointerDownHandler, IEventSystemHandler, IPointerUpHandler
	{
		[Serializable]
		public class ContextItem
		{
			public string itemText = "Item Text";

			public Sprite itemIcon;

			public ContextItemType contextItemType;

			public UnityEvent onClick;
		}

		public enum ContextItemType
		{
			BUTTON = 0
		}

		[Header("Resources")]
		public ContextMenuManager contextManager;

		public Transform itemParent;

		[Header("Settings")]
		[Range(0.1f, 6f)]
		public float holdToOpen = 0.75f;

		[Header("Items")]
		public List<ContextItem> contexItems = new List<ContextItem>();

		private Animator contextAnimator;

		private GameObject selectedItem;

		private Image setItemImage;

		private TextMeshProUGUI setItemText;

		private Sprite imageHelper;

		private string textHelper;

		private float timer;

		private bool timerEnabled;

		private void Start()
		{
			if (contextManager == null)
			{
				try
				{
					contextManager = GameObject.Find("Context Menu").GetComponent<ContextMenuManager>();
					itemParent = contextManager.transform.Find("Content/Item List").transform;
				}
				catch
				{
					Debug.Log("<b>[Context Menu]</b> Context Manager is missing.", this);
					return;
				}
			}
			contextAnimator = contextManager.contextAnimator;
			foreach (Transform item in itemParent)
			{
				UnityEngine.Object.Destroy(item.gameObject);
			}
		}

		private void Update()
		{
			if (timerEnabled)
			{
				timer += Time.deltaTime;
				if (timer >= holdToOpen)
				{
					CheckForTimer();
					timerEnabled = false;
					timer = 0f;
				}
			}
		}

		public void OnPointerDown(PointerEventData eventData)
		{
			timerEnabled = true;
		}

		public void OnPointerUp(PointerEventData eventData)
		{
			timerEnabled = false;
			timer = 0f;
		}

		public void CheckForTimer()
		{
			if (timer <= holdToOpen)
			{
				return;
			}
			if (contextManager.isOn)
			{
				contextAnimator.Play("Menu Out");
				contextManager.isOn = false;
			}
			else
			{
				if (contextManager.isOn)
				{
					return;
				}
				foreach (Transform item in itemParent)
				{
					UnityEngine.Object.Destroy(item.gameObject);
				}
				for (int i = 0; i < contexItems.Count; i++)
				{
					if (contexItems[i].contextItemType == ContextItemType.BUTTON)
					{
						selectedItem = contextManager.contextButton;
					}
					GameObject gameObject = UnityEngine.Object.Instantiate(selectedItem, new Vector3(0f, 0f, 0f), Quaternion.identity);
					gameObject.transform.SetParent(itemParent, worldPositionStays: false);
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
					component.onClick.AddListener(CloseOnClick);
					StartCoroutine(ExecuteAfterTime(0.01f));
				}
				contextManager.SetContextMenuPosition();
				contextAnimator.Play("Menu In");
				contextManager.isOn = true;
				contextManager.SetContextMenuPosition();
			}
		}

		private IEnumerator ExecuteAfterTime(float time)
		{
			yield return new WaitForSeconds(time);
			itemParent.gameObject.SetActive(value: false);
			itemParent.gameObject.SetActive(value: true);
			StopCoroutine(ExecuteAfterTime(0.01f));
			StopCoroutine("ExecuteAfterTime");
		}

		public void CloseOnClick()
		{
			contextAnimator.Play("Menu Out");
			contextManager.isOn = false;
		}
	}
}
