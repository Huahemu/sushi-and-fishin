using System;
using System.Collections.Generic;
using DG.Tweening;
using ECM2;
using ECM2.Examples.FirstPerson;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Data.Tools;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Wheel;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.PlayerTools
{
	public class SeaScooter : BaseTool
	{
		private const string ScooterMeshName = "UnderwaterScooter";

		[Header("Data")]
		[SerializeField]
		private SeaScooterData _data;

		[SerializeField]
		private ItemOffset _fpsOffset;

		[SerializeField]
		private ItemOffset _tpsOffset;

		[Header("Visual Pitch Sync")]
		[SerializeField]
		private bool _syncVisualPitch = true;

		[SerializeField]
		[Range(0f, 80f)]
		private float _maxVisualPitchAngle = 55f;

		[SerializeField]
		[Min(0f)]
		private float _visualPitchMinSpeed = 0.15f;

		[SerializeField]
		[Min(0.01f)]
		private float _visualPitchSmoothSpeed = 8f;

		[SerializeField]
		[Min(0f)]
		private float _visualPitchNetworkThreshold = 1.5f;

		[SerializeField]
		[Min(0.05f)]
		private float _visualPitchNetworkInterval = 0.1f;

		private readonly NetworkVariable<bool> _engineRun = new NetworkVariable<bool>(value: false);

		private readonly NetworkVariable<float> _visualPitch = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

		private readonly NetworkVariable<bool> _airbornePose = new NetworkVariable<bool>(value: false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

		private int _levelIndex;

		private bool _canMotorBeStarted;

		private bool _localScooterMovementActive;

		private byte _spawnGraphicLevel;

		private float _currentVisualPitch;

		private float _nextVisualPitchNetworkTime;

		private Quaternion _tpsVisualBaseLocalRotation = Quaternion.identity;

		private bool _hasTpsVisualBaseLocalRotation;

		private AudioSource _audioSource;

		[SerializeField]
		private AudioClip _engineRunAudioClip;

		[SerializeField]
		private float _engineFadeInDuration = 0.2f;

		[SerializeField]
		private float _engineFadeOutDuration = 0.25f;

		[SerializeField]
		private float _engineMinVolume = 0.1f;

		[SerializeField]
		private float _engineMaxVolume = 0.5f;

		[SerializeField]
		private float _engineMinPitch = 1f;

		[SerializeField]
		private float _engineMaxPitch = 1.3f;

		private bool _isLoopPlaying;

		private Tweener _engineVolumeTween;

		private AudioClip _cachedEngineRunAudioClip;

		private string[] _vfxNames = new string[2] { "VFX_UnderwaterMist", "VFX_CoreBubbles" };

		private List<ParticleSystem> _vfxList_FPS = new List<ParticleSystem>();

		private List<ParticleSystem> _vfxList_TPS = new List<ParticleSystem>();

		private bool _isRunning;

		private Transform _propellerFPS;

		private Transform _propellerTPS;

		private Tweener _tweenFPS;

		private Tweener _tweenTPS;

		private float _minSpinSpeed = 180f;

		private float _maxSpinSpeed = 1200f;

		private float _spinResponse = 4f;

		private float _currentSpinSpeed;

		public override byte Level => _player.ToolsData.ScooterLevel;

		public override bool IsUnlocked => _player.ToolsData.ScooterUnlocked;

		public override string Name
		{
			get
			{
				if (!(_data != null))
				{
					return "Sea Scooter";
				}
				return _data.Name;
			}
		}

		public override Sprite Icon
		{
			get
			{
				if (!(_data != null))
				{
					return null;
				}
				return _data.Sprite;
			}
		}

		public bool IsEngineRun => _engineRun.Value;

		public bool CanMotorBeStarted
		{
			get
			{
				return _canMotorBeStarted;
			}
			set
			{
				if (_canMotorBeStarted == value)
				{
					return;
				}
				_canMotorBeStarted = value;
				if (base.IsOwner)
				{
					if (_canMotorBeStarted)
					{
						SetAirbornePose(active: false);
						TryAutoStartEngineIfPossible();
					}
					else
					{
						bool airbornePose = ShouldStartAirbornePose();
						TryAutoStopEngineIfPossible(resetMovementMode: true);
						SetAirbornePose(airbornePose);
					}
				}
			}
		}

		private void Start()
		{
			if (_tpsGraphicParent != null)
			{
				_tpsGraphicParent.gameObject.SetActive(value: false);
			}
			if (_fpsGraphicParent != null)
			{
				_fpsGraphicParent.gameObject.SetActive(value: false);
			}
			TrySetupHandParentsAndOffsets();
			if (base.IsOwner)
			{
				_levelIndex = Level;
				ApplyLevelToCharacter(_levelIndex);
			}
		}

		private void OnDisable()
		{
			StopAllLocalEffects();
			ResetVisualPitchImmediate();
			SetAirbornePose(active: false);
			ApplyAirbornePose(active: false);
			if (base.IsOwner)
			{
				if (IsEngineRun)
				{
					RequestEngineRun(run: false);
				}
				SetLocalMovementMode(engineOn: false);
			}
		}

		protected override void NetworkSpawn()
		{
			base.NetworkSpawn();
			NetworkVariable<bool> engineRun = _engineRun;
			engineRun.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(engineRun.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(HandleEngineRunChanged));
			NetworkVariable<bool> airbornePose = _airbornePose;
			airbornePose.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(airbornePose.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(HandleAirbornePoseChanged));
			if (_player != null)
			{
				NetworkVariable<byte> colorIndex = _player.ColorIndex;
				colorIndex.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Combine(colorIndex.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(HandlePlayerColorIndexChanged));
			}
			ApplyAirbornePose(_airbornePose.Value);
			ApplySeaScooterMaterial((_player != null) ? _player.ColorIndex.Value : byte.MaxValue);
		}

		protected override void NetworkDespawn()
		{
			NetworkVariable<bool> engineRun = _engineRun;
			engineRun.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(engineRun.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(HandleEngineRunChanged));
			NetworkVariable<bool> airbornePose = _airbornePose;
			airbornePose.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(airbornePose.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(HandleAirbornePoseChanged));
			if (_player != null)
			{
				NetworkVariable<byte> colorIndex = _player.ColorIndex;
				colorIndex.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Remove(colorIndex.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(HandlePlayerColorIndexChanged));
			}
			ApplyAirbornePose(active: false);
			base.NetworkDespawn();
		}

		public override bool DisableIf()
		{
			return true;
		}

		public override void LoadLevel(ToolDataNet data)
		{
			ApplyLevel(data.ScooterLevel);
			if (base.IsOwner)
			{
				SetUnlock(data.ScooterUnlocked);
			}
		}

		public override void ApplyLevel(byte newLevel)
		{
			_spawnGraphicLevel = newLevel;
			base.ApplyLevel(newLevel);
			if (base.IsOwner)
			{
				ApplyLevelToCharacter(newLevel);
			}
		}

		protected override void SetUnlock(bool isUnlocked)
		{
			if (base.IsOwner && Singleton<PPG_WheelManager>.Instance != null)
			{
				Singleton<PPG_WheelManager>.Instance.UnlockScooter(isUnlocked);
			}
		}

		protected override GameObject GetGraphicByLevel()
		{
			return _data.Levels[_spawnGraphicLevel].GraphicPrefab;
		}

		protected override int GetLevelCount()
		{
			if (_data.Levels == null)
			{
				return 0;
			}
			return _data.Levels.Count;
		}

		protected override void SpawnGraphics()
		{
			base.SpawnGraphics();
			if (_tpsGraphic != null)
			{
				_tpsGraphic.transform.SetLocalPositionAndRotation(_tpsOffset.PositionOffset, Quaternion.Euler(_tpsOffset.RotationOffset));
			}
			if (base.IsOwner && _fpsGraphic != null)
			{
				_fpsGraphic.transform.SetLocalPositionAndRotation(_fpsOffset.PositionOffset, Quaternion.Euler(_fpsOffset.RotationOffset));
			}
			ApplySeaScooterMaterial((_player != null) ? _player.ColorIndex.Value : byte.MaxValue);
		}

		private void HandlePlayerColorIndexChanged(byte previous, byte current)
		{
			ApplySeaScooterMaterial(current);
		}

		private void ApplySeaScooterMaterial(byte colorIndex)
		{
			if (!(Singleton<PlayersManager>.Instance == null) && Singleton<PlayersManager>.Instance.TryGetSeaScooterMaterial(colorIndex, out var material))
			{
				ApplySeaScooterMaterial(_tpsGraphic, material);
				if (base.IsOwner)
				{
					ApplySeaScooterMaterial(_fpsGraphic, material);
				}
			}
		}

		private void ApplySeaScooterMaterial(GameObject graphic, Material material)
		{
			if (!(graphic == null))
			{
				Transform transform = FindDeepChild(graphic.transform, "UnderwaterScooter");
				if (!(transform == null) && transform.TryGetComponent<Renderer>(out var component))
				{
					component.sharedMaterial = material;
				}
			}
		}

		private void ApplyLevelToCharacter(int index)
		{
			_levelIndex = index;
			if (TryGetLevel(_levelIndex, out var level))
			{
				FirstPersonCharacter firstPersonCharacter = _player?.FirstPersonCharacter;
				if (!(firstPersonCharacter == null))
				{
					firstPersonCharacter.maxSeaScooterSpeed = level.MaxSeaScooterSpeed;
					firstPersonCharacter.brakingDecelerationSeaScooter = level.BrakingDecelerationSeaScooter;
					firstPersonCharacter.seaScooterFriction = level.SeaScooterFriction;
				}
			}
		}

		private bool TryGetLevel(int idx, out SeaScooterLevelData level)
		{
			level = null;
			if (_data == null || _data.Levels == null || _data.Levels.Count == 0)
			{
				return false;
			}
			idx = Mathf.Clamp(idx, 0, _data.Levels.Count - 1);
			level = _data.Levels[idx];
			return level != null;
		}

		protected override void ApplyToolStatus(bool status)
		{
			UtilityMethods.Log("SeaScooter-ApplyToolStatus: " + status);
			if (!status)
			{
				EngineStopAudioClip();
				KillTweens();
				Vfx_StopAllImmediate();
				ResetVisualPitchImmediate();
				SetAirbornePose(active: false);
				if (base.IsOwner)
				{
					SetLocalMovementMode(engineOn: false);
				}
				if (base.IsOwner)
				{
					RequestEngineRun(run: false);
				}
			}
			base.ApplyToolStatus(status);
			_player.AnimSetBool(PPG_Player.SCOOTER, status);
			ApplyScooterConstraintState(status);
			if (status)
			{
				TryResolveRuntimeRefs();
				EnsureVfxReferencesResolved();
				EnsureEngineAudioMatchesState();
				TryAutoStartEngineIfPossible();
			}
		}

		private void TryAutoStartEngineIfPossible()
		{
			if (base.IsOwner && base.ToolStatus && CanMotorBeStarted)
			{
				if (!IsEngineRun)
				{
					RequestEngineRun(run: true);
				}
				SetLocalMovementMode(engineOn: true);
			}
		}

		private void TryAutoStopEngineIfPossible(bool resetMovementMode)
		{
			if (base.IsOwner)
			{
				if (resetMovementMode)
				{
					SetLocalMovementMode(engineOn: false);
				}
				RequestEngineRun(run: false);
				StopAllLocalEffects();
			}
		}

		private void ApplyScooterConstraintState(bool isUsingScooter)
		{
			if (!(_player == null))
			{
				if (isUsingScooter)
				{
					_player.PauseConstraints();
				}
				else
				{
					_player.ResumeConstraintsIfAllowed();
				}
			}
		}

		private void LateUpdate()
		{
			UpdateAirbornePose();
			UpdateScooterVisualPitch();
		}

		private void UpdateAirbornePose()
		{
			if (base.IsOwner && _airbornePose.Value)
			{
				FirstPersonCharacter firstPersonCharacter = _player?.FirstPersonCharacter;
				if (!base.ToolStatus || _canMotorBeStarted || !(firstPersonCharacter != null) || !firstPersonCharacter.IsFalling() || firstPersonCharacter.IsGrounded())
				{
					SetAirbornePose(active: false);
				}
			}
		}

		private bool ShouldStartAirbornePose()
		{
			if (base.ToolStatus)
			{
				return _localScooterMovementActive;
			}
			return false;
		}

		private void SetAirbornePose(bool active)
		{
			if (base.IsOwner)
			{
				if (base.IsSpawned && _airbornePose.Value != active)
				{
					_airbornePose.Value = active;
				}
				ApplyAirbornePose(active);
			}
		}

		private void HandleAirbornePoseChanged(bool previous, bool current)
		{
			ApplyAirbornePose(current);
		}

		private void ApplyAirbornePose(bool active)
		{
			if (_player != null)
			{
				_player.SetScooterAirborneAnimator(active);
			}
		}

		private void UpdateScooterVisualPitch()
		{
			if (_syncVisualPitch && !(_player == null) && !(_player.TPS_Character == null))
			{
				if (_player.IsSurfing)
				{
					_currentVisualPitch = 0f;
				}
				else if (base.ToolStatus || !(Mathf.Abs(_currentVisualPitch) < 0.01f))
				{
					CacheTpsVisualBaseRotation();
					float b = (base.ToolStatus ? GetTargetVisualPitch() : 0f);
					float t = 1f - Mathf.Exp((0f - _visualPitchSmoothSpeed) * Time.deltaTime);
					_currentVisualPitch = Mathf.Lerp(_currentVisualPitch, b, t);
					_player.TPS_Character.transform.localRotation = _tpsVisualBaseLocalRotation * Quaternion.Euler(_currentVisualPitch, 0f, 0f);
				}
			}
		}

		private float GetTargetVisualPitch()
		{
			if (base.IsOwner)
			{
				float num = CalculateOwnerVisualPitch();
				SyncVisualPitch(num);
				return num;
			}
			return _visualPitch.Value;
		}

		private float CalculateOwnerVisualPitch()
		{
			FirstPersonCharacter firstPersonCharacter = _player?.FirstPersonCharacter;
			if (firstPersonCharacter == null)
			{
				return 0f;
			}
			if (!IsEngineRun || firstPersonCharacter.velocity.magnitude < _visualPitchMinSpeed)
			{
				return 0f;
			}
			return Mathf.Clamp(firstPersonCharacter.CameraPitch, 0f - _maxVisualPitchAngle, _maxVisualPitchAngle);
		}

		private void SyncVisualPitch(float targetPitch)
		{
			if (base.IsOwner && base.IsSpawned)
			{
				float num = Mathf.Abs(_visualPitch.Value - targetPitch);
				bool flag = Mathf.Abs(targetPitch) <= 0.01f && num > 0.01f;
				if ((flag || !(Time.unscaledTime < _nextVisualPitchNetworkTime)) && (flag || !(num <= _visualPitchNetworkThreshold)))
				{
					_visualPitch.Value = targetPitch;
					_nextVisualPitchNetworkTime = Time.unscaledTime + Mathf.Max(0.05f, _visualPitchNetworkInterval);
				}
			}
		}

		private void ResetVisualPitchImmediate()
		{
			if (!(_player == null) && !(_player.TPS_Character == null))
			{
				_currentVisualPitch = 0f;
				if (!_player.IsSurfing)
				{
					CacheTpsVisualBaseRotation();
					_player.TPS_Character.transform.localRotation = _tpsVisualBaseLocalRotation;
				}
				if (base.IsOwner && base.IsSpawned && !Mathf.Approximately(_visualPitch.Value, 0f))
				{
					_visualPitch.Value = 0f;
				}
				_nextVisualPitchNetworkTime = 0f;
			}
		}

		private void CacheTpsVisualBaseRotation()
		{
			if (!_hasTpsVisualBaseLocalRotation && !(_player == null) && !(_player.TPS_Character == null))
			{
				_tpsVisualBaseLocalRotation = _player.TPS_Character.transform.localRotation;
				_hasTpsVisualBaseLocalRotation = true;
			}
		}

		private void FixedUpdate()
		{
			if (!base.ToolStatus)
			{
				return;
			}
			FirstPersonCharacter firstPersonCharacter = _player?.FirstPersonCharacter;
			if (!(firstPersonCharacter == null))
			{
				Transform prop = (base.IsOwner ? _propellerFPS : _propellerTPS);
				Propulsion_UpdateSpinFor(prop, ref base.IsOwner ? ref _tweenFPS : ref _tweenTPS);
				Vfx_FixedUpdate();
				if (TryGetLevel(_levelIndex, out var level) && !(level.MaxSeaScooterSpeed <= 0.0001f))
				{
					float speed = Mathf.Clamp01(firstPersonCharacter.velocity.magnitude / level.MaxSeaScooterSpeed);
					UpdateLoopBySpeed(speed);
				}
			}
		}

		private void RequestEngineRun(bool run)
		{
			if (base.IsOwner)
			{
				SetEngineRunServerRpc(run);
			}
		}

		private void SetLocalMovementMode(bool engineOn)
		{
			FirstPersonCharacter firstPersonCharacter = _player?.FirstPersonCharacter;
			if (firstPersonCharacter == null)
			{
				if (!engineOn)
				{
					_localScooterMovementActive = false;
				}
			}
			else if (engineOn)
			{
				if (CanMotorBeStarted)
				{
					firstPersonCharacter.SetMovementMode(Character.MovementMode.SeaScooter);
					_localScooterMovementActive = true;
				}
			}
			else
			{
				_localScooterMovementActive = false;
				if (firstPersonCharacter.IsSeaScooter())
				{
					firstPersonCharacter.SetMovementMode(firstPersonCharacter.IsInWaterPhysicsVolume() ? Character.MovementMode.Swimming : Character.MovementMode.Falling);
				}
			}
		}

		[ServerRpc]
		public void SetEngineRunServerRpc(bool run, ServerRpcParams rpcParams = default(ServerRpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute && (networkManager.IsClient || networkManager.IsHost))
			{
				if (base.OwnerClientId != networkManager.LocalClientId)
				{
					if (networkManager.LogLevel <= LogLevel.Normal)
					{
						Debug.LogError("Only the owner can invoke a ServerRpc that requires ownership!");
					}
					return;
				}
				FastBufferWriter bufferWriter = __beginSendServerRpc(1437117190u, rpcParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in run, default(FastBufferWriter.ForPrimitives));
				__endSendServerRpc(ref bufferWriter, 1437117190u, rpcParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute && (networkManager.IsServer || networkManager.IsHost))
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsServer && rpcParams.Receive.SenderClientId == base.OwnerClientId)
				{
					_engineRun.Value = run;
				}
			}
		}

		private void HandleEngineRunChanged(bool prev, bool curr)
		{
			UtilityMethods.Log("CurrentEngineState:" + curr);
			if (curr)
			{
				EngineStart();
			}
			else
			{
				EngineStopAudioClip();
			}
		}

		private void TrySetupHandParentsAndOffsets()
		{
			if (!(_player == null) && !(_data == null))
			{
				if (_tpsGraphicParent != null && _player.RigBones?.TpsRig?.LeftHand != null)
				{
					_tpsGraphicParent.SetParent(_player.RigBones.TpsRig.LeftHand);
					_tpsGraphicParent.transform.SetLocalPositionAndRotation(_data.OffsetTps.PositionOffset, Quaternion.Euler(_data.OffsetTps.RotationOffset));
				}
				if (base.IsOwner && _fpsGraphicParent != null && _player.RigBones?.FpsRig?.LeftHand != null)
				{
					_fpsGraphicParent.SetParent(_player.RigBones.FpsRig.LeftHand);
					_fpsGraphicParent.transform.SetLocalPositionAndRotation(_data.OffsetFps.PositionOffset, Quaternion.Euler(_data.OffsetFps.RotationOffset));
				}
			}
		}

		private void TryResolveRuntimeRefs()
		{
			if (_audioSource == null)
			{
				_audioSource = TryFindAudioSource(_fpsGraphicParent) ?? TryFindAudioSource(_tpsGraphicParent);
			}
			if (_propellerFPS == null && _fpsGraphicParent != null)
			{
				_propellerFPS = FindDeepChild(_fpsGraphicParent, "Propeller");
			}
			if (_propellerTPS == null && _tpsGraphicParent != null)
			{
				_propellerTPS = FindDeepChild(_tpsGraphicParent, "Propeller");
			}
		}

		private AudioSource TryFindAudioSource(Transform parent)
		{
			if (parent == null)
			{
				return null;
			}
			Transform transform = FindDeepChild(parent, "AudioPrefab");
			if (transform != null && transform.TryGetComponent<AudioSource>(out var component))
			{
				return component;
			}
			return parent.GetComponentInChildren<AudioSource>(includeInactive: true);
		}

		private void StopAllLocalEffects()
		{
			StopEngineAudioImmediate();
			KillTweens();
			Vfx_StopAllImmediate();
		}

		private Transform FindDeepChild(Transform parent, string childName)
		{
			if (parent == null)
			{
				return null;
			}
			Transform[] componentsInChildren = parent.GetComponentsInChildren<Transform>(includeInactive: true);
			foreach (Transform transform in componentsInChildren)
			{
				if (transform.name == childName)
				{
					return transform;
				}
			}
			return null;
		}

		private void EnsureVfxReferencesResolved()
		{
			if (_vfxList_FPS.Count >= _vfxNames.Length && _vfxList_TPS.Count >= _vfxNames.Length)
			{
				return;
			}
			_vfxList_FPS.Clear();
			_vfxList_TPS.Clear();
			string[] vfxNames = _vfxNames;
			foreach (string childName in vfxNames)
			{
				if (_fpsGraphicParent != null)
				{
					ParticleSystem particleSystem = FindDeepChild(_fpsGraphicParent, childName)?.GetComponent<ParticleSystem>();
					if (particleSystem != null)
					{
						_vfxList_FPS.Add(particleSystem);
					}
				}
				if (_tpsGraphicParent != null)
				{
					ParticleSystem particleSystem2 = FindDeepChild(_tpsGraphicParent, childName)?.GetComponent<ParticleSystem>();
					if (particleSystem2 != null)
					{
						_vfxList_TPS.Add(particleSystem2);
					}
				}
			}
		}

		private void Vfx_PlayAll(bool play)
		{
			foreach (ParticleSystem item in base.IsOwner ? _vfxList_FPS : _vfxList_TPS)
			{
				if (!(item == null))
				{
					bool isPlaying = item.isPlaying;
					if (play && !isPlaying)
					{
						item.Play(withChildren: true);
					}
					else if (!play & isPlaying)
					{
						item.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
					}
				}
			}
		}

		private void Vfx_FixedUpdate()
		{
			Vfx_PlayAll(IsEngineRun);
		}

		private void Vfx_StopAllImmediate()
		{
			if (_vfxList_FPS != null)
			{
				for (int i = 0; i < _vfxList_FPS.Count; i++)
				{
					ParticleSystem particleSystem = _vfxList_FPS[i];
					if (particleSystem != null)
					{
						particleSystem.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
					}
				}
			}
			if (_vfxList_TPS == null)
			{
				return;
			}
			for (int j = 0; j < _vfxList_TPS.Count; j++)
			{
				ParticleSystem particleSystem2 = _vfxList_TPS[j];
				if (particleSystem2 != null)
				{
					particleSystem2.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
				}
			}
		}

		public void EngineStart()
		{
			TryResolveRuntimeRefs();
			if (_audioSource == null)
			{
				return;
			}
			AudioClip engineRunAudioClip = GetEngineRunAudioClip();
			if (!(engineRunAudioClip == null))
			{
				_engineVolumeTween?.Kill();
				_isRunning = true;
				_audioSource.clip = engineRunAudioClip;
				_audioSource.loop = true;
				_audioSource.pitch = _engineMinPitch;
				if (!_audioSource.isPlaying)
				{
					_audioSource.volume = Mathf.Max(0f, _engineMinVolume);
					_audioSource.Play();
				}
				_isLoopPlaying = true;
				_engineVolumeTween = _audioSource.DOFade(_engineMaxVolume, Mathf.Max(0f, _engineFadeInDuration)).SetEase(Ease.OutSine);
			}
		}

		private void EnsureEngineAudioMatchesState()
		{
			if (IsEngineRun)
			{
				EngineStart();
			}
			else
			{
				EngineStopAudioClip();
			}
		}

		public void UpdateLoopBySpeed(float speed01)
		{
			if (!(_audioSource == null) && _isLoopPlaying)
			{
				speed01 = Mathf.Clamp01(speed01);
				_audioSource.pitch = Mathf.Lerp(_engineMinPitch, _engineMaxPitch, speed01);
				if (_engineVolumeTween == null || !_engineVolumeTween.IsActive())
				{
					_audioSource.volume = Mathf.Lerp(_engineMinVolume, _engineMaxVolume, speed01);
				}
			}
		}

		public void EngineStopAudioClip()
		{
			if (!(_audioSource == null) && _isRunning)
			{
				_engineVolumeTween?.Kill();
				_isRunning = false;
				_isLoopPlaying = false;
				float num = Mathf.Max(0f, _engineFadeOutDuration);
				if (num <= 0f)
				{
					StopEngineLoopImmediate();
				}
				else
				{
					_engineVolumeTween = _audioSource.DOFade(0f, num).SetEase(Ease.InSine).OnComplete(StopEngineLoopImmediate);
				}
			}
		}

		private void StopEngineLoopImmediate()
		{
			if (!(_audioSource == null))
			{
				_audioSource.Stop();
				_audioSource.loop = false;
				_audioSource.volume = 0f;
				_audioSource.pitch = _engineMinPitch;
			}
		}

		private AudioClip GetEngineRunAudioClip()
		{
			if (_engineRunAudioClip != null)
			{
				return _engineRunAudioClip;
			}
			if (_cachedEngineRunAudioClip != null)
			{
				return _cachedEngineRunAudioClip;
			}
			if (_audioSource != null && _audioSource.clip != null)
			{
				_cachedEngineRunAudioClip = _audioSource.clip;
			}
			return _cachedEngineRunAudioClip;
		}

		private void StopEngineAudioImmediate()
		{
			if (_engineVolumeTween != null && _engineVolumeTween.IsActive())
			{
				_engineVolumeTween.Kill();
			}
			_engineVolumeTween = null;
			_isRunning = false;
			_isLoopPlaying = false;
			StopEngineLoopImmediate();
		}

		private void Propulsion_UpdateSpinFor(Transform prop, ref Tweener tween)
		{
			SeaScooterLevelData level;
			if (!IsEngineRun)
			{
				Propulsion_KillTween(ref tween);
				_currentSpinSpeed = 0f;
			}
			else if (!(prop == null) && _player?.FirstPersonCharacter != null && TryGetLevel(_levelIndex, out level))
			{
				float magnitude = _player.FirstPersonCharacter.velocity.magnitude;
				float num = Mathf.Max(0.001f, level.MaxSeaScooterSpeed);
				float t = Mathf.Clamp01(magnitude / num);
				float b = Mathf.Lerp(_minSpinSpeed, _maxSpinSpeed, t);
				_currentSpinSpeed = Mathf.Lerp(_currentSpinSpeed, b, _spinResponse * Time.deltaTime);
				if (tween == null || !tween.IsActive())
				{
					tween = Propulsion_StartSpinTween(prop);
				}
				tween.timeScale = _currentSpinSpeed / 360f;
			}
		}

		private Tweener Propulsion_StartSpinTween(Transform target)
		{
			return target.DOLocalRotate(new Vector3(0f, 0f, -360f), 1f, RotateMode.FastBeyond360).SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart)
				.SetUpdate(UpdateType.Normal, isIndependentUpdate: true);
		}

		private void Propulsion_KillTween(ref Tweener tween)
		{
			if (tween != null && tween.IsActive())
			{
				tween.Kill();
			}
			tween = null;
		}

		private void KillTweens()
		{
			Propulsion_KillTween(ref _tweenFPS);
			Propulsion_KillTween(ref _tweenTPS);
		}

		protected override void __initializeVariables()
		{
			if (_engineRun == null)
			{
				throw new Exception("SeaScooter._engineRun cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_engineRun.Initialize(this);
			__nameNetworkVariable(_engineRun, "_engineRun");
			NetworkVariableFields.Add(_engineRun);
			if (_visualPitch == null)
			{
				throw new Exception("SeaScooter._visualPitch cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_visualPitch.Initialize(this);
			__nameNetworkVariable(_visualPitch, "_visualPitch");
			NetworkVariableFields.Add(_visualPitch);
			if (_airbornePose == null)
			{
				throw new Exception("SeaScooter._airbornePose cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_airbornePose.Initialize(this);
			__nameNetworkVariable(_airbornePose, "_airbornePose");
			NetworkVariableFields.Add(_airbornePose);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(1437117190u, __rpc_handler_1437117190, "SetEngineRunServerRpc", RpcInvokePermission.Owner);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_1437117190(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			if (rpcParams.Server.Receive.SenderClientId != target.OwnerClientId)
			{
				if (networkManager.LogLevel <= LogLevel.Normal)
				{
					Debug.LogError("Only the owner can invoke a ServerRpc that requires ownership!");
				}
			}
			else
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				ServerRpcParams server = rpcParams.Server;
				((SeaScooter)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((SeaScooter)target).SetEngineRunServerRpc(value, server);
				((SeaScooter)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "SeaScooter";
		}
	}
}
