using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Staff;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace PaperPixelGames.Core.Tablet
{
	public class StaffApp : BaseApp
	{
		[SerializeField]
		private StaffAppView _view;

		private StaffManager _staffManager;

		private int _pendingFireEmployeeId = -1;

		private bool _initialized;

		private readonly Dictionary<int, StaffCandidateCardView> candidateCards = new Dictionary<int, StaffCandidateCardView>();

		private readonly Dictionary<int, StaffTeamCardView> teamCards = new Dictionary<int, StaffTeamCardView>();

		private readonly Dictionary<StaffRole, StaffRoleLimitView> roleViews = new Dictionary<StaffRole, StaffRoleLimitView>();

		public override void Initialize()
		{
			if (!_initialized)
			{
				_initialized = true;
				_view.Initialize(OnRefreshRequested, OnFireConfirmed, OnKitchenShelfProtectionChanged, OnStationStorageCollectionChanged);
				StaffManager.InstanceAvailable -= OnStaffManagerAvailable;
				StaffManager.InstanceAvailable += OnStaffManagerAvailable;
				LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
				LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
				BindStaffManager(StaffManager.Instance);
			}
		}

		public override void Show()
		{
			base.Show();
			BindStaffManager(StaffManager.Instance);
			RefreshView();
		}

		private void OnDestroy()
		{
			StaffManager.InstanceAvailable -= OnStaffManagerAvailable;
			LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
			if (_staffManager != null)
			{
				_staffManager.StaffChanged -= RefreshView;
			}
			_staffManager = null;
		}

		private void OnStaffManagerAvailable(StaffManager manager)
		{
			BindStaffManager(manager);
		}

		private void OnSelectedLocaleChanged(Locale locale)
		{
			RefreshView();
		}

		private void BindStaffManager(StaffManager manager)
		{
			if (!(_staffManager == manager))
			{
				if (_staffManager != null)
				{
					_staffManager.StaffChanged -= RefreshView;
				}
				_staffManager = manager;
				if (_staffManager != null)
				{
					_staffManager.StaffChanged += RefreshView;
				}
				RefreshView();
			}
		}

		private void RefreshView()
		{
			if (!_initialized || _view == null || !base.isActiveAndEnabled)
			{
				return;
			}
			if (_staffManager == null || !_staffManager.IsSpawned)
			{
				_view.ClearRoleLimits();
				_view.ClearCandidateCards();
				_view.ClearTeamCards();
				candidateCards.Clear();
				teamCards.Clear();
				roleViews.Clear();
				_view.SetCandidateHeader(0);
				_view.SetTeamHeader(0, 0);
				_view.SetKitchenShelfProtection(hasStocker: false, enabled: true);
				_view.SetStationStorageCollection(hasStocker: false, enabled: false);
				_pendingFireEmployeeId = -1;
				_view.HideFireConfirmation();
				return;
			}
			if (_view.RoleLimitsContent != null && _view.RoleLimitPrefab != null)
			{
				foreach (StaffRole item in StaffAppView.DisplayRoleOrder)
				{
					StaffRoleDefinition roleDefinition = _staffManager.GetRoleDefinition(item);
					if (!roleViews.TryGetValue(item, out var value) || value == null)
					{
						value = (roleViews[item] = Object.Instantiate(_view.RoleLimitPrefab, _view.RoleLimitsContent));
					}
					value.transform.SetSiblingIndex(StaffAppView.RoleDisplayIndex(item));
					value.Bind(roleDefinition, _staffManager.GetHiredCount(item), _staffManager.GetRoleLimit(item));
				}
			}
			StaffEmployeeEntry[] array = (from e in Enumerable.Range(0, _staffManager.CandidateCount).Select(_staffManager.GetCandidate)
				orderby StaffAppView.RoleDisplayIndex(e.Role), e.EmployeeId
				select e).ToArray();
			StaffEmployeeEntry[] array2 = (from e in Enumerable.Range(0, _staffManager.TeamCount).Select(_staffManager.GetTeamMember)
				orderby StaffAppView.RoleDisplayIndex(e.Role), e.HireSequence, e.EmployeeId
				select e).ToArray();
			HashSet<int> activeCandidates = new HashSet<int>(array.Select((StaffEmployeeEntry e) => e.EmployeeId));
			HashSet<int> activeTeam = new HashSet<int>(array2.Select((StaffEmployeeEntry e) => e.EmployeeId));
			int[] array3 = candidateCards.Keys.Where((int id) => !activeCandidates.Contains(id)).ToArray();
			foreach (int key in array3)
			{
				RemoveCard(candidateCards[key]);
				candidateCards.Remove(key);
			}
			array3 = teamCards.Keys.Where((int id) => !activeTeam.Contains(id)).ToArray();
			foreach (int key2 in array3)
			{
				RemoveCard(teamCards[key2]);
				teamCards.Remove(key2);
			}
			Dictionary<Transform, int> dictionary = new Dictionary<Transform, int>();
			StaffEmployeeEntry[] array4 = array;
			for (int num = 0; num < array4.Length; num++)
			{
				StaffEmployeeEntry employee = array4[num];
				Transform parent = _view.CandidateContentFor(employee.Role);
				if (!candidateCards.TryGetValue(employee.EmployeeId, out var value2) || value2 == null)
				{
					value2 = (candidateCards[employee.EmployeeId] = Object.Instantiate(_view.CandidateCardPrefab, parent));
				}
				PlaceCard(value2.transform, parent, dictionary);
				value2.Bind(employee, _staffManager.GetRoleDefinition(employee.Role), _staffManager.IsRoleFull(employee.Role), OnHireRequested, _staffManager.HiringLevel);
			}
			dictionary.Clear();
			array4 = array2;
			for (int num = 0; num < array4.Length; num++)
			{
				StaffEmployeeEntry employee2 = array4[num];
				Transform parent2 = _view.TeamContentFor(employee2.Role);
				if (!teamCards.TryGetValue(employee2.EmployeeId, out var value3) || value3 == null)
				{
					value3 = (teamCards[employee2.EmployeeId] = Object.Instantiate(_view.TeamCardPrefab, parent2));
				}
				PlaceCard(value3.transform, parent2, dictionary);
				value3.Bind(employee2, _staffManager.GetRoleDefinition(employee2.Role), OnFireRequested, OnStockerSettingChanged);
			}
			_view.SetCandidateHeader(_staffManager.CandidateCount);
			_view.SetTeamHeader(_staffManager.TeamCount, _staffManager.GetDailyPayrollDollars());
			_view.SetKitchenShelfProtection(_staffManager.HasActiveStocker, _staffManager.ProtectKitchenShelf);
			_view.SetStationStorageCollection(_staffManager.HasActiveStocker, _staffManager.CollectStationStorage);
			RefreshFireConfirmation();
		}

		private static void RemoveCard(Component card)
		{
			if (!(card == null))
			{
				card.gameObject.SetActive(value: false);
				card.transform.SetParent(null, worldPositionStays: false);
				Object.Destroy(card.gameObject);
			}
		}

		private static void PlaceCard(Transform card, Transform parent, Dictionary<Transform, int> nextPositions)
		{
			if (!(parent == null))
			{
				if (card.parent != parent)
				{
					card.SetParent(parent, worldPositionStays: false);
				}
				nextPositions.TryGetValue(parent, out var value);
				card.SetSiblingIndex(value);
				nextPositions[parent] = value + 1;
			}
		}

		private void OnRefreshRequested()
		{
			_staffManager?.RequestRefreshCandidates();
		}

		private void OnHireRequested(int employeeId)
		{
			_staffManager?.RequestHire(employeeId);
		}

		private void OnFireRequested(int employeeId)
		{
			if (_staffManager == null)
			{
				return;
			}
			for (int i = 0; i < _staffManager.TeamCount; i++)
			{
				StaffEmployeeEntry teamMember = _staffManager.GetTeamMember(i);
				if (teamMember.EmployeeId == employeeId)
				{
					_pendingFireEmployeeId = employeeId;
					_view.ShowFireConfirmation(StaffNames.DisplayName(teamMember));
					break;
				}
			}
		}

		private void RefreshFireConfirmation()
		{
			if (_pendingFireEmployeeId >= 0 && _view.IsFireConfirmationOpen)
			{
				if (_staffManager != null && _staffManager.TryGetEmployee(_pendingFireEmployeeId, out var employee))
				{
					_view.ShowFireConfirmation(StaffNames.DisplayName(employee));
					return;
				}
				_pendingFireEmployeeId = -1;
				_view.HideFireConfirmation();
			}
		}

		private void OnFireConfirmed()
		{
			if (_pendingFireEmployeeId >= 0)
			{
				_staffManager?.RequestFire(_pendingFireEmployeeId);
				_pendingFireEmployeeId = -1;
			}
		}

		private void OnStockerSettingChanged(int employeeId, StockerSetting setting, bool enabled)
		{
			_staffManager?.RequestStockerSetting(employeeId, setting, enabled);
		}

		private void OnKitchenShelfProtectionChanged(bool enabled)
		{
			_staffManager?.RequestKitchenShelfProtection(enabled);
		}

		private void OnStationStorageCollectionChanged(bool enabled)
		{
			_staffManager?.RequestStationStorageCollection(enabled);
		}
	}
}
