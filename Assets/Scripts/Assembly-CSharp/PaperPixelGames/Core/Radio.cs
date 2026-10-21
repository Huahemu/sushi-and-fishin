using System;
using System.Collections;
using System.Collections.Generic;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core
{
	public class Radio : BaseInteractable, ISaveable<RadioData>
	{
		[Header("State")]
		[SerializeField]
		private PhysicalStateController _physicalStateController;

		[Header("Audio")]
		[SerializeField]
		private AudioSource _audioSource;

		[Header("Music Clips")]
		[SerializeField]
		private List<AudioClip> _musicClips = new List<AudioClip>();

		[Header("Settings")]
		[SerializeField]
		private bool _avoidSameClipBackToBack = true;

		[SerializeField]
		[Min(0f)]
		private float _delayBetweenTracks = 0.25f;

		[SerializeField]
		private bool _preloadAudioData = true;

		private readonly NetworkVariable<int> _currentClipIndex = new NetworkVariable<int>(-1);

		private readonly NetworkVariable<bool> _isPlaying = new NetworkVariable<bool>(value: false);

		private Coroutine _serverTrackRoutine;

		private int _lastClipIndex = -1;

		public bool IsPlaying => _isPlaying.Value;

		public bool IsPhysicallyUsable
		{
			get
			{
				if (!(_physicalStateController == null))
				{
					return _physicalStateController.IsPhysicallyUsable;
				}
				return true;
			}
		}

		public RadioData Save()
		{
			return new RadioData
			{
				IsPhysicallyUsable = (_physicalStateController == null || _physicalStateController.IsPhysicallyUsable)
			};
		}

		public void Load(RadioData data)
		{
			if (!base.IsServer)
			{
				return;
			}
			ServerStop();
			if (!(_physicalStateController == null))
			{
				bool flag = data?.IsPhysicallyUsable ?? IsPhysicallyUsable;
				_physicalStateController.ServerSetUsable(flag);
				if (!flag)
				{
					StopLocalAudio();
				}
			}
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			SetupAudioSource();
			if (_preloadAudioData)
			{
				PreloadClips();
			}
			if (_physicalStateController != null)
			{
				_physicalStateController.OnPhysicallyUsableChanged += OnPhysicallyUsableChanged;
			}
			if (_isPlaying.Value && IsValidClipIndex(_currentClipIndex.Value) && IsPhysicallyUsable)
			{
				PlayClipLocally(_currentClipIndex.Value);
			}
			else
			{
				StopLocalAudio();
			}
		}

		public override void OnNetworkDespawn()
		{
			base.OnNetworkDespawn();
			StopServerTrackRoutine();
			StopLocalAudio();
			if (_physicalStateController != null)
			{
				_physicalStateController.OnPhysicallyUsableChanged -= OnPhysicallyUsableChanged;
			}
		}

		protected override void ClientInteract(Interactor interactor, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started)
			{
				if (_physicalStateController != null && !_physicalStateController.IsPhysicallyUsable)
				{
					RequestRadioRepairRpc();
				}
				else
				{
					RequestToggleRpc();
				}
			}
		}

		protected override void ClientCrossInteract(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started)
			{
				if (_physicalStateController != null && !_physicalStateController.IsPhysicallyUsable)
				{
					RequestRadioRepairRpc();
				}
				else
				{
					RequestToggleRpc();
				}
			}
		}

		protected override void ClientMouse1(Interactor interactor, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started && IsPhysicallyUsable)
			{
				RequestNextTrackRpc();
			}
		}

		public void BreakFromHarpoon()
		{
			RequestRadioBreakRpc();
		}

		[Rpc(SendTo.Server)]
		private void RequestRadioRepairRpc()
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(3920565185u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3920565185u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!(_physicalStateController == null) && !_physicalStateController.IsPhysicallyUsable)
				{
					_physicalStateController.Repair();
					ClientPlayRadioFeedbackRpc(SoundType.Repaired);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void RequestRadioBreakRpc()
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(3050757064u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3050757064u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!(_physicalStateController == null) && _physicalStateController.IsPhysicallyUsable)
				{
					_physicalStateController.BreakFromHarpoon();
					ClientPlayRadioFeedbackRpc(SoundType.Radio_Break);
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientPlayRadioFeedbackRpc(SoundType soundType)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(2827431988u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in soundType, default(FastBufferWriter.ForEnums));
				__endSendRpc(ref bufferWriter, 2827431988u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!(Singleton<SoundManager>.Instance == null))
				{
					Singleton<SoundManager>.Instance.PlaySFX(soundType, base.transform.position);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void RequestToggleRpc()
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(2581218689u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 2581218689u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (CanUseRadioOnServer())
			{
				if (_isPlaying.Value)
				{
					ClientPlayRadioFeedbackRpc(SoundType.Radio_PowerOff);
					ServerStop();
				}
				else
				{
					ClientPlayRadioFeedbackRpc(SoundType.Radio_PowerOn);
					ServerPlayRandomClip();
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void RequestNextTrackRpc()
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(917932680u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 917932680u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (CanUseRadioOnServer())
				{
					ClientPlayRadioFeedbackRpc(SoundType.Radio_ChangeTrack);
					ServerPlayRandomClip();
				}
			}
		}

		private bool CanUseRadioOnServer()
		{
			if (!base.IsServer)
			{
				return false;
			}
			if (_physicalStateController != null && !_physicalStateController.IsPhysicallyUsable)
			{
				ServerStop();
				return false;
			}
			return true;
		}

		private void ServerPlayRandomClip()
		{
			if (CanUseRadioOnServer())
			{
				if (!TryGetRandomClipIndex(out var clipIndex))
				{
					ServerStop();
				}
				else
				{
					ServerPlayClip(clipIndex);
				}
			}
		}

		private void ServerPlayClip(int clipIndex)
		{
			if (CanUseRadioOnServer() && IsValidClipIndex(clipIndex))
			{
				AudioClip audioClip = _musicClips[clipIndex];
				_lastClipIndex = clipIndex;
				_currentClipIndex.Value = clipIndex;
				_isPlaying.Value = true;
				ClientPlayClipRpc(clipIndex);
				RestartServerTrackRoutine(audioClip.length);
			}
		}

		private void ServerStop()
		{
			if (base.IsServer)
			{
				StopServerTrackRoutine();
				_isPlaying.Value = false;
				_currentClipIndex.Value = -1;
				ClientStopRpc();
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientPlayClipRpc(int clipIndex)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(1807864887u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, clipIndex);
				__endSendRpc(ref bufferWriter, 1807864887u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!IsPhysicallyUsable)
				{
					StopLocalAudio();
				}
				else
				{
					PlayClipLocally(clipIndex);
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientStopRpc()
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(363864640u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 363864640u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				StopLocalAudio();
			}
		}

		private void PlayClipLocally(int clipIndex)
		{
			if (!(_audioSource == null) && IsValidClipIndex(clipIndex))
			{
				AudioClip clip = _musicClips[clipIndex];
				_audioSource.Stop();
				_audioSource.clip = clip;
				_audioSource.Play();
			}
		}

		private void StopLocalAudio()
		{
			if (!(_audioSource == null))
			{
				_audioSource.Stop();
			}
		}

		private void RestartServerTrackRoutine(float clipLength)
		{
			StopServerTrackRoutine();
			_serverTrackRoutine = StartCoroutine(ServerTrackRoutine(clipLength));
		}

		private void StopServerTrackRoutine()
		{
			if (_serverTrackRoutine != null)
			{
				StopCoroutine(_serverTrackRoutine);
				_serverTrackRoutine = null;
			}
		}

		private IEnumerator ServerTrackRoutine(float clipLength)
		{
			yield return new WaitForSeconds(clipLength + _delayBetweenTracks);
			if (_isPlaying.Value && CanUseRadioOnServer())
			{
				ServerPlayRandomClip();
			}
		}

		private bool TryGetRandomClipIndex(out int clipIndex)
		{
			clipIndex = -1;
			if (_musicClips == null || _musicClips.Count == 0)
			{
				return false;
			}
			if (_musicClips.Count == 1)
			{
				clipIndex = ((!IsValidClipIndex(0)) ? (-1) : 0);
				return clipIndex >= 0;
			}
			for (int i = 0; i < 12; i++)
			{
				int num = UnityEngine.Random.Range(0, _musicClips.Count);
				if (IsValidClipIndex(num) && (!_avoidSameClipBackToBack || num != _lastClipIndex))
				{
					clipIndex = num;
					return true;
				}
			}
			for (int j = 0; j < _musicClips.Count; j++)
			{
				if (IsValidClipIndex(j) && (!_avoidSameClipBackToBack || j != _lastClipIndex))
				{
					clipIndex = j;
					return true;
				}
			}
			return false;
		}

		private bool IsValidClipIndex(int clipIndex)
		{
			if (_musicClips != null && clipIndex >= 0 && clipIndex < _musicClips.Count)
			{
				return _musicClips[clipIndex] != null;
			}
			return false;
		}

		private void SetupAudioSource()
		{
			if (_audioSource == null)
			{
				_audioSource = GetComponent<AudioSource>();
			}
			if (!(_audioSource == null))
			{
				_audioSource.loop = false;
				_audioSource.playOnAwake = false;
				_audioSource.dopplerLevel = 0f;
				_audioSource.spatialBlend = 1f;
			}
		}

		private void PreloadClips()
		{
			if (_musicClips == null)
			{
				return;
			}
			foreach (AudioClip musicClip in _musicClips)
			{
				if (!(musicClip == null) && musicClip.loadState == AudioDataLoadState.Unloaded)
				{
					musicClip.LoadAudioData();
				}
			}
		}

		private void OnPhysicallyUsableChanged(bool usable)
		{
			if (!usable)
			{
				StopLocalAudio();
				if (base.IsServer)
				{
					ServerStop();
				}
			}
			if (_inGaze)
			{
				DisableGazeHints();
				EnableGazeHints();
			}
		}

		public override void EnableGazeHints()
		{
			if (!(Singleton<HintsManager>.Instance == null))
			{
				if (!IsPhysicallyUsable)
				{
					Singleton<HintsManager>.Instance.AddHint("Interact", "key_repair");
					return;
				}
				Singleton<HintsManager>.Instance.AddHint("Interact", "key_use");
				Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_interact");
			}
		}

		public override void DisableGazeHints()
		{
			if (!(Singleton<HintsManager>.Instance == null))
			{
				Singleton<HintsManager>.Instance.RemoveHint("Interact");
				Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
			}
		}

		protected override void __initializeVariables()
		{
			if (_currentClipIndex == null)
			{
				throw new Exception("Radio._currentClipIndex cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_currentClipIndex.Initialize(this);
			__nameNetworkVariable(_currentClipIndex, "_currentClipIndex");
			NetworkVariableFields.Add(_currentClipIndex);
			if (_isPlaying == null)
			{
				throw new Exception("Radio._isPlaying cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isPlaying.Initialize(this);
			__nameNetworkVariable(_isPlaying, "_isPlaying");
			NetworkVariableFields.Add(_isPlaying);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(3920565185u, __rpc_handler_3920565185, "RequestRadioRepairRpc", RpcInvokePermission.Everyone);
			__registerRpc(3050757064u, __rpc_handler_3050757064, "RequestRadioBreakRpc", RpcInvokePermission.Everyone);
			__registerRpc(2827431988u, __rpc_handler_2827431988, "ClientPlayRadioFeedbackRpc", RpcInvokePermission.Everyone);
			__registerRpc(2581218689u, __rpc_handler_2581218689, "RequestToggleRpc", RpcInvokePermission.Everyone);
			__registerRpc(917932680u, __rpc_handler_917932680, "RequestNextTrackRpc", RpcInvokePermission.Everyone);
			__registerRpc(1807864887u, __rpc_handler_1807864887, "ClientPlayClipRpc", RpcInvokePermission.Everyone);
			__registerRpc(363864640u, __rpc_handler_363864640, "ClientStopRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_3920565185(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((Radio)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Radio)target).RequestRadioRepairRpc();
				((Radio)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3050757064(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((Radio)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Radio)target).RequestRadioBreakRpc();
				((Radio)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2827431988(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out SoundType value, default(FastBufferWriter.ForEnums));
				((Radio)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Radio)target).ClientPlayRadioFeedbackRpc(value);
				((Radio)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2581218689(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((Radio)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Radio)target).RequestToggleRpc();
				((Radio)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_917932680(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((Radio)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Radio)target).RequestNextTrackRpc();
				((Radio)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1807864887(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				((Radio)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Radio)target).ClientPlayClipRpc(value);
				((Radio)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_363864640(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((Radio)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Radio)target).ClientStopRpc();
				((Radio)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "Radio";
		}
	}
}
