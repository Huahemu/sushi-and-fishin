using System;
using Steamworks.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperPixelGames.Framework.States.MainMenu
{
	public class LobbyDataUI : MonoBehaviour, ISelectHandler, IEventSystemHandler, IDeselectHandler
	{
		private Action<Lobby> _onSelect;

		[SerializeField]
		private TextMeshProUGUI _lobbyName;

		[SerializeField]
		private TextMeshProUGUI _numberOfPlayers;

		[SerializeField]
		private TextMeshProUGUI _ping;

		[SerializeField]
		private GameObject _selectedOutline;

		private Selectable _selectable;

		private Lobby _lobby;

		public Selectable Selectable => _selectable;

		public void Initialize(Lobby lobby, Action<Lobby> callback, int ping = 50)
		{
			_selectable = GetComponent<Selectable>();
			_lobby = lobby;
			string data = lobby.GetData("name");
			_lobbyName.text = ((data != "") ? data : "Untitled").ToString();
			UpdateNumberOfPlayers(lobby.MemberCount);
			UpdatePing(ping);
			_onSelect = callback;
		}

		private void UpdateNumberOfPlayers(int numberOfPlayers)
		{
			_numberOfPlayers.text = $"{_lobby.MemberCount}/{_lobby.MaxMembers}";
		}

		private void UpdatePing(int ping)
		{
			ping = UnityEngine.Random.Range(35, 75);
			_ping.text = ping.ToString();
		}

		private void SetSelected(bool selectValue)
		{
			_selectedOutline.SetActive(selectValue);
		}

		public void OnSelect(BaseEventData eventData)
		{
			SetSelected(selectValue: true);
			_onSelect?.Invoke(_lobby);
		}

		public void OnDeselect(BaseEventData eventData)
		{
			SetSelected(selectValue: false);
		}
	}
}
