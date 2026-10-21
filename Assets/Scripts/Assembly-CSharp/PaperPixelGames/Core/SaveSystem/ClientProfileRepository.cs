using System;
using PaperPixelGames.Framework.Utility;
using UnityEngine;

namespace PaperPixelGames.Core.SaveSystem
{
	internal sealed class ClientProfileRepository
	{
		private readonly SaveStorage _storage;

		private readonly string _path;

		public ClientProfileRepository(SaveStorage storage, string path)
		{
			_storage = storage;
			_path = path;
		}

		public ClientData Load(string hostSteamId, int slotId, out string applySource)
		{
			applySource = "SaveManager.LoadClientProfile";
			string clientDataKey = GetClientDataKey(hostSteamId, slotId);
			ClientData result = new ClientData
			{
				HostSteamID = hostSteamId,
				SlotID = slotId
			};
			try
			{
				SaveStorage.LoadResult loadResult = _storage.TryLoadWithRecovery<ClientData>(clientDataKey, _path, allowMissingKey: true, out var data, out var source);
				switch (loadResult)
				{
				case SaveStorage.LoadResult.MissingFile:
					UtilityMethods.Log("[SaveManager] No ClientSave file found. Created empty client profile.\n   HostSteamID: " + hostSteamId + $"\n   SlotID: {slotId}");
					applySource = "SaveManager.LoadClientProfile.NoFile";
					return result;
				case SaveStorage.LoadResult.MissingKey:
					UtilityMethods.Log("[SaveManager] No ClientData key found. Created empty client profile.\n   Key: " + clientDataKey + "\n   HostSteamID: " + hostSteamId + $"\n   SlotID: {slotId}");
					applySource = "SaveManager.LoadClientProfile.NoKey";
					return result;
				case SaveStorage.LoadResult.Unrecoverable:
					applySource = "SaveManager.LoadClientProfile.Unrecoverable";
					return result;
				default:
					result = data ?? new ClientData();
					result.HostSteamID = hostSteamId;
					result.SlotID = slotId;
					ClientDataMigrations.Cleanup(result);
					if (loadResult == SaveStorage.LoadResult.Recovered)
					{
						applySource = "SaveManager.LoadClientProfile.Recovered:" + source;
					}
					UtilityMethods.Log("[SaveManager] ClientData loaded.\n   Key: " + clientDataKey + "\n   HostSteamID: " + hostSteamId + $"\n   SlotID: {slotId}");
					break;
				}
			}
			catch (Exception ex)
			{
				_storage.ProtectWrites(_path);
				UtilityMethods.Log("[SaveManager] LoadClientProfile fallback. Created empty client profile.\n   Key: " + clientDataKey + "\n   Exception: " + ex.Message, LogType.Warning);
				result = new ClientData
				{
					HostSteamID = hostSteamId,
					SlotID = slotId
				};
			}
			return result;
		}

		public bool Save(string hostSteamId, int slotId, ClientData data)
		{
			string key = GetClientDataKey(hostSteamId, slotId);
			if (data == null)
			{
				data = new ClientData();
			}
			data.HostSteamID = hostSteamId;
			data.SlotID = slotId;
			ClientDataMigrations.Cleanup(data);
			return _storage.RunSafely(_path, () =>
			{
				_storage.Save(key, data, _path);
			});
		}

		public void Clear(string hostSteamId, int slotId)
		{
			string clientDataKey = GetClientDataKey(hostSteamId, slotId);
			if (!_storage.FileExists(_path) || !_storage.KeyExists(clientDataKey, _path))
			{
				return;
			}
			try
			{
				_storage.DeleteKey(clientDataKey, _path);
				UtilityMethods.Log($"[SaveManager] ClientData cleared for host {hostSteamId} slot {slotId}.");
			}
			catch (Exception ex)
			{
				UtilityMethods.Log("[SaveManager] ClearClientProfile error: " + ex.Message, LogType.Error);
			}
		}

		public void ClearProfilesForSlot(int slotId)
		{
			if (slotId < 0 || !_storage.FileExists(_path))
			{
				return;
			}
			string value = "_" + slotId;
			string[] keys;
			try
			{
				keys = _storage.GetKeys(_path);
			}
			catch (Exception ex)
			{
				UtilityMethods.Log("[SaveManager] ClearProfilesForSlot failed reading keys: " + ex.Message, LogType.Error);
				return;
			}
			if (keys == null || keys.Length == 0)
			{
				return;
			}
			string[] array = keys;
			foreach (string text in array)
			{
				if (!string.IsNullOrWhiteSpace(text) && text.StartsWith("ClientData_", StringComparison.Ordinal) && text.EndsWith(value, StringComparison.Ordinal))
				{
					try
					{
						_storage.DeleteKey(text, _path);
						UtilityMethods.Log("[SaveManager] Client profile key cleared: " + text);
					}
					catch (Exception ex2)
					{
						UtilityMethods.Log("[SaveManager] ClearProfilesForSlot failed for key " + text + ": " + ex2.Message, LogType.Error);
					}
				}
			}
		}

		public void ClearAll()
		{
			if (_storage.FileExists(_path))
			{
				_storage.DeleteFile(_path);
				UtilityMethods.Log("[SaveManager] All client profiles cleared.");
			}
		}

		public static string GetClientDataKey(string hostSteamId, int slotId)
		{
			return $"ClientData_{hostSteamId}_{slotId}";
		}
	}
}
