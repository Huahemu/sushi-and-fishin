using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace Michsky.LSS.Demo
{
	public class DemoElementSway : MonoBehaviour, IPointerEnterHandler, IEventSystemHandler, IPointerExitHandler, IPointerClickHandler
	{
		[Header("Resources")]
		[SerializeField]
		private DemoElementSwayParent swayParent;

		[SerializeField]
		private Canvas mainCanvas;

		[SerializeField]
		private RectTransform swayObject;

		[SerializeField]
		private CanvasGroup normalCG;

		[SerializeField]
		private CanvasGroup highlightedCG;

		[SerializeField]
		private CanvasGroup selectedCG;

		[Header("Settings")]
		[SerializeField]
		private float smoothness = 10f;

		[SerializeField]
		private float transitionSpeed = 8f;

		[SerializeField]
		[Range(0f, 1f)]
		private float dissolveAlpha = 0.5f;

		[Header("Events")]
		[SerializeField]
		private UnityEvent onClick;

		private bool allowSway;

		[HideInInspector]
		public bool wmSelected;

		private Vector3 cursorPos;

		private Vector2 defaultPos;

		private void Awake()
		{
			if (swayParent == null)
			{
				DemoElementSwayParent component = base.transform.parent.GetComponent<DemoElementSwayParent>();
				if (component == null)
				{
					base.transform.parent.gameObject.AddComponent<DemoElementSwayParent>();
				}
				swayParent = component;
			}
			defaultPos = swayObject.anchoredPosition;
			normalCG.alpha = 1f;
			highlightedCG.alpha = 0f;
		}

		private void Update()
		{
			if (allowSway)
			{
				cursorPos = Input.mousePosition;
			}
			if (mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
			{
				ProcessOverlay();
			}
			else if (mainCanvas.renderMode == RenderMode.ScreenSpaceCamera)
			{
				ProcessSSC();
			}
			else if (mainCanvas.renderMode == RenderMode.WorldSpace)
			{
				ProcessWorldSpace();
			}
		}

		private void ProcessOverlay()
		{
			if (allowSway)
			{
				swayObject.position = Vector2.Lerp(swayObject.position, cursorPos, Time.deltaTime * smoothness);
			}
			else
			{
				swayObject.localPosition = Vector2.Lerp(swayObject.localPosition, defaultPos, Time.deltaTime * smoothness);
			}
		}

		private void ProcessSSC()
		{
			if (allowSway)
			{
				swayObject.position = Vector2.Lerp(swayObject.position, Camera.main.ScreenToWorldPoint(cursorPos), Time.deltaTime * smoothness);
			}
			else
			{
				swayObject.localPosition = Vector2.Lerp(swayObject.localPosition, defaultPos, Time.deltaTime * smoothness);
			}
		}

		private void ProcessWorldSpace()
		{
			if (allowSway)
			{
				Vector3 position = new Vector3(cursorPos.x, cursorPos.y, mainCanvas.transform.position.z / 6f);
				swayObject.position = Vector3.Lerp(swayObject.position, Camera.main.ScreenToWorldPoint(position), Time.deltaTime * smoothness);
			}
			else
			{
				swayObject.localPosition = Vector3.Lerp(swayObject.localPosition, defaultPos, Time.deltaTime * smoothness);
			}
		}

		public void Dissolve()
		{
			if (!wmSelected)
			{
				StopCoroutine("DissolveHelper");
				StopCoroutine("HighlightHelper");
				StopCoroutine("ActiveHelper");
				StartCoroutine("DissolveHelper");
			}
		}

		public void Highlight()
		{
			if (!wmSelected)
			{
				StopCoroutine("DissolveHelper");
				StopCoroutine("HighlightHelper");
				StopCoroutine("ActiveHelper");
				StartCoroutine("HighlightHelper");
			}
		}

		public void Active()
		{
			if (!wmSelected)
			{
				StopCoroutine("DissolveHelper");
				StopCoroutine("HighlightHelper");
				StopCoroutine("HighlightHelper");
				StartCoroutine("ActiveHelper");
			}
		}

		public void WindowManagerSelect()
		{
			wmSelected = true;
			StopCoroutine("ActiveHelper");
			StopCoroutine("HighlightHelper");
			StartCoroutine("WMSelectHelper");
		}

		public void WindowManagerDeselect()
		{
			wmSelected = false;
			StartCoroutine("WMDeselectHelper");
			StartCoroutine("DissolveHelper");
		}

		public void OnPointerEnter(PointerEventData data)
		{
			allowSway = true;
			swayParent.DissolveAll(this);
		}

		public void OnPointerExit(PointerEventData data)
		{
			allowSway = false;
			swayParent.HighlightAll();
		}

		public void OnPointerClick(PointerEventData data)
		{
			onClick.Invoke();
		}

		private IEnumerator DissolveHelper()
		{
			while (normalCG.alpha > dissolveAlpha)
			{
				normalCG.alpha -= Time.unscaledDeltaTime * transitionSpeed;
				highlightedCG.alpha -= Time.unscaledDeltaTime * transitionSpeed;
				yield return null;
			}
			highlightedCG.alpha = 0f;
			normalCG.alpha = dissolveAlpha;
			highlightedCG.gameObject.SetActive(value: false);
		}

		private IEnumerator HighlightHelper()
		{
			while (normalCG.alpha < 1f)
			{
				normalCG.alpha += Time.unscaledDeltaTime * transitionSpeed;
				highlightedCG.alpha -= Time.unscaledDeltaTime * transitionSpeed;
				yield return null;
			}
			normalCG.alpha = 1f;
			highlightedCG.alpha = 0f;
			highlightedCG.gameObject.SetActive(value: false);
		}

		private IEnumerator ActiveHelper()
		{
			highlightedCG.gameObject.SetActive(value: true);
			while (highlightedCG.alpha < 1f)
			{
				normalCG.alpha -= Time.unscaledDeltaTime * transitionSpeed;
				highlightedCG.alpha += Time.unscaledDeltaTime * transitionSpeed;
				yield return null;
			}
			highlightedCG.alpha = 1f;
			normalCG.alpha = 0f;
		}

		private IEnumerator WMSelectHelper()
		{
			selectedCG.gameObject.SetActive(value: true);
			while (selectedCG.alpha < 1f)
			{
				normalCG.alpha -= Time.unscaledDeltaTime * transitionSpeed;
				highlightedCG.alpha -= Time.unscaledDeltaTime * transitionSpeed;
				selectedCG.alpha += Time.unscaledDeltaTime * transitionSpeed;
				yield return null;
			}
			highlightedCG.alpha = 0f;
			normalCG.alpha = 0f;
			selectedCG.alpha = 1f;
		}

		private IEnumerator WMDeselectHelper()
		{
			while (selectedCG.alpha > 0f)
			{
				selectedCG.alpha -= Time.unscaledDeltaTime * transitionSpeed;
				yield return null;
			}
			selectedCG.alpha = 0f;
			selectedCG.gameObject.SetActive(value: false);
		}
	}
}
