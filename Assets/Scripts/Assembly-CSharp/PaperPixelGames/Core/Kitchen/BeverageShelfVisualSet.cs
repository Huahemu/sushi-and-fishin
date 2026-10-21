using System;
using System.Collections.Generic;
using EPOOutline;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using UnityEngine;

namespace PaperPixelGames.Core.Kitchen
{
	[Serializable]
	public class BeverageShelfVisualSet
	{
		public BeverageType Type;

		public BeverageSO BeverageSO;

		[Header("Real Visuals")]
		public List<GameObject> BeverageObjects = new List<GameObject>();

		[Header("Preview Visuals")]
		public List<GameObject> PreviewObjects = new List<GameObject>();

		public int GetCapacity(int maxBeverages)
		{
			return Mathf.Min(maxBeverages, BeverageObjects.Count);
		}

		public void SetAllActive(bool active)
		{
			for (int i = 0; i < BeverageObjects.Count; i++)
			{
				if (BeverageObjects[i] != null)
				{
					BeverageObjects[i].SetActive(active);
					DisableOutlines(BeverageObjects[i]);
				}
			}
			SetAllPreviewActive(active: false);
		}

		public void SetAllPreviewActive(bool active)
		{
			for (int i = 0; i < PreviewObjects.Count; i++)
			{
				if (PreviewObjects[i] != null)
				{
					PreviewObjects[i].SetActive(active);
					DisableOutlines(PreviewObjects[i]);
				}
			}
		}

		public void Rebuild(int count, int maxBeverages)
		{
			int capacity = GetCapacity(maxBeverages);
			int num = Mathf.Clamp(count, 0, capacity);
			for (int i = 0; i < BeverageObjects.Count; i++)
			{
				GameObject gameObject = BeverageObjects[i];
				if (!(gameObject == null))
				{
					gameObject.SetActive(i < num);
					DisableOutlines(gameObject);
				}
			}
			SetAllPreviewActive(active: false);
		}

		public void ShowPreviewAt(int index)
		{
			SetAllPreviewActive(active: false);
			if (index >= 0 && index < PreviewObjects.Count && PreviewObjects[index] != null)
			{
				PreviewObjects[index].SetActive(value: true);
				DisableOutlines(PreviewObjects[index]);
			}
		}

		public GameObject GetBeverageObjectAt(int index)
		{
			if (index < 0 || index >= BeverageObjects.Count)
			{
				return null;
			}
			return BeverageObjects[index];
		}

		public void DisableAllOutlines()
		{
			for (int i = 0; i < BeverageObjects.Count; i++)
			{
				DisableOutlines(BeverageObjects[i]);
			}
			for (int j = 0; j < PreviewObjects.Count; j++)
			{
				DisableOutlines(PreviewObjects[j]);
			}
		}

		private static void DisableOutlines(GameObject root)
		{
			if (root == null)
			{
				return;
			}
			Outlinable[] componentsInChildren = root.GetComponentsInChildren<Outlinable>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				if (componentsInChildren[i] != null)
				{
					componentsInChildren[i].enabled = false;
				}
			}
		}
	}
}
