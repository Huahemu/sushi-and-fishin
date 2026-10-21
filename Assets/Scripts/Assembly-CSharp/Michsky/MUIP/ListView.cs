using System;
using System.Collections.Generic;
using UnityEngine;

namespace Michsky.MUIP
{
	public class ListView : MonoBehaviour
	{
		[Serializable]
		public class ListItem
		{
			public string itemTitle = "List Item";

			[HideInInspector]
			public ListRow row0;

			[HideInInspector]
			public ListRow row1;

			[HideInInspector]
			public ListRow row2;
		}

		[Serializable]
		public class ListRow
		{
			public RowType rowType = RowType.Text;

			public Sprite rowIcon;

			public string rowText = "Row text";

			public bool usePreferredWidth;

			public int preferredWidth = 50;

			[Range(0.1f, 1f)]
			public float iconScale = 1f;
		}

		public enum RowType
		{
			Icon = 0,
			Text = 1
		}

		public enum RowCount
		{
			One = 0,
			Two = 1,
			Three = 2
		}

		public Transform itemParent;

		public GameObject itemPreset;

		public GameObject scrollbar;

		public bool initializeOnAwake = true;

		public bool showScrollbar = true;

		public RowCount rowCount = RowCount.Two;

		[SerializeField]
		public List<ListItem> listItems = new List<ListItem>();

		private void Awake()
		{
			if (itemParent == null)
			{
				Debug.LogError("<b>[List View]</b> 'Item Parent' is missing.");
			}
			else if (initializeOnAwake)
			{
				InitializeItems();
			}
		}

		public void InitializeItems()
		{
			foreach (Transform item in itemParent)
			{
				UnityEngine.Object.Destroy(item.gameObject);
			}
			for (int i = 0; i < listItems.Count; i++)
			{
				GameObject obj = UnityEngine.Object.Instantiate(itemPreset, new Vector3(0f, 0f, 0f), Quaternion.identity);
				obj.transform.SetParent(itemParent, worldPositionStays: false);
				obj.name = listItems[i].itemTitle;
				ListViewItem component = obj.GetComponent<ListViewItem>();
				component.rowCount = rowCount;
				component.row0Ref = listItems[i].row0;
				component.row1Ref = listItems[i].row1;
				component.row2Ref = listItems[i].row2;
				component.PassReferences();
			}
			if (!showScrollbar && scrollbar != null)
			{
				scrollbar.transform.localScale = new Vector3(0f, 0f, 0f);
			}
			else if (showScrollbar && scrollbar != null)
			{
				scrollbar.transform.localScale = new Vector3(1f, 1f, 1f);
			}
		}
	}
}
