using System;
using System.Collections.Generic;
using UnityEngine;

namespace PaperPixelGames.Core.Data
{
	public static class CustomSushiThumbnailStore
	{
		public const int MaxJpegBytes = 32768;

		private static readonly Dictionary<string, Sprite> Icons = new Dictionary<string, Sprite>(StringComparer.Ordinal);

		public static event Action<string> IconChanged;

		public static Sprite GetIcon(string recipeId)
		{
			if (string.IsNullOrEmpty(recipeId) || !Icons.TryGetValue(recipeId, out var value))
			{
				return null;
			}
			return value;
		}

		public static bool IsValidJpeg(byte[] bytes)
		{
			if (bytes != null && bytes.Length >= 64 && bytes.Length <= 32768 && bytes[0] == byte.MaxValue && bytes[1] == 216 && bytes[^2] == byte.MaxValue)
			{
				return bytes[^1] == 217;
			}
			return false;
		}

		public static byte[] DecodeSavedJpeg(CustomSushiRecipeData recipe)
		{
			if (recipe == null || string.IsNullOrEmpty(recipe.IconJpegBase64) || recipe.IconJpegBase64.Length > 43692)
			{
				return null;
			}
			try
			{
				byte[] array = Convert.FromBase64String(recipe.IconJpegBase64);
				return IsValidJpeg(array) ? array : null;
			}
			catch (FormatException)
			{
				return null;
			}
		}

		public static bool NeedsPhotoRepair(CustomSushiRecipeData recipe)
		{
			if (recipe != null)
			{
				if (!(recipe.IconId != "photo"))
				{
					return DecodeSavedJpeg(recipe) == null;
				}
				return true;
			}
			return false;
		}

		public static bool SetIcon(string recipeId, byte[] bytes)
		{
			if (string.IsNullOrWhiteSpace(recipeId) || !IsValidJpeg(bytes))
			{
				return false;
			}
			Texture2D texture2D = null;
			try
			{
				texture2D = new Texture2D(2, 2, TextureFormat.RGB24, mipChain: false);
				if (!texture2D.LoadImage(bytes, markNonReadable: true) || texture2D.width > 512 || texture2D.height > 512)
				{
					return false;
				}
				Sprite value = Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f), 100f);
				texture2D = null;
				if (Icons.TryGetValue(recipeId, out var value2))
				{
					UnityEngine.Object.Destroy(value2.texture);
					UnityEngine.Object.Destroy(value2);
				}
				Icons[recipeId] = value;
				IconChanged?.Invoke(recipeId);
				return true;
			}
			catch (UnityException)
			{
				return false;
			}
			finally
			{
				if (texture2D != null)
				{
					UnityEngine.Object.Destroy(texture2D);
				}
			}
		}

		public static void Clear()
		{
			List<string> list = new List<string>(Icons.Keys);
			foreach (Sprite value in Icons.Values)
			{
				if (!(value == null))
				{
					UnityEngine.Object.Destroy(value.texture);
					UnityEngine.Object.Destroy(value);
				}
			}
			Icons.Clear();
			foreach (string item in list)
			{
				IconChanged?.Invoke(item);
			}
		}

		public static void RemoveIcon(string recipeId)
		{
			if (!string.IsNullOrWhiteSpace(recipeId) && Icons.Remove(recipeId, out var value))
			{
				if (value != null)
				{
					UnityEngine.Object.Destroy(value.texture);
					UnityEngine.Object.Destroy(value);
				}
				IconChanged?.Invoke(recipeId);
			}
		}
	}
}
