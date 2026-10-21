using System;
using PaperPixelGames.Core.Ocean;
using PaperPixelGames.Core.Ocean.Creatures;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Tablet
{
	public sealed class MapHabitatMarker : MonoBehaviour
	{
		[Header("HABITAT")]
		[SerializeField]
		private byte _seaLayerIndex;

		[SerializeField]
		private string _fallbackDisplayName;

		[SerializeField]
		private Vector2 _revealRadiusUV = new Vector2(0.08f, 0.08f);

		[SerializeField]
		private bool _allowClickWhenLocked;

		[Header("UI")]
		[SerializeField]
		private BaseButton _button;

		[SerializeField]
		private GameObject _lockedRoot;

		[SerializeField]
		private GameObject _unlockedRoot;

		[SerializeField]
		private TextMeshProUGUI _nameText;

		[SerializeField]
		private Image _questionIcon;

		private Action<MapHabitatMarker> _clicked;

		private bool _isDiscovered;

		private SeaLayer _cachedSeaLayer;

		public byte SeaLayerIndex => _seaLayerIndex;

		public Vector2 RevealRadiusUV => _revealRadiusUV;

		public RectTransform RectTransform => base.transform as RectTransform;

		public SeaLayer SeaLayer
		{
			get
			{
				if (_cachedSeaLayer != null)
				{
					return _cachedSeaLayer;
				}
				if (Singleton<SeaCreatureManager>.Instance != null && Singleton<SeaCreatureManager>.Instance.TryGetLayer(_seaLayerIndex, out var layer))
				{
					_cachedSeaLayer = layer;
				}
				return _cachedSeaLayer;
			}
		}

		public void Initialize(Action<MapHabitatMarker> clicked)
		{
			_clicked = clicked;
			if (_button == null)
			{
				_button = GetComponent<BaseButton>();
			}
			RefreshName();
			if (_button != null)
			{
				_button.Clicked -= OnClicked;
				_button.Clicked += OnClicked;
			}
		}

		public void SetDiscovered(bool discovered)
		{
			_isDiscovered = discovered;
			RefreshName();
			if (_lockedRoot != null)
			{
				_lockedRoot.SetActive(!discovered);
			}
			if (_unlockedRoot != null)
			{
				_unlockedRoot.SetActive(discovered);
			}
			if (_questionIcon != null)
			{
				_questionIcon.enabled = !discovered;
			}
			if (_nameText != null)
			{
				_nameText.gameObject.SetActive(discovered);
			}
		}

		public string GetDisplayName()
		{
			SeaLayer seaLayer = SeaLayer;
			if (seaLayer != null)
			{
				return TabletLocalization.GetHabitatName((seaLayer.DisplayName ?? seaLayer.name).Trim());
			}
			if (!string.IsNullOrWhiteSpace(_fallbackDisplayName))
			{
				return TabletLocalization.GetHabitatName(_fallbackDisplayName.Trim());
			}
			return TabletLocalization.Format("tablet.map.habitat", "Habitat {0}", _seaLayerIndex);
		}

		private void RefreshName()
		{
			if (_nameText != null)
			{
				_nameText.text = GetDisplayName();
			}
		}

		private void OnClicked()
		{
			if (_isDiscovered || _allowClickWhenLocked)
			{
				_clicked?.Invoke(this);
			}
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
