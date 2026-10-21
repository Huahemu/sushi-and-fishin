using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using PaperPixelGames.Core.DailyReport;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Staff;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityServiceLocator;

namespace PaperPixelGames.Core.UI.DailyReportUI
{
	public sealed class DailyLogScreen : MonoBehaviour
	{
		[Header("Data")]
		[SerializeField]
		private ItemDatabaseSO _databaseSO;

		[SerializeField]
		private List<AwardSO> _awardList = new List<AwardSO>();

		[Header("Screen")]
		[SerializeField]
		private BaseButton _continueButton;

		[SerializeField]
		private GameObject _logRoot;

		[Header("Common Header")]
		[SerializeField]
		private TextMeshProUGUI _restaurantNameText;

		[SerializeField]
		private TextMeshProUGUI _dayText;

		[Header("Common Summary")]
		[SerializeField]
		private TextMeshProUGUI _totalEarningsText;

		[SerializeField]
		private TextMeshProUGUI _totalCustomersText;

		[SerializeField]
		private TextMeshProUGUI _totalFishCaughtText;

		[SerializeField]
		private TextMeshProUGUI _commonDeepestDiveText;

		[Header("Common Biggest Fish")]
		[SerializeField]
		private GameObject _commonBiggestFishRoot;

		[SerializeField]
		private GameObject _commonBiggestFishVisualRoot;

		[SerializeField]
		private Image _commonBiggestFishImage;

		[SerializeField]
		private TextMeshProUGUI _commonBiggestFishNameText;

		[SerializeField]
		private TextMeshProUGUI _commonBiggestFishWeightText;

		[SerializeField]
		private TextMeshProUGUI _commonBiggestFishOwnerText;

		[SerializeField]
		private GameObject _commonNoFishCaughtRoot;

		[Header("Common MVP")]
		[SerializeField]
		private GameObject _mvpRoot;

		[SerializeField]
		private Image _mvpImage;

		[SerializeField]
		private TextMeshProUGUI _mvpNameText;

		[SerializeField]
		private TextMeshProUGUI _mvpScoreText;

		[Header("Personal Identity")]
		[SerializeField]
		private TextMeshProUGUI _personalPlayerNameText;

		[SerializeField]
		private Image _personalPlayerImage;

		[Header("Personal Summary")]
		[SerializeField]
		private TextMeshProUGUI _personalEarningsText;

		[SerializeField]
		private TextMeshProUGUI _personalCustomersText;

		[SerializeField]
		private TextMeshProUGUI _personalSushiPreparedText;

		[SerializeField]
		private TextMeshProUGUI _personalFishCaughtText;

		[SerializeField]
		private TextMeshProUGUI _personalDeepestDiveText;

		[SerializeField]
		private TextMeshProUGUI _personalDrownCountText;

		[SerializeField]
		private TextMeshProUGUI _personalMvpScoreText;

		[Header("Personal Biggest Fish")]
		[SerializeField]
		private GameObject _personalBiggestFishRoot;

		[SerializeField]
		private Image _personalBiggestFishImage;

		[SerializeField]
		private TextMeshProUGUI _personalBiggestFishNameText;

		[SerializeField]
		private TextMeshProUGUI _personalBiggestFishWeightText;

		[SerializeField]
		private GameObject _personalNoFishCaughtRoot;

		[Header("Personal Awards")]
		[SerializeField]
		private Transform _personalAwardsRoot;

		[SerializeField]
		private DailyReportAwardItem _personalAwardItemPrefab;

		[SerializeField]
		private GameObject _noPersonalAwardsRoot;

		[SerializeField]
		private Transform _staffCardsRoot;

		[SerializeField]
		private TextMeshProUGUI _staffCardPrefab;

		[SerializeField]
		private StaffDailyReportPopup _staffReportPrefab;

		private StaffDailyReportPopup _staffReport;

		private bool _continueWasInteractable;

		private int _avatarLoadVersion;

		public bool StaffReportOpen
		{
			get
			{
				if (_staffReport != null)
				{
					return _staffReport.IsOpen;
				}
				return false;
			}
		}

		public void Initialize()
		{
			PPG_Player pPG_Player = ((Singleton<PlayersManager>.Instance != null) ? Singleton<PlayersManager>.Instance.LocalPlayer : null);
			if (pPG_Player == null)
			{
				UtilityMethods.Log("[DailyLogScreen] Initialize failed. Local player is unavailable.", LogType.Error);
				return;
			}
			_continueButton.Clicked -= HideLogRequest;
			if (pPG_Player.IsThisHost)
			{
				_continueButton.Clicked += HideLogRequest;
				_continueButton.gameObject.SetActive(value: true);
			}
			else
			{
				_continueButton.gameObject.SetActive(value: false);
			}
		}

		private void OnDestroy()
		{
			if (_continueButton != null)
			{
				_continueButton.Clicked -= HideLogRequest;
			}
			_avatarLoadVersion++;
		}

		public void BindReport(DailyLogReportPayload report)
		{
			if (report?.Common == null || report.Personal == null)
			{
				UtilityMethods.Log("[DailyLogScreen] Report binding failed. Common or personal payload is missing.", LogType.Error);
				return;
			}
			ResetUI();
			int bindVersion = ++_avatarLoadVersion;
			BindCommonReport(report.Common, bindVersion);
			BindPersonalReport(report.Personal, bindVersion);
			ShowLog();
			ShowStaffReport(report.Common);
		}

		private void ShowStaffReport(CommonReportPayload report)
		{
			StaffReportToggle[] componentsInChildren = _logRoot.GetComponentsInChildren<StaffReportToggle>(includeInactive: true);
			foreach (StaffReportToggle staffReportToggle in componentsInChildren)
			{
				if (staffReportToggle.Panel != null)
				{
					staffReportToggle.Panel.SetActive(value: false);
				}
				staffReportToggle.gameObject.SetActive(value: false);
			}
			if (_staffReport == null && _staffReportPrefab != null)
			{
				_staffReport = Object.Instantiate(_staffReportPrefab, _logRoot.transform);
			}
			if (!(_staffReport == null))
			{
				if (!StaffReportOpen)
				{
					_continueWasInteractable = _continueButton.interactable;
				}
				_continueButton.interactable = false;
				_staffReport.Show(report, OnStaffReportAcknowledged);
			}
		}

		private void OnStaffReportAcknowledged()
		{
			_continueButton.interactable = _continueWasInteractable;
			if (_continueButton.gameObject.activeInHierarchy && _continueButton.IsInteractable())
			{
				_continueButton.Select();
			}
		}

		public void HideLogRequest()
		{
			if (!StaffReportOpen)
			{
				ServiceLocator.ForSceneOf(this).Get<DailyReportBroadcaster>(out var service);
				if (service == null)
				{
					UtilityMethods.Log("[DailyLogScreen] Hide request failed. DailyReportBroadcaster is unavailable.", LogType.Error);
				}
				else
				{
					service.ServerHideLog();
				}
			}
		}

		public void HideLog()
		{
			if (StaffReportOpen)
			{
				_staffReport.Close();
				_continueButton.interactable = _continueWasInteractable;
			}
			_logRoot.SetActive(value: false);
			PPG_Player pPG_Player = ((Singleton<PlayersManager>.Instance != null) ? Singleton<PlayersManager>.Instance.LocalPlayer : null);
			if (!(pPG_Player == null))
			{
				pPG_Player.SwitchTo("Player", cursorVisible: false);
				if (Singleton<HintsManager>.Instance != null)
				{
					Singleton<HintsManager>.Instance.SetVisibilitySuppressed(suppressed: false);
				}
				pPG_Player.DivingEquipment?.SetUIVisibilitySuppressed(suppressed: false);
				_logRoot.SetActive(value: false);
				_avatarLoadVersion++;
			}
		}

		public void ShowLog()
		{
			PPG_Player pPG_Player = ((Singleton<PlayersManager>.Instance != null) ? Singleton<PlayersManager>.Instance.LocalPlayer : null);
			if (pPG_Player == null)
			{
				UtilityMethods.Log("[DailyLogScreen] Show failed. Local player is unavailable.", LogType.Error);
				return;
			}
			pPG_Player.SwitchTo("UI", cursorVisible: true);
			if (Singleton<HintsManager>.Instance != null)
			{
				Singleton<HintsManager>.Instance.SetVisibilitySuppressed(suppressed: true);
			}
			pPG_Player.DivingEquipment?.SetUIVisibilitySuppressed(suppressed: true);
			_logRoot.SetActive(value: true);
		}

		private void BindCommonReport(CommonReportPayload common, int bindVersion)
		{
			SetText(_restaurantNameText, common.RestaurantName);
			SetText(_dayText, $"Day {common.Day}");
			SetText(_totalEarningsText, FormatMoney(common.TotalEarned));
			SetText(_totalCustomersText, common.TotalCustomerCount.ToString());
			SetText(_totalFishCaughtText, common.TotalFishCaught.ToString());
			SetText(_commonDeepestDiveText, FormatDepth(common.DeepestDepthMeters));
			BindBiggestFish(common.BiggestCatch, _commonBiggestFishRoot, _commonBiggestFishVisualRoot, _commonBiggestFishImage, _commonBiggestFishNameText, _commonBiggestFishWeightText, _commonBiggestFishOwnerText, _commonNoFishCaughtRoot);
			BindMvp(common.MVP, bindVersion);
		}

		private void BindPersonalReport(PersonalReportPayload personal, int bindVersion)
		{
			SetText(_personalPlayerNameText, personal.PlayerName);
			SetText(_personalEarningsText, FormatMoney(personal.Earnings));
			SetText(_personalCustomersText, personal.CustomersServed.ToString());
			SetText(_personalSushiPreparedText, personal.SushiPrepared.ToString());
			SetText(_personalFishCaughtText, personal.FishCaught.ToString());
			SetText(_personalDeepestDiveText, FormatDepth(personal.DeepestDepthMeters));
			SetText(_personalDrownCountText, personal.DrownCount.ToString());
			SetText(_personalMvpScoreText, personal.MvpScore.ToString());
			BindBiggestFish(personal.BiggestCatch, _personalBiggestFishRoot, null, _personalBiggestFishImage, _personalBiggestFishNameText, _personalBiggestFishWeightText, null, _personalNoFishCaughtRoot);
			BindPersonalAwards(personal.AwardIds);
			LoadSteamAvatarAsync(personal.SteamId, _personalPlayerImage, bindVersion, "personal player").Forget();
		}

		private void BindMvp(MvpPayload mvp, int bindVersion)
		{
			bool flag = mvp != null && mvp.SteamId != 0;
			if (_mvpRoot != null)
			{
				_mvpRoot.SetActive(flag);
			}
			if (flag)
			{
				SetText(_mvpNameText, mvp.PlayerName);
				SetText(_mvpScoreText, mvp.Score.ToString());
				LoadSteamAvatarAsync(mvp.SteamId, _mvpImage, bindVersion, "MVP").Forget();
			}
		}

		private void BindBiggestFish(BiggestCatchPayload biggestCatch, GameObject root, GameObject visualRoot, Image fishImage, TextMeshProUGUI fishNameText, TextMeshProUGUI weightText, TextMeshProUGUI ownerText, GameObject noFishCaughtRoot)
		{
			bool flag = biggestCatch != null && !string.IsNullOrEmpty(biggestCatch.FishId);
			if (root != null)
			{
				root.SetActive(value: true);
			}
			SetActive(visualRoot, flag);
			SetActive((fishImage != null) ? fishImage.gameObject : null, flag);
			SetActive((fishNameText != null) ? fishNameText.gameObject : null, flag);
			SetActive((weightText != null) ? weightText.gameObject : null, flag);
			SetActive((ownerText != null) ? ownerText.gameObject : null, flag);
			SetActive(noFishCaughtRoot, !flag);
			ResetImage(fishImage);
			SetText(fishNameText, "-");
			SetText(weightText, "-");
			SetText(ownerText, "-");
			if (!flag)
			{
				return;
			}
			FishSO fishSO = FindFish(biggestCatch.FishId);
			if (fishSO == null)
			{
				if (root != null)
				{
					root.SetActive(value: false);
				}
				SetActive(noFishCaughtRoot, active: false);
				return;
			}
			SetText(fishNameText, fishSO.LocalizedName);
			SetText(weightText, $"Weight {biggestCatch.Kg:0.0} kg");
			if (ownerText != null)
			{
				SetText(ownerText, "Caught by " + biggestCatch.PlayerName);
			}
			if (fishImage != null)
			{
				fishImage.sprite = fishSO.Sprite;
				fishImage.enabled = fishSO.Sprite != null;
			}
		}

		private void BindPersonalAwards(List<ushort> awardIds)
		{
			ClearChildren(_personalAwardsRoot);
			int num = 0;
			if (awardIds != null && _personalAwardsRoot != null && _personalAwardItemPrefab != null)
			{
				for (int i = 0; i < awardIds.Count; i++)
				{
					AwardSO awardSO = FindAward(awardIds[i]);
					if (!(awardSO == null))
					{
						Object.Instantiate(_personalAwardItemPrefab, _personalAwardsRoot).InitializeBadge(awardSO);
						num++;
					}
				}
			}
			if (_noPersonalAwardsRoot != null)
			{
				_noPersonalAwardsRoot.SetActive(num == 0);
			}
		}

		private async UniTaskVoid LoadSteamAvatarAsync(ulong steamId, Image target, int bindVersion, string context)
		{
			if (target == null || steamId == 0L)
			{
				return;
			}
			Sprite sprite = await SteamAvatarCache.GetAvatarSpriteAsync(steamId);
			if (!(this == null) && !(target == null) && bindVersion == _avatarLoadVersion)
			{
				if (sprite == null)
				{
					UtilityMethods.Log($"[DailyLogScreen] Steam avatar load returned no sprite. Context={context}, SteamId={steamId}", LogType.Warning);
					return;
				}
				target.sprite = sprite;
				target.enabled = true;
				target.gameObject.SetActive(value: true);
			}
		}

		private void ResetUI()
		{
			_avatarLoadVersion++;
			SetText(_restaurantNameText, "-");
			SetText(_dayText, "-");
			SetText(_totalEarningsText, "$0");
			SetText(_totalCustomersText, "0");
			SetText(_totalFishCaughtText, "0");
			SetText(_commonDeepestDiveText, "0 m");
			ResetBiggestFish(_commonBiggestFishRoot, _commonBiggestFishVisualRoot, _commonBiggestFishImage, _commonBiggestFishNameText, _commonBiggestFishWeightText, _commonBiggestFishOwnerText, _commonNoFishCaughtRoot);
			if (_mvpRoot != null)
			{
				_mvpRoot.SetActive(value: false);
			}
			ResetImage(_mvpImage);
			SetText(_mvpNameText, "-");
			SetText(_mvpScoreText, "0");
			SetText(_personalPlayerNameText, "-");
			ResetImage(_personalPlayerImage);
			SetText(_personalEarningsText, "$0");
			SetText(_personalCustomersText, "0");
			SetText(_personalSushiPreparedText, "0");
			SetText(_personalFishCaughtText, "0");
			SetText(_personalDeepestDiveText, "0 m");
			SetText(_personalDrownCountText, "0");
			SetText(_personalMvpScoreText, "0");
			ResetBiggestFish(_personalBiggestFishRoot, null, _personalBiggestFishImage, _personalBiggestFishNameText, _personalBiggestFishWeightText, null, _personalNoFishCaughtRoot);
			ClearChildren(_personalAwardsRoot);
			if (_noPersonalAwardsRoot != null)
			{
				_noPersonalAwardsRoot.SetActive(value: true);
			}
		}

		private static void ResetBiggestFish(GameObject root, GameObject visualRoot, Image image, TextMeshProUGUI nameText, TextMeshProUGUI weightText, TextMeshProUGUI ownerText, GameObject noFishCaughtRoot)
		{
			if (root != null)
			{
				root.SetActive(value: true);
			}
			SetActive(visualRoot, active: false);
			SetActive((image != null) ? image.gameObject : null, active: false);
			SetActive((nameText != null) ? nameText.gameObject : null, active: false);
			SetActive((weightText != null) ? weightText.gameObject : null, active: false);
			SetActive((ownerText != null) ? ownerText.gameObject : null, active: false);
			SetActive(noFishCaughtRoot, active: true);
			ResetImage(image);
			SetText(nameText, "-");
			SetText(weightText, "-");
			SetText(ownerText, "-");
		}

		private static void SetActive(GameObject target, bool active)
		{
			if (target != null)
			{
				target.SetActive(active);
			}
		}

		private FishSO FindFish(string fishId)
		{
			if (_databaseSO == null)
			{
				UtilityMethods.Log("[DailyLogScreen] Fish lookup failed. Item database is missing. FishId=" + fishId, LogType.Error);
				return null;
			}
			FishSO fishSO = _databaseSO.FindById(fishId) as FishSO;
			if (fishSO != null)
			{
				return fishSO;
			}
			UtilityMethods.Log("[DailyLogScreen] Fish lookup failed. FishId=" + fishId, LogType.Warning);
			return null;
		}

		private AwardSO FindAward(ushort awardId)
		{
			if (_awardList == null)
			{
				return null;
			}
			for (int i = 0; i < _awardList.Count; i++)
			{
				AwardSO awardSO = _awardList[i];
				if (awardSO != null && (uint)awardSO.Id == awardId)
				{
					return awardSO;
				}
			}
			UtilityMethods.Log($"[DailyLogScreen] Award lookup failed. AwardId={awardId}", LogType.Warning);
			return null;
		}

		private static void ClearChildren(Transform root)
		{
			if (!(root == null))
			{
				for (int num = root.childCount - 1; num >= 0; num--)
				{
					Object.Destroy(root.GetChild(num).gameObject);
				}
			}
		}

		private static void ResetImage(Image image)
		{
			if (!(image == null))
			{
				image.sprite = null;
				image.enabled = false;
			}
		}

		private static void SetText(TextMeshProUGUI target, string value)
		{
			if (target != null)
			{
				target.text = (string.IsNullOrWhiteSpace(value) ? "-" : value);
			}
		}

		private static string FormatMoney(int amount)
		{
			return $"${Mathf.Max(0, amount):n0}";
		}

		private static string FormatDepth(float depthMeters)
		{
			return $"{Mathf.Max(0f, depthMeters):0.#} m";
		}
	}
}
