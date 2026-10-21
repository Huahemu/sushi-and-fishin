using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Michsky.MUIP
{
	[RequireComponent(typeof(Animator))]
	[RequireComponent(typeof(Button))]
	public class SwitchManager : MonoBehaviour, IPointerEnterHandler, IEventSystemHandler
	{
		[Serializable]
		public class SwitchEvent : UnityEvent<bool>
		{
		}

		[SerializeField]
		public SwitchEvent onValueChanged = new SwitchEvent();

		public UnityEvent OnEvents = new UnityEvent();

		public UnityEvent OffEvents = new UnityEvent();

		public bool saveValue = true;

		public string switchTag = "Switch";

		public bool isOn = true;

		public bool invokeAtStart = true;

		public bool enableSwitchSounds;

		public bool useHoverSound = true;

		public bool useClickSound = true;

		public Animator switchAnimator;

		public Button switchButton;

		public AudioSource soundSource;

		public AudioClip hoverSound;

		public AudioClip clickSound;

		private bool isInitialized;

		private void Awake()
		{
			if (switchAnimator == null)
			{
				switchAnimator = base.gameObject.GetComponent<Animator>();
			}
			if (switchButton == null)
			{
				switchButton = base.gameObject.GetComponent<Button>();
				switchButton.onClick.AddListener(AnimateSwitch);
				if (enableSwitchSounds && useClickSound)
				{
					switchButton.onClick.AddListener(() =>
					{
						soundSource.PlayOneShot(clickSound);
					});
				}
			}
			if (saveValue)
			{
				GetSavedData();
			}
			else
			{
				if (base.gameObject.activeInHierarchy)
				{
					StopCoroutine("DisableAnimator");
				}
				if (base.gameObject.activeInHierarchy)
				{
					StartCoroutine("DisableAnimator");
				}
				switchAnimator.enabled = true;
				if (isOn)
				{
					switchAnimator.Play("On Instant");
				}
				else
				{
					switchAnimator.Play("Off Instant");
				}
			}
			if (invokeAtStart && isOn)
			{
				OnEvents.Invoke();
			}
			else if (invokeAtStart && !isOn)
			{
				OffEvents.Invoke();
			}
			isInitialized = true;
		}

		private void OnEnable()
		{
			if (isInitialized)
			{
				UpdateUI();
			}
		}

		private void GetSavedData()
		{
			if (base.gameObject.activeInHierarchy)
			{
				StopCoroutine("DisableAnimator");
				StartCoroutine("DisableAnimator");
			}
			switchAnimator.enabled = true;
			if (PlayerPrefs.GetString(switchTag + "Switch") == "" || !PlayerPrefs.HasKey(switchTag + "Switch"))
			{
				if (isOn)
				{
					switchAnimator.Play("Switch On");
					PlayerPrefs.SetString(switchTag + "Switch", "true");
				}
				else
				{
					switchAnimator.Play("Switch Off");
					PlayerPrefs.SetString(switchTag + "Switch", "false");
				}
			}
			else if (PlayerPrefs.GetString(switchTag + "Switch") == "true")
			{
				switchAnimator.Play("Switch On");
				isOn = true;
			}
			else if (PlayerPrefs.GetString(switchTag + "Switch") == "false")
			{
				switchAnimator.Play("Switch Off");
				isOn = false;
			}
		}

		public void AnimateSwitch()
		{
			if (base.gameObject.activeInHierarchy)
			{
				StopCoroutine("DisableAnimator");
				StartCoroutine("DisableAnimator");
			}
			switchAnimator.enabled = true;
			if (isOn)
			{
				isOn = false;
				switchAnimator.Play("Switch Off");
				OffEvents.Invoke();
				if (saveValue)
				{
					PlayerPrefs.SetString(switchTag + "Switch", "false");
				}
			}
			else
			{
				isOn = true;
				switchAnimator.Play("Switch On");
				OnEvents.Invoke();
				if (saveValue)
				{
					PlayerPrefs.SetString(switchTag + "Switch", "true");
				}
			}
			onValueChanged.Invoke(isOn);
		}

		public void SetOn()
		{
			if (saveValue)
			{
				PlayerPrefs.SetString(switchTag + "Switch", "true");
			}
			if (base.gameObject.activeInHierarchy)
			{
				StopCoroutine("DisableAnimator");
				StartCoroutine("DisableAnimator");
			}
			isOn = true;
			switchAnimator.enabled = true;
			switchAnimator.Play("Switch On");
			OnEvents.Invoke();
			onValueChanged.Invoke(arg0: true);
		}

		public void SetOff()
		{
			if (saveValue)
			{
				PlayerPrefs.SetString(switchTag + "Switch", "false");
			}
			if (base.gameObject.activeInHierarchy)
			{
				StopCoroutine("DisableAnimator");
				StartCoroutine("DisableAnimator");
			}
			isOn = false;
			switchAnimator.enabled = true;
			switchAnimator.Play("Switch Off");
			OffEvents.Invoke();
			onValueChanged.Invoke(arg0: false);
		}

		public void UpdateUI()
		{
			if (base.gameObject.activeInHierarchy)
			{
				StopCoroutine("DisableAnimator");
				StartCoroutine("DisableAnimator");
			}
			switchAnimator.enabled = true;
			if (isOn && switchAnimator.gameObject.activeInHierarchy)
			{
				switchAnimator.Play("On Instant");
			}
			else if (!isOn && switchAnimator.gameObject.activeInHierarchy)
			{
				switchAnimator.Play("Off Instant");
			}
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			if (enableSwitchSounds && useHoverSound && switchButton.interactable)
			{
				soundSource.PlayOneShot(hoverSound);
			}
		}

		private IEnumerator DisableAnimator()
		{
			yield return new WaitForSecondsRealtime(0.5f);
			switchAnimator.enabled = false;
		}
	}
}
