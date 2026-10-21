using UnityEngine;
using UnityEngine.EventSystems;

namespace Bozo.ModularCharacters
{
	public class Drawer : MonoBehaviour, IDeselectHandler, IEventSystemHandler
	{
		public RectTransform scroll;

		private Vector3 target;

		private float dur = 1f;

		private float time = 1f;

		private void OnEnable()
		{
			scroll.localPosition = new Vector3(1000f, scroll.localPosition.y, scroll.localPosition.z);
		}

		public void Open()
		{
			target = new Vector3(0f, scroll.localPosition.y, scroll.localPosition.z);
			time = 0f;
		}

		public void Close()
		{
			target = new Vector3(scroll.rect.width, scroll.localPosition.y, scroll.localPosition.z);
			time = 0f;
		}

		private void Update()
		{
			if (time < dur)
			{
				float t = time / dur;
				time += Time.deltaTime;
				scroll.localPosition = Vector3.Lerp(scroll.localPosition, target, t);
			}
		}

		public void OnDeselect(BaseEventData eventData)
		{
			Invoke("Close", 0.2f);
		}
	}
}
