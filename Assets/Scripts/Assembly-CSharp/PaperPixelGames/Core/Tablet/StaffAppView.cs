using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Staff;
using PaperPixelGames.Framework.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Tablet
{
	public sealed class StaffAppView : MonoBehaviour
	{
		[Header("Tabs")]
		[SerializeField]
		private BaseButton _candidatesTabButton;

		[SerializeField]
		private BaseButton _teamTabButton;

		[SerializeField]
		private Image _candidatesTabImage;

		[SerializeField]
		private Image _teamTabImage;

		[SerializeField]
		private GameObject _candidatesPage;

		[SerializeField]
		private GameObject _teamPage;

		[SerializeField]
		private Color _selectedTabColor = new Color32(207, 77, 38, byte.MaxValue);

		[SerializeField]
		private Color _inactiveTabColor = new Color32(249, 235, 200, byte.MaxValue);

		[Header("Headers")]
		[SerializeField]
		private TextMeshProUGUI _candidateHeaderText;

		[SerializeField]
		private TextMeshProUGUI _teamHeaderText;

		[SerializeField]
		private BaseButton _refreshAllButton;

		[Header("Shared Stocker Settings")]
		[SerializeField]
		private GameObject _kitchenShelfSetting;

		[SerializeField]
		private Toggle _kitchenShelfToggle;

		[SerializeField]
		private TextMeshProUGUI _kitchenShelfLabel;

		[SerializeField]
		private TextMeshProUGUI _kitchenShelfDescription;

		[SerializeField]
		private TextMeshProUGUI _kitchenShelfScope;

		[SerializeField]
		private GameObject _stationStorageSetting;

		[SerializeField]
		private Toggle _stationStorageToggle;

		[SerializeField]
		private TextMeshProUGUI _stationStorageLabel;

		[SerializeField]
		private TextMeshProUGUI _stationStorageDescription;

		[SerializeField]
		private TextMeshProUGUI _stationStorageScope;

		private Action<bool> _kitchenShelfProtectionChanged;

		private Action<bool> _stationStorageCollectionChanged;

		private bool _hasStocker;

		private bool _layoutCached;

		private Vector2 _originalTeamOffset;

		private Vector2 _originalRolePosition;

		[Header("Runtime Content")]
		[SerializeField]
		private Transform _roleLimitsContent;

		[SerializeField]
		private Transform _candidateContent;

		[SerializeField]
		private Transform _teamContent;

		[Tooltip("Fixed candidate slots, left to right: Chef, Prep Cook, Waitress, Stocker. Each slot contains cards only.")]
		[SerializeField]
		private Transform[] _candidateRoleContents = new Transform[4];

		[Tooltip("Fixed team columns, left to right: Chef, Prep Cook, Waitress, Stocker. Each column contains cards only.")]
		[SerializeField]
		private Transform[] _teamRoleContents = new Transform[4];

		[SerializeField]
		private StaffRoleLimitView _roleLimitPrefab;

		[SerializeField]
		private StaffCandidateCardView _candidateCardPrefab;

		[SerializeField]
		private StaffTeamCardView _teamCardPrefab;

		[Header("Fire Confirmation")]
		[SerializeField]
		private GameObject _fireConfirmationModal;

		[SerializeField]
		private TextMeshProUGUI _fireConfirmationMessageText;

		[SerializeField]
		private BaseButton _cancelFireButton;

		[SerializeField]
		private BaseButton _confirmFireButton;

		private Action _refreshRequested;

		private Action _fireConfirmed;

		public Transform RoleLimitsContent => _roleLimitsContent;

		public Transform CandidateContent => _candidateContent;

		public Transform TeamContent => _teamContent;

		public StaffRoleLimitView RoleLimitPrefab => _roleLimitPrefab;

		public StaffCandidateCardView CandidateCardPrefab => _candidateCardPrefab;

		public StaffTeamCardView TeamCardPrefab => _teamCardPrefab;

		public bool IsFireConfirmationOpen
		{
			get
			{
				if (_fireConfirmationModal != null)
				{
					return _fireConfirmationModal.activeSelf;
				}
				return false;
			}
		}

		public static IReadOnlyList<StaffRole> DisplayRoleOrder { get; } = Array.AsReadOnly(new StaffRole[4]
		{
			StaffRole.SushiChef,
			StaffRole.PrepCook,
			StaffRole.Waitress,
			StaffRole.Stocker
		});

		public static int RoleDisplayIndex(StaffRole role)
		{
			return role switch
			{
				StaffRole.SushiChef => 0, 
				StaffRole.PrepCook => 1, 
				StaffRole.Waitress => 2, 
				StaffRole.Stocker => 3, 
				_ => 4, 
			};
		}

		public Transform CandidateContentFor(StaffRole role)
		{
			return ContentFor(_candidateRoleContents, role, _candidateContent);
		}

		public Transform TeamContentFor(StaffRole role)
		{
			return ContentFor(_teamRoleContents, role, _teamContent);
		}

		private static Transform ContentFor(Transform[] contents, StaffRole role, Transform fallback)
		{
			int num = RoleDisplayIndex(role);
			if (contents == null || num >= contents.Length || !(contents[num] != null))
			{
				return fallback;
			}
			return contents[num];
		}

		public void Initialize(Action refreshRequested, Action fireConfirmed, Action<bool> kitchenShelfProtectionChanged = null, Action<bool> stationStorageCollectionChanged = null)
		{
			_kitchenShelfProtectionChanged = kitchenShelfProtectionChanged;
			_stationStorageCollectionChanged = stationStorageCollectionChanged;
			if (!_layoutCached)
			{
				_originalTeamOffset = ((RectTransform)_teamContent).offsetMax;
				_originalRolePosition = ((RectTransform)_roleLimitsContent).anchoredPosition;
				_layoutCached = true;
			}
			if (_kitchenShelfToggle != null)
			{
				_kitchenShelfToggle.onValueChanged.RemoveListener(OnKitchenShelfProtectionChanged);
				_kitchenShelfToggle.onValueChanged.AddListener(OnKitchenShelfProtectionChanged);
			}
			if (_stationStorageToggle != null)
			{
				_stationStorageToggle.onValueChanged.RemoveListener(OnStationStorageCollectionChanged);
				_stationStorageToggle.onValueChanged.AddListener(OnStationStorageCollectionChanged);
			}
			_refreshRequested = refreshRequested;
			_fireConfirmed = fireConfirmed;
			BindButton(_candidatesTabButton, ShowCandidates);
			BindButton(_teamTabButton, ShowTeam);
			BindButton(_refreshAllButton, OnRefreshClicked);
			BindButton(_cancelFireButton, HideFireConfirmation);
			BindButton(_confirmFireButton, OnConfirmFireClicked);
			ClearPreviewContent();
			ShowCandidates();
			HideFireConfirmation();
		}

		public void SetCandidateHeader(int candidateCount)
		{
			TextMeshProUGUI componentInChildren = _refreshAllButton.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
			if (componentInChildren != null)
			{
				componentInChildren.text = StaffText.Get("refresh");
			}
			if (!(_candidateHeaderText == null))
			{
				_candidateHeaderText.text = TabletLocalization.Format("tablet.staff.candidate_header", "{0} CANDIDATES AVAILABLE\n<size=65%>One candidate from each role</size>", candidateCount);
			}
		}

		public void SetTeamHeader(int teamCount, int payrollDollars)
		{
			if (!(_teamHeaderText == null))
			{
				_teamHeaderText.text = TabletLocalization.Format("tablet.staff.team_header", "{0} TEAM MEMBERS\n<size=65%>Daily Payroll: ${1:N0}   |   Paid at 21:00</size>", teamCount, payrollDollars);
			}
		}

		public void SetKitchenShelfProtection(bool hasStocker, bool enabled)
		{
			_hasStocker = hasStocker;
			if (_kitchenShelfSetting != null)
			{
				_kitchenShelfSetting.SetActive(hasStocker);
				_kitchenShelfToggle.SetIsOnWithoutNotify(enabled);
				_kitchenShelfLabel.text = StaffText.Get("protect_kitchen_shelf");
				_kitchenShelfDescription.text = StaffText.Get("protect_kitchen_shelf_description");
				_kitchenShelfScope.text = StaffText.Get("all_stockers");
			}
			UpdateSharedSettingLayout();
		}

		public void SetStationStorageCollection(bool hasStocker, bool enabled)
		{
			_hasStocker = hasStocker;
			if (_stationStorageSetting != null)
			{
				_stationStorageSetting.SetActive(hasStocker);
				_stationStorageToggle.SetIsOnWithoutNotify(enabled);
				_stationStorageLabel.text = StaffText.Get("collect_station_storage");
				_stationStorageDescription.text = StaffText.Get("collect_station_storage_description");
				_stationStorageScope.text = StaffText.Get("all_stockers");
			}
			UpdateSharedSettingLayout();
		}

		private void UpdateSharedSettingLayout()
		{
			if (_layoutCached)
			{
				float space = 0f;
				if (_hasStocker)
				{
					PositionSharedSetting(_kitchenShelfSetting, ref space);
					PositionSharedSetting(_stationStorageSetting, ref space);
				}
				((RectTransform)_teamContent).offsetMax = _originalTeamOffset - new Vector2(0f, space);
				((RectTransform)_roleLimitsContent).anchoredPosition = _originalRolePosition - new Vector2(0f, _teamPage.activeSelf ? space : 0f);
			}
		}

		private static void PositionSharedSetting(GameObject setting, ref float space)
		{
			if (!(setting == null) && setting.activeSelf)
			{
				RectTransform obj = (RectTransform)setting.transform;
				obj.anchoredPosition = new Vector2(obj.anchoredPosition.x, -26f - space);
				space += 16f;
			}
		}

		private void OnKitchenShelfProtectionChanged(bool enabled)
		{
			_kitchenShelfProtectionChanged?.Invoke(enabled);
		}

		private void OnStationStorageCollectionChanged(bool enabled)
		{
			_stationStorageCollectionChanged?.Invoke(enabled);
		}

		public void ShowFireConfirmation(string employeeName)
		{
			_fireConfirmationMessageText.text = TabletLocalization.Format("tablet.staff.v1.fire_confirmation", StaffText.Fallback("fire_confirmation"), employeeName);
			_fireConfirmationModal.SetActive(value: true);
		}

		public void HideFireConfirmation()
		{
			_fireConfirmationModal.SetActive(value: false);
		}

		public void ClearRoleLimits()
		{
			ClearContainer(_roleLimitsContent);
		}

		public void ClearCandidateCards()
		{
			ClearCards<StaffCandidateCardView>(_candidateContent);
		}

		public void ClearTeamCards()
		{
			ClearCards<StaffTeamCardView>(_teamContent);
		}

		private void OnDestroy()
		{
			if (_kitchenShelfToggle != null)
			{
				_kitchenShelfToggle.onValueChanged.RemoveListener(OnKitchenShelfProtectionChanged);
			}
			if (_stationStorageToggle != null)
			{
				_stationStorageToggle.onValueChanged.RemoveListener(OnStationStorageCollectionChanged);
			}
			UnbindButton(_candidatesTabButton, ShowCandidates);
			UnbindButton(_teamTabButton, ShowTeam);
			UnbindButton(_refreshAllButton, OnRefreshClicked);
			UnbindButton(_cancelFireButton, HideFireConfirmation);
			UnbindButton(_confirmFireButton, OnConfirmFireClicked);
		}

		private void ShowCandidates()
		{
			_candidatesPage.SetActive(value: true);
			_teamPage.SetActive(value: false);
			_candidatesTabImage.color = _selectedTabColor;
			_teamTabImage.color = _inactiveTabColor;
			UpdateSharedSettingLayout();
		}

		private void ShowTeam()
		{
			_candidatesPage.SetActive(value: false);
			_teamPage.SetActive(value: true);
			_candidatesTabImage.color = _inactiveTabColor;
			_teamTabImage.color = _selectedTabColor;
			UpdateSharedSettingLayout();
		}

		private void OnRefreshClicked()
		{
			_refreshRequested?.Invoke();
		}

		private void OnConfirmFireClicked()
		{
			_fireConfirmed?.Invoke();
			HideFireConfirmation();
		}

		private void ClearPreviewContent()
		{
			ClearRoleLimits();
			ClearCandidateCards();
			ClearTeamCards();
		}

		private static void ClearCards<T>(Transform container) where T : Component
		{
			if (!(container == null))
			{
				T[] componentsInChildren = container.GetComponentsInChildren<T>(includeInactive: true);
				foreach (T val in componentsInChildren)
				{
					val.gameObject.SetActive(value: false);
					val.transform.SetParent(null, worldPositionStays: false);
					UnityEngine.Object.Destroy(val.gameObject);
				}
			}
		}

		private static void ClearContainer(Transform container)
		{
			if (!(container == null))
			{
				for (int num = container.childCount - 1; num >= 0; num--)
				{
					GameObject obj = container.GetChild(num).gameObject;
					obj.SetActive(value: false);
					UnityEngine.Object.Destroy(obj);
				}
			}
		}

		private static void BindButton(BaseButton button, Action callback)
		{
			if (!(button == null))
			{
				button.Clicked -= callback;
				button.Clicked += callback;
			}
		}

		private static void UnbindButton(BaseButton button, Action callback)
		{
			if (button != null)
			{
				button.Clicked -= callback;
			}
		}
	}
}
