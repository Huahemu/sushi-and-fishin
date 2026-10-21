using PaperPixelGames.Framework.Patterns;
using UnityEngine;

namespace PaperPixelGames.Framework.WorldUISystem
{
	public class WorldUIAnchor : MonoBehaviour
	{
		[Header("Anchor")]
		[SerializeField]
		private Transform _anchorPoint;

		[SerializeField]
		private Vector3 _worldOffset = Vector3.up;

		[SerializeField]
		private Vector2 _screenOffset;

		[Header("Visual")]
		[SerializeField]
		private Sprite _defaultIconSprite;

		[SerializeField]
		private Sprite _specificIconSprite;

		[SerializeField]
		private float _specificIconDistance = 6f;

		[SerializeField]
		private float _defaultIconScaleMultiplier = 0.75f;

		[Header("Distance UI")]
		[SerializeField]
		private bool _showDistanceText;

		[SerializeField]
		private string _distanceSuffix = "m";

		[SerializeField]
		[Min(0f)]
		private int _distanceDecimalCount;

		[Header("Visibility")]
		[SerializeField]
		private bool _isVisible = true;

		[SerializeField]
		private float _minDistance = 1.5f;

		[SerializeField]
		private float _maxDistance = 20f;

		[SerializeField]
		private bool _clampToScreen;

		[SerializeField]
		private float _screenClampPadding = 50f;

		[SerializeField]
		private bool _disableCompletelyAfterMinDistanceReached;

		[Header("Scale")]
		[SerializeField]
		private bool _scaleByDistance;

		[SerializeField]
		private float _minScale = 0.75f;

		[SerializeField]
		private float _maxScale = 1f;

		[SerializeField]
		private float _scaleStartDistance = 2f;

		[SerializeField]
		private float _scaleEndDistance = 20f;

		private bool _wasDisabledByMinDistance;

		public Vector2 ScreenOffset => _screenOffset;

		public float MinDistance => _minDistance;

		public float MaxDistance => _maxDistance;

		public bool ClampToScreen => _clampToScreen;

		public float ScreenClampPadding => _screenClampPadding;

		public bool DisableCompletelyAfterMinDistanceReached => _disableCompletelyAfterMinDistanceReached;

		public bool ShowDistanceText => _showDistanceText;

		private void OnEnable()
		{
			if (Singleton<WorldUIManager>.Instance != null)
			{
				Singleton<WorldUIManager>.Instance.Register(this);
			}
		}

		private void Start()
		{
			if (Singleton<WorldUIManager>.Instance != null)
			{
				Singleton<WorldUIManager>.Instance.Refresh(this);
			}
		}

		private void OnDisable()
		{
			if (Singleton<WorldUIManager>.Instance != null)
			{
				Singleton<WorldUIManager>.Instance.Unregister(this);
			}
		}

		public Vector3 GetWorldPosition()
		{
			return ((_anchorPoint != null) ? _anchorPoint : base.transform).position + _worldOffset;
		}

		public bool CanShow()
		{
			if (!base.isActiveAndEnabled)
			{
				return false;
			}
			if (!_isVisible)
			{
				return false;
			}
			return true;
		}

		public Sprite GetIconForDistance(float distance)
		{
			if (_specificIconSprite != null && distance <= _specificIconDistance)
			{
				return _specificIconSprite;
			}
			return _defaultIconSprite;
		}

		public string GetDistanceText(float distance)
		{
			string text = "F" + Mathf.Max(0, _distanceDecimalCount);
			return distance.ToString(text) + _distanceSuffix;
		}

		public float GetScaleMultiplier(float distance)
		{
			float num = 1f;
			if (_scaleByDistance)
			{
				if (_scaleEndDistance <= _scaleStartDistance)
				{
					num = _maxScale;
				}
				else
				{
					float t = Mathf.InverseLerp(_scaleStartDistance, _scaleEndDistance, distance);
					num = Mathf.Lerp(_maxScale, _minScale, t);
				}
			}
			if (!(_specificIconSprite != null) || !(distance <= _specificIconDistance))
			{
				num *= _defaultIconScaleMultiplier;
			}
			return num;
		}

		public void NotifyHiddenByMinDistance(float distance)
		{
			if (!_wasDisabledByMinDistance && _disableCompletelyAfterMinDistanceReached && !(distance > _minDistance))
			{
				_wasDisabledByMinDistance = true;
				_isVisible = false;
				if (Singleton<WorldUIManager>.Instance != null)
				{
					Singleton<WorldUIManager>.Instance.Refresh(this);
				}
			}
		}

		public void SetVisible(bool value)
		{
			if (_isVisible != value)
			{
				_isVisible = value;
				if (value)
				{
					_wasDisabledByMinDistance = false;
				}
				if (Singleton<WorldUIManager>.Instance != null)
				{
					Singleton<WorldUIManager>.Instance.Refresh(this);
				}
			}
		}

		public void SetDistanceTextVisible(bool value)
		{
			if (_showDistanceText != value)
			{
				_showDistanceText = value;
				if (Singleton<WorldUIManager>.Instance != null)
				{
					Singleton<WorldUIManager>.Instance.Refresh(this);
				}
			}
		}
	}
}
