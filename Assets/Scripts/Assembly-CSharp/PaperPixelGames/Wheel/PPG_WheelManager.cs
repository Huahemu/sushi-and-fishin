using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Wheel
{
	public class PPG_WheelManager : Singleton<PPG_WheelManager>
	{
		[SerializeField]
		private PPG_Wheel _toolWheel;

		[SerializeField]
		private PPG_Wheel _emoteWheel;

		[SerializeField]
		private PPG_Wheel _expressionWheel;

		[SerializeField]
		private PPG_Wheel _baitWheel;

		[SerializeField]
		private PPG_WheelInfo _wheelInfo;

		[Space]
		[SerializeField]
		private PPG_WheelElement _divingEquipment;

		[SerializeField]
		private PPG_WheelElement _harpoon;

		[SerializeField]
		private PPG_WheelElement _fishingRod;

		[SerializeField]
		private PPG_WheelElement _mop;

		[SerializeField]
		private PPG_WheelElement _scooter;

		[SerializeField]
		private PPG_WheelElement _compasss;

		[SerializeField]
		private PPG_WheelElement _poseidonTrident;

		[Header("Anti-Spam / Cooldowns")]
		[SerializeField]
		[Min(0f)]
		private float _openToolsCooldown = 0.15f;

		[SerializeField]
		[Min(0f)]
		private float _openEmotesCooldown = 0.15f;

		private PPG_Wheel _currentWheel;

		private int _activeWheelIndex;

		private bool _toolsOpenedByHold;

		private bool _toolsToggledOpen;

		private bool _emotesOpenedByHold;

		private bool _emotesToggledOpen;

		public override SingletonScope Scope => SingletonScope.Scene;

		private InputHandler _input => Singleton<InputManager>.Instance.InputHandler;

		public PPG_Wheel ToolWheel => _toolWheel;

		public PPG_Wheel EmoteWheel => _emoteWheel;

		public PPG_Wheel ExpressionWheel => _expressionWheel;

		public PPG_Wheel BaitWheel => _baitWheel;

		public bool IsEmotePageWheelOpen
		{
			get
			{
				if (_emotesOpenedByHold && _currentWheel != null)
				{
					if (!(_currentWheel == _emoteWheel))
					{
						return _currentWheel == _expressionWheel;
					}
					return true;
				}
				return false;
			}
		}

		public bool IsCompassUnlocked => _compasss.Unlocked;

		public PPG_Player LocalPlayer { get; set; }

		public bool DisableToolWheel { get; set; }

		public bool DisableEmoteWheel { get; set; }

		public event Action ShowWheel;

		public event Action HideWheel;

		public bool IsWheelOpen(PPG_Wheel wheel)
		{
			if (wheel != null && _currentWheel == wheel)
			{
				return wheel.WheelStatus;
			}
			return false;
		}

		public void Initialize()
		{
			DisableToolWheel = false;
			DisableEmoteWheel = false;
			_input.OpenTools += OnOpenTools;
			_input.OpenEmotes += OnOpenEmotes;
			_input.Mouse2 += OnMouse2;
		}

		private void OnDestroy()
		{
			if (_currentWheel != null)
			{
				DeactiveWheel(selectHoveredSlice: false);
			}
			if (!(Singleton<InputManager>.Instance == null))
			{
				_input.OpenTools -= OnOpenTools;
				_input.OpenEmotes -= OnOpenEmotes;
				_input.Mouse2 -= OnMouse2;
			}
		}

		private void OnOpenTools(InputActionPhase phase, bool pressed, bool isHold)
		{
            if(SushiAndFish.FishingIntegration.FishingMigration.ReloadTap(LocalPlayer,phase,isHold))return;
			if (DisableToolWheel || LocalPlayer == null)
			{
				return;
			}
			PlayerControlAction action = ((isHold || phase != InputActionPhase.Performed || !LocalPlayer.IsToolActive) ? PlayerControlAction.OpenToolWheel : PlayerControlAction.CloseActiveTool);
			if (!LocalPlayer.CanStartControlAction(action))
			{
				return;
			}
			if (isHold)
			{
				switch (phase)
				{
				case InputActionPhase.Performed:
					if (_toolsToggledOpen)
					{
						_toolsToggledOpen = false;
					}
					ActivateWheel(_toolWheel, 0);
					_toolsOpenedByHold = true;
					break;
				case InputActionPhase.Canceled:
					if (_toolsOpenedByHold)
					{
						_toolsOpenedByHold = false;
						DeactiveWheel();
					}
					break;
				}
			}
			else if (phase == InputActionPhase.Performed && !ShouldSkipToolTapForHeldThrow() && !(LocalPlayer == null))
			{
				if (LocalPlayer.IsToolActive)
				{
					LocalPlayer.CloseActiveToolFromShortcut();
				}
				else
				{
					LocalPlayer.LastToolAction?.Invoke(obj: false);
				}
			}
		}

		private bool ShouldSkipToolTapForHeldThrow()
		{
			if (LocalPlayer == null || LocalPlayer.Interactor == null)
			{
				return false;
			}
			if (LocalPlayer.Interactor.CurrentHeld != null)
			{
				return LocalPlayer.Interactor.CurrentHeld.IsThrowable;
			}
			return false;
		}

		private void OnOpenEmotes(InputActionPhase phase, bool pressed, bool isHold)
		{
			if (DisableEmoteWheel || LocalPlayer == null || !LocalPlayer.CanStartControlAction(PlayerControlAction.OpenEmoteWheel))
			{
				return;
			}
			if (isHold)
			{
				switch (phase)
				{
				case InputActionPhase.Performed:
					if (_emotesToggledOpen)
					{
						_emotesToggledOpen = false;
					}
					ActivateWheel(_emoteWheel, 1);
					_emotesOpenedByHold = true;
					break;
				case InputActionPhase.Canceled:
					if (_emotesOpenedByHold)
					{
						_emotesOpenedByHold = false;
						DeactiveWheel();
					}
					break;
				}
			}
			else if (phase == InputActionPhase.Performed)
			{
				LocalPlayer.LastEmoteAction?.Invoke();
			}
		}

		private void OnMouse2(InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started && IsEmotePageWheelOpen)
			{
				if (_expressionWheel == null)
				{
					UtilityMethods.Log("[PPG_WheelManager] Expression wheel is not assigned.", LogType.Warning);
				}
				else if (_currentWheel == _emoteWheel)
				{
					ActivateWheel(_expressionWheel, 1, selectCurrentWheel: false);
				}
				else
				{
					ActivateWheel(_emoteWheel, 1, selectCurrentWheel: false);
				}
			}
		}

		public void ActivateWheel(PPG_Wheel wheel, int index, bool selectCurrentWheel = true)
		{
			TryActivateWheel(wheel, index, selectCurrentWheel);
		}

		public bool TryActivateWheel(PPG_Wheel wheel, int index, bool selectCurrentWheel = true)
		{
			if (wheel == null || !wheel.CanShow())
			{
				return false;
			}
			DeactiveWheel(selectCurrentWheel);
			_currentWheel = wheel;
			_activeWheelIndex = index;
			_currentWheel.transform.SetAsLastSibling();
			_currentWheel.Show();
			ShowWheel?.Invoke();
			_wheelInfo.WheelVisible(_activeWheelIndex, _currentWheel == _emoteWheel);
			EventBus<WheelShow>.Raise(default);
			return true;
		}

		public void DeactiveWheel(bool selectHoveredSlice = true)
		{
			if (!(_currentWheel == null))
			{
				PPG_Wheel currentWheel = _currentWheel;
				int activeWheelIndex = _activeWheelIndex;
				_currentWheel = null;
				if (currentWheel.WheelStatus)
				{
					currentWheel.Hide(selectHoveredSlice);
				}
				HideWheel?.Invoke();
				_wheelInfo.WheelInvisible(activeWheelIndex);
				EventBus<WheelHide>.Raise(default);
			}
		}

		public void UnlockDivingEquipment(bool status)
		{
			_divingEquipment.SetLock(status);
		}

		public void UnlockHarpoon(bool status)
		{
			_harpoon.SetLock(status || SushiAndFish.FishingIntegration.FishingMigration.Enabled);
		}

		public void UnlockFishingRod(bool status)
		{
			_fishingRod.SetLock(status);
		}

		public void PopulateBaitWheel(IEnumerable<BaitSO> baits)
		{
			if (_baitWheel == null || baits == null)
			{
				return;
			}
			PPG_WheelElement pPG_WheelElement = _baitWheel.ElementTemplate ?? _toolWheel?.ElementTemplate;
			if (pPG_WheelElement == null)
			{
				UtilityMethods.Log("[PPG_WheelManager] Bait wheel could not be populated because no element template exists.", LogType.Warning);
				return;
			}
			_baitWheel.PopulateRuntimeElements(pPG_WheelElement, baits.Where((BaitSO bait) => bait != null).Distinct().Cast<ScriptableObject>());
		}

		public void UnlockMop(bool status)
		{
			_compasss.SetLock(status);
		}

		public void UnlockScooter(bool status)
		{
			_scooter.SetLock(status);
		}

		public void UnlockCompass(bool status)
		{
			_compasss.SetLock(status);
		}

		public void UnlockPoseidonTrident(bool status)
		{
			if (!(_poseidonTrident == null))
			{
				_toolWheel?.RegisterElement(_poseidonTrident);
				_poseidonTrident.SetLock(status);
			}
		}
	}
}
