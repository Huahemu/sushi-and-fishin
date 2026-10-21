using UnityEngine;
using UnityEngine.EventSystems;

namespace Michsky.MUIP
{
	[RequireComponent(typeof(Animator))]
	public class WindowManagerButton : MonoBehaviour, IPointerEnterHandler, IEventSystemHandler, IPointerExitHandler
	{
		public bool enableMobileMode;

		[HideInInspector]
		public Animator buttonAnimator;

		private void Awake()
		{
			if (buttonAnimator == null)
			{
				buttonAnimator = base.gameObject.GetComponent<Animator>();
			}
			if (Application.platform == RuntimePlatform.IPhonePlayer || Application.platform == RuntimePlatform.Android)
			{
				enableMobileMode = true;
			}
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			if (!enableMobileMode && !buttonAnimator.GetCurrentAnimatorStateInfo(0).IsName("Hover to Pressed") && !buttonAnimator.GetCurrentAnimatorStateInfo(0).IsName("Normal to Pressed"))
			{
				buttonAnimator.Play("Normal to Hover");
			}
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			if (!enableMobileMode && !buttonAnimator.GetCurrentAnimatorStateInfo(0).IsName("Hover to Pressed") && !buttonAnimator.GetCurrentAnimatorStateInfo(0).IsName("Normal to Pressed"))
			{
				buttonAnimator.Play("Hover to Normal");
			}
		}
	}
}
