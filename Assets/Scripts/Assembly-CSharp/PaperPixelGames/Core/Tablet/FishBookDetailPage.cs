using System.Collections.Generic;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Struct;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Tablet
{
	public class FishBookDetailPage : MonoBehaviour
	{
		[Header("Fish Info")]
		[SerializeField]
		private TextMeshProUGUI _fishName;

		[SerializeField]
		private Image _fishImage;

		[Header("Locked Visual")]
		[SerializeField]
		private Color _unlockedFishColor = Color.white;

		[SerializeField]
		private Color _lockedFishColor = new Color(0f, 0f, 0f, 0.35f);

		[Header("Stats")]
		[SerializeField]
		private TextMeshProUGUI _totalAmount;

		[SerializeField]
		private TextMeshProUGUI _personalBestRecord;

		[Header("Leaderboard Runtime Spawn")]
		[SerializeField]
		private FishBookScoreBoardItem _scoreBoardItemPrefab;

		[SerializeField]
		private Transform _scoreBoardContainer;

		[SerializeField]
		private bool _clearExistingRowsOnBuild = true;

		private readonly List<FishBookScoreBoardItem> _scoreBoardItems = new List<FishBookScoreBoardItem>();

		private bool _scoreboardBuilt;

		public void Render(FishBookEntry fishData, FishSO fishSO, float personalBestKg)
		{
			EnsureScoreboardBuilt();
            if(_fishImage){_fishImage.material=null;_fishImage.useSpriteMesh=false;}
			if (fishSO == null)
			{
				RenderEmpty();
			}
			else if (!fishData.IsUnlocked)
			{
				RenderLocked(fishSO);
			}
			else
			{
				RenderUnlocked(fishData, fishSO, personalBestKg);
			}
            if(SushiAndFish.FishingIntegration.FishingMigration.Enabled)SushiAndFish.FishingIntegration.FishingTablet.Detail(this,fishSO,_fishImage,_fishName,_totalAmount,_lockedFishColor,_unlockedFishColor,fishData.IsUnlocked);
		}

		private void RenderUnlocked(FishBookEntry fishData, FishSO fishSO, float personalBestKg)
		{
			if (_fishName != null)
			{
				_fishName.text = fishSO.LocalizedName;
			}
			if (_fishImage != null)
			{
				_fishImage.sprite = fishSO.Sprite;
				_fishImage.color = _unlockedFishColor;
				_fishImage.enabled = fishSO.Sprite != null;
			}
			if (_totalAmount != null)
			{
				_totalAmount.text = fishData.TotalCaughtAmount.ToString();
			}
			if (_personalBestRecord != null)
			{
				_personalBestRecord.text = ((personalBestKg > 0f) ? $"{personalBestKg:F2} kg" : "N/A");
			}
			UpdateScoreboard(fishData);
		}

		private void RenderLocked(FishSO fishSO)
		{
			if (_fishName != null)
			{
				_fishName.text = TabletLocalization.Get("tablet.common.unknown", "Unknown");
			}
			if (_fishImage != null)
			{
				_fishImage.sprite = fishSO.Sprite;
				_fishImage.color = _lockedFishColor;
				_fishImage.enabled = fishSO.Sprite != null;
			}
			if (_totalAmount != null)
			{
				_totalAmount.text = "N/A";
			}
			if (_personalBestRecord != null)
			{
				_personalBestRecord.text = "N/A";
			}
			ClearScoreboard();
		}

		private void RenderEmpty()
		{
			if (_fishName != null)
			{
				_fishName.text = string.Empty;
			}
			if (_fishImage != null)
			{
				_fishImage.sprite = null;
				_fishImage.color = _unlockedFishColor;
				_fishImage.enabled = false;
			}
			if (_totalAmount != null)
			{
				_totalAmount.text = string.Empty;
			}
			if (_personalBestRecord != null)
			{
				_personalBestRecord.text = string.Empty;
			}
			ClearScoreboard();
		}

		private void EnsureScoreboardBuilt()
		{
			if (_scoreboardBuilt)
			{
				return;
			}
			_scoreboardBuilt = true;
			if (!(_scoreBoardItemPrefab == null) && !(_scoreBoardContainer == null))
			{
				if (_clearExistingRowsOnBuild)
				{
					ClearExistingContainerChildren();
				}
				_scoreBoardItems.Clear();
				for (int i = 0; i < 3; i++)
				{
					FishBookScoreBoardItem fishBookScoreBoardItem = Object.Instantiate(_scoreBoardItemPrefab, _scoreBoardContainer);
					fishBookScoreBoardItem.ClearData(i + 1);
					_scoreBoardItems.Add(fishBookScoreBoardItem);
				}
			}
		}

		private void ClearExistingContainerChildren()
		{
			if (_scoreBoardContainer == null)
			{
				return;
			}
			for (int num = _scoreBoardContainer.childCount - 1; num >= 0; num--)
			{
				Transform child = _scoreBoardContainer.GetChild(num);
				if (Application.isPlaying)
				{
					Object.Destroy(child.gameObject);
				}
				else
				{
					Object.DestroyImmediate(child.gameObject);
				}
			}
		}

		private void UpdateScoreboard(FishBookEntry fishData)
		{
			FishLeaderboardEntry[] array = new FishLeaderboardEntry[3] { fishData.Score1, fishData.Score2, fishData.Score3 };
			int num = Mathf.Min(_scoreBoardItems.Count, array.Length);
			for (int i = 0; i < num; i++)
			{
				if (_scoreBoardItems[i] != null)
				{
					_scoreBoardItems[i].UpdateData(i + 1, array[i]);
				}
			}
			for (int j = num; j < _scoreBoardItems.Count; j++)
			{
				if (_scoreBoardItems[j] != null)
				{
					_scoreBoardItems[j].ClearData(j + 1);
				}
			}
		}

		private void ClearScoreboard()
		{
			EnsureScoreboardBuilt();
			for (int i = 0; i < _scoreBoardItems.Count; i++)
			{
				if (_scoreBoardItems[i] != null)
				{
					_scoreBoardItems[i].ClearData(i + 1);
				}
			}
		}
	}
}
