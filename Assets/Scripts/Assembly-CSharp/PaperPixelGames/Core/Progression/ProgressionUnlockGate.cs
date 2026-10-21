using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;

namespace PaperPixelGames.Core.Progression
{
	public class ProgressionUnlockGate : MonoBehaviour
	{
		[SerializeField]
		private ProgressionUnlockGateMode _mode;

		[SerializeField]
		private ProgressionUnlockKey _unlockKey;

		[SerializeField]
		private int _objectId;

		[SerializeField]
		private GameObject _lockedRoot;

		[SerializeField]
		private GameObject _unlockedRoot;

		[SerializeField]
		private Behaviour[] _enabledWhenUnlocked;

		[SerializeField]
		private Collider[] _collidersEnabledWhenUnlocked;

		[SerializeField]
		private PhysicalStateController _physicalStateController;

		[SerializeField]
		private bool _setPhysicalStateOnServer;

		private ProgressionManager _progressionManager;

		private void OnEnable()
		{
			TryBind();
			Refresh();
		}

		private void OnDisable()
		{
			if (_progressionManager != null)
			{
				_progressionManager.UnlockGranted -= OnUnlockGranted;
				_progressionManager.ProgressionChanged -= OnProgressionChanged;
			}
		}

		public void Refresh()
		{
			TryBind();
			bool flag = IsUnlocked();
			if (_lockedRoot != null)
			{
				_lockedRoot.SetActive(!flag);
			}
			if (_unlockedRoot != null)
			{
				_unlockedRoot.SetActive(flag);
			}
			if (_enabledWhenUnlocked != null)
			{
				Behaviour[] enabledWhenUnlocked = _enabledWhenUnlocked;
				foreach (Behaviour behaviour in enabledWhenUnlocked)
				{
					if (behaviour != null)
					{
						behaviour.enabled = flag;
					}
				}
			}
			if (_collidersEnabledWhenUnlocked != null)
			{
				Collider[] collidersEnabledWhenUnlocked = _collidersEnabledWhenUnlocked;
				foreach (Collider collider in collidersEnabledWhenUnlocked)
				{
					if (collider != null)
					{
						collider.enabled = flag;
					}
				}
			}
			if (_setPhysicalStateOnServer && _mode != ProgressionUnlockGateMode.DiningTable && _physicalStateController != null && _physicalStateController.IsServer)
			{
				_physicalStateController.ServerSetUsable(flag);
			}
		}

		private void TryBind()
		{
			ProgressionManager progressionManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.ProgressionManager : null);
			if (!(_progressionManager == progressionManager))
			{
				if (_progressionManager != null)
				{
					_progressionManager.UnlockGranted -= OnUnlockGranted;
					_progressionManager.ProgressionChanged -= OnProgressionChanged;
				}
				_progressionManager = progressionManager;
				if (_progressionManager != null)
				{
					_progressionManager.UnlockGranted += OnUnlockGranted;
					_progressionManager.ProgressionChanged += OnProgressionChanged;
				}
			}
		}

		private void OnUnlockGranted(ProgressionUnlockKey key)
		{
			if (key == _unlockKey)
			{
				Refresh();
			}
		}

		private void OnProgressionChanged(int level, double currentXp, double nextLevelRequiredTotalXp)
		{
			Refresh();
		}

		private bool IsUnlocked()
		{
			if (_progressionManager == null)
			{
				return false;
			}
			return _mode switch
			{
				ProgressionUnlockGateMode.DiningTable => true, 
				ProgressionUnlockGateMode.ConveyorTable => _progressionManager.IsConveyorTableUnlocked(_objectId), 
				_ => _unlockKey == ProgressionUnlockKey.None || _progressionManager.IsUnlocked(_unlockKey), 
			};
		}
	}
}
