namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::Dissonance;
using global::UnityEngine;

public class VoipManipulationManager : MonoBehaviour
{
	public enum VoipFX
	{
		Default = 0,
		Wobble = 1,
		Low = 2,
		High = 3,
		Radio = 4
	}

	public DissonanceComms dissonanceComms;

	private Dictionary<string, VoipFX> _desiredVoipFX = new Dictionary<string, VoipFX>();

	private Dictionary<string, bool> _mouthFX = new Dictionary<string, bool>();

	private Dictionary<string, int> _desiredVoipBus = new Dictionary<string, int>();

	public event Action OnDesiredVoipFXChanged;

	public event Action OnDesiredVoipBusChanged;

	public event Action OnMouthFXChanged;

	private void OnValidate()
	{
		OnDesiredVoipFXChanged?.Invoke();
		OnDesiredVoipBusChanged?.Invoke();
	}

	private void OnEnable()
	{
		dissonanceComms.OnPlayerJoinedSession += AddPlayer;
		dissonanceComms.OnPlayerLeftSession += RemovePlayer;
	}

	private void OnDisable()
	{
		dissonanceComms.OnPlayerJoinedSession -= AddPlayer;
		dissonanceComms.OnPlayerLeftSession -= RemovePlayer;
	}

	private void AddPlayer(VoicePlayerState playerVoice)
	{
		_desiredVoipFX.TryAdd(playerVoice.Name, VoipFX.Default);
		_mouthFX.TryAdd(playerVoice.Name, value: false);
		for (int i = 0; i < 10; i++)
		{
			if (!_desiredVoipBus.ContainsValue(i))
			{
				_desiredVoipBus.TryAdd(playerVoice.Name, i);
				OnDesiredVoipBusChanged?.Invoke();
				break;
			}
		}
	}

	private void RemovePlayer(VoicePlayerState playerVoice)
	{
		if (_desiredVoipFX.ContainsKey(playerVoice.Name))
		{
			_desiredVoipFX.Remove(playerVoice.Name);
		}
		if (_desiredVoipBus.ContainsKey(playerVoice.Name))
		{
			_desiredVoipBus.Remove(playerVoice.Name);
		}
		if (_mouthFX.ContainsKey(playerVoice.Name))
		{
			_mouthFX.Remove(playerVoice.Name);
		}
	}

	public void AssignPlayerVoipFX(string playerName, VoipFX voipFX)
	{
		if (playerName == dissonanceComms.LocalPlayerName)
		{
			Debug.Log("VoiceFX on Local Player: " + voipFX);
			return;
		}
		if (!_desiredVoipFX.ContainsKey(playerName))
		{
			Debug.LogError("Can't find player in the dict");
			return;
		}
		VoipFX num = _desiredVoipFX[playerName];
		_desiredVoipFX[playerName] = voipFX;
		if (num != voipFX)
		{
			OnDesiredVoipFXChanged?.Invoke();
			Debug.Log("VoiceFX on " + playerName + ": " + voipFX);
		}
	}

	public bool SetPlayerNoMouthFX(string playerName, bool active)
	{
		if (playerName == null)
		{
			return false;
		}
		if (playerName == dissonanceComms.LocalPlayerName)
		{
			Debug.Log("Local player mouth FX: " + active);
			return true;
		}
		if (!_desiredVoipBus.ContainsKey(playerName))
		{
			return false;
		}
		if (_mouthFX[playerName] == active)
		{
			return true;
		}
		_mouthFX[playerName] = active;
		OnMouthFXChanged?.Invoke();
		Debug.Log("Player mouth FX on " + playerName + ": " + active);
		return true;
	}

	public VoipFX GetDesiredVoipFX(string playerName)
	{
		if (_desiredVoipFX.ContainsKey(playerName))
		{
			return _desiredVoipFX[playerName];
		}
		Debug.Log("Player not added to voip fx dict, assigning default bus");
		return VoipFX.Default;
	}

	public int GetDesiredVoipBus(string playerName)
	{
		if (_desiredVoipBus.ContainsKey(playerName))
		{
			Debug.Log("Player voice bus : " + _desiredVoipBus[playerName]);
			return _desiredVoipBus[playerName];
		}
		Debug.Log("Player not added to voip fx dict yet, assigning to 0");
		return 0;
	}

	public int GetDesiredMouthFX(string playerName)
	{
		if (_mouthFX.ContainsKey(playerName))
		{
			if (!_mouthFX[playerName])
			{
				return 0;
			}
			return 1;
		}
		Debug.Log("Player not added to voip fx dict, assigning default bus");
		return 0;
	}
}

}
