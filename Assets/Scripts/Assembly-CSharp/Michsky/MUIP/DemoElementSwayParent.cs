using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Michsky.MUIP
{
	public class DemoElementSwayParent : MonoBehaviour
	{
		[SerializeField]
		private Animator titleAnimator;

		[SerializeField]
		private TextMeshProUGUI elementTitle;

		[SerializeField]
		private TextMeshProUGUI elementTitleHelper;

		private List<DemoElementSway> elements = new List<DemoElementSway>();

		private int prevIndex;

		private void Awake()
		{
			foreach (Transform item in base.transform)
			{
				elements.Add(item.GetComponent<DemoElementSway>());
			}
		}

		public void DissolveAll(DemoElementSway currentSway)
		{
			for (int i = 0; i < elements.Count; i++)
			{
				if (elements[i] == currentSway)
				{
					elements[i].Active();
				}
				else
				{
					elements[i].Dissolve();
				}
			}
		}

		public void HighlightAll()
		{
			for (int i = 0; i < elements.Count; i++)
			{
				elements[i].Highlight();
			}
		}

		public void SetWindowManagerButton(int index)
		{
			if (elements.Count == 0)
			{
				StartCoroutine("SWMHelper", index);
				return;
			}
			for (int i = 0; i < elements.Count; i++)
			{
				if (i == index)
				{
					elements[i].WindowManagerSelect();
				}
				else if (elements[i].wmSelected)
				{
					elements[i].WindowManagerDeselect();
				}
			}
			if (!(titleAnimator == null))
			{
				elementTitleHelper.text = elements[prevIndex].gameObject.name;
				elementTitle.text = elements[index].gameObject.name;
				titleAnimator.Play("Idle");
				titleAnimator.Play("Transition");
				prevIndex = index;
			}
		}

		private IEnumerator SWMHelper(int index)
		{
			yield return new WaitForSeconds(0.1f);
			SetWindowManagerButton(index);
		}
	}
}
