using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Framework.Indicator
{
	public sealed class OffscreenIndicatorWidget : MonoBehaviour
	{
		[SerializeField]
		private RectTransform _root;

		[SerializeField]
		private Image _icon;

		[SerializeField]
		private TextMeshProUGUI _distanceText;

		private bool _defaultsCached;

		private Vector2 _defaultIconSize;

		private Color _defaultIconColor;

		private Color _defaultDistanceColor;

		private Vector3 _defaultRootScale;

		private bool _lastVisible;

		private bool _hasLastPosition;

		private bool _hasLastRotation;

		private Vector2 _lastPosition;

		private float _lastRotation;

		private string _lastDistanceText;

		public RectTransform Root
		{
			get
			{
				if (!(_root != null))
				{
					return (RectTransform)base.transform;
				}
				return _root;
			}
		}

		private void Awake()
		{
			CacheDefaults();
		}

		public static string FormatDistance(float meters)
		{
			if (!(meters < 1000f))
			{
				return $"{meters / 1000f:0.0}km";
			}
			return $"{meters:0}m";
		}

		public void SetAnchoredPosition(Vector2 targetPosition)
		{
			targetPosition.x = Mathf.Round(targetPosition.x);
			targetPosition.y = Mathf.Round(targetPosition.y);
			if (!_hasLastPosition || !((_lastPosition - targetPosition).sqrMagnitude < 0.25f))
			{
				_hasLastPosition = true;
				_lastPosition = targetPosition;
				Root.anchoredPosition = targetPosition;
			}
		}

		public void SetIcon(Sprite sprite)
		{
			if (_icon != null && _icon.sprite != sprite)
			{
				_icon.sprite = sprite;
			}
		}

		public void SetVisible(bool visible)
		{
			if (_lastVisible != visible || base.gameObject.activeSelf != visible)
			{
				_lastVisible = visible;
				if (base.gameObject.activeSelf != visible)
				{
					base.gameObject.SetActive(visible);
				}
			}
		}

		public void SetRotation(float zDegrees)
		{
			if (!_hasLastRotation || !(Mathf.Abs(Mathf.DeltaAngle(_lastRotation, zDegrees)) < 0.1f))
			{
				_hasLastRotation = true;
				_lastRotation = zDegrees;
				Root.localRotation = Quaternion.Euler(0f, 0f, zDegrees);
			}
		}

		public void SetDistanceMeters(float meters)
		{
			if (!(_distanceText == null))
			{
				string text = FormatDistance(meters);
				if (!_distanceText.gameObject.activeSelf)
				{
					_distanceText.gameObject.SetActive(value: true);
				}
				if (!(_lastDistanceText == text))
				{
					_lastDistanceText = text;
					_distanceText.text = text;
				}
			}
		}

		public void SetDistanceHidden()
		{
			if (!(_distanceText == null))
			{
				if (!string.IsNullOrEmpty(_lastDistanceText))
				{
					_lastDistanceText = string.Empty;
					_distanceText.text = string.Empty;
				}
				if (_distanceText.gameObject.activeSelf)
				{
					_distanceText.gameObject.SetActive(value: false);
				}
			}
		}

		public void ApplyVisualSettings(in OffscreenIndicatorVisualSettings settings)
		{
			ResetVisualState();
			if (_icon != null)
			{
				if (settings.IconSprite != null)
				{
					_icon.sprite = settings.IconSprite;
				}
				if (settings.ApplyIconSize)
				{
					_icon.rectTransform.sizeDelta = settings.IconSize;
				}
				if (settings.ApplyIconColor)
				{
					_icon.color = settings.IconColor;
				}
			}
			if (_distanceText != null)
			{
				if (settings.ApplyDistanceVisible)
				{
					_distanceText.gameObject.SetActive(settings.DistanceVisible);
				}
				if (settings.ApplyDistanceColor)
				{
					_distanceText.color = settings.DistanceColor;
				}
			}
			if (settings.ApplyRootScale)
			{
				Root.localScale = Vector3.one * settings.RootScale;
			}
			if (settings.ResetRotation)
			{
				Root.localRotation = Quaternion.identity;
			}
		}

		public void ResetForPool()
		{
			CacheDefaults();
			ResetVisualState();
			SetIcon(null);
			SetRotation(0f);
			SetDistanceHidden();
			SetAnchoredPosition(Vector2.zero);
			_lastVisible = false;
			_hasLastPosition = false;
			_hasLastRotation = false;
		}

		private void CacheDefaults()
		{
			if (!_defaultsCached)
			{
				if (_icon != null)
				{
					_defaultIconSize = _icon.rectTransform.sizeDelta;
					_defaultIconColor = _icon.color;
				}
				if (_distanceText != null)
				{
					_defaultDistanceColor = _distanceText.color;
				}
				_defaultRootScale = Root.localScale;
				_defaultsCached = true;
			}
		}

		private void ResetVisualState()
		{
			CacheDefaults();
			if (_icon != null)
			{
				_icon.rectTransform.sizeDelta = _defaultIconSize;
				_icon.color = _defaultIconColor;
			}
			if (_distanceText != null)
			{
				_distanceText.color = _defaultDistanceColor;
			}
			Root.localScale = _defaultRootScale;
		}
	}
}
