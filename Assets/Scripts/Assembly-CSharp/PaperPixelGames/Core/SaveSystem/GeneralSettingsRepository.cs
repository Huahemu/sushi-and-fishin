using System;
using PaperPixelGames.Framework.Utility;
using UnityEngine;

namespace PaperPixelGames.Core.SaveSystem
{
	internal sealed class GeneralSettingsRepository
	{
		private const string UserSettingsKey = "UserSettings";

		private readonly SaveStorage _storage;

		private readonly string _path;

		public GeneralSettingsRepository(SaveStorage storage, string path)
		{
			_storage = storage;
			_path = path;
		}

		public UserSettingsData LoadOrCreate(UserSettingsData fallback)
		{
			UserSettingsData userSettingsData = fallback ?? new UserSettingsData();
			try
			{
				SaveStorage.LoadResult loadResult = _storage.TryLoadWithRecovery<UserSettingsData>("UserSettings", _path, allowMissingKey: true, out var data, out var source);
				switch (loadResult)
				{
				case SaveStorage.LoadResult.Loaded:
				case SaveStorage.LoadResult.Recovered:
					userSettingsData = data ?? new UserSettingsData();
					break;
				case SaveStorage.LoadResult.MissingFile:
					Save(userSettingsData);
					break;
				case SaveStorage.LoadResult.MissingKey:
					UtilityMethods.Log("[SaveManager] General settings key was missing in a readable legacy file. Defaults will be used until the next settings save. Source=" + source, LogType.Warning);
					break;
				default:
					UtilityMethods.Log($"[SaveManager] General settings could not be loaded. Result={loadResult}, Source={source}", LogType.Error);
					break;
				}
			}
			catch (Exception ex)
			{
				_storage.ProtectWrites(_path);
				UtilityMethods.Log("[SaveManager] LoadGeneralData error: " + ex.Message, LogType.Error);
				userSettingsData = new UserSettingsData();
			}
			return userSettingsData;
		}

		public bool Save(UserSettingsData data)
		{
			if (data == null)
			{
				data = new UserSettingsData();
			}
			return _storage.RunSafely(_path, () =>
			{
				_storage.Save("UserSettings", data, _path);
			});
		}

		public void Clear()
		{
			if (_storage.FileExists(_path) && _storage.KeyExists("UserSettings", _path))
			{
				_storage.DeleteKey("UserSettings", _path);
			}
		}
	}
}
