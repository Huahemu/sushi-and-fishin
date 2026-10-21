using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Michsky.MUIP
{
	public class ContextMenuSubMenu : MonoBehaviour, IPointerClickHandler, IEventSystemHandler, IPointerEnterHandler, IPointerExitHandler
	{
		public ContextMenuManager cmManager;

		public ContextMenuContent cmContent;

		public Animator subMenuAnimator;

		public Transform itemParent;

		public GameObject trigger;

		[HideInInspector]
		public int subMenuIndex;

		private GameObject selectedItem;

		private Image setItemImage;

		private TextMeshProUGUI setItemText;

		private Sprite imageHelper;

		private string textHelper;

		private RectTransform listParent;

		[HideInInspector]
		public bool enableFadeOut = true;

		private void OnEnable()
		{
			if (itemParent == null)
			{
				Debug.Log("<b>[Context Menu]</b> Item Parent is missing.", this);
			}
			else
			{
				listParent = itemParent.parent.gameObject.GetComponent<RectTransform>();
			}
		}

		public void OnPointerClick(PointerEventData eventData)
		{
			if (cmManager.subMenuBehaviour != ContextMenuManager.SubMenuBehaviour.Click)
			{
				return;
			}
			if (subMenuAnimator.GetCurrentAnimatorStateInfo(0).IsName("Menu In"))
			{
				subMenuAnimator.Play("Menu Out");
				if (trigger != null)
				{
					trigger.SetActive(value: false);
				}
			}
			else
			{
				subMenuAnimator.Play("Menu In");
				if (trigger != null)
				{
					trigger.SetActive(value: true);
				}
			}
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			foreach (Transform item in itemParent)
			{
				Object.Destroy(item.gameObject);
			}
			for (int i = 0; i < cmContent.contexItems[subMenuIndex].subMenuItems.Count; i++)
			{
				bool flag = false;
				if (cmContent.contexItems[subMenuIndex].subMenuItems[i].contextItemType == ContextMenuContent.ContextItemType.Button && cmManager.contextButton != null)
				{
					selectedItem = cmManager.contextButton;
				}
				else if (cmContent.contexItems[subMenuIndex].subMenuItems[i].contextItemType == ContextMenuContent.ContextItemType.Separator && cmManager.contextSeparator != null)
				{
					selectedItem = cmManager.contextSeparator;
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
				GameObject gameObject = Object.Instantiate(selectedItem, new Vector3(0f, 0f, 0f), Quaternion.identity);
				gameObject.transform.SetParent(itemParent, worldPositionStays: false);
				if (cmContent.contexItems[subMenuIndex].subMenuItems[i].contextItemType == ContextMenuContent.ContextItemType.Button)
				{
					setItemText = gameObject.GetComponentInChildren<TextMeshProUGUI>();
					textHelper = cmContent.contexItems[subMenuIndex].subMenuItems[i].itemText;
					setItemText.text = textHelper;
					Transform transform = gameObject.gameObject.transform.Find("Icon");
					setItemImage = transform.GetComponent<Image>();
					imageHelper = cmContent.contexItems[subMenuIndex].subMenuItems[i].itemIcon;
					setItemImage.sprite = imageHelper;
					if (imageHelper == null)
					{
						setItemImage.color = new Color(0f, 0f, 0f, 0f);
					}
					Button component = gameObject.GetComponent<Button>();
					component.onClick.AddListener(cmContent.contexItems[subMenuIndex].subMenuItems[i].onClick.Invoke);
					component.onClick.AddListener(CloseOnClick);
					StartCoroutine(ExecuteAfterTime(0.01f));
				}
			}
			if (cmManager.autoSubMenuPosition)
			{
				if (cmManager.horizontalBound == ContextMenuManager.CursorBoundHorizontal.Left)
				{
					listParent.pivot = new Vector2(0f, listParent.pivot.y);
				}
				else if (cmManager.horizontalBound == ContextMenuManager.CursorBoundHorizontal.Right)
				{
					listParent.pivot = new Vector2(1f, listParent.pivot.y);
				}
				if (cmManager.verticalBound == ContextMenuManager.CursorBoundVertical.Top)
				{
					listParent.pivot = new Vector2(listParent.pivot.x, 0f);
				}
				else if (cmManager.verticalBound == ContextMenuManager.CursorBoundVertical.Bottom)
				{
					listParent.pivot = new Vector2(listParent.pivot.x, 1f);
				}
			}
			if (cmManager.subMenuBehaviour == ContextMenuManager.SubMenuBehaviour.Hover)
			{
				subMenuAnimator.Play("Menu In");
			}
		}

		public void OnPointerExit(PointerEventData eventData)
		{
		}

		private IEnumerator ExecuteAfterTime(float time)
		{
			yield return new WaitForSecondsRealtime(time);
			itemParent.gameObject.SetActive(value: false);
			itemParent.gameObject.SetActive(value: true);
			StopCoroutine(ExecuteAfterTime(0.01f));
			StopCoroutine("ExecuteAfterTime");
		}

		public void CloseOnClick()
		{
			cmManager.contextAnimator.Play("Menu Out");
			cmManager.isOn = false;
			trigger.SetActive(value: false);
		}
	}
}
