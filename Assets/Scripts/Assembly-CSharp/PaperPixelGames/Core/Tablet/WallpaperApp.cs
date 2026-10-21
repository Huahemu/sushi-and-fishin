using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Lean.Pool;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.Utility;
using SFB;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Tablet
{
	public class WallpaperApp : BaseApp
	{
		[SerializeField]
		private Image _tabletWallpaper;

		[SerializeField]
		private BaseButton _openFileBrowserButton;

		[Space]
		[SerializeField]
		private Transform _contentTransform;

		[SerializeField]
		private WallpaperItem _wallpaperItem;

		[Header("LoadingInfo")]
		[SerializeField]
		private GameObject _loadingInfoObj;

		[SerializeField]
		private TextMeshProUGUI _loadingText;

		private Dictionary<WallpaperItem, string> _wallpaperItemToFileName = new Dictionary<WallpaperItem, string>();

		private List<Sprite> _allWallpapers = new List<Sprite>();

		private readonly HashSet<Sprite> _ownedWallpapers = new HashSet<Sprite>();

		private readonly HashSet<WallpaperItem> _spawnedItems = new HashSet<WallpaperItem>();

		private string _wallpaperPath;

		private EventBinding<DeleteWallpaper> _deleteWallpeperEventBinding;

		private EventBinding<ChangeTabletWallpaper> _changeTabletWallpaperEventBinding;

		private bool _initialized;

		private bool _destroyed;

		private SaveSlotData _saveSlot => Singleton<SaveManager>.Instance.CurrentSaveSlotData;

		public override void Initialize()
		{
			if (_initialized)
			{
				return;
			}
			_initialized = true;
			_openFileBrowserButton.Clicked += OnClickOpenFileBrowserButton;
			_deleteWallpeperEventBinding = new EventBinding<DeleteWallpaper>(DeleteWallpaper);
			EventBus<global::DeleteWallpaper>.Register(_deleteWallpeperEventBinding);
			_changeTabletWallpaperEventBinding = new EventBinding<ChangeTabletWallpaper>(ChangeWallpaper);
			EventBus<ChangeTabletWallpaper>.Register(_changeTabletWallpaperEventBinding);
			_wallpaperPath = Path.Combine(Application.persistentDataPath, "Wallpapers");
			if (!Directory.Exists(_wallpaperPath))
			{
				Directory.CreateDirectory(_wallpaperPath);
			}
			LoadDefaultWallpapers();
			LoadSavedWallpapers();
			string wallpaperName = _saveSlot?.PlayerData?.TabletData?.WallpaperName;
			if (!string.IsNullOrEmpty(wallpaperName))
			{
				_tabletWallpaper.sprite = _allWallpapers.FirstOrDefault((Sprite item) => item.name == wallpaperName);
			}
			UtilityMethods.Log("CurrentSaveSlotIndex: " + Singleton<SaveManager>.Instance.CurrentSaveSlotIndex);
		}

		private void OnDestroy()
		{
			_destroyed = true;
			if (_openFileBrowserButton != null)
			{
				_openFileBrowserButton.Clicked -= OnClickOpenFileBrowserButton;
			}
			if (_initialized)
			{
				EventBus<global::DeleteWallpaper>.Deregister(_deleteWallpeperEventBinding);
				EventBus<ChangeTabletWallpaper>.Deregister(_changeTabletWallpaperEventBinding);
			}
			_initialized = false;
			if (_tabletWallpaper != null && _ownedWallpapers.Contains(_tabletWallpaper.sprite))
			{
				_tabletWallpaper.sprite = null;
			}
			foreach (WallpaperItem spawnedItem in _spawnedItems)
			{
				if (!(spawnedItem == null))
				{
					spawnedItem.PrepareForDespawn();
					LeanPool.Despawn(spawnedItem);
				}
			}
			_spawnedItems.Clear();
			foreach (Sprite ownedWallpaper in _ownedWallpapers)
			{
				DestroyWallpaper(ownedWallpaper);
			}
			_ownedWallpapers.Clear();
			_allWallpapers.Clear();
			_wallpaperItemToFileName.Clear();
		}

		public async void OnClickOpenFileBrowserButton()
		{
			UtilityMethods.Log("Clicked");
			Singleton<SoundManager>.Instance.PlaySFX(SoundType.Click_Down);
			string[] array = StandaloneFileBrowser.OpenFilePanel("Select Images", "", "png", multiselect: true);
			if (array.Length != 0)
			{
				try
				{
					await LoadAndSaveWallpapers(array);
				}
				catch (Exception arg)
				{
					Debug.LogError($"Wallpaper import failed: {arg}");
				}
			}
		}

		private void LoadDefaultWallpapers()
		{
			Sprite[] array = Resources.LoadAll<Sprite>("Wallpapers");
			foreach (Sprite sprite in array)
			{
				DefaultWallpaperData wallpaperData = GetWallpaperData(sprite.name);
				if (wallpaperData != null)
				{
					WallpaperItem wallpaperItem = LeanPool.Spawn(_wallpaperItem, _contentTransform);
					_spawnedItems.Add(wallpaperItem);
					wallpaperItem.Initialize(sprite, wallpaperData.IsHidden, defaultWallpaper: true);
					_allWallpapers.Add(sprite);
				}
			}
		}

		private DefaultWallpaperData GetWallpaperData(string spriteName)
		{
			return _saveSlot.PlayerData.TabletData.WallpaperData.Find((DefaultWallpaperData data) => data.SpriteName == spriteName);
		}

		private async Task LoadAndSaveWallpapers(string[] filePaths)
		{
			int currentSaveSlotIndex = Singleton<SaveManager>.Instance.CurrentSaveSlotIndex;
			string saveSlotPath = Path.Combine(_wallpaperPath, "SaveSlot_" + currentSaveSlotIndex);
			if (!Directory.Exists(saveSlotPath))
			{
				Directory.CreateDirectory(saveSlotPath);
			}
			_loadingInfoObj.SetActive(value: true);
			int totalFiles = filePaths.Length;
			try
			{
				for (int i = 0; i < totalFiles; i++)
				{
					string text = filePaths[i];
					string fileName = Path.GetFileName(text);
					string savePath = Path.Combine(saveSlotPath, fileName);
					_loadingText.text = TabletLocalization.Format("tablet.wallpaper.loading_file", "Loading {0}/{1}: {2}", i + 1, totalFiles, fileName);
					if (_wallpaperItemToFileName.ContainsValue(Path.GetFileNameWithoutExtension(fileName)))
					{
						Debug.LogWarning("Wallpaper already exists: " + fileName + ". Skipping save.");
						continue;
					}
					byte[] fileData = await LoadImageFileAsync(text);
					if (_destroyed || this == null)
					{
						return;
					}
					Sprite sprite = CreateOwnedWallpaper(fileData, Path.GetFileNameWithoutExtension(fileName), savePath);
					_allWallpapers.Add(sprite);
					WallpaperItem wallpaperItem = LeanPool.Spawn(_wallpaperItem, _contentTransform);
					_spawnedItems.Add(wallpaperItem);
					wallpaperItem.Initialize(sprite);
					_wallpaperItemToFileName.Add(wallpaperItem, sprite.name);
				}
				Debug.Log("All wallpapers loaded successfully.");
			}
			finally
			{
				if (!_destroyed && _loadingInfoObj != null)
				{
					_loadingInfoObj.SetActive(value: false);
				}
			}
		}

		private Sprite CreateOwnedWallpaper(byte[] fileData, string spriteName, string savePath = null)
		{
			Texture2D texture2D = new Texture2D(2, 2);
			Sprite sprite = null;
			try
			{
				if (!texture2D.LoadImage(fileData))
				{
					throw new InvalidDataException("Invalid wallpaper image: " + spriteName);
				}
				if (savePath != null)
				{
					File.WriteAllBytes(savePath, fileData);
				}
				sprite = Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f));
				sprite.name = spriteName;
				_ownedWallpapers.Add(sprite);
				return sprite;
			}
			catch
			{
				DestroyOwnedObject(sprite);
				DestroyOwnedObject(texture2D);
				throw;
			}
		}

		private static void DestroyOwnedObject(UnityEngine.Object resource)
		{
			if (!(resource == null))
			{
				if (Application.isPlaying)
				{
					UnityEngine.Object.Destroy(resource);
				}
				else
				{
					UnityEngine.Object.DestroyImmediate(resource);
				}
			}
		}

		private static void DestroyWallpaper(Sprite sprite)
		{
			if (!(sprite == null))
			{
				Texture2D texture = sprite.texture;
				DestroyOwnedObject(sprite);
				DestroyOwnedObject(texture);
			}
		}

		private void ReleaseUnusedWallpaper(Sprite sprite)
		{
			if (!(sprite == null) && !_allWallpapers.Contains(sprite) && (!(_tabletWallpaper != null) || !(_tabletWallpaper.sprite == sprite)) && _ownedWallpapers.Remove(sprite))
			{
				DestroyWallpaper(sprite);
			}
		}

		private async Task<byte[]> LoadImageFileAsync(string filePath)
		{
			return await Task.Run(() => File.ReadAllBytes(filePath));
		}

		private void LoadSavedWallpapers()
		{
			int currentSaveSlotIndex = Singleton<SaveManager>.Instance.CurrentSaveSlotIndex;
			string text = Path.Combine(_wallpaperPath, "SaveSlot_" + currentSaveSlotIndex);
			if (!Directory.Exists(text))
			{
				UtilityMethods.Log("Save slot directory does not exist: " + text);
				return;
			}
			string[] files = Directory.GetFiles(text, "*.png");
			foreach (string path in files)
			{
				byte[] fileData = File.ReadAllBytes(path);
				Sprite sprite = CreateOwnedWallpaper(fileData, Path.GetFileNameWithoutExtension(path));
				_allWallpapers.Add(sprite);
				WallpaperItem wallpaperItem = LeanPool.Spawn(_wallpaperItem, _contentTransform);
				_spawnedItems.Add(wallpaperItem);
				wallpaperItem.Initialize(sprite);
				_wallpaperItemToFileName.Add(wallpaperItem, sprite.name);
			}
		}

		public void DeleteWallpaper(DeleteWallpaper @event)
		{
			string spirteName = @event.SpriteName;
			int currentSaveSlotIndex = Singleton<SaveManager>.Instance.CurrentSaveSlotIndex;
			string text = Path.Combine(Path.Combine(_wallpaperPath, "SaveSlot_" + currentSaveSlotIndex), spirteName + ".png");
			if (File.Exists(text))
			{
				File.Delete(text);
				Debug.Log("File deleted: " + text);
				Sprite sprite = _allWallpapers.Find((Sprite sprite2) => sprite2.name == spirteName);
				if (!(sprite == null))
				{
					_allWallpapers.Remove(sprite);
					UpdateUIAfterDeletion(sprite);
					ReleaseUnusedWallpaper(sprite);
					Debug.Log("Sprite removed: " + spirteName);
				}
			}
		}

		private void ChangeWallpaper(ChangeTabletWallpaper @event)
		{
			Sprite sprite = _tabletWallpaper.sprite;
			_tabletWallpaper.sprite = @event.WallpaperSprite;
			_tabletWallpaper.color = Color.white;
			_saveSlot.PlayerData.TabletData.WallpaperName = @event.WallpaperSprite.name;
			ReleaseUnusedWallpaper(sprite);
		}

		private void UpdateUIAfterDeletion(Sprite sprite)
		{
			KeyValuePair<WallpaperItem, string> keyValuePair = _wallpaperItemToFileName.FirstOrDefault((KeyValuePair<WallpaperItem, string> item) => item.Value == sprite.name);
			if (!keyValuePair.Equals(default(KeyValuePair<WallpaperItem, string>)))
			{
				keyValuePair.Key.PrepareForDespawn();
				LeanPool.Despawn(keyValuePair.Key);
				_spawnedItems.Remove(keyValuePair.Key);
				_wallpaperItemToFileName.Remove(keyValuePair.Key);
			}
		}
	}
}
