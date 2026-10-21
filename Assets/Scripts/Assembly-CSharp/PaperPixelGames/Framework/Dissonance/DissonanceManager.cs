using System.Collections.Generic;
using Dissonance;
using Kamgam.SettingsGenerator;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Framework.Dissonance
{
	public class DissonanceManager : Singleton<DissonanceManager>
	{
		[Header("Dissonance")]
		[SerializeField]
		private DissonanceComms _comms;

		[SerializeField]
		private VoiceProximityBroadcastTrigger _broadcastTrigger;

		[SerializeField]
		private VoiceProximityReceiptTrigger _receiptTrigger;

		[Header("Settings Connections")]
		[SerializeField]
		private MicrophoneConnectionSO _microphoneConnectionSO;

		[SerializeField]
		private PTTConnectionSO _pushToTalkConnectionSO;

		private PPG_Player _localPlayer;

		private MicrophoneConnection _microphoneConnection;

		private PTTConnection _pushToTalkConnection;

		private bool _isInitialized;

		private CommActivationMode _activationMode = CommActivationMode.VoiceActivation;

		private bool _pushToTalkInputBound;

		private bool _pushToTalkPressed;

		public DissonanceComms Comms => _comms;

		public bool IsMuted
		{
			get
			{
				if (_comms != null)
				{
					return _comms.IsMuted;
				}
				return false;
			}
		}

		private void Start()
		{
			BindSettingsListeners();
			ApplyAllSettingsFromConnections();
		}

		public void Init(PPG_Player player)
		{
			if (_comms == null)
			{
				UtilityMethods.Log("[Dissonance] Comms is null in Init");
				return;
			}
			_localPlayer = player;
			if (_isInitialized)
			{
				UtilityMethods.Log($"[Dissonance] Init refreshed local player. LocalPlayer: {_localPlayer != null}");
				return;
			}
			UtilityMethods.Log("[Dissonance] Init - " + _comms.LocalPlayerName);
			SetMute(mute: false);
			BindPushToTalkInput();
			_isInitialized = true;
		}

		private void OnDestroy()
		{
			UnbindPushToTalkInput();
			UnbindSettingsListeners();
			_isInitialized = false;
		}

		private void BindPushToTalkInput()
		{
			if (!_pushToTalkInputBound)
			{
				if (Singleton<InputManager>.Instance == null || Singleton<InputManager>.Instance.InputHandler == null)
				{
					UtilityMethods.Log("[Dissonance] Cannot bind PushToTalk input. InputHandler is null.");
					return;
				}
				Singleton<InputManager>.Instance.InputHandler.PushToTalk -= OnPushTheTalk;
				Singleton<InputManager>.Instance.InputHandler.PushToTalk += OnPushTheTalk;
				_pushToTalkInputBound = true;
				UtilityMethods.Log("[Dissonance] PushToTalk input bound.");
			}
		}

		private void UnbindPushToTalkInput()
		{
			if (_pushToTalkInputBound)
			{
				if (Singleton<InputManager>.Instance != null && Singleton<InputManager>.Instance.InputHandler != null)
				{
					Singleton<InputManager>.Instance.InputHandler.PushToTalk -= OnPushTheTalk;
				}
				_pushToTalkInputBound = false;
			}
		}

		private void OnPushTheTalk(InputActionPhase phase)
		{
			if (!(_broadcastTrigger == null) && _activationMode == CommActivationMode.PushToTalk)
			{
				switch (phase)
				{
				case InputActionPhase.Started:
				case InputActionPhase.Performed:
					SetPushToTalkPressed(pressed: true);
					break;
				case InputActionPhase.Canceled:
					SetPushToTalkPressed(pressed: false);
					break;
				}
			}
		}

		private void SetPushToTalkPressed(bool pressed)
		{
			if (!(_broadcastTrigger == null) && _pushToTalkPressed != pressed)
			{
				_pushToTalkPressed = pressed;
				_broadcastTrigger.Mode = (pressed ? CommActivationMode.Open : CommActivationMode.None);
				SetLocalSpeakingVisual(pressed);
				UtilityMethods.Log("[Dissonance] PTT " + (pressed ? "Pressed" : "Released") + $"\n   Mode: {_activationMode}" + $"\n   BroadcastMode: {_broadcastTrigger.Mode}" + $"\n   LocalPlayer: {_localPlayer != null}");
			}
		}

		public void SetActivationMode(CommActivationMode mode)
		{
			if (!(_broadcastTrigger == null))
			{
				_activationMode = mode;
				_pushToTalkPressed = false;
				switch (_activationMode)
				{
				case CommActivationMode.PushToTalk:
					_broadcastTrigger.Mode = CommActivationMode.None;
					break;
				case CommActivationMode.VoiceActivation:
					_broadcastTrigger.Mode = CommActivationMode.VoiceActivation;
					break;
				default:
					_broadcastTrigger.Mode = CommActivationMode.None;
					break;
				}
				SetLocalSpeakingVisual(active: false);
				UtilityMethods.Log("[Dissonance] Activation mode set" + $"\n   ActivationMode: {_activationMode}" + $"\n   BroadcastMode: {_broadcastTrigger.Mode}");
			}
		}

		public CommActivationMode GetActivationMode()
		{
			return _activationMode;
		}

		private void SetLocalSpeakingVisual(bool active)
		{
			if (_localPlayer == null)
			{
				UtilityMethods.Log($"[Dissonance] Cannot set local speaking visual. LocalPlayer is null. Active: {active}");
			}
			else
			{
				_localPlayer.ChangePTTChildStatus(active);
			}
		}

		private void BindSettingsListeners()
		{
			BindMicrophoneListener();
			BindPushToTalkListener();
		}

		private void UnbindSettingsListeners()
		{
			UnbindMicrophoneListener();
			UnbindPushToTalkListener();
		}

		private void BindMicrophoneListener()
		{
			if (_microphoneConnectionSO == null)
			{
				UtilityMethods.Log("[Dissonance] MicrophoneConnectionSO is null.");
				return;
			}
			_microphoneConnection = _microphoneConnectionSO.GetConnection() as MicrophoneConnection;
			if (_microphoneConnection == null)
			{
				UtilityMethods.Log("[Dissonance] Failed to resolve MicrophoneConnection.");
				return;
			}
			_microphoneConnection.RemoveChangeListener(OnMicrophoneChanged);
			_microphoneConnection.AddChangeListener(OnMicrophoneChanged);
			UtilityMethods.Log("[Dissonance] Microphone listener bound.");
		}

		private void UnbindMicrophoneListener()
		{
			if (_microphoneConnection != null)
			{
				_microphoneConnection.RemoveChangeListener(OnMicrophoneChanged);
				_microphoneConnection = null;
			}
		}

		private void BindPushToTalkListener()
		{
			if (_pushToTalkConnectionSO == null)
			{
				UtilityMethods.Log("[Dissonance] PushToTalkConnectionSO is null.");
				return;
			}
			_pushToTalkConnection = _pushToTalkConnectionSO.GetConnection() as PTTConnection;
			if (_pushToTalkConnection == null)
			{
				UtilityMethods.Log("[Dissonance] Failed to resolve PushToTalk connection.");
				return;
			}
			_pushToTalkConnection.RemoveChangeListener(OnPushToTalkSettingChanged);
			_pushToTalkConnection.AddChangeListener(OnPushToTalkSettingChanged);
			UtilityMethods.Log("[Dissonance] PushToTalk listener bound.");
		}

		private void UnbindPushToTalkListener()
		{
			if (_pushToTalkConnection != null)
			{
				_pushToTalkConnection.RemoveChangeListener(OnPushToTalkSettingChanged);
				_pushToTalkConnection = null;
			}
		}

		private void ApplyAllSettingsFromConnections()
		{
			ApplyMicrophoneFromConnection();
			ApplyPushToTalkFromConnection();
		}

		private void OnMicrophoneChanged(int _)
		{
			ApplyMicrophoneFromConnection();
		}

		private void ApplyMicrophoneFromConnection()
		{
			if (_microphoneConnection == null)
			{
				UtilityMethods.Log("[Dissonance] Cannot apply microphone. Connection is null.");
				return;
			}
			_microphoneConnection.GetOptionLabels();
			string selectedDeviceName = _microphoneConnection.GetSelectedDeviceName();
			SetMicrophoneDevice(selectedDeviceName);
		}

		private void OnPushToTalkSettingChanged(bool isPushToTalkEnabled)
		{
			ApplyPushToTalkSetting(isPushToTalkEnabled);
		}

		private void ApplyPushToTalkFromConnection()
		{
			if (_pushToTalkConnection == null)
			{
				UtilityMethods.Log("[Dissonance] Cannot apply PushToTalk. Connection is null.");
				return;
			}
			bool isPushToTalkEnabled = _pushToTalkConnection.Get();
			ApplyPushToTalkSetting(isPushToTalkEnabled);
		}

		private void ApplyPushToTalkSetting(bool isPushToTalkEnabled)
		{
			SetActivationMode((!isPushToTalkEnabled) ? CommActivationMode.VoiceActivation : CommActivationMode.PushToTalk);
			UtilityMethods.Log($"[Dissonance] PushToTalk applied: {isPushToTalkEnabled}");
		}

		public List<string> GetAvailableMicrophones()
		{
			List<string> list = new List<string>();
			if (_comms == null)
			{
				return list;
			}
			_comms.GetMicrophoneDevices(list);
			return list;
		}

		public string GetCurrentMicrophone()
		{
			if (!(_comms != null))
			{
				return null;
			}
			return _comms.MicrophoneName;
		}

		public void SetMicrophoneDevice(string deviceName)
		{
			if (_comms == null)
			{
				UtilityMethods.Log("[Dissonance] Cannot set microphone. Comms is null.");
				return;
			}
			bool flag = string.IsNullOrEmpty(deviceName);
			if (_comms.MicrophoneName == deviceName || (flag && string.IsNullOrEmpty(_comms.MicrophoneName)))
			{
				return;
			}
			if (!flag)
			{
				List<string> list = new List<string>();
				_comms.GetMicrophoneDevices(list);
				if (!list.Contains(deviceName))
				{
					UtilityMethods.Log("[Dissonance] Microphone '" + deviceName + "' not found.");
					return;
				}
			}
			_comms.MicrophoneName = deviceName;
			UtilityMethods.Log("[Dissonance] Microphone set to: " + (flag ? "Default" : deviceName));
		}

		public void SetMute(bool mute)
		{
			if (!(_comms == null))
			{
				_comms.IsMuted = mute;
				EventBus<MuteToggleEvent>.Raise(new MuteToggleEvent
				{
					Toggle = mute
				});
				UtilityMethods.Log($"[Dissonance] Mute set to: {mute}");
			}
		}

		public bool TrySetPlayerVolumeByPlayerId(string playerId, float volume)
		{
			if (_comms == null || string.IsNullOrWhiteSpace(playerId))
			{
				return false;
			}
			VoicePlayerState voicePlayerState = _comms.FindPlayer(playerId);
			if (voicePlayerState == null)
			{
				UtilityMethods.Log("[Dissonance] Voice player not found for volume. PlayerId: " + playerId);
				return false;
			}
			voicePlayerState.Volume = Mathf.Clamp01(volume);
			UtilityMethods.Log("[Dissonance] Player volume set\n   PlayerId: " + playerId + $"\n   Volume: {voicePlayerState.Volume:0.00}");
			return true;
		}

		public float GetPlayerVolumeByPlayerId(string playerId, float fallback = 1f)
		{
			if (_comms == null || string.IsNullOrWhiteSpace(playerId))
			{
				return fallback;
			}
			return _comms.FindPlayer(playerId)?.Volume ?? fallback;
		}
	}
}
