using System.Collections.Generic;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace PaperPixelGames.Core.Interaction.ToolTip
{
	[DefaultExecutionOrder(10000)]
	public sealed class ToolTipHandler : Singleton<ToolTipHandler>
	{
		[Header("Refs")]
		[SerializeField]
		private ToolTipUI _toolTipPrefab;

		[SerializeField]
		private ToolTipUI _toolTipPrefab2;

		[SerializeField]
		private Transform _toolTipRoot;

		[Header("Timing")]
		[SerializeField]
		[Min(0f)]
		private float _fadeInDuration = 0.2f;

		[SerializeField]
		[Min(0f)]
		private float _fadeOutDuration = 0.08f;

		private Transform _player;

		private readonly Dictionary<ITooltipProvider, ToolTipUI> _active = new Dictionary<ITooltipProvider, ToolTipUI>();

		private readonly Dictionary<ITooltipProvider, Vector3> _anchorOffsets = new Dictionary<ITooltipProvider, Vector3>();

		private readonly List<ITooltipProvider> _providersToRelease = new List<ITooltipProvider>();

		private readonly Stack<ToolTipUI> _pooledNormalTooltips = new Stack<ToolTipUI>();

		private readonly Stack<ToolTipUI> _pooledHeaderOnlyTooltips = new Stack<ToolTipUI>();

		public override SingletonScope Scope => SingletonScope.Scene;

		private void OnEnable()
		{
			LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
			LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
		}

		private void OnDisable()
		{
			LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
			_providersToRelease.Clear();
			_providersToRelease.AddRange(_active.Keys);
			foreach (ITooltipProvider item in _providersToRelease)
			{
				ReleaseTooltip(item, _active[item]);
			}
			_providersToRelease.Clear();
		}

		private void LateUpdate()
		{
			if (_active.Count == 0)
			{
				return;
			}
			_providersToRelease.Clear();
			foreach (KeyValuePair<ITooltipProvider, ToolTipUI> item in _active)
			{
				ITooltipProvider key = item.Key;
				ToolTipUI value = item.Value;
				if (!IsProviderAlive(key) || value == null)
				{
					_providersToRelease.Add(key);
				}
				else if (value.gameObject.activeInHierarchy)
				{
					value.SetWorldPosition(GetProviderWorldPosition(key));
				}
			}
			foreach (ITooltipProvider item2 in _providersToRelease)
			{
				if (_active.TryGetValue(item2, out var value2))
				{
					ReleaseTooltip(item2, value2);
				}
			}
		}

		public void Initialize(Transform player)
		{
			_player = player;
		}

		private void OnSelectedLocaleChanged(Locale locale)
		{
			foreach (KeyValuePair<ITooltipProvider, ToolTipUI> item in _active)
			{
				if (IsProviderAlive(item.Key) && !(item.Value == null))
				{
					item.Value.SetData(item.Key.GetTooltip());
				}
			}
		}

		public void OnGazeEnter(BaseHoldable holdable)
		{
			if (!(holdable == null) && !(_toolTipPrefab == null) && !(_player == null) && !holdable.IsPlacementTooltipSuppressed && !(holdable.ItemSO == null) && holdable.ItemSO.Tooltip.Enabled)
			{
				ShowTooltip(holdable, holdable.transform.position);
				HideOthers(holdable);
			}
		}

		public void OnGazeEnter(ITooltipProvider provider, Vector3 pos)
		{
			if (base.isActiveAndEnabled && IsProviderAlive(provider) && !(_player == null))
			{
				HideOthers(provider);
				ShowTooltip(provider, pos);
			}
		}

		public void OnGazeExit(ITooltipProvider provider)
		{
			if (provider != null)
			{
				HideTooltip(provider);
			}
		}

		private void ShowTooltip(ITooltipProvider provider, Vector3 pos)
		{
			if (!base.isActiveAndEnabled || !IsProviderAlive(provider))
			{
				return;
			}
			TooltipData data = provider.GetTooltip();
			bool onlyHeader = data.OnlyHeader;
			ToolTipUI orCreateTooltip = GetOrCreateTooltip(provider, onlyHeader);
			if (!(orCreateTooltip == null))
			{
				_anchorOffsets[provider] = ((provider is Component component) ? component.transform.InverseTransformPoint(pos) : pos);
				Vector3 providerWorldPosition = GetProviderWorldPosition(provider);
				orCreateTooltip.gameObject.SetActive(value: true);
				orCreateTooltip.SetData(in data);
				orCreateTooltip.SetWorldPosition(providerWorldPosition);
				orCreateTooltip.KillTweens();
				orCreateTooltip.PrepareHidden();
				if (_active.TryGetValue(provider, out var value) && !(value == null))
				{
					value.FadeIn(_fadeInDuration);
				}
			}
		}

		private ToolTipUI GetOrCreateTooltip(ITooltipProvider provider, bool isHeaderOnly)
		{
			if (_active.TryGetValue(provider, out var value) && value != null)
			{
				if (value.IsHeaderOnlyPrefab == isHeaderOnly)
				{
					return value;
				}
				ReleaseTooltip(provider, value);
			}
			Stack<ToolTipUI> stack = (isHeaderOnly ? _pooledHeaderOnlyTooltips : _pooledNormalTooltips);
			ToolTipUI toolTipUI = (isHeaderOnly ? _toolTipPrefab2 : _toolTipPrefab);
			if (toolTipUI == null)
			{
				return null;
			}
			ToolTipUI toolTipUI2;
			if (stack.Count > 0)
			{
				toolTipUI2 = stack.Pop();
			}
			else
			{
				toolTipUI2 = Object.Instantiate(toolTipUI, _toolTipRoot);
				toolTipUI2.Initialize();
			}
			_active[provider] = toolTipUI2;
			return toolTipUI2;
		}

		private Vector3 GetProviderWorldPosition(ITooltipProvider provider)
		{
			if (provider is ITooltipWorldAnchor tooltipWorldAnchor)
			{
				return tooltipWorldAnchor.GetWorldPosition();
			}
			if (provider is Component component)
			{
				if (!_anchorOffsets.TryGetValue(provider, out var value))
				{
					return component.transform.position;
				}
				return component.transform.TransformPoint(value);
			}
			if (!_anchorOffsets.TryGetValue(provider, out var value2))
			{
				return Vector3.zero;
			}
			return value2;
		}

		private static bool IsProviderAlive(ITooltipProvider provider)
		{
			if (provider == null)
			{
				return false;
			}
			if (provider is Object obj && obj == null)
			{
				return false;
			}
			if (provider is Behaviour behaviour)
			{
				return behaviour.isActiveAndEnabled;
			}
			return true;
		}

		private void HideTooltip(ITooltipProvider provider)
		{
			if (_active.TryGetValue(provider, out var ui) && !(ui == null))
			{
				ui.KillTweens();
				ui.FadeOut(_fadeOutDuration, () =>
				{
					ReleaseTooltip(provider, ui);
				});
			}
		}

		private void HideOthers(ITooltipProvider exceptProvider)
		{
			List<ITooltipProvider> list = new List<ITooltipProvider>();
			foreach (KeyValuePair<ITooltipProvider, ToolTipUI> item in _active)
			{
				if (item.Key != exceptProvider)
				{
					list.Add(item.Key);
				}
			}
			foreach (ITooltipProvider item2 in list)
			{
				HideTooltip(item2);
			}
		}

		private void ReleaseTooltip(ITooltipProvider provider, ToolTipUI ui)
		{
			_active.Remove(provider);
			_anchorOffsets.Remove(provider);
			if (!(ui == null))
			{
				ui.KillTweens();
				ui.gameObject.SetActive(value: false);
				if (ui.IsHeaderOnlyPrefab)
				{
					_pooledHeaderOnlyTooltips.Push(ui);
				}
				else
				{
					_pooledNormalTooltips.Push(ui);
				}
			}
		}

		private void OnDestroy()
		{
			foreach (ToolTipUI value in _active.Values)
			{
				if (value != null)
				{
					Object.Destroy(value.gameObject);
				}
			}
			foreach (ToolTipUI pooledNormalTooltip in _pooledNormalTooltips)
			{
				if (pooledNormalTooltip != null)
				{
					Object.Destroy(pooledNormalTooltip.gameObject);
				}
			}
			foreach (ToolTipUI pooledHeaderOnlyTooltip in _pooledHeaderOnlyTooltips)
			{
				if (pooledHeaderOnlyTooltip != null)
				{
					Object.Destroy(pooledHeaderOnlyTooltip.gameObject);
				}
			}
			_active.Clear();
			_anchorOffsets.Clear();
		}
	}
}
