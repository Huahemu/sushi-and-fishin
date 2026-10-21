using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Michsky.MUIP
{
	[ExecuteInEditMode]
	[DisallowMultipleComponent]
	[AddComponentMenu("Modern UI Pack/Layout/Layout Group Fix")]
	public class LayoutGroupFix : MonoBehaviour
	{
		[SerializeField]
		private bool fixOnEnable = true;

		[SerializeField]
		private bool fixWithDelay = true;

		private float fixDelay = 0.025f;

		private void OnEnable()
		{
			if (!fixWithDelay && fixOnEnable)
			{
				LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
			}
			else if (fixWithDelay)
			{
				StartCoroutine(FixDelay());
			}
		}

		public void FixLayout()
		{
			if (!fixWithDelay)
			{
				LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
			}
			else
			{
				StartCoroutine(FixDelay());
			}
		}

		private IEnumerator FixDelay()
		{
			yield return new WaitForSecondsRealtime(fixDelay);
			LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
		}
	}
}
