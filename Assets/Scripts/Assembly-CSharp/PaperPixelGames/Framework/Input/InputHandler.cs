using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

namespace PaperPixelGames.Framework.Input
{
	[CreateAssetMenu(fileName = "InputHandler", menuName = "InputHandler/Player")]
	public class InputHandler : ScriptableObject, InputMap.IPlayerActions, InputMap.IUIActions, InputMap.IPrepStationActions, InputMap.ITabletActions, InputMap.IChefStationActions
	{
		public InputMap InputMap;

		private InputActionMap _voiceMap;

		private InputAction _pushToTalkAction;

		private readonly HashSet<int> _gameplayInputSuppressors = new HashSet<int>();

		private bool IsGameplayInputSuppressed => _gameplayInputSuppressors.Count > 0;
        public bool GameplayInputEnabled => InputMap != null && InputMap.Player.enabled && !IsGameplayInputSuppressed;

		public event UnityAction<InputActionPhase, Vector2> Move = delegate
		{
		};

		public event UnityAction<InputActionPhase> Jump = delegate
		{
		};

		public event UnityAction<InputActionPhase> Crouch = delegate
		{
		};

		public event UnityAction<InputActionPhase> Sprint = delegate
		{
		};

		public event UnityAction<InputActionPhase, Vector2, bool> Look = delegate
		{
		};

		public event UnityAction<InputActionPhase> Mouse1 = delegate
		{
		};

		public event UnityAction<InputActionPhase> RapidPress = delegate
		{
		};

		public event UnityAction<InputActionPhase> Mouse2 = delegate
		{
		};

		public event UnityAction<InputActionPhase, Vector2> Scroll = delegate
		{
		};

		public event UnityAction<InputActionPhase> OpenSettings = delegate
		{
		};

		public event UnityAction<InputActionPhase> Interact = delegate
		{
		};

		public event UnityAction<InputActionPhase> Rotate = delegate
		{
		};

		public event UnityAction<InputActionPhase, int> Slot = delegate
		{
		};

		public event UnityAction<InputActionPhase> Phone = delegate
		{
		};

		public event UnityAction<InputActionPhase> Map = delegate
		{
		};

		public event UnityAction<InputActionPhase, bool, bool> OpenTools = delegate
		{
		};

		public event UnityAction<InputActionPhase, bool, bool> OpenEmotes = delegate
		{
		};

		public event UnityAction<InputActionPhase> PushToTalk = delegate
		{
		};

		public event UnityAction<InputActionPhase> Ping = delegate
		{
		};

		public event UnityAction<InputActionPhase> Scan = delegate
		{
		};

		public event UnityAction<InputActionPhase> Flashlight = delegate
		{
		};

		public event UnityAction<InputActionPhase> Throw = delegate
		{
		};

		public event UnityAction<InputActionPhase> Any = delegate
		{
		};

		public event UnityAction<InputActionPhase> OpenChat = delegate
		{
		};

		public event UnityAction<InputActionPhase> Submit = delegate
		{
		};

		public event UnityAction<InputActionPhase> Cancel = delegate
		{
		};

		public event UnityAction<InputActionPhase> Click = delegate
		{
		};

		public event UnityAction<InputActionPhase> MiddleClick = delegate
		{
		};

		public event UnityAction<InputActionPhase> RightClick = delegate
		{
		};

		public event UnityAction<InputActionPhase, Vector2> Navigate = delegate
		{
		};

		public event UnityAction<InputActionPhase, Vector2> Point = delegate
		{
		};

		public event UnityAction<InputActionPhase, Vector2> ScrollWheel = delegate
		{
		};

		public event UnityAction<InputActionPhase, Vector3> TrackedDevicePosition = delegate
		{
		};

		public event UnityAction<InputActionPhase, Quaternion> TrackedDeviceOrientation = delegate
		{
		};

		public event UnityAction<InputActionPhase, int> PrepStationSlot = delegate
		{
		};

		public event UnityAction<InputActionPhase> KnifeAction = delegate
		{
		};

		public event UnityAction<InputActionPhase> Exit = delegate
		{
		};

		public event UnityAction<InputActionPhase, Vector2> PrepStationScroll = delegate
		{
		};

		public event UnityAction<InputActionPhase> ChefDeskExit = delegate
		{
		};

		public event UnityAction<InputActionPhase> CloseTablet = delegate
		{
		};

		public event UnityAction<InputActionPhase> CloseMap = delegate
		{
		};

		public event UnityAction<InputActionPhase> BackTablet = delegate
		{
		};

		public void Register()
		{
			_gameplayInputSuppressors.Clear();
			InputMap = new InputMap();
			InputMap.UI.SetCallbacks(this);
			InputMap.Player.SetCallbacks(this);
			InputMap.PrepStation.SetCallbacks(this);
			InputMap.Tablet.SetCallbacks(this);
			InputMap.ChefStation.SetCallbacks(this);
			RegisterVoiceInput();
			InputMap.UI.Enable();
		}

		public void UnRegister()
		{
			_gameplayInputSuppressors.Clear();
			UnregisterVoiceInput();
			InputMap.UI.Disable();
			InputMap.UI.RemoveCallbacks(this);
			InputMap.Player.RemoveCallbacks(this);
			InputMap.PrepStation.RemoveCallbacks(this);
			InputMap.Tablet.RemoveCallbacks(this);
			InputMap.ChefStation.RemoveCallbacks(this);
			Move = null;
			Jump = null;
			Crouch = null;
			Sprint = null;
			Look = null;
			Mouse1 = null;
			RapidPress = null;
			Mouse2 = null;
			Scroll = null;
			OpenSettings = null;
			Interact = null;
			Rotate = null;
			Slot = null;
			Phone = null;
			Map = null;
			OpenTools = null;
			OpenEmotes = null;
			PushToTalk = null;
			Ping = null;
			Scan = null;
			Flashlight = null;
			Any = null;
			PrepStationSlot = null;
			KnifeAction = null;
			Exit = null;
			ChefDeskExit = null;
			CloseTablet = null;
			CloseMap = null;
			BackTablet = null;
		}

		private void RegisterVoiceInput()
		{
			_voiceMap = InputMap.asset.FindActionMap("Voice", throwIfNotFound: true);
			_pushToTalkAction = _voiceMap.FindAction("PushToTalk", throwIfNotFound: true);
			_pushToTalkAction.started += OnPushToTalk;
			_pushToTalkAction.performed += OnPushToTalk;
			_pushToTalkAction.canceled += OnPushToTalk;
			_voiceMap.Enable();
		}

		private void UnregisterVoiceInput()
		{
			if (_voiceMap != null)
			{
				_voiceMap.Disable();
			}
			if (_pushToTalkAction != null)
			{
				_pushToTalkAction.started -= OnPushToTalk;
				_pushToTalkAction.performed -= OnPushToTalk;
				_pushToTalkAction.canceled -= OnPushToTalk;
			}
			_pushToTalkAction = null;
			_voiceMap = null;
		}

		private bool IsDeviceMouse(InputAction.CallbackContext context)
		{
			return context.control.device.name == "Mouse";
		}

		private void SafeInvoke<T>(UnityAction<T> action, T arg)
		{
			if (action == null)
			{
				return;
			}
			foreach (UnityAction<T> item in action.GetInvocationList())
			{
				try
				{
					item(arg);
				}
				catch (Exception arg2)
				{
					UtilityMethods.Log($"Input invoke error: {arg2}", LogType.Error);
				}
			}
		}

		private void SafeInvoke<T1, T2>(UnityAction<T1, T2> action, T1 arg1, T2 arg2)
		{
			if (action == null)
			{
				return;
			}
			foreach (UnityAction<T1, T2> item in action.GetInvocationList())
			{
				try
				{
					item(arg1, arg2);
				}
				catch (Exception arg3)
				{
					UtilityMethods.Log($"Input invoke error: {arg3}", LogType.Error);
				}
			}
		}

		private void SafeInvoke<T1, T2, T3>(UnityAction<T1, T2, T3> action, T1 arg1, T2 arg2, T3 arg3)
		{
			if (action == null)
			{
				return;
			}
			foreach (UnityAction<T1, T2, T3> item in action.GetInvocationList())
			{
				try
				{
					item(arg1, arg2, arg3);
				}
				catch (Exception arg4)
				{
					UtilityMethods.Log($"Input invoke error: {arg4}", LogType.Error);
				}
			}
		}

		public void ResetUIMap()
		{
			InputMap.UI.Disable();
			InputMap.UI.RemoveCallbacks(this);
			Singleton<DelayHelper>.Instance.DelayAction(1f, () =>
			{
				InputMap.UI.SetCallbacks(this);
			});
		}

		public void ActivateUIMap()
		{
			InputMap.UI.SetCallbacks(this);
			InputMap.UI.Enable();
		}

		public void SetGameplayInputSuppressed(int ownerId, bool suppressed)
		{
			bool isGameplayInputSuppressed = IsGameplayInputSuppressed;
			if (suppressed)
			{
				_gameplayInputSuppressors.Add(ownerId);
			}
			else
			{
				_gameplayInputSuppressors.Remove(ownerId);
			}
			bool isGameplayInputSuppressed2 = IsGameplayInputSuppressed;
			if (isGameplayInputSuppressed != isGameplayInputSuppressed2)
			{
				if (isGameplayInputSuppressed2)
				{
					SafeInvoke(Move, InputActionPhase.Canceled, Vector2.zero);
				}
				else
				{
					RefreshMovementInput();
				}
			}
		}

		private void RefreshMovementInput()
		{
			Vector2 vector = Vector2.zero;
			if (InputMap != null)
			{
				if (InputMap.Tablet.enabled)
				{
					vector = InputMap.Tablet.Move.ReadValue<Vector2>();
				}
				else if (InputMap.Player.enabled)
				{
					vector = InputMap.Player.Move.ReadValue<Vector2>();
				}
			}
			InputActionPhase arg = ((vector == Vector2.zero) ? InputActionPhase.Canceled : InputActionPhase.Performed);
			SafeInvoke(Move, arg, vector);
		}

		public void OnMove(InputAction.CallbackContext context)
		{
			if (!IsGameplayInputSuppressed)
			{
				SafeInvoke(Move, context.phase, context.ReadValue<Vector2>());
			}
		}

		public void OnLook(InputAction.CallbackContext context)
		{
			SafeInvoke(Look, context.phase, context.ReadValue<Vector2>(), IsDeviceMouse(context));
		}

		public void OnJump(InputAction.CallbackContext context)
		{
			if (!IsGameplayInputSuppressed)
			{
				SafeInvoke(Jump, context.phase);
			}
		}

		public void OnSprint(InputAction.CallbackContext context)
		{
			if (!IsGameplayInputSuppressed)
			{
				SafeInvoke(Sprint, context.phase);
			}
		}

		public void OnCrouch(InputAction.CallbackContext context)
		{
			if (!IsGameplayInputSuppressed)
			{
				SafeInvoke(Crouch, context.phase);
			}
		}

		public void OnLeftMouseMechanic(InputAction.CallbackContext context)
		{
			SafeInvoke(Mouse1, context.phase);
		}

		public void OnRapidPress(InputAction.CallbackContext context)
		{
			SafeInvoke(RapidPress, context.phase);
		}

		public void OnRightMouseMechanic(InputAction.CallbackContext context)
		{
			SafeInvoke(Mouse2, context.phase);
		}

		public void OnScroll(InputAction.CallbackContext context)
		{
			SafeInvoke(Scroll, context.phase, context.ReadValue<Vector2>());
		}

		public void OnOpenSettings(InputAction.CallbackContext context)
		{
			SafeInvoke(OpenSettings, context.phase);
		}

		public void OnPhone(InputAction.CallbackContext context)
		{
			SafeInvoke(Phone, context.phase);
		}

		public void OnMap(InputAction.CallbackContext context)
		{
			SafeInvoke(Map, context.phase);
		}

		public void OnOpenTools(InputAction.CallbackContext context)
		{
			bool arg = context.ReadValueAsButton();
			bool arg2 = context.interaction is HoldInteraction;
			SafeInvoke(OpenTools, context.phase, arg, arg2);
		}

		public void OnOpenEmotes(InputAction.CallbackContext context)
		{
			bool arg = context.ReadValueAsButton();
			bool arg2 = context.interaction is HoldInteraction;
			SafeInvoke(OpenEmotes, context.phase, arg, arg2);
		}

		public void OnInteract(InputAction.CallbackContext context)
		{
			SafeInvoke(Interact, context.phase);
		}

		private void HandleSlot(InputAction.CallbackContext context, int index)
		{
			SafeInvoke(Slot, context.phase, index);
		}

		public void OnSlot1(InputAction.CallbackContext context)
		{
			HandleSlot(context, 0);
		}

		public void OnSlot2(InputAction.CallbackContext context)
		{
			HandleSlot(context, 1);
		}

		public void OnSlot3(InputAction.CallbackContext context)
		{
			HandleSlot(context, 2);
		}

		public void OnSlot4(InputAction.CallbackContext context)
		{
			HandleSlot(context, 3);
		}

		public void OnSlot5(InputAction.CallbackContext context)
		{
		}

		public void OnPushToTalk(InputAction.CallbackContext context)
		{
			SafeInvoke(PushToTalk, context.phase);
		}

		public void OnPing(InputAction.CallbackContext context)
		{
			SafeInvoke(Ping, context.phase);
		}

		public void OnScan(InputAction.CallbackContext context)
		{
			SafeInvoke(Scan, context.phase);
		}

		public void OnFlashlight(InputAction.CallbackContext context)
		{
			SafeInvoke(Flashlight, context.phase);
		}

		public void OnAny(InputAction.CallbackContext context)
		{
			SafeInvoke(Any, context.phase);
		}

		public void OnHeldActionMode(InputAction.CallbackContext context)
		{
		}

		public void OnHeldPlaceTap(InputAction.CallbackContext context)
		{
		}

		public void OnHeldThrowSlowTap(InputAction.CallbackContext context)
		{
		}

		public void OnThrow(InputAction.CallbackContext context)
		{
			SafeInvoke(Throw, context.phase);
		}

		public void OnOpenChat(InputAction.CallbackContext context)
		{
			SafeInvoke(OpenChat, context.phase);
		}

		public void OnSubmit(InputAction.CallbackContext context)
		{
			SafeInvoke(Submit, context.phase);
		}

		public void OnCancel(InputAction.CallbackContext context)
		{
			SafeInvoke(Cancel, context.phase);
		}

		public void OnClick(InputAction.CallbackContext context)
		{
			SafeInvoke(Click, context.phase);
		}

		public void OnMiddleClick(InputAction.CallbackContext context)
		{
			SafeInvoke(MiddleClick, context.phase);
		}

		public void OnRightClick(InputAction.CallbackContext context)
		{
			SafeInvoke(RightClick, context.phase);
		}

		public void OnNavigate(InputAction.CallbackContext context)
		{
			SafeInvoke(Navigate, context.phase, context.ReadValue<Vector2>());
		}

		public void OnPoint(InputAction.CallbackContext context)
		{
			SafeInvoke(Point, context.phase, context.ReadValue<Vector2>());
		}

		public void OnScrollWheel(InputAction.CallbackContext context)
		{
			SafeInvoke(ScrollWheel, context.phase, context.ReadValue<Vector2>());
		}

		private void PrepStationHandleSlot(InputAction.CallbackContext context, int index)
		{
			SafeInvoke(PrepStationSlot, context.phase, index);
		}

		public void OnPrepStationSlot1(InputAction.CallbackContext context)
		{
			PrepStationHandleSlot(context, 0);
		}

		public void OnPrepStationSlot2(InputAction.CallbackContext context)
		{
			PrepStationHandleSlot(context, 1);
		}

		public void OnPrepStationSlot3(InputAction.CallbackContext context)
		{
			PrepStationHandleSlot(context, 2);
		}

		public void OnPrepStationSlot4(InputAction.CallbackContext context)
		{
			PrepStationHandleSlot(context, 3);
		}

		public void OnPrepStationSlot5(InputAction.CallbackContext context)
		{
			PrepStationHandleSlot(context, 4);
		}

		public void OnPrepStationScroll(InputAction.CallbackContext context)
		{
			SafeInvoke(PrepStationScroll, context.phase, context.ReadValue<Vector2>());
		}

		public void OnKnifeAction(InputAction.CallbackContext context)
		{
			SafeInvoke(KnifeAction, context.phase);
		}

		public void OnPrepStationSlotLeft(InputAction.CallbackContext context)
		{
		}

		public void OnPrepStationSlotRight(InputAction.CallbackContext context)
		{
		}

		public void OnChefDeskExit(InputAction.CallbackContext context)
		{
			SafeInvoke(ChefDeskExit, context.phase);
		}

		public void OnCloseTablet(InputAction.CallbackContext context)
		{
			SafeInvoke(CloseTablet, context.phase);
		}

		public void OnCloseMap(InputAction.CallbackContext context)
		{
			SafeInvoke(CloseMap, context.phase);
		}

		public void OnBackTablet(InputAction.CallbackContext context)
		{
			SafeInvoke(BackTablet, context.phase);
		}

		public void OnExit(InputAction.CallbackContext context)
		{
			SafeInvoke(Exit, context.phase);
		}

		public void OnZoom(InputAction.CallbackContext context)
		{
		}
	}
}
