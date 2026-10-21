using System.Collections;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using UnityEngine;
using UnityEngine.Serialization;

namespace PaperPixelGames.Core.Managers
{
	[DefaultExecutionOrder(-50)]
	public class OceanFXManager : Singleton<OceanFXManager>
	{
		private const float LOCOMOTION_FADE_TIME = 0.25f;

		private const float DROWN_FADE_TIME = 0.35f;

		private const float AMBIENT_FADE_TIME = 0.4f;

		[Header("State")]
		[SerializeField]
		private bool _debug;

		[SerializeField]
		private OceanLocomotionState _currentState;

		[SerializeField]
		[Range(0f, 1f)]
		private float _normalizedSpeed;

		private bool _isUnderwater;

		private bool _isSurfaceSwimming;

		[Header("Audio - Base")]
		[Tooltip("Base underwater ambience / rumble loop. Plays while the local camera is fully underwater.")]
		[FormerlySerializedAs("_ambientSource")]
		[SerializeField]
		private AudioSource _underwaterAmbientLoopSource;

		[Tooltip("Light bubble stream loop used by normal underwater movement.")]
		[FormerlySerializedAs("_bubbleStreamSource")]
		[SerializeField]
		private AudioSource _moveBubbleStreamLoopSource;

		[Tooltip("Heavy water rush loop used by underwater sprint. Optional, but recommended to separate sprint body from foam detail.")]
		[SerializeField]
		private AudioSource _sprintWaterRushLoopSource;

		[Tooltip("Foamy / cavitation detail loop used by underwater sprint.")]
		[FormerlySerializedAs("_foamCavitationSource")]
		[SerializeField]
		private AudioSource _sprintFoamCavitationLoopSource;

		[Header("Audio - Drown")]
		[Tooltip("Heartbeat loop used by drowning state.")]
		[FormerlySerializedAs("_drownHeartbeatSource")]
		[SerializeField]
		private AudioSource _drownHeartbeatLoopSource;

		[Tooltip("High pressure / lowpass tone loop used by drowning state.")]
		[FormerlySerializedAs("_drownPressureSource")]
		[SerializeField]
		private AudioSource _drownPressureLoopSource;

		[Header("Audio - Head Submerge / Surface")]
		[Tooltip("One-shot source used when the local camera becomes fully submerged.")]
		[FormerlySerializedAs("_enterWaterSource")]
		[SerializeField]
		private AudioSource _headSubmergeOneShotSource;

		[Tooltip("One-shot source used when the local camera breaks back above the surface.")]
		[FormerlySerializedAs("_exitWaterSource")]
		[SerializeField]
		private AudioSource _headSurfaceBreakOneShotSource;

		[Header("Audio - Surface")]
		[Tooltip("Surface-level idle ambience loop used while swimming in a water volume with head above water.")]
		[FormerlySerializedAs("_surfaceIdleSource")]
		[SerializeField]
		private AudioSource _surfaceIdleLoopSource;

		[Tooltip("Surface-level movement loop used while swimming forward with head above water.")]
		[SerializeField]
		private AudioSource _surfaceSwimMoveLoopSource;

		[Tooltip("Head submerge one-shot variations.")]
		[FormerlySerializedAs("_enterWaterClips")]
		[SerializeField]
		private AudioClip[] _headSubmergeClips;

		[Tooltip("Head surface-break one-shot variations.")]
		[FormerlySerializedAs("_exitWaterClips")]
		[SerializeField]
		private AudioClip[] _headSurfaceBreakClips;

		[Tooltip("Pitch range for head submerge / surface-break one-shots.")]
		[FormerlySerializedAs("_enterExitPitchRange")]
		[SerializeField]
		private Vector2 _headTransitionPitchRange = new Vector2(0.95f, 1.05f);

		[Tooltip("Volume multiplier range for head submerge / surface-break one-shots (linear).")]
		[FormerlySerializedAs("_enterExitVolumeRange")]
		[SerializeField]
		private Vector2 _headTransitionVolumeRange = new Vector2(0.9f, 1.2f);

		[Header("VFX - Head Surface")]
		[Tooltip("Screen-space one-shot prefab played when the local camera breaks back above the water surface.")]
		[SerializeField]
		private GameObject _headSurfaceBreakScreenVfxPrefab;

		[Header("Object Water Impact")]
		[SerializeField]
		private ParticleSystem _objectWaterImpactVfxPrefab;

		[SerializeField]
		private AudioClipSO _objectWaterImpactSfx;

		[SerializeField]
		[Min(0f)]
		private float _objectWaterImpactBigScale = 1.35f;

		[SerializeField]
		[Min(0f)]
		private float _objectWaterImpactSmallScale = 0.8f;

		[Header("Audio - Locomotion Volume")]
		[SerializeField]
		private float _surfaceIdleVolumeDb = -4f;

		[SerializeField]
		private Vector2 _surfaceSwimMoveVolumeDbRange = new Vector2(-18f, -6f);

		[SerializeField]
		private Vector2 _moveBubbleVolumeDbRange = new Vector2(-18f, -10f);

		[SerializeField]
		private Vector2 _sprintWaterRushVolumeDbRange = new Vector2(-8f, -3f);

		[SerializeField]
		private Vector2 _sprintFoamVolumeDbRange = new Vector2(-10f, -4f);

		[Header("Audio - Locomotion Pitch")]
		[SerializeField]
		private Vector2 _surfaceSwimMovePitchRange = new Vector2(0.96f, 1.04f);

		[SerializeField]
		private Vector2 _movePitchRange = new Vector2(0.96f, 1.04f);

		[SerializeField]
		private Vector2 _sprintPitchRange = new Vector2(0.98f, 1.08f);

		[Header("Audio - Surface Locomotion")]
		[SerializeField]
		private float _surfaceMoveSpeedThreshold = 0.1f;

		[Header("Audio - Pressure Variations")]
		[FormerlySerializedAs("_pressureSource")]
		[SerializeField]
		private AudioSource _idlePressureLoopSource;

		[FormerlySerializedAs("_pressureClips")]
		[SerializeField]
		private AudioClip[] _idlePressureClips;

		[SerializeField]
		private Vector2 _pressurePitchRange = new Vector2(0.96f, 1.03f);

		[SerializeField]
		private Vector2 _pressureVolumeRange = new Vector2(-7f, -3f);

		[Header("Audio - Bubble Pops Variations")]
		[FormerlySerializedAs("_bubblePopsOneShotSource")]
		[SerializeField]
		private AudioSource _idleBubblePopOneShotSource;

		[FormerlySerializedAs("_bubblePopClips")]
		[SerializeField]
		private AudioClip[] _idleBubblePopClips;

		[SerializeField]
		private Vector2 _bubblePitchRange = new Vector2(0.96f, 1.05f);

		[SerializeField]
		private Vector2 _bubbleVolumeRange = new Vector2(0.8f, 1.2f);

		[Header("Random Bubble Pops")]
		[SerializeField]
		private float _minBubblePopInterval = 6f;

		[SerializeField]
		private float _maxBubblePopInterval = 15f;

		private Coroutine _bubblePopRoutine;

		private Coroutine _pressureVariationRoutine;

		private float _pressureTargetDb = -8f;

		private GameObject _headSurfaceBreakScreenVfxInstance;

		public override SingletonScope Scope => SingletonScope.Scene;

		public PPG_Player Player { get; set; }

		protected override void Awake()
		{
			base.Awake();
			InitializeAudioSources();
			UtilityMethods.Log("[OceanFXManager] Initialized.");
		}

		private void Update()
		{
			UpdateContinuousAudio();
		}

		private void OnDisable()
		{
			StopRuntimeRoutines();
			StopHeadSurfaceBreakScreenVfx();
			MuteAllLayers();
		}

		public void SetSurface(bool isSwimming)
		{
			if (_isSurfaceSwimming == isSwimming)
			{
				return;
			}
			_isSurfaceSwimming = isSwimming;
			if (!_isUnderwater)
			{
				if (_isSurfaceSwimming)
				{
					SetLocomotionState(OceanLocomotionState.None, 0f);
					EnsureSurfaceLoopSourcesPlaying();
				}
				else
				{
					StopSurfaceLoopSources();
					MuteAllLayers();
				}
			}
		}

		public void SetUnderwater(bool isUnderwater)
		{
			if (_isUnderwater != isUnderwater)
			{
				_isUnderwater = isUnderwater;
				if (_isUnderwater)
				{
					OnEnterUnderwater();
				}
				else
				{
					OnExitUnderwater();
				}
			}
		}

		public void SetLocomotionState(OceanLocomotionState newState, float normalizedSpeed)
		{
			_normalizedSpeed = Mathf.Clamp01(normalizedSpeed);
			OceanLocomotionState currentState = _currentState;
			_currentState = newState;
			if (_isUnderwater && newState != currentState)
			{
				HandleStateExit(currentState);
				HandleStateEnter(_currentState);
			}
		}

		public void SetDrownIntensity(float t)
		{
			t = Mathf.Clamp01(t);
			if (_drownHeartbeatLoopSource != null)
			{
				_drownHeartbeatLoopSource.pitch = Mathf.Lerp(1.25f, 0.65f, t);
			}
		}

		public void PlayObjectWaterImpact(Vector3 position, float speed, bool isBig)
		{
			if (IsFinite(position))
			{
				PlayObjectWaterImpactVfx(position, isBig);
				PlayObjectWaterImpactSfx(position, speed, isBig);
				if (_debug)
				{
					UtilityMethods.Log($"[OceanFXManager] ObjectWaterImpact Position={position} Speed={speed:F2} Big={isBig}");
				}
			}
		}

		private void InitializeAudioSources()
		{
			SafeInitLoop(_underwaterAmbientLoopSource, playOnStart: false);
			SafeInitLoop(_idlePressureLoopSource, playOnStart: false);
			SafeInitLoop(_moveBubbleStreamLoopSource, playOnStart: false);
			SafeInitLoop(_sprintWaterRushLoopSource, playOnStart: false);
			SafeInitLoop(_sprintFoamCavitationLoopSource, playOnStart: false);
			SafeInitLoop(_drownHeartbeatLoopSource, playOnStart: false);
			SafeInitLoop(_drownPressureLoopSource, playOnStart: false);
			SafeInitLoop(_surfaceIdleLoopSource, playOnStart: false);
			SafeInitLoop(_surfaceSwimMoveLoopSource, playOnStart: false);
			SafeInitOneShot(_headSubmergeOneShotSource);
			SafeInitOneShot(_headSurfaceBreakOneShotSource);
			PreloadConfiguredAudioClips();
			MuteAllLayers();
		}

		private void HandleStateEnter(OceanLocomotionState state)
		{
			switch (state)
			{
			case OceanLocomotionState.Idle:
				OnEnterIdle();
				break;
			case OceanLocomotionState.Move:
				OnEnterMove();
				break;
			case OceanLocomotionState.Sprint:
				OnEnterSprint();
				break;
			case OceanLocomotionState.Drown:
				OnEnterDrown();
				break;
			}
		}

		private void HandleStateExit(OceanLocomotionState state)
		{
			switch (state)
			{
			case OceanLocomotionState.Idle:
				OnExitIdle();
				break;
			case OceanLocomotionState.Move:
				OnExitMove();
				break;
			case OceanLocomotionState.Sprint:
				OnExitSprint();
				break;
			case OceanLocomotionState.Drown:
				OnExitDrown();
				break;
			}
		}

		private void OnEnterIdle()
		{
			if (_debug)
			{
				UtilityMethods.Log("[OceanFXManager] OnEnterIdle");
			}
			_pressureTargetDb = -4f;
			ApplyRandomPressureVariation();
			StartPressureVariationRoutine();
			StartBubblePopRoutine();
		}

		private void OnExitIdle()
		{
			if (_debug)
			{
				UtilityMethods.Log("[OceanFXManager] OnExitIdle");
			}
			if (_pressureVariationRoutine != null)
			{
				StopCoroutine(_pressureVariationRoutine);
			}
			_pressureVariationRoutine = null;
			StopBubblePopRoutine();
		}

		private void OnEnterMove()
		{
			if (_debug)
			{
				UtilityMethods.Log("[OceanFXManager] OnEnterMove");
			}
			EnsureLoopSourcePlaying(_moveBubbleStreamLoopSource);
		}

		private void OnExitMove()
		{
			if (_debug)
			{
				UtilityMethods.Log("[OceanFXManager] OnExitMove");
			}
		}

		private void OnEnterSprint()
		{
			if (_debug)
			{
				UtilityMethods.Log("[OceanFXManager] OnEnterSprint");
			}
			EnsureLoopSourcePlaying(_sprintWaterRushLoopSource);
			EnsureLoopSourcePlaying(_sprintFoamCavitationLoopSource);
		}

		private void OnExitSprint()
		{
			if (_debug)
			{
				UtilityMethods.Log("[OceanFXManager] OnExitSprint");
			}
		}

		private void OnEnterDrown()
		{
			if (_debug)
			{
				UtilityMethods.Log("[OceanFXManager] OnEnterDrown");
			}
			SetVolume(_moveBubbleStreamLoopSource, -80f);
			SetVolume(_sprintWaterRushLoopSource, -80f);
			SetVolume(_sprintFoamCavitationLoopSource, -80f);
			StopBubblePopRoutine();
			if (_drownHeartbeatLoopSource != null && !_drownHeartbeatLoopSource.isPlaying)
			{
				_drownHeartbeatLoopSource.Play();
			}
			if (_drownPressureLoopSource != null && !_drownPressureLoopSource.isPlaying)
			{
				_drownPressureLoopSource.Play();
			}
		}

		private void OnExitDrown()
		{
			if (_debug)
			{
				UtilityMethods.Log("[OceanFXManager] OnExitDrown");
			}
			if (_drownHeartbeatLoopSource != null)
			{
				SetVolume(_drownHeartbeatLoopSource, -80f);
			}
			if (_drownPressureLoopSource != null)
			{
				SetVolume(_drownPressureLoopSource, -80f);
			}
		}

		private void OnEnterUnderwater()
		{
			if (_debug)
			{
				UtilityMethods.Log("[OceanFXManager] OnEnterUnderwater");
			}
			_isSurfaceSwimming = false;
			StopHeadSurfaceBreakScreenVfx();
			EnsureLoopSourcePlaying(_underwaterAmbientLoopSource);
			EnsureLoopSourcePlaying(_idlePressureLoopSource);
			StopSurfaceLoopSources();
			if (_currentState == OceanLocomotionState.None)
			{
				_currentState = OceanLocomotionState.Idle;
			}
			HandleStateEnter(_currentState);
			PlayRandomHeadSubmergeSfx();
			if (Player != null)
			{
				Player.PlayOceanEnterFPSVFX();
			}
		}

		private void OnExitUnderwater()
		{
			if (_debug)
			{
				UtilityMethods.Log("[OceanFXManager] OnExitUnderwater");
			}
			_isSurfaceSwimming = IsPlayerStillSurfaceSwimming();
			_currentState = OceanLocomotionState.None;
			_normalizedSpeed = 0f;
			if (_idlePressureLoopSource != null)
			{
				_idlePressureLoopSource.Stop();
			}
			if (_moveBubbleStreamLoopSource != null)
			{
				_moveBubbleStreamLoopSource.Stop();
			}
			if (_sprintWaterRushLoopSource != null)
			{
				_sprintWaterRushLoopSource.Stop();
			}
			if (_sprintFoamCavitationLoopSource != null)
			{
				_sprintFoamCavitationLoopSource.Stop();
			}
			if (_drownHeartbeatLoopSource != null)
			{
				_drownHeartbeatLoopSource.Stop();
			}
			if (_drownPressureLoopSource != null)
			{
				_drownPressureLoopSource.Stop();
			}
			if (!_isSurfaceSwimming)
			{
				StopSurfaceLoopSources();
			}
			StopBubblePopRoutine();
			if (_pressureVariationRoutine != null)
			{
				StopCoroutine(_pressureVariationRoutine);
				_pressureVariationRoutine = null;
			}
			MuteAllLayers();
			if (_underwaterAmbientLoopSource != null && _underwaterAmbientLoopSource.isPlaying)
			{
				_underwaterAmbientLoopSource.Stop();
			}
			if (_isSurfaceSwimming)
			{
				EnsureSurfaceLoopSourcesPlaying();
			}
			PlayRandomHeadSurfaceBreakSfx();
			PlayHeadSurfaceBreakScreenVfx();
			if (Player != null)
			{
				Player.PlayOceanExitVFX();
			}
		}

		private void ApplySurfaceSwimmingAudio()
		{
			EnsureSurfaceLoopSourcesPlaying();
			float surfaceSwimSpeed = GetSurfaceSwimSpeed01();
			float targetDb = ((surfaceSwimSpeed > 0.01f) ? (-80f) : _surfaceIdleVolumeDb);
			float targetDb2 = ((surfaceSwimSpeed > 0.01f) ? Mathf.Lerp(_surfaceSwimMoveVolumeDbRange.x, _surfaceSwimMoveVolumeDbRange.y, surfaceSwimSpeed) : (-80f));
			SetVolumeSmooth(_surfaceIdleLoopSource, targetDb, 0.4f);
			SetVolumeSmooth(_surfaceSwimMoveLoopSource, targetDb2, 0.25f);
			SetVolumeSmooth(_underwaterAmbientLoopSource, -80f, 0.4f);
			SetVolumeSmooth(_idlePressureLoopSource, -80f, 0.25f);
			SetVolumeSmooth(_moveBubbleStreamLoopSource, -80f, 0.25f);
			SetVolumeSmooth(_sprintWaterRushLoopSource, -80f, 0.25f);
			SetVolumeSmooth(_sprintFoamCavitationLoopSource, -80f, 0.25f);
			SetVolumeSmooth(_drownHeartbeatLoopSource, -80f, 0.35f);
			SetVolumeSmooth(_drownPressureLoopSource, -80f, 0.35f);
			if (_surfaceSwimMoveLoopSource != null)
			{
				float pitch = Mathf.Lerp(_surfaceSwimMovePitchRange.x, _surfaceSwimMovePitchRange.y, surfaceSwimSpeed);
				_surfaceSwimMoveLoopSource.pitch = pitch;
			}
		}

		private void UpdateContinuousAudio()
		{
			if (!_isUnderwater)
			{
				if (_isSurfaceSwimming)
				{
					ApplySurfaceSwimmingAudio();
				}
				return;
			}
			float targetDb = -4f;
			if (_currentState == OceanLocomotionState.Sprint)
			{
				targetDb = -6f;
			}
			SetVolumeSmooth(_underwaterAmbientLoopSource, targetDb, 0.4f);
			SetVolumeSmooth(_surfaceIdleLoopSource, -80f, 0.25f);
			SetVolumeSmooth(_surfaceSwimMoveLoopSource, -80f, 0.25f);
			switch (_currentState)
			{
			case OceanLocomotionState.Idle:
				ApplyIdleAudio();
				break;
			case OceanLocomotionState.Move:
				ApplyMoveAudio();
				break;
			case OceanLocomotionState.Sprint:
				ApplySprintAudio();
				break;
			case OceanLocomotionState.Drown:
				ApplyDrownAudio();
				break;
			}
		}

		private void ApplyIdleAudio()
		{
			SetVolumeSmooth(_idlePressureLoopSource, _pressureTargetDb, 0.25f);
			SetVolumeSmooth(_moveBubbleStreamLoopSource, -80f, 0.25f);
			SetVolumeSmooth(_sprintWaterRushLoopSource, -80f, 0.25f);
			SetVolumeSmooth(_sprintFoamCavitationLoopSource, -80f, 0.25f);
			SetVolumeSmooth(_drownHeartbeatLoopSource, -80f, 0.35f);
			SetVolumeSmooth(_drownPressureLoopSource, -80f, 0.35f);
		}

		private void ApplyMoveAudio()
		{
			float targetDb = Mathf.Lerp(_moveBubbleVolumeDbRange.x, _moveBubbleVolumeDbRange.y, _normalizedSpeed);
			_pressureTargetDb = -8f;
			SetVolumeSmooth(_idlePressureLoopSource, _pressureTargetDb, 0.25f);
			SetVolumeSmooth(_moveBubbleStreamLoopSource, targetDb, 0.25f);
			SetVolumeSmooth(_sprintWaterRushLoopSource, -80f, 0.25f);
			SetVolumeSmooth(_sprintFoamCavitationLoopSource, -80f, 0.25f);
			SetVolumeSmooth(_drownHeartbeatLoopSource, -80f, 0.35f);
			SetVolumeSmooth(_drownPressureLoopSource, -80f, 0.35f);
			if (_moveBubbleStreamLoopSource != null)
			{
				float pitch = Mathf.Lerp(_movePitchRange.x, _movePitchRange.y, _normalizedSpeed);
				_moveBubbleStreamLoopSource.pitch = pitch;
			}
		}

		private void ApplySprintAudio()
		{
			float targetDb = Mathf.Lerp(_sprintWaterRushVolumeDbRange.x, _sprintWaterRushVolumeDbRange.y, _normalizedSpeed);
			float targetDb2 = Mathf.Lerp(_sprintFoamVolumeDbRange.x, _sprintFoamVolumeDbRange.y, _normalizedSpeed);
			_pressureTargetDb = -80f;
			SetVolumeSmooth(_idlePressureLoopSource, _pressureTargetDb, 0.25f);
			SetVolumeSmooth(_moveBubbleStreamLoopSource, -80f, 0.25f);
			SetVolumeSmooth(_sprintWaterRushLoopSource, targetDb, 0.25f);
			SetVolumeSmooth(_sprintFoamCavitationLoopSource, targetDb2, 0.25f);
			SetVolumeSmooth(_drownHeartbeatLoopSource, -80f, 0.35f);
			SetVolumeSmooth(_drownPressureLoopSource, -80f, 0.35f);
			float pitch = Mathf.Lerp(_sprintPitchRange.x, _sprintPitchRange.y, _normalizedSpeed);
			if (_sprintWaterRushLoopSource != null)
			{
				_sprintWaterRushLoopSource.pitch = pitch;
			}
			if (_sprintFoamCavitationLoopSource != null)
			{
				_sprintFoamCavitationLoopSource.pitch = pitch;
			}
		}

		private void ApplyDrownAudio()
		{
			_pressureTargetDb = -80f;
			SetVolumeSmooth(_idlePressureLoopSource, _pressureTargetDb, 0.25f);
			SetVolumeSmooth(_moveBubbleStreamLoopSource, -80f, 0.25f);
			SetVolumeSmooth(_sprintWaterRushLoopSource, -80f, 0.25f);
			SetVolumeSmooth(_sprintFoamCavitationLoopSource, -80f, 0.25f);
			SetVolumeSmooth(_underwaterAmbientLoopSource, -12f, 0.4f);
			SetVolumeSmooth(_drownPressureLoopSource, -4f, 0.35f);
			SetVolumeSmooth(_drownHeartbeatLoopSource, -2f, 0.35f);
		}

		private void MuteAllLayers()
		{
			SetVolume(_underwaterAmbientLoopSource, -80f);
			SetVolume(_idlePressureLoopSource, -80f);
			SetVolume(_moveBubbleStreamLoopSource, -80f);
			SetVolume(_sprintWaterRushLoopSource, -80f);
			SetVolume(_sprintFoamCavitationLoopSource, -80f);
			SetVolume(_drownHeartbeatLoopSource, -80f);
			SetVolume(_drownPressureLoopSource, -80f);
			SetVolume(_surfaceIdleLoopSource, -80f);
			SetVolume(_surfaceSwimMoveLoopSource, -80f);
		}

		private void EnsureSurfaceLoopSourcesPlaying()
		{
			EnsureLoopSourcePlaying(_surfaceIdleLoopSource);
			EnsureLoopSourcePlaying(_surfaceSwimMoveLoopSource);
		}

		private static void EnsureLoopSourcePlaying(AudioSource source)
		{
			if (source != null && !source.isPlaying)
			{
				source.Play();
			}
		}

		private void StopSurfaceLoopSources()
		{
			if (_surfaceIdleLoopSource != null)
			{
				SetVolume(_surfaceIdleLoopSource, -80f);
				_surfaceIdleLoopSource.Stop();
			}
			if (_surfaceSwimMoveLoopSource != null)
			{
				SetVolume(_surfaceSwimMoveLoopSource, -80f);
				_surfaceSwimMoveLoopSource.Stop();
			}
		}

		private bool IsPlayerStillSurfaceSwimming()
		{
			if (Player != null && Player.FirstPersonCharacter != null)
			{
				return Player.FirstPersonCharacter.IsSwimming();
			}
			return false;
		}

		private float GetSurfaceSwimSpeed01()
		{
			if (Player == null || Player.FirstPersonCharacter == null)
			{
				return 0f;
			}
			float speed = Player.FirstPersonCharacter.speed;
			if (speed < _surfaceMoveSpeedThreshold)
			{
				return 0f;
			}
			float b = Mathf.Max(_surfaceMoveSpeedThreshold + 0.001f, Player.FirstPersonCharacter.maxSwimSpeed);
			return Mathf.InverseLerp(_surfaceMoveSpeedThreshold, b, speed);
		}

		private void SafeInitLoop(AudioSource source, bool playOnStart)
		{
			if (!(source == null))
			{
				source.loop = true;
				if (playOnStart && !source.isPlaying)
				{
					source.Play();
				}
			}
		}

		private void PreloadConfiguredAudioClips()
		{
			PreloadSourceClip(_underwaterAmbientLoopSource);
			PreloadSourceClip(_idlePressureLoopSource);
			PreloadSourceClip(_moveBubbleStreamLoopSource);
			PreloadSourceClip(_sprintWaterRushLoopSource);
			PreloadSourceClip(_sprintFoamCavitationLoopSource);
			PreloadSourceClip(_drownHeartbeatLoopSource);
			PreloadSourceClip(_drownPressureLoopSource);
			PreloadSourceClip(_surfaceIdleLoopSource);
			PreloadSourceClip(_surfaceSwimMoveLoopSource);
			PreloadSourceClip(_headSubmergeOneShotSource);
			PreloadSourceClip(_headSurfaceBreakOneShotSource);
			PreloadClips(_headSubmergeClips);
			PreloadClips(_headSurfaceBreakClips);
			PreloadClips(_idlePressureClips);
			PreloadClips(_idleBubblePopClips);
		}

		private static void PreloadSourceClip(AudioSource source)
		{
			if (source != null)
			{
				PreloadClip(source.clip);
			}
		}

		private static void PreloadClips(AudioClip[] clips)
		{
			if (clips != null)
			{
				for (int i = 0; i < clips.Length; i++)
				{
					PreloadClip(clips[i]);
				}
			}
		}

		private static void PreloadClip(AudioClip clip)
		{
			if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
			{
				clip.LoadAudioData();
			}
		}

		private void SafeInitOneShot(AudioSource source)
		{
			if (!(source == null))
			{
				source.loop = false;
			}
		}

		private void PlayHeadSurfaceBreakScreenVfx()
		{
			StopHeadSurfaceBreakScreenVfx();
			if (_headSurfaceBreakScreenVfxPrefab == null)
			{
				return;
			}
			Transform transform = ((Player != null && Player.MainCamera != null) ? Player.MainCamera.transform : null);
			if (transform == null)
			{
				UtilityMethods.Log("[OceanFXManager] HeadSurfaceBreak screen VFX could not play because the local ViewModel camera is unavailable.", LogType.Warning);
				return;
			}
			_headSurfaceBreakScreenVfxInstance = Object.Instantiate(_headSurfaceBreakScreenVfxPrefab, transform, worldPositionStays: false);
			_headSurfaceBreakScreenVfxInstance.name = _headSurfaceBreakScreenVfxPrefab.name + " (Runtime)";
			MainCameraAutoParent component = _headSurfaceBreakScreenVfxInstance.GetComponent<MainCameraAutoParent>();
			if (component != null)
			{
				component.enabled = false;
			}
			Transform obj = _headSurfaceBreakScreenVfxInstance.transform;
			obj.localPosition = Vector3.zero;
			obj.localRotation = Quaternion.identity;
			obj.localScale = Vector3.one;
			ParticleSystem[] componentsInChildren = _headSurfaceBreakScreenVfxInstance.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
			float num = 0.5f;
			ParticleSystem[] array = componentsInChildren;
			foreach (ParticleSystem obj2 in array)
			{
				ParticleSystem.MainModule main = obj2.main;
				main.loop = false;
				num = Mathf.Max(num, main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
				obj2.Stop(withChildren: false, ParticleSystemStopBehavior.StopEmittingAndClear);
				obj2.Play(withChildren: false);
			}
			Object.Destroy(_headSurfaceBreakScreenVfxInstance, num);
			if (_debug)
			{
				UtilityMethods.Log("[OceanFXManager] HeadSurfaceBreak screen VFX -> " + _headSurfaceBreakScreenVfxPrefab.name);
			}
		}

		private void StopHeadSurfaceBreakScreenVfx()
		{
			if (!(_headSurfaceBreakScreenVfxInstance == null))
			{
				_headSurfaceBreakScreenVfxInstance.SetActive(value: false);
				Object.Destroy(_headSurfaceBreakScreenVfxInstance);
				_headSurfaceBreakScreenVfxInstance = null;
			}
		}

		private void PlayObjectWaterImpactVfx(Vector3 position, bool isBig)
		{
			if (!(_objectWaterImpactVfxPrefab == null))
			{
				ParticleSystem particleSystem = Object.Instantiate(_objectWaterImpactVfxPrefab, position, Quaternion.identity);
				float b = (isBig ? _objectWaterImpactBigScale : _objectWaterImpactSmallScale);
				particleSystem.transform.localScale = Vector3.one * Mathf.Max(0.01f, b);
				particleSystem.Play(withChildren: true);
				float b2 = particleSystem.main.duration + particleSystem.main.startLifetime.constantMax;
				Object.Destroy(particleSystem.gameObject, Mathf.Max(0.5f, b2));
			}
		}

		private void PlayObjectWaterImpactSfx(Vector3 position, float speed, bool isBig)
		{
			if (!(Singleton<SoundManager>.Instance == null))
			{
				AudioClipSO audioClipSO = ((_objectWaterImpactSfx != null) ? _objectWaterImpactSfx : Singleton<SoundManager>.Instance.GetSoundByType(SoundType.Harpoon_Hit_Water));
				if (audioClipSO != null)
				{
					Singleton<SoundManager>.Instance.PlaySFX(audioClipSO, position);
				}
			}
		}

		private void SetVolume(AudioSource source, float db)
		{
			if (!(source == null))
			{
				float volume = DbToLinear(db);
				source.volume = volume;
			}
		}

		private float DbToLinear(float db)
		{
			return Mathf.Max(0f, Mathf.Pow(10f, db / 20f));
		}

		private void PlayRandomHeadSubmergeSfx()
		{
			if (_headSubmergeOneShotSource == null)
			{
				return;
			}
			AudioClip audioClip = null;
			if (_headSubmergeClips != null && _headSubmergeClips.Length != 0)
			{
				audioClip = _headSubmergeClips[Random.Range(0, _headSubmergeClips.Length)];
			}
			else if (_headSubmergeOneShotSource.clip != null)
			{
				audioClip = _headSubmergeOneShotSource.clip;
			}
			if (!(audioClip == null))
			{
				float num = Random.Range(_headTransitionPitchRange.x, _headTransitionPitchRange.y);
				float num2 = Random.Range(_headTransitionVolumeRange.x, _headTransitionVolumeRange.y);
				_headSubmergeOneShotSource.pitch = num;
				_headSubmergeOneShotSource.PlayOneShot(audioClip, num2);
				if (_debug)
				{
					UtilityMethods.Log($"[OceanFXManager] HeadSubmerge SFX -> {audioClip.name}, Pitch={num:F2}, Vol={num2:F2}");
				}
			}
		}

		private void PlayRandomHeadSurfaceBreakSfx()
		{
			if (_headSurfaceBreakOneShotSource == null)
			{
				return;
			}
			AudioClip audioClip = null;
			if (_headSurfaceBreakClips != null && _headSurfaceBreakClips.Length != 0)
			{
				audioClip = _headSurfaceBreakClips[Random.Range(0, _headSurfaceBreakClips.Length)];
			}
			else if (_headSurfaceBreakOneShotSource.clip != null)
			{
				audioClip = _headSurfaceBreakOneShotSource.clip;
			}
			if (!(audioClip == null))
			{
				float num = Random.Range(_headTransitionPitchRange.x, _headTransitionPitchRange.y);
				float num2 = Random.Range(_headTransitionVolumeRange.x, _headTransitionVolumeRange.y);
				_headSurfaceBreakOneShotSource.pitch = num;
				_headSurfaceBreakOneShotSource.PlayOneShot(audioClip, num2);
				if (_debug)
				{
					UtilityMethods.Log($"[OceanFXManager] HeadSurfaceBreak SFX -> {audioClip.name}, Pitch={num:F2}, Vol={num2:F2}");
				}
			}
		}

		private void StartBubblePopRoutine()
		{
			if (!(_idleBubblePopOneShotSource == null))
			{
				if (_bubblePopRoutine != null)
				{
					StopCoroutine(_bubblePopRoutine);
				}
				_bubblePopRoutine = StartCoroutine(BubblePopLoop());
			}
		}

		private void StopBubblePopRoutine()
		{
			if (_bubblePopRoutine != null)
			{
				StopCoroutine(_bubblePopRoutine);
				_bubblePopRoutine = null;
			}
		}

		private IEnumerator BubblePopLoop()
		{
			while (_isUnderwater && _currentState == OceanLocomotionState.Idle)
			{
				float seconds = Random.Range(_minBubblePopInterval, _maxBubblePopInterval);
				yield return new WaitForSeconds(seconds);
				if (!_isUnderwater || _currentState != OceanLocomotionState.Idle)
				{
					break;
				}
				if (_idleBubblePopClips != null && _idleBubblePopClips.Length != 0)
				{
					PlayRandomBubblePop();
				}
			}
		}

		private void PlayRandomBubblePop()
		{
			if (!(_idleBubblePopOneShotSource == null) && _idleBubblePopClips != null && _idleBubblePopClips.Length != 0)
			{
				AudioClip audioClip = _idleBubblePopClips[Random.Range(0, _idleBubblePopClips.Length)];
				_idleBubblePopOneShotSource.pitch = Random.Range(_bubblePitchRange.x, _bubblePitchRange.y);
				float num = Random.Range(_bubbleVolumeRange.x, _bubbleVolumeRange.y);
				_idleBubblePopOneShotSource.PlayOneShot(audioClip, num);
				if (_debug)
				{
					UtilityMethods.Log($"[OceanFXManager] Bubble pop: {audioClip.name} Pitch={_idleBubblePopOneShotSource.pitch:F2} Vol={num:F2}");
				}
			}
		}

		private void StartPressureVariationRoutine()
		{
			if (_idlePressureClips != null && _idlePressureClips.Length != 0)
			{
				if (_pressureVariationRoutine != null)
				{
					StopCoroutine(_pressureVariationRoutine);
				}
				_pressureVariationRoutine = StartCoroutine(PressureVariationLoop());
			}
		}

		private IEnumerator PressureVariationLoop()
		{
			while (_isUnderwater && _currentState == OceanLocomotionState.Idle)
			{
				yield return new WaitForSeconds(Random.Range(8f, 15f));
				if (!_isUnderwater || _currentState != OceanLocomotionState.Idle)
				{
					break;
				}
				ApplyRandomPressureVariation();
			}
		}

		private void ApplyRandomPressureVariation()
		{
			if (!(_idlePressureLoopSource == null) && _idlePressureClips != null && _idlePressureClips.Length != 0)
			{
				AudioClip audioClip = _idlePressureClips[Random.Range(0, _idlePressureClips.Length)];
				if (_idlePressureLoopSource.clip != audioClip)
				{
					PreloadClip(audioClip);
					_idlePressureLoopSource.clip = audioClip;
					EnsureLoopSourcePlaying(_idlePressureLoopSource);
				}
				_idlePressureLoopSource.pitch = Random.Range(_pressurePitchRange.x, _pressurePitchRange.y);
				_pressureTargetDb = Random.Range(_pressureVolumeRange.x, _pressureVolumeRange.y);
				if (_debug)
				{
					UtilityMethods.Log($"[OceanFXManager] Pressure variation: Clip={audioClip.name}, Pitch={_idlePressureLoopSource.pitch:F2}, dB={_pressureTargetDb:F1}");
				}
			}
		}

		private void StopRuntimeRoutines()
		{
			StopBubblePopRoutine();
			if (_pressureVariationRoutine != null)
			{
				StopCoroutine(_pressureVariationRoutine);
				_pressureVariationRoutine = null;
			}
		}

		private float LinearToDb(float linear)
		{
			if (linear <= 0.0001f)
			{
				return -80f;
			}
			return 20f * Mathf.Log10(linear);
		}

		private void SetVolumeSmooth(AudioSource source, float targetDb, float lerpTime)
		{
			if (!(source == null))
			{
				if (lerpTime <= 0f)
				{
					source.volume = DbToLinear(targetDb);
					return;
				}
				float a = LinearToDb(Mathf.Max(0.0001f, source.volume));
				float t = Time.deltaTime / Mathf.Max(0.0001f, lerpTime);
				float db = Mathf.Lerp(a, targetDb, t);
				source.volume = DbToLinear(db);
			}
		}

		private static bool IsFinite(Vector3 value)
		{
			if (float.IsFinite(value.x) && float.IsFinite(value.y))
			{
				return float.IsFinite(value.z);
			}
			return false;
		}
	}
}
