using Cysharp.Threading.Tasks;
using Steamworks;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Framework.UI
{
	public class SleepingPlayerAvatarUI : MonoBehaviour
	{
		[Header("Images")]
		[SerializeField]
		private Image _steamAvatarImage;

		[SerializeField]
		private Image _zzImage;

		private ulong _steamIdValue;

		private SteamId _steamId;

		private int _avatarLoadVersion;

		private void Awake()
		{
			SetZzImageVisible(isVisible: false);
		}

		private void OnDisable()
		{
			_avatarLoadVersion++;
			SetZzImageVisible(isVisible: false);
		}

		private void OnDestroy()
		{
			_avatarLoadVersion++;
		}

		public void SetRootVisible(bool isVisible)
		{
			base.gameObject.SetActive(isVisible);
			if (!isVisible)
			{
				SetZzImageVisible(isVisible: false);
			}
		}

		public void SetSteamId(ulong steamIdValue)
		{
			_steamIdValue = steamIdValue;
			_steamId = steamIdValue;
			LoadAvatarAsync().Forget();
		}

		public void SetSteamId(SteamId steamId)
		{
			_steamId = steamId;
			_steamIdValue = steamId.Value;
			LoadAvatarAsync().Forget();
		}

		public void SetZzImageVisible(bool isVisible)
		{
			if (!(_zzImage == null))
			{
				_zzImage.gameObject.SetActive(isVisible);
			}
		}

		public ulong GetSteamIdValue()
		{
			return _steamIdValue;
		}

		private async UniTaskVoid LoadAvatarAsync()
		{
			int version = ++_avatarLoadVersion;
			if (_steamAvatarImage == null || _steamId.Value == 0L)
			{
				return;
			}
			Sprite sprite = _steamAvatarImage.sprite;
			Sprite avatarSprite = await SteamAvatarCache.GetAvatarSpriteAsync(_steamId, sprite);
			if (!(this == null) && version == _avatarLoadVersion)
			{
				await UniTask.SwitchToMainThread();
				if (!(_steamAvatarImage == null))
				{
					_steamAvatarImage.sprite = avatarSprite;
				}
			}
		}
	}
}
