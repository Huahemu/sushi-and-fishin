using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Struct;
using PaperPixelGames.Framework.Utility;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using UnityServiceLocator;

namespace PaperPixelGames.Core.Tablet
{
	public class FishBookApp : BaseApp
	{
		[Header("Database")]
		[SerializeField]
		private ItemDatabaseSO _itemDatabase;

		[Header("Fish Grid")]
		[SerializeField]
		private FishBookItem _fishBookItemPrefab;

		[SerializeField]
		private Transform _bookContainer;

		[Header("Progress")]
		[SerializeField]
		private Image _progressFillImage;

		[SerializeField]
		private TextMeshProUGUI _progressPercentText;

		[Header("Detail Page")]
		[SerializeField]
		private FishBookDetailPage _detailPage;

		private readonly List<FishBookItem> _fishBookItems = new List<FishBookItem>();

		private readonly Dictionary<string, FishBookItem> _itemsByFishId = new Dictionary<string, FishBookItem>(StringComparer.Ordinal);

		private readonly Dictionary<string, FishBookEntry> _entriesByFishId = new Dictionary<string, FishBookEntry>(StringComparer.Ordinal);

		private FishBookItem _selectedItem;

		private string _selectedFishId;

		private bool _itemsBuilt;

		public override void Initialize()
		{
			base.Initialize();
			LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
			LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
			EnsureFishItemsBuilt();
			ApplyLockedStateToAllItems();
			UpdateProgressFill();
			UpdateDetailPageStatus(isShowingDetailPage: false);
		}

		public override void Show()
		{
			base.Show();
			RefreshLocalizedContent();
		}

		private void OnDestroy()
		{
			LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
		}

		private void OnSelectedLocaleChanged(Locale locale)
		{
			RefreshLocalizedContent();
		}

		private void RefreshLocalizedContent()
		{
			for (int i = 0; i < _fishBookItems.Count; i++)
			{
				if (_fishBookItems[i] != null)
				{
					_fishBookItems[i].RefreshLocalizedName();
				}
			}
			RefreshSelectedDetailPage();
		}

		public void OnFishBookEntryChanged(NetworkListEvent<FishBookEntry> change)
		{
			EnsureFishItemsBuilt();
			if ((uint)change.Type == 5u)
			{
				_entriesByFishId.Clear();
				ApplyLockedStateToAllItems();
				UpdateProgressFill();
				RefreshSelectedDetailPage();
				return;
			}
			FishBookEntry value = change.Value;
			string text = value.FishId.ToString();
			if (!string.IsNullOrEmpty(text))
			{
				switch (change.Type)
				{
				case NetworkListEvent<FishBookEntry>.EventType.Add:
				case NetworkListEvent<FishBookEntry>.EventType.Insert:
				case NetworkListEvent<FishBookEntry>.EventType.Value:
					ApplyEntryToLookupAndItem(value);
					break;
				case NetworkListEvent<FishBookEntry>.EventType.Remove:
				case NetworkListEvent<FishBookEntry>.EventType.RemoveAt:
					_entriesByFishId.Remove(text);
					ApplyLockedEntryToItem(text);
					break;
				}
				UpdateProgressFill();
				if (string.Equals(_selectedFishId, text, StringComparison.Ordinal))
				{
					RefreshSelectedDetailPage();
				}
			}
		}

		public void SetEntries(List<FishBookEntry> entries)
		{
			EnsureFishItemsBuilt();
			_entriesByFishId.Clear();
			if (entries != null)
			{
				for (int i = 0; i < entries.Count; i++)
				{
					FishBookEntry value = entries[i];
					string text = value.FishId.ToString();
					if (!string.IsNullOrEmpty(text))
					{
						_entriesByFishId[text] = value;
					}
				}
			}
			for (int j = 0; j < _fishBookItems.Count; j++)
			{
				FishBookItem fishBookItem = _fishBookItems[j];
				if (!(fishBookItem == null))
				{
					FishBookEntry entryOrLocked = GetEntryOrLocked(fishBookItem.FishId);
					fishBookItem.ApplyEntry(entryOrLocked);
				}
			}
			UpdateProgressFill();
			RefreshSelectedDetailPage();
		}

		public void SelectFish(FishBookItem item)
		{
			if (!(item == null) && !(item.FishData == null))
			{
				if (_selectedItem != null && _selectedItem != item)
				{
					_selectedItem.SetSelected(selected: false);
				}
				_selectedItem = item;
				_selectedFishId = item.FishId;
				_selectedItem.SetSelected(selected: true);
				RefreshSelectedDetailPage();
			}
		}

		public void UpdateDetailPageStatus(bool isShowingDetailPage)
		{
			if (_detailPage != null)
			{
				_detailPage.gameObject.SetActive(isShowingDetailPage);
			}
		}

		public void RefreshPersonalBest()
		{
			RefreshSelectedDetailPage();
		}

		private void EnsureFishItemsBuilt()
		{
			if (_itemsBuilt)
			{
				return;
			}
			_itemsBuilt = true;
			if (_itemDatabase == null)
			{
				UtilityMethods.Log("[FishBookApp] ItemDatabaseSO is missing.", LogType.Warning);
				return;
			}
			if (_fishBookItemPrefab == null)
			{
				UtilityMethods.Log("[FishBookApp] FishBookItem prefab is missing.", LogType.Warning);
				return;
			}
			if (_bookContainer == null)
			{
				UtilityMethods.Log("[FishBookApp] Book container is missing.", LogType.Warning);
				return;
			}
			IReadOnlyList<ItemSO> itemsByItemType = _itemDatabase.GetItemsByItemType(ItemType.Creature);
			for (int i = 0; i < itemsByItemType.Count; i++)
			{
				if (itemsByItemType[i] is FishSO fishSO)
				{
					if (string.IsNullOrEmpty(fishSO.Id))
					{
						UtilityMethods.Log("[FishBookApp] FishSO has empty Id: " + fishSO.name, LogType.Warning);
						continue;
					}
					if (_itemsByFishId.ContainsKey(fishSO.Id))
					{
						UtilityMethods.Log("[FishBookApp] Duplicate fish Id found: " + fishSO.Id, LogType.Warning);
						continue;
					}
					FishBookItem fishBookItem = UnityEngine.Object.Instantiate(_fishBookItemPrefab, _bookContainer);
					fishBookItem.Initialize(this, fishSO);
					FishBookEntry entry = CreateLockedEntry(fishSO.Id);
					fishBookItem.ApplyEntry(entry);
					_fishBookItems.Add(fishBookItem);
					_itemsByFishId.Add(fishSO.Id, fishBookItem);
				}
			}
		}

		private void ApplyEntryToLookupAndItem(FishBookEntry entry)
		{
			string text = entry.FishId.ToString();
			if (!string.IsNullOrEmpty(text))
			{
				_entriesByFishId[text] = entry;
				if (_itemsByFishId.TryGetValue(text, out var value))
				{
					value.ApplyEntry(entry);
				}
				else
				{
					UtilityMethods.Log("[FishBookApp] Received FishBookEntry for missing FishSO id: " + text, LogType.Warning);
				}
			}
		}

		private void ApplyLockedStateToAllItems()
		{
			for (int i = 0; i < _fishBookItems.Count; i++)
			{
				FishBookItem fishBookItem = _fishBookItems[i];
				if (!(fishBookItem == null))
				{
					fishBookItem.ApplyEntry(CreateLockedEntry(fishBookItem.FishId));
				}
			}
		}

		private void ApplyLockedEntryToItem(string fishId)
		{
			if (_itemsByFishId.TryGetValue(fishId, out var value))
			{
				value.ApplyEntry(CreateLockedEntry(fishId));
			}
		}

		private FishBookEntry GetEntryOrLocked(string fishId)
		{
			if (!string.IsNullOrEmpty(fishId) && _entriesByFishId.TryGetValue(fishId, out var value))
			{
				return value;
			}
			return CreateLockedEntry(fishId);
		}

		private void UpdateProgressFill()
		{
			int count = _fishBookItems.Count;
			int num = 0;
			for (int i = 0; i < _fishBookItems.Count; i++)
			{
				FishBookItem fishBookItem = _fishBookItems[i];
				if (fishBookItem != null && fishBookItem.IsUnlocked)
				{
					num++;
				}
			}
			float num2 = ((count <= 0) ? 0f : ((float)num / (float)count));
			if (_progressFillImage != null)
			{
				_progressFillImage.fillAmount = num2;
			}
			if (_progressPercentText != null)
			{
				_progressPercentText.text = $"{Mathf.RoundToInt(num2 * 100f)}%";
			}
		}

		private void RefreshSelectedDetailPage()
		{
			if (_selectedItem == null || string.IsNullOrEmpty(_selectedFishId))
			{
				UpdateDetailPageStatus(isShowingDetailPage: false);
				return;
			}
			if (_detailPage == null)
			{
				UpdateDetailPageStatus(isShowingDetailPage: false);
				return;
			}
			FishSO fishData = _selectedItem.FishData;
			if (fishData == null)
			{
				UpdateDetailPageStatus(isShowingDetailPage: false);
				return;
			}
			FishBookEntry entryOrLocked = GetEntryOrLocked(_selectedFishId);
			float personalBest = GetPersonalBest(fishData.Id);
			_detailPage.Render(entryOrLocked, fishData, personalBest);
			UpdateDetailPageStatus(isShowingDetailPage: true);
		}

		private float GetPersonalBest(string fishId)
		{
			ServiceLocator.ForSceneOf(this).Get<TabletNetworkHandler>(out var service);
			if (service == null)
			{
				return 0f;
			}
			return service.GetPersonalBest(fishId);
		}

		private static FishBookEntry CreateLockedEntry(string fishId)
		{
			FishLeaderboardEntry score = default;
			FishLeaderboardEntry score2 = default;
			FishLeaderboardEntry score3 = default;
			score.Clear();
			score2.Clear();
			score3.Clear();
			return new FishBookEntry
			{
				FishId = fishId,
				IsUnlocked = false,
				TotalCaughtAmount = 0,
				Score1 = score,
				Score2 = score2,
				Score3 = score3
			};
		}
	}
}
