using UnityEngine;
using UnityEngine.UI;

namespace Michsky.MUIP
{
	[ExecuteInEditMode]
	public class UIManagerScrollbar : MonoBehaviour
	{
		[Header("Settings")]
		[SerializeField]
		private UIManager UIManagerAsset;

		[Header("Resources")]
		[SerializeField]
		private Image background;

		[SerializeField]
		private Image bar;

		private void Awake()
		{
			if (UIManagerAsset == null)
			{
				UIManagerAsset = Resources.Load<UIManager>("MUIP Manager");
			}
			base.enabled = true;
			if (!UIManagerAsset.enableDynamicUpdate)
			{
				UpdateScrollbar();
				base.enabled = false;
			}
		}

		private void Update()
		{
			if (!(UIManagerAsset == null) && UIManagerAsset.enableDynamicUpdate)
			{
				UpdateScrollbar();
			}
		}

		private void UpdateScrollbar()
		{
			background.color = UIManagerAsset.scrollbarBackgroundColor;
			bar.color = UIManagerAsset.scrollbarColor;
		}
	}
}
