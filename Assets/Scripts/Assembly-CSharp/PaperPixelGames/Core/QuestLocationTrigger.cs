using System.Collections.Generic;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Quests;
using UnityEngine;

namespace PaperPixelGames.Core
{
	public class QuestLocationTrigger : MonoBehaviour
	{
		[Header("Quest")]
		[SerializeField]
		private QuestSO _quest;

		[Header("Progress")]
		[SerializeField]
		private bool _addProgress;

		[SerializeField]
		[Min(1f)]
		private int _progressAmount = 1;

		[Header("Options")]
		[SerializeField]
		private bool _disableAfterSuccess = true;

		private readonly HashSet<int> _playerColliderIds = new HashSet<int>();

		private QuestManager _subscribedManager;

		private bool _requestIssuedForCurrentOccupancy;

		private bool _completed;

		private void OnEnable()
		{
			QuestManager manager = EnsureQuestManagerSubscription();
			TryFinalizeCompletion(manager);
		}

		private void Start()
		{
			EnsureQuestManagerSubscription();
			TryApplyQuestProgress();
		}

		private void OnDisable()
		{
			UnsubscribeFromQuestManager();
			_playerColliderIds.Clear();
			_requestIssuedForCurrentOccupancy = false;
		}

		private void OnTriggerEnter(Collider other)
		{
			if (!_completed && TryGetEligiblePlayer(other))
			{
				_playerColliderIds.Add(other.GetInstanceID());
				TryApplyQuestProgress();
			}
		}

		private void OnTriggerExit(Collider other)
		{
			if (TryGetEligiblePlayer(other))
			{
				_playerColliderIds.Remove(other.GetInstanceID());
				if (_playerColliderIds.Count == 0)
				{
					_requestIssuedForCurrentOccupancy = false;
				}
			}
		}

		private void TryApplyQuestProgress()
		{
			if (_completed || _requestIssuedForCurrentOccupancy || _playerColliderIds.Count == 0)
			{
				return;
			}
			if (_quest == null)
			{
				Debug.LogWarning("[QuestLocationTrigger] Quest is null.", this);
				return;
			}
			QuestManager questManager = EnsureQuestManagerSubscription();
			if (questManager == null)
			{
				Debug.LogWarning("[QuestLocationTrigger] QuestManager is null.", this);
			}
			else if (!TryFinalizeCompletion(questManager) && (questManager.IsQuestActive(_quest) || _quest.CanCompleteBeforeActive))
			{
				_requestIssuedForCurrentOccupancy = true;
				if (!(_addProgress ? questManager.RequestAddQuestProgress(_quest, _progressAmount) : questManager.RequestCompleteQuest(_quest)))
				{
					_requestIssuedForCurrentOccupancy = false;
				}
				else
				{
					TryFinalizeCompletion(questManager);
				}
			}
		}

		private void OnQuestStateChanged()
		{
			QuestManager manager = EnsureQuestManagerSubscription();
			if (!TryFinalizeCompletion(manager))
			{
				TryApplyQuestProgress();
			}
		}

		private bool TryFinalizeCompletion(QuestManager manager)
		{
			if (_completed)
			{
				return true;
			}
			if (manager == null || _quest == null || !manager.IsQuestCompleted(_quest))
			{
				return false;
			}
			_completed = true;
			if (_disableAfterSuccess)
			{
				base.gameObject.SetActive(value: false);
			}
			return true;
		}

		private QuestManager EnsureQuestManagerSubscription()
		{
			QuestManager instance = QuestManager.Instance;
			if (_subscribedManager == instance)
			{
				return instance;
			}
			UnsubscribeFromQuestManager();
			_subscribedManager = instance;
			_requestIssuedForCurrentOccupancy = false;
			if (_subscribedManager != null)
			{
				_subscribedManager.QuestStateChanged += OnQuestStateChanged;
			}
			return instance;
		}

		private void UnsubscribeFromQuestManager()
		{
			if (!(_subscribedManager == null))
			{
				_subscribedManager.QuestStateChanged -= OnQuestStateChanged;
				_subscribedManager = null;
			}
		}

		private static bool TryGetEligiblePlayer(Collider other)
		{
			if (other == null || !other.CompareTag("Player"))
			{
				return false;
			}
			PPG_Player componentInParent = other.GetComponentInParent<PPG_Player>();
			if (componentInParent != null)
			{
				if (componentInParent.IsSpawned)
				{
					return componentInParent.IsOwner;
				}
				return true;
			}
			return false;
		}
	}
}
