using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

public class InputMap : IInputActionCollection2, IInputActionCollection, IEnumerable<InputAction>, IEnumerable, IDisposable
{
	public struct PlayerActions
	{
		private InputMap m_Wrapper;

		public InputAction Move => m_Wrapper.m_Player_Move;

		public InputAction Jump => m_Wrapper.m_Player_Jump;

		public InputAction Crouch => m_Wrapper.m_Player_Crouch;

		public InputAction Sprint => m_Wrapper.m_Player_Sprint;

		public InputAction Look => m_Wrapper.m_Player_Look;

		public InputAction LeftMouseMechanic => m_Wrapper.m_Player_LeftMouseMechanic;

		public InputAction RapidPress => m_Wrapper.m_Player_RapidPress;

		public InputAction RightMouseMechanic => m_Wrapper.m_Player_RightMouseMechanic;

		public InputAction Scroll => m_Wrapper.m_Player_Scroll;

		public InputAction OpenSettings => m_Wrapper.m_Player_OpenSettings;

		public InputAction Interact => m_Wrapper.m_Player_Interact;

		public InputAction Phone => m_Wrapper.m_Player_Phone;

		public InputAction Map => m_Wrapper.m_Player_Map;

		public InputAction OpenTools => m_Wrapper.m_Player_OpenTools;

		public InputAction OpenEmotes => m_Wrapper.m_Player_OpenEmotes;

		public InputAction Slot1 => m_Wrapper.m_Player_Slot1;

		public InputAction Slot2 => m_Wrapper.m_Player_Slot2;

		public InputAction Slot3 => m_Wrapper.m_Player_Slot3;

		public InputAction Slot4 => m_Wrapper.m_Player_Slot4;

		public InputAction Slot5 => m_Wrapper.m_Player_Slot5;

		public InputAction Ping => m_Wrapper.m_Player_Ping;

		public InputAction Scan => m_Wrapper.m_Player_Scan;

		public InputAction Any => m_Wrapper.m_Player_Any;

		public InputAction OpenChat => m_Wrapper.m_Player_OpenChat;

		public InputAction HeldActionMode => m_Wrapper.m_Player_HeldActionMode;

		public InputAction HeldPlaceTap => m_Wrapper.m_Player_HeldPlaceTap;

		public InputAction HeldThrowSlowTap => m_Wrapper.m_Player_HeldThrowSlowTap;

		public InputAction Flashlight => m_Wrapper.m_Player_Flashlight;

		public InputAction Throw => m_Wrapper.m_Player_Throw;

		public bool enabled => Get().enabled;

		public PlayerActions(InputMap wrapper)
		{
			m_Wrapper = wrapper;
		}

		public InputActionMap Get()
		{
			return m_Wrapper.m_Player;
		}

		public void Enable()
		{
			Get().Enable();
		}

		public void Disable()
		{
			Get().Disable();
		}

		public static implicit operator InputActionMap(PlayerActions set)
		{
			return set.Get();
		}

		public void AddCallbacks(IPlayerActions instance)
		{
			if (instance != null && !m_Wrapper.m_PlayerActionsCallbackInterfaces.Contains(instance))
			{
				m_Wrapper.m_PlayerActionsCallbackInterfaces.Add(instance);
				Move.started += instance.OnMove;
				Move.performed += instance.OnMove;
				Move.canceled += instance.OnMove;
				Jump.started += instance.OnJump;
				Jump.performed += instance.OnJump;
				Jump.canceled += instance.OnJump;
				Crouch.started += instance.OnCrouch;
				Crouch.performed += instance.OnCrouch;
				Crouch.canceled += instance.OnCrouch;
				Sprint.started += instance.OnSprint;
				Sprint.performed += instance.OnSprint;
				Sprint.canceled += instance.OnSprint;
				Look.started += instance.OnLook;
				Look.performed += instance.OnLook;
				Look.canceled += instance.OnLook;
				LeftMouseMechanic.started += instance.OnLeftMouseMechanic;
				LeftMouseMechanic.performed += instance.OnLeftMouseMechanic;
				LeftMouseMechanic.canceled += instance.OnLeftMouseMechanic;
				RapidPress.started += instance.OnRapidPress;
				RapidPress.performed += instance.OnRapidPress;
				RapidPress.canceled += instance.OnRapidPress;
				RightMouseMechanic.started += instance.OnRightMouseMechanic;
				RightMouseMechanic.performed += instance.OnRightMouseMechanic;
				RightMouseMechanic.canceled += instance.OnRightMouseMechanic;
				Scroll.started += instance.OnScroll;
				Scroll.performed += instance.OnScroll;
				Scroll.canceled += instance.OnScroll;
				OpenSettings.started += instance.OnOpenSettings;
				OpenSettings.performed += instance.OnOpenSettings;
				OpenSettings.canceled += instance.OnOpenSettings;
				Interact.started += instance.OnInteract;
				Interact.performed += instance.OnInteract;
				Interact.canceled += instance.OnInteract;
				Phone.started += instance.OnPhone;
				Phone.performed += instance.OnPhone;
				Phone.canceled += instance.OnPhone;
				Map.started += instance.OnMap;
				Map.performed += instance.OnMap;
				Map.canceled += instance.OnMap;
				OpenTools.started += instance.OnOpenTools;
				OpenTools.performed += instance.OnOpenTools;
				OpenTools.canceled += instance.OnOpenTools;
				OpenEmotes.started += instance.OnOpenEmotes;
				OpenEmotes.performed += instance.OnOpenEmotes;
				OpenEmotes.canceled += instance.OnOpenEmotes;
				Slot1.started += instance.OnSlot1;
				Slot1.performed += instance.OnSlot1;
				Slot1.canceled += instance.OnSlot1;
				Slot2.started += instance.OnSlot2;
				Slot2.performed += instance.OnSlot2;
				Slot2.canceled += instance.OnSlot2;
				Slot3.started += instance.OnSlot3;
				Slot3.performed += instance.OnSlot3;
				Slot3.canceled += instance.OnSlot3;
				Slot4.started += instance.OnSlot4;
				Slot4.performed += instance.OnSlot4;
				Slot4.canceled += instance.OnSlot4;
				Slot5.started += instance.OnSlot5;
				Slot5.performed += instance.OnSlot5;
				Slot5.canceled += instance.OnSlot5;
				Ping.started += instance.OnPing;
				Ping.performed += instance.OnPing;
				Ping.canceled += instance.OnPing;
				Scan.started += instance.OnScan;
				Scan.performed += instance.OnScan;
				Scan.canceled += instance.OnScan;
				Any.started += instance.OnAny;
				Any.performed += instance.OnAny;
				Any.canceled += instance.OnAny;
				OpenChat.started += instance.OnOpenChat;
				OpenChat.performed += instance.OnOpenChat;
				OpenChat.canceled += instance.OnOpenChat;
				HeldActionMode.started += instance.OnHeldActionMode;
				HeldActionMode.performed += instance.OnHeldActionMode;
				HeldActionMode.canceled += instance.OnHeldActionMode;
				HeldPlaceTap.started += instance.OnHeldPlaceTap;
				HeldPlaceTap.performed += instance.OnHeldPlaceTap;
				HeldPlaceTap.canceled += instance.OnHeldPlaceTap;
				HeldThrowSlowTap.started += instance.OnHeldThrowSlowTap;
				HeldThrowSlowTap.performed += instance.OnHeldThrowSlowTap;
				HeldThrowSlowTap.canceled += instance.OnHeldThrowSlowTap;
				Flashlight.started += instance.OnFlashlight;
				Flashlight.performed += instance.OnFlashlight;
				Flashlight.canceled += instance.OnFlashlight;
				Throw.started += instance.OnThrow;
				Throw.performed += instance.OnThrow;
				Throw.canceled += instance.OnThrow;
			}
		}

		private void UnregisterCallbacks(IPlayerActions instance)
		{
			Move.started -= instance.OnMove;
			Move.performed -= instance.OnMove;
			Move.canceled -= instance.OnMove;
			Jump.started -= instance.OnJump;
			Jump.performed -= instance.OnJump;
			Jump.canceled -= instance.OnJump;
			Crouch.started -= instance.OnCrouch;
			Crouch.performed -= instance.OnCrouch;
			Crouch.canceled -= instance.OnCrouch;
			Sprint.started -= instance.OnSprint;
			Sprint.performed -= instance.OnSprint;
			Sprint.canceled -= instance.OnSprint;
			Look.started -= instance.OnLook;
			Look.performed -= instance.OnLook;
			Look.canceled -= instance.OnLook;
			LeftMouseMechanic.started -= instance.OnLeftMouseMechanic;
			LeftMouseMechanic.performed -= instance.OnLeftMouseMechanic;
			LeftMouseMechanic.canceled -= instance.OnLeftMouseMechanic;
			RapidPress.started -= instance.OnRapidPress;
			RapidPress.performed -= instance.OnRapidPress;
			RapidPress.canceled -= instance.OnRapidPress;
			RightMouseMechanic.started -= instance.OnRightMouseMechanic;
			RightMouseMechanic.performed -= instance.OnRightMouseMechanic;
			RightMouseMechanic.canceled -= instance.OnRightMouseMechanic;
			Scroll.started -= instance.OnScroll;
			Scroll.performed -= instance.OnScroll;
			Scroll.canceled -= instance.OnScroll;
			OpenSettings.started -= instance.OnOpenSettings;
			OpenSettings.performed -= instance.OnOpenSettings;
			OpenSettings.canceled -= instance.OnOpenSettings;
			Interact.started -= instance.OnInteract;
			Interact.performed -= instance.OnInteract;
			Interact.canceled -= instance.OnInteract;
			Phone.started -= instance.OnPhone;
			Phone.performed -= instance.OnPhone;
			Phone.canceled -= instance.OnPhone;
			Map.started -= instance.OnMap;
			Map.performed -= instance.OnMap;
			Map.canceled -= instance.OnMap;
			OpenTools.started -= instance.OnOpenTools;
			OpenTools.performed -= instance.OnOpenTools;
			OpenTools.canceled -= instance.OnOpenTools;
			OpenEmotes.started -= instance.OnOpenEmotes;
			OpenEmotes.performed -= instance.OnOpenEmotes;
			OpenEmotes.canceled -= instance.OnOpenEmotes;
			Slot1.started -= instance.OnSlot1;
			Slot1.performed -= instance.OnSlot1;
			Slot1.canceled -= instance.OnSlot1;
			Slot2.started -= instance.OnSlot2;
			Slot2.performed -= instance.OnSlot2;
			Slot2.canceled -= instance.OnSlot2;
			Slot3.started -= instance.OnSlot3;
			Slot3.performed -= instance.OnSlot3;
			Slot3.canceled -= instance.OnSlot3;
			Slot4.started -= instance.OnSlot4;
			Slot4.performed -= instance.OnSlot4;
			Slot4.canceled -= instance.OnSlot4;
			Slot5.started -= instance.OnSlot5;
			Slot5.performed -= instance.OnSlot5;
			Slot5.canceled -= instance.OnSlot5;
			Ping.started -= instance.OnPing;
			Ping.performed -= instance.OnPing;
			Ping.canceled -= instance.OnPing;
			Scan.started -= instance.OnScan;
			Scan.performed -= instance.OnScan;
			Scan.canceled -= instance.OnScan;
			Any.started -= instance.OnAny;
			Any.performed -= instance.OnAny;
			Any.canceled -= instance.OnAny;
			OpenChat.started -= instance.OnOpenChat;
			OpenChat.performed -= instance.OnOpenChat;
			OpenChat.canceled -= instance.OnOpenChat;
			HeldActionMode.started -= instance.OnHeldActionMode;
			HeldActionMode.performed -= instance.OnHeldActionMode;
			HeldActionMode.canceled -= instance.OnHeldActionMode;
			HeldPlaceTap.started -= instance.OnHeldPlaceTap;
			HeldPlaceTap.performed -= instance.OnHeldPlaceTap;
			HeldPlaceTap.canceled -= instance.OnHeldPlaceTap;
			HeldThrowSlowTap.started -= instance.OnHeldThrowSlowTap;
			HeldThrowSlowTap.performed -= instance.OnHeldThrowSlowTap;
			HeldThrowSlowTap.canceled -= instance.OnHeldThrowSlowTap;
			Flashlight.started -= instance.OnFlashlight;
			Flashlight.performed -= instance.OnFlashlight;
			Flashlight.canceled -= instance.OnFlashlight;
			Throw.started -= instance.OnThrow;
			Throw.performed -= instance.OnThrow;
			Throw.canceled -= instance.OnThrow;
		}

		public void RemoveCallbacks(IPlayerActions instance)
		{
			if (m_Wrapper.m_PlayerActionsCallbackInterfaces.Remove(instance))
			{
				UnregisterCallbacks(instance);
			}
		}

		public void SetCallbacks(IPlayerActions instance)
		{
			foreach (IPlayerActions playerActionsCallbackInterface in m_Wrapper.m_PlayerActionsCallbackInterfaces)
			{
				UnregisterCallbacks(playerActionsCallbackInterface);
			}
			m_Wrapper.m_PlayerActionsCallbackInterfaces.Clear();
			AddCallbacks(instance);
		}
	}

	public struct UIActions
	{
		private InputMap m_Wrapper;

		public InputAction Navigate => m_Wrapper.m_UI_Navigate;

		public InputAction Submit => m_Wrapper.m_UI_Submit;

		public InputAction Cancel => m_Wrapper.m_UI_Cancel;

		public InputAction Point => m_Wrapper.m_UI_Point;

		public InputAction Click => m_Wrapper.m_UI_Click;

		public InputAction ScrollWheel => m_Wrapper.m_UI_ScrollWheel;

		public InputAction MiddleClick => m_Wrapper.m_UI_MiddleClick;

		public InputAction RightClick => m_Wrapper.m_UI_RightClick;

		public InputAction OpenChat => m_Wrapper.m_UI_OpenChat;

		public bool enabled => Get().enabled;

		public UIActions(InputMap wrapper)
		{
			m_Wrapper = wrapper;
		}

		public InputActionMap Get()
		{
			return m_Wrapper.m_UI;
		}

		public void Enable()
		{
			Get().Enable();
		}

		public void Disable()
		{
			Get().Disable();
		}

		public static implicit operator InputActionMap(UIActions set)
		{
			return set.Get();
		}

		public void AddCallbacks(IUIActions instance)
		{
			if (instance != null && !m_Wrapper.m_UIActionsCallbackInterfaces.Contains(instance))
			{
				m_Wrapper.m_UIActionsCallbackInterfaces.Add(instance);
				Navigate.started += instance.OnNavigate;
				Navigate.performed += instance.OnNavigate;
				Navigate.canceled += instance.OnNavigate;
				Submit.started += instance.OnSubmit;
				Submit.performed += instance.OnSubmit;
				Submit.canceled += instance.OnSubmit;
				Cancel.started += instance.OnCancel;
				Cancel.performed += instance.OnCancel;
				Cancel.canceled += instance.OnCancel;
				Point.started += instance.OnPoint;
				Point.performed += instance.OnPoint;
				Point.canceled += instance.OnPoint;
				Click.started += instance.OnClick;
				Click.performed += instance.OnClick;
				Click.canceled += instance.OnClick;
				ScrollWheel.started += instance.OnScrollWheel;
				ScrollWheel.performed += instance.OnScrollWheel;
				ScrollWheel.canceled += instance.OnScrollWheel;
				MiddleClick.started += instance.OnMiddleClick;
				MiddleClick.performed += instance.OnMiddleClick;
				MiddleClick.canceled += instance.OnMiddleClick;
				RightClick.started += instance.OnRightClick;
				RightClick.performed += instance.OnRightClick;
				RightClick.canceled += instance.OnRightClick;
				OpenChat.started += instance.OnOpenChat;
				OpenChat.performed += instance.OnOpenChat;
				OpenChat.canceled += instance.OnOpenChat;
			}
		}

		private void UnregisterCallbacks(IUIActions instance)
		{
			Navigate.started -= instance.OnNavigate;
			Navigate.performed -= instance.OnNavigate;
			Navigate.canceled -= instance.OnNavigate;
			Submit.started -= instance.OnSubmit;
			Submit.performed -= instance.OnSubmit;
			Submit.canceled -= instance.OnSubmit;
			Cancel.started -= instance.OnCancel;
			Cancel.performed -= instance.OnCancel;
			Cancel.canceled -= instance.OnCancel;
			Point.started -= instance.OnPoint;
			Point.performed -= instance.OnPoint;
			Point.canceled -= instance.OnPoint;
			Click.started -= instance.OnClick;
			Click.performed -= instance.OnClick;
			Click.canceled -= instance.OnClick;
			ScrollWheel.started -= instance.OnScrollWheel;
			ScrollWheel.performed -= instance.OnScrollWheel;
			ScrollWheel.canceled -= instance.OnScrollWheel;
			MiddleClick.started -= instance.OnMiddleClick;
			MiddleClick.performed -= instance.OnMiddleClick;
			MiddleClick.canceled -= instance.OnMiddleClick;
			RightClick.started -= instance.OnRightClick;
			RightClick.performed -= instance.OnRightClick;
			RightClick.canceled -= instance.OnRightClick;
			OpenChat.started -= instance.OnOpenChat;
			OpenChat.performed -= instance.OnOpenChat;
			OpenChat.canceled -= instance.OnOpenChat;
		}

		public void RemoveCallbacks(IUIActions instance)
		{
			if (m_Wrapper.m_UIActionsCallbackInterfaces.Remove(instance))
			{
				UnregisterCallbacks(instance);
			}
		}

		public void SetCallbacks(IUIActions instance)
		{
			foreach (IUIActions uIActionsCallbackInterface in m_Wrapper.m_UIActionsCallbackInterfaces)
			{
				UnregisterCallbacks(uIActionsCallbackInterface);
			}
			m_Wrapper.m_UIActionsCallbackInterfaces.Clear();
			AddCallbacks(instance);
		}
	}

	public struct PrepStationActions
	{
		private InputMap m_Wrapper;

		public InputAction KnifeAction => m_Wrapper.m_PrepStation_KnifeAction;

		public InputAction PrepStationSlot5 => m_Wrapper.m_PrepStation_PrepStationSlot5;

		public InputAction Exit => m_Wrapper.m_PrepStation_Exit;

		public InputAction PrepStationScroll => m_Wrapper.m_PrepStation_PrepStationScroll;

		public InputAction PrepStationSlot1 => m_Wrapper.m_PrepStation_PrepStationSlot1;

		public InputAction PrepStationSlot2 => m_Wrapper.m_PrepStation_PrepStationSlot2;

		public InputAction PrepStationSlot3 => m_Wrapper.m_PrepStation_PrepStationSlot3;

		public InputAction PrepStationSlot4 => m_Wrapper.m_PrepStation_PrepStationSlot4;

		public InputAction PrepStationSlotLeft => m_Wrapper.m_PrepStation_PrepStationSlotLeft;

		public InputAction PrepStationSlotRight => m_Wrapper.m_PrepStation_PrepStationSlotRight;

		public bool enabled => Get().enabled;

		public PrepStationActions(InputMap wrapper)
		{
			m_Wrapper = wrapper;
		}

		public InputActionMap Get()
		{
			return m_Wrapper.m_PrepStation;
		}

		public void Enable()
		{
			Get().Enable();
		}

		public void Disable()
		{
			Get().Disable();
		}

		public static implicit operator InputActionMap(PrepStationActions set)
		{
			return set.Get();
		}

		public void AddCallbacks(IPrepStationActions instance)
		{
			if (instance != null && !m_Wrapper.m_PrepStationActionsCallbackInterfaces.Contains(instance))
			{
				m_Wrapper.m_PrepStationActionsCallbackInterfaces.Add(instance);
				KnifeAction.started += instance.OnKnifeAction;
				KnifeAction.performed += instance.OnKnifeAction;
				KnifeAction.canceled += instance.OnKnifeAction;
				PrepStationSlot5.started += instance.OnPrepStationSlot5;
				PrepStationSlot5.performed += instance.OnPrepStationSlot5;
				PrepStationSlot5.canceled += instance.OnPrepStationSlot5;
				Exit.started += instance.OnExit;
				Exit.performed += instance.OnExit;
				Exit.canceled += instance.OnExit;
				PrepStationScroll.started += instance.OnPrepStationScroll;
				PrepStationScroll.performed += instance.OnPrepStationScroll;
				PrepStationScroll.canceled += instance.OnPrepStationScroll;
				PrepStationSlot1.started += instance.OnPrepStationSlot1;
				PrepStationSlot1.performed += instance.OnPrepStationSlot1;
				PrepStationSlot1.canceled += instance.OnPrepStationSlot1;
				PrepStationSlot2.started += instance.OnPrepStationSlot2;
				PrepStationSlot2.performed += instance.OnPrepStationSlot2;
				PrepStationSlot2.canceled += instance.OnPrepStationSlot2;
				PrepStationSlot3.started += instance.OnPrepStationSlot3;
				PrepStationSlot3.performed += instance.OnPrepStationSlot3;
				PrepStationSlot3.canceled += instance.OnPrepStationSlot3;
				PrepStationSlot4.started += instance.OnPrepStationSlot4;
				PrepStationSlot4.performed += instance.OnPrepStationSlot4;
				PrepStationSlot4.canceled += instance.OnPrepStationSlot4;
				PrepStationSlotLeft.started += instance.OnPrepStationSlotLeft;
				PrepStationSlotLeft.performed += instance.OnPrepStationSlotLeft;
				PrepStationSlotLeft.canceled += instance.OnPrepStationSlotLeft;
				PrepStationSlotRight.started += instance.OnPrepStationSlotRight;
				PrepStationSlotRight.performed += instance.OnPrepStationSlotRight;
				PrepStationSlotRight.canceled += instance.OnPrepStationSlotRight;
			}
		}

		private void UnregisterCallbacks(IPrepStationActions instance)
		{
			KnifeAction.started -= instance.OnKnifeAction;
			KnifeAction.performed -= instance.OnKnifeAction;
			KnifeAction.canceled -= instance.OnKnifeAction;
			PrepStationSlot5.started -= instance.OnPrepStationSlot5;
			PrepStationSlot5.performed -= instance.OnPrepStationSlot5;
			PrepStationSlot5.canceled -= instance.OnPrepStationSlot5;
			Exit.started -= instance.OnExit;
			Exit.performed -= instance.OnExit;
			Exit.canceled -= instance.OnExit;
			PrepStationScroll.started -= instance.OnPrepStationScroll;
			PrepStationScroll.performed -= instance.OnPrepStationScroll;
			PrepStationScroll.canceled -= instance.OnPrepStationScroll;
			PrepStationSlot1.started -= instance.OnPrepStationSlot1;
			PrepStationSlot1.performed -= instance.OnPrepStationSlot1;
			PrepStationSlot1.canceled -= instance.OnPrepStationSlot1;
			PrepStationSlot2.started -= instance.OnPrepStationSlot2;
			PrepStationSlot2.performed -= instance.OnPrepStationSlot2;
			PrepStationSlot2.canceled -= instance.OnPrepStationSlot2;
			PrepStationSlot3.started -= instance.OnPrepStationSlot3;
			PrepStationSlot3.performed -= instance.OnPrepStationSlot3;
			PrepStationSlot3.canceled -= instance.OnPrepStationSlot3;
			PrepStationSlot4.started -= instance.OnPrepStationSlot4;
			PrepStationSlot4.performed -= instance.OnPrepStationSlot4;
			PrepStationSlot4.canceled -= instance.OnPrepStationSlot4;
			PrepStationSlotLeft.started -= instance.OnPrepStationSlotLeft;
			PrepStationSlotLeft.performed -= instance.OnPrepStationSlotLeft;
			PrepStationSlotLeft.canceled -= instance.OnPrepStationSlotLeft;
			PrepStationSlotRight.started -= instance.OnPrepStationSlotRight;
			PrepStationSlotRight.performed -= instance.OnPrepStationSlotRight;
			PrepStationSlotRight.canceled -= instance.OnPrepStationSlotRight;
		}

		public void RemoveCallbacks(IPrepStationActions instance)
		{
			if (m_Wrapper.m_PrepStationActionsCallbackInterfaces.Remove(instance))
			{
				UnregisterCallbacks(instance);
			}
		}

		public void SetCallbacks(IPrepStationActions instance)
		{
			foreach (IPrepStationActions prepStationActionsCallbackInterface in m_Wrapper.m_PrepStationActionsCallbackInterfaces)
			{
				UnregisterCallbacks(prepStationActionsCallbackInterface);
			}
			m_Wrapper.m_PrepStationActionsCallbackInterfaces.Clear();
			AddCallbacks(instance);
		}
	}

	public struct ChefStationActions
	{
		private InputMap m_Wrapper;

		public InputAction ChefDeskExit => m_Wrapper.m_ChefStation_ChefDeskExit;

		public InputAction KnifeAction => m_Wrapper.m_ChefStation_KnifeAction;

		public bool enabled => Get().enabled;

		public ChefStationActions(InputMap wrapper)
		{
			m_Wrapper = wrapper;
		}

		public InputActionMap Get()
		{
			return m_Wrapper.m_ChefStation;
		}

		public void Enable()
		{
			Get().Enable();
		}

		public void Disable()
		{
			Get().Disable();
		}

		public static implicit operator InputActionMap(ChefStationActions set)
		{
			return set.Get();
		}

		public void AddCallbacks(IChefStationActions instance)
		{
			if (instance != null && !m_Wrapper.m_ChefStationActionsCallbackInterfaces.Contains(instance))
			{
				m_Wrapper.m_ChefStationActionsCallbackInterfaces.Add(instance);
				ChefDeskExit.started += instance.OnChefDeskExit;
				ChefDeskExit.performed += instance.OnChefDeskExit;
				ChefDeskExit.canceled += instance.OnChefDeskExit;
				KnifeAction.started += instance.OnKnifeAction;
				KnifeAction.performed += instance.OnKnifeAction;
				KnifeAction.canceled += instance.OnKnifeAction;
			}
		}

		private void UnregisterCallbacks(IChefStationActions instance)
		{
			ChefDeskExit.started -= instance.OnChefDeskExit;
			ChefDeskExit.performed -= instance.OnChefDeskExit;
			ChefDeskExit.canceled -= instance.OnChefDeskExit;
			KnifeAction.started -= instance.OnKnifeAction;
			KnifeAction.performed -= instance.OnKnifeAction;
			KnifeAction.canceled -= instance.OnKnifeAction;
		}

		public void RemoveCallbacks(IChefStationActions instance)
		{
			if (m_Wrapper.m_ChefStationActionsCallbackInterfaces.Remove(instance))
			{
				UnregisterCallbacks(instance);
			}
		}

		public void SetCallbacks(IChefStationActions instance)
		{
			foreach (IChefStationActions chefStationActionsCallbackInterface in m_Wrapper.m_ChefStationActionsCallbackInterfaces)
			{
				UnregisterCallbacks(chefStationActionsCallbackInterface);
			}
			m_Wrapper.m_ChefStationActionsCallbackInterfaces.Clear();
			AddCallbacks(instance);
		}
	}

	public struct TabletActions
	{
		private InputMap m_Wrapper;

		public InputAction Move => m_Wrapper.m_Tablet_Move;

		public InputAction Zoom => m_Wrapper.m_Tablet_Zoom;

		public InputAction Sprint => m_Wrapper.m_Tablet_Sprint;

		public InputAction Jump => m_Wrapper.m_Tablet_Jump;

		public InputAction Look => m_Wrapper.m_Tablet_Look;

		public InputAction CloseTablet => m_Wrapper.m_Tablet_CloseTablet;

		public InputAction CloseMap => m_Wrapper.m_Tablet_CloseMap;

		public InputAction BackTablet => m_Wrapper.m_Tablet_BackTablet;

		public bool enabled => Get().enabled;

		public TabletActions(InputMap wrapper)
		{
			m_Wrapper = wrapper;
		}

		public InputActionMap Get()
		{
			return m_Wrapper.m_Tablet;
		}

		public void Enable()
		{
			Get().Enable();
		}

		public void Disable()
		{
			Get().Disable();
		}

		public static implicit operator InputActionMap(TabletActions set)
		{
			return set.Get();
		}

		public void AddCallbacks(ITabletActions instance)
		{
			if (instance != null && !m_Wrapper.m_TabletActionsCallbackInterfaces.Contains(instance))
			{
				m_Wrapper.m_TabletActionsCallbackInterfaces.Add(instance);
				Move.started += instance.OnMove;
				Move.performed += instance.OnMove;
				Move.canceled += instance.OnMove;
				Zoom.started += instance.OnZoom;
				Zoom.performed += instance.OnZoom;
				Zoom.canceled += instance.OnZoom;
				Sprint.started += instance.OnSprint;
				Sprint.performed += instance.OnSprint;
				Sprint.canceled += instance.OnSprint;
				Jump.started += instance.OnJump;
				Jump.performed += instance.OnJump;
				Jump.canceled += instance.OnJump;
				Look.started += instance.OnLook;
				Look.performed += instance.OnLook;
				Look.canceled += instance.OnLook;
				CloseTablet.started += instance.OnCloseTablet;
				CloseTablet.performed += instance.OnCloseTablet;
				CloseTablet.canceled += instance.OnCloseTablet;
				CloseMap.started += instance.OnCloseMap;
				CloseMap.performed += instance.OnCloseMap;
				CloseMap.canceled += instance.OnCloseMap;
				BackTablet.started += instance.OnBackTablet;
				BackTablet.performed += instance.OnBackTablet;
				BackTablet.canceled += instance.OnBackTablet;
			}
		}

		private void UnregisterCallbacks(ITabletActions instance)
		{
			Move.started -= instance.OnMove;
			Move.performed -= instance.OnMove;
			Move.canceled -= instance.OnMove;
			Zoom.started -= instance.OnZoom;
			Zoom.performed -= instance.OnZoom;
			Zoom.canceled -= instance.OnZoom;
			Sprint.started -= instance.OnSprint;
			Sprint.performed -= instance.OnSprint;
			Sprint.canceled -= instance.OnSprint;
			Jump.started -= instance.OnJump;
			Jump.performed -= instance.OnJump;
			Jump.canceled -= instance.OnJump;
			Look.started -= instance.OnLook;
			Look.performed -= instance.OnLook;
			Look.canceled -= instance.OnLook;
			CloseTablet.started -= instance.OnCloseTablet;
			CloseTablet.performed -= instance.OnCloseTablet;
			CloseTablet.canceled -= instance.OnCloseTablet;
			CloseMap.started -= instance.OnCloseMap;
			CloseMap.performed -= instance.OnCloseMap;
			CloseMap.canceled -= instance.OnCloseMap;
			BackTablet.started -= instance.OnBackTablet;
			BackTablet.performed -= instance.OnBackTablet;
			BackTablet.canceled -= instance.OnBackTablet;
		}

		public void RemoveCallbacks(ITabletActions instance)
		{
			if (m_Wrapper.m_TabletActionsCallbackInterfaces.Remove(instance))
			{
				UnregisterCallbacks(instance);
			}
		}

		public void SetCallbacks(ITabletActions instance)
		{
			foreach (ITabletActions tabletActionsCallbackInterface in m_Wrapper.m_TabletActionsCallbackInterfaces)
			{
				UnregisterCallbacks(tabletActionsCallbackInterface);
			}
			m_Wrapper.m_TabletActionsCallbackInterfaces.Clear();
			AddCallbacks(instance);
		}
	}

	public struct VoiceActions
	{
		private InputMap m_Wrapper;

		public InputAction PushToTalk => m_Wrapper.m_Voice_PushToTalk;

		public bool enabled => Get().enabled;

		public VoiceActions(InputMap wrapper)
		{
			m_Wrapper = wrapper;
		}

		public InputActionMap Get()
		{
			return m_Wrapper.m_Voice;
		}

		public void Enable()
		{
			Get().Enable();
		}

		public void Disable()
		{
			Get().Disable();
		}

		public static implicit operator InputActionMap(VoiceActions set)
		{
			return set.Get();
		}

		public void AddCallbacks(IVoiceActions instance)
		{
			if (instance != null && !m_Wrapper.m_VoiceActionsCallbackInterfaces.Contains(instance))
			{
				m_Wrapper.m_VoiceActionsCallbackInterfaces.Add(instance);
				PushToTalk.started += instance.OnPushToTalk;
				PushToTalk.performed += instance.OnPushToTalk;
				PushToTalk.canceled += instance.OnPushToTalk;
			}
		}

		private void UnregisterCallbacks(IVoiceActions instance)
		{
			PushToTalk.started -= instance.OnPushToTalk;
			PushToTalk.performed -= instance.OnPushToTalk;
			PushToTalk.canceled -= instance.OnPushToTalk;
		}

		public void RemoveCallbacks(IVoiceActions instance)
		{
			if (m_Wrapper.m_VoiceActionsCallbackInterfaces.Remove(instance))
			{
				UnregisterCallbacks(instance);
			}
		}

		public void SetCallbacks(IVoiceActions instance)
		{
			foreach (IVoiceActions voiceActionsCallbackInterface in m_Wrapper.m_VoiceActionsCallbackInterfaces)
			{
				UnregisterCallbacks(voiceActionsCallbackInterface);
			}
			m_Wrapper.m_VoiceActionsCallbackInterfaces.Clear();
			AddCallbacks(instance);
		}
	}

	public interface IPlayerActions
	{
		void OnMove(InputAction.CallbackContext context);

		void OnJump(InputAction.CallbackContext context);

		void OnCrouch(InputAction.CallbackContext context);

		void OnSprint(InputAction.CallbackContext context);

		void OnLook(InputAction.CallbackContext context);

		void OnLeftMouseMechanic(InputAction.CallbackContext context);

		void OnRapidPress(InputAction.CallbackContext context);

		void OnRightMouseMechanic(InputAction.CallbackContext context);

		void OnScroll(InputAction.CallbackContext context);

		void OnOpenSettings(InputAction.CallbackContext context);

		void OnInteract(InputAction.CallbackContext context);

		void OnPhone(InputAction.CallbackContext context);

		void OnMap(InputAction.CallbackContext context);

		void OnOpenTools(InputAction.CallbackContext context);

		void OnOpenEmotes(InputAction.CallbackContext context);

		void OnSlot1(InputAction.CallbackContext context);

		void OnSlot2(InputAction.CallbackContext context);

		void OnSlot3(InputAction.CallbackContext context);

		void OnSlot4(InputAction.CallbackContext context);

		void OnSlot5(InputAction.CallbackContext context);

		void OnPing(InputAction.CallbackContext context);

		void OnScan(InputAction.CallbackContext context);

		void OnAny(InputAction.CallbackContext context);

		void OnOpenChat(InputAction.CallbackContext context);

		void OnHeldActionMode(InputAction.CallbackContext context);

		void OnHeldPlaceTap(InputAction.CallbackContext context);

		void OnHeldThrowSlowTap(InputAction.CallbackContext context);

		void OnFlashlight(InputAction.CallbackContext context);

		void OnThrow(InputAction.CallbackContext context);
	}

	public interface IUIActions
	{
		void OnNavigate(InputAction.CallbackContext context);

		void OnSubmit(InputAction.CallbackContext context);

		void OnCancel(InputAction.CallbackContext context);

		void OnPoint(InputAction.CallbackContext context);

		void OnClick(InputAction.CallbackContext context);

		void OnScrollWheel(InputAction.CallbackContext context);

		void OnMiddleClick(InputAction.CallbackContext context);

		void OnRightClick(InputAction.CallbackContext context);

		void OnOpenChat(InputAction.CallbackContext context);
	}

	public interface IPrepStationActions
	{
		void OnKnifeAction(InputAction.CallbackContext context);

		void OnPrepStationSlot5(InputAction.CallbackContext context);

		void OnExit(InputAction.CallbackContext context);

		void OnPrepStationScroll(InputAction.CallbackContext context);

		void OnPrepStationSlot1(InputAction.CallbackContext context);

		void OnPrepStationSlot2(InputAction.CallbackContext context);

		void OnPrepStationSlot3(InputAction.CallbackContext context);

		void OnPrepStationSlot4(InputAction.CallbackContext context);

		void OnPrepStationSlotLeft(InputAction.CallbackContext context);

		void OnPrepStationSlotRight(InputAction.CallbackContext context);
	}

	public interface IChefStationActions
	{
		void OnChefDeskExit(InputAction.CallbackContext context);

		void OnKnifeAction(InputAction.CallbackContext context);
	}

	public interface ITabletActions
	{
		void OnMove(InputAction.CallbackContext context);

		void OnZoom(InputAction.CallbackContext context);

		void OnSprint(InputAction.CallbackContext context);

		void OnJump(InputAction.CallbackContext context);

		void OnLook(InputAction.CallbackContext context);

		void OnCloseTablet(InputAction.CallbackContext context);

		void OnCloseMap(InputAction.CallbackContext context);

		void OnBackTablet(InputAction.CallbackContext context);
	}

	public interface IVoiceActions
	{
		void OnPushToTalk(InputAction.CallbackContext context);
	}

	private readonly InputActionMap m_Player;

	private List<IPlayerActions> m_PlayerActionsCallbackInterfaces = new List<IPlayerActions>();

	private readonly InputAction m_Player_Move;

	private readonly InputAction m_Player_Jump;

	private readonly InputAction m_Player_Crouch;

	private readonly InputAction m_Player_Sprint;

	private readonly InputAction m_Player_Look;

	private readonly InputAction m_Player_LeftMouseMechanic;

	private readonly InputAction m_Player_RapidPress;

	private readonly InputAction m_Player_RightMouseMechanic;

	private readonly InputAction m_Player_Scroll;

	private readonly InputAction m_Player_OpenSettings;

	private readonly InputAction m_Player_Interact;

	private readonly InputAction m_Player_Phone;

	private readonly InputAction m_Player_Map;

	private readonly InputAction m_Player_OpenTools;

	private readonly InputAction m_Player_OpenEmotes;

	private readonly InputAction m_Player_Slot1;

	private readonly InputAction m_Player_Slot2;

	private readonly InputAction m_Player_Slot3;

	private readonly InputAction m_Player_Slot4;

	private readonly InputAction m_Player_Slot5;

	private readonly InputAction m_Player_Ping;

	private readonly InputAction m_Player_Scan;

	private readonly InputAction m_Player_Any;

	private readonly InputAction m_Player_OpenChat;

	private readonly InputAction m_Player_HeldActionMode;

	private readonly InputAction m_Player_HeldPlaceTap;

	private readonly InputAction m_Player_HeldThrowSlowTap;

	private readonly InputAction m_Player_Flashlight;

	private readonly InputAction m_Player_Throw;

	private readonly InputActionMap m_UI;

	private List<IUIActions> m_UIActionsCallbackInterfaces = new List<IUIActions>();

	private readonly InputAction m_UI_Navigate;

	private readonly InputAction m_UI_Submit;

	private readonly InputAction m_UI_Cancel;

	private readonly InputAction m_UI_Point;

	private readonly InputAction m_UI_Click;

	private readonly InputAction m_UI_ScrollWheel;

	private readonly InputAction m_UI_MiddleClick;

	private readonly InputAction m_UI_RightClick;

	private readonly InputAction m_UI_OpenChat;

	private readonly InputActionMap m_PrepStation;

	private List<IPrepStationActions> m_PrepStationActionsCallbackInterfaces = new List<IPrepStationActions>();

	private readonly InputAction m_PrepStation_KnifeAction;

	private readonly InputAction m_PrepStation_PrepStationSlot5;

	private readonly InputAction m_PrepStation_Exit;

	private readonly InputAction m_PrepStation_PrepStationScroll;

	private readonly InputAction m_PrepStation_PrepStationSlot1;

	private readonly InputAction m_PrepStation_PrepStationSlot2;

	private readonly InputAction m_PrepStation_PrepStationSlot3;

	private readonly InputAction m_PrepStation_PrepStationSlot4;

	private readonly InputAction m_PrepStation_PrepStationSlotLeft;

	private readonly InputAction m_PrepStation_PrepStationSlotRight;

	private readonly InputActionMap m_ChefStation;

	private List<IChefStationActions> m_ChefStationActionsCallbackInterfaces = new List<IChefStationActions>();

	private readonly InputAction m_ChefStation_ChefDeskExit;

	private readonly InputAction m_ChefStation_KnifeAction;

	private readonly InputActionMap m_Tablet;

	private List<ITabletActions> m_TabletActionsCallbackInterfaces = new List<ITabletActions>();

	private readonly InputAction m_Tablet_Move;

	private readonly InputAction m_Tablet_Zoom;

	private readonly InputAction m_Tablet_Sprint;

	private readonly InputAction m_Tablet_Jump;

	private readonly InputAction m_Tablet_Look;

	private readonly InputAction m_Tablet_CloseTablet;

	private readonly InputAction m_Tablet_CloseMap;

	private readonly InputAction m_Tablet_BackTablet;

	private readonly InputActionMap m_Voice;

	private List<IVoiceActions> m_VoiceActionsCallbackInterfaces = new List<IVoiceActions>();

	private readonly InputAction m_Voice_PushToTalk;

	private int m_KeyboardMouseSchemeIndex = -1;

	private int m_GamepadSchemeIndex = -1;

	private int m_XboxControllerSchemeIndex = -1;

	private int m_PS4ControllerSchemeIndex = -1;

	public InputActionAsset asset { get; }

	public InputBinding? bindingMask
	{
		get
		{
			return asset.bindingMask;
		}
		set
		{
			asset.bindingMask = value;
		}
	}

	public ReadOnlyArray<InputDevice>? devices
	{
		get
		{
			return asset.devices;
		}
		set
		{
			asset.devices = value;
		}
	}

	public ReadOnlyArray<InputControlScheme> controlSchemes => asset.controlSchemes;

	public IEnumerable<InputBinding> bindings => asset.bindings;

	public PlayerActions Player => new PlayerActions(this);

	public UIActions UI => new UIActions(this);

	public PrepStationActions PrepStation => new PrepStationActions(this);

	public ChefStationActions ChefStation => new ChefStationActions(this);

	public TabletActions Tablet => new TabletActions(this);

	public VoiceActions Voice => new VoiceActions(this);

	public InputControlScheme KeyboardMouseScheme
	{
		get
		{
			if (m_KeyboardMouseSchemeIndex == -1)
			{
				m_KeyboardMouseSchemeIndex = asset.FindControlSchemeIndex("KeyboardMouse");
			}
			return asset.controlSchemes[m_KeyboardMouseSchemeIndex];
		}
	}

	public InputControlScheme GamepadScheme
	{
		get
		{
			if (m_GamepadSchemeIndex == -1)
			{
				m_GamepadSchemeIndex = asset.FindControlSchemeIndex("Gamepad");
			}
			return asset.controlSchemes[m_GamepadSchemeIndex];
		}
	}

	public InputControlScheme XboxControllerScheme
	{
		get
		{
			if (m_XboxControllerSchemeIndex == -1)
			{
				m_XboxControllerSchemeIndex = asset.FindControlSchemeIndex("Xbox Controller");
			}
			return asset.controlSchemes[m_XboxControllerSchemeIndex];
		}
	}

	public InputControlScheme PS4ControllerScheme
	{
		get
		{
			if (m_PS4ControllerSchemeIndex == -1)
			{
				m_PS4ControllerSchemeIndex = asset.FindControlSchemeIndex("PS4 Controller");
			}
			return asset.controlSchemes[m_PS4ControllerSchemeIndex];
		}
	}

	public InputMap()
	{
		asset = InputActionAsset.FromJson("{\n    \"version\": 1,\n    \"name\": \"InputMap\",\n    \"maps\": [\n        {\n            \"name\": \"Player\",\n            \"id\": \"f62a4b92-ef5e-4175-8f4c-c9075429d32c\",\n            \"actions\": [\n                {\n                    \"name\": \"Move\",\n                    \"type\": \"Value\",\n                    \"id\": \"6bc1aaf4-b110-4ff7-891e-5b9fe6f32c4d\",\n                    \"expectedControlType\": \"Vector2\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": true\n                },\n                {\n                    \"name\": \"Jump\",\n                    \"type\": \"Button\",\n                    \"id\": \"8c4abdf8-4099-493a-aa1a-129acec7c3df\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"Crouch\",\n                    \"type\": \"Button\",\n                    \"id\": \"5805829a-f283-47ef-a739-4e18aca602ff\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"Sprint\",\n                    \"type\": \"Button\",\n                    \"id\": \"980e881e-182c-404c-8cbf-3d09fdb48fef\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"Look\",\n                    \"type\": \"Value\",\n                    \"id\": \"2690c379-f54d-45be-a724-414123833eb4\",\n                    \"expectedControlType\": \"Vector2\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": true\n                },\n                {\n                    \"name\": \"LeftMouseMechanic\",\n                    \"type\": \"Button\",\n                    \"id\": \"ba783e27-578c-4f97-a5df-a058b0fa2f42\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"Hold(duration=0.4)\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"RapidPress\",\n                    \"type\": \"Button\",\n                    \"id\": \"594e7166-7122-4fe1-abfa-3adad4d8fbba\",\n                    \"expectedControlType\": \"Button\",\n                    \"processors\": \"\",\n                    \"interactions\": \"Press(behavior=0)\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"RightMouseMechanic\",\n                    \"type\": \"Button\",\n                    \"id\": \"cf13035e-ea53-4a0a-bce6-0738fb4c9e4b\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"Scroll\",\n                    \"type\": \"PassThrough\",\n                    \"id\": \"389572cd-ddaf-4f48-a581-c6ce5a234581\",\n                    \"expectedControlType\": \"Vector2\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"OpenSettings\",\n                    \"type\": \"Button\",\n                    \"id\": \"b9fe8689-7b59-4afe-9ed8-6c4a73f4e917\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"Interact\",\n                    \"type\": \"Button\",\n                    \"id\": \"42ed82c7-a33d-4795-ad73-21ffc292611e\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"Phone\",\n                    \"type\": \"Button\",\n                    \"id\": \"62c38883-0948-4f2b-a64f-e82b2443df73\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"Map\",\n                    \"type\": \"Button\",\n                    \"id\": \"563b24f4-d172-4b70-951c-fb55cb8b18a8\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"OpenTools\",\n                    \"type\": \"Value\",\n                    \"id\": \"73484b76-f879-4947-a20d-79279fa09cb2\",\n                    \"expectedControlType\": \"Digital\",\n                    \"processors\": \"\",\n                    \"interactions\": \"Hold(duration=0.3),Tap(duration=0.2)\",\n                    \"initialStateCheck\": true\n                },\n                {\n                    \"name\": \"OpenEmotes\",\n                    \"type\": \"Value\",\n                    \"id\": \"853b7052-b1cf-476c-bc30-08929f8a7b95\",\n                    \"expectedControlType\": \"Digital\",\n                    \"processors\": \"\",\n                    \"interactions\": \"Hold(duration=0.3),Tap(duration=0.2)\",\n                    \"initialStateCheck\": true\n                },\n                {\n                    \"name\": \"Slot1\",\n                    \"type\": \"Button\",\n                    \"id\": \"ee2cf6f8-0d7a-4571-aa68-96dbab8448a7\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"Slot2\",\n                    \"type\": \"Button\",\n                    \"id\": \"a7a8c1e3-1879-4e46-9738-c1400c5cff47\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"Slot3\",\n                    \"type\": \"Button\",\n                    \"id\": \"64b35089-3b72-46e5-99d3-4ff9da2362cf\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"Slot4\",\n                    \"type\": \"Button\",\n                    \"id\": \"5d79c4d4-09e3-46b0-abf5-64534beedf98\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"Slot5\",\n                    \"type\": \"Button\",\n                    \"id\": \"cc3c0feb-9bbe-4f10-ad1f-3a18dac691af\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"Ping\",\n                    \"type\": \"Button\",\n                    \"id\": \"0924afc3-1a83-4c87-825d-5c89b46e8c64\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"Scan\",\n                    \"type\": \"Button\",\n                    \"id\": \"f268655e-593d-4a9a-8d48-8c2c4964fb80\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"Any\",\n                    \"type\": \"Button\",\n                    \"id\": \"24218d21-6b82-40a1-a8ba-d6d94cb08c72\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"OpenChat\",\n                    \"type\": \"Button\",\n                    \"id\": \"177a4452-de11-4c68-aaba-40374e94adb1\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"HeldActionMode\",\n                    \"type\": \"Button\",\n                    \"id\": \"bdb3c608-49f9-4793-b5c5-003546ea5fad\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"Press\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"HeldPlaceTap\",\n                    \"type\": \"Button\",\n                    \"id\": \"8036d8b5-d7f9-4350-aba9-4ae52cf0d4b6\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"Tap(duration=0.18)\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"HeldThrowSlowTap\",\n                    \"type\": \"Button\",\n                    \"id\": \"270a42d6-5cfa-45f7-a918-86518b9c6fdf\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"SlowTap(duration=0.18)\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"Flashlight\",\n                    \"type\": \"Button\",\n                    \"id\": \"a2c37300-2355-48fb-8126-78b4e05add53\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"Throw\",\n                    \"type\": \"Button\",\n                    \"id\": \"489ae937-b280-49f0-a4ee-61dca587312c\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                }\n            ],\n            \"bindings\": [\n                {\n                    \"name\": \"WASD\",\n                    \"id\": \"b7594ddb-26c9-4ba2-bd5a-901468929edc\",\n                    \"path\": \"2DVector\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Move\",\n                    \"isComposite\": true,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"Gamepad\",\n                    \"id\": \"693c9b86-9dc4-4d88-9ed2-4d72abda6653\",\n                    \"path\": \"2DVector\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Move\",\n                    \"isComposite\": true,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"up\",\n                    \"id\": \"81aeb2cf-4859-42a9-8a3c-e03ba07eb40d\",\n                    \"path\": \"<Gamepad>/leftStick/up\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Gamepad\",\n                    \"action\": \"Move\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"down\",\n                    \"id\": \"2da53e76-813c-411b-bd1a-bb4a65182f58\",\n                    \"path\": \"<Gamepad>/leftStick/down\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Gamepad\",\n                    \"action\": \"Move\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"left\",\n                    \"id\": \"44324adf-d483-4f62-8174-f63471fb2a8b\",\n                    \"path\": \"<Gamepad>/leftStick/left\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Gamepad\",\n                    \"action\": \"Move\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"right\",\n                    \"id\": \"f6717e7b-e12d-4621-a020-3391111d1f7a\",\n                    \"path\": \"<Gamepad>/leftStick/right\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Gamepad\",\n                    \"action\": \"Move\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"Up\",\n                    \"id\": \"2063a8b5-6a45-43de-851b-65f3d46e7b58\",\n                    \"path\": \"<Keyboard>/w\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"KeyboardMouse\",\n                    \"action\": \"Move\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"Down\",\n                    \"id\": \"64e4d037-32e1-4fb9-80e4-fc7330404dfe\",\n                    \"path\": \"<Keyboard>/s\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"KeyboardMouse\",\n                    \"action\": \"Move\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"Left\",\n                    \"id\": \"57120a41-ee1f-474b-820d-92bd1458d1bd\",\n                    \"path\": \"<Keyboard>/a\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"KeyboardMouse\",\n                    \"action\": \"Move\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"Right\",\n                    \"id\": \"811b9f69-26b0-4d76-a1c4-2082b89c27ba\",\n                    \"path\": \"<Keyboard>/d\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"KeyboardMouse\",\n                    \"action\": \"Move\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"2D Vector\",\n                    \"id\": \"f093d2e3-fd6f-438d-aff8-22ab4356339e\",\n                    \"path\": \"2DVector\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Move\",\n                    \"isComposite\": true,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"up\",\n                    \"id\": \"ff42489c-1b99-4e8b-ac1e-2d727b5c3c40\",\n                    \"path\": \"<Keyboard>/upArrow\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Move\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"down\",\n                    \"id\": \"ca8d8445-1859-4d18-9431-32277c52e2bb\",\n                    \"path\": \"<Keyboard>/downArrow\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Move\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"left\",\n                    \"id\": \"3b44dcfe-3cff-4110-8fc1-5266450c2284\",\n                    \"path\": \"<Keyboard>/leftArrow\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Move\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"right\",\n                    \"id\": \"32c8ed38-1d0e-417b-adcc-912782685994\",\n                    \"path\": \"<Keyboard>/rightArrow\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Move\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"ed66cbff-2900-4a62-8896-696503cfcd31\",\n                    \"path\": \"<Pointer>/delta\",\n                    \"interactions\": \"\",\n                    \"processors\": \"InvertVector2(invertX=false),ScaleVector2(x=0.05,y=0.05)\",\n                    \"groups\": \"KeyboardMouse\",\n                    \"action\": \"Look\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"d1d171b6-19d8-47a6-ba3a-71b6a8e7b3c0\",\n                    \"path\": \"<Gamepad>/rightStick\",\n                    \"interactions\": \"\",\n                    \"processors\": \"InvertVector2(invertX=false),StickDeadzone,ScaleVector2\",\n                    \"groups\": \"Gamepad\",\n                    \"action\": \"Look\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"1bd55a0b-761e-4ae4-89ae-8ec127e08a29\",\n                    \"path\": \"<Keyboard>/space\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"KeyboardMouse\",\n                    \"action\": \"Jump\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"9f973413-5e27-4239-acee-38c4a63feeba\",\n                    \"path\": \"<Gamepad>/buttonSouth\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Gamepad\",\n                    \"action\": \"Jump\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"9df07d4d-ca22-4445-81ca-c31387219c0d\",\n                    \"path\": \"<Mouse>/leftButton\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"KeyboardMouse\",\n                    \"action\": \"LeftMouseMechanic\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"b7d6a867-21d5-4803-8cb8-7fcb9f5c8ae4\",\n                    \"path\": \"<Gamepad>/leftShoulder\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Gamepad\",\n                    \"action\": \"LeftMouseMechanic\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"47955239-d89f-49b3-83d0-918d4011a5df\",\n                    \"path\": \"<Mouse>/leftButton\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"KeyboardMouse\",\n                    \"action\": \"RapidPress\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"bf25496e-9e86-4cd8-870b-a82e21cedd50\",\n                    \"path\": \"<Gamepad>/leftShoulder\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Gamepad\",\n                    \"action\": \"RapidPress\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"06ed4740-a7f3-4c81-8b4f-7c8aa44a610c\",\n                    \"path\": \"<Keyboard>/escape\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"KeyboardMouse\",\n                    \"action\": \"OpenSettings\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"cda3ac13-4445-4ebf-90ce-c59d5102f353\",\n                    \"path\": \"<Gamepad>/start\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Gamepad\",\n                    \"action\": \"OpenSettings\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"dc65b89f-9bd3-43fb-92af-d0d87ba5faa4\",\n                    \"path\": \"<Keyboard>/leftShift\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"KeyboardMouse\",\n                    \"action\": \"Sprint\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"c8fcd86e-dcfd-4f88-8e93-b638cdbf3320\",\n                    \"path\": \"<Gamepad>/leftStickPress\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Gamepad\",\n                    \"action\": \"Sprint\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"2f467405-e447-4ce6-9beb-91173b912639\",\n                    \"path\": \"<Mouse>/rightButton\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"RightMouseMechanic\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"24d326f7-7458-4034-be2b-6aa62b191164\",\n                    \"path\": \"<Gamepad>/rightShoulder\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Gamepad\",\n                    \"action\": \"RightMouseMechanic\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"495b27d8-b83a-477b-913b-12642b1202af\",\n                    \"path\": \"<Keyboard>/tab\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Phone\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"05f7f57d-3ca2-459a-99da-29fc010bb92c\",\n                    \"path\": \"<Gamepad>/select\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Gamepad\",\n                    \"action\": \"Phone\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"f3206ff3-4800-4e62-ab2f-cb0bf69cb95e\",\n                    \"path\": \"<Keyboard>/1\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Slot1\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"d678bae5-7d71-4d92-89e4-f67c9aaaab2f\",\n                    \"path\": \"<Keyboard>/2\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Slot2\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"f81c0e16-6211-45e2-b74b-0179de3d6999\",\n                    \"path\": \"<Keyboard>/3\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Slot3\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"0ed2802e-664b-4cdf-ba12-4231b311ee09\",\n                    \"path\": \"<Keyboard>/4\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Slot4\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"29e6616e-107d-4024-8bf0-b834d4ca1518\",\n                    \"path\": \"<Keyboard>/m\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Map\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"7a3797a3-7b77-44ad-9e9b-e16982bbf1d7\",\n                    \"path\": \"<Gamepad>/dpad/up\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Gamepad\",\n                    \"action\": \"Map\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"45053cff-bb09-4e47-8bbf-6fccb248ea8b\",\n                    \"path\": \"<Keyboard>/e\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Interact\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"326e7ef2-eebd-4824-93ca-abca293c6e6d\",\n                    \"path\": \"<Gamepad>/buttonWest\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Gamepad\",\n                    \"action\": \"Interact\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"97357528-e947-4820-8c81-2306fb1e3591\",\n                    \"path\": \"<Keyboard>/leftCtrl\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"KeyboardMouse\",\n                    \"action\": \"Crouch\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"e755b7e6-6c05-45e9-916c-264eb340be2a\",\n                    \"path\": \"<Gamepad>/rightStickPress\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Gamepad\",\n                    \"action\": \"Crouch\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"54824bd3-2636-432e-9b02-75d698cdeb35\",\n                    \"path\": \"<Mouse>/scroll\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Scroll\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"c7410d2e-e972-46eb-8490-c66cc053ded9\",\n                    \"path\": \"<Keyboard>/r\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"OpenTools\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"8d43c657-0d10-4273-a12c-7070b6b18b03\",\n                    \"path\": \"<Gamepad>/leftTrigger\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Gamepad\",\n                    \"action\": \"OpenTools\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"9fcf0a78-0f4f-485c-b157-8ec758e0e64f\",\n                    \"path\": \"<Keyboard>/t\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"OpenEmotes\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"5188a12d-3fdf-435b-b2bb-ae629197903a\",\n                    \"path\": \"<Gamepad>/rightTrigger\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Gamepad\",\n                    \"action\": \"OpenEmotes\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"29e23467-f4ad-4c3d-9fc1-9132dbc14873\",\n                    \"path\": \"<Keyboard>/5\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Slot5\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"1395b8c0-927f-4b87-a514-007807995b98\",\n                    \"path\": \"<Keyboard>/z\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \";KeyboardMouse\",\n                    \"action\": \"Ping\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"491cbbd4-3436-417e-8b34-21ff3d035f53\",\n                    \"path\": \"<Keyboard>/x\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \";KeyboardMouse\",\n                    \"action\": \"Scan\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"e08c8ab2-5baf-4929-8366-ae5994d56933\",\n                    \"path\": \"<Keyboard>/w\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Any\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"c299d1ad-9b69-4910-a253-5ed5b7e25563\",\n                    \"path\": \"<Keyboard>/a\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Any\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"a0753022-ac1e-46e0-a7dd-3aee9e317e26\",\n                    \"path\": \"<Keyboard>/s\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Any\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"5881ea61-0986-4299-8cea-9fba796b8745\",\n                    \"path\": \"<Keyboard>/d\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Any\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"3121ebec-9281-4280-94c3-cec1570cbbc1\",\n                    \"path\": \"<Keyboard>/e\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Any\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"4cacf18f-e17e-4027-8b49-d33207ca01dd\",\n                    \"path\": \"<Keyboard>/space\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Any\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"e8817685-b38a-4423-92f1-a10622c13f04\",\n                    \"path\": \"<Keyboard>/escape\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Any\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"4f7b96b2-4a3c-4e51-a55c-7db388f1611f\",\n                    \"path\": \"<Mouse>/leftButton\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Any\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"c41eb5d5-aea4-42c2-a60c-aca98b3e9455\",\n                    \"path\": \"<Keyboard>/shift\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Any\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"a31edc09-c779-48f1-8882-f3eb26eb6119\",\n                    \"path\": \"<Keyboard>/ctrl\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Any\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"2f0e138e-f732-4379-ba87-aaffc4daf543\",\n                    \"path\": \"<Keyboard>/escape\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Any\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"4e599c7b-d14d-45d4-9219-bb800d9646eb\",\n                    \"path\": \"<Keyboard>/enter\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \";KeyboardMouse\",\n                    \"action\": \"OpenChat\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"425176e0-51d2-463e-8921-dfc4a1b386b4\",\n                    \"path\": \"<Mouse>/rightButton\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \";KeyboardMouse\",\n                    \"action\": \"HeldActionMode\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"85566972-f263-4454-9fb1-ab09ce18ae28\",\n                    \"path\": \"<Mouse>/leftButton\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \";KeyboardMouse\",\n                    \"action\": \"HeldPlaceTap\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"7d54a954-234c-48c2-8c5f-fd9eae196f8f\",\n                    \"path\": \"<Mouse>/leftButton\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \";KeyboardMouse\",\n                    \"action\": \"HeldThrowSlowTap\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"9ad49b68-923f-40c6-a709-a91030c1806f\",\n                    \"path\": \"<Keyboard>/f\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \";KeyboardMouse\",\n                    \"action\": \"Flashlight\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"cefc0615-5993-40f3-9e1f-b35a1af5ae63\",\n                    \"path\": \"<Keyboard>/g\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \";KeyboardMouse\",\n                    \"action\": \"Throw\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                }\n            ]\n        },\n        {\n            \"name\": \"UI\",\n            \"id\": \"79e6010b-ee23-4564-864f-1d2bee6ee6a4\",\n            \"actions\": [\n                {\n                    \"name\": \"Navigate\",\n                    \"type\": \"PassThrough\",\n                    \"id\": \"7b3a2856-defd-4e60-b9ed-2835e606bdbf\",\n                    \"expectedControlType\": \"Vector2\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"Submit\",\n                    \"type\": \"Button\",\n                    \"id\": \"38c1b47a-2332-4e6e-a4d5-53ea5b7f7dfd\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"Cancel\",\n                    \"type\": \"Button\",\n                    \"id\": \"b22e0c5c-bec6-4892-babd-b9f6a89f8cf2\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"Point\",\n                    \"type\": \"PassThrough\",\n                    \"id\": \"d8512f7c-1f3f-4789-af05-c8c7c5159481\",\n                    \"expectedControlType\": \"Vector2\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": true\n                },\n                {\n                    \"name\": \"Click\",\n                    \"type\": \"PassThrough\",\n                    \"id\": \"8aad9294-5b6c-46ff-8400-5ab0ce25615b\",\n                    \"expectedControlType\": \"Button\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": true\n                },\n                {\n                    \"name\": \"ScrollWheel\",\n                    \"type\": \"PassThrough\",\n                    \"id\": \"0950b3ec-dd35-44b2-b59c-7a56d19db27e\",\n                    \"expectedControlType\": \"Vector2\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"MiddleClick\",\n                    \"type\": \"PassThrough\",\n                    \"id\": \"920185ae-21f0-4852-a749-7e4e04e7d708\",\n                    \"expectedControlType\": \"Button\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"RightClick\",\n                    \"type\": \"PassThrough\",\n                    \"id\": \"c1859beb-866a-44dc-b671-bf6aebc00ae5\",\n                    \"expectedControlType\": \"Button\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"OpenChat\",\n                    \"type\": \"Button\",\n                    \"id\": \"980cdfe5-bae9-49ee-96ea-c2c404708880\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                }\n            ],\n            \"bindings\": [\n                {\n                    \"name\": \"Gamepad\",\n                    \"id\": \"0cfcfad8-5264-494c-8e1e-3d3a10b78d2d\",\n                    \"path\": \"2DVector\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Navigate\",\n                    \"isComposite\": true,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"up\",\n                    \"id\": \"ada4a10a-77fd-4e74-98da-4f2bbb1c22c3\",\n                    \"path\": \"<Gamepad>/leftStick/up\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \";Gamepad\",\n                    \"action\": \"Navigate\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"up\",\n                    \"id\": \"eed109ff-4c1e-4430-8582-7e850943f456\",\n                    \"path\": \"<Gamepad>/rightStick/up\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \";Gamepad\",\n                    \"action\": \"Navigate\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"down\",\n                    \"id\": \"742b414c-a383-4931-91c8-93ee0a551e96\",\n                    \"path\": \"<Gamepad>/leftStick/down\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \";Gamepad\",\n                    \"action\": \"Navigate\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"down\",\n                    \"id\": \"9379ea1e-4040-47e7-86bb-fa81b3764290\",\n                    \"path\": \"<Gamepad>/rightStick/down\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \";Gamepad\",\n                    \"action\": \"Navigate\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"left\",\n                    \"id\": \"65173f90-0d32-47fc-bd76-9111a49c97c4\",\n                    \"path\": \"<Gamepad>/leftStick/left\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \";Gamepad\",\n                    \"action\": \"Navigate\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"left\",\n                    \"id\": \"6d9cec22-3606-4601-b119-43fd19087585\",\n                    \"path\": \"<Gamepad>/rightStick/left\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \";Gamepad\",\n                    \"action\": \"Navigate\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"right\",\n                    \"id\": \"6505b54d-2676-4f0d-a858-dbada4276581\",\n                    \"path\": \"<Gamepad>/leftStick/right\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \";Gamepad\",\n                    \"action\": \"Navigate\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"right\",\n                    \"id\": \"af177fde-2f5a-4c4f-bc06-30e3423db0ec\",\n                    \"path\": \"<Gamepad>/rightStick/right\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \";Gamepad\",\n                    \"action\": \"Navigate\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"433a4e28-893c-4a33-9b1b-6f915ce989fc\",\n                    \"path\": \"<Gamepad>/dpad\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \";Gamepad\",\n                    \"action\": \"Navigate\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"Joystick\",\n                    \"id\": \"34f6fd75-7933-4bc9-a846-2e0e84cf6b24\",\n                    \"path\": \"2DVector\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Navigate\",\n                    \"isComposite\": true,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"up\",\n                    \"id\": \"8d044e14-02d0-4b9a-9303-5836ada64016\",\n                    \"path\": \"<Joystick>/stick/up\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Joystick\",\n                    \"action\": \"Navigate\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"down\",\n                    \"id\": \"5b6d4bd5-d1ba-4ae6-a1a7-e50c188ea76d\",\n                    \"path\": \"<Joystick>/stick/down\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Joystick\",\n                    \"action\": \"Navigate\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"left\",\n                    \"id\": \"937d9d20-2e75-4a67-a48e-5aeaa1108cdd\",\n                    \"path\": \"<Joystick>/stick/left\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Joystick\",\n                    \"action\": \"Navigate\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"right\",\n                    \"id\": \"767dca90-4cd3-4d53-b5bc-1d780d026380\",\n                    \"path\": \"<Joystick>/stick/right\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Joystick\",\n                    \"action\": \"Navigate\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"Keyboard\",\n                    \"id\": \"bb541d33-c04a-415b-b0d7-d9d997c341f8\",\n                    \"path\": \"2DVector\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Navigate\",\n                    \"isComposite\": true,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"up\",\n                    \"id\": \"58c29b86-d96e-419b-8851-4addc2345d34\",\n                    \"path\": \"<Keyboard>/w\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Keyboard&Mouse;KeyboardMouse\",\n                    \"action\": \"Navigate\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"up\",\n                    \"id\": \"9c78f8b4-f357-48d6-b06d-70a1b31429e1\",\n                    \"path\": \"<Keyboard>/upArrow\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Keyboard&Mouse;KeyboardMouse\",\n                    \"action\": \"Navigate\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"down\",\n                    \"id\": \"d6c79936-deed-491b-bbfb-827c1d075ccb\",\n                    \"path\": \"<Keyboard>/s\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Keyboard&Mouse;KeyboardMouse\",\n                    \"action\": \"Navigate\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"down\",\n                    \"id\": \"70bd2cb7-7cb1-4f25-84d7-4f137a78d3d8\",\n                    \"path\": \"<Keyboard>/downArrow\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Keyboard&Mouse;KeyboardMouse\",\n                    \"action\": \"Navigate\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"left\",\n                    \"id\": \"40893864-5cef-4238-b63e-04212b49ce0d\",\n                    \"path\": \"<Keyboard>/a\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Keyboard&Mouse;KeyboardMouse\",\n                    \"action\": \"Navigate\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"left\",\n                    \"id\": \"08c70ed4-d8c8-4a5e-ba48-7718b796d3cb\",\n                    \"path\": \"<Keyboard>/leftArrow\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Keyboard&Mouse;KeyboardMouse\",\n                    \"action\": \"Navigate\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"right\",\n                    \"id\": \"9f9e0b25-e711-4c48-9a45-6d1205fadd8c\",\n                    \"path\": \"<Keyboard>/d\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Keyboard&Mouse;KeyboardMouse\",\n                    \"action\": \"Navigate\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"right\",\n                    \"id\": \"b9bceb1c-1de1-47a8-aeca-5b4dc3214742\",\n                    \"path\": \"<Keyboard>/rightArrow\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Keyboard&Mouse;KeyboardMouse\",\n                    \"action\": \"Navigate\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"64e9e901-85a4-4cbd-a62d-a1de13bd7840\",\n                    \"path\": \"<Keyboard>/enter\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Keyboard&Mouse;Touch;Joystick;XR;KeyboardMouse\",\n                    \"action\": \"Submit\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"1b9159e4-abe6-4472-b4c4-b707ab3050ae\",\n                    \"path\": \"<Keyboard>/escape\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Keyboard&Mouse;Touch;Joystick;XR;KeyboardMouse\",\n                    \"action\": \"Cancel\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"e4796218-a086-4611-9d3f-983ef5e3a456\",\n                    \"path\": \"<Mouse>/position\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Keyboard&Mouse\",\n                    \"action\": \"Point\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"b24e2cbd-62cc-4f3d-8963-78580a6a9069\",\n                    \"path\": \"<Mouse>/leftButton\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \";Keyboard&Mouse\",\n                    \"action\": \"Click\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"1577e5c1-6294-43d4-b255-dd0c92a6654d\",\n                    \"path\": \"<Mouse>/scroll\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \";Keyboard&Mouse\",\n                    \"action\": \"ScrollWheel\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"f99b22aa-7c5d-48ae-8f56-f957ea27a4c9\",\n                    \"path\": \"<Mouse>/middleButton\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \";Keyboard&Mouse\",\n                    \"action\": \"MiddleClick\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"aa5e389a-b2a8-426d-a202-d5bd73afa17e\",\n                    \"path\": \"<Mouse>/rightButton\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \";Keyboard&Mouse\",\n                    \"action\": \"RightClick\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"d710d0e9-bf76-4d28-bbbe-210f792d1e92\",\n                    \"path\": \"<Keyboard>/enter\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \";KeyboardMouse\",\n                    \"action\": \"OpenChat\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                }\n            ]\n        },\n        {\n            \"name\": \"PrepStation\",\n            \"id\": \"265277e6-fb3b-461f-94d0-c00a68e094ce\",\n            \"actions\": [\n                {\n                    \"name\": \"KnifeAction\",\n                    \"type\": \"Button\",\n                    \"id\": \"708da8b1-6e85-46ba-8d72-473c288ff19a\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"PrepStationSlot5\",\n                    \"type\": \"Button\",\n                    \"id\": \"6ae4c224-830e-4741-a50d-13ed0964ef91\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"Exit\",\n                    \"type\": \"Button\",\n                    \"id\": \"e3df3a88-db94-4602-a9b1-0c00fe7481cb\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"PrepStationScroll\",\n                    \"type\": \"PassThrough\",\n                    \"id\": \"72228af6-7cce-4784-8703-daf77d3da520\",\n                    \"expectedControlType\": \"Vector2\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"PrepStationSlot1\",\n                    \"type\": \"Button\",\n                    \"id\": \"7624916d-28c2-4548-9c2f-6858f22beb12\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"PrepStationSlot2\",\n                    \"type\": \"Button\",\n                    \"id\": \"e1fb0e70-3ffb-45b4-877e-fafa9c27a22d\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"PrepStationSlot3\",\n                    \"type\": \"Button\",\n                    \"id\": \"10c150ec-2af0-4384-aa2a-8c5b712812c1\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"PrepStationSlot4\",\n                    \"type\": \"Button\",\n                    \"id\": \"011fd762-69ce-467d-8111-9668d31dc9a1\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"PrepStationSlotLeft\",\n                    \"type\": \"Button\",\n                    \"id\": \"72440f92-b1ea-42d3-b13c-201a2d7e4acb\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"PrepStationSlotRight\",\n                    \"type\": \"Button\",\n                    \"id\": \"db256386-e25a-48bc-afa1-064035689973\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                }\n            ],\n            \"bindings\": [\n                {\n                    \"name\": \"\",\n                    \"id\": \"e0a9d688-4ff9-4fec-a949-57126f4a5f89\",\n                    \"path\": \"<Mouse>/leftButton\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"KeyboardMouse\",\n                    \"action\": \"KnifeAction\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"2adbdb57-ddd7-4e93-8628-dc0b1a86a391\",\n                    \"path\": \"<Gamepad>/buttonNorth\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Gamepad\",\n                    \"action\": \"KnifeAction\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"0f9974dd-df07-48a9-b3dd-27a0079506aa\",\n                    \"path\": \"<Keyboard>/5\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"PrepStationSlot5\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"60cab107-b4fe-4291-9d71-e6751f3fba39\",\n                    \"path\": \"<Keyboard>/escape\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"KeyboardMouse\",\n                    \"action\": \"Exit\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"b25caad9-c8bc-4a94-b873-56b8a2e14c3a\",\n                    \"path\": \"<Gamepad>/start\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Gamepad\",\n                    \"action\": \"Exit\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"02b45198-80fd-4a03-b87f-6059880c9401\",\n                    \"path\": \"<Keyboard>/1\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"PrepStationSlot1\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"9eb085a2-3042-4d69-bfd7-13d6b69a20b3\",\n                    \"path\": \"<Keyboard>/2\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"PrepStationSlot2\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"81acadc7-863b-409b-97ab-6b2493d03fa6\",\n                    \"path\": \"<Keyboard>/3\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"PrepStationSlot3\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"c6eef324-3510-4b94-bd5a-4f30e7a8f373\",\n                    \"path\": \"<Keyboard>/4\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"PrepStationSlot4\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"7ef061bb-d840-49e7-9a0c-425da3ab276e\",\n                    \"path\": \"<Gamepad>/dpad/left\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Gamepad\",\n                    \"action\": \"PrepStationSlotLeft\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"e8ca0dd8-13dc-400a-bba3-c7355073f719\",\n                    \"path\": \"<Gamepad>/dpad/right\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Gamepad\",\n                    \"action\": \"PrepStationSlotRight\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"694ffc87-04cf-412e-8451-ebdc4cb3d2e3\",\n                    \"path\": \"<Mouse>/scroll\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"PrepStationScroll\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                }\n            ]\n        },\n        {\n            \"name\": \"ChefStation\",\n            \"id\": \"a7a2e9e7-1463-482d-a97d-15c4c9d7a917\",\n            \"actions\": [\n                {\n                    \"name\": \"ChefDeskExit\",\n                    \"type\": \"Button\",\n                    \"id\": \"48630631-eff1-480e-b727-f37c9f3d9c70\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"KnifeAction\",\n                    \"type\": \"Button\",\n                    \"id\": \"0568c365-dbc2-443d-b09e-663d2f2149bb\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                }\n            ],\n            \"bindings\": [\n                {\n                    \"name\": \"\",\n                    \"id\": \"78ad62ee-97a0-42ad-a4a3-2c670081b862\",\n                    \"path\": \"<Keyboard>/escape\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"KeyboardMouse\",\n                    \"action\": \"ChefDeskExit\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"2e122554-5b47-4b23-bca5-32f0fcf3b216\",\n                    \"path\": \"<Gamepad>/start\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Gamepad\",\n                    \"action\": \"ChefDeskExit\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"8bb82787-da8c-4706-b654-9784e3e9bf26\",\n                    \"path\": \"<Mouse>/leftButton\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"KeyboardMouse\",\n                    \"action\": \"KnifeAction\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"6dc155da-3b91-4a4b-b3eb-4422e3919e8e\",\n                    \"path\": \"<Gamepad>/buttonNorth\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Gamepad\",\n                    \"action\": \"KnifeAction\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                }\n            ]\n        },\n        {\n            \"name\": \"Tablet\",\n            \"id\": \"181a8a51-4942-415d-9884-db4e9cd8439e\",\n            \"actions\": [\n                {\n                    \"name\": \"Move\",\n                    \"type\": \"Value\",\n                    \"id\": \"1d00c0f4-df07-47a6-91a6-be7905e4206b\",\n                    \"expectedControlType\": \"Vector2\",\n                    \"processors\": \"NormalizeVector2\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": true\n                },\n                {\n                    \"name\": \"Zoom\",\n                    \"type\": \"PassThrough\",\n                    \"id\": \"2cf2e80b-78b4-4423-8d6a-da41e3d68cca\",\n                    \"expectedControlType\": \"Vector2\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"Sprint\",\n                    \"type\": \"Button\",\n                    \"id\": \"fd50c5a2-2d6c-464b-8fe9-f48c1ab5d0c4\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"Jump\",\n                    \"type\": \"Button\",\n                    \"id\": \"50a4bd1e-6acb-4fb6-b82b-d3338d102c2c\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"Look\",\n                    \"type\": \"Value\",\n                    \"id\": \"ad7524d9-d62f-40a7-8b83-a6a7a63f1f60\",\n                    \"expectedControlType\": \"Vector2\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": true\n                },\n                {\n                    \"name\": \"CloseTablet\",\n                    \"type\": \"Button\",\n                    \"id\": \"50c84a31-a0a7-4ee5-91ad-2867aec7dfbf\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"CloseMap\",\n                    \"type\": \"Button\",\n                    \"id\": \"6e419629-4df2-4670-8406-9e190a5375fa\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"BackTablet\",\n                    \"type\": \"Button\",\n                    \"id\": \"8e12299e-bfea-473e-9d28-49325feea8f7\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                }\n            ],\n            \"bindings\": [\n                {\n                    \"name\": \"WASD\",\n                    \"id\": \"b97ca3a9-4da0-48ff-befd-b02c704cf43c\",\n                    \"path\": \"2DVector\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Move\",\n                    \"isComposite\": true,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"Up\",\n                    \"id\": \"310fdb30-772e-4922-9390-96fa4b89cb62\",\n                    \"path\": \"<Keyboard>/w\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"KeyboardMouse\",\n                    \"action\": \"Move\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"Down\",\n                    \"id\": \"5810f0c4-427c-4f80-b3c2-179619e7b5f4\",\n                    \"path\": \"<Keyboard>/s\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"KeyboardMouse\",\n                    \"action\": \"Move\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"Left\",\n                    \"id\": \"304ba773-66d6-4961-bc2c-c1cd58fe7b7a\",\n                    \"path\": \"<Keyboard>/a\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"KeyboardMouse\",\n                    \"action\": \"Move\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"Right\",\n                    \"id\": \"c4cd7b11-1ff4-4c76-bde2-c5fd16836615\",\n                    \"path\": \"<Keyboard>/d\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"KeyboardMouse\",\n                    \"action\": \"Move\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"568b6a95-5bca-4f3b-b7f2-f2d4de63795b\",\n                    \"path\": \"<Gamepad>/leftStick\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Gamepad\",\n                    \"action\": \"Move\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"5a8fb223-3610-4392-9633-106aa4476516\",\n                    \"path\": \"<XInputController>/leftStick\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Xbox Controller\",\n                    \"action\": \"Move\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"6a7b831c-a9cf-4c6e-a225-b49744156734\",\n                    \"path\": \"<DualShockGamepad>/leftStick\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"PS4 Controller\",\n                    \"action\": \"Move\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"2D Vector\",\n                    \"id\": \"7a15f2b6-167f-4863-a70c-12a0f0d20148\",\n                    \"path\": \"2DVector\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Move\",\n                    \"isComposite\": true,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"up\",\n                    \"id\": \"4f1b24bc-2212-4f08-be13-fe27ae7866eb\",\n                    \"path\": \"<Keyboard>/upArrow\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Move\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"down\",\n                    \"id\": \"bf136e0c-f99f-4958-8042-b8a40c450440\",\n                    \"path\": \"<Keyboard>/downArrow\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Move\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"left\",\n                    \"id\": \"0f2768f2-37a2-4c99-8328-a7f7eef572d4\",\n                    \"path\": \"<Keyboard>/leftArrow\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Move\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"right\",\n                    \"id\": \"f6c97aac-e330-4076-8e92-d1786948b34f\",\n                    \"path\": \"<Keyboard>/rightArrow\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Move\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"9e58b207-b67c-45d4-8d5a-db71ac95074f\",\n                    \"path\": \"<Mouse>/scroll\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Zoom\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"eb819d39-9fd5-416d-9fe4-d96930c186fb\",\n                    \"path\": \"<Keyboard>/leftShift\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"KeyboardMouse\",\n                    \"action\": \"Sprint\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"19e9d7d5-78be-4160-82c8-5520e2c39ec4\",\n                    \"path\": \"<Gamepad>/leftStickPress\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Gamepad\",\n                    \"action\": \"Sprint\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"5dfd4fd9-5361-4b28-97ec-45a0c20ba013\",\n                    \"path\": \"<Keyboard>/space\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"KeyboardMouse\",\n                    \"action\": \"Jump\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"5a714cc2-7ee3-4dfd-9d7c-9051ea9faffe\",\n                    \"path\": \"<Gamepad>/buttonSouth\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Gamepad\",\n                    \"action\": \"Jump\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"b28248f3-d1c4-4f12-931a-d534652bf49d\",\n                    \"path\": \"<Pointer>/delta\",\n                    \"interactions\": \"\",\n                    \"processors\": \"InvertVector2(invertX=false),ScaleVector2(x=0.05,y=0.05)\",\n                    \"groups\": \"KeyboardMouse\",\n                    \"action\": \"Look\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"bf27614b-7944-4b7d-879d-d8f3f14b29d2\",\n                    \"path\": \"<Gamepad>/rightStick\",\n                    \"interactions\": \"\",\n                    \"processors\": \"InvertVector2(invertX=false),StickDeadzone,ScaleVector2\",\n                    \"groups\": \"Gamepad\",\n                    \"action\": \"Look\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"3a29d247-1195-43a2-8c02-ae748c3ac779\",\n                    \"path\": \"<Keyboard>/escape\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"KeyboardMouse\",\n                    \"action\": \"CloseTablet\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"1d460cb9-8b5c-46b8-b5cf-3d9cfcc5be36\",\n                    \"path\": \"<Gamepad>/start\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Gamepad\",\n                    \"action\": \"CloseTablet\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"084246e6-00ed-4e87-89d6-88cceacdca92\",\n                    \"path\": \"<XInputController>/start\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Xbox Controller\",\n                    \"action\": \"CloseTablet\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"f0b1a9e2-17ff-49fd-8bb7-42135c8b986a\",\n                    \"path\": \"<DualShockGamepad>/select\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"PS4 Controller\",\n                    \"action\": \"CloseTablet\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"c25eb5fd-b6e5-4ec8-b7c2-d24689a7ab6e\",\n                    \"path\": \"<Keyboard>/tab\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"BackTablet\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"45d63262-0e20-4109-94d6-700f0df406dc\",\n                    \"path\": \"<Gamepad>/select\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Gamepad\",\n                    \"action\": \"BackTablet\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"fb88b134-697d-49f2-b0dd-67612f21350b\",\n                    \"path\": \"<Keyboard>/m\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"CloseMap\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"61c70b36-5d6c-4855-aa8a-a8dddc91c14c\",\n                    \"path\": \"<Gamepad>/dpad/up\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"Gamepad\",\n                    \"action\": \"CloseMap\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                }\n            ]\n        },\n        {\n            \"name\": \"Voice\",\n            \"id\": \"5f542379-130c-4e14-97c1-3d991eaf6620\",\n            \"actions\": [\n                {\n                    \"name\": \"PushToTalk\",\n                    \"type\": \"Button\",\n                    \"id\": \"209d960e-e8ef-421e-85d1-dcf821ca5907\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                }\n            ],\n            \"bindings\": [\n                {\n                    \"name\": \"\",\n                    \"id\": \"f642c781-c382-4247-a89f-4fba83d1a9f7\",\n                    \"path\": \"<Keyboard>/v\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \";KeyboardMouse\",\n                    \"action\": \"PushToTalk\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                }\n            ]\n        }\n    ],\n    \"controlSchemes\": [\n        {\n            \"name\": \"KeyboardMouse\",\n            \"bindingGroup\": \"KeyboardMouse\",\n            \"devices\": [\n                {\n                    \"devicePath\": \"<Keyboard>\",\n                    \"isOptional\": false,\n                    \"isOR\": false\n                },\n                {\n                    \"devicePath\": \"<Mouse>\",\n                    \"isOptional\": false,\n                    \"isOR\": false\n                }\n            ]\n        },\n        {\n            \"name\": \"Gamepad\",\n            \"bindingGroup\": \"Gamepad\",\n            \"devices\": [\n                {\n                    \"devicePath\": \"<Gamepad>\",\n                    \"isOptional\": true,\n                    \"isOR\": false\n                },\n                {\n                    \"devicePath\": \"<XInputController>\",\n                    \"isOptional\": true,\n                    \"isOR\": false\n                },\n                {\n                    \"devicePath\": \"<DualShockGamepad>\",\n                    \"isOptional\": true,\n                    \"isOR\": false\n                }\n            ]\n        },\n        {\n            \"name\": \"Xbox Controller\",\n            \"bindingGroup\": \"Xbox Controller\",\n            \"devices\": []\n        },\n        {\n            \"name\": \"PS4 Controller\",\n            \"bindingGroup\": \"PS4 Controller\",\n            \"devices\": []\n        }\n    ]\n}");
		m_Player = asset.FindActionMap("Player", throwIfNotFound: true);
		m_Player_Move = m_Player.FindAction("Move", throwIfNotFound: true);
		m_Player_Jump = m_Player.FindAction("Jump", throwIfNotFound: true);
		m_Player_Crouch = m_Player.FindAction("Crouch", throwIfNotFound: true);
		m_Player_Sprint = m_Player.FindAction("Sprint", throwIfNotFound: true);
		m_Player_Look = m_Player.FindAction("Look", throwIfNotFound: true);
		m_Player_LeftMouseMechanic = m_Player.FindAction("LeftMouseMechanic", throwIfNotFound: true);
		m_Player_RapidPress = m_Player.FindAction("RapidPress", throwIfNotFound: true);
		m_Player_RightMouseMechanic = m_Player.FindAction("RightMouseMechanic", throwIfNotFound: true);
		m_Player_Scroll = m_Player.FindAction("Scroll", throwIfNotFound: true);
		m_Player_OpenSettings = m_Player.FindAction("OpenSettings", throwIfNotFound: true);
		m_Player_Interact = m_Player.FindAction("Interact", throwIfNotFound: true);
		m_Player_Phone = m_Player.FindAction("Phone", throwIfNotFound: true);
		m_Player_Map = m_Player.FindAction("Map", throwIfNotFound: true);
		m_Player_OpenTools = m_Player.FindAction("OpenTools", throwIfNotFound: true);
		m_Player_OpenEmotes = m_Player.FindAction("OpenEmotes", throwIfNotFound: true);
		m_Player_Slot1 = m_Player.FindAction("Slot1", throwIfNotFound: true);
		m_Player_Slot2 = m_Player.FindAction("Slot2", throwIfNotFound: true);
		m_Player_Slot3 = m_Player.FindAction("Slot3", throwIfNotFound: true);
		m_Player_Slot4 = m_Player.FindAction("Slot4", throwIfNotFound: true);
		m_Player_Slot5 = m_Player.FindAction("Slot5", throwIfNotFound: true);
		m_Player_Ping = m_Player.FindAction("Ping", throwIfNotFound: true);
		m_Player_Scan = m_Player.FindAction("Scan", throwIfNotFound: true);
		m_Player_Any = m_Player.FindAction("Any", throwIfNotFound: true);
		m_Player_OpenChat = m_Player.FindAction("OpenChat", throwIfNotFound: true);
		m_Player_HeldActionMode = m_Player.FindAction("HeldActionMode", throwIfNotFound: true);
		m_Player_HeldPlaceTap = m_Player.FindAction("HeldPlaceTap", throwIfNotFound: true);
		m_Player_HeldThrowSlowTap = m_Player.FindAction("HeldThrowSlowTap", throwIfNotFound: true);
		m_Player_Flashlight = m_Player.FindAction("Flashlight", throwIfNotFound: true);
		m_Player_Throw = m_Player.FindAction("Throw", throwIfNotFound: true);
		m_UI = asset.FindActionMap("UI", throwIfNotFound: true);
		m_UI_Navigate = m_UI.FindAction("Navigate", throwIfNotFound: true);
		m_UI_Submit = m_UI.FindAction("Submit", throwIfNotFound: true);
		m_UI_Cancel = m_UI.FindAction("Cancel", throwIfNotFound: true);
		m_UI_Point = m_UI.FindAction("Point", throwIfNotFound: true);
		m_UI_Click = m_UI.FindAction("Click", throwIfNotFound: true);
		m_UI_ScrollWheel = m_UI.FindAction("ScrollWheel", throwIfNotFound: true);
		m_UI_MiddleClick = m_UI.FindAction("MiddleClick", throwIfNotFound: true);
		m_UI_RightClick = m_UI.FindAction("RightClick", throwIfNotFound: true);
		m_UI_OpenChat = m_UI.FindAction("OpenChat", throwIfNotFound: true);
		m_PrepStation = asset.FindActionMap("PrepStation", throwIfNotFound: true);
		m_PrepStation_KnifeAction = m_PrepStation.FindAction("KnifeAction", throwIfNotFound: true);
		m_PrepStation_PrepStationSlot5 = m_PrepStation.FindAction("PrepStationSlot5", throwIfNotFound: true);
		m_PrepStation_Exit = m_PrepStation.FindAction("Exit", throwIfNotFound: true);
		m_PrepStation_PrepStationScroll = m_PrepStation.FindAction("PrepStationScroll", throwIfNotFound: true);
		m_PrepStation_PrepStationSlot1 = m_PrepStation.FindAction("PrepStationSlot1", throwIfNotFound: true);
		m_PrepStation_PrepStationSlot2 = m_PrepStation.FindAction("PrepStationSlot2", throwIfNotFound: true);
		m_PrepStation_PrepStationSlot3 = m_PrepStation.FindAction("PrepStationSlot3", throwIfNotFound: true);
		m_PrepStation_PrepStationSlot4 = m_PrepStation.FindAction("PrepStationSlot4", throwIfNotFound: true);
		m_PrepStation_PrepStationSlotLeft = m_PrepStation.FindAction("PrepStationSlotLeft", throwIfNotFound: true);
		m_PrepStation_PrepStationSlotRight = m_PrepStation.FindAction("PrepStationSlotRight", throwIfNotFound: true);
		m_ChefStation = asset.FindActionMap("ChefStation", throwIfNotFound: true);
		m_ChefStation_ChefDeskExit = m_ChefStation.FindAction("ChefDeskExit", throwIfNotFound: true);
		m_ChefStation_KnifeAction = m_ChefStation.FindAction("KnifeAction", throwIfNotFound: true);
		m_Tablet = asset.FindActionMap("Tablet", throwIfNotFound: true);
		m_Tablet_Move = m_Tablet.FindAction("Move", throwIfNotFound: true);
		m_Tablet_Zoom = m_Tablet.FindAction("Zoom", throwIfNotFound: true);
		m_Tablet_Sprint = m_Tablet.FindAction("Sprint", throwIfNotFound: true);
		m_Tablet_Jump = m_Tablet.FindAction("Jump", throwIfNotFound: true);
		m_Tablet_Look = m_Tablet.FindAction("Look", throwIfNotFound: true);
		m_Tablet_CloseTablet = m_Tablet.FindAction("CloseTablet", throwIfNotFound: true);
		m_Tablet_CloseMap = m_Tablet.FindAction("CloseMap", throwIfNotFound: true);
		m_Tablet_BackTablet = m_Tablet.FindAction("BackTablet", throwIfNotFound: true);
		m_Voice = asset.FindActionMap("Voice", throwIfNotFound: true);
		m_Voice_PushToTalk = m_Voice.FindAction("PushToTalk", throwIfNotFound: true);
	}

	~InputMap()
	{
	}

	public void Dispose()
	{
		UnityEngine.Object.Destroy(asset);
	}

	public bool Contains(InputAction action)
	{
		return asset.Contains(action);
	}

	public IEnumerator<InputAction> GetEnumerator()
	{
		return asset.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}

	public void Enable()
	{
		asset.Enable();
	}

	public void Disable()
	{
		asset.Disable();
	}

	public InputAction FindAction(string actionNameOrId, bool throwIfNotFound = false)
	{
		return asset.FindAction(actionNameOrId, throwIfNotFound);
	}

	public int FindBinding(InputBinding bindingMask, out InputAction action)
	{
		return asset.FindBinding(bindingMask, out action);
	}
}
