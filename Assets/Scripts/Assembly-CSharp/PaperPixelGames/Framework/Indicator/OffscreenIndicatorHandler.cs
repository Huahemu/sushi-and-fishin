using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Serialization;
using UnityServiceLocator;

namespace PaperPixelGames.Framework.Indicator
{
	public sealed class OffscreenIndicatorHandler : Singleton<OffscreenIndicatorHandler>
	{
		public readonly struct OffscreenIndicatorHandle
		{
			public readonly int Id;

			public bool IsValid => Id != 0;

			public OffscreenIndicatorHandle(int id)
			{
				Id = id;
			}
		}

		private sealed class TransformTarget : IOffscreenIndicatorTarget
		{
			public Transform Follow { get; }

			public Transform DistanceOrigin { get; private set; }

			public Sprite Icon { get; private set; }

			public bool ShowDistance { get; private set; }

			public Vector3 WorldOffset { get; private set; }

			public bool IsValid => Follow != null;

			public TransformTarget(Transform follow, Sprite icon, bool showDistance, Vector3 worldOffset, Transform distanceOrigin)
			{
				Follow = follow;
				Icon = icon;
				ShowDistance = showDistance;
				WorldOffset = worldOffset;
				DistanceOrigin = distanceOrigin;
			}

			public void UpdateData(Sprite icon, bool showDistance, Vector3 worldOffset, Transform distanceOrigin)
			{
				Icon = icon;
				ShowDistance = showDistance;
				WorldOffset = worldOffset;
				DistanceOrigin = distanceOrigin;
			}

			public Vector3 GetWorldPosition()
			{
				if (!(Follow != null))
				{
					return Vector3.zero;
				}
				return Follow.position + WorldOffset;
			}
		}

		private sealed class Entry
		{
			public IOffscreenIndicatorTarget Target;

			public OffscreenIndicatorWidget Widget;

			public Transform FollowTransform;

			public OffscreenIndicatorVisualSettings VisualSettingsOverride;

			public bool WidgetReleased;

			public bool HasExpiry;

			public float ExpireAt;

			public bool UnregisterWhenReached;

			public float ReachDistance;

			public Action Reached;

			public void SetLifetime(float lifetimeSeconds)
			{
				HasExpiry = lifetimeSeconds > 0f;
				ExpireAt = (HasExpiry ? (Time.time + lifetimeSeconds) : 0f);
			}

			public void SetReachRule(bool unregisterWhenReached, float reachDistance, Action reached = null)
			{
				UnregisterWhenReached = unregisterWhenReached;
				ReachDistance = Mathf.Max(0f, reachDistance);
				Reached = reached;
			}

			public void ClearReachRule()
			{
				UnregisterWhenReached = false;
				ReachDistance = 0f;
				Reached = null;
			}
		}

		[Header("Refs")]
		[SerializeField]
		private RectTransform _canvasRect;

		[SerializeField]
		private OffscreenIndicatorWidget _widgetPrefab;

		[SerializeField]
		private Sprite _fallbackIcon;

		[Header("Pooling")]
		[SerializeField]
		[Min(0f)]
		private int _prewarmCount;

		[SerializeField]
		[Min(1f)]
		private int _maxPoolSize = 64;

		[SerializeField]
		private RectTransform _poolRoot;

		[Header("Tuning")]
		[SerializeField]
		[Min(0f)]
		private float _edgePadding = 40f;

		[SerializeField]
		private bool _rotateToDirection = true;

		[SerializeField]
		[Min(0.01f)]
		private float _updateInterval = 0.05f;

		[Header("Distance Rules")]
		[FormerlySerializedAs("_showBeyondMeters")]
		[SerializeField]
		[Min(0f)]
		private float _autoHideWithinMeters = 4f;

		[SerializeField]
		private bool _showWhenOnScreenIfFar = true;

		private Camera _camera;

		private Canvas _canvas;

		private int _nextId = 1;

		private float _nextUpdateTime;

		private int _lastUpdatedEntryCount;

		private float _cachedMinX;

		private float _cachedMaxX;

		private float _cachedMinY;

		private float _cachedMaxY;

		private Vector2 _cachedScreenCenter;

		private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();

		private readonly Dictionary<OffscreenIndicatorTarget, int> _byTarget = new Dictionary<OffscreenIndicatorTarget, int>();

		private readonly Dictionary<Transform, int> _byTransform = new Dictionary<Transform, int>();

		private readonly List<int> _tempIds = new List<int>();

		private ObjectPool<OffscreenIndicatorWidget> _widgetPool;

		private bool _isDestroying;

		public override SingletonScope Scope => SingletonScope.Scene;

		public int RegisteredEntryCount => _entries.Count;

		public int LastUpdatedEntryCount => _lastUpdatedEntryCount;

		private bool IsConfigured
		{
			get
			{
				if (_canvasRect != null)
				{
					return _widgetPrefab != null;
				}
				return false;
			}
		}

		protected override void Awake()
		{
			base.Awake();
			ServiceLocator.ForSceneOf(this).Register(this);
			if (IsConfigured)
			{
				CacheCanvas();
				EnsurePoolRoot();
				_tempIds.Capacity = Mathf.Max(_tempIds.Capacity, _maxPoolSize);
				CreatePool();
				PrewarmPool();
			}
		}

		private void OnDisable()
		{
			HideAllWidgets();
		}

		public void SetVisibility(bool s)
		{
			base.gameObject.SetActive(s);
		}

		private void HideAllWidgets()
		{
			foreach (KeyValuePair<int, Entry> entry in _entries)
			{
				Entry value = entry.Value;
				if (value != null && !(value.Widget == null) && !value.WidgetReleased)
				{
					value.Widget.SetVisible(visible: false);
				}
			}
		}

		private void OnDestroy()
		{
			_isDestroying = true;
			DestroyAllWidgetsImmediately();
			if (_widgetPool != null)
			{
				_widgetPool.Clear();
				_widgetPool = null;
			}
			_entries.Clear();
			_byTarget.Clear();
			_byTransform.Clear();
			_tempIds.Clear();
		}

		private void DestroyAllWidgetsImmediately()
		{
			if (_entries.Count <= 0)
			{
				return;
			}
			foreach (KeyValuePair<int, Entry> entry in _entries)
			{
				Entry value = entry.Value;
				if (value != null && !(value.Widget == null))
				{
					UnityEngine.Object.Destroy(value.Widget.gameObject);
					value.WidgetReleased = true;
				}
			}
		}

		private void LateUpdate()
		{
			if (!TryResolveCamera())
			{
				return;
			}
			if (!IsConfigured || _entries.Count == 0)
			{
				_lastUpdatedEntryCount = 0;
				return;
			}
			float unscaledTime = Time.unscaledTime;
			if (!(unscaledTime < _nextUpdateTime))
			{
				_nextUpdateTime = unscaledTime + Mathf.Max(0.01f, _updateInterval);
				_lastUpdatedEntryCount = 0;
				CacheScreenBounds();
				CacheEntryIds();
				for (int i = 0; i < _tempIds.Count; i++)
				{
					UpdateEntry(_tempIds[i]);
				}
			}
		}

		public OffscreenIndicatorHandle ShowIndefinite(OffscreenIndicatorTarget target)
		{
			OffscreenIndicatorHandle result = Register(target, 0f);
			if (result.IsValid && _entries.TryGetValue(result.Id, out var value))
			{
				value.ClearReachRule();
			}
			return result;
		}

		public void HideIndefinite(OffscreenIndicatorTarget target)
		{
			Hide(target);
		}

		public void Hide(OffscreenIndicatorTarget target)
		{
			if (!(target == null) && _byTarget.TryGetValue(target, out var value))
			{
				Unregister(new OffscreenIndicatorHandle(value));
			}
		}

		public OffscreenIndicatorHandle ShowUntilReached(OffscreenIndicatorTarget target)
		{
			return ShowUntilReached(target, _autoHideWithinMeters);
		}

		public OffscreenIndicatorHandle ShowUntilReached(OffscreenIndicatorTarget target, float reachDistance)
		{
			if (target == null)
			{
				return default;
			}
			if (TryGetTargetEntry(target, out var id, out var entry))
			{
				if (IsEntryUsable(entry))
				{
					RefreshEntry(entry, target, 0f);
					entry.SetReachRule(unregisterWhenReached: true, reachDistance);
					return new OffscreenIndicatorHandle(id);
				}
				Unregister(new OffscreenIndicatorHandle(id));
			}
			OffscreenIndicatorHandle result = CreateEntry(target, null, 0f);
			if (!result.IsValid)
			{
				return default;
			}
			_byTarget[target] = result.Id;
			if (_entries.TryGetValue(result.Id, out var value))
			{
				value.SetReachRule(unregisterWhenReached: true, reachDistance);
			}
			return result;
		}

		public OffscreenIndicatorHandle Register(OffscreenIndicatorTarget target)
		{
			return Register(target, 0f);
		}

		public OffscreenIndicatorHandle Register(OffscreenIndicatorTarget target, float lifetimeSeconds, bool refreshIfExists = true, OffscreenIndicatorVisualSettings visualSettingsOverride = null)
		{
			if (target == null)
			{
				return default;
			}
			if (TryGetTargetEntry(target, out var id, out var entry))
			{
				if (IsEntryUsable(entry))
				{
					if (refreshIfExists)
					{
						RefreshEntry(entry, target, lifetimeSeconds, visualSettingsOverride);
						entry.ClearReachRule();
					}
					return new OffscreenIndicatorHandle(id);
				}
				Unregister(new OffscreenIndicatorHandle(id));
			}
			OffscreenIndicatorHandle result = CreateEntry(target, null, lifetimeSeconds, visualSettingsOverride);
			if (result.IsValid)
			{
				_byTarget[target] = result.Id;
			}
			return result;
		}

		public OffscreenIndicatorHandle RegisterTransform(Transform follow, Sprite icon, bool showDistance = true, Vector3 worldOffset = default(Vector3))
		{
			return RegisterTransform(follow, 0f, refreshIfExists: true, icon, showDistance, worldOffset);
		}

		public OffscreenIndicatorHandle RegisterTransform(Transform follow, float lifetimeSeconds, bool refreshIfExists = true, Sprite icon = null, bool showDistance = true, Vector3 worldOffset = default(Vector3), Transform distanceOrigin = null, OffscreenIndicatorVisualSettings visualSettingsOverride = null)
		{
			if (follow == null)
			{
				return default;
			}
			icon = ((icon != null) ? icon : _fallbackIcon);
			if (TryGetTransformEntry(follow, out var id, out var entry))
			{
				if (IsEntryUsable(entry) && entry.Target is TransformTarget transformTarget)
				{
					if (refreshIfExists)
					{
						transformTarget.UpdateData(icon, showDistance, worldOffset, distanceOrigin);
						RefreshEntry(entry, transformTarget, lifetimeSeconds, visualSettingsOverride);
						entry.ClearReachRule();
					}
					return new OffscreenIndicatorHandle(id);
				}
				Unregister(new OffscreenIndicatorHandle(id));
			}
			TransformTarget target = new TransformTarget(follow, icon, showDistance, worldOffset, distanceOrigin);
			return CreateEntry(target, follow, lifetimeSeconds, visualSettingsOverride);
		}

		public OffscreenIndicatorHandle RegisterTransformUntilReached(Transform follow, float reachDistance, Action onReached, Sprite icon = null, bool showDistance = true, Vector3 worldOffset = default(Vector3), Transform distanceOrigin = null, OffscreenIndicatorVisualSettings visualSettingsOverride = null)
		{
			OffscreenIndicatorHandle result = RegisterTransform(follow, 0f, refreshIfExists: true, icon, showDistance, worldOffset, distanceOrigin, visualSettingsOverride);
			if (result.IsValid && _entries.TryGetValue(result.Id, out var value))
			{
				value.SetReachRule(unregisterWhenReached: true, reachDistance, onReached);
			}
			return result;
		}

		public void UnregisterTransform(Transform follow)
		{
			if (!(follow == null) && _byTransform.TryGetValue(follow, out var value))
			{
				Unregister(new OffscreenIndicatorHandle(value));
			}
		}

		public void Unregister(OffscreenIndicatorHandle handle)
		{
			if (handle.IsValid && _entries.TryGetValue(handle.Id, out var value))
			{
				RemoveLookups(value);
				ReleaseWidget(value);
				_entries.Remove(handle.Id);
			}
		}

		public void RegisterScan(IReadOnlyList<BaseInteractable> targets, float lifetimeSeconds)
		{
			if (targets == null || targets.Count == 0)
			{
				return;
			}
			for (int i = 0; i < targets.Count; i++)
			{
				BaseInteractable baseInteractable = targets[i];
				if (!(baseInteractable == null) && baseInteractable.IsScanable)
				{
					baseInteractable.TryGetScanIndicatorVisualSettings(out var settings);
					if (baseInteractable.IndicatorTarget != null)
					{
						Register(baseInteractable.IndicatorTarget, lifetimeSeconds, refreshIfExists: true, settings);
						continue;
					}
					Transform follow = baseInteractable.transform;
					OffscreenIndicatorVisualSettings visualSettingsOverride = settings;
					RegisterTransform(follow, lifetimeSeconds, refreshIfExists: true, null, showDistance: true, default, null, visualSettingsOverride);
				}
			}
		}

		private void CacheCanvas()
		{
			if (_canvas == null && _canvasRect != null)
			{
				_canvas = _canvasRect.GetComponentInParent<Canvas>();
			}
		}

		private Camera GetUiCamera()
		{
			CacheCanvas();
			if (_canvas == null || _canvas.renderMode == RenderMode.ScreenSpaceOverlay)
			{
				return null;
			}
			if (!(_canvas.worldCamera != null))
			{
				return _camera;
			}
			return _canvas.worldCamera;
		}

		private void EnsurePoolRoot()
		{
			if (!(_poolRoot != null) && !(_canvasRect == null))
			{
				GameObject gameObject = new GameObject("OffscreenIndicatorWidgetPool");
				_poolRoot = gameObject.AddComponent<RectTransform>();
				_poolRoot.SetParent(_canvasRect, worldPositionStays: false);
				_poolRoot.gameObject.SetActive(value: false);
			}
		}

		private void CreatePool()
		{
			if (_widgetPool == null)
			{
				bool collectionCheck = false;
				int defaultCapacity = Mathf.Min(Mathf.Max(1, _prewarmCount), _maxPoolSize);
				_widgetPool = new ObjectPool<OffscreenIndicatorWidget>(CreateWidgetInstance, OnGetWidget, OnReleaseWidget, OnDestroyWidget, collectionCheck, defaultCapacity, _maxPoolSize);
			}
		}

		private void PrewarmPool()
		{
			if (_widgetPool != null && _prewarmCount > 0)
			{
				int num = Mathf.Min(_prewarmCount, _maxPoolSize);
				List<OffscreenIndicatorWidget> list = new List<OffscreenIndicatorWidget>(num);
				for (int i = 0; i < num; i++)
				{
					list.Add(_widgetPool.Get());
				}
				for (int j = 0; j < list.Count; j++)
				{
					_widgetPool.Release(list[j]);
				}
			}
		}

		private OffscreenIndicatorWidget CreateWidgetInstance()
		{
			if (!IsConfigured)
			{
				return null;
			}
			OffscreenIndicatorWidget offscreenIndicatorWidget = UnityEngine.Object.Instantiate(_widgetPrefab, _canvasRect);
			offscreenIndicatorWidget.ResetForPool();
			offscreenIndicatorWidget.SetVisible(visible: false);
			return offscreenIndicatorWidget;
		}

		private void OnGetWidget(OffscreenIndicatorWidget widget)
		{
			if (!(widget == null))
			{
				widget.ResetForPool();
				if (_canvasRect != null)
				{
					widget.transform.SetParent(_canvasRect, worldPositionStays: false);
				}
			}
		}

		private void OnReleaseWidget(OffscreenIndicatorWidget widget)
		{
			if (!(widget == null))
			{
				widget.ResetForPool();
				widget.SetVisible(visible: false);
				if (!_isDestroying && !(_poolRoot == null) && !(_poolRoot.gameObject == null))
				{
					widget.transform.SetParent(_poolRoot, worldPositionStays: false);
				}
			}
		}

		private void OnDestroyWidget(OffscreenIndicatorWidget widget)
		{
			if (widget != null)
			{
				UnityEngine.Object.Destroy(widget.gameObject);
			}
		}

		private OffscreenIndicatorHandle CreateEntry(IOffscreenIndicatorTarget target, Transform followTransform, float lifetimeSeconds, OffscreenIndicatorVisualSettings visualSettingsOverride = null)
		{
			if (target == null || !IsConfigured)
			{
				return default;
			}
			if (_widgetPool == null)
			{
				CreatePool();
			}
			OffscreenIndicatorWidget offscreenIndicatorWidget = _widgetPool.Get();
			if (offscreenIndicatorWidget == null)
			{
				return default;
			}
			ApplyVisualSettings(offscreenIndicatorWidget, target, visualSettingsOverride);
			offscreenIndicatorWidget.SetVisible(visible: false);
			int num = _nextId++;
			Entry entry = new Entry
			{
				Target = target,
				Widget = offscreenIndicatorWidget,
				FollowTransform = followTransform,
				VisualSettingsOverride = visualSettingsOverride,
				WidgetReleased = false
			};
			entry.SetLifetime(lifetimeSeconds);
			entry.ClearReachRule();
			_entries.Add(num, entry);
			if (followTransform != null)
			{
				_byTransform[followTransform] = num;
			}
			return new OffscreenIndicatorHandle(num);
		}

		private void RefreshEntry(Entry entry, IOffscreenIndicatorTarget target, float lifetimeSeconds, OffscreenIndicatorVisualSettings visualSettingsOverride = null)
		{
			if (entry != null && target != null)
			{
				entry.Target = target;
				entry.VisualSettingsOverride = visualSettingsOverride;
				entry.SetLifetime(lifetimeSeconds);
				if (entry.Widget != null)
				{
					ApplyVisualSettings(entry.Widget, target, visualSettingsOverride);
				}
			}
		}

		private void ApplyVisualSettings(OffscreenIndicatorWidget widget, IOffscreenIndicatorTarget target, OffscreenIndicatorVisualSettings visualSettingsOverride = null)
		{
			if (!(widget == null) && target != null)
			{
				widget.ApplyVisualSettings(ResolveVisualSettings(target, visualSettingsOverride));
			}
		}

		private bool TryGetTargetEntry(OffscreenIndicatorTarget target, out int id, out Entry entry)
		{
			id = 0;
			entry = null;
			if (target == null)
			{
				return false;
			}
			if (!_byTarget.TryGetValue(target, out id))
			{
				return false;
			}
			return _entries.TryGetValue(id, out entry);
		}

		private bool TryGetTransformEntry(Transform follow, out int id, out Entry entry)
		{
			id = 0;
			entry = null;
			if (follow == null)
			{
				return false;
			}
			if (!_byTransform.TryGetValue(follow, out id))
			{
				return false;
			}
			return _entries.TryGetValue(id, out entry);
		}

		private static bool IsEntryUsable(Entry entry)
		{
			if (entry != null && entry.Target != null && entry.Widget != null)
			{
				return !entry.WidgetReleased;
			}
			return false;
		}

		private void RemoveLookups(Entry entry)
		{
			if (entry != null)
			{
				if (entry.FollowTransform != null)
				{
					_byTransform.Remove(entry.FollowTransform);
				}
				if (entry.Target is OffscreenIndicatorTarget key)
				{
					_byTarget.Remove(key);
				}
			}
		}

		private void ReleaseWidget(Entry entry)
		{
			if (entry != null && !(entry.Widget == null) && !entry.WidgetReleased)
			{
				entry.WidgetReleased = true;
				if (_widgetPool != null)
				{
					_widgetPool.Release(entry.Widget);
				}
				else
				{
					UnityEngine.Object.Destroy(entry.Widget.gameObject);
				}
			}
		}

		private void CleanupAll()
		{
			if (_entries.Count != 0)
			{
				CacheEntryIds();
				for (int i = 0; i < _tempIds.Count; i++)
				{
					Unregister(new OffscreenIndicatorHandle(_tempIds[i]));
				}
			}
		}

		private void CacheEntryIds()
		{
			_tempIds.Clear();
			foreach (KeyValuePair<int, Entry> entry in _entries)
			{
				_tempIds.Add(entry.Key);
			}
		}

		private bool TryResolveCamera()
		{
			if (_camera == null)
			{
				_camera = Camera.main;
			}
			return _camera != null;
		}

		private void UpdateEntry(int id)
		{
			if (_entries.TryGetValue(id, out var value))
			{
				if (ShouldRemove(value))
				{
					Unregister(new OffscreenIndicatorHandle(id));
				}
				else if (ShouldUnregisterBecauseReached(value))
				{
					Action reached = value.Reached;
					Unregister(new OffscreenIndicatorHandle(id));
					reached?.Invoke();
				}
				else
				{
					UpdateOne(value);
					_lastUpdatedEntryCount++;
				}
			}
		}

		private bool ShouldUnregisterBecauseReached(Entry entry)
		{
			if (entry == null || !entry.UnregisterWhenReached)
			{
				return false;
			}
			Vector3 worldPosition = entry.Target.GetWorldPosition();
			return GetDistanceMeters(entry.Target, worldPosition) <= entry.ReachDistance;
		}

		private static bool ShouldRemove(Entry entry)
		{
			if (entry == null)
			{
				return true;
			}
			if (entry.Target == null || entry.Widget == null)
			{
				return true;
			}
			if (entry.HasExpiry && Time.time >= entry.ExpireAt)
			{
				return true;
			}
			if (!entry.Target.IsValid)
			{
				return true;
			}
			return false;
		}

		private void UpdateOne(Entry entry)
		{
			IOffscreenIndicatorTarget target = entry.Target;
			OffscreenIndicatorWidget widget = entry.Widget;
			Vector3 worldPosition = target.GetWorldPosition();
			float distanceMeters = GetDistanceMeters(target, worldPosition);
			Vector3 vector = _camera.WorldToScreenPoint(worldPosition);
			bool flag = vector.z > 0.0001f;
			bool flag2 = flag && vector.x >= 0f && vector.x <= (float)Screen.width && vector.y >= 0f && vector.y <= (float)Screen.height;
			if (flag2 && !_showWhenOnScreenIfFar)
			{
				widget.SetVisible(visible: false);
				return;
			}
			Vector2 screenPoint;
			if (flag2)
			{
				screenPoint = new Vector2(vector.x, vector.y);
				screenPoint.x = Mathf.Clamp(screenPoint.x, _cachedMinX, _cachedMaxX);
				screenPoint.y = Mathf.Clamp(screenPoint.y, _cachedMinY, _cachedMaxY);
				widget.SetRotation(0f);
			}
			else
			{
				Vector2 vector2 = new Vector2(vector.x - _cachedScreenCenter.x, vector.y - _cachedScreenCenter.y);
				if (!flag)
				{
					vector2 = -vector2;
				}
				if (vector2.sqrMagnitude < 0.0001f)
				{
					vector2 = Vector2.right;
				}
				screenPoint = GetClampedToEdge(_cachedScreenCenter, vector2, _cachedMinX, _cachedMaxX, _cachedMinY, _cachedMaxY);
				if (_rotateToDirection)
				{
					float rotation = Mathf.Atan2(vector2.y, vector2.x) * 57.29578f;
					widget.SetRotation(rotation);
				}
				else
				{
					widget.SetRotation(0f);
				}
			}
			Camera uiCamera = GetUiCamera();
			if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPoint, uiCamera, out var localPoint))
			{
				widget.SetVisible(visible: false);
				return;
			}
			widget.SetVisible(visible: true);
			widget.SetAnchoredPosition(localPoint);
			if (ShouldShowDistance(entry))
			{
				widget.SetDistanceMeters(distanceMeters);
			}
			else
			{
				widget.SetDistanceHidden();
			}
		}

		private static bool ShouldShowDistance(Entry entry)
		{
			if (entry == null || entry.Target == null)
			{
				return false;
			}
			OffscreenIndicatorVisualSettings visualSettingsOverride = entry.VisualSettingsOverride;
			if (visualSettingsOverride != null && visualSettingsOverride.ApplyDistanceVisible)
			{
				return visualSettingsOverride.DistanceVisible;
			}
			return entry.Target.ShowDistance;
		}

		private float GetDistanceToCamera(Vector3 worldPosition)
		{
			return Vector3.Distance(_camera.transform.position, worldPosition);
		}

		private float GetDistanceMeters(IOffscreenIndicatorTarget target, Vector3 worldPosition)
		{
			if (target is TransformTarget transformTarget && transformTarget.DistanceOrigin != null)
			{
				return Vector3.Distance(transformTarget.DistanceOrigin.position, worldPosition);
			}
			return GetDistanceToCamera(worldPosition);
		}

		private void CacheScreenBounds()
		{
			_cachedMinX = _edgePadding;
			_cachedMaxX = (float)Screen.width - _edgePadding;
			_cachedMinY = _edgePadding;
			_cachedMaxY = (float)Screen.height - _edgePadding;
			_cachedScreenCenter = new Vector2((float)Screen.width * 0.5f, (float)Screen.height * 0.5f);
			if (_cachedMaxX < _cachedMinX)
			{
				_cachedMaxX = _cachedMinX;
			}
			if (_cachedMaxY < _cachedMinY)
			{
				_cachedMaxY = _cachedMinY;
			}
		}

		private static Vector2 GetClampedToEdge(Vector2 center, Vector2 direction, float minX, float maxX, float minY, float maxY)
		{
			float a = float.PositiveInfinity;
			if (Mathf.Abs(direction.x) > 0.0001f)
			{
				a = ((direction.x > 0f) ? ((maxX - center.x) / direction.x) : ((minX - center.x) / direction.x));
			}
			float b = float.PositiveInfinity;
			if (Mathf.Abs(direction.y) > 0.0001f)
			{
				b = ((direction.y > 0f) ? ((maxY - center.y) / direction.y) : ((minY - center.y) / direction.y));
			}
			float num = Mathf.Min(a, b);
			if (!float.IsFinite(num) || num <= 0f)
			{
				num = 1f;
			}
			Vector2 result = center + direction * num;
			result.x = Mathf.Clamp(result.x, minX, maxX);
			result.y = Mathf.Clamp(result.y, minY, maxY);
			return result;
		}

		private OffscreenIndicatorVisualSettings ResolveVisualSettings(IOffscreenIndicatorTarget target, OffscreenIndicatorVisualSettings visualSettingsOverride = null)
		{
			OffscreenIndicatorVisualSettings offscreenIndicatorVisualSettings = ((visualSettingsOverride != null) ? visualSettingsOverride.Clone() : new OffscreenIndicatorVisualSettings());
			if (visualSettingsOverride == null && target is OffscreenIndicatorTarget offscreenIndicatorTarget)
			{
				offscreenIndicatorVisualSettings = offscreenIndicatorTarget.Settings;
			}
			if (offscreenIndicatorVisualSettings.IconSprite == null)
			{
				offscreenIndicatorVisualSettings.IconSprite = ((target.Icon != null) ? target.Icon : _fallbackIcon);
			}
			return offscreenIndicatorVisualSettings;
		}
	}
}
