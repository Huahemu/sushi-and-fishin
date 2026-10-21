using PaperPixelGames.Core.Struct;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Tablet
{
	public class FishBookScoreBoardItem : MonoBehaviour
	{
		[SerializeField]
		private TextMeshProUGUI _rankText;

		[SerializeField]
		private TextMeshProUGUI _playerName;

		[SerializeField]
		private TextMeshProUGUI _playerRecord;

		[SerializeField]
		private Image _playerIcon;

		private int _avatarRequestVersion;

		public async void UpdateData(int rank, FishLeaderboardEntry entry)
		{
			int version = ++_avatarRequestVersion;
			if (_rankText != null)
			{
				_rankText.text = $"#{rank}";
			}
			if (entry.SteamId == 0L && entry.BestWeightKg <= 0f && entry.PlayerName.Length == 0)
			{
				ClearData(rank);
				return;
			}
			if (_playerName != null)
			{
				_playerName.text = entry.PlayerName.ToString();
			}
			if (_playerRecord != null)
			{
				_playerRecord.text = $"{entry.BestWeightKg:F2} kg";
			}
			if (_playerIcon == null)
			{
				return;
			}
			Sprite sprite = await SteamAvatarCache.GetAvatarSpriteAsync(entry.SteamId);
			if (version == _avatarRequestVersion)
			{
				if (sprite != null)
				{
					_playerIcon.sprite = sprite;
					_playerIcon.enabled = true;
				}
				else
				{
					_playerIcon.sprite = null;
					_playerIcon.enabled = false;
				}
			}
		}

		public void ClearData(int rank)
		{
			_avatarRequestVersion++;
			if (_rankText != null)
			{
				_rankText.text = $"#{rank}";
			}
			if (_playerName != null)
			{
				_playerName.text = "-";
			}
			if (_playerRecord != null)
			{
				_playerRecord.text = "-";
			}
			if (_playerIcon != null)
			{
				_playerIcon.sprite = null;
				_playerIcon.enabled = false;
			}
		}
	}
}
