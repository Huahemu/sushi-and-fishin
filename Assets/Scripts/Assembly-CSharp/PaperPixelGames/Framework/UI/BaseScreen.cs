using UnityEngine;

namespace PaperPixelGames.Framework.UI
{
	public abstract class BaseScreen : MonoBehaviour
	{
		[Header("BaseScreen")]
		[SerializeField]
		private bool _hideOnInitialize;

		private bool _isInitialized;

		public bool IsInitialized => _isInitialized;

		public bool IsVisible => base.gameObject.activeSelf;

		public void Initialize()
		{
			if (!_isInitialized)
			{
				OnInitialize();
				_isInitialized = true;
				if (_hideOnInitialize)
				{
					base.gameObject.SetActive(value: false);
				}
			}
		}

		public virtual void Show()
		{
			if (!_isInitialized)
			{
				Initialize();
			}
			base.gameObject.SetActive(value: true);
			OnShow();
		}

		public virtual void Hide()
		{
			OnHide();
			base.gameObject.SetActive(value: false);
		}

		protected virtual void OnInitialize()
		{
		}

		protected virtual void OnShow()
		{
		}

		protected virtual void OnHide()
		{
		}

		public virtual bool TryHandleBack()
		{
			return false;
		}
	}
}
