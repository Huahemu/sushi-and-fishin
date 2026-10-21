using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Michsky.LSS.Demo
{
	public class DemoListShadow : MonoBehaviour, IBeginDragHandler, IEventSystemHandler
	{
		[Header("Resources")]
		[SerializeField]
		private Scrollbar listScrollbar;

		[SerializeField]
		private CanvasGroup leftCG;

		[SerializeField]
		private CanvasGroup rightCG;

		[Header("Settings")]
		[SerializeField]
		private float scrollTime = 5f;

		[SerializeField]
		private float transitionSpeed = 4f;

		private void Start()
		{
			CheckForValue(0f);
		}

		public void CheckForValue(float value)
		{
			if ((double)value > 0.03)
			{
				StopCoroutine("LeftCGFadeOut");
				StartCoroutine("LeftCGFadeIn");
			}
			else
			{
				StopCoroutine("LeftCGFadeIn");
				StartCoroutine("LeftCGFadeOut");
			}
			if ((double)value < 0.97)
			{
				StopCoroutine("RightCGFadeOut");
				StartCoroutine("RightCGFadeIn");
			}
			else
			{
				StopCoroutine("RightCGFadeIn");
				StartCoroutine("RightCGFadeOut");
			}
		}

		public void ScrollUp()
		{
			StopCoroutine("ScrollDownHelper");
			StartCoroutine("ScrollUpHelper");
		}

		public void ScrollDown()
		{
			StopCoroutine("ScrollUpHelper");
			StartCoroutine("ScrollDownHelper");
		}

		public void OnBeginDrag(PointerEventData data)
		{
			StopCoroutine("ScrollUpHelper");
			StopCoroutine("ScrollDownHelper");
		}

		private IEnumerator ScrollUpHelper()
		{
			float elapsedTime = 0f;
			while (elapsedTime < scrollTime)
			{
				listScrollbar.value = Mathf.Lerp(listScrollbar.value, 0f, elapsedTime / scrollTime);
				elapsedTime += Time.unscaledDeltaTime;
				yield return null;
			}
		}

		private IEnumerator ScrollDownHelper()
		{
			float elapsedTime = 0f;
			while (elapsedTime < scrollTime)
			{
				listScrollbar.value = Mathf.Lerp(listScrollbar.value, 1f, elapsedTime / scrollTime);
				elapsedTime += Time.unscaledDeltaTime;
				yield return null;
			}
		}

		private IEnumerator LeftCGFadeIn()
		{
			leftCG.interactable = true;
			leftCG.blocksRaycasts = true;
			while (leftCG.alpha < 0.99f)
			{
				leftCG.alpha += Time.unscaledDeltaTime * transitionSpeed;
				yield return null;
			}
			leftCG.alpha = 1f;
		}

		private IEnumerator LeftCGFadeOut()
		{
			leftCG.interactable = false;
			leftCG.blocksRaycasts = false;
			while (leftCG.alpha > 0.01f)
			{
				leftCG.alpha -= Time.unscaledDeltaTime * transitionSpeed;
				yield return null;
			}
			leftCG.alpha = 0f;
		}

		private IEnumerator RightCGFadeIn()
		{
			rightCG.interactable = true;
			rightCG.blocksRaycasts = true;
			while (rightCG.alpha < 0.99f)
			{
				rightCG.alpha += Time.unscaledDeltaTime * transitionSpeed;
				yield return null;
			}
			rightCG.alpha = 1f;
		}

		private IEnumerator RightCGFadeOut()
		{
			rightCG.interactable = false;
			rightCG.blocksRaycasts = false;
			while (rightCG.alpha > 0.01f)
			{
				rightCG.alpha -= Time.unscaledDeltaTime * transitionSpeed;
				yield return null;
			}
			rightCG.alpha = 0f;
		}
	}
}
