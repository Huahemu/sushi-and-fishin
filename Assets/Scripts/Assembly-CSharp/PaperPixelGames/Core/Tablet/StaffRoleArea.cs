using UnityEngine;

namespace PaperPixelGames.Core.Tablet
{
	public sealed class StaffRoleArea : MonoBehaviour
	{
		public GameObject Empty;

		private void OnEnable()
		{
			RefreshEmpty();
		}

		private void OnTransformChildrenChanged()
		{
			RefreshEmpty();
		}

		public void RefreshEmpty()
		{
			if (Empty == null)
			{
				return;
			}
			bool flag = false;
			for (int i = 0; i < base.transform.childCount; i++)
			{
				if (base.transform.GetChild(i).gameObject.activeSelf)
				{
					flag = true;
					break;
				}
			}
			Empty.SetActive(!flag);
		}
	}
}
