using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Networking;
using Steamworks;
using UnityEngine;

namespace PaperPixelGames.Framework.States.Game
{
	public class PlayersScreen : BaseScreen
	{
		[SerializeField]
		private PlayersScreenItem _playerScreenItemPrefab;

		[SerializeField]
		private BaseButton _inviteFriendsButton;

		[SerializeField]
		private BaseButton _backButton;

		[SerializeField]
		private Transform _playersContainer;

		private List<PlayersScreenItem> _playersScreenItemList = new List<PlayersScreenItem>();

		public event Action RequestedClose;

		protected override void OnInitialize()
		{
			if (_inviteFriendsButton != null)
			{
				_inviteFriendsButton.Clicked += OnClickInviteFriendsButton;
			}
			if (_backButton != null)
			{
				_backButton.Clicked += OnClickBackButton;
			}
		}

		protected override void OnHide()
		{
			ClearExistingItems();
		}

		public void LoadCurrentPlayers()
		{
			ClearExistingItems();
			PlayersManager instance = Singleton<PlayersManager>.Instance;
			if (instance == null)
			{
				return;
			}
			IReadOnlyDictionary<ulong, PPG_Player> players = instance.Players;
			if (players == null || _playerScreenItemPrefab == null || _playersContainer == null)
			{
				return;
			}
			foreach (KeyValuePair<ulong, PPG_Player> item in players)
			{
				PPG_Player value = item.Value;
				if (!(value == null))
				{
					PlayersScreenItem playersScreenItem = UnityEngine.Object.Instantiate(_playerScreenItemPrefab, _playersContainer);
					playersScreenItem.Initialize(value);
					_playersScreenItemList.Add(playersScreenItem);
				}
			}
		}

		private void ClearExistingItems()
		{
			if (_playersScreenItemList == null)
			{
				return;
			}
			for (int i = 0; i < _playersScreenItemList.Count; i++)
			{
				PlayersScreenItem playersScreenItem = _playersScreenItemList[i];
				if (playersScreenItem != null)
				{
					UnityEngine.Object.Destroy(playersScreenItem.gameObject);
				}
			}
			_playersScreenItemList.Clear();
		}

		private void OnClickInviteFriendsButton()
		{
			if (SteamClient.IsValid)
			{
				MultiplayerCoordinator instance = Singleton<MultiplayerCoordinator>.Instance;
				if (!(instance == null) && !(instance.LobbyService == null))
				{
					SteamFriends.OpenGameInviteOverlay(instance.LobbyService.CurrentLobby.Id);
				}
			}
		}

		private void OnClickBackButton()
		{
			Hide();
			RequestedClose?.Invoke();
		}
	}
}
