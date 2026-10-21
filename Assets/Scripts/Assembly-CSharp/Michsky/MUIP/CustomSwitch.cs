using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Michsky.MUIP
{
	[RequireComponent(typeof(Toggle))]
	[RequireComponent(typeof(Animator))]
	public class CustomSwitch : MonoBehaviour
	{
		public Toggle toggleObject;

		public Animator toggleAnimator;

		[Header("Settings")]
		public bool invokeOnAwake;

		private bool isInitialized;

		private void Awake()
		{
			if (toggleObject == null)
			{
				toggleObject = base.gameObject.GetComponent<Toggle>();
			}
			if (toggleAnimator == null)
			{
				toggleAnimator = toggleObject.GetComponent<Animator>();
			}
			if (invokeOnAwake)
			{
				toggleObject.onValueChanged.Invoke(toggleObject.isOn);
			}
			toggleObject.onValueChanged.AddListener(UpdateState);
			UpdateState();
			isInitialized = true;
		}

		private void OnEnable()
		{
			if (isInitialized)
			{
				UpdateState();
			}
		}

		public void UpdateState()
		{
			if (base.gameObject.activeInHierarchy)
			{
				StopCoroutine("DisableAnimator");
				StartCoroutine("DisableAnimator");
				toggleAnimator.enabled = true;
				if (toggleObject.isOn)
				{
					toggleAnimator.Play("On Instant");
				}
				else
				{
					toggleAnimator.Play("Off Instant");
				}
			}
		}

		public void UpdateState(bool value)
		{
			if (base.gameObject.activeInHierarchy)
			{
				StopCoroutine("DisableAnimator");
				StartCoroutine("DisableAnimator");
				toggleAnimator.enabled = true;
				if (toggleObject.isOn)
				{
					toggleAnimator.Play("Switch On");
				}
				else
				{
					toggleAnimator.Play("Switch Off");
				}
			}
		}

		private IEnumerator DisableAnimator()
		{
			yield return new WaitForSecondsRealtime(0.6f);
			toggleAnimator.enabled = false;
		}
	}
}
