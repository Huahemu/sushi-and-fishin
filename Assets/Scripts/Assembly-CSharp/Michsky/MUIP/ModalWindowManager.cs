using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Michsky.MUIP
{
	[RequireComponent(typeof(CanvasGroup))]
	public class ModalWindowManager : MonoBehaviour
	{
		public enum StartBehaviour
		{
			None = 0,
			Disable = 1,
			Enable = 2
		}

		public enum CloseBehaviour
		{
			None = 0,
			Disable = 1,
			Destroy = 2
		}

		public enum OnEnableBehaviour
		{
			None = 0,
			Restore = 1
		}

		public Image windowIcon;

		public TextMeshProUGUI windowTitle;

		public TextMeshProUGUI windowDescription;

		public ButtonManager confirmButton;

		public ButtonManager cancelButton;

		public Animator mwAnimator;

		public Sprite icon;

		public string titleText = "Title";

		[TextArea(1, 4)]
		public string descriptionText = "Description here";

		public UnityEvent onOpen = new UnityEvent();

		public UnityEvent onClose = new UnityEvent();

		public UnityEvent onConfirm = new UnityEvent();

		public UnityEvent onCancel = new UnityEvent();

		public bool useCustomContent;

		public bool isOn;

		public bool closeOnCancel = true;

		public bool closeOnConfirm = true;

		public bool showCancelButton = true;

		public bool showConfirmButton = true;

		public StartBehaviour startBehaviour = StartBehaviour.Disable;

		public CloseBehaviour closeBehaviour = CloseBehaviour.Disable;

		public OnEnableBehaviour onEnableBehaviour;

		private float cachedStateLength;

		private void Awake()
		{
			isOn = false;
			if (mwAnimator == null)
			{
				mwAnimator = base.gameObject.GetComponent<Animator>();
			}
			if (closeOnCancel)
			{
				onCancel.AddListener(CloseWindow);
			}
			if (closeOnConfirm)
			{
				onConfirm.AddListener(CloseWindow);
			}
			if (confirmButton != null)
			{
				confirmButton.onClick.AddListener(onConfirm.Invoke);
			}
			if (cancelButton != null)
			{
				cancelButton.onClick.AddListener(onCancel.Invoke);
			}
			if (startBehaviour == StartBehaviour.Disable)
			{
				isOn = false;
				base.gameObject.SetActive(value: false);
			}
			else if (startBehaviour == StartBehaviour.Enable)
			{
				isOn = false;
				OpenWindow();
			}
			cachedStateLength = MUIPInternalTools.GetAnimatorClipLength(mwAnimator, MUIPInternalTools.modalWindowStateName);
			UpdateUI();
		}

		private void OnEnable()
		{
			if (onEnableBehaviour == OnEnableBehaviour.Restore && isOn)
			{
				isOn = false;
				Open();
			}
		}

		private void OnDisable()
		{
			if (onEnableBehaviour == OnEnableBehaviour.None)
			{
				isOn = false;
			}
		}

		public void UpdateUI()
		{
			if (!useCustomContent)
			{
				if (windowIcon != null)
				{
					windowIcon.sprite = icon;
				}
				if (windowTitle != null)
				{
					windowTitle.text = titleText;
				}
				if (windowDescription != null)
				{
					windowDescription.text = descriptionText;
				}
				if (showCancelButton && cancelButton != null)
				{
					cancelButton.gameObject.SetActive(value: true);
				}
				else if (cancelButton != null)
				{
					cancelButton.gameObject.SetActive(value: false);
				}
				if (showConfirmButton && confirmButton != null)
				{
					confirmButton.gameObject.SetActive(value: true);
				}
				else if (confirmButton != null)
				{
					confirmButton.gameObject.SetActive(value: false);
				}
			}
		}

		public void Open()
		{
			if (!isOn)
			{
				isOn = true;
				base.gameObject.SetActive(value: true);
				onOpen.Invoke();
				StopCoroutine("DisableObject");
				mwAnimator.Play("Fade-in");
			}
		}

		public void Close()
		{
			if (isOn)
			{
				isOn = false;
				onClose.Invoke();
				mwAnimator.Play("Fade-out");
				StartCoroutine("DisableObject");
			}
		}

		public void OpenWindow()
		{
			Open();
		}

		public void CloseWindow()
		{
			Close();
		}

		public void AnimateWindow()
		{
			if (!isOn)
			{
				StopCoroutine("DisableObject");
				isOn = true;
				base.gameObject.SetActive(value: true);
				mwAnimator.Play("Fade-in");
			}
			else
			{
				isOn = false;
				mwAnimator.Play("Fade-out");
				StartCoroutine("DisableObject");
			}
		}

		private IEnumerator DisableObject()
		{
			yield return new WaitForSecondsRealtime(cachedStateLength);
			if (closeBehaviour == CloseBehaviour.Disable)
			{
				base.gameObject.SetActive(value: false);
			}
			else if (closeBehaviour == CloseBehaviour.Destroy)
			{
				Object.Destroy(base.gameObject);
			}
		}
	}
}
