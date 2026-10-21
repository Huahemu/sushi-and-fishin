using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Tablet
{
	public class WallpaperItem : MonoBehaviour
	{
		[SerializeField]
		private Image _image;

		[SerializeField]
		private BaseButton _deleteButton;

		[SerializeField]
		private BaseButton _wallpaperSelectButton;

		[SerializeField]
		private Image _border;

		private static WallpaperItem _currentlySelectedWallpaperItem;

		private string _spriteName;

		private bool _isDefaultWallpaper;

		private SaveSlotData _saveSlot => Singleton<SaveManager>.Instance.CurrentSaveSlotData;

		public void Initialize(Sprite sprite, bool isHidden = false, bool defaultWallpaper = false)
		{
			PrepareForDespawn();
			_image.sprite = sprite;
			_spriteName = sprite.name;
			_isDefaultWallpaper = defaultWallpaper;
			_deleteButton.Clicked += OnClickDeleteButton;
			_wallpaperSelectButton.Clicked += OnClickWallpaperSelectButton;
			if (sprite.name == _saveSlot.PlayerData.TabletData.WallpaperName)
			{
				_currentlySelectedWallpaperItem = this;
				Select();
			}
			if (isHidden)
			{
				base.gameObject.SetActive(value: false);
			}
		}

		public void PrepareForDespawn()
		{
			if (_deleteButton != null)
			{
				_deleteButton.Clicked -= OnClickDeleteButton;
			}
			if (_wallpaperSelectButton != null)
			{
				_wallpaperSelectButton.Clicked -= OnClickWallpaperSelectButton;
			}
			if (_image != null)
			{
				_image.sprite = null;
			}
			if (_border != null)
			{
				Deselect();
			}
			if (_currentlySelectedWallpaperItem == this)
			{
				_currentlySelectedWallpaperItem = null;
			}
		}

		private void OnDestroy()
		{
			PrepareForDespawn();
		}

		private void OnClickDeleteButton()
		{
			if (_isDefaultWallpaper)
			{
				DefaultWallpaperData defaultWallpaperData = _saveSlot.PlayerData.TabletData.WallpaperData.Find((DefaultWallpaperData data) => data.SpriteName == _spriteName);
				if (defaultWallpaperData == null)
				{
					DefaultWallpaperData item = new DefaultWallpaperData
					{
						SpriteName = _spriteName,
						IsHidden = true
					};
					_saveSlot.PlayerData.TabletData.WallpaperData.Add(item);
				}
				else
				{
					defaultWallpaperData.IsHidden = true;
				}
				base.gameObject.SetActive(value: false);
			}
			else
			{
				EventBus<DeleteWallpaper>.Raise(new DeleteWallpaper
				{
					SpriteName = _spriteName
				});
			}
			Singleton<SoundManager>.Instance.PlaySFX(SoundType.Click_Down);
		}

		private void OnClickWallpaperSelectButton()
		{
			if (_currentlySelectedWallpaperItem != null && _currentlySelectedWallpaperItem != this)
			{
				_currentlySelectedWallpaperItem.Deselect();
			}
			Singleton<SoundManager>.Instance.PlaySFX(SoundType.Click_Down);
			_currentlySelectedWallpaperItem = this;
			Select();
			EventBus<ChangeTabletWallpaper>.Raise(new ChangeTabletWallpaper
			{
				WallpaperSprite = _image.sprite
			});
		}

		private void Select()
		{
			_border.color = Color.green;
		}

		private void Deselect()
		{
			_border.color = Color.white;
		}
	}
}
