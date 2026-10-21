using System.Threading.Tasks;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Dissonance;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Framework.States.Game
{
	public class PlayersScreenItem : MonoBehaviour
	{
		[SerializeField]
		private Image _playerIcon;

		[SerializeField]
		private TextMeshProUGUI _playerName;

		[SerializeField]
		private BaseButton _kickButton;

		[SerializeField]
		private Image _hostIcon;

		[Header("Voice UI")]
		[SerializeField]
		private Slider _voiceVolumeSlider;

		[Header("Defaults")]
		[SerializeField]
		private Sprite _defaultAvatarSprite;

		private ulong _targetClientId;

		private int _initVersion;

		private string _voicePlayerKey;

		private bool _volumeBound;

		private bool _volumeDirty;

		private const string VolumePrefPrefix = "voice.player_volume.";

		private void OnDestroy()
		{
			UnbindKick();
			UnbindVolume();
			if (_volumeDirty)
			{
				PlayerPrefs.Save();
			}
		}

		public void Initialize(PPG_Player playerData)
		{
			InitializeInternalAsync(playerData);
		}

		private async Task InitializeInternalAsync(PPG_Player playerData)
		{
			if (playerData == null)
			{
				return;
			}
			int version = ++_initVersion;
			UnbindKick();
			UnbindVolume();
			_voicePlayerKey = ResolveVoicePlayerKey(playerData);
			if (_hostIcon != null)
			{
				_hostIcon.gameObject.SetActive(playerData.IsThisHost);
			}
			if (_playerName != null)
			{
				_playerName.text = playerData.PlayerName.Value.ToString();
			}
			_targetClientId = playerData.OwnerClientId;
			if (_playerIcon != null && _defaultAvatarSprite != null)
			{
				_playerIcon.sprite = _defaultAvatarSprite;
			}
			PlayersManager instance = Singleton<PlayersManager>.Instance;
			bool flag = instance != null && instance.LocalPlayer != null && instance.LocalPlayer.IsThisHost && !playerData.IsThisHost;
			if (_kickButton != null)
			{
				_kickButton.gameObject.SetActive(flag);
			}
			if (flag)
			{
				BindKick();
			}
			bool flag2 = instance != null && instance.LocalPlayer != null && instance.LocalPlayer.OwnerClientId == playerData.OwnerClientId;
			if (_voiceVolumeSlider != null)
			{
				_voiceVolumeSlider.gameObject.SetActive(!flag2);
			}
			if (!flag2)
			{
				SetupVoiceVolumeUI();
			}
			if (ulong.TryParse(playerData.SteamID.Value.ToString(), out var result))
			{
				Sprite sprite;
				try
				{
					sprite = await SteamAvatarCache.GetAvatarSpriteAsync(result);
				}
				catch
				{
					sprite = null;
				}
				if (!(this == null) && version == _initVersion && _playerIcon != null)
				{
					_playerIcon.sprite = ((sprite != null) ? sprite : _defaultAvatarSprite);
				}
			}
		}

		private string ResolveVoicePlayerKey(PPG_Player playerData)
		{
			if (playerData == null)
			{
				return null;
			}
			if (playerData.NfgoPlayer != null && !string.IsNullOrWhiteSpace(playerData.NfgoPlayer.PlayerId))
			{
				return playerData.NfgoPlayer.PlayerId;
			}
			string text = playerData.SteamID.Value.ToString();
			if (!string.IsNullOrWhiteSpace(text))
			{
				return text;
			}
			return null;
		}

		private void SetupVoiceVolumeUI()
		{
			if (_voiceVolumeSlider == null)
			{
				return;
			}
			if (string.IsNullOrWhiteSpace(_voicePlayerKey))
			{
				_voiceVolumeSlider.gameObject.SetActive(value: false);
				return;
			}
			_voiceVolumeSlider.gameObject.SetActive(value: true);
			_voiceVolumeSlider.minValue = 0f;
			_voiceVolumeSlider.maxValue = 1f;
			float value = PlayerPrefs.GetFloat(GetVolumePrefKey(), 1f);
			value = Mathf.Clamp01(value);
			DissonanceManager instance = Singleton<DissonanceManager>.Instance;
			if (instance != null)
			{
				value = instance.GetPlayerVolumeByPlayerId(_voicePlayerKey, value);
			}
			_voiceVolumeSlider.SetValueWithoutNotify(value);
			ApplyVoiceVolume(value);
			BindVolume();
		}

		private void OnVolumeSliderChanged(float value)
		{
			value = Mathf.Clamp01(value);
			ApplyVoiceVolume(value);
			PlayerPrefs.SetFloat(GetVolumePrefKey(), value);
			_volumeDirty = true;
		}

		private void ApplyVoiceVolume(float value)
		{
			DissonanceManager instance = Singleton<DissonanceManager>.Instance;
			if (!(instance == null) && !string.IsNullOrWhiteSpace(_voicePlayerKey))
			{
				instance.TrySetPlayerVolumeByPlayerId(_voicePlayerKey, value);
			}
		}

		private void BindVolume()
		{
			if (!_volumeBound && !(_voiceVolumeSlider == null))
			{
				_volumeBound = true;
				_voiceVolumeSlider.onValueChanged.AddListener(OnVolumeSliderChanged);
			}
		}

		private void UnbindVolume()
		{
			if (_volumeBound)
			{
				_volumeBound = false;
				if (_voiceVolumeSlider != null)
				{
					_voiceVolumeSlider.onValueChanged.RemoveListener(OnVolumeSliderChanged);
				}
			}
		}

		private string GetVolumePrefKey()
		{
			return "voice.player_volume." + _voicePlayerKey;
		}

		private void BindKick()
		{
			if (!(_kickButton == null))
			{
				_kickButton.Clicked -= OnClickKickButton;
				_kickButton.Clicked += OnClickKickButton;
			}
		}

		private void UnbindKick()
		{
			if (!(_kickButton == null))
			{
				_kickButton.Clicked -= OnClickKickButton;
			}
		}

		private void OnClickKickButton()
		{
			PlayersManager instance = Singleton<PlayersManager>.Instance;
			PPG_Player pPG_Player = ((instance != null) ? instance.LocalPlayer : null);
			if (!(pPG_Player == null) && pPG_Player.IsThisHost)
			{
				instance.KickPlayer(_targetClientId);
			}
		}
	}
}
