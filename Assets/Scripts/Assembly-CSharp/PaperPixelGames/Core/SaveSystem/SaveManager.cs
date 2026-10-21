using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Staff;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using Unity.Profiling;
using UnityEngine;

namespace PaperPixelGames.Core.SaveSystem
{
	public class SaveManager : Singleton<SaveManager>
	{
		private static readonly ProfilerMarker AutoSaveCaptureMarker = new ProfilerMarker("Save.Auto.Capture");

		private static readonly ProfilerMarker AutoSaveStepMarker = new ProfilerMarker("Save.Auto.Step");

		private bool _autoSaveActive;

		private int _autoSaveCaptureFrame;

		private SaveResult _autoSaveCaptureResult;

		[SerializeField]
		private List<SaveSlotData> _saveSlotsData = new List<SaveSlotData>();

		[SerializeField]
		private string _saveSlotName = "SaveSlot_";

		[SerializeField]
		private string _generalSaveName = "GeneralSave";

		[SerializeField]
		private string _clientProfileFileName = "ClientSave";

		[SerializeField]
		private int _currentSaveSlotIndex = -1;

		[SerializeField]
		private string _currentSaveSlotHostSteamID = "";

		[Header("Steam Cloud Debug")]
		[SerializeField]
		private bool _logSteamCloudAutoCloudReportOnInitialize = true;

		private ES3Settings _es3Settings;

		private SaveStorage _storage;

		private GeneralSettingsRepository _generalSettingsRepository;

		private ClientProfileRepository _clientProfileRepository;

		private SaveSlotRepository _saveSlotRepository;

		private WorldSaveCollector _worldSaveCollector;

		private bool _saveFlag;

		public SaveResult LastAutoSaveResult { get; private set; } = SaveResult.Skipped;

		public ClientData CurrentClientData { get; private set; }

		public UserSettingsData UserSettings { get; private set; } = new UserSettingsData();

		public int CurrentSaveSlotIndex => _currentSaveSlotIndex;

		public string CurrentSaveSlotHostSteamID => _currentSaveSlotHostSteamID;

		public List<SaveSlotData> SaveSlotsData => _saveSlotsData;

		public bool HasValidCurrentSlot
		{
			get
			{
				if (_saveSlotsData != null && _currentSaveSlotIndex >= 0 && _currentSaveSlotIndex < _saveSlotsData.Count)
				{
					return _saveSlotsData[_currentSaveSlotIndex] != null;
				}
				return false;
			}
		}

		public SaveSlotData CurrentSaveSlotData
		{
			get
			{
				if (!TryGetCurrentSaveSlot(out var data))
				{
					UtilityMethods.Log($"[SaveManager] CurrentSaveSlotData requested but current slot is invalid. index={_currentSaveSlotIndex}", LogType.Error);
					return null;
				}
				return data;
			}
		}

		public bool SaveFlag
		{
			get
			{
				if (!_saveFlag)
				{
					return _autoSaveActive;
				}
				return true;
			}
		}

		public void RequestHourlySave(bool host)
		{
			if (SaveFlag)
			{
				return;
			}
			EnsureRepositories();
			_storage.BeginDeferredWrites();
			_autoSaveCaptureResult = SaveResult.Failed;
			try
			{
				using (AutoSaveCaptureMarker.Auto())
				{
					_autoSaveCaptureResult = (host ? SaveHostSession() : SaveCurrentClientProfile());
				}
			}
			finally
			{
				_storage.EndDeferredWrites();
				_autoSaveCaptureFrame = Time.frameCount;
				_autoSaveActive = true;
			}
		}

		private void Update()
		{
			if (_autoSaveActive && Time.frameCount > _autoSaveCaptureFrame)
			{
				using (AutoSaveStepMarker.Auto())
				{
					_storage.ProcessDeferredStep();
				}
				if (!_storage.HasDeferredWrites)
				{
					CompleteAutoSave();
				}
			}
		}

		private SaveResult CompleteAutoSave()
		{
			LastAutoSaveResult = ((!_storage.DeferredWritesSucceeded) ? SaveResult.Failed : _autoSaveCaptureResult);
			_autoSaveActive = false;
			UtilityMethods.Log($"[SaveManager] Hourly save completed. Result={LastAutoSaveResult}", (LastAutoSaveResult != SaveResult.Failed) ? LogType.Log : LogType.Error);
			return LastAutoSaveResult;
		}

		public SaveResult FlushPendingAutoSave()
		{
			if (!_autoSaveActive || _storage == null || _storage.IsCapturingDeferredWrites)
			{
				return SaveResult.Skipped;
			}
			_storage.FlushDeferredWrites();
			return CompleteAutoSave();
		}

		private void OnDisable()
		{
			FlushPendingAutoSave();
		}

		private void OnApplicationQuit()
		{
			FlushPendingAutoSave();
		}

		public void Initialize()
		{
			EnsureRepositories();
			LoadGeneralData();
			_saveSlotRepository.LoadAll();
			if (_logSteamCloudAutoCloudReportOnInitialize)
			{
				LogSteamCloudAutoCloudReport();
			}
		}

		private void EnsureRepositories()
		{
			if (_es3Settings == null)
			{
				_es3Settings = new ES3Settings(ES3.Location.Cache);
			}
			if (_storage == null)
			{
				_storage = new SaveStorage(_es3Settings);
			}
			if (_generalSettingsRepository == null)
			{
				_generalSettingsRepository = new GeneralSettingsRepository(_storage, _generalSaveName + ".es3");
			}
			if (_clientProfileRepository == null)
			{
				_clientProfileRepository = new ClientProfileRepository(_storage, GetClientProfilePath());
			}
			if (_saveSlotRepository == null)
			{
				_saveSlotRepository = new SaveSlotRepository(_storage, _saveSlotsData, _saveSlotName);
			}
			if (_worldSaveCollector == null)
			{
				_worldSaveCollector = new WorldSaveCollector();
			}
		}

		private void LoadGeneralData()
		{
			EnsureRepositories();
			UserSettings = _generalSettingsRepository.LoadOrCreate(UserSettings);
		}

		public bool SaveGeneralSettings()
		{
			EnsureRepositories();
			return _generalSettingsRepository.Save(UserSettings);
		}

		private string GetClientProfilePath()
		{
			return _clientProfileFileName + ".es3";
		}

		public void LogSteamCloudAutoCloudReport()
		{
			EnsureRepositories();
			List<string> steamCloudAutoCloudRelativePaths = GetSteamCloudAutoCloudRelativePaths();
			string text = ResolveSteamCloudAutoCloudRootDirectory(steamCloudAutoCloudRelativePaths);
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("<b><color=#66CCFF>[SteamCloud]</color></b> Auto-Cloud save file report");
			stringBuilder.AppendLine($"   ES3Location: {_es3Settings.location}");
			stringBuilder.AppendLine("   AutoCloudRootCandidate: " + text);
			stringBuilder.AppendLine("   Steamworks File Patterns:");
			stringBuilder.AppendLine("      " + _generalSaveName + ".es3");
			stringBuilder.AppendLine("      " + GetClientProfilePath());
			stringBuilder.AppendLine("      " + _saveSlotName + "*.es3");
			stringBuilder.AppendLine("   Existing Files:");
			for (int i = 0; i < steamCloudAutoCloudRelativePaths.Count; i++)
			{
				string text2 = steamCloudAutoCloudRelativePaths[i];
				string text3 = ResolvePersistentSaveFullPath(text2);
				bool flag = _storage.FileExists(text2);
				bool flag2 = File.Exists(text3);
				long num = (flag2 ? new FileInfo(text3).Length : 0);
				stringBuilder.AppendLine($"      {text2} | ES3Exists={flag} | DiskExists={flag2} | Bytes={num} | FullPath={text3}");
			}
			UtilityMethods.Log(stringBuilder.ToString());
		}

		private List<string> GetSteamCloudAutoCloudRelativePaths()
		{
			List<string> list = new List<string>
			{
				_generalSaveName + ".es3",
				GetClientProfilePath()
			};
			int num = ((_saveSlotsData != null) ? _saveSlotsData.Count : 0);
			for (int i = 0; i < num; i++)
			{
				list.Add($"{_saveSlotName}{i}.es3");
			}
			return list;
		}

		private string ResolveSteamCloudAutoCloudRootDirectory(List<string> relativePaths)
		{
			if (relativePaths == null || relativePaths.Count == 0)
			{
				return string.Empty;
			}
			return Path.GetDirectoryName(ResolvePersistentSaveFullPath(relativePaths[0])) ?? string.Empty;
		}

		private string ResolvePersistentSaveFullPath(string relativePath)
		{
			return CreatePersistentSaveSettings(relativePath).FullPath;
		}

		private ES3Settings CreatePersistentSaveSettings(string relativePath)
		{
			ES3Settings eS3Settings = new ES3Settings(relativePath, _es3Settings);
			if (eS3Settings.location == ES3.Location.Cache)
			{
				eS3Settings = (ES3Settings)eS3Settings.Clone();
				eS3Settings.location = ((ES3Settings.defaultSettings.location != ES3.Location.Cache) ? ES3Settings.defaultSettings.location : ES3.Location.File);
			}
			return eS3Settings;
		}

		public void LoadClientProfile(string hostSteamId, int slotId)
		{
			TryLoadClientProfile(hostSteamId, slotId);
		}

		public bool TryLoadClientProfile(string hostSteamId, int slotId)
		{
			if (string.IsNullOrWhiteSpace(hostSteamId) || slotId < 0)
			{
				UtilityMethods.Log($"[SaveManager] LoadClientProfile failed. Invalid context. hostSteamId={hostSteamId}, slotId={slotId}", LogType.Error);
				CurrentClientData = null;
				return false;
			}
			EnsureRepositories();
			CurrentClientData = _clientProfileRepository.Load(hostSteamId, slotId, out var applySource);
			return TryApplyCurrentClientProfileToLocalPlayer(applySource);
		}

		public bool TryApplyCurrentClientProfileToLocalPlayer(string source)
		{
			if (CurrentClientData == null)
			{
				return false;
			}
			if (Singleton<PlayersManager>.Instance == null || Singleton<PlayersManager>.Instance.LocalPlayer == null)
			{
				UtilityMethods.Log("[SaveManager] Client profile apply pending. Source=" + source);
				return false;
			}
			Singleton<PlayersManager>.Instance.LocalPlayer.LoadClientData(CurrentClientData);
			UtilityMethods.Log("[SaveManager] Client profile applied.\n   Source: " + source + "\n   HostSteamID: " + CurrentClientData.HostSteamID + $"\n   SlotID: {CurrentClientData.SlotID}");
			return true;
		}

		public SaveResult SaveClientProfile(string hostSteamId, int slotId)
		{
			FlushPendingAutoSave();
			if (string.IsNullOrWhiteSpace(hostSteamId) || slotId < 0)
			{
				UtilityMethods.Log($"[SaveManager] SaveClientProfile failed. Invalid context. hostSteamId={hostSteamId}, slotId={slotId}", LogType.Error);
				return SaveResult.Failed;
			}
			if (Singleton<PlayersManager>.Instance == null || Singleton<PlayersManager>.Instance.LocalPlayer == null)
			{
				return SaveResult.Failed;
			}
			try
			{
				if (CurrentClientData == null)
				{
					ClientData obj = new ClientData
					{
						HostSteamID = hostSteamId,
						SlotID = slotId
					};
					ClientData clientData = obj;
					CurrentClientData = obj;
				}
				CurrentClientData.HostSteamID = hostSteamId;
				CurrentClientData.SlotID = slotId;
				CurrentClientData = Singleton<PlayersManager>.Instance.LocalPlayer.SaveClientData();
				if (CurrentClientData == null)
				{
					UtilityMethods.Log("[SaveManager] SaveClientProfile failed. LocalPlayer.SaveClientData returned null.", LogType.Error);
					return SaveResult.Failed;
				}
				CurrentClientData.HostSteamID = hostSteamId;
				CurrentClientData.SlotID = slotId;
				EnsureRepositories();
				return (!_clientProfileRepository.Save(hostSteamId, slotId, CurrentClientData)) ? SaveResult.Failed : SaveResult.Success;
			}
			catch (Exception arg)
			{
				UtilityMethods.Log($"[SaveManager] SaveClientProfile failed. Exception={arg}", LogType.Error);
				return SaveResult.Failed;
			}
		}

		public void ClearClientProfile(string hostSteamId, int slotId)
		{
			EnsureRepositories();
			_clientProfileRepository.Clear(hostSteamId, slotId);
		}

		public void ClearAllClientProfiles()
		{
			EnsureRepositories();
			_clientProfileRepository.ClearAll();
			CurrentClientData = null;
		}

		public void SetCurrentSaveSlot(int slotIndex)
		{
			FlushPendingAutoSave();
			EnsureRepositories();
			if (!_saveSlotRepository.TryEnsureSlot(slotIndex, out var data))
			{
				UtilityMethods.Log($"[SaveManager] SetCurrentSaveSlot failed. Invalid slotIndex={slotIndex}", LogType.Error);
				_currentSaveSlotIndex = -1;
				_currentSaveSlotHostSteamID = string.Empty;
			}
			else
			{
				_currentSaveSlotIndex = slotIndex;
				data.SlotID = slotIndex;
				if (Netcode.Transports.Local.OfflineSingleplayerMode.Enabled)
				{
					Netcode.Transports.Local.OfflineSingleplayerMode.SelectSaveProfile(data.HostSteamID);
					if (string.IsNullOrWhiteSpace(data.HostSteamID) || data.HostSteamID == "0") data.HostSteamID = Netcode.Transports.Local.OfflineSingleplayerMode.LocalUserId.ToString();
				}
				_currentSaveSlotHostSteamID = data.HostSteamID ?? string.Empty;
				UtilityMethods.Log("[SaveManager] Current save slot set." + $"\n   SlotIndex: {_currentSaveSlotIndex}" + "\n   HostSteamID: " + _currentSaveSlotHostSteamID);
			}
		}

		public bool TryGetCurrentSaveSlot(out SaveSlotData data)
		{
			data = null;
			if (!HasValidCurrentSlot)
			{
				return false;
			}
			data = _saveSlotsData[_currentSaveSlotIndex];
			return true;
		}

		public bool TryGetSaveSlot(int slotIndex, out SaveSlotData data)
		{
			EnsureRepositories();
			return _saveSlotRepository.TryGetSlot(slotIndex, out data);
		}

		public bool IsSaveSlotCorrupted(int slotIndex)
		{
			EnsureRepositories();
			return _saveSlotRepository.IsSlotCorrupted(slotIndex);
		}

		public SaveResult SaveGame()
		{
			FlushPendingAutoSave();
			if (_saveFlag)
			{
				return SaveResult.Skipped;
			}
			if (Singleton<GameProgressManager>.Instance == null)
			{
				return SaveResult.Failed;
			}
			if (!TryGetCurrentSaveSlot(out var _))
			{
				UtilityMethods.Log($"[SaveManager] SaveGame failed. Invalid current slot. index={_currentSaveSlotIndex}", LogType.Error);
				return SaveResult.Failed;
			}
			_saveFlag = true;
			try
			{
				SaveStorage storage = _storage;
				if (storage == null || !storage.IsCapturingDeferredWrites)
				{
					UtilityMethods.Log("[SaveManager] SaveGame started.");
				}
				bool num = SaveGeneralSettings();
				bool flag = SaveCurrentSlot();
				if (!num || !flag)
				{
					return SaveResult.Failed;
				}
				if (!_storage.IsCapturingDeferredWrites)
				{
					UtilityMethods.Log("[SaveManager] SaveGame finished.");
				}
				return SaveResult.Success;
			}
			catch (Exception arg)
			{
				UtilityMethods.Log($"[SaveManager] SaveGame failed. Exception={arg}", LogType.Error);
				return SaveResult.Failed;
			}
			finally
			{
				_saveFlag = false;
			}
		}

		private bool SaveCurrentSlot()
		{
			EnsureRepositories();
			if (!TryGetCurrentSaveSlot(out var data))
			{
				UtilityMethods.Log("[SaveManager] SaveCurrentSlot failed. Current slot invalid.", LogType.Error);
				return false;
			}
			if (!_worldSaveCollector.TryPopulateSlot(data, _currentSaveSlotIndex, _currentSaveSlotHostSteamID, out var error))
			{
				UtilityMethods.Log("[SaveManager] SaveCurrentSlot failed. " + error, LogType.Error);
				return false;
			}
			bool num = _saveSlotRepository.SaveSlot(_currentSaveSlotIndex, data);
			if (num && !_storage.IsCapturingDeferredWrites)
			{
				UtilityMethods.Log($"[SaveManager] Saved slot {_currentSaveSlotIndex} (Version {data.Version}).");
			}
			return num;
		}

		public void SaveHostSteamId(string id)
		{
			_currentSaveSlotHostSteamID = id ?? string.Empty;
			if (TryGetCurrentSaveSlot(out var data))
			{
				data.HostSteamID = _currentSaveSlotHostSteamID;
				data.SlotID = _currentSaveSlotIndex;
			}
			UtilityMethods.Log("[SaveManager] HostSteamID cached." + $"\n   SlotIndex: {_currentSaveSlotIndex}" + "\n   HostSteamID: " + _currentSaveSlotHostSteamID);
		}

		public void ClearAllGameData()
		{
			EnsureRepositories();
			for (int i = 0; i < _saveSlotsData.Count; i++)
			{
				Clear(i);
			}
			_generalSettingsRepository.Clear();
		}

		public void Clear(int slotIndex)
		{
			EnsureRepositories();
			string text = string.Empty;
			int num = slotIndex;
			HashSet<string> hashSet = new HashSet<string>();
			if (_saveSlotRepository.TryGetSlot(slotIndex, out var data) && data != null)
			{
				text = data.HostSteamID;
				num = ((data.SlotID >= 0) ? data.SlotID : slotIndex);
			}
			if (!string.IsNullOrWhiteSpace(text))
			{
				hashSet.Add(text);
			}
			if (!string.IsNullOrWhiteSpace(_currentSaveSlotHostSteamID))
			{
				hashSet.Add(_currentSaveSlotHostSteamID);
			}
			if (CurrentClientData != null && CurrentClientData.SlotID == num && !string.IsNullOrWhiteSpace(CurrentClientData.HostSteamID))
			{
				hashSet.Add(CurrentClientData.HostSteamID);
			}
			foreach (string item in hashSet)
			{
				_clientProfileRepository.Clear(item, num);
			}
			_clientProfileRepository.ClearProfilesForSlot(num);
			_saveSlotRepository.Clear(slotIndex);
			if (_currentSaveSlotIndex == slotIndex)
			{
				_currentSaveSlotIndex = -1;
				_currentSaveSlotHostSteamID = string.Empty;
				CurrentClientData = null;
			}
		}

		public ClientData GetClientData()
		{
			return CurrentClientData;
		}

		private static SaveResult MergeSaveResults(SaveResult primary, SaveResult secondary)
		{
			if (primary == SaveResult.Failed || secondary == SaveResult.Failed)
			{
				return SaveResult.Failed;
			}
			if (primary == SaveResult.Success || secondary == SaveResult.Success)
			{
				return SaveResult.Success;
			}
			return SaveResult.Skipped;
		}

		public bool TryResolveCurrentClientProfileContext(out string hostSteamId, out int slotId)
		{
			hostSteamId = string.Empty;
			slotId = -1;
			if (CurrentClientData != null && !string.IsNullOrWhiteSpace(CurrentClientData.HostSteamID) && CurrentClientData.SlotID >= 0)
			{
				hostSteamId = CurrentClientData.HostSteamID;
				slotId = CurrentClientData.SlotID;
				return true;
			}
			if (!TryGetCurrentSaveSlot(out var data))
			{
				return false;
			}
			hostSteamId = ((!string.IsNullOrWhiteSpace(_currentSaveSlotHostSteamID)) ? _currentSaveSlotHostSteamID : data.HostSteamID);
			slotId = ((data.SlotID >= 0) ? data.SlotID : _currentSaveSlotIndex);
			if (!string.IsNullOrWhiteSpace(hostSteamId))
			{
				return slotId >= 0;
			}
			return false;
		}

		public SaveResult SaveCurrentClientProfile()
		{
			if (!TryResolveCurrentClientProfileContext(out var hostSteamId, out var slotId))
			{
				UtilityMethods.Log("[SaveManager] SaveCurrentClientProfile failed. Context could not be resolved." + $"\n   CurrentSlotIndex: {_currentSaveSlotIndex}" + "\n   CachedHostSteamID: " + _currentSaveSlotHostSteamID, LogType.Error);
				return SaveResult.Failed;
			}
			return SaveClientProfile(hostSteamId, slotId);
		}

		public SaveResult SaveHostSession()
		{
			SaveResult saveResult = SaveGame();
			SaveResult saveResult2 = SaveCurrentClientProfile();
			SaveResult saveResult3 = MergeSaveResults(saveResult, saveResult2);
			SaveStorage storage = _storage;
			if (storage == null || !storage.IsCapturingDeferredWrites)
			{
				UtilityMethods.Log("[SaveManager] SaveHostSession completed." + $"\n   WorldResult: {saveResult}" + $"\n   ProfileResult: {saveResult2}" + $"\n   FinalResult: {saveResult3}");
			}
			return saveResult3;
		}

		public RestaurantData ApplyTemporaryStaffStackMigration(RestaurantData source)
		{
			if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer || !TryGetCurrentSaveSlot(out var data) || data.RestaurantData != source || !TemporaryStaffStackMigration.ShouldRun(data, enabled: true))
			{
				return source;
			}
			try
			{
				EnsureRepositories();
				if (FlushPendingAutoSave() == SaveResult.Failed)
				{
					throw new IOException("Pending autosave failed before staff migration.");
				}
				TemporaryStaffStackMigration.Layout layout = TemporaryStaffStackMigration.CaptureLayout(StaffWorkplaceRegistry.Instance, source);
				if (_saveSlotRepository.ApplyTemporaryStaffStackMigration(_currentSaveSlotIndex, layout, enabled: true, out var result, out var backup))
				{
					Debug.Log($"[StaffStackMigration] Slot {_currentSaveSlotIndex}: {result}. Backup={backup}");
				}
				return data.RestaurantData;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[StaffStackMigration] Migration was not committed. Loading the original stock. " + ex.Message);
				return source;
			}
		}
	}
}
