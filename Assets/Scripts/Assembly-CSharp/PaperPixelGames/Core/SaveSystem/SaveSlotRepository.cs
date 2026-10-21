using System;
using System.Collections.Generic;
using PaperPixelGames.Framework.Utility;
using UnityEngine;

namespace PaperPixelGames.Core.SaveSystem
{
	internal sealed class SaveSlotRepository
	{
		private const string SaveSlotDataKey = "SaveSlotData";

		private readonly SaveStorage _storage;

		private readonly List<SaveSlotData> _slots;

		private readonly string _slotPrefix;

		private readonly HashSet<int> _corruptedSlots = new HashSet<int>();

		public SaveSlotRepository(SaveStorage storage, List<SaveSlotData> slots, string slotPrefix)
		{
			_storage = storage;
			_slots = slots;
			_slotPrefix = slotPrefix;
		}

		public void LoadAll()
		{
			if (_slots != null)
			{
				for (int i = 0; i < _slots.Count; i++)
				{
					LoadSlot(i);
				}
			}
		}

		public void LoadSlot(int slotIndex)
		{
			if (!IsValidIndex(slotIndex))
			{
				return;
			}
			string slotPath = GetSlotPath(slotIndex);
			try
			{
				SaveStorage.LoadResult loadResult = _storage.TryLoadWithRecovery<SaveSlotData>("SaveSlotData", slotPath, allowMissingKey: false, out var data, out var source);
				switch (loadResult)
				{
				case SaveStorage.LoadResult.MissingFile:
				{
					List<SaveSlotData> slots = _slots;
					if (slots[slotIndex] == null)
					{
						SaveSlotData saveSlotData = (slots[slotIndex] = SaveSlotData.CreateNew());
					}
					_slots[slotIndex].Version = 4;
					_slots[slotIndex].BossGlitchResetApplied = true;
					_corruptedSlots.Remove(slotIndex);
					break;
				}
				default:
					if (data != null)
					{
						if (!data.BossGlitchResetApplied)
						{
							string text = _storage.CreateMigrationBackup("SaveSlotData", data, slotPath, "before-boss-progress-reset-v3");
							try
							{
								SaveMigrations.MigrateToCurrent(data);
								data.SlotID = slotIndex;
								_storage.Save("SaveSlotData", data, slotPath);
							}
							catch
							{
								_storage.RestoreMigrationBackup(slotPath, text);
								throw;
							}
							UtilityMethods.Log($"[SaveManager] Boss progression migration completed for slot {slotIndex}. Backup={text}");
						}
						else
						{
							SaveMigrations.MigrateToCurrent(data);
							data.SlotID = slotIndex;
						}
						_slots[slotIndex] = data;
						_corruptedSlots.Remove(slotIndex);
						UtilityMethods.Log($"[SaveManager] Loaded & migrated SaveSlot_{slotIndex} (Version {data.Version}). " + $"LoadResult={loadResult}, Source={source}");
						break;
					}
					goto case SaveStorage.LoadResult.MissingKey;
				case SaveStorage.LoadResult.MissingKey:
				case SaveStorage.LoadResult.Unrecoverable:
					_corruptedSlots.Add(slotIndex);
					UtilityMethods.Log($"[SaveManager] Slot {slotIndex} could not be loaded. Result={loadResult}. Writes to the damaged file are blocked.", LogType.Error);
					break;
				}
			}
			catch (Exception ex)
			{
				_storage.ProtectWrites(slotPath);
				_corruptedSlots.Add(slotIndex);
				UtilityMethods.Log($"[SaveManager] Error loading slot {slotIndex}: {ex.Message}", LogType.Error);
			}
		}

		public bool TryEnsureSlot(int slotIndex, out SaveSlotData data)
		{
			data = null;
			if (!IsValidIndex(slotIndex))
			{
				return false;
			}
			List<SaveSlotData> slots = _slots;
			if (slots[slotIndex] == null)
			{
				SaveSlotData saveSlotData = (slots[slotIndex] = SaveSlotData.CreateNew());
			}
			data = _slots[slotIndex];
			return true;
		}

		public bool TryGetSlot(int slotIndex, out SaveSlotData data)
		{
			data = null;
			if (!IsValidIndex(slotIndex))
			{
				return false;
			}
			data = _slots[slotIndex];
			return data != null;
		}

		public bool IsSlotCorrupted(int slotIndex)
		{
			return _corruptedSlots.Contains(slotIndex);
		}

		public bool SaveSlot(int slotIndex, SaveSlotData data)
		{
			if (!IsValidIndex(slotIndex) || data == null || IsSlotCorrupted(slotIndex))
			{
				return false;
			}
			string path = GetSlotPath(slotIndex);
			return _storage.RunSafely(path, () =>
			{
				_storage.Save("SaveSlotData", data, path);
			});
		}

		public void Clear(int slotIndex)
		{
			if (!IsValidIndex(slotIndex))
			{
				UtilityMethods.Log($"[SaveManager] Clear failed. Invalid slotIndex={slotIndex}", LogType.Error);
				return;
			}
			string slotPath = GetSlotPath(slotIndex);
			if (_storage.FileExists(slotPath))
			{
				_storage.DeleteFile(slotPath);
			}
			_slots[slotIndex] = SaveSlotData.CreateNew();
			_corruptedSlots.Remove(slotIndex);
			UtilityMethods.Log($"[SaveManager] Cleared SaveSlot_{slotIndex}");
		}

		private string GetSlotPath(int slotIndex)
		{
			return $"{_slotPrefix}{slotIndex}.es3";
		}

		private bool IsValidIndex(int slotIndex)
		{
			if (_slots != null && slotIndex >= 0)
			{
				return slotIndex < _slots.Count;
			}
			return false;
		}

		public bool ApplyTemporaryStaffStackMigration(int index, TemporaryStaffStackMigration.Layout layout, bool enabled, out TemporaryStaffStackMigration.Result result, out string backup)
		{
			result = null;
			backup = null;
			if (!TryGetSlot(index, out var data) || IsSlotCorrupted(index) || !TemporaryStaffStackMigration.ShouldRun(data, enabled))
			{
				return false;
			}
			result = TemporaryStaffStackMigration.Plan(data.RestaurantData, layout);
			string slotPath = GetSlotPath(index);
			backup = _storage.CreateStaffMigrationBackup("SaveSlotData", data, slotPath);
			CommitTemporaryStaffStackMigration(data, result.Data, slotPath, backup);
			return true;
		}

		private void CommitTemporaryStaffStackMigration(SaveSlotData slot, RestaurantData migrated, string path, string backup)
		{
			RestaurantData restaurantData = slot.RestaurantData;
			int temporaryStaffStackRevision = slot.TemporaryStaffStackRevision;
			try
			{
				slot.RestaurantData = migrated;
				slot.TemporaryStaffStackRevision = 1;
				_storage.Save("SaveSlotData", slot, path);
			}
			catch
			{
				slot.RestaurantData = restaurantData;
				slot.TemporaryStaffStackRevision = temporaryStaffStackRevision;
				_storage.RestoreStaffMigrationBackup(path, backup);
				throw;
			}
		}
	}
}
