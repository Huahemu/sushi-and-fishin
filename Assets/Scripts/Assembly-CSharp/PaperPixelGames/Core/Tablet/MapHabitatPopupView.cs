using System;
using PaperPixelGames.Framework.UI;
using TMPro;
using UnityEngine;

namespace PaperPixelGames.Core.Tablet
{
	public sealed class MapHabitatPopupView : MonoBehaviour
	{
		[SerializeField]
		private TextMeshProUGUI _titleText;

		[SerializeField]
		private TextMeshProUGUI _bodyText;

		[SerializeField]
		private BaseButton _closeButton;

		private Action _closeRequested;

		public void Initialize(Action closeRequested)
		{
			_closeRequested = closeRequested;
			if (!(_closeButton == null))
			{
				_closeButton.Clicked -= OnCloseClicked;
				_closeButton.Clicked += OnCloseClicked;
			}
		}

		public void Show(string title, string body)
		{
			if (_titleText != null)
			{
				_titleText.text = title;
			}
			if (_bodyText != null)
			{
				_bodyText.text = body;
			}
			base.gameObject.SetActive(value: true);
		}

		public void Hide()
		{
			base.gameObject.SetActive(value: false);
		}

		private void OnCloseClicked()
		{
			_closeRequested?.Invoke();
		}

		private void OnDestroy()
		{
			if (_closeButton != null)
			{
				_closeButton.Clicked -= OnCloseClicked;
			}
		}
	}
}
