using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace Michsky.MUIP
{
	public class WindowManager : MonoBehaviour
	{
		[Serializable]
		public class WindowChangeEvent : UnityEvent<int>
		{
		}

		[Serializable]
		public class WindowItem
		{
			public string windowName = "My Window";

			public GameObject windowObject;

			public GameObject buttonObject;

			public GameObject firstSelected;
		}

		public List<WindowItem> windows = new List<WindowItem>();

		public int currentWindowIndex;

		private int currentButtonIndex;

		private int newWindowIndex;

		public bool cullWindows = true;

		public bool initializeButtons = true;

		private bool isInitialized;

		public WindowChangeEvent onWindowChange;

		private GameObject currentWindow;

		private GameObject nextWindow;

		private GameObject currentButton;

		private GameObject nextButton;

		private Animator currentWindowAnimator;

		private Animator nextWindowAnimator;

		private Animator currentButtonAnimator;

		private Animator nextButtonAnimator;

		private string windowFadeIn = "In";

		private string windowFadeOut = "Out";

		private string buttonFadeIn = "Hover to Pressed";

		private string buttonFadeOut = "Pressed to Normal";

		private float cachedStateLength;

		public bool altMode;

		private void Awake()
		{
			if (windows.Count != 0)
			{
				InitializeWindows();
			}
		}

		private void OnEnable()
		{
			if (isInitialized && nextWindowAnimator == null)
			{
				currentWindowAnimator.Play(windowFadeIn);
				if (currentButtonAnimator != null)
				{
					currentButtonAnimator.Play(buttonFadeIn);
				}
			}
			else if (isInitialized && nextWindowAnimator != null)
			{
				nextWindowAnimator.Play(windowFadeIn);
				if (nextButtonAnimator != null)
				{
					nextButtonAnimator.Play(buttonFadeIn);
				}
			}
		}

		public void InitializeWindows()
		{
			if (windows[currentWindowIndex].firstSelected != null)
			{
				EventSystem.current.firstSelectedGameObject = windows[currentWindowIndex].firstSelected;
			}
			if (windows[currentWindowIndex].buttonObject != null)
			{
				currentButton = windows[currentWindowIndex].buttonObject;
				currentButtonAnimator = currentButton.GetComponent<Animator>();
				currentButtonAnimator.Play(buttonFadeIn);
			}
			currentWindow = windows[currentWindowIndex].windowObject;
			currentWindowAnimator = currentWindow.GetComponent<Animator>();
			currentWindowAnimator.Play(windowFadeIn);
			onWindowChange.Invoke(currentWindowIndex);
			if (altMode)
			{
				cachedStateLength = 0.3f;
			}
			else
			{
				cachedStateLength = MUIPInternalTools.GetAnimatorClipLength(currentWindowAnimator, MUIPInternalTools.windowManagerStateName);
			}
			isInitialized = true;
			for (int i = 0; i < windows.Count; i++)
			{
				if (i != currentWindowIndex && cullWindows)
				{
					windows[i].windowObject.SetActive(value: false);
				}
				if (!(windows[i].buttonObject != null) || !initializeButtons)
				{
					continue;
				}
				string tempName = windows[i].windowName;
				ButtonManager component = windows[i].buttonObject.GetComponent<ButtonManager>();
				if (component != null)
				{
					component.onClick.RemoveAllListeners();
					component.onClick.AddListener(() =>
					{
						OpenPanel(tempName);
					});
				}
			}
		}

		public void OpenFirstTab()
		{
			if (currentWindowIndex != 0)
			{
				currentWindow = windows[currentWindowIndex].windowObject;
				currentWindowAnimator = currentWindow.GetComponent<Animator>();
				currentWindowAnimator.Play(windowFadeOut);
				if (windows[currentWindowIndex].buttonObject != null)
				{
					currentButton = windows[currentWindowIndex].buttonObject;
					currentButtonAnimator = currentButton.GetComponent<Animator>();
					currentButtonAnimator.Play(buttonFadeOut);
				}
				currentWindowIndex = 0;
				currentButtonIndex = 0;
				currentWindow = windows[currentWindowIndex].windowObject;
				currentWindowAnimator = currentWindow.GetComponent<Animator>();
				currentWindowAnimator.Play(windowFadeIn);
				if (windows[currentWindowIndex].firstSelected != null)
				{
					EventSystem.current.firstSelectedGameObject = windows[currentWindowIndex].firstSelected;
				}
				if (windows[currentButtonIndex].buttonObject != null)
				{
					currentButton = windows[currentButtonIndex].buttonObject;
					currentButtonAnimator = currentButton.GetComponent<Animator>();
					currentButtonAnimator.Play(buttonFadeIn);
				}
				onWindowChange.Invoke(currentWindowIndex);
			}
			else if (currentWindowIndex == 0)
			{
				currentWindow = windows[currentWindowIndex].windowObject;
				currentWindowAnimator = currentWindow.GetComponent<Animator>();
				currentWindowAnimator.Play(windowFadeIn);
				if (windows[currentWindowIndex].firstSelected != null)
				{
					EventSystem.current.firstSelectedGameObject = windows[currentWindowIndex].firstSelected;
				}
				if (windows[currentButtonIndex].buttonObject != null)
				{
					currentButton = windows[currentButtonIndex].buttonObject;
					currentButtonAnimator = currentButton.GetComponent<Animator>();
					currentButtonAnimator.Play(buttonFadeIn);
				}
			}
		}

		public void OpenWindow(string newWindow)
		{
			for (int i = 0; i < windows.Count; i++)
			{
				if (windows[i].windowName == newWindow)
				{
					newWindowIndex = i;
					break;
				}
			}
			if (newWindowIndex != currentWindowIndex)
			{
				if (cullWindows)
				{
					StopCoroutine("DisablePreviousWindow");
				}
				currentWindow = windows[currentWindowIndex].windowObject;
				if (windows[currentWindowIndex].buttonObject != null)
				{
					currentButton = windows[currentWindowIndex].buttonObject;
				}
				currentWindowIndex = newWindowIndex;
				nextWindow = windows[currentWindowIndex].windowObject;
				nextWindow.SetActive(value: true);
				currentWindowAnimator = currentWindow.GetComponent<Animator>();
				nextWindowAnimator = nextWindow.GetComponent<Animator>();
				currentWindowAnimator.Play(windowFadeOut);
				nextWindowAnimator.Play(windowFadeIn);
				if (cullWindows)
				{
					StartCoroutine("DisablePreviousWindow");
				}
				currentButtonIndex = newWindowIndex;
				if (windows[currentWindowIndex].firstSelected != null)
				{
					EventSystem.current.firstSelectedGameObject = windows[currentWindowIndex].firstSelected;
				}
				if (windows[currentButtonIndex].buttonObject != null)
				{
					nextButton = windows[currentButtonIndex].buttonObject;
					currentButtonAnimator = currentButton.GetComponent<Animator>();
					nextButtonAnimator = nextButton.GetComponent<Animator>();
					currentButtonAnimator.Play(buttonFadeOut);
					nextButtonAnimator.Play(buttonFadeIn);
				}
				onWindowChange.Invoke(currentWindowIndex);
			}
		}

		public void OpenPanel(string newPanel)
		{
			OpenWindow(newPanel);
		}

		public void OpenWindowByIndex(int windowIndex)
		{
			for (int i = 0; i < windows.Count; i++)
			{
				if (windows[i].windowName == windows[windowIndex].windowName)
				{
					OpenWindow(windows[windowIndex].windowName);
					break;
				}
			}
		}

		public void NextWindow()
		{
			if (currentWindowIndex <= windows.Count - 2)
			{
				if (cullWindows)
				{
					StopCoroutine("DisablePreviousWindow");
				}
				currentWindow = windows[currentWindowIndex].windowObject;
				currentWindow.gameObject.SetActive(value: true);
				if (windows[currentButtonIndex].buttonObject != null)
				{
					currentButton = windows[currentButtonIndex].buttonObject;
					nextButton = windows[currentButtonIndex + 1].buttonObject;
					currentButtonAnimator = currentButton.GetComponent<Animator>();
					currentButtonAnimator.Play(buttonFadeOut);
				}
				currentWindowAnimator = currentWindow.GetComponent<Animator>();
				currentWindowAnimator.Play(windowFadeOut);
				currentWindowIndex++;
				currentButtonIndex++;
				nextWindow = windows[currentWindowIndex].windowObject;
				nextWindow.gameObject.SetActive(value: true);
				nextWindowAnimator = nextWindow.GetComponent<Animator>();
				nextWindowAnimator.Play(windowFadeIn);
				if (cullWindows)
				{
					StartCoroutine("DisablePreviousWindow");
				}
				if (windows[currentWindowIndex].firstSelected != null)
				{
					EventSystem.current.firstSelectedGameObject = windows[currentWindowIndex].firstSelected;
				}
				if (nextButton != null)
				{
					nextButtonAnimator = nextButton.GetComponent<Animator>();
					nextButtonAnimator.Play(buttonFadeIn);
				}
				onWindowChange.Invoke(currentWindowIndex);
			}
		}

		public void PrevWindow()
		{
			if (currentWindowIndex >= 1)
			{
				if (cullWindows)
				{
					StopCoroutine("DisablePreviousWindow");
				}
				currentWindow = windows[currentWindowIndex].windowObject;
				currentWindow.gameObject.SetActive(value: true);
				if (windows[currentButtonIndex].buttonObject != null)
				{
					currentButton = windows[currentButtonIndex].buttonObject;
					nextButton = windows[currentButtonIndex - 1].buttonObject;
					currentButtonAnimator = currentButton.GetComponent<Animator>();
					currentButtonAnimator.Play(buttonFadeOut);
				}
				currentWindowAnimator = currentWindow.GetComponent<Animator>();
				currentWindowAnimator.Play(windowFadeOut);
				currentWindowIndex--;
				currentButtonIndex--;
				nextWindow = windows[currentWindowIndex].windowObject;
				nextWindow.gameObject.SetActive(value: true);
				nextWindowAnimator = nextWindow.GetComponent<Animator>();
				nextWindowAnimator.Play(windowFadeIn);
				if (cullWindows)
				{
					StartCoroutine("DisablePreviousWindow");
				}
				if (windows[currentWindowIndex].firstSelected != null)
				{
					EventSystem.current.firstSelectedGameObject = windows[currentWindowIndex].firstSelected;
				}
				if (nextButton != null)
				{
					nextButtonAnimator = nextButton.GetComponent<Animator>();
					nextButtonAnimator.Play(buttonFadeIn);
				}
				onWindowChange.Invoke(currentWindowIndex);
			}
		}

		public void ShowCurrentWindow()
		{
			if (nextWindowAnimator == null)
			{
				currentWindowAnimator.Play(windowFadeIn);
			}
			else
			{
				nextWindowAnimator.Play(windowFadeIn);
			}
		}

		public void HideCurrentWindow()
		{
			if (nextWindowAnimator == null)
			{
				currentWindowAnimator.Play(windowFadeOut);
			}
			else
			{
				nextWindowAnimator.Play(windowFadeOut);
			}
		}

		public void ShowCurrentButton()
		{
			if (nextButtonAnimator == null)
			{
				currentButtonAnimator.Play(buttonFadeIn);
			}
			else
			{
				nextButtonAnimator.Play(buttonFadeIn);
			}
		}

		public void HideCurrentButton()
		{
			if (nextButtonAnimator == null)
			{
				currentButtonAnimator.Play(buttonFadeOut);
			}
			else
			{
				nextButtonAnimator.Play(buttonFadeOut);
			}
		}

		public void AddNewItem()
		{
			WindowItem windowItem = new WindowItem();
			if (windows.Count != 0 && windows[windows.Count - 1].windowObject != null)
			{
				int index = windows.Count - 1;
				GameObject gameObject = UnityEngine.Object.Instantiate(windows[index].windowObject.transform.parent.GetChild(index).gameObject, new Vector3(0f, 0f, 0f), Quaternion.identity);
				gameObject.transform.SetParent(windows[index].windowObject.transform.parent, worldPositionStays: false);
				gameObject.gameObject.name = "New Window " + index;
				windowItem.windowName = "New Window " + index;
				windowItem.windowObject = gameObject;
				if (windows[index].buttonObject != null)
				{
					GameObject gameObject2 = UnityEngine.Object.Instantiate(windows[index].buttonObject.transform.parent.GetChild(index).gameObject, new Vector3(0f, 0f, 0f), Quaternion.identity);
					gameObject2.transform.SetParent(windows[index].buttonObject.transform.parent, worldPositionStays: false);
					gameObject2.gameObject.name = "New Window " + index;
					windowItem.buttonObject = gameObject2;
				}
			}
			windows.Add(windowItem);
		}

		private IEnumerator DisablePreviousWindow()
		{
			yield return new WaitForSecondsRealtime(cachedStateLength);
			for (int i = 0; i < windows.Count; i++)
			{
				if (i != currentWindowIndex)
				{
					windows[i].windowObject.SetActive(value: false);
				}
			}
		}
	}
}
