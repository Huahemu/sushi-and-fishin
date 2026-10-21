namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::UnityEngine;
using global::UnityEngine.InputSystem;
using global::UnityEngine.InputSystem.Utilities;

public class InputActions : IInputActionCollection2, IInputActionCollection, IEnumerable<InputAction>, IEnumerable, IDisposable
{
	public struct PlayerActions
	{
		private InputActions m_Wrapper;

		public InputAction Move => m_Wrapper.m_Player_Move;

		public InputAction Aim => m_Wrapper.m_Player_Aim;

		public InputAction Jump => m_Wrapper.m_Player_Jump;

		public InputAction Crouch => m_Wrapper.m_Player_Crouch;

		public InputAction Sprint => m_Wrapper.m_Player_Sprint;

		public InputAction Interact => m_Wrapper.m_Player_Interact;

		public InputAction SkipUI => m_Wrapper.m_Player_SkipUI;

		public InputAction ThrowItem => m_Wrapper.m_Player_ThrowItem;

		public InputAction Zoom => m_Wrapper.m_Player_Zoom;

		public InputAction ItemSelect => m_Wrapper.m_Player_ItemSelect;

		public InputAction Scroll => m_Wrapper.m_Player_Scroll;

		public InputAction UseItem => m_Wrapper.m_Player_UseItem;

		public InputAction Console => m_Wrapper.m_Player_Console;

		public InputAction EscapeMenu => m_Wrapper.m_Player_EscapeMenu;

		public InputAction EmoteWheel => m_Wrapper.m_Player_EmoteWheel;

		public InputAction F1 => m_Wrapper.m_Player_F1;

		public InputAction F2 => m_Wrapper.m_Player_F2;

		public InputAction F3 => m_Wrapper.m_Player_F3;

		public InputAction F4 => m_Wrapper.m_Player_F4;

		public InputAction Ping => m_Wrapper.m_Player_Ping;

		public InputAction PushToTalk => m_Wrapper.m_Player_PushToTalk;

		public bool enabled => Get().enabled;

		public PlayerActions(InputActions wrapper)
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
				Aim.started += instance.OnAim;
				Aim.performed += instance.OnAim;
				Aim.canceled += instance.OnAim;
				Jump.started += instance.OnJump;
				Jump.performed += instance.OnJump;
				Jump.canceled += instance.OnJump;
				Crouch.started += instance.OnCrouch;
				Crouch.performed += instance.OnCrouch;
				Crouch.canceled += instance.OnCrouch;
				Sprint.started += instance.OnSprint;
				Sprint.performed += instance.OnSprint;
				Sprint.canceled += instance.OnSprint;
				Interact.started += instance.OnInteract;
				Interact.performed += instance.OnInteract;
				Interact.canceled += instance.OnInteract;
				SkipUI.started += instance.OnSkipUI;
				SkipUI.performed += instance.OnSkipUI;
				SkipUI.canceled += instance.OnSkipUI;
				ThrowItem.started += instance.OnThrowItem;
				ThrowItem.performed += instance.OnThrowItem;
				ThrowItem.canceled += instance.OnThrowItem;
				Zoom.started += instance.OnZoom;
				Zoom.performed += instance.OnZoom;
				Zoom.canceled += instance.OnZoom;
				ItemSelect.started += instance.OnItemSelect;
				ItemSelect.performed += instance.OnItemSelect;
				ItemSelect.canceled += instance.OnItemSelect;
				Scroll.started += instance.OnScroll;
				Scroll.performed += instance.OnScroll;
				Scroll.canceled += instance.OnScroll;
				UseItem.started += instance.OnUseItem;
				UseItem.performed += instance.OnUseItem;
				UseItem.canceled += instance.OnUseItem;
				Console.started += instance.OnConsole;
				Console.performed += instance.OnConsole;
				Console.canceled += instance.OnConsole;
				EscapeMenu.started += instance.OnEscapeMenu;
				EscapeMenu.performed += instance.OnEscapeMenu;
				EscapeMenu.canceled += instance.OnEscapeMenu;
				EmoteWheel.started += instance.OnEmoteWheel;
				EmoteWheel.performed += instance.OnEmoteWheel;
				EmoteWheel.canceled += instance.OnEmoteWheel;
				F1.started += instance.OnF1;
				F1.performed += instance.OnF1;
				F1.canceled += instance.OnF1;
				F2.started += instance.OnF2;
				F2.performed += instance.OnF2;
				F2.canceled += instance.OnF2;
				F3.started += instance.OnF3;
				F3.performed += instance.OnF3;
				F3.canceled += instance.OnF3;
				F4.started += instance.OnF4;
				F4.performed += instance.OnF4;
				F4.canceled += instance.OnF4;
				Ping.started += instance.OnPing;
				Ping.performed += instance.OnPing;
				Ping.canceled += instance.OnPing;
				PushToTalk.started += instance.OnPushToTalk;
				PushToTalk.performed += instance.OnPushToTalk;
				PushToTalk.canceled += instance.OnPushToTalk;
			}
		}

		private void UnregisterCallbacks(IPlayerActions instance)
		{
			Move.started -= instance.OnMove;
			Move.performed -= instance.OnMove;
			Move.canceled -= instance.OnMove;
			Aim.started -= instance.OnAim;
			Aim.performed -= instance.OnAim;
			Aim.canceled -= instance.OnAim;
			Jump.started -= instance.OnJump;
			Jump.performed -= instance.OnJump;
			Jump.canceled -= instance.OnJump;
			Crouch.started -= instance.OnCrouch;
			Crouch.performed -= instance.OnCrouch;
			Crouch.canceled -= instance.OnCrouch;
			Sprint.started -= instance.OnSprint;
			Sprint.performed -= instance.OnSprint;
			Sprint.canceled -= instance.OnSprint;
			Interact.started -= instance.OnInteract;
			Interact.performed -= instance.OnInteract;
			Interact.canceled -= instance.OnInteract;
			SkipUI.started -= instance.OnSkipUI;
			SkipUI.performed -= instance.OnSkipUI;
			SkipUI.canceled -= instance.OnSkipUI;
			ThrowItem.started -= instance.OnThrowItem;
			ThrowItem.performed -= instance.OnThrowItem;
			ThrowItem.canceled -= instance.OnThrowItem;
			Zoom.started -= instance.OnZoom;
			Zoom.performed -= instance.OnZoom;
			Zoom.canceled -= instance.OnZoom;
			ItemSelect.started -= instance.OnItemSelect;
			ItemSelect.performed -= instance.OnItemSelect;
			ItemSelect.canceled -= instance.OnItemSelect;
			Scroll.started -= instance.OnScroll;
			Scroll.performed -= instance.OnScroll;
			Scroll.canceled -= instance.OnScroll;
			UseItem.started -= instance.OnUseItem;
			UseItem.performed -= instance.OnUseItem;
			UseItem.canceled -= instance.OnUseItem;
			Console.started -= instance.OnConsole;
			Console.performed -= instance.OnConsole;
			Console.canceled -= instance.OnConsole;
			EscapeMenu.started -= instance.OnEscapeMenu;
			EscapeMenu.performed -= instance.OnEscapeMenu;
			EscapeMenu.canceled -= instance.OnEscapeMenu;
			EmoteWheel.started -= instance.OnEmoteWheel;
			EmoteWheel.performed -= instance.OnEmoteWheel;
			EmoteWheel.canceled -= instance.OnEmoteWheel;
			F1.started -= instance.OnF1;
			F1.performed -= instance.OnF1;
			F1.canceled -= instance.OnF1;
			F2.started -= instance.OnF2;
			F2.performed -= instance.OnF2;
			F2.canceled -= instance.OnF2;
			F3.started -= instance.OnF3;
			F3.performed -= instance.OnF3;
			F3.canceled -= instance.OnF3;
			F4.started -= instance.OnF4;
			F4.performed -= instance.OnF4;
			F4.canceled -= instance.OnF4;
			Ping.started -= instance.OnPing;
			Ping.performed -= instance.OnPing;
			Ping.canceled -= instance.OnPing;
			PushToTalk.started -= instance.OnPushToTalk;
			PushToTalk.performed -= instance.OnPushToTalk;
			PushToTalk.canceled -= instance.OnPushToTalk;
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

	public interface IPlayerActions
	{
		void OnMove(InputAction.CallbackContext context);

		void OnAim(InputAction.CallbackContext context);

		void OnJump(InputAction.CallbackContext context);

		void OnCrouch(InputAction.CallbackContext context);

		void OnSprint(InputAction.CallbackContext context);

		void OnInteract(InputAction.CallbackContext context);

		void OnSkipUI(InputAction.CallbackContext context);

		void OnThrowItem(InputAction.CallbackContext context);

		void OnZoom(InputAction.CallbackContext context);

		void OnItemSelect(InputAction.CallbackContext context);

		void OnScroll(InputAction.CallbackContext context);

		void OnUseItem(InputAction.CallbackContext context);

		void OnConsole(InputAction.CallbackContext context);

		void OnEscapeMenu(InputAction.CallbackContext context);

		void OnEmoteWheel(InputAction.CallbackContext context);

		void OnF1(InputAction.CallbackContext context);

		void OnF2(InputAction.CallbackContext context);

		void OnF3(InputAction.CallbackContext context);

		void OnF4(InputAction.CallbackContext context);

		void OnPing(InputAction.CallbackContext context);

		void OnPushToTalk(InputAction.CallbackContext context);
	}

	private readonly InputActionMap m_Player;

	private List<IPlayerActions> m_PlayerActionsCallbackInterfaces = new List<IPlayerActions>();

	private readonly InputAction m_Player_Move;

	private readonly InputAction m_Player_Aim;

	private readonly InputAction m_Player_Jump;

	private readonly InputAction m_Player_Crouch;

	private readonly InputAction m_Player_Sprint;

	private readonly InputAction m_Player_Interact;

	private readonly InputAction m_Player_SkipUI;

	private readonly InputAction m_Player_ThrowItem;

	private readonly InputAction m_Player_Zoom;

	private readonly InputAction m_Player_ItemSelect;

	private readonly InputAction m_Player_Scroll;

	private readonly InputAction m_Player_UseItem;

	private readonly InputAction m_Player_Console;

	private readonly InputAction m_Player_EscapeMenu;

	private readonly InputAction m_Player_EmoteWheel;

	private readonly InputAction m_Player_F1;

	private readonly InputAction m_Player_F2;

	private readonly InputAction m_Player_F3;

	private readonly InputAction m_Player_F4;

	private readonly InputAction m_Player_Ping;

	private readonly InputAction m_Player_PushToTalk;

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

	public InputActions()
	{
		asset = InputActionAsset.FromJson("{\n    \"version\": 1,\n    \"name\": \"InputActions\",\n    \"maps\": [\n        {\n            \"name\": \"Player\",\n            \"id\": \"5770ebab-5e21-4325-93b0-162d5ad04eab\",\n            \"actions\": [\n                {\n                    \"name\": \"Move\",\n                    \"type\": \"Value\",\n                    \"id\": \"4da15314-2555-43e8-a6f9-09af9a4faa97\",\n                    \"expectedControlType\": \"Vector2\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": true\n                },\n                {\n                    \"name\": \"Aim\",\n                    \"type\": \"Value\",\n                    \"id\": \"34f98767-7179-4ce9-a1ec-543f5d4db0af\",\n                    \"expectedControlType\": \"Delta\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": true\n                },\n                {\n                    \"name\": \"Jump\",\n                    \"type\": \"Button\",\n                    \"id\": \"7965c018-f2f3-4a37-a653-ffdd121a9e1f\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"Crouch\",\n                    \"type\": \"Button\",\n                    \"id\": \"88d012bc-c097-414e-9d04-912ab3b0e534\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"Sprint\",\n                    \"type\": \"Button\",\n                    \"id\": \"922e8702-a416-4b83-a438-44f85500e4f0\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"Interact\",\n                    \"type\": \"Button\",\n                    \"id\": \"2ad185b5-6df6-4f44-b25b-9d992357da91\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"SkipUI\",\n                    \"type\": \"Button\",\n                    \"id\": \"cac312c6-6853-4ff1-80ca-f0805bfc5eb3\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"ThrowItem\",\n                    \"type\": \"Button\",\n                    \"id\": \"d529c9ae-50ea-46c8-a403-1d75e2c7d192\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"Zoom\",\n                    \"type\": \"Button\",\n                    \"id\": \"7009ea0d-3c8d-4b64-82d7-bd565f632550\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"ItemSelect\",\n                    \"type\": \"Value\",\n                    \"id\": \"8be36012-d455-4c4a-8dd0-542ca097d813\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": true\n                },\n                {\n                    \"name\": \"Scroll\",\n                    \"type\": \"Value\",\n                    \"id\": \"90283829-0eae-46de-b689-eb3ee8c99ec6\",\n                    \"expectedControlType\": \"Axis\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": true\n                },\n                {\n                    \"name\": \"UseItem\",\n                    \"type\": \"Button\",\n                    \"id\": \"950fea2a-a26e-4d20-ab16-da3e264f4722\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"Console\",\n                    \"type\": \"Button\",\n                    \"id\": \"c57aa9eb-e564-45f7-ae36-dfeb9a973802\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"EscapeMenu\",\n                    \"type\": \"Button\",\n                    \"id\": \"459af410-5dd2-42a7-848d-e49bc83470a8\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"EmoteWheel\",\n                    \"type\": \"Button\",\n                    \"id\": \"6641fb91-1537-44dd-b0f7-edbd636c768d\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"F1\",\n                    \"type\": \"Button\",\n                    \"id\": \"fbf13060-94cf-4871-88a5-89a69dd2236c\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"F2\",\n                    \"type\": \"Button\",\n                    \"id\": \"0557d4f4-b887-43f9-b0af-a97cced3c620\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"F3\",\n                    \"type\": \"Button\",\n                    \"id\": \"ed352d11-aaea-4478-a18e-0eb4aa08a9aa\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"F4\",\n                    \"type\": \"Button\",\n                    \"id\": \"b745e2d5-3955-4f0d-817c-6f57ae56bf64\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"Ping\",\n                    \"type\": \"Button\",\n                    \"id\": \"7440d5fa-26ed-45c0-870c-0784d92b3a68\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                },\n                {\n                    \"name\": \"PushToTalk\",\n                    \"type\": \"Button\",\n                    \"id\": \"72f3d30d-af8f-4b1c-a4f7-230dc397c5df\",\n                    \"expectedControlType\": \"\",\n                    \"processors\": \"\",\n                    \"interactions\": \"\",\n                    \"initialStateCheck\": false\n                }\n            ],\n            \"bindings\": [\n                {\n                    \"name\": \"2D Vector\",\n                    \"id\": \"d687c5f2-e728-4f77-a1a9-134b479d50e4\",\n                    \"path\": \"2DVector\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Move\",\n                    \"isComposite\": true,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"up\",\n                    \"id\": \"60e42552-63b5-4f6a-a0a0-b8ffa4ed0820\",\n                    \"path\": \"<Keyboard>/w\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Move\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"down\",\n                    \"id\": \"fb8ea55c-d97a-4015-bc00-891e84e6aa60\",\n                    \"path\": \"<Keyboard>/s\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Move\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"left\",\n                    \"id\": \"63d69966-07f3-4e0b-afa9-264a2017d7f0\",\n                    \"path\": \"<Keyboard>/a\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Move\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"right\",\n                    \"id\": \"da43350f-806c-4b28-9bd0-7951cf3bc076\",\n                    \"path\": \"<Keyboard>/d\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Move\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"1c4f0f3e-aede-4444-b598-83be157bf524\",\n                    \"path\": \"<Gamepad>/leftStick\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Move\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"aa34ec15-e4ef-43dd-81d6-0cde3ba3dbea\",\n                    \"path\": \"<Keyboard>/space\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Jump\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"456a0c4b-71a2-44a0-b3b7-2855ee6b311d\",\n                    \"path\": \"<Gamepad>/buttonSouth\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Jump\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"e5e2314c-0520-4816-b79a-1016443095f0\",\n                    \"path\": \"<Keyboard>/ctrl\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Crouch\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"05e54a9d-ddac-448f-899b-5dcb55dfabf1\",\n                    \"path\": \"<Gamepad>/buttonEast\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Crouch\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"bda22789-09e5-4d96-a611-31e79672c01e\",\n                    \"path\": \"<Keyboard>/leftShift\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Sprint\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"6ceabf96-ebf8-4fcd-8905-3c74af189df5\",\n                    \"path\": \"<Gamepad>/leftTrigger\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Sprint\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"2b364bda-011f-4862-a237-63310d5569f2\",\n                    \"path\": \"<Keyboard>/e\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Interact\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"d2a39044-781b-4207-bcf9-b55fada0f0ff\",\n                    \"path\": \"<Gamepad>/buttonWest\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Interact\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"e2fbf232-d586-41f6-9195-ebd3cc7d960f\",\n                    \"path\": \"<Keyboard>/e\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"SkipUI\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"fb647880-60f3-4c7c-b0ad-42c4bbbb84f7\",\n                    \"path\": \"<Gamepad>/buttonWest\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"SkipUI\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"cbee88a6-6c48-427c-84eb-493277ccbb56\",\n                    \"path\": \"<Mouse>/rightButton\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Zoom\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"c000b279-2eef-4234-9a16-4d3d6ede73a8\",\n                    \"path\": \"<Gamepad>/buttonNorth\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Zoom\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"91eaf33e-d060-4882-aa2e-52ffbf656254\",\n                    \"path\": \"<Mouse>/delta\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Aim\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"2D Vector\",\n                    \"id\": \"6d074707-1344-4083-894b-45653355865a\",\n                    \"path\": \"2DVector(mode=2)\",\n                    \"interactions\": \"\",\n                    \"processors\": \"StickDeadzone(min=0.1,max=0.9)\",\n                    \"groups\": \"\",\n                    \"action\": \"Aim\",\n                    \"isComposite\": true,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"up\",\n                    \"id\": \"4bce01e8-3a9b-428f-ba94-0e2778b86fcc\",\n                    \"path\": \"<Gamepad>/rightStick/up\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Aim\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"down\",\n                    \"id\": \"9ddb12f4-8898-48ba-9e71-0ebd423735e4\",\n                    \"path\": \"<Gamepad>/rightStick/down\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Aim\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"left\",\n                    \"id\": \"d5a3c8d3-4510-4e10-893c-d07e2493b390\",\n                    \"path\": \"<Gamepad>/rightStick/left\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Aim\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"right\",\n                    \"id\": \"f74febb7-02f7-4fe9-8b48-7dc08cb5cb53\",\n                    \"path\": \"<Gamepad>/rightStick/right\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Aim\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"da87e40b-bf9b-4857-a604-e4f7a7f6a164\",\n                    \"path\": \"<Keyboard>/q\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"ThrowItem\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"e59e7485-7dbf-4a36-96ed-c9892809db07\",\n                    \"path\": \"<Gamepad>/rightTrigger\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"ThrowItem\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"27cb19bc-fd75-4a6a-9906-608b58df8f0d\",\n                    \"path\": \"<Keyboard>/1\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"ItemSelect\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"fba0e559-a13f-4969-b130-62543dc0b053\",\n                    \"path\": \"<Keyboard>/2\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"ItemSelect\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"43ec85f7-51d2-4cc2-93dd-fc2bba7c1b52\",\n                    \"path\": \"<Keyboard>/3\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"ItemSelect\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"abaf2d22-13f8-4c2f-b2df-b2d86b2e03a3\",\n                    \"path\": \"<Mouse>/leftButton\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"UseItem\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"0a2f30f2-7fbb-43bd-9729-fe41fe150635\",\n                    \"path\": \"<Gamepad>/rightShoulder\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"UseItem\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"1D Axis\",\n                    \"id\": \"e6b8e1dd-38e6-4c7e-8b49-ef3039465fbd\",\n                    \"path\": \"1DAxis\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Scroll\",\n                    \"isComposite\": true,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"negative\",\n                    \"id\": \"fb21ae81-1c94-4d73-8879-19e3db204827\",\n                    \"path\": \"<Mouse>/scroll/down\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Scroll\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"positive\",\n                    \"id\": \"977a3bc4-5747-469b-99ff-f18f4df03c18\",\n                    \"path\": \"<Mouse>/scroll/up\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Scroll\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": true\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"f45dd3c1-9706-4681-9468-e19143cfe468\",\n                    \"path\": \"<Keyboard>/backquote\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Console\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"57626e84-0cbf-43bb-8d76-045e0b8b43f5\",\n                    \"path\": \"<Keyboard>/escape\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"EscapeMenu\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"2d6cea85-82fe-476c-b5f4-efb7f965fa7d\",\n                    \"path\": \"<Gamepad>/start\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"EscapeMenu\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"880dcebc-d7c3-44ea-92ea-76a1f41f9b08\",\n                    \"path\": \"<Keyboard>/r\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"EmoteWheel\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"ef084e41-08c6-4017-8a0e-d2fd6207fd1e\",\n                    \"path\": \"<DualShockGamepad>/touchpadButton\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"EmoteWheel\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"b8652654-6a7a-416e-b57f-da3eefdcc6b7\",\n                    \"path\": \"<Keyboard>/f1\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"F1\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"123283aa-47a3-4c22-85ed-6e8603fa1847\",\n                    \"path\": \"<Keyboard>/f2\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"F2\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"d10551ac-385b-4abc-9572-5c2720d997c3\",\n                    \"path\": \"<Keyboard>/f3\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"F3\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"0ff68ab0-c6bb-4ece-937d-cfbffb491fdb\",\n                    \"path\": \"<Keyboard>/f4\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"F4\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"05db042d-cc7f-47b2-bf26-553e11a672dc\",\n                    \"path\": \"<Mouse>/middleButton\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Ping\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"c2f7bfc6-27f4-42bb-a10c-1991ab73b32c\",\n                    \"path\": \"<Gamepad>/rightStickPress\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"Ping\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                },\n                {\n                    \"name\": \"\",\n                    \"id\": \"ebce6cde-5841-4119-bc5f-4e525bfa629c\",\n                    \"path\": \"<Keyboard>/v\",\n                    \"interactions\": \"\",\n                    \"processors\": \"\",\n                    \"groups\": \"\",\n                    \"action\": \"PushToTalk\",\n                    \"isComposite\": false,\n                    \"isPartOfComposite\": false\n                }\n            ]\n        }\n    ],\n    \"controlSchemes\": []\n}");
		m_Player = asset.FindActionMap("Player", throwIfNotFound: true);
		m_Player_Move = m_Player.FindAction("Move", throwIfNotFound: true);
		m_Player_Aim = m_Player.FindAction("Aim", throwIfNotFound: true);
		m_Player_Jump = m_Player.FindAction("Jump", throwIfNotFound: true);
		m_Player_Crouch = m_Player.FindAction("Crouch", throwIfNotFound: true);
		m_Player_Sprint = m_Player.FindAction("Sprint", throwIfNotFound: true);
		m_Player_Interact = m_Player.FindAction("Interact", throwIfNotFound: true);
		m_Player_SkipUI = m_Player.FindAction("SkipUI", throwIfNotFound: true);
		m_Player_ThrowItem = m_Player.FindAction("ThrowItem", throwIfNotFound: true);
		m_Player_Zoom = m_Player.FindAction("Zoom", throwIfNotFound: true);
		m_Player_ItemSelect = m_Player.FindAction("ItemSelect", throwIfNotFound: true);
		m_Player_Scroll = m_Player.FindAction("Scroll", throwIfNotFound: true);
		m_Player_UseItem = m_Player.FindAction("UseItem", throwIfNotFound: true);
		m_Player_Console = m_Player.FindAction("Console", throwIfNotFound: true);
		m_Player_EscapeMenu = m_Player.FindAction("EscapeMenu", throwIfNotFound: true);
		m_Player_EmoteWheel = m_Player.FindAction("EmoteWheel", throwIfNotFound: true);
		m_Player_F1 = m_Player.FindAction("F1", throwIfNotFound: true);
		m_Player_F2 = m_Player.FindAction("F2", throwIfNotFound: true);
		m_Player_F3 = m_Player.FindAction("F3", throwIfNotFound: true);
		m_Player_F4 = m_Player.FindAction("F4", throwIfNotFound: true);
		m_Player_Ping = m_Player.FindAction("Ping", throwIfNotFound: true);
		m_Player_PushToTalk = m_Player.FindAction("PushToTalk", throwIfNotFound: true);
	}

	~InputActions()
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

}
