namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::Dissonance;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;

public class EscapeMenuVoiceList : MonoBehaviour
{
	[SerializeField]
	private DissonanceComms dissonanceComms;

	[SerializeField]
	private Transform container;

	[SerializeField]
	private VoiceVolumeEntry entryPrefab;

	private readonly Dictionary<string, VoiceVolumeEntry> _entriesByPlayerId = new Dictionary<string, VoiceVolumeEntry>();

	private Dictionary<string, float> _savedVolumes = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

	private Coroutine _refreshRetry;

	private void Awake()
	{
		if (dissonanceComms == null)
		{
			dissonanceComms = UnityEngine.Object.FindFirstObjectByType<DissonanceComms>();
		}
		_savedVolumes = PlayerVoiceVolumePersistence.Load();
	}

	private void OnEnable()
	{
		if (dissonanceComms != null)
		{
			dissonanceComms.OnPlayerJoinedSession += OnPlayerJoined;
			dissonanceComms.OnPlayerLeftSession += OnPlayerLeft;
		}
		RefreshList();
		if (_refreshRetry == null)
		{
			_refreshRetry = StartCoroutine(RefreshListRetryRoutine());
		}
	}

	private void OnDisable()
	{
		if (_refreshRetry != null)
		{
			StopCoroutine(_refreshRetry);
			_refreshRetry = null;
		}
		if (dissonanceComms != null)
		{
			dissonanceComms.OnPlayerJoinedSession -= OnPlayerJoined;
			dissonanceComms.OnPlayerLeftSession -= OnPlayerLeft;
		}
	}

	private void OnPlayerJoined(VoicePlayerState state)
	{
		RefreshList();
	}

	private void OnPlayerLeft(VoicePlayerState state)
	{
		RefreshList();
	}

	public void RefreshList()
	{
		if (container == null || entryPrefab == null || dissonanceComms == null)
		{
			return;
		}
		string localPlayerName = dissonanceComms.LocalPlayerName;
		if (string.IsNullOrEmpty(localPlayerName))
		{
			return;
		}
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, VoiceVolumeEntry> item in _entriesByPlayerId)
		{
			if (dissonanceComms.FindPlayer(item.Key) == null)
			{
				list.Add(item.Key);
			}
		}
		foreach (string item2 in list)
		{
			if (_entriesByPlayerId.TryGetValue(item2, out var value) && value != null)
			{
				UnityEngine.Object.Destroy(value.gameObject);
			}
			_entriesByPlayerId.Remove(item2);
		}
		foreach (VoicePlayerState player in dissonanceComms.Players)
		{
			if (player.Name == localPlayerName)
			{
				continue;
			}
			string text = player.Name;
			if (_entriesByPlayerId.TryGetValue(text, out var value2) && value2 != null)
			{
				(string, string, bool) tuple = ResolveDisplayNameAndSteamId(text);
				if (tuple.Item3)
				{
					value2.UpdateIdentity(tuple.Item1, tuple.Item2);
				}
				continue;
			}
			(string, string, bool) tuple2 = ResolveDisplayNameAndSteamId(text);
			object obj;
			if (tuple2.Item3)
			{
				(obj, _, _) = tuple2;
			}
			else
			{
				obj = "";
			}
			string displayName = (string)obj;
			string text2 = (tuple2.Item3 ? tuple2.Item2 : text);
			float initialVolume = 1f;
			if (!string.IsNullOrEmpty(text2) && _savedVolumes.TryGetValue(text2, out var value3))
			{
				initialVolume = value3;
			}
			VoiceVolumeEntry component = UnityEngine.Object.Instantiate(entryPrefab.gameObject, container).GetComponent<VoiceVolumeEntry>();
			if (component != null)
			{
				component.Setup(player, displayName, text2, initialVolume, OnVolumeChanged);
				_entriesByPlayerId[text] = component;
			}
		}
	}

	private IEnumerator RefreshListRetryRoutine()
	{
		yield return new WaitForSecondsRealtime(0.75f);
		RefreshList();
		yield return new WaitForSecondsRealtime(0.75f);
		RefreshList();
		_refreshRetry = null;
	}

	private (string displayName, string steamId, bool resolved) ResolveDisplayNameAndSteamId(string dissonancePlayerId)
	{
		if (MonoSingleton<LocalManager>.Instance?.players == null)
		{
			return (displayName: dissonancePlayerId, steamId: dissonancePlayerId, resolved: false);
		}
		foreach (PlayerReferences player in MonoSingleton<LocalManager>.Instance.players)
		{
			if (!(player.mirrorIgnorance == null) && !(player.profile == null) && !(player.mirrorIgnorance.PlayerId != dissonancePlayerId) && player.profile.hasSynced)
			{
				string playerName = player.profile.playerName;
				string item = ((player.profile.steamId != 0L) ? player.profile.steamId.ToString() : "");
				return (displayName: playerName, steamId: item, resolved: true);
			}
		}
		return (displayName: dissonancePlayerId, steamId: dissonancePlayerId, resolved: false);
	}

	private void OnVolumeChanged(string steamId, float volume)
	{
		if (!string.IsNullOrEmpty(steamId))
		{
			_savedVolumes[steamId] = volume;
			PlayerVoiceVolumePersistence.Save(_savedVolumes);
		}
	}
}

}
