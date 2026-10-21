using System;
using PaperPixelGames.Core.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace PaperPixelGames.Framework.States.MainMenu
{
	public sealed class LobbyRoomLabel : MonoBehaviour
	{
		[SerializeField]
		private string _key;

		[SerializeField]
		private string _english;

		[SerializeField]
		private string _turkish;

		public void Set(string key, string english, string turkish)
		{
			_key = key;
			_english = english;
			_turkish = turkish;
			Refresh();
		}

		public void Refresh()
		{
			if (TryGetComponent<TMP_Text>(out var component))
			{
				component.text = Text(_key, _english, _turkish);
			}
		}

		public static string Text(string key, string english, string turkish)
		{
			if (!Application.isPlaying)
			{
				return english;
			}
			string fallback = ((LocalizationSettings.SelectedLocale?.Identifier.Code ?? string.Empty).StartsWith("tr", StringComparison.OrdinalIgnoreCase) ? turkish : english);
			return LocalizationDatabase.GetStringOrFallback("UiTable", "ui.lobby." + key, fallback);
		}
	}
}
