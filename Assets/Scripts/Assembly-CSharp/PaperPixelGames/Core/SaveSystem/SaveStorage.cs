using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PaperPixelGames.Framework;
using PaperPixelGames.Framework.Utility;
using Unity.Profiling;
using UnityEngine;

namespace PaperPixelGames.Core.SaveSystem
{
	internal sealed class SaveStorage
	{
		internal enum LoadResult
		{
			Loaded = 0,
			Recovered = 1,
			MissingFile = 2,
			MissingKey = 3,
			Unrecoverable = 4
		}

		private enum CandidateState
		{
			Missing = 0,
			Valid = 1,
			MissingKey = 2,
			Invalid = 3
		}

		private sealed class DeferredWrite
		{
			public string Path;

			public byte[] Snapshot;

			public int Stage;
		}

		private const int BackupCount = 3;

		private static readonly ProfilerMarker BackupMarker = new ProfilerMarker("Save.Backup");

		private static readonly ProfilerMarker SerializeMarker = new ProfilerMarker("Save.Serialize");

		private static readonly ProfilerMarker WriteMarker = new ProfilerMarker("Save.Write");

		private static readonly ProfilerMarker VerifyMarker = new ProfilerMarker("Save.Verify");

		private readonly ES3Settings _settings;

		private readonly HashSet<string> _writeProtectedPaths = new HashSet<string>(StringComparer.Ordinal);

		private readonly Queue<DeferredWrite> _deferredWrites = new Queue<DeferredWrite>();

		private static readonly ProfilerMarker SnapshotMarker = new ProfilerMarker("Save.Auto.SnapshotBytes");

		private static readonly ProfilerMarker VerifyBytesMarker = new ProfilerMarker("Save.VerifyBytes");

		private bool _processingDeferredWrite;

		private bool _deferredWriteFailed;

		public bool IsCapturingDeferredWrites { get; private set; }

		public bool HasDeferredWrites => _deferredWrites.Count > 0;

		public bool DeferredWritesSucceeded => !_deferredWriteFailed;

		public SaveStorage(ES3Settings settings)
		{
			_settings = settings;
		}

		public bool FileExists(string path)
		{
			if (!ES3.FileExists(path))
			{
				return ES3.FileExists(path, _settings);
			}
			return true;
		}

		public bool KeyExists(string key, string path)
		{
			if (!EnsureCachedFile(path))
			{
				return false;
			}
			return ES3.KeyExists(key, path, _settings);
		}

		public void CacheFile(string path)
		{
			FlushDeferredWrites();
			ES3.CacheFile(CreatePersistentSettings(path));
		}

		public void ProtectWrites(string path)
		{
			if (!string.IsNullOrWhiteSpace(path))
			{
				_writeProtectedPaths.Add(path);
			}
		}

		public LoadResult TryLoadWithRecovery<T>(string key, string path, bool allowMissingKey, out T data, out string source)
		{
			FlushDeferredWrites();
			data = default;
			source = string.Empty;
			CandidateState candidateState = TryReadPhysicalCandidate<T>(key, path, out var data2);
			if (candidateState == CandidateState.Valid)
			{
				try
				{
					CacheFile(path);
					data = data2;
					source = path;
					_writeProtectedPaths.Remove(path);
					return LoadResult.Loaded;
				}
				catch (Exception ex)
				{
					candidateState = CandidateState.Invalid;
					UtilityMethods.Log("[SaveStorage] Valid primary could not be cached. Recovery will be attempted. Path=" + path + ", Exception=" + ex.Message, LogType.Warning);
				}
			}
			if ((candidateState == CandidateState.MissingKey) & allowMissingKey)
			{
				try
				{
					CacheFile(path);
					_writeProtectedPaths.Remove(path);
					source = path;
					return LoadResult.MissingKey;
				}
				catch (Exception ex2)
				{
					candidateState = CandidateState.Invalid;
					UtilityMethods.Log("[SaveStorage] Primary with a missing key could not be cached. Recovery will be attempted. Path=" + path + ", Exception=" + ex2.Message, LogType.Warning);
				}
			}
			string[] recoveryCandidates = GetRecoveryCandidates(path);
			bool flag = false;
			foreach (string text in recoveryCandidates)
			{
				CandidateState candidateState2 = TryReadPhysicalCandidate<T>(key, text, out var data3);
				if (candidateState2 == CandidateState.Missing)
				{
					continue;
				}
				flag = true;
				if (candidateState2 == CandidateState.Valid)
				{
					try
					{
						RestoreCandidate(path, text);
						CacheFile(path);
						data = data3;
						source = text;
						_writeProtectedPaths.Remove(path);
						UtilityMethods.Log("[SaveStorage] Recovered save file. Primary=" + path + ", Source=" + text, LogType.Warning);
						return LoadResult.Recovered;
					}
					catch (Exception ex3)
					{
						_writeProtectedPaths.Add(path);
						UtilityMethods.Log("[SaveStorage] Recovery candidate could not be restored. Primary=" + path + ", Source=" + text + ", Exception=" + ex3.Message, LogType.Error);
					}
				}
			}
			if (candidateState == CandidateState.Missing && !flag)
			{
				_writeProtectedPaths.Remove(path);
				source = path;
				return LoadResult.MissingFile;
			}
			_writeProtectedPaths.Add(path);
			source = path;
			UtilityMethods.Log("[SaveStorage] Save file is unreadable and no valid recovery candidate exists. Writes are blocked. Path=" + path, LogType.Error);
			return LoadResult.Unrecoverable;
		}

		public T Load<T>(string key, string path)
		{
			FlushDeferredWrites();
			return ES3.Load<T>(key, path, _settings);
		}

		public void Save<T>(string key, T data, string path)
		{
			if (IsCapturingDeferredWrites)
			{
				CaptureDeferredWrite(key, data, path);
				return;
			}
			FlushDeferredWrites();
			ThrowIfWriteProtected(path);
			using (BackupMarker.Auto())
			{
				CreateRotatingBackup(path);
			}
			using (SerializeMarker.Auto())
			{
				ES3.Save(key, data, path, _settings);
			}
			using (WriteMarker.Auto())
			{
				ES3.StoreCachedFile(CreatePersistentSettings(path));
			}
			VerifyWrittenFile<T>(key, path);
		}

		public void DeleteFile(string path)
		{
			FlushDeferredWrites();
			if (ES3.FileExists(path, _settings))
			{
				ES3.DeleteFile(path, _settings);
			}
			ES3.DeleteFile(path);
			DeleteRecoveryArtifacts(path);
			_writeProtectedPaths.Remove(path);
		}

		public void DeleteKey(string key, string path)
		{
			FlushDeferredWrites();
			if (EnsureCachedFile(path) && ES3.KeyExists(key, path, _settings))
			{
				ThrowIfWriteProtected(path);
				CreateRotatingBackup(path);
				ES3.DeleteKey(key, path, _settings);
				ES3.StoreCachedFile(CreatePersistentSettings(path));
				if (!IsPhysicalFileStructurallyValid(path))
				{
					TryRestoreBackupAfterFailedWrite(path);
					throw new InvalidDataException("Save verification failed after deleting key '" + key + "' from '" + path + "'.");
				}
			}
		}

		public string[] GetKeys(string path)
		{
			if (!EnsureCachedFile(path))
			{
				return Array.Empty<string>();
			}
			return ES3.GetKeys(path, _settings);
		}

		private bool EnsureCachedFile(string path)
		{
			if (ES3.FileExists(path, _settings))
			{
				return true;
			}
			if (!ES3.FileExists(path))
			{
				return false;
			}
			ES3.CacheFile(CreatePersistentSettings(path));
			return ES3.FileExists(path, _settings);
		}

		private CandidateState TryReadPhysicalCandidate<T>(string key, string path, out T data)
		{
			data = default;
			ES3Settings settings = CreatePersistentSettings(path);
			if (!ES3.FileExists(settings))
			{
				return CandidateState.Missing;
			}
			try
			{
				if (Array.IndexOf(ES3.GetKeys(settings), key) < 0)
				{
					return CandidateState.MissingKey;
				}
				data = ES3.Load<T>(key, settings);
				return (data != null) ? CandidateState.Valid : CandidateState.Invalid;
			}
			catch (Exception ex)
			{
				UtilityMethods.Log("[SaveStorage] Candidate validation failed. Path=" + path + ", Exception=" + ex.Message, LogType.Warning);
				return CandidateState.Invalid;
			}
		}

		private bool IsPhysicalFileStructurallyValid(string path)
		{
			ES3Settings settings = CreatePersistentSettings(path);
			if (!ES3.FileExists(settings))
			{
				return false;
			}
			try
			{
				ES3.GetKeys(settings);
				return true;
			}
			catch (Exception ex)
			{
				UtilityMethods.Log("[SaveStorage] Structural validation failed. Path=" + path + ", Exception=" + ex.Message, LogType.Warning);
				return false;
			}
		}

		private void CreateRotatingBackup(string path)
		{
			ES3Settings eS3Settings = CreatePersistentSettings(path);
			if (!ES3.FileExists(eS3Settings))
			{
				return;
			}
			if (!IsPhysicalFileStructurallyValid(path))
			{
				_writeProtectedPaths.Add(path);
				throw new InvalidDataException("Refusing to overwrite unreadable save file '" + path + "'.");
			}
			string path2 = ResolveFullPath(BuildBackupPath(path, 3));
			if (File.Exists(path2))
			{
				File.Delete(path2);
			}
			for (int num = 2; num >= 1; num--)
			{
				string text = ResolveFullPath(BuildBackupPath(path, num));
				if (File.Exists(text))
				{
					string destFileName = ResolveFullPath(BuildBackupPath(path, num + 1));
					File.Move(text, destFileName);
				}
			}
			File.Copy(eS3Settings.FullPath, ResolveFullPath(BuildBackupPath(path, 1)));
		}

		private void TryRestoreBackupAfterFailedWrite(string path)
		{
			for (int i = 1; i <= 3; i++)
			{
				string text = BuildBackupPath(path, i);
				if (IsPhysicalFileStructurallyValid(text))
				{
					try
					{
						RestoreCandidate(path, text);
						CacheFile(path);
						_writeProtectedPaths.Remove(path);
						return;
					}
					catch (Exception ex)
					{
						_writeProtectedPaths.Add(path);
						UtilityMethods.Log("[SaveStorage] Previous backup could not be restored after failed write. Path=" + path + ", Backup=" + text + ", Exception=" + ex.Message, LogType.Error);
					}
				}
			}
			_writeProtectedPaths.Add(path);
		}

		private void RestoreCandidate(string primaryPath, string candidatePath)
		{
			string text = ResolveFullPath(primaryPath);
			string sourceFileName = ResolveFullPath(candidatePath);
			if (File.Exists(text))
			{
				string destFileName = ResolveFullPath(BuildCorruptPath(primaryPath));
				File.Move(text, destFileName);
			}
			File.Copy(sourceFileName, text);
		}

		private void DeleteRecoveryArtifacts(string path)
		{
			DeletePhysicalFileIfExists(path + ".tmp");
			DeletePhysicalFileIfExists(path + ".tmp.bak");
			DeletePhysicalFileIfExists(path + ".bac");
			for (int i = 1; i <= 3; i++)
			{
				DeletePhysicalFileIfExists(BuildBackupPath(path, i));
			}
		}

		private void DeletePhysicalFileIfExists(string path)
		{
			string path2 = ResolveFullPath(path);
			if (File.Exists(path2))
			{
				File.Delete(path2);
			}
		}

		private string ResolveFullPath(string path)
		{
			return CreatePersistentSettings(path).FullPath;
		}

		private ES3Settings CreatePersistentSettings(string path)
		{
			ES3Settings eS3Settings = new ES3Settings(path, _settings);
			if (eS3Settings.location != ES3.Location.Cache)
			{
				return eS3Settings;
			}
			eS3Settings = (ES3Settings)eS3Settings.Clone();
			eS3Settings.location = ((ES3Settings.defaultSettings.location != ES3.Location.Cache) ? ES3Settings.defaultSettings.location : ES3.Location.File);
			return eS3Settings;
		}

		private static string[] GetRecoveryCandidates(string path)
		{
			return new string[6]
			{
				path + ".tmp",
				path + ".tmp.bak",
				BuildBackupPath(path, 1),
				BuildBackupPath(path, 2),
				BuildBackupPath(path, 3),
				path + ".bac"
			};
		}

		private static string BuildBackupPath(string path, int index)
		{
			string directoryName = Path.GetDirectoryName(path);
			string extension = Path.GetExtension(path);
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
			string text = $"{fileNameWithoutExtension}.backup{index}{extension}";
			if (!string.IsNullOrEmpty(directoryName))
			{
				return Path.Combine(directoryName, text);
			}
			return text;
		}

		private static string BuildCorruptPath(string path)
		{
			string directoryName = Path.GetDirectoryName(path);
			string extension = Path.GetExtension(path);
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
			string text = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
			string text2 = fileNameWithoutExtension + ".corrupt_" + text + extension;
			if (!string.IsNullOrEmpty(directoryName))
			{
				return Path.Combine(directoryName, text2);
			}
			return text2;
		}

		private void ThrowIfWriteProtected(string path)
		{
			if (_writeProtectedPaths.Contains(path))
			{
				throw new InvalidDataException("Writes to '" + path + "' are blocked because the existing save could not be recovered.");
			}
		}

		public bool RunSafely(string fileName, Action action)
		{
			try
			{
				action();
				return true;
			}
			catch (Exception ex)
			{
				UtilityMethods.Log($"[SaveStorage] Save failed. fileName={fileName}, exception={ex}", LogType.Error);
				ErrorHandler.HandleSaveError("Saving Error", ex, fileName);
				return false;
			}
		}

		public string CreateStaffMigrationBackup(string key, SaveSlotData slot, string path)
		{
			return CreateMigrationBackup(key, slot, path, "before-staff-stack-test-v1");
		}

		public string CreateMigrationBackup(string key, SaveSlotData slot, string path, string backupTag)
		{
			if (IsCapturingDeferredWrites)
			{
				throw new InvalidOperationException("Cannot migrate during save capture.");
			}
			FlushDeferredWrites();
			ThrowIfWriteProtected(path);
			string text = ResolveFullPath(path);
			string text2 = Path.Combine(Path.GetDirectoryName(text), $"{Path.GetFileNameWithoutExtension(text)}.{backupTag}.{Guid.NewGuid():N}.es3");
			if (File.Exists(text))
			{
				if (ES3.Load<SaveSlotData>(key, CreatePersistentSettings(path)) == null)
				{
					throw new IOException("Original save could not be verified before migration.");
				}
				File.Copy(text, text2, overwrite: false);
				if (!File.ReadAllBytes(text).SequenceEqual(File.ReadAllBytes(text2)))
				{
					throw new IOException("Migration backup does not match the original file.");
				}
			}
			else
			{
				Directory.CreateDirectory(Path.GetDirectoryName(text2));
				ES3.Save(key, slot, CreatePersistentSettings(text2));
			}
			if (ES3.Load<SaveSlotData>(key, CreatePersistentSettings(text2)) == null)
			{
				throw new IOException("Migration backup could not be read.");
			}
			return text2;
		}

		public void RestoreStaffMigrationBackup(string path, string backup)
		{
			RestoreMigrationBackup(path, backup);
		}

		public void RestoreMigrationBackup(string path, string backup)
		{
			try
			{
				File.Copy(backup, ResolveFullPath(path), overwrite: true);
				if (_settings.location == ES3.Location.Cache)
				{
					ES3.DeleteFile(path, _settings);
				}
				CacheFile(path);
			}
			catch
			{
				ProtectWrites(path);
				throw;
			}
		}

		public void BeginDeferredWrites()
		{
			if (IsCapturingDeferredWrites || _processingDeferredWrite || HasDeferredWrites)
			{
				throw new InvalidOperationException("A save batch is already active.");
			}
			_deferredWriteFailed = false;
			IsCapturingDeferredWrites = true;
		}

		public void EndDeferredWrites()
		{
			IsCapturingDeferredWrites = false;
		}

		private void CaptureDeferredWrite<T>(string key, T data, string path)
		{
			ThrowIfWriteProtected(path);
			using (SerializeMarker.Auto())
			{
				ES3.Save(key, data, path, _settings);
			}
			ES3Settings settings = new ES3Settings(path, _settings)
			{
				postprocessRawCachedData = false
			};
			byte[] snapshot;
			using (SnapshotMarker.Auto())
			{
				snapshot = ES3.LoadRawBytes(settings);
			}
			foreach (DeferredWrite deferredWrite in _deferredWrites)
			{
				if (string.Equals(deferredWrite.Path, path, StringComparison.Ordinal))
				{
					deferredWrite.Snapshot = snapshot;
					return;
				}
			}
			_deferredWrites.Enqueue(new DeferredWrite
			{
				Path = path,
				Snapshot = snapshot
			});
		}

		public void ProcessDeferredStep()
		{
			if (IsCapturingDeferredWrites || _processingDeferredWrite || !HasDeferredWrites)
			{
				return;
			}
			_processingDeferredWrite = true;
			DeferredWrite write = _deferredWrites.Peek();
			try
			{
				if (!RunSafely(write.Path, () =>
				{
					ThrowIfWriteProtected(write.Path);
					switch (write.Stage)
					{
					case 0:
						using (BackupMarker.Auto())
						{
							CreateRotatingBackup(write.Path);
							break;
						}
					case 1:
						using (WriteMarker.Auto())
						{
							ES3.SaveRaw(write.Snapshot, CreatePersistentSettings(write.Path));
							break;
						}
					default:
						VerifySnapshot(write.Path, write.Snapshot);
						break;
					}
				}))
				{
					_deferredWriteFailed = true;
					if (write.Stage == 1)
					{
						TryRestoreBackupAfterFailedWrite(write.Path);
					}
					ES3.DeleteFile(write.Path, _settings);
					if (IsPhysicalFileStructurallyValid(write.Path))
					{
						CacheFile(write.Path);
					}
					_deferredWrites.Dequeue();
				}
				else if (++write.Stage == 3)
				{
					_deferredWrites.Dequeue();
				}
			}
			catch (Exception arg)
			{
				_deferredWriteFailed = true;
				_writeProtectedPaths.Add(write.Path);
				_deferredWrites.Dequeue();
				UtilityMethods.Log($"[SaveStorage] Deferred save recovery failed. Path={write.Path}, Exception={arg}", LogType.Error);
			}
			finally
			{
				_processingDeferredWrite = false;
			}
		}

		public bool FlushDeferredWrites()
		{
			if (IsCapturingDeferredWrites || _processingDeferredWrite)
			{
				return false;
			}
			while (HasDeferredWrites)
			{
				ProcessDeferredStep();
			}
			return DeferredWritesSucceeded;
		}

		private void VerifyWrittenFile<T>(string key, string path)
		{
			CandidateState candidateState;
			using (VerifyMarker.Auto())
			{
				candidateState = TryReadPhysicalCandidate<T>(key, path, out var _);
			}
			if (candidateState == CandidateState.Valid)
			{
				return;
			}
			TryRestoreBackupAfterFailedWrite(path);
			throw new InvalidDataException("Save verification failed after writing '" + path + "'. The previous backup was restored when available.");
		}

		private void VerifySnapshot(string path, byte[] expected)
		{
			bool flag;
			using (VerifyMarker.Auto())
			{
				using (VerifyBytesMarker.Auto())
				{
					try
					{
						byte[] array = ES3.LoadRawBytes(CreatePersistentSettings(path));
						flag = expected.AsSpan().SequenceEqual(array);
					}
					catch
					{
						flag = false;
					}
				}
			}
			if (flag)
			{
				return;
			}
			TryRestoreBackupAfterFailedWrite(path);
			throw new InvalidDataException("Save snapshot verification failed after writing '" + path + "'. The previous backup was restored when available.");
		}
	}
}
