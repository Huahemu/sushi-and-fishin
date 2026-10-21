using System;
using PaperPixelGames.Framework.UI;
using UnityEngine;

namespace PaperPixelGames.Core.Tablet
{
	public sealed class MapLocationMarker : MonoBehaviour
	{
		[SerializeField]
		private BaseButton _button;

		private Action _clicked;

		public RectTransform RectTransform => base.transform as RectTransform;

		public void Initialize(Action clicked)
		{
			_clicked = clicked;
			if (_button == null)
			{
				_button = GetComponent<BaseButton>();
			}
			if (_button != null)
			{
				_button.Clicked -= OnClicked;
				_button.Clicked += OnClicked;
			}
		}

		private void OnClicked()
		{
			_clicked?.Invoke();
		}

		private void OnDestroy()
		{
			if (_button != null)
			{
				_button.Clicked -= OnClicked;
			}
		}
	}
}
