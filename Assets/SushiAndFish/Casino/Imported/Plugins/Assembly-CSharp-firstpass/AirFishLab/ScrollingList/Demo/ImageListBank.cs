namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using global::System.IO;
using SushiAndFish.Casino.Original.AirFishLab.ScrollingList.ContentManagement;
using global::UnityEngine;

namespace AirFishLab.ScrollingList.Demo
{
	public class ImageListBank : BaseListBank
	{
		[SerializeField]
		private Texture2D _textureAtlas;

		private List<Sprite> _sprites = new List<Sprite>();

		private readonly SpriteListContent _contentWrapper = new SpriteListContent();

		public void SetTextureAtlas(Texture2D texture)
		{
			_textureAtlas = texture;
			LoadSpritesFromAtlas();
		}

		private void Awake()
		{
			LoadSpritesFromAtlas();
		}

		private void LoadSpritesFromAtlas()
		{
			_sprites.Clear();
			if (_textureAtlas == null)
			{
				Debug.LogWarning("Texture2D atlas not assigned in ImageListBank.");
				return;
			}
			string resourcesPath = GetResourcesPath();
			if (!string.IsNullOrEmpty(resourcesPath))
			{
				Sprite[] collection = SushiAndFish.Casino.Original.CasinoResources.LoadAll<Sprite>(resourcesPath);
				_sprites.AddRange(collection);
			}
			else
			{
				Debug.LogWarning("Could not load sprites from texture '" + _textureAtlas.name + "'. Make sure it's in a Resources folder.");
			}
		}

		private string GetResourcesPath()
		{
			if (_textureAtlas == null)
			{
				return null;
			}
			return Path.GetFileNameWithoutExtension(_textureAtlas.name);
		}

		public override IListContent GetListContent(int index)
		{
			_contentWrapper.Value = _sprites[Random.Range(0, _sprites.Count)];
			return _contentWrapper;
		}

		public override int GetContentCount()
		{
			return _sprites.Count;
		}
	}
}

}
