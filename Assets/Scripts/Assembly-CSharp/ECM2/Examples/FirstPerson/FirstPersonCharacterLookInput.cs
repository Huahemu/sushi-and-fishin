using Kamgam.SettingsGenerator;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Settings;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ECM2.Examples.FirstPerson
{
	public class FirstPersonCharacterLookInput : MonoBehaviour
	{
		[SerializeField]
		private PPG_Player _ppgPlayer;

		[Space]
		[SerializeField]
		private FirstPersonCharacter _character;

		[Space]
		[SerializeField]
		private SensitivityConnectionSO _sensConn;

		[SerializeField]
		private InvertXaxisConnectionSO _invertXConn;

		[SerializeField]
		private InvertYaxisConnectionSO _invertYConn;

		[Space(15f)]
		[Tooltip("How far in degrees can you move the camera down.")]
		public float minPitch = -80f;

		[Tooltip("How far in degrees can you move the camera up.")]
		public float maxPitch = 80f;

		[SerializeField]
		private SwayHandler _sway;

		private Vector2 _lookInput;

		private float _pendingYaw;

		private float _pendingPitch;

		private float _sens;

		private bool _invertX;

		private bool _invertY;

		private SensitivityConnection _sensitivityConnection;

		private InvertXaxisConnection _invertXConnection;

		private InvertYaxisConnection _invertYConnection;

		public bool PauseLook { get; set; }

		public float SensivityFactor { get; set; }

		public Vector2 LookInput => _lookInput;

		public void Register()
		{
			Singleton<InputManager>.Instance.InputHandler.Look += OnLook;
			SensivityFactor = 1f;
			BindSensitivityConnection();
			BindInvertConnections();
		}

		public void UnRegister()
		{
			Singleton<InputManager>.Instance.InputHandler.Look -= OnLook;
			UnbindSensitivityConnection();
			UnbindInvertConnections();
		}

		private void BindInvertConnections()
		{
			_invertX = RuntimeSettings.InvertXAxisEnabled;
			_invertY = RuntimeSettings.InvertYAxisEnabled;
			if (_invertXConn != null)
			{
				_invertXConnection = _invertXConn.GetConnection() as InvertXaxisConnection;
				if (_invertXConnection != null)
				{
					_invertXConnection.RemoveChangeListener(OnInvertXChanged);
					_invertXConnection.AddChangeListener(OnInvertXChanged);
					_invertX = _invertXConnection.Get();
				}
			}
			if (_invertYConn != null)
			{
				_invertYConnection = _invertYConn.GetConnection() as InvertYaxisConnection;
				if (_invertYConnection != null)
				{
					_invertYConnection.RemoveChangeListener(OnInvertYChanged);
					_invertYConnection.AddChangeListener(OnInvertYChanged);
					_invertY = _invertYConnection.Get();
				}
			}
		}

		private void UnbindInvertConnections()
		{
			if (_invertXConnection != null)
			{
				_invertXConnection.RemoveChangeListener(OnInvertXChanged);
				_invertXConnection = null;
			}
			if (_invertYConnection != null)
			{
				_invertYConnection.RemoveChangeListener(OnInvertYChanged);
				_invertYConnection = null;
			}
		}

		private void OnInvertXChanged(bool enabled)
		{
			_invertX = enabled;
		}

		private void OnInvertYChanged(bool enabled)
		{
			_invertY = enabled;
		}

		private void BindSensitivityConnection()
		{
			_sens = RuntimeSettings.Sensitivity;
			if (!(_sensConn == null))
			{
				_sensitivityConnection = _sensConn.GetConnection() as SensitivityConnection;
				if (_sensitivityConnection != null)
				{
					_sensitivityConnection.RemoveChangeListener(OnSensitivityChanged);
					_sensitivityConnection.AddChangeListener(OnSensitivityChanged);
					_sens = _sensitivityConnection.Get();
				}
			}
		}

		private void UnbindSensitivityConnection()
		{
			if (_sensitivityConnection != null)
			{
				_sensitivityConnection.RemoveChangeListener(OnSensitivityChanged);
				_sensitivityConnection = null;
			}
		}

		private void OnSensitivityChanged(float sensitivity)
		{
			_sens = sensitivity;
		}

		private void OnLook(InputActionPhase phase, Vector2 input, bool isMouse)
		{
			if (!PauseLook && phase == InputActionPhase.Performed)
			{
				_lookInput = input;
			}
		}

		public void LookInputUpdate()
		{
			if (_character == null || PauseLook)
			{
				return;
			}
			float num = _sens;
			if (SensivityFactor != 1f)
			{
				num = SensivityFactor;
			}
			Vector2 delta = _lookInput * num * 0.5f;
			float num2 = delta.x;
			float num3 = delta.y;
			if (_invertX)
			{
				num2 = 0f - num2;
			}
			if (_invertY)
			{
				num3 = 0f - num3;
			}
			if (_ppgPlayer.TryApplyEmoteFpsLookDelta(new Vector2(num2, num3)))
			{
				_lookInput = Vector2.zero;
				if (_sway != null)
				{
					_sway.AddLookDelta(delta);
				}
				return;
			}
			_pendingYaw += num2;
			_pendingPitch += num3;
			_lookInput = Vector2.zero;
			_character.AddControlYawInput(_pendingYaw);
			_character.AddControlPitchInput(_pendingPitch, minPitch, maxPitch);
			_pendingYaw = 0f;
			_pendingPitch = 0f;
			if (_sway != null)
			{
				_sway.AddLookDelta(delta);
			}
		}
	}
}
