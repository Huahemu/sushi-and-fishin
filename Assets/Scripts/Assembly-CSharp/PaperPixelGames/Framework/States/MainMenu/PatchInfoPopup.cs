using System.Collections;
using PaperPixelGames.Framework.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperPixelGames.Framework.States.MainMenu
{
	public sealed class PatchInfoPopup : MonoBehaviour
	{
		public const string ResourcePath = "PatchInfoPopup";

		public const string CurrentUpdateId = "lobby-staff-boss-v1";

		public const string AcknowledgementKey = "PatchInfo.Seen.lobby-staff-boss-v1";

		[SerializeField]
		private BaseButton _continueButton;

		[SerializeField]
		private BaseButton _closeButton;

		[Header("Screenshots — assign a Sprite to each Image's Source Image in the prefab")]
		[SerializeField]
		private Image _lobbyScreenshot;

		[SerializeField]
		private Image _staffScreenshot;

		[SerializeField]
		private Image _bossScreenshot;

		private GameObject _previousSelection;

		private bool _bound;

		public bool IsVisible => base.gameObject.activeInHierarchy;

		public bool TryShow(GameObject fallbackSelection = null)
		{
			if (PlayerPrefs.GetInt("PatchInfo.Seen.lobby-staff-boss-v1", 0) == 1)
			{
				return false;
			}
			if (IsVisible)
			{
				return true;
			}
			if (_continueButton == null || _closeButton == null)
			{
				Debug.LogError("[PatchInfoPopup] The popup prefab has missing button references.", this);
				return false;
			}
			Bind();
			_previousSelection = ((EventSystem.current != null) ? EventSystem.current.currentSelectedGameObject : null);
			if (_previousSelection == null || !_previousSelection.activeInHierarchy)
			{
				_previousSelection = fallbackSelection;
			}
			RefreshScreenshot(_lobbyScreenshot);
			RefreshScreenshot(_staffScreenshot);
			RefreshScreenshot(_bossScreenshot);
			base.gameObject.SetActive(value: true);
			StartCoroutine(FocusContinue());
			return true;
		}

		private void Bind()
		{
			if (!_bound)
			{
				_bound = true;
				_continueButton.Clicked += Dismiss;
				_closeButton.Clicked += Dismiss;
			}
		}

		private static void RefreshScreenshot(Image image)
		{
			if (image != null)
			{
				image.enabled = image.sprite != null;
			}
		}

		private IEnumerator FocusContinue()
		{
			yield return null;
			if (IsVisible && EventSystem.current != null)
			{
				EventSystem.current.SetSelectedGameObject(_continueButton.gameObject);
			}
		}

		public void Dismiss()
		{
			if (IsVisible)
			{
				PlayerPrefs.SetInt("PatchInfo.Seen.lobby-staff-boss-v1", 1);
				PlayerPrefs.Save();
				RestoreSelection();
				base.gameObject.SetActive(value: false);
			}
		}

		public void Suspend()
		{
			if (IsVisible)
			{
				RestoreSelection();
				base.gameObject.SetActive(value: false);
			}
		}

		private void OnDisable()
		{
			StopAllCoroutines();
			RestoreSelection();
		}

		private void RestoreSelection()
		{
			if (!(EventSystem.current == null))
			{
				GameObject currentSelectedGameObject = EventSystem.current.currentSelectedGameObject;
				if (currentSelectedGameObject == null || currentSelectedGameObject.transform.IsChildOf(base.transform))
				{
					EventSystem.current.SetSelectedGameObject((_previousSelection != null && _previousSelection.activeInHierarchy) ? _previousSelection : null);
				}
			}
		}

		private void OnDestroy()
		{
			if (_continueButton != null)
			{
				_continueButton.Clicked -= Dismiss;
			}
			if (_closeButton != null)
			{
				_closeButton.Clicked -= Dismiss;
			}
		}
	}
}
