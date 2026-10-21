using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.Patterns;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Blackjack
{
	[RequireComponent(typeof(NetworkObject))]
	[RequireComponent(typeof(Collider))]
	public class BlackjackSeat : BaseUsable
	{
		[Header("Blackjack Seat")]
		[SerializeField]
		private BlackjackTable _table;

		[SerializeField]
		[Range(0f, 0f)]
		private int _seatIndex;

		[Header("Seated Look")]
		[SerializeField]
		[Range(0f, 90f)]
		private float _maxLookYaw = 35f;

		[SerializeField]
		[Min(0f)]
		private float _lookSensitivity = 0.08f;

		private PPG_Player _localPlayer;

		private bool _isSeatedLookEnabled;

		private Quaternion _usableCameraBaseLocalRotation;

		private float _currentLookYaw;

		public int SeatIndex => _seatIndex;

		public void Configure(BlackjackTable table, int seatIndex)
		{
			_table = table;
			_seatIndex = Mathf.Clamp(seatIndex, 0, 0);
		}

		public override bool IsAvailable()
		{
			if (base.IsAvailable() && _table != null)
			{
				return _table.CanJoinSeat(_seatIndex);
			}
			return false;
		}

		public override void ServerUseEnter(Interactor interactor)
		{
			if (!(_table == null) && _table.ServerTrySeatPlayer(_seatIndex, interactor))
			{
				base.ServerUseEnter(interactor);
			}
		}

		public override void ServerUseExit(Interactor interactor)
		{
			_table?.ServerRemoveSeatedPlayer(_seatIndex, interactor.OwnerClientId);
			base.ServerUseExit(interactor);
		}

		public override void ClientUseEnter(Interactor interactor)
		{
			base.ClientUseEnter(interactor);
			_localPlayer = interactor.Player;
			_currentLookYaw = 0f;
			if (base.UsableCam != null)
			{
				_usableCameraBaseLocalRotation = base.UsableCam.transform.localRotation;
			}
			interactor.Player.SwitchTo("UI", cursorVisible: true);
			RegisterBlackjackExitInput();
			_table?.OpenLocalTable(_seatIndex);
		}

		public override void ClientUseExit(Interactor interactor)
		{
			ClearLocalSeatState();
			_table?.CloseLocalTable(_seatIndex);
			base.ClientUseExit(interactor);
			interactor.Player.SwitchTo("Player", cursorVisible: false);
		}

		private void Update()
		{
			if (_localPlayer == null)
			{
				return;
			}
			if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
			{
				RequestUseExit();
			}
			SetSeatedLookEnabled(_table != null && _table.Phase != BlackjackTablePhase.Betting);
			if (_isSeatedLookEnabled && Mouse.current != null)
			{
				Vector2 delta = Mouse.current.delta.ReadValue() * _lookSensitivity;
				if (delta.sqrMagnitude > 1E-06f)
				{
					ApplyLookDelta(delta);
				}
			}
		}

		private void ApplyLookDelta(Vector2 delta)
		{
			if (base.UsableCam == null)
			{
				_localPlayer.ApplyLimitedFreeLookDelta(new Vector2(delta.x, 0f), 0f);
				return;
			}
			_currentLookYaw = Mathf.Clamp(_currentLookYaw + delta.x, 0f - _maxLookYaw, _maxLookYaw);
			Quaternion quaternion = Quaternion.AngleAxis(_currentLookYaw, Vector3.up);
			base.UsableCam.transform.localRotation = _usableCameraBaseLocalRotation * quaternion;
		}

		private void SetSeatedLookEnabled(bool enabled)
		{
			if (_isSeatedLookEnabled != enabled)
			{
				_isSeatedLookEnabled = enabled;
				_currentLookYaw = 0f;
				if (base.UsableCam != null)
				{
					base.UsableCam.transform.localRotation = _usableCameraBaseLocalRotation;
				}
				else if (enabled)
				{
					_localPlayer?.BeginLimitedFreeLook(_maxLookYaw);
				}
				else
				{
					_localPlayer?.EndLimitedFreeLook();
				}
			}
		}

		public void RequestLocalExit()
		{
			RequestUseExit();
		}

		private void RegisterBlackjackExitInput()
		{
			if (!(Singleton<InputManager>.Instance == null) && !(Singleton<InputManager>.Instance.InputHandler == null))
			{
				Singleton<InputManager>.Instance.InputHandler.Cancel -= OnBlackjackCancelInput;
				Singleton<InputManager>.Instance.InputHandler.Cancel += OnBlackjackCancelInput;
			}
		}

		private void UnregisterBlackjackExitInput()
		{
			if (!(Singleton<InputManager>.Instance == null) && !(Singleton<InputManager>.Instance.InputHandler == null))
			{
				Singleton<InputManager>.Instance.InputHandler.Cancel -= OnBlackjackCancelInput;
			}
		}

		private void OnBlackjackCancelInput(InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started)
			{
				RequestUseExit();
			}
		}

		private void OnDisable()
		{
			ClearLocalSeatState();
		}

		private void ClearLocalSeatState()
		{
			UnregisterBlackjackExitInput();
			SetSeatedLookEnabled(enabled: false);
			_localPlayer = null;
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			base.__initializeRpcs();
		}

		protected override string __getTypeName()
		{
			return "BlackjackSeat";
		}
	}
}
