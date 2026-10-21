using System;
using ECM2.Walkthrough.Ex42;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Settings;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using StylizedWater3.UnderwaterRendering;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ECM2.Examples.FirstPerson
{
	public class FirstPersonCharacterInput : MonoBehaviour
	{
		public Action<bool> MovementEvent;

		[SerializeField]
		private Character _character;

		[SerializeField]
		private SprintAbility _sprintAbility;

		[SerializeField]
		private float _riseSpeed = 1f;

		[SerializeField]
		private float _diveSpeed = 1f;

		[SerializeField]
		private SwayHandler _sway;

		[Header("Weights")]
		[SerializeField]
		[Range(0f, 1f)]
		private float _walkBobWeight = 0.6f;

		[SerializeField]
		[Range(0f, 1f)]
		private float _runBobWeight = 1f;

		[SerializeField]
		[Range(0f, 1f)]
		private float _crouchBobWeight = 0.25f;

		[SerializeField]
		[Range(0f, 1f)]
		private float _swimBobWeight = 0.15f;

		private bool _isCrouched;

		private PPG_Player _player;

		private Vector2 _inputMove;

		private bool _isInputPressed;

		private bool _sprintActive;

		private bool _isHeadOut;

		private bool _isRegistered;

		public bool IsMovementReversed;

		private UnderwaterArea _underwaterArea;

		private bool _setHeadIn;

		private bool _setHeadOut;

		public bool IsSprintActive => _sprintActive;

		public bool IsHeadOut => _isHeadOut;

		public Character Character => _character;

		public SprintAbility SprintAbility => _sprintAbility;

		public UnderwaterArea UnderwaterArea => _underwaterArea;

		public bool IsGroundLocomotionBlocked
		{
			get
			{
				if (!(_character == null) && !_character.IsSwimming())
				{
					return _character.IsSeaScooter();
				}
				return true;
			}
		}

		public bool IsInputPaused
		{
			get
			{
				return _isInputPressed;
			}
			set
			{
				_isInputPressed = value;
				if (value)
				{
					_inputMove = Vector2.zero;
					StopSprintState();
					_character.UnCrouch();
					_character.StopJumping();
				}
			}
		}

		public Vector2 InputMove
		{
			get
			{
				return _inputMove;
			}
			set
			{
				MovementEvent?.Invoke(value != Vector2.zero);
				_inputMove = value;
				if (_inputMove == Vector2.zero)
				{
					StopSprintState();
				}
			}
		}

		public void Register(PPG_Player player)
		{
			_player = player;
			_isRegistered = _player != null;
			if (!_isRegistered)
			{
				UtilityMethods.Log("[FirstPersonCharacterInput] Register failed. Player is null.", LogType.Error);
				return;
			}
			if (_sprintAbility != null)
			{
				_sprintAbility.enabled = true;
			}
			ApplyToggleSprintSetting(RuntimeSettings.ToggleSprintEnabled);
			Singleton<InputManager>.Instance.InputHandler.Move += OnMove;
			Singleton<InputManager>.Instance.InputHandler.Sprint += OnSprint;
			Singleton<InputManager>.Instance.InputHandler.Jump += OnJump;
			Singleton<InputManager>.Instance.InputHandler.Crouch += OnCrouch;
			if (_character != null)
			{
				_character.Crouched += Crouched;
				_character.UnCrouched += UnCrouched;
				_character.Landed += Landed;
			}
			else
			{
				UtilityMethods.Log("[FirstPersonCharacterInput] Register missing Character reference.", LogType.Error);
			}
			RuntimeSettings.ToggleSprintChanged += ApplyToggleSprintSetting;
			_isHeadOut = false;
			_underwaterArea = ((UnderwaterArea.Instances != null && UnderwaterArea.Instances.Count > 0) ? UnderwaterArea.Instances[0] : null);
			if (_underwaterArea != null)
			{
				UtilityMethods.Log("[FirsPersonCharacterInput] Register _underwaterArea found!");
			}
		}

		public void UnRegister()
		{
			_isRegistered = false;
			Singleton<InputManager>.Instance.InputHandler.Move -= OnMove;
			Singleton<InputManager>.Instance.InputHandler.Sprint -= OnSprint;
			Singleton<InputManager>.Instance.InputHandler.Jump -= OnJump;
			Singleton<InputManager>.Instance.InputHandler.Crouch -= OnCrouch;
			if (_character != null)
			{
				_character.Landed -= Landed;
				_character.Crouched -= Crouched;
				_character.UnCrouched -= UnCrouched;
			}
			RuntimeSettings.ToggleSprintChanged -= ApplyToggleSprintSetting;
		}

		private void ApplyToggleSprintSetting(bool useToggleSprint)
		{
			if (!(_sprintAbility == null))
			{
				bool useToggleSprint2 = _sprintAbility.UseToggleSprint;
				_sprintAbility.UseToggleSprint = useToggleSprint;
				if (useToggleSprint2 && !useToggleSprint && _sprintActive)
				{
					StopSprintState();
				}
			}
		}

		private void OnCrouch(InputActionPhase phase)
		{
			if (_isRegistered && !(_character == null) && !IsInputPaused)
			{
				switch (phase)
				{
				case InputActionPhase.Started:
					_character.Crouch();
					break;
				case InputActionPhase.Canceled:
					_character.UnCrouch();
					break;
				}
			}
		}

		private void OnJump(InputActionPhase phase)
		{
			if (!_isRegistered || _character == null || _player == null || IsInputPaused)
			{
				return;
			}
			switch (phase)
			{
			case InputActionPhase.Started:
				_character.Jump();
				if (!Character.IsSwimming())
				{
					_player.SurfaceChecker.HandleJump();
				}
				_player.TryTriggerJumpAnimator();
				break;
			case InputActionPhase.Canceled:
				_character.StopJumping();
				break;
			}
		}

		private void OnMove(InputActionPhase phase, Vector2 input)
		{
			if (_isRegistered && !(_player == null) && !IsInputPaused)
			{
				switch (phase)
				{
				case InputActionPhase.Performed:
					InputMove = input;
					break;
				case InputActionPhase.Canceled:
					InputMove = Vector2.zero;
					break;
				}
			}
		}

		private void OnSprint(InputActionPhase phase)
		{
			if (!_isRegistered || _player == null || _sprintAbility == null)
			{
				return;
			}
			switch (phase)
			{
			case InputActionPhase.Canceled:
				if (!_sprintAbility.UseToggleSprint)
				{
					StopSprintState();
				}
				break;
			case InputActionPhase.Started:
				if (!IsInputPaused && !_player.Harpoon.OnAim)
				{
					if (_sprintAbility.UseToggleSprint && _sprintActive)
					{
						StopSprintState();
					}
					else if (!_sprintActive)
					{
						_sprintAbility.Sprint();
						_player.SetTransientCameraFovOffset(5f, 0.5f);
						_player.SetSprinting(isSprinting: true);
						_sprintActive = true;
					}
				}
				break;
			}
		}

		public void CharacterInputUpdate()
		{
			if (!_isRegistered || _player == null || _character == null)
			{
				return;
			}
			Vector3 zero = Vector3.zero;
			float num = InputMove.x;
			float num2 = InputMove.y;
			if (IsMovementReversed)
			{
				num = 0f - num;
				num2 = 0f - num2;
			}
			if (_character.IsSeaScooter())
			{
				num2 = Mathf.Max(0f, num2);
				Vector3 vector = (_character.camera ? _character.cameraTransform.forward : _character.GetForwardVector());
				zero += vector * num2;
				if (_character.jumpInputPressed)
				{
					zero += Vector3.up;
				}
				if (_character.crouchInputPressed)
				{
					zero += Vector3.down;
				}
				UpdateWaterHeadState();
			}
			else if (_character.IsSwimming())
			{
				zero += _character.GetRightVector() * num;
				Vector3 vector2 = (_character.camera ? _character.cameraTransform.forward : _character.GetForwardVector());
				zero += vector2 * num2;
				UpdateWaterHeadState();
				if (_character.jumpInputPressed)
				{
					if (!_isHeadOut)
					{
						zero += _character.GetUpVector();
					}
					else
					{
						_character.SetMovementMode(Character.MovementMode.Falling);
						_character.LaunchCharacter(_character.GetUpVector() * _riseSpeed, overrideVerticalVelocity: true);
					}
				}
				if (_character.crouchInputPressed)
				{
					zero += -_character.GetUpVector() * _diveSpeed;
				}
			}
			else
			{
				zero += _character.GetRightVector() * num;
				zero += _character.GetForwardVector() * num2;
			}
			_character.SetMovementDirection(zero);
			UpdateCameraBob();
			UpdateViewmodelBob();
		}

		private void UpdateWaterHeadState()
		{
			float num = ((!(_underwaterArea != null)) ? _character.CalcImmersionDepth() : _underwaterArea.CameraSubmersionAmount(_player.MainCamera));
			if (num <= 0.75f)
			{
				if (!_setHeadOut)
				{
					OutUnderWater();
					_player.SetRippleTrail(s: false);
					UtilityMethods.Log("[FirsPersonCharacterInput] CharacterInputUpdate Depth:" + num);
					UtilityMethods.Log("[FirsPersonCharacterInput] CharacterInputUpdate HeadOut");
				}
				_player.SetRippleTrail(s: true);
			}
			else if (!_setHeadIn)
			{
				_player.SetUnderwaterState(isUnderwater: true);
				if (Singleton<SoundManager>.Instance != null)
				{
					Singleton<SoundManager>.Instance.EnterUnderwater();
				}
				if (Singleton<OceanFXManager>.Instance != null)
				{
					Singleton<OceanFXManager>.Instance.SetUnderwater(isUnderwater: true);
				}
				_setHeadIn = true;
				_setHeadOut = false;
				_isHeadOut = false;
				_player.SetRippleTrail(s: false);
				UtilityMethods.Log("[FirsPersonCharacterInput] CharacterInputUpdate Depth:" + num);
				UtilityMethods.Log("[FirsPersonCharacterInput] CharacterInputUpdate HeadIn");
			}
		}

		public void OutUnderWater()
		{
			if (!(_player == null))
			{
				_player.SetUnderwaterState(isUnderwater: false);
				if (Singleton<SoundManager>.Instance != null)
				{
					Singleton<SoundManager>.Instance.ExitUnderwater();
				}
				if (Singleton<OceanFXManager>.Instance != null)
				{
					Singleton<OceanFXManager>.Instance.SetUnderwater(isUnderwater: false);
				}
				_setHeadIn = false;
				_setHeadOut = true;
				_isHeadOut = true;
			}
		}

		private void Landed(Vector3 landingVelocity)
		{
			if (!(_player == null) && !(_character == null) && !_character.IsSwimming() && !_character.IsSeaScooter())
			{
				float num = Mathf.Abs(landingVelocity.y);
				if (num <= 0.01f)
				{
					num = landingVelocity.magnitude;
				}
				_player.PlayLandingCameraPolish(num);
			}
		}

		private void Crouched()
		{
			if (!(_player == null))
			{
				_isCrouched = true;
				_player.SetCameraCrouch(status: true);
			}
		}

		private void UnCrouched()
		{
			if (!(_player == null))
			{
				_isCrouched = false;
				_player.SetCameraCrouch(status: false);
			}
		}

		private void UpdateCameraBob()
		{
			if (!(_player == null) && !(_character == null))
			{
				bool flag = _character.IsSwimming();
				bool flag2 = _character.IsSeaScooter();
				Vector2 inputMove = InputMove;
				bool flag3 = inputMove.sqrMagnitude > 0.0001f;
				float strafe = Mathf.Clamp(inputMove.x, -1f, 1f);
				bool flag4 = _sprintActive;
				float speed;
				if (!flag3)
				{
					speed = 0f;
				}
				else if (flag2)
				{
					speed = 0f;
					flag3 = false;
				}
				else if (flag)
				{
					speed = _swimBobWeight * Mathf.Clamp01(inputMove.magnitude);
					flag4 = false;
				}
				else if (_isCrouched)
				{
					speed = _crouchBobWeight * Mathf.Clamp01(inputMove.magnitude);
					flag4 = false;
				}
				else
				{
					speed = (flag4 ? _runBobWeight : _walkBobWeight) * Mathf.Clamp01(inputMove.magnitude);
				}
				_player.SetCameraBobState(flag3, speed, flag4, strafe);
			}
		}

		private void UpdateViewmodelBob()
		{
			if (!(_sway == null))
			{
				Vector2 inputMove = InputMove;
				bool flag = inputMove.sqrMagnitude > 0.0001f;
				float strafe = Mathf.Clamp(inputMove.x, -1f, 1f);
				float speed = (flag ? Mathf.Clamp01(inputMove.magnitude) : 0f);
				bool isRunning = _sprintActive;
				if (_character.IsSwimming() || _character.IsSeaScooter())
				{
					flag = false;
					speed = 0f;
					isRunning = false;
				}
				_sway.SetMoveState(flag, speed, isRunning, strafe);
				_sway.SetGrounded(_character.IsGrounded());
			}
		}

		private void StopSprintState(bool resetFov = true)
		{
			if (!(_sprintAbility == null))
			{
				_sprintAbility.StopSprinting();
				if (_player != null)
				{
					_player.SetSprinting(isSprinting: false);
				}
				_sprintActive = false;
				if (resetFov && _player != null)
				{
					_player.ResetCameraFov(0.2f);
				}
			}
		}
	}
}
