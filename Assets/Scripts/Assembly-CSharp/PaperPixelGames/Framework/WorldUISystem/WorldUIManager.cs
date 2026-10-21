using System.Collections.Generic;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;

namespace PaperPixelGames.Framework.WorldUISystem
{
	public class WorldUIManager : Singleton<WorldUIManager>
	{
		[Header("References")]
		[SerializeField]
		private Canvas _targetCanvas;

		[SerializeField]
		private RectTransform _container;

		[SerializeField]
		private WorldUIElement _elementPrefab;

		[SerializeField]
		private Camera _worldCamera;

		[Header("Settings")]
		[SerializeField]
		private bool _hideIfBehindCamera = true;

		[SerializeField]
		[Min(0.01f)]
		private float _updateInterval = 0.01f;

		private readonly List<WorldUIAnchor> _anchors = new List<WorldUIAnchor>();

		private readonly Dictionary<WorldUIAnchor, WorldUIElement> _elements = new Dictionary<WorldUIAnchor, WorldUIElement>();

		private RectTransform _canvasRect;

		private Camera _cachedWorldCamera;

		private Camera _cachedUiCamera;

		private float _nextUpdateTime;

		private int _lastUpdatedAnchorCount;

		private int _cachedScreenWidth;

		private int _cachedScreenHeight;

		public int RegisteredAnchorCount => _anchors.Count;

		public int LastUpdatedAnchorCount => _lastUpdatedAnchorCount;

		private Camera WorldCamera
		{
			get
			{
				if (_worldCamera != null)
				{
					return _worldCamera;
				}
				if (_cachedWorldCamera == null)
				{
					_cachedWorldCamera = Camera.main;
				}
				return _cachedWorldCamera;
			}
		}

		protected override void Awake()
		{
			base.Awake();
			if (_targetCanvas == null)
			{
				_targetCanvas = GetComponentInChildren<Canvas>();
			}
			if (_targetCanvas != null && _container == null)
			{
				_container = _targetCanvas.transform as RectTransform;
			}
			if (_targetCanvas != null)
			{
				_canvasRect = _targetCanvas.transform as RectTransform;
			}
			RefreshUiCameraCache();
		}

		private void LateUpdate()
		{
			if (_targetCanvas == null || _container == null || _elementPrefab == null)
			{
				return;
			}
			if (_anchors.Count == 0)
			{
				_lastUpdatedAnchorCount = 0;
				return;
			}
			float unscaledTime = Time.unscaledTime;
			if (unscaledTime < _nextUpdateTime)
			{
				return;
			}
			_nextUpdateTime = unscaledTime + Mathf.Max(0.01f, _updateInterval);
			_lastUpdatedAnchorCount = 0;
			Camera worldCamera = WorldCamera;
			if (worldCamera == null)
			{
				return;
			}
			RefreshScreenCache();
			RefreshUiCameraCache();
			for (int i = 0; i < _anchors.Count; i++)
			{
				WorldUIAnchor worldUIAnchor = _anchors[i];
				if (!(worldUIAnchor == null) && _elements.TryGetValue(worldUIAnchor, out var value) && !(value == null))
				{
					UpdateElement(worldUIAnchor, value, worldCamera);
					_lastUpdatedAnchorCount++;
				}
			}
		}

		public void Register(WorldUIAnchor anchor)
		{
			if (!(anchor == null) && !_anchors.Contains(anchor))
			{
				if (_targetCanvas == null || _container == null || _elementPrefab == null)
				{
					Debug.LogWarning("[WorldUIManager] Missing canvas, container, or element prefab.");
					return;
				}
				_anchors.Add(anchor);
				WorldUIElement worldUIElement = Object.Instantiate(_elementPrefab, _container);
				worldUIElement.Initialize(anchor);
				_elements.Add(anchor, worldUIElement);
			}
		}

		public void Unregister(WorldUIAnchor anchor)
		{
			if (anchor == null)
			{
				return;
			}
			_anchors.Remove(anchor);
			if (_elements.TryGetValue(anchor, out var value))
			{
				if (value != null)
				{
					Object.Destroy(value.gameObject);
				}
				_elements.Remove(anchor);
			}
		}

		public void Refresh(WorldUIAnchor anchor)
		{
			if (!(anchor == null) && _elements.TryGetValue(anchor, out var value) && value != null)
			{
				value.Initialize(anchor);
			}
		}

		private void UpdateElement(WorldUIAnchor anchor, WorldUIElement element, Camera cam)
		{
			if (!anchor.CanShow())
			{
				element.SetVisible(visible: false);
				return;
			}
			Vector3 worldPosition = anchor.GetWorldPosition();
			Vector3 vector = cam.WorldToScreenPoint(worldPosition);
			if (_hideIfBehindCamera && vector.z <= 0f)
			{
				element.SetVisible(visible: false);
				return;
			}
			float num = Vector3.Distance(cam.transform.position, worldPosition);
			if (num < anchor.MinDistance)
			{
				anchor.NotifyHiddenByMinDistance(num);
				element.SetVisible(visible: false);
				return;
			}
			if (num > anchor.MaxDistance)
			{
				element.SetVisible(visible: false);
				return;
			}
			Sprite iconForDistance = anchor.GetIconForDistance(num);
			element.SetIcon(iconForDistance);
			element.SetDistance(num);
			RectTransform canvasRect = _canvasRect;
			if (canvasRect == null)
			{
				element.SetVisible(visible: false);
				return;
			}
			Camera cachedUiCamera = _cachedUiCamera;
			if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, vector, cachedUiCamera, out var localPoint))
			{
				element.SetVisible(visible: false);
				return;
			}
			localPoint += anchor.ScreenOffset;
			if ((!(vector.x >= 0f) || !(vector.x <= (float)_cachedScreenWidth) || !(vector.y >= 0f) || !(vector.y <= (float)_cachedScreenHeight)) && !anchor.ClampToScreen)
			{
				element.SetVisible(visible: false);
				return;
			}
			if (anchor.ClampToScreen)
			{
				Vector3 vector2 = vector;
				vector2.x = Mathf.Clamp(vector2.x, anchor.ScreenClampPadding, (float)_cachedScreenWidth - anchor.ScreenClampPadding);
				vector2.y = Mathf.Clamp(vector2.y, anchor.ScreenClampPadding, (float)_cachedScreenHeight - anchor.ScreenClampPadding);
				RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, vector2, cachedUiCamera, out localPoint);
				localPoint += anchor.ScreenOffset;
			}
			element.SetPosition(localPoint);
			element.SetVisible(visible: true);
			element.SetScale(anchor.GetScaleMultiplier(num));
		}

		private void RefreshScreenCache()
		{
			_cachedScreenWidth = Screen.width;
			_cachedScreenHeight = Screen.height;
		}

		private void RefreshUiCameraCache()
		{
			if (_targetCanvas == null || _targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
			{
				_cachedUiCamera = null;
			}
			else
			{
				_cachedUiCamera = _targetCanvas.worldCamera;
			}
		}
	}
}
