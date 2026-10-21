using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Bozo.ModularCharacters;
using Cysharp.Threading.Tasks;
using PaperPixelGames.Core.Player.Visuals;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Networking;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace PaperPixelGames.Framework.States.MainMenu
{
	[DisallowMultipleComponent]
	internal sealed class LobbyCharacterCustomizationSync : MonoBehaviour
	{
		private sealed class PendingTransfer
		{
			public readonly string[] Chunks;

			public int ReceivedChunkCount;

			public float LastReceiveTime;

			public PendingTransfer(int chunkCount)
			{
				Chunks = new string[chunkCount];
				LastReceiveTime = Time.realtimeSinceStartup;
			}
		}

		private const string RequestProtocol = "CCREQ1";

		private const string DataProtocol = "CCDAT1";

		private const int ChunkPayloadLength = 2800;

		private const int MaxChunkCount = 128;

		private const int MaxJsonLength = 1048576;

		private const float PendingTransferLifetimeSeconds = 20f;

		private readonly Dictionary<ulong, CharacterData> _characterDataBySteamId = new Dictionary<ulong, CharacterData>();

		private readonly Dictionary<string, PendingTransfer> _pendingTransfers = new Dictionary<string, PendingTransfer>();

		private readonly Dictionary<ulong, int> _applyVersionBySteamId = new Dictionary<ulong, int>();

		private MainMenuManager _mainMenu;

		private MainMenuCustomizationSceneHost _customizationHost;

		private SteamLobbyService _steam;

		private ulong _activeLobbyId;

		private bool _initialized;

		private GameObject _lastLocalPuppetRoot;

		public void Initialize(MainMenuManager mainMenu, MainMenuCustomizationSceneHost customizationHost, SteamLobbyService steam)
		{
			if (!_initialized || !(_mainMenu == mainMenu) || !(_customizationHost == customizationHost) || !(_steam == steam))
			{
				Unbind();
				_mainMenu = mainMenu;
				_customizationHost = customizationHost;
				_steam = steam;
				_lastLocalPuppetRoot = ((_mainMenu != null) ? _mainMenu.GetLocalCharModel() : null);
				Bind();
				_initialized = true;
				if (_steam != null && (ulong)_steam.CurrentLobby.Id != 0L)
				{
					HandleLobbyActivated(_steam.CurrentLobby);
				}
			}
		}

		private void OnDestroy()
		{
			Unbind();
		}

		private void Bind()
		{
			if (_customizationHost != null)
			{
				_customizationHost.LocalCustomizationChanged += OnLocalCustomizationChanged;
			}
			if (!(_steam == null))
			{
				_steam.LobbyCreated += OnLobbyCreated;
				_steam.LobbyEntered += OnLobbyEntered;
				_steam.LobbyMemberJoined += OnLobbyMemberJoined;
				_steam.LobbyMemberLeft += OnLobbyMemberLeft;
				_steam.LobbyInternalMessageReceived += OnLobbyInternalMessageReceived;
			}
		}

		private void Unbind()
		{
			if (_customizationHost != null)
			{
				_customizationHost.LocalCustomizationChanged -= OnLocalCustomizationChanged;
			}
			if (_steam != null)
			{
				_steam.LobbyCreated -= OnLobbyCreated;
				_steam.LobbyEntered -= OnLobbyEntered;
				_steam.LobbyMemberJoined -= OnLobbyMemberJoined;
				_steam.LobbyMemberLeft -= OnLobbyMemberLeft;
				_steam.LobbyInternalMessageReceived -= OnLobbyInternalMessageReceived;
			}
			_initialized = false;
		}

		private void OnLobbyCreated(Lobby lobby)
		{
			HandleLobbyActivated(lobby);
		}

		private void OnLobbyEntered(Lobby lobby)
		{
			HandleLobbyActivated(lobby);
		}

		private void HandleLobbyActivated(Lobby lobby)
		{
			if ((ulong)lobby.Id != 0L && MainMenuManager.TryGetLocalSteamId(out var _))
			{
				ulong value = lobby.Id.Value;
				if (_activeLobbyId != value)
				{
					_activeLobbyId = value;
					_characterDataBySteamId.Clear();
					_pendingTransfers.Clear();
					_applyVersionBySteamId.Clear();
				}
				_mainMenu?.RefreshLobbyPuppetNameTags(lobby);
				SendLocalSnapshotAsync().Forget();
				_steam?.SendLobbyInternalMessage("CCREQ1");
			}
		}

		private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
		{
			if (IsActiveLobby(lobby))
			{
				_mainMenu?.RefreshLobbyPuppetNameTags(lobby);
				RemapCachedCharactersAsync().Forget();
				SendLocalSnapshotAsync().Forget();
				if (MainMenuManager.TryGetLocalSteamId(out var steamId) && (ulong)friend.Id == (ulong)steamId)
				{
					_steam?.SendLobbyInternalMessage("CCREQ1");
				}
			}
		}

		private void OnLobbyMemberLeft(Lobby lobby, Friend friend)
		{
			if (IsActiveLobby(lobby))
			{
				ulong value = friend.Id.Value;
				_characterDataBySteamId.Remove(value);
				_applyVersionBySteamId.Remove(value);
				_mainMenu?.RefreshLobbyPuppetNameTags(lobby);
				RemapCachedCharactersAsync().Forget();
			}
		}

		private void OnLocalCustomizationChanged(CharacterData _)
		{
			if (MainMenuManager.TryGetLocalSteamId(out var steamId))
			{
				CharacterData currentCharacterData = PlayerCustomizationSession.GetCurrentCharacterData();
				if (currentCharacterData != null)
				{
					_characterDataBySteamId[steamId.Value] = currentCharacterData;
					SendCharacterDataAsync(currentCharacterData).Forget();
					RemapRemoteCharactersAsync().Forget();
				}
			}
		}

		private void OnLobbyInternalMessageReceived(Lobby lobby, Friend friend, string payload)
		{
			if (!IsActiveLobby(lobby) || string.IsNullOrEmpty(payload))
			{
				return;
			}
			SteamId steamId2;
			if (payload == "CCREQ1")
			{
				if (MainMenuManager.TryGetLocalSteamId(out var steamId) && (ulong)friend.Id != (ulong)steamId)
				{
					SendLocalSnapshotAsync().Forget();
				}
			}
			else if (MainMenuManager.TryGetLocalSteamId(out steamId2) && (ulong)friend.Id != (ulong)steamId2 && payload.StartsWith("CCDAT1|", StringComparison.Ordinal))
			{
				CleanupExpiredTransfers();
				ReceiveCharacterChunk(friend.Id, payload);
			}
		}

		private async UniTaskVoid SendLocalSnapshotAsync()
		{
			await UniTask.Yield();
			if (_steam == null || (ulong)_steam.CurrentLobby.Id == 0L || _mainMenu == null || !MainMenuManager.TryGetLocalSteamId(out var steamId))
			{
				return;
			}
			CharacterData currentCharacterData = PlayerCustomizationSession.GetCurrentCharacterData();
			if (currentCharacterData == null)
			{
				GameObject localCharModel = _mainMenu.GetLocalCharModel();
				if (localCharModel != null)
				{
					PlayerCustomizationSession.CaptureFromMenuPuppets(new GameObject[1] { localCharModel });
					currentCharacterData = PlayerCustomizationSession.GetCurrentCharacterData();
				}
			}
			if (currentCharacterData != null)
			{
				_characterDataBySteamId[steamId.Value] = currentCharacterData;
				QueueLocalCharacterIfSlotChanged(currentCharacterData);
				await SendCharacterDataAsync(currentCharacterData);
			}
		}

		private async UniTask SendCharacterDataAsync(CharacterData data)
		{
			if (_steam == null || (ulong)_steam.CurrentLobby.Id == 0L || data == null)
			{
				return;
			}
			CharacterData obj = CreateNetworkData(data);
			string encodedPayload = CompressToBase64(JsonUtility.ToJson(obj));
			int chunkCount = Mathf.CeilToInt((float)encodedPayload.Length / 2800f);
			if (chunkCount <= 0 || chunkCount > 128)
			{
				UtilityMethods.Log($"[LobbyCharacterCustomizationSync] Character payload rejected. ChunkCount={chunkCount}", LogType.Warning);
				return;
			}
			string transferId = Guid.NewGuid().ToString("N");
			for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
			{
				int num = chunkIndex * 2800;
				int length = Math.Min(2800, encodedPayload.Length - num);
				string text = encodedPayload.Substring(num, length);
				string payload = string.Format("{0}|{1}|{2}|{3}|{4}", "CCDAT1", transferId, chunkIndex, chunkCount, text);
				if (!_steam.SendLobbyInternalMessage(payload))
				{
					UtilityMethods.Log($"[LobbyCharacterCustomizationSync] Failed to send chunk {chunkIndex + 1}/{chunkCount}.", LogType.Warning);
					return;
				}
				await UniTask.Yield();
			}
			UtilityMethods.Log($"[LobbyCharacterCustomizationSync] Sent local character in {chunkCount} chunk(s).");
		}

		private void ReceiveCharacterChunk(SteamId senderId, string payload)
		{
			string[] array = payload.Split(new char[1] { '|' }, 5);
			if (array.Length != 5 || !string.Equals(array[0], "CCDAT1", StringComparison.Ordinal) || !int.TryParse(array[2], out var result) || !int.TryParse(array[3], out var result2) || result2 <= 0 || result2 > 128 || result < 0 || result >= result2)
			{
				return;
			}
			string key = senderId.Value + ":" + array[1];
			if (!_pendingTransfers.TryGetValue(key, out var value) || value.Chunks.Length != result2)
			{
				value = new PendingTransfer(result2);
				_pendingTransfers[key] = value;
			}
			value.LastReceiveTime = Time.realtimeSinceStartup;
			if (value.Chunks[result] == null)
			{
				value.Chunks[result] = array[4];
				value.ReceivedChunkCount++;
			}
			if (value.ReceivedChunkCount != result2)
			{
				return;
			}
			_pendingTransfers.Remove(key);
			try
			{
				CharacterData characterData = JsonUtility.FromJson<CharacterData>(DecompressFromBase64(string.Concat(value.Chunks)));
				if (characterData?.outfitDatas == null)
				{
					throw new InvalidDataException("Character payload did not contain outfit data.");
				}
				RestoreNetworkResourceReferences(characterData);
				_characterDataBySteamId[senderId.Value] = characterData;
				QueueCharacterApply(senderId, characterData);
			}
			catch (Exception ex)
			{
				UtilityMethods.Log($"[LobbyCharacterCustomizationSync] Failed to decode character from {senderId.Value}: {ex.Message}", LogType.Warning);
			}
		}

		private void QueueCharacterApply(SteamId steamId, CharacterData data)
		{
			ulong value = steamId.Value;
			int num = ((!_applyVersionBySteamId.TryGetValue(value, out var value2)) ? 1 : (value2 + 1));
			_applyVersionBySteamId[value] = num;
			ApplyCharacterToPuppetAsync(steamId, data, num).Forget();
		}

		private async UniTaskVoid ApplyCharacterToPuppetAsync(SteamId steamId, CharacterData data, int version)
		{
			await UniTask.Yield();
			if (!(_mainMenu == null) && _applyVersionBySteamId.TryGetValue(steamId.Value, out var value) && value == version && _mainMenu.TryGetCharModelForSteamId(steamId, out var charModel) && !(charModel == null) && charModel.activeInHierarchy)
			{
				OutfitSystem componentInChildren = charModel.GetComponentInChildren<OutfitSystem>(includeInactive: true);
				if (!(componentInChildren == null) && !componentInChildren.isStatic)
				{
					await PlayerCustomizationSession.ApplyCharacterDataToOutfitSystemAsync(componentInChildren, data, $"lobby puppet {steamId.Value}");
				}
			}
		}

		private async UniTaskVoid RemapCachedCharactersAsync()
		{
			await UniTask.Yield();
			if (!MainMenuManager.TryGetLocalSteamId(out var steamId))
			{
				return;
			}
			KeyValuePair<ulong, CharacterData>[] array = _characterDataBySteamId.ToArray();
			for (int i = 0; i < array.Length; i++)
			{
				KeyValuePair<ulong, CharacterData> keyValuePair = array[i];
				SteamId steamId2 = keyValuePair.Key;
				if ((ulong)steamId2 == (ulong)steamId)
				{
					QueueLocalCharacterIfSlotChanged(keyValuePair.Value);
				}
				else
				{
					QueueCharacterApply(steamId2, keyValuePair.Value);
				}
			}
		}

		private void QueueLocalCharacterIfSlotChanged(CharacterData data)
		{
			if (_mainMenu == null || data == null)
			{
				return;
			}
			GameObject localCharModel = _mainMenu.GetLocalCharModel();
			if (!(localCharModel == null) && !(localCharModel == _lastLocalPuppetRoot))
			{
				_lastLocalPuppetRoot = localCharModel;
				if (MainMenuManager.TryGetLocalSteamId(out var steamId))
				{
					QueueCharacterApply(steamId, data);
				}
			}
		}

		private async UniTaskVoid RemapRemoteCharactersAsync()
		{
			await UniTask.Yield();
			if (!MainMenuManager.TryGetLocalSteamId(out var steamId))
			{
				return;
			}
			ulong value = steamId.Value;
			KeyValuePair<ulong, CharacterData>[] array = _characterDataBySteamId.ToArray();
			for (int i = 0; i < array.Length; i++)
			{
				KeyValuePair<ulong, CharacterData> keyValuePair = array[i];
				if (keyValuePair.Key != value)
				{
					SteamId steamId2 = keyValuePair.Key;
					QueueCharacterApply(steamId2, keyValuePair.Value);
				}
			}
		}

		private bool IsActiveLobby(Lobby lobby)
		{
			if ((ulong)lobby.Id != 0L)
			{
				return lobby.Id.Value == _activeLobbyId;
			}
			return false;
		}

		private void CleanupExpiredTransfers()
		{
			float now = Time.realtimeSinceStartup;
			string[] array = (from pair in _pendingTransfers
				where now - pair.Value.LastReceiveTime > 20f
				select pair.Key).ToArray();
			foreach (string key in array)
			{
				_pendingTransfers.Remove(key);
			}
		}

		private static CharacterData CreateNetworkData(CharacterData source)
		{
			CharacterData characterData = JsonUtility.FromJson<CharacterData>(JsonUtility.ToJson(source));
			if (characterData?.outfitDatas == null)
			{
				return characterData;
			}
			for (int i = 0; i < characterData.outfitDatas.Count; i++)
			{
				OutfitData value = characterData.outfitDatas[i];
				if (value.decalDatas != null)
				{
					for (int j = 0; j < value.decalDatas.Count; j++)
					{
						DecalData value2 = value.decalDatas[j];
						value2.decalTexture = null;
						value.decalDatas[j] = value2;
					}
				}
				characterData.outfitDatas[i] = value;
			}
			return characterData;
		}

		private static void RestoreNetworkResourceReferences(CharacterData data)
		{
			if (data?.outfitDatas == null)
			{
				return;
			}
			for (int i = 0; i < data.outfitDatas.Count; i++)
			{
				OutfitData value = data.outfitDatas[i];
				if (value.decalDatas == null)
				{
					continue;
				}
				for (int j = 0; j < value.decalDatas.Count; j++)
				{
					DecalData value2 = value.decalDatas[j];
					if (!string.IsNullOrWhiteSpace(value2.decal))
					{
						value2.decalTexture = Resources.Load<Texture>("Decal/" + value2.decal);
					}
					value.decalDatas[j] = value2;
				}
				data.outfitDatas[i] = value;
			}
		}

		private static string CompressToBase64(string value)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(value);
			using MemoryStream memoryStream = new MemoryStream();
			using (GZipStream gZipStream = new GZipStream(memoryStream, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
			{
				gZipStream.Write(bytes, 0, bytes.Length);
			}
			return Convert.ToBase64String(memoryStream.ToArray());
		}

		private static string DecompressFromBase64(string value)
		{
			using MemoryStream stream = new MemoryStream(Convert.FromBase64String(value));
			using GZipStream gZipStream = new GZipStream(stream, CompressionMode.Decompress);
			using MemoryStream memoryStream = new MemoryStream();
			byte[] array = new byte[8192];
			int num;
			while ((num = gZipStream.Read(array, 0, array.Length)) > 0)
			{
				if (memoryStream.Length + num > 1048576)
				{
					throw new InvalidDataException("Decompressed character payload exceeded the allowed size.");
				}
				memoryStream.Write(array, 0, num);
			}
			return Encoding.UTF8.GetString(memoryStream.ToArray());
		}
	}
}
