using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Networking;
using PaperPixelGames.Wheel;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Managers
{
	public class PlayersManager : Singleton<PlayersManager>
	{
		public const int MaxPlayerSpawnSlots = 5;

		[SerializeField]
		private Transform _playerSpawnPoint;

		[SerializeField]
		private List<Transform> _playerSpawnPoints = new List<Transform>(5);

		[SerializeField]
		private List<Color> _playerColorPalette = new List<Color>();

		[SerializeField]
		private List<Material> _pingMaterialPalette = new List<Material>();

		[SerializeField]
		private List<Material> _seaScooterMaterialPalette = new List<Material>();

		[SerializeField]
		private List<Material> _fishingRodMaterialPalette = new List<Material>();

		[SerializeField]
		private Transform _restaurantSpawnPoint;

		[SerializeField]
		private bool _spawnOnRestaurant;

		private readonly Dictionary<ulong, byte> _colorIndexByClientId = new Dictionary<ulong, byte>();

		private readonly Dictionary<ulong, PPG_Player> _playersById = new Dictionary<ulong, PPG_Player>();

		private readonly Dictionary<string, bool> _underwaterDict = new Dictionary<string, bool>();

		public IReadOnlyDictionary<ulong, PPG_Player> Players => _playersById;

		public IReadOnlyList<Color> PlayerColorPalette => _playerColorPalette;

		public IReadOnlyList<Material> PingMaterialPalette => _pingMaterialPalette;

		public IReadOnlyList<Material> SeaScooterMaterialPalette => _seaScooterMaterialPalette;

		public IReadOnlyList<Material> FishingRodMaterialPalette => _fishingRodMaterialPalette;

		public PPG_Player LocalPlayer { get; private set; }

		public Transform RestaurantSpawnPoint => _restaurantSpawnPoint;

		public Transform PlayerSpawnPoint => _playerSpawnPoint;

		public event Action<PPG_Player> PlayerRegistered;

		public event Action<PPG_Player> PlayerUnregistered;

		public Transform GetPlayerSpawnPointForSlot(int slotIndex)
		{
			if (slotIndex < 0 || _playerSpawnPoints == null || slotIndex >= _playerSpawnPoints.Count)
			{
				return null;
			}
			return _playerSpawnPoints[slotIndex];
		}

		public Vector3 GetFallbackSpawnPosition(int fallbackIndex)
		{
			Transform playerSpawnPoint = PlayerSpawnPoint;
			if (playerSpawnPoint == null)
			{
				return Vector3.right * ((float)fallbackIndex * 1.5f);
			}
			return playerSpawnPoint.position + Vector3.right * ((float)fallbackIndex * 1.5f);
		}

		public Quaternion GetFallbackSpawnRotation()
		{
			Transform playerSpawnPoint = PlayerSpawnPoint;
			if (!(playerSpawnPoint != null))
			{
				return Quaternion.identity;
			}
			return playerSpawnPoint.rotation;
		}

		public void RegisterPlayer(PPG_Player player)
		{
			ulong ownerClientId = player.OwnerClientId;
			if (ownerClientId == NetworkManager.Singleton.LocalClientId)
			{
				LocalPlayer = player;
				LocalPlayer.LocalRegistered();
				Singleton<HintsManager>.Instance.CachePlayerInput(LocalPlayer.PlayerInput);
				Singleton<PPG_WheelManager>.Instance.LocalPlayer = LocalPlayer;
				Singleton<GameProgressManager>.Instance?.RestaurantManager?.RefreshCompassTargetForLocalPlayer();
			}
			if (!_playersById.ContainsKey(ownerClientId))
			{
				_playersById[ownerClientId] = player;
			}
			if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
			{
				if (player.ColorIndex.Value == byte.MaxValue)
				{
					byte value = AllocateColorIndex();
					player.ColorIndex.Value = value;
					_colorIndexByClientId[ownerClientId] = value;
				}
				else
				{
					_colorIndexByClientId[ownerClientId] = player.ColorIndex.Value;
				}
			}
			else
			{
				if (player.ColorIndex.Value != byte.MaxValue)
				{
					_colorIndexByClientId[ownerClientId] = player.ColorIndex.Value;
				}
				ulong capturedId = ownerClientId;
				NetworkVariable<byte> colorIndex = player.ColorIndex;
				colorIndex.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Combine(colorIndex.OnValueChanged, (NetworkVariable<byte>.OnValueChangedDelegate)((byte prev, byte curr) =>
				{
					_colorIndexByClientId[capturedId] = curr;
				}));
			}
			player.ClientRegistered();
			PlayerRegistered?.Invoke(player);
		}

		public void UnregisterPlayer(ulong clientId)
		{
			_playersById.TryGetValue(clientId, out var value);
			_playersById.Remove(clientId);
			_colorIndexByClientId.Remove(clientId);
			if (LocalPlayer != null && LocalPlayer.OwnerClientId == clientId)
			{
				LocalPlayer = null;
			}
			if (value != null)
			{
				if (value.NfgoPlayer != null)
				{
					_underwaterDict.Remove(value.NfgoPlayer.PlayerId);
				}
				PlayerUnregistered?.Invoke(value);
			}
		}

		public void ClearSessionStateForMainMenu()
		{
			int count = _playersById.Count;
			int count2 = _underwaterDict.Count;
			_playersById.Clear();
			_colorIndexByClientId.Clear();
			_underwaterDict.Clear();
			LocalPlayer = null;
			PlayerRegistered = null;
			PlayerUnregistered = null;
			UtilityMethods.Log($"[PlayersManager] Cleared session state for main menu. Players={count}, UnderwaterEntries={count2}");
		}

		public void ServerResetPlayersForEndDay()
		{
			if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
			{
				return;
			}
			foreach (PPG_Player value in _playersById.Values)
			{
				if (!(value == null))
				{
					value.ServerResetForEndDay();
				}
			}
		}

		public bool TryGetPlayer(ulong clientId, out PPG_Player player)
		{
			return _playersById.TryGetValue(clientId, out player);
		}

		public bool TryGetPlayer(string speakerId, out PPG_Player player)
		{
			player = _playersById.Values.FirstOrDefault((PPG_Player x) => x.NfgoPlayer.PlayerId == speakerId);
			return player != null;
		}

		public bool TryGetPlayerNameBySteamId(ulong steamId, out string playerName)
		{
			playerName = string.Empty;
			if (steamId == 0L || steamId == ulong.MaxValue)
			{
				return false;
			}
			PPG_Player pPG_Player = _playersById.Values.FirstOrDefault((PPG_Player x) => x != null && x.SteamID.Value == steamId);
			if (pPG_Player == null)
			{
				return false;
			}
			playerName = pPG_Player.PlayerName.Value.ToString();
			return true;
		}

		public bool TryGetPlayerBySteamId(ulong steamId, out PPG_Player player)
		{
			player = _playersById.Values.FirstOrDefault((PPG_Player x) => x != null && x.SteamID.Value == steamId);
			return player != null;
		}

		public void KickPlayer(ulong clientId)
		{
			MultiplayerCoordinator instance = Singleton<MultiplayerCoordinator>.Instance;
			if (!(instance == null))
			{
				LobbyRoomResult lobbyRoomResult = instance.KickRoomClient(clientId);
				UtilityMethods.Log(lobbyRoomResult.Success ? $"Client {clientId} kicked." : lobbyRoomResult.Message);
			}
		}

		public void EnterUnderwater(string speakerId)
		{
			if (!_underwaterDict.ContainsKey(speakerId))
			{
				_underwaterDict.Add(speakerId, value: true);
			}
			else
			{
				_underwaterDict[speakerId] = true;
			}
		}

		public void ExitUnderwater(string speakerId)
		{
			if (!_underwaterDict.ContainsKey(speakerId))
			{
				_underwaterDict.Add(speakerId, value: false);
			}
			else
			{
				_underwaterDict[speakerId] = false;
			}
		}

		public bool IsLocalUnderWater()
		{
			if (LocalPlayer == null || LocalPlayer.NfgoPlayer == null)
			{
				return false;
			}
			return IsPlayerUnderwater(LocalPlayer.NfgoPlayer.PlayerId);
		}

		public bool IsPlayerUnderwater(string speakerId)
		{
			bool value;
			return _underwaterDict.TryGetValue(speakerId, out value) & value;
		}

		public bool TryGetPlayerColor(ulong clientId, out Color color)
		{
			color = Color.white;
			if (_playerColorPalette == null || _playerColorPalette.Count == 0)
			{
				return false;
			}
			if (_colorIndexByClientId.TryGetValue(clientId, out var value))
			{
				int index = Mathf.Clamp(value, 0, (byte)(_playerColorPalette.Count - 1));
				color = _playerColorPalette[index];
				return true;
			}
			if (_playersById.TryGetValue(clientId, out var value2) && value2 != null && value2.ColorIndex.Value != byte.MaxValue)
			{
				byte value3 = value2.ColorIndex.Value;
				_colorIndexByClientId[clientId] = value3;
				int index2 = Mathf.Clamp(value3, 0, (byte)(_playerColorPalette.Count - 1));
				color = _playerColorPalette[index2];
				return true;
			}
			return false;
		}

		public bool TryGetPingMaterial(byte colorIndex, out Material material)
		{
			material = null;
			if (_pingMaterialPalette == null || _pingMaterialPalette.Count == 0)
			{
				return false;
			}
			if (colorIndex == byte.MaxValue)
			{
				return false;
			}
			int index = Mathf.Clamp(colorIndex, 0, _pingMaterialPalette.Count - 1);
			material = _pingMaterialPalette[index];
			return material != null;
		}

		public bool TryGetSeaScooterMaterial(byte colorIndex, out Material material)
		{
			material = null;
			if (_seaScooterMaterialPalette == null || _seaScooterMaterialPalette.Count == 0)
			{
				return false;
			}
			if (colorIndex == byte.MaxValue)
			{
				return false;
			}
			int index = Mathf.Clamp(colorIndex, 0, _seaScooterMaterialPalette.Count - 1);
			material = _seaScooterMaterialPalette[index];
			return material != null;
		}

		public bool TryGetFishingRodMaterial(byte colorIndex, out Material material)
		{
			material = null;
			if (_fishingRodMaterialPalette == null || _fishingRodMaterialPalette.Count == 0)
			{
				return false;
			}
			if (colorIndex == byte.MaxValue)
			{
				return false;
			}
			int index = Mathf.Clamp(colorIndex, 0, _fishingRodMaterialPalette.Count - 1);
			material = _fishingRodMaterialPalette[index];
			return material != null;
		}

		private byte AllocateColorIndex()
		{
			if (_playerColorPalette == null || _playerColorPalette.Count == 0)
			{
				return 0;
			}
			HashSet<byte> hashSet = new HashSet<byte>(_colorIndexByClientId.Values);
			for (byte b = 0; b < _playerColorPalette.Count; b++)
			{
				if (!hashSet.Contains(b))
				{
					return b;
				}
			}
			return (byte)(hashSet.Count % _playerColorPalette.Count);
		}

		public bool TryGetPlayerColorBySteamId(ulong steamId, out Color color)
		{
			color = Color.white;
			if (steamId == 0L || steamId == ulong.MaxValue)
			{
				return false;
			}
			if (!TryGetPlayerBySteamId(steamId, out var player))
			{
				return false;
			}
			if (player == null)
			{
				return false;
			}
			return TryGetPlayerColor(player.OwnerClientId, out color);
		}
	}
}
