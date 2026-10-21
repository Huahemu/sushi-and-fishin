using System.Collections;
using System.Collections.Generic;
using Lean.Pool;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Networking;
using Steamworks;
using Steamworks.Data;
using TMPro;
using UnityEngine;

namespace PaperPixelGames.Framework.States.MainMenu
{
	public class LobbyHandler : MonoBehaviour
	{
		[SerializeField]
		private BaseButton _backButton;

		[SerializeField]
		private BaseButton _createButton;

		[SerializeField]
		private BaseButton _joinWithCodeButton;

		[SerializeField]
		private BaseButton _refreshButton;

		[SerializeField]
		private BaseButton _joinButton;

		[Space(10f)]
		[SerializeField]
		private GameObject _joinWithCodePopUp;

		[SerializeField]
		private BaseButton _joinWithCodePopUpJoinButton;

		[SerializeField]
		private BaseButton _joinWithCodePopUpBackButton;

		[SerializeField]
		private TMP_InputField _joinWithCodeInputField;

		[Space(10f)]
		[SerializeField]
		private GameObject _parent;

		[SerializeField]
		private LobbyDataUI _lobbyDataUI;

		[SerializeField]
		private Transform _lobbyListContainer;

		[SerializeField]
		private string _inputCode;

		private Dictionary<ulong, LobbyDataUI> _lobbyListElements = new Dictionary<ulong, LobbyDataUI>();

		private Dictionary<ulong, Coroutine> _lobbyRefreshCoroutines = new Dictionary<ulong, Coroutine>();

		private Lobby _selectedLobby;

		private bool _canJoinSelected;

		private const float _lobbyRefreshInterval = 5f;

		private float _lastTimeLobbyRequest;

		private float _waitTimeLobbyRequest = 3f;

		public List<Lobby> _lobbies = new List<Lobby>();

		public void Initialize()
		{
			_backButton.Clicked += OnClickBackButton;
			_createButton.Clicked += OnClickCreateButton;
			_joinWithCodeButton.Clicked += OnClickJoinWithCodeButton;
			_refreshButton.Clicked += OnClickRefreshButton;
			_joinButton.Clicked += OnClickJoinButton;
			_joinWithCodePopUpJoinButton.Clicked += OnClickJoinWithCodePopUpJoinButton;
			_joinWithCodePopUpBackButton.Clicked += OnClickJoinWithCodePopUpBackButton;
			_joinWithCodeInputField.onValueChanged.RemoveAllListeners();
			_joinWithCodeInputField.onValueChanged.AddListener(OnValueChangedInputField);
			if (Singleton<MultiplayerCoordinator>.Instance != null)
			{
				Singleton<MultiplayerCoordinator>.Instance.LobbyService.LobbyListUpdated -= OnLobbyList;
				Singleton<MultiplayerCoordinator>.Instance.LobbyService.LobbyListUpdated += OnLobbyList;
			}
			GetCurrentLobies();
		}

		public void Show()
		{
			base.gameObject.SetActive(value: true);
			_parent.gameObject.SetActive(value: true);
		}

		private void OnLobbyList(List<Lobby> lobbies)
		{
			_lobbies = lobbies;
			bool flag = true;
			foreach (Lobby lobby in _lobbies)
			{
				LobbyDataUI lobbyDataUI = LeanPool.Spawn(_lobbyDataUI, _lobbyListContainer.transform);
				lobbyDataUI.transform.localScale = Vector3.one;
				lobbyDataUI.Initialize(lobby, OnLobbySelect);
				_lobbyListElements[lobby.Id] = lobbyDataUI;
				if ((ulong)lobby.Owner.Id == 0)
				{
					if (!_lobbyRefreshCoroutines.ContainsKey(lobby.Id))
					{
						_lobbyRefreshCoroutines[lobby.Id] = StartCoroutine(LobbyRefreshCoroutine(lobby));
					}
				}
				else
				{
					_lobbyRefreshCoroutines.Remove(lobby.Id);
				}
				if (flag)
				{
					_selectedLobby = lobby;
					_canJoinSelected = (ulong)lobby.Owner.Id != 0L && lobby.MemberCount < lobby.MaxMembers;
					_joinButton.interactable = _canJoinSelected;
					flag = false;
				}
			}
		}

		private IEnumerator LobbyRefreshCoroutine(Lobby lobby)
		{
			float elapsedTime = 0f;
			byte attempts = 0;
			lobby.Refresh();
			while ((ulong)lobby.Owner.Id == 0L)
			{
				yield return null;
				elapsedTime += Time.deltaTime;
				if (elapsedTime >= 5f)
				{
					elapsedTime = 0f;
					attempts++;
					lobby.Refresh();
				}
			}
			OnLobbyRefresh(lobby);
			_lobbyRefreshCoroutines.Remove(lobby.Id);
		}

		private void OnLobbyRefresh(Lobby lobby)
		{
			if ((ulong)lobby.Owner.Id == 0L)
			{
				if (_lobbyListElements.TryGetValue(lobby.Id, out var value))
				{
					LeanPool.Despawn(value);
					_lobbyListElements.Remove(lobby.Id);
				}
			}
			else
			{
				_lobbyListElements[lobby.Id].Initialize(lobby, OnLobbySelect);
			}
		}

		private void GetCurrentLobies()
		{
			_refreshButton.interactable = false;
			_canJoinSelected = false;
			_joinButton.interactable = false;
			_lastTimeLobbyRequest = Time.time;
			foreach (LobbyDataUI value in _lobbyListElements.Values)
			{
				LeanPool.Despawn(value);
			}
			_lobbyListElements.Clear();
			StopAllCoroutines();
			_lobbyRefreshCoroutines.Clear();
			Singleton<MultiplayerCoordinator>.Instance.RefreshLobbiesAsync();
			Singleton<DelayHelper>.Instance.DelayAction(_waitTimeLobbyRequest, () =>
			{
				_refreshButton.interactable = true;
			});
		}

		private void OnClickBackButton()
		{
			_parent.SetActive(value: false);
		}

		private void OnClickCreateButton()
		{
		}

		private void OnClickJoinWithCodeButton()
		{
			_joinWithCodeInputField.text = "";
			_joinWithCodePopUp.SetActive(value: true);
		}

		private void OnClickRefreshButton()
		{
			GetCurrentLobies();
		}

		private void OnClickJoinButton()
		{
			if ((ulong)_selectedLobby.Id == 0L)
			{
				_canJoinSelected = false;
				_joinButton.interactable = _canJoinSelected;
			}
		}

		private async void OnClickJoinWithCodePopUpJoinButton()
		{
			if (string.IsNullOrEmpty(_inputCode))
			{
				UtilityMethods.Log("Join Failed: No code entered", LogType.Warning);
				return;
			}
			UtilityMethods.Log("Searching for lobby with code: " + _inputCode);
			Lobby[] array = await SteamMatchmaking.LobbyList.FilterDistanceWorldwide().WithSlotsAvailable(1).RequestAsync();
			if (array == null || array.Length == 0)
			{
				UtilityMethods.Log("No lobbies found.", LogType.Warning);
				return;
			}
			Lobby[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				Lobby lobby = array2[i];
				if (lobby.GetData("code") == _inputCode)
				{
					UtilityMethods.Log($"Lobby found! Joining... ID: {lobby.Id.Value}");
					return;
				}
			}
			UtilityMethods.Log("No lobby found with the entered code.", LogType.Warning);
		}

		private void OnClickJoinWithCodePopUpBackButton()
		{
			_joinWithCodePopUp.SetActive(value: false);
		}

		private void OnValueChangedInputField(string input)
		{
			_inputCode = input;
		}

		private void OnLobbySelect(Lobby lobby)
		{
			_selectedLobby = lobby;
			_canJoinSelected = (ulong)_selectedLobby.Owner.Id != 0L && _selectedLobby.MemberCount < _selectedLobby.MaxMembers;
			_joinButton.interactable = _canJoinSelected;
		}
	}
}
