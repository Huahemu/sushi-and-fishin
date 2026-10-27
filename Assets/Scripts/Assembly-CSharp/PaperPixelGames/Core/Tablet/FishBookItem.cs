using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Struct;
using PaperPixelGames.Framework.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Tablet
{
	public class FishBookItem : MonoBehaviour
	{
		[Header("Main UI")]
		[SerializeField]
		private Image _fishIconImage;

		[SerializeField]
		private TextMeshProUGUI _fishNameText;

		[SerializeField]
		private BaseButton _detailsButton;

		[SerializeField]
		private GameObject _selectedOutline;

		[Header("Locked Visual")]
		[SerializeField]
		private Color _unlockedIconColor = Color.white;

		[SerializeField]
		private Color _lockedIconColor = new Color(0f, 0f, 0f, 0.35f);

		private FishBookApp _fishBookApp;

		private FishSO _fishData;

		private FishBookEntry _currentEntry;

		public FishSO FishData => _fishData;

		public string FishId
		{
			get
			{
				if (!(_fishData != null))
				{
					return string.Empty;
				}
				return _fishData.Id;
			}
		}

		public bool IsUnlocked => SushiAndFish.FishingIntegration.FishingTablet.Acquired(_fishData,_currentEntry.IsUnlocked);

		public void Initialize(FishBookApp app, FishSO fishData)
		{
			_fishBookApp = app;
			_fishData = fishData;
			if (_fishNameText != null)
			{
				_fishNameText.text = ((_fishData != null) ? _fishData.LocalizedName : string.Empty);
			}
			if (_fishIconImage != null)
			{
				_fishIconImage.material=null;_fishIconImage.useSpriteMesh=false;
				_fishIconImage.sprite = ((_fishData != null) ? _fishData.Sprite : null);
				_fishIconImage.color = _lockedIconColor;
			}
			if (_detailsButton != null)
			{
				_detailsButton.Clicked -= OnClickDetailsButton;
				_detailsButton.Clicked += OnClickDetailsButton;
			}
			SetSelected(selected: false);
		}

		public void ApplyEntry(FishBookEntry entry)
		{
			_currentEntry = entry;
			bool isUnlocked = entry.IsUnlocked;
			ApplyLockedVisual(isUnlocked);
		}

		public void SetSelected(bool selected)
		{
			if (_selectedOutline != null)
			{
				_selectedOutline.SetActive(selected);
			}
		}

		public void RefreshLocalizedName()
		{
			if (!(_fishNameText == null) && !(_fishData == null))
			{
				_fishNameText.text = (IsUnlocked ? _fishData.LocalizedName : "???");
			}
		}

		private void ApplyLockedVisual(bool unlocked)
		{
			if (!(_fishIconImage == null))
			{
				_fishIconImage.color = (unlocked ? _unlockedIconColor : _lockedIconColor);
				RefreshLocalizedName();
			}
		}

		private void OnClickDetailsButton()
		{
			_fishBookApp?.SelectFish(this);
		}

        private void Update(){if(SushiAndFish.FishingIntegration.FishingMigration.Enabled)SushiAndFish.FishingIntegration.FishingTablet.Paint(_fishData,_fishIconImage,_fishNameText,false,_lockedIconColor,_unlockedIconColor,_currentEntry.IsUnlocked);}
		private void OnDestroy()
		{
			if (_detailsButton != null)
			{
				_detailsButton.Clicked -= OnClickDetailsButton;
			}
		}
	}
}
