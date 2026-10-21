using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Lean.Pool;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using UnityEngine;
using UnityEngine.Audio;

namespace PaperPixelGames.Core.Managers
{
	public class SoundManager : Singleton<SoundManager>
	{
		[Header("Mixer")]
		[SerializeField]
		private AudioMixer _audioMixer;

		[SerializeField]
		private AudioMixerGroup _sfxAffectedGroup;

		[SerializeField]
		private AudioMixerGroup _sfxUnaffectedGroup;

		[SerializeField]
		private AudioMixerGroup _uiGeneralGroup;

		[Header("Snapshots")]
		[SerializeField]
		private string _surfaceSnapshotName = "Default";

		[SerializeField]
		private string _underwaterSnapshotName = "Underwater";

		[Header("Runtime Prefab / Limits")]
		[SerializeField]
		private AudioSource _audioPrefab;

		[SerializeField]
		private int _maxSimultaneousSFX = 20;

		[Header("Item SFX 3D")]
		[SerializeField]
		[Min(0f)]
		private float _itemSfxMinDistance = 1f;

		[SerializeField]
		[Min(0.01f)]
		private float _itemSfxMaxDistance = 10f;

		[Header("Data")]
		[SerializeField]
		private List<AudioClipSO> _sounds;

		[SerializeField]
		private MaterialSoundEntry[] _materialSoundEntries;

		private readonly Dictionary<SoundType, AudioSource> _cachedSounds = new Dictionary<SoundType, AudioSource>();

		private readonly List<AudioSource> _activeSFXSources = new List<AudioSource>();

		private readonly Dictionary<(SoundType, HoldableMaterialType), AudioClip[]> _clips = new Dictionary<(SoundType, HoldableMaterialType), AudioClip[]>();

		private readonly Dictionary<AudioClipSO, float> _lastPlayedSfxTime = new Dictionary<AudioClipSO, float>();

		private readonly Dictionary<AudioClipSO, List<AudioSource>> _activeSourcesBySfx = new Dictionary<AudioClipSO, List<AudioSource>>();

		private readonly Dictionary<AudioSource, AudioClipSO> _sfxBySource = new Dictionary<AudioSource, AudioClipSO>();

		private readonly Dictionary<AudioSource, ulong> _playbackIds = new Dictionary<AudioSource, ulong>();

		private readonly Dictionary<AudioSource, ulong> _ownedLoopPlaybackIds = new Dictionary<AudioSource, ulong>();

		private ulong _nextPlaybackId;

		private FurnitureWeight _lastWeight;

		private Tweener _pitchTween;

		private Tweener _volumeTween;

		private bool _playerStoreUpdate;

		private AudioMixerSnapshot _surfaceSnapshot;

		private AudioMixerSnapshot _underwaterSnapshot;

		private bool _isUnderwaterSnapshotActive;

		public Dictionary<SoundType, AudioSource> CachedSounds => _cachedSounds;

		protected override void Awake()
		{
			base.Awake();
			if (_audioMixer == null)
			{
				UtilityMethods.Log("AudioMixer is not assigned on SoundManager.", LogType.Error);
				return;
			}
			_surfaceSnapshot = _audioMixer.FindSnapshot(_surfaceSnapshotName);
			_underwaterSnapshot = _audioMixer.FindSnapshot(_underwaterSnapshotName);
			if (_surfaceSnapshot == null)
			{
				UtilityMethods.Log("Surface snapshot '" + _surfaceSnapshotName + "' not found in AudioMixer.", LogType.Error);
			}
			if (_underwaterSnapshot == null)
			{
				UtilityMethods.Log("Underwater snapshot '" + _underwaterSnapshotName + "' not found in AudioMixer.", LogType.Error);
			}
		}

		private void OnEnable()
		{
			InitializeClips();
		}

		private void InitializeClips()
		{
			_clips.Clear();
			if (_materialSoundEntries == null)
			{
				return;
			}
			MaterialSoundEntry[] materialSoundEntries = _materialSoundEntries;
			for (int i = 0; i < materialSoundEntries.Length; i++)
			{
				MaterialSoundEntry materialSoundEntry = materialSoundEntries[i];
				if (materialSoundEntry.Clips != null && materialSoundEntry.Clips.Length != 0)
				{
					(SoundType, HoldableMaterialType) key = (materialSoundEntry.SoundType, materialSoundEntry.MaterialType);
					if (_clips.ContainsKey(key))
					{
						_clips[key] = materialSoundEntry.Clips;
					}
					else
					{
						_clips.Add(key, materialSoundEntry.Clips);
					}
				}
			}
		}

		public void ResetManager()
		{
			StopAllCoroutines();
			foreach (AudioSource activeSFXSource in _activeSFXSources)
			{
				if (!(activeSFXSource == null))
				{
					LeanPool.Despawn(activeSFXSource);
				}
			}
			_activeSFXSources.Clear();
			_cachedSounds.Clear();
			_lastPlayedSfxTime.Clear();
			_activeSourcesBySfx.Clear();
			_sfxBySource.Clear();
			_playbackIds.Clear();
			_ownedLoopPlaybackIds.Clear();
		}

		public void AddInstantSource(AudioSource source)
		{
			if (source != null)
			{
				_activeSFXSources.Add(source);
			}
		}

		public AudioSource PlaySFX(SoundType type, FurnitureWeight weight, Vector3 position = default(Vector3), Transform parent = null)
		{
			AudioClipSO soundByType = GetSoundByType(type);
			if (soundByType != null)
			{
				_lastWeight = weight;
				return PlaySFX(soundByType, position, parent);
			}
			UtilityMethods.Log($"No audio clip found for SoundType: {type}", LogType.Warning);
			return null;
		}

		public AudioSource PlaySFX(SoundType type, Vector3 position = default(Vector3), Transform parent = null)
		{
			AudioClipSO soundByType = GetSoundByType(type);
			if (soundByType != null)
			{
				return PlaySFX(soundByType, position, parent);
			}
			UtilityMethods.Log($"No audio clip found for SoundType: {type}", LogType.Warning);
			return null;
		}

		public AudioSource PlaySFX(AudioClipSO sfxData, Vector3 position = default(Vector3), Transform parent = null)
		{
			return PlaySfxInternal(sfxData, position, parent, protectFromAutomaticEviction: false, null);
		}

		public AudioSource PlayItemImpactSFX(AudioClipSO sfxData, Vector3 position)
		{
			AudioSource audioSource = PlaySFX(sfxData, position);
			if (audioSource != null)
			{
				ConfigureItemSfxSource(audioSource);
			}
			return audioSource;
		}

		public OwnedLoopHandle PlayOwnedLoop(SoundType type, Vector3 position = default(Vector3), Transform parent = null, string ownerName = null)
		{
			AudioClipSO soundByType = GetSoundByType(type);
			if (soundByType != null)
			{
				return PlayOwnedLoop(soundByType, position, parent, ownerName);
			}
			UtilityMethods.Log($"[SoundManager.OwnedLoop] No audio clip found for SoundType: {type}", LogType.Warning);
			return null;
		}

		public OwnedLoopHandle PlayOwnedLoop(AudioClipSO sfxData, Vector3 position = default(Vector3), Transform parent = null, string ownerName = null)
		{
			if (sfxData == null)
			{
				UtilityMethods.Log("[SoundManager.OwnedLoop] Play rejected. SFX data is null.", LogType.Error);
				return null;
			}
			if (!sfxData.Loop)
			{
				UtilityMethods.Log("[SoundManager.OwnedLoop] Play rejected. Asset is not configured as a loop. Asset=" + sfxData.name, LogType.Warning);
				return null;
			}
			string text = (string.IsNullOrWhiteSpace(ownerName) ? "UnknownOwner" : ownerName);
			AudioSource audioSource = PlaySfxInternal(sfxData, position, parent, protectFromAutomaticEviction: true, text);
			if (audioSource == null || !TryGetPlaybackId(audioSource, out var playbackId))
			{
				return null;
			}
			_ownedLoopPlaybackIds[audioSource] = playbackId;
			UtilityMethods.Log("[SoundManager.OwnedLoop] Play. Owner=" + text + " " + $"Asset={sfxData.name} Source={audioSource.GetInstanceID()} Playback={playbackId}");
			return new OwnedLoopHandle(this, audioSource, playbackId, text);
		}

		private AudioSource PlaySfxInternal(AudioClipSO sfxData, Vector3 position, Transform parent, bool protectFromAutomaticEviction, string ownerName)
		{
			if (sfxData == null)
			{
				UtilityMethods.Log("SFX data is null. Cannot play sound.", LogType.Error);
				return null;
			}
			AudioClip audioClip = ResolveClip(sfxData);
			if (audioClip == null)
			{
				UtilityMethods.Log("SFX clip is null. Cannot play sound. Asset:" + sfxData.name, LogType.Error);
				return null;
			}
			if (!CanPlaySfxData(sfxData, protectFromAutomaticEviction, ownerName))
			{
				return null;
			}
			if (!TryReleaseOldestSfxIfAtCapacity())
			{
				return null;
			}
			if (_audioPrefab == null)
			{
				UtilityMethods.Log("AudioSource Prefab is not assigned.", LogType.Error);
				return null;
			}
			AudioSource audioSource = LeanPool.Spawn(_audioPrefab, position, Quaternion.identity);
			if (audioSource == null)
			{
				UtilityMethods.Log("Failed to spawn AudioSource from pool.", LogType.Error);
				return null;
			}
			audioSource.clip = audioClip;
			audioSource.volume = ResolveVolume(sfxData);
			audioSource.loop = sfxData.Loop;
			AssignOutputMixerGroup(audioSource, sfxData);
			if (sfxData.UseRandomPitch)
			{
				float num = sfxData.RandomPitchRange.x;
				float num2 = sfxData.RandomPitchRange.y;
				if (num2 < num)
				{
					float num3 = num;
					float num4 = num2;
					num2 = num3;
					num = num4;
				}
				float num5 = Random.Range(num, num2);
				if (_lastWeight != FurnitureWeight.None)
				{
					switch (_lastWeight)
					{
					case FurnitureWeight.Light:
						num5 += 0.1f;
						break;
					case FurnitureWeight.Heavy:
						num5 -= 0.15f;
						break;
					case FurnitureWeight.UltraHeavy:
						num5 -= 0.225f;
						break;
					case FurnitureWeight.Fixed:
						num5 -= 0.3f;
						break;
					}
				}
				num5 = Mathf.Clamp(num5, num, num2);
				audioSource.pitch = num5;
				_lastWeight = FurnitureWeight.None;
			}
			else
			{
				audioSource.pitch = 1f;
			}
			if (sfxData.Is3D)
			{
				audioSource.spatialBlend = Mathf.Clamp01(sfxData.SpatialBlend);
				audioSource.transform.position = position;
				audioSource.minDistance = Mathf.Max(0f, sfxData.MinDistance);
				audioSource.maxDistance = sfxData.HearDistance;
				audioSource.rolloffMode = sfxData.RolloffMode;
				audioSource.dopplerLevel = Mathf.Max(0f, sfxData.DopplerLevel);
				if (parent != null)
				{
					audioSource.transform.SetParent(parent);
				}
			}
			else
			{
				audioSource.spatialBlend = 0f;
			}
			ApplyRandomStartTime(audioSource, sfxData);
			if (sfxData.FadeIn)
			{
				audioSource.DOFade(audioSource.volume, sfxData.FadeInDuration).From(0f);
			}
			audioSource.Play();
			_activeSFXSources.Add(audioSource);
			RegisterSfxInstance(sfxData, audioSource);
			ulong playbackId = RegisterPlayback(audioSource);
			StartCoroutine(ReturnToPoolAfterPlay(audioSource, playbackId));
			if (sfxData.Cache && !_cachedSounds.ContainsKey(sfxData.Type))
			{
				_cachedSounds.Add(sfxData.Type, audioSource);
			}
			return audioSource;
		}

		public void PlaySFX(SoundType type, HoldableMaterialType material, FurnitureWeight weight, Vector3? pos = null)
		{
			AudioClip clip = GetClip(type, material);
			if (clip == null)
			{
				UtilityMethods.Log($"No SFX found for {type} + {material}", LogType.Warning);
				return;
			}
			float volume = WeightToVolume(weight);
			float pitch = WeightToPitch(weight);
			if (pos.HasValue)
			{
				Play3DClip(clip, pos.Value, volume, pitch);
			}
			else
			{
				Play2DClip(clip, volume, pitch);
			}
		}

		private AudioSource Play3DClip(AudioClip clip, Vector3 position, float volume, float pitch)
		{
			if (clip == null)
			{
				UtilityMethods.Log("Clip is null. Cannot play 3D sound.", LogType.Error);
				return null;
			}
			if (!TryReleaseOldestSfxIfAtCapacity())
			{
				return null;
			}
			if (_audioPrefab == null)
			{
				UtilityMethods.Log("AudioSource Prefab is not assigned.", LogType.Error);
				return null;
			}
			AudioSource audioSource = LeanPool.Spawn(_audioPrefab, position, Quaternion.identity);
			if (audioSource == null)
			{
				UtilityMethods.Log("Failed to spawn AudioSource from pool.", LogType.Error);
				return null;
			}
			audioSource.clip = clip;
			audioSource.volume = volume;
			audioSource.pitch = pitch;
			audioSource.loop = false;
			ConfigureItemSfxSource(audioSource);
			AssignOutputMixerGroup(audioSource, null);
			audioSource.Play();
			_activeSFXSources.Add(audioSource);
			ulong playbackId = RegisterPlayback(audioSource);
			StartCoroutine(ReturnToPoolAfterPlay(audioSource, playbackId));
			return audioSource;
		}

		private void ConfigureItemSfxSource(AudioSource source)
		{
			float num = Mathf.Max(0f, _itemSfxMinDistance);
			source.spatialBlend = 1f;
			source.minDistance = num;
			source.maxDistance = Mathf.Max(num + 0.01f, _itemSfxMaxDistance);
			source.rolloffMode = AudioRolloffMode.Linear;
			source.dopplerLevel = 0f;
		}

		public AudioSource Play2DClip(AudioClip clip, float volume, float pitch)
		{
			if (clip == null)
			{
				UtilityMethods.Log("Clip is null. Cannot play 2D sound.", LogType.Error);
				return null;
			}
			if (!TryReleaseOldestSfxIfAtCapacity())
			{
				return null;
			}
			if (_audioPrefab == null)
			{
				UtilityMethods.Log("AudioSource Prefab is not assigned.", LogType.Error);
				return null;
			}
			AudioSource audioSource = LeanPool.Spawn(_audioPrefab, Vector3.zero, Quaternion.identity);
			if (audioSource == null)
			{
				UtilityMethods.Log("Failed to spawn AudioSource from pool.", LogType.Error);
				return null;
			}
			audioSource.clip = clip;
			audioSource.volume = volume;
			audioSource.pitch = pitch;
			audioSource.loop = false;
			audioSource.spatialBlend = 0f;
			AssignOutputMixerGroup(audioSource, null);
			audioSource.Play();
			_activeSFXSources.Add(audioSource);
			ulong playbackId = RegisterPlayback(audioSource);
			StartCoroutine(ReturnToPoolAfterPlay(audioSource, playbackId));
			return audioSource;
		}

		private IEnumerator ReturnToPoolAfterPlay(AudioSource source, ulong playbackId)
		{
			if (source == null || source.clip == null)
			{
				yield break;
			}
			while (IsPlaybackActive(source, playbackId) && source.gameObject.activeSelf && source.loop)
			{
				yield return null;
			}
			if (!IsPlaybackActive(source, playbackId))
			{
				yield break;
			}
			if (!source.gameObject.activeSelf)
			{
				RemoveSourceTracking(source);
				yield break;
			}
			float num = source.clip.length - source.time;
			if (num > 0f)
			{
				yield return new WaitForSecondsRealtime(num);
			}
			if (IsPlaybackActive(source, playbackId))
			{
				Despawn(source);
			}
		}

		public void Despawn(AudioSource source)
		{
			if (!(source == null) && _activeSFXSources.Contains(source))
			{
				if (IsOwnedLoopPlayback(source))
				{
					UtilityMethods.Log("[SoundManager.OwnedLoop] Generic despawn rejected. " + $"Source={source.GetInstanceID()} must be released by its owner handle.", LogType.Warning);
				}
				else
				{
					DespawnInternal(source);
				}
			}
		}

		private void DespawnInternal(AudioSource source)
		{
			if (!(source == null) && _activeSFXSources.Contains(source))
			{
				source.loop = false;
				source.Stop();
				RemoveSourceTracking(source);
				LeanPool.Despawn(source.gameObject);
			}
		}

		public bool TryGetPlaybackId(AudioSource source, out ulong playbackId)
		{
			if (source != null && _activeSFXSources.Contains(source) && _playbackIds.TryGetValue(source, out playbackId))
			{
				return true;
			}
			playbackId = 0uL;
			return false;
		}

		public bool IsPlaybackActive(AudioSource source, ulong playbackId)
		{
			if (source != null && playbackId != 0L && _activeSFXSources.Contains(source) && _playbackIds.TryGetValue(source, out var value))
			{
				return value == playbackId;
			}
			return false;
		}

		private bool IsOwnedLoopPlayback(AudioSource source)
		{
			if (source != null && _ownedLoopPlaybackIds.TryGetValue(source, out var value))
			{
				return IsPlaybackActive(source, value);
			}
			return false;
		}

		private bool IsOwnedLoopPlayback(AudioSource source, ulong playbackId)
		{
			if (source != null && playbackId != 0L && _ownedLoopPlaybackIds.TryGetValue(source, out var value) && value == playbackId)
			{
				return IsPlaybackActive(source, playbackId);
			}
			return false;
		}

		public void StopAndDespawn(AudioSource source, ulong playbackId)
		{
			if (IsPlaybackActive(source, playbackId))
			{
				Despawn(source);
			}
		}

		public void ReleaseOwnedLoop(OwnedLoopHandle handle)
		{
			if (handle == null)
			{
				return;
			}
			if (handle.Manager != this)
			{
				handle.Invalidate();
				return;
			}
			AudioSource trackedSource = handle.TrackedSource;
			ulong playbackId = handle.PlaybackId;
			if (IsOwnedLoopPlayback(trackedSource, playbackId))
			{
				UtilityMethods.Log("[SoundManager.OwnedLoop] Release. Owner=" + handle.OwnerName + " " + $"Source={trackedSource.GetInstanceID()} Playback={playbackId}");
				DespawnInternal(trackedSource);
			}
			handle.Invalidate();
		}

		private ulong RegisterPlayback(AudioSource source)
		{
			_nextPlaybackId++;
			if (_nextPlaybackId == 0L)
			{
				_nextPlaybackId++;
			}
			_playbackIds[source] = _nextPlaybackId;
			return _nextPlaybackId;
		}

		private void RemoveSourceTracking(AudioSource source)
		{
			if (!(source == null))
			{
				RemoveCachedSourceReference(source);
				_ownedLoopPlaybackIds.Remove(source);
				_playbackIds.Remove(source);
				UnregisterSfxInstance(source);
				_activeSFXSources.Remove(source);
			}
		}

		private void RemoveCachedSourceReference(AudioSource source)
		{
			if (source == null || _cachedSounds.Count == 0)
			{
				return;
			}
			SoundType? soundType = null;
			foreach (KeyValuePair<SoundType, AudioSource> cachedSound in _cachedSounds)
			{
				if (!(cachedSound.Value != source))
				{
					soundType = cachedSound.Key;
					break;
				}
			}
			if (soundType.HasValue)
			{
				_cachedSounds.Remove(soundType.Value);
			}
		}

		private bool TryReleaseOldestSfxIfAtCapacity()
		{
			if (_maxSimultaneousSFX <= 0)
			{
				return true;
			}
			while (_activeSFXSources.Count >= _maxSimultaneousSFX)
			{
				AudioSource audioSource = null;
				for (int i = 0; i < _activeSFXSources.Count; i++)
				{
					AudioSource audioSource2 = _activeSFXSources[i];
					if (audioSource2 == null)
					{
						_activeSFXSources.RemoveAt(i);
						i--;
					}
					else if (!IsOwnedLoopPlayback(audioSource2))
					{
						audioSource = audioSource2;
						break;
					}
				}
				if (audioSource == null)
				{
					UtilityMethods.Log("[SoundManager] SFX capacity reached, but every active source is an owner-controlled loop. New playback rejected.", LogType.Warning);
					return false;
				}
				UtilityMethods.Log("Max simultaneous SFX reached. Despawning oldest transient SFX source.", LogType.Warning);
				DespawnInternal(audioSource);
			}
			return true;
		}

		public void PauseAllSounds()
		{
			foreach (AudioSource activeSFXSource in _activeSFXSources)
			{
				if (activeSFXSource != null && activeSFXSource.isPlaying)
				{
					activeSFXSource.Pause();
				}
			}
		}

		public void ResumeAllSounds()
		{
			foreach (AudioSource activeSFXSource in _activeSFXSources)
			{
				if (activeSFXSource != null && !activeSFXSource.isPlaying)
				{
					activeSFXSource.UnPause();
				}
			}
		}

		public void ChangePitch(AudioSource source, float targetPitch, float duration = 0f)
		{
			if (source == null)
			{
				UtilityMethods.Log("AudioSource is null. Cannot change pitch.", LogType.Error);
			}
			else if (duration > 0f)
			{
				_pitchTween?.Kill();
				_pitchTween = source.DOPitch(targetPitch, duration).OnComplete(() =>
				{
					UtilityMethods.Log($"Pitch change to {targetPitch} completed.");
				});
			}
			else
			{
				source.pitch = targetPitch;
				UtilityMethods.Log($"Pitch directly set to {targetPitch}.");
			}
		}

		public void ChangeVolume(AudioSource source, float targetVolume, float duration = 0f)
		{
			if (source == null)
			{
				UtilityMethods.Log("AudioSource is null. Cannot change volume.", LogType.Error);
			}
			else if (duration > 0f)
			{
				_volumeTween?.Kill();
				_volumeTween = source.DOFade(targetVolume, duration).OnComplete(() =>
				{
					UtilityMethods.Log($"Volume change to {targetVolume} completed.");
				});
			}
			else
			{
				source.volume = targetVolume;
				UtilityMethods.Log($"Volume directly set to {targetVolume}.");
			}
		}

		public void UpdatePlayerStoreStatus(bool status)
		{
			if (_playerStoreUpdate != status)
			{
				_playerStoreUpdate = status;
			}
		}

		public void EnterUnderwater()
		{
			if (!_isUnderwaterSnapshotActive)
			{
				_isUnderwaterSnapshotActive = true;
				if (_underwaterSnapshot != null)
				{
					_underwaterSnapshot.TransitionTo(0.5f);
					UtilityMethods.Log("<color=blue>Entered Underwater Snapshot</color>");
				}
				else
				{
					UtilityMethods.Log("Underwater snapshot is not configured.", LogType.Warning);
				}
			}
		}

		public void ExitUnderwater()
		{
			if (_isUnderwaterSnapshotActive)
			{
				_isUnderwaterSnapshotActive = false;
				if (_surfaceSnapshot != null)
				{
					_surfaceSnapshot.TransitionTo(0.5f);
					UtilityMethods.Log("<color=blue>Exited Underwater Snapshot</color>");
				}
				else
				{
					UtilityMethods.Log("Surface snapshot is not configured.", LogType.Warning);
				}
			}
		}

		public AudioClipSO GetSoundByType(SoundType type)
		{
			return _sounds.FirstOrDefault((AudioClipSO x) => x.Type == type);
		}

		public AudioClip GetClip(SoundType soundType, HoldableMaterialType materialType)
		{
			(SoundType, HoldableMaterialType) key = (soundType, materialType);
			if (_clips.TryGetValue(key, out var value) && value.Length != 0)
			{
				if (value.Length == 1)
				{
					return value[0];
				}
				return value[Random.Range(0, value.Length)];
			}
			(SoundType, HoldableMaterialType) key2 = (soundType, HoldableMaterialType.None);
			if (_clips.TryGetValue(key2, out var value2) && value2.Length != 0)
			{
				if (value2.Length != 1)
				{
					return value2[Random.Range(0, value2.Length)];
				}
				return value2[0];
			}
			return null;
		}

		private float WeightToVolume(FurnitureWeight weight)
		{
			return weight switch
			{
				FurnitureWeight.Light => 0.35f, 
				FurnitureWeight.Medium => 0.45f, 
				FurnitureWeight.Heavy => 0.65f, 
				FurnitureWeight.UltraHeavy => 0.75f, 
				FurnitureWeight.Fixed => 1f, 
				_ => 0.7f, 
			};
		}

		private float WeightToPitch(FurnitureWeight weight)
		{
			return weight switch
			{
				FurnitureWeight.Light => 1.1f, 
				FurnitureWeight.Medium => 1.05f, 
				FurnitureWeight.Heavy => 0.9f, 
				FurnitureWeight.UltraHeavy => 0.8f, 
				FurnitureWeight.Fixed => 0.7f, 
				_ => 1f, 
			};
		}

		private void AssignOutputMixerGroup(AudioSource source, AudioClipSO sfxData)
		{
			if (source == null)
			{
				return;
			}
			if (sfxData != null && sfxData.MixerGroup != null)
			{
				source.outputAudioMixerGroup = sfxData.MixerGroup;
				return;
			}
			AudioMixerGroup audioMixerGroup = null;
			if (sfxData != null)
			{
				switch (sfxData.Route)
				{
				case AudioRoute.SfxAffected:
					audioMixerGroup = _sfxAffectedGroup;
					break;
				case AudioRoute.SfxUnaffected:
					audioMixerGroup = _sfxUnaffectedGroup;
					break;
				case AudioRoute.UiGeneral:
					audioMixerGroup = _uiGeneralGroup;
					break;
				}
			}
			if (audioMixerGroup == null)
			{
				audioMixerGroup = _sfxAffectedGroup;
			}
			if (audioMixerGroup != null)
			{
				source.outputAudioMixerGroup = audioMixerGroup;
			}
		}

		private AudioClip ResolveClip(AudioClipSO sfxData)
		{
			if (sfxData == null)
			{
				return null;
			}
			if (sfxData.Variants != null && sfxData.Variants.Length != 0)
			{
				List<AudioClip> list = null;
				for (int i = 0; i < sfxData.Variants.Length; i++)
				{
					AudioClip audioClip = sfxData.Variants[i];
					if (!(audioClip == null))
					{
						if (list == null)
						{
							list = new List<AudioClip>();
						}
						list.Add(audioClip);
					}
				}
				if (list != null && list.Count > 0)
				{
					return list[Random.Range(0, list.Count)];
				}
			}
			return sfxData.Clip;
		}

		private float ResolveVolume(AudioClipSO sfxData)
		{
			if (sfxData == null)
			{
				return 1f;
			}
			if (!sfxData.UseRandomVolume)
			{
				return Mathf.Clamp01(sfxData.Volume);
			}
			float num = Mathf.Clamp01(sfxData.RandomVolumeRange.x);
			float num2 = Mathf.Clamp01(sfxData.RandomVolumeRange.y);
			if (num2 < num)
			{
				float num3 = num;
				float num4 = num2;
				num2 = num3;
				num = num4;
			}
			return Mathf.Clamp01(sfxData.Volume * Random.Range(num, num2));
		}

		private bool CanPlaySfxData(AudioClipSO sfxData, bool protectFromAutomaticEviction, string ownerName)
		{
			if (sfxData == null)
			{
				return false;
			}
			float num = Mathf.Max(0f, sfxData.Cooldown);
			if (num > 0f && _lastPlayedSfxTime.TryGetValue(sfxData, out var value) && Time.unscaledTime - value < num)
			{
				return false;
			}
			int num2 = Mathf.Max(0, sfxData.MaxInstances);
			if (num2 > 0)
			{
				List<AudioSource> activeInstances = GetActiveInstances(sfxData);
				if (activeInstances.Count >= num2)
				{
					if (sfxData.InstanceLimitMode == AudioInstanceLimitMode.SkipNew)
					{
						return false;
					}
					AudioSource audioSource = null;
					for (int i = 0; i < activeInstances.Count; i++)
					{
						AudioSource audioSource2 = activeInstances[i];
						if (!(audioSource2 == null) && !IsOwnedLoopPlayback(audioSource2))
						{
							audioSource = audioSource2;
							break;
						}
					}
					if (audioSource == null)
					{
						string text = (protectFromAutomaticEviction ? (" Owner=" + ownerName) : string.Empty);
						UtilityMethods.Log("[SoundManager] Instance limit reached, but all existing instances are owner-controlled loops. New playback rejected. Asset=" + sfxData.name + text, LogType.Warning);
						return false;
					}
					DespawnInternal(audioSource);
				}
			}
			_lastPlayedSfxTime[sfxData] = Time.unscaledTime;
			return true;
		}

		private List<AudioSource> GetActiveInstances(AudioClipSO sfxData)
		{
			if (!_activeSourcesBySfx.TryGetValue(sfxData, out var value))
			{
				value = new List<AudioSource>();
				_activeSourcesBySfx.Add(sfxData, value);
				return value;
			}
			for (int num = value.Count - 1; num >= 0; num--)
			{
				AudioSource audioSource = value[num];
				if (audioSource == null || !audioSource.gameObject.activeSelf)
				{
					value.RemoveAt(num);
				}
			}
			return value;
		}

		private void RegisterSfxInstance(AudioClipSO sfxData, AudioSource source)
		{
			if (!(sfxData == null) && !(source == null))
			{
				List<AudioSource> activeInstances = GetActiveInstances(sfxData);
				if (!activeInstances.Contains(source))
				{
					activeInstances.Add(source);
				}
				_sfxBySource[source] = sfxData;
			}
		}

		private void UnregisterSfxInstance(AudioSource source)
		{
			if (source == null || !_sfxBySource.TryGetValue(source, out var value))
			{
				return;
			}
			_sfxBySource.Remove(source);
			if (_activeSourcesBySfx.TryGetValue(value, out var value2))
			{
				value2.Remove(source);
				if (value2.Count == 0)
				{
					_activeSourcesBySfx.Remove(value);
				}
			}
		}

		private void ApplyRandomStartTime(AudioSource source, AudioClipSO sfxData)
		{
			if (!(source == null) && !(sfxData == null) && !(source.clip == null) && sfxData.UseRandomStartTime)
			{
				float num = Mathf.Clamp01(sfxData.RandomStartTimeRange.x);
				float num2 = Mathf.Clamp01(sfxData.RandomStartTimeRange.y);
				if (num2 < num)
				{
					float num3 = num;
					float num4 = num2;
					num2 = num3;
					num = num4;
				}
				source.time = source.clip.length * Random.Range(num, num2);
			}
		}
	}
}
