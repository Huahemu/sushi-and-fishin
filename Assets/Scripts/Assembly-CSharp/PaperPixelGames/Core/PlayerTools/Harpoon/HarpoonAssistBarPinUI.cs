using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.PlayerTools.Harpoon
{
	public sealed class HarpoonAssistBarPinUI : MonoBehaviour
	{
		[Header("References")]
		[SerializeField]
		private RectTransform _root;

		[SerializeField]
		private Image _avatarImage;

		[Header("Motion")]
		[SerializeField]
		[Min(1f)]
		private float _smoothSpeed = 18f;

		[SerializeField]
		private float _verticalPadding;

		[SerializeField]
		private bool _invertY;

		private ulong _clientId;

		private ulong _avatarSteamIdRaw;

		private bool _avatarLoadRequested;

		private float _targetValue01;

		private float _visualValue01;

		private float _lastReceiveTime;

		public ulong ClientId => _clientId;

		private void Awake()
		{
			if (_root == null)
			{
				_root = base.transform as RectTransform;
			}
		}

		public void Initialize(ulong clientId, Sprite fallbackAvatar)
		{
			_clientId = clientId;
			_avatarSteamIdRaw = 0uL;
			_avatarLoadRequested = false;
			_targetValue01 = 0f;
			_visualValue01 = 0f;
			_lastReceiveTime = Time.unscaledTime;
			if (_avatarImage != null)
			{
				_avatarImage.sprite = fallbackAvatar;
			}
			base.gameObject.SetActive(value: true);
		}

		public void SetTargetValue(float value01)
		{
			_targetValue01 = Mathf.Clamp01(value01);
			_lastReceiveTime = Time.unscaledTime;
			if (!base.gameObject.activeSelf)
			{
				base.gameObject.SetActive(value: true);
			}
		}

		public void Tick(RectTransform container, float deltaTime)
		{
			if (!(_root == null) && !(container == null))
			{
				_visualValue01 = Mathf.Lerp(_visualValue01, _targetValue01, deltaTime * _smoothSpeed);
				float num = Mathf.Max(1f, container.rect.height - _verticalPadding * 2f);
				float num2 = (_invertY ? (1f - _visualValue01) : _visualValue01);
				float y = _verticalPadding + num * num2;
				Vector2 anchoredPosition = _root.anchoredPosition;
				anchoredPosition.y = y;
				_root.anchoredPosition = anchoredPosition;
			}
		}

		public bool IsExpired(float timeout)
		{
			return Time.unscaledTime - _lastReceiveTime > timeout;
		}

		public bool ShouldLoadAvatar(ulong steamIdRaw)
		{
			if (steamIdRaw == 0L)
			{
				return false;
			}
			if (_avatarSteamIdRaw == steamIdRaw)
			{
				return false;
			}
			return !_avatarLoadRequested;
		}

		public void MarkAvatarLoadRequested(ulong steamIdRaw)
		{
			_avatarSteamIdRaw = steamIdRaw;
			_avatarLoadRequested = true;
		}

		public bool CanApplyAvatar(ulong steamIdRaw)
		{
			return _avatarSteamIdRaw == steamIdRaw;
		}

		public void SetAvatar(ulong steamIdRaw, Sprite avatar)
		{
			if (CanApplyAvatar(steamIdRaw))
			{
				_avatarLoadRequested = false;
				if (_avatarImage != null && avatar != null)
				{
					_avatarImage.sprite = avatar;
				}
			}
		}
	}
}
