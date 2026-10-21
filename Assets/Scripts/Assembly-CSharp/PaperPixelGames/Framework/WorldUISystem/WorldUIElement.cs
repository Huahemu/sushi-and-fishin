using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Framework.WorldUISystem
{
	public class WorldUIElement : MonoBehaviour
	{
		[SerializeField]
		private RectTransform _rectTransform;

		[SerializeField]
		private Image _iconImage;

		[SerializeField]
		private TextMeshProUGUI _distanceText;

		[SerializeField]
		private GameObject _distanceRoot;

		[SerializeField]
		private CanvasGroup _canvasGroup;

		private WorldUIAnchor _anchor;

		private string _lastDistanceText;

		private bool _lastVisible;

		private bool _hasLastPosition;

		private bool _hasLastScale;

		private Vector2 _lastPosition;

		private float _lastScale = 1f;

		private void Awake()
		{
			if (_rectTransform == null)
			{
				_rectTransform = base.transform as RectTransform;
			}
			if (_canvasGroup == null)
			{
				_canvasGroup = GetComponent<CanvasGroup>();
			}
			if (_canvasGroup == null)
			{
				_canvasGroup = base.gameObject.AddComponent<CanvasGroup>();
			}
			if (_distanceRoot == null && _distanceText != null)
			{
				_distanceRoot = _distanceText.gameObject;
			}
		}

		public void Initialize(WorldUIAnchor anchor)
		{
			_anchor = anchor;
			if (_anchor == null)
			{
				SetDistanceVisible(visible: false);
				SetVisible(visible: false);
			}
			else
			{
				_hasLastPosition = false;
				_hasLastScale = false;
				RefreshDistanceVisibility();
			}
		}

		public void SetPosition(Vector2 anchoredPosition)
		{
			if (!(_rectTransform == null) && (!_hasLastPosition || !((_lastPosition - anchoredPosition).sqrMagnitude < 0.25f)))
			{
				_hasLastPosition = true;
				_lastPosition = anchoredPosition;
				_rectTransform.anchoredPosition = anchoredPosition;
			}
		}

		public void SetScale(float scale)
		{
			if (!(_rectTransform == null) && (!_hasLastScale || !(Mathf.Abs(_lastScale - scale) < 0.001f)))
			{
				_hasLastScale = true;
				_lastScale = scale;
				_rectTransform.localScale = Vector3.one * scale;
			}
		}

		public void SetIcon(Sprite sprite)
		{
			if (!(_iconImage == null) && !(_iconImage.sprite == sprite))
			{
				_iconImage.sprite = sprite;
			}
		}

		public void SetDistance(float distance)
		{
			if (_anchor == null || !_anchor.ShowDistanceText || _distanceText == null)
			{
				SetDistanceVisible(visible: false);
				return;
			}
			SetDistanceVisible(visible: true);
			string distanceText = _anchor.GetDistanceText(distance);
			if (!(_lastDistanceText == distanceText))
			{
				_lastDistanceText = distanceText;
				_distanceText.text = distanceText;
			}
		}

		public void SetVisible(bool visible)
		{
			if (_lastVisible != visible || base.gameObject.activeSelf != visible)
			{
				_lastVisible = visible;
				if (_canvasGroup != null)
				{
					_canvasGroup.alpha = (visible ? 1f : 0f);
					_canvasGroup.blocksRaycasts = false;
					_canvasGroup.interactable = false;
				}
				if (base.gameObject.activeSelf != visible)
				{
					base.gameObject.SetActive(visible);
				}
			}
		}

		private void RefreshDistanceVisibility()
		{
			bool distanceVisible = _anchor != null && _anchor.ShowDistanceText && _distanceText != null;
			SetDistanceVisible(distanceVisible);
		}

		private void SetDistanceVisible(bool visible)
		{
			if (_distanceRoot != null && _distanceRoot.activeSelf != visible)
			{
				_distanceRoot.SetActive(visible);
			}
			if (!visible)
			{
				_lastDistanceText = null;
				if (_distanceText != null)
				{
					_distanceText.text = string.Empty;
				}
			}
		}
	}
}
