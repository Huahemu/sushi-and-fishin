using UnityEngine;
using UnityEngine.UI;

namespace Michsky.MUIP
{
	public class Ripple : MonoBehaviour
	{
		public bool unscaledTime;

		public float speed;

		public float maxSize;

		public Color startColor;

		public Color transitionColor;

		private Image colorImg;

		private void Start()
		{
			base.transform.localScale = new Vector3(0f, 0f, 0f);
			colorImg = GetComponent<Image>();
			colorImg.raycastTarget = false;
			colorImg.color = new Color(startColor.r, startColor.g, startColor.b, startColor.a);
		}

		private void Update()
		{
			if (!unscaledTime)
			{
				base.transform.localScale = Vector3.Lerp(base.transform.localScale, new Vector3(maxSize, maxSize, maxSize), Time.deltaTime * speed);
				colorImg.color = Color.Lerp(colorImg.color, new Color(transitionColor.r, transitionColor.g, transitionColor.b, transitionColor.a), Time.deltaTime * speed);
				if ((double)base.transform.localScale.x >= (double)maxSize * 0.998)
				{
					if (base.transform.parent.childCount == 1)
					{
						base.transform.parent.gameObject.SetActive(value: false);
					}
					Object.Destroy(base.gameObject);
				}
				return;
			}
			base.transform.localScale = Vector3.Lerp(base.transform.localScale, new Vector3(maxSize, maxSize, maxSize), Time.unscaledDeltaTime * speed);
			colorImg.color = Color.Lerp(colorImg.color, new Color(transitionColor.r, transitionColor.g, transitionColor.b, transitionColor.a), Time.unscaledDeltaTime * speed);
			if ((double)base.transform.localScale.x >= (double)maxSize * 0.998)
			{
				if (base.transform.parent.childCount == 1)
				{
					base.transform.parent.gameObject.SetActive(value: false);
				}
				Object.Destroy(base.gameObject);
			}
		}
	}
}
