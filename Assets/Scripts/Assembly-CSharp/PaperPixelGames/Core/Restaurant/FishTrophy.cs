using System;
using System.Collections;
using System.Collections.Generic;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Core.Struct;
using PaperPixelGames.Core.Tablet;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace PaperPixelGames.Core.Restaurant
{
	[RequireComponent(typeof(NetworkObject))]
	[RequireComponent(typeof(AudioSource))]
	[RequireComponent(typeof(FishTrophyAudioRecorder))]
	public sealed class FishTrophy : BaseInteractable, ISaveable<FishTrophyData>
	{
		private sealed class PendingUpload
		{
			public int UploadId;

			public ulong ClientId;

			public byte[] Bytes;

			public int WrittenBytes;

			public int SampleRate;

			public uint Checksum;

			public FishBookChampion Champion;

			public double ExpiresAt;
		}

		private const int AudioFormatVersion = 1;

		private const int AudioSampleRate = 16000;

		private const int AudioBytesPerSample = 2;

		private const int AudioChunkSize = 4096;

		private const int FirstApproachPlaybackVersion = 1;

		private const float AudioChunkSendInterval = 0.025f;

		private const int MaximumAudioBytes = 160000;

		private const double RecordingReservationGraceSeconds = 3.0;

		private const double UploadTimeoutSeconds = 20.0;

		private const double PlaybackLeadSeconds = 0.15;

		private const ulong NoClient = ulong.MaxValue;

		[Header("Fish Trophy")]
		[SerializeField]
		private ItemDatabaseSO _itemDatabase;

		[SerializeField]
		private Transform _fishVisualSocket;

		[SerializeField]
		private GameObject _emptyVisual;

		[SerializeField]
		private TextMeshProUGUI _fishNameText;

		[SerializeField]
		private TextMeshProUGUI _weightText;

		[SerializeField]
		private TextMeshProUGUI _recordHolderText;

		[SerializeField]
		private GameObject _trophyInfoRoot;

		[SerializeField]
		private TextMeshPro _trophyInfoText;

		[SerializeField]
		private FishSO _editorPreviewFish;

		[Header("Audio")]
		[SerializeField]
		private FishTrophyAudioRecorder _recorder;

		[SerializeField]
		private AudioSource _audioSource;

		[SerializeField]
		private AudioClip _firstApproachClip;

		[SerializeField]
		[Min(0.5f)]
		private float _firstApproachRange = 5f;

		[SerializeField]
		[Min(0.05f)]
		private float _firstApproachProbeInterval = 0.25f;

		[SerializeField]
		[Range(1f, 5f)]
		private float _maximumRecordingSeconds = 5f;

		[SerializeField]
		[Min(0.5f)]
		private float _serverInteractionRange = 4f;

		[SerializeField]
		[Range(0f, 1f)]
		private float _minimumPlaybackAmplitude = 0.005f;

		[SerializeField]
		[Min(0.001f)]
		private float _maximumPlaybackAmplitude = 0.2f;

		[SerializeField]
		private UnityEvent<float> _playbackAmplitudeChanged;

		private readonly NetworkVariable<int> _audioRevision = new NetworkVariable<int>(0);

		private readonly NetworkVariable<int> _audioLength = new NetworkVariable<int>(0);

		private readonly NetworkVariable<int> _audioSampleRate = new NetworkVariable<int>(16000);

		private Renderer[] _staticOutlineRenderers = Array.Empty<Renderer>();

		private readonly float[] _amplitudeSamples = new float[128];

		private TabletNetworkHandler _tabletNetworkHandler;

		private FishBookChampion _currentChampion;

		private FishBookChampion _recordingChampion;

		private FixedString64Bytes _debugFishId;

		private GameObject _spawnedFishVisual;

		private FishTrophyGraphic _spawnedTrophyGraphic;

		private byte[] _serverAudioBytes = Array.Empty<byte>();

		private string _serverRecordFishId = string.Empty;

		private float _serverRecordWeightKg;

		private ulong _serverRecordSteamId;

		private ulong _serverRecordingClientId = ulong.MaxValue;

		private double _serverRecordingReservationExpiresAt;

		private double _serverPlaybackEndsAt;

		private int _nextUploadId = 1;

		private PendingUpload _pendingUpload;

		private FishTrophyData _pendingLoadedData;

		private byte[] _pendingLocalUploadBytes = Array.Empty<byte>();

		private uint _pendingLocalUploadChecksum;

		private int _pendingLocalUploadSampleRate;

		private FishBookChampion _pendingLocalUploadChampion;

		private byte[] _downloadBuffer;

		private int _downloadRevision;

		private int _downloadReceivedBytes;

		private AudioClip _localAudioClip;

		private int _localAudioRevision;

		private int _scheduledPlaybackRevision;

		private double _scheduledPlaybackAt = -1.0;

		private double _scheduledFirstApproachPlaybackAt = -1.0;

		private bool _firstApproachPlaybackCompleted;

		private bool _firstApproachPlaybackStateLoaded;

		private bool _firstApproachPlaybackArmed;

		private bool _firstApproachProximityInitialized;

		private bool _firstApproachPlayerWasNearby;

		private bool _firstApproachPlaybackPending;

		private bool _missingFirstApproachClipLogged;

		private double _nextFirstApproachProbeAt;

		private bool _audioRequestPending;

		private Coroutine _localUploadCoroutine;

		private readonly Dictionary<ulong, Coroutine> _serverDownloadCoroutines = new Dictionary<ulong, Coroutine>();

		public float CurrentPlaybackAmplitude { get; private set; }

		public bool IsRecordingLocally
		{
			get
			{
				if (_recorder != null)
				{
					return _recorder.IsRecording;
				}
				return false;
			}
		}

		public bool HasRecording => _audioLength.Value > 0;

		private bool IsDebugFishOverrideActive => !_debugFishId.IsEmpty;

		protected override void Awake()
		{
			base.Awake();
			if (_fishVisualSocket == null)
			{
				_fishVisualSocket = base.transform;
			}
			if (_audioSource == null)
			{
				TryGetComponent<AudioSource>(out _audioSource);
			}
			if (_recorder == null)
			{
				TryGetComponent<FishTrophyAudioRecorder>(out _recorder);
			}
			SetOptionalObjectActive(_trophyInfoRoot, active: false);
			_staticOutlineRenderers = GetComponentsInChildren<Renderer>(includeInactive: true);
			SetPlaybackAmplitude(0f);
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			NetworkVariable<int> audioRevision = _audioRevision;
			audioRevision.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Combine(audioRevision.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnAudioRevisionChanged));
			NetworkVariable<int> audioLength = _audioLength;
			audioLength.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Combine(audioLength.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnAudioLengthChanged));
			LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
			LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
			TryBindFishBook();
			RefreshChampion();
			RefreshAudioFromNetworkState();
		}

		public override void OnNetworkDespawn()
		{
			LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
			UnbindFishBook();
			NetworkVariable<int> audioRevision = _audioRevision;
			audioRevision.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Remove(audioRevision.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnAudioRevisionChanged));
			NetworkVariable<int> audioLength = _audioLength;
			audioLength.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Remove(audioLength.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnAudioLengthChanged));
			_debugFishId = default;
			_recorder?.Cancel();
			StopPlaybackLocal();
			ClearLocalAudioClip();
			StopAudioTransferCoroutines();
			ClearPendingLocalUpload();
			_downloadBuffer = null;
			_downloadReceivedBytes = 0;
			_audioRequestPending = false;
			_pendingUpload = null;
			_serverRecordingClientId = ulong.MaxValue;
			SetOptionalObjectActive(_trophyInfoRoot, active: false);
			base.OnNetworkDespawn();
		}

		public override void OnDestroy()
		{
			LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
			UnbindFishBook();
			StopAudioTransferCoroutines();
			ClearPendingLocalUpload();
			ClearLocalAudioClip();
			base.OnDestroy();
		}

		private void Update()
		{
			if (_tabletNetworkHandler == null)
			{
				TryBindFishBook();
				RefreshChampion();
			}
			if (IsRecordingLocally && _recorder.ElapsedSeconds >= _maximumRecordingSeconds)
			{
				FinishLocalRecordingAndUpload();
			}
			if (base.IsServer)
			{
				ServerTickTransientState();
			}
			TryStartScheduledFirstApproachPlayback();
			TryStartScheduledPlayback();
			UpdatePlaybackAmplitude();
		}

		public override void EnableGazeHints()
		{
			SetOptionalObjectActive(_trophyInfoRoot, _currentChampion.IsValid);
			if (_currentChampion.IsValid && !(Singleton<HintsManager>.Instance == null))
			{
				if (!IsDebugFishOverrideActive)
				{
					Singleton<HintsManager>.Instance.AddHint("Interact", IsRecordingLocally ? "key_finish_recording" : "key_record");
				}
				if (HasRecording)
				{
					Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_play_recording");
				}
			}
		}

		public override void DisableGazeHints()
		{
			SetOptionalObjectActive(_trophyInfoRoot, active: false);
			if (!(Singleton<HintsManager>.Instance == null))
			{
				Singleton<HintsManager>.Instance.RemoveHint("Interact");
				Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
			}
		}

		protected override bool CanTrigger(Interactor interactor)
		{
			if (base.CanTrigger(interactor) && _currentChampion.IsValid)
			{
				return !IsDebugFishOverrideActive;
			}
			return false;
		}

		protected override void ClientInteract(Interactor interactor, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started)
			{
				if (IsRecordingLocally)
				{
					FinishLocalRecordingAndUpload();
					return;
				}
				if (_recorder == null)
				{
					UtilityMethods.Log("[FishTrophy] Recorder reference is missing.", LogType.Warning);
					return;
				}
				_recordingChampion = _currentChampion;
				ServerRequestRecordingRpc(_recordingChampion.FishId, _recordingChampion.BestWeightKg, _recordingChampion.SteamId);
			}
		}

		protected override void ClientMouse1(Interactor interactor, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started && HasRecording)
			{
				ServerRequestPlaybackRpc();
			}
		}

		private void TryBindFishBook()
		{
			TabletNetworkHandler instance = TabletNetworkHandler.Instance;
			if (!(instance == null) && !(instance == _tabletNetworkHandler))
			{
				UnbindFishBook();
				_tabletNetworkHandler = instance;
				_tabletNetworkHandler.FishBookChanged += OnFishBookChanged;
			}
		}

		private void UnbindFishBook()
		{
			if (!(_tabletNetworkHandler == null))
			{
				_tabletNetworkHandler.FishBookChanged -= OnFishBookChanged;
				_tabletNetworkHandler = null;
			}
		}

		private void OnFishBookChanged()
		{
			RefreshChampion();
		}

		private void RefreshChampion()
		{
			bool flag = TryBuildDebugChampion(out var champion);
			if (!flag)
			{
				_tabletNetworkHandler?.TryGetHeaviestFishBookCatch(out champion, IsTrophyCatchEligible);
			}
			if (_currentChampion.Equals(champion))
			{
				if (base.IsServer && !flag)
				{
					TryApplyLoadedAudio();
				}
				return;
			}
			_currentChampion = champion;
			RebuildFishVisual();
			if (base.IsServer && !flag)
			{
				ServerCancelRecordingState();
				if (!ServerAudioMatchesChampion())
				{
					ServerClearRecording();
				}
				TryApplyLoadedAudio();
			}
			RefreshFirstApproachPlaybackArm();
			RefreshGazeFeedback();
		}

		private bool TryBuildDebugChampion(out FishBookChampion champion)
		{
			champion = default;
			if (!IsDebugFishOverrideActive)
			{
				return false;
			}
			FishSO fishSO = ((_itemDatabase != null) ? (_itemDatabase.FindById(_debugFishId) as FishSO) : null);
			if (fishSO == null || !fishSO.CanDisplayAsTrophy || fishSO.TrophyGraphicPrefab == null)
			{
				return false;
			}
			champion = new FishBookChampion(fishSO.Id, Mathf.Max(0.01f, fishSO.MaxWeight), 0uL, new FixedString64Bytes("Trophy Test"));
			return true;
		}

		private bool IsTrophyCatchEligible(FixedString64Bytes fishId)
		{
			FishSO fishSO = ((_itemDatabase != null) ? (_itemDatabase.FindById(fishId) as FishSO) : null);
			if (fishSO != null && fishSO.CanDisplayAsTrophy)
			{
				return fishSO.TrophyGraphicPrefab != null;
			}
			return false;
		}

		public bool ClientTrySetDebugFish(FishSO fish, int creatureIndex, out string message)
		{
			if (!base.IsSpawned)
			{
				message = "Fish trophy is not spawned.";
				return false;
			}
			if (fish == null || string.IsNullOrWhiteSpace(fish.Id))
			{
				message = $"CreatureItems[{creatureIndex}] is not a valid FishSO.";
				return false;
			}
			FishSO fishSO = ((_itemDatabase != null) ? (_itemDatabase.FindById(fish.Id) as FishSO) : null);
			if (fishSO == null)
			{
				message = "Fish trophy database does not contain '" + fish.Id + "'.";
				return false;
			}
			if (!fishSO.CanDisplayAsTrophy || fishSO.TrophyGraphicPrefab == null)
			{
				message = $"CreatureItems[{creatureIndex}] {fishSO.Name} cannot be displayed as a trophy.";
				return false;
			}
			ServerSetDebugFishRpc(fishSO.Id);
			message = $"Trophy preview requested: [{creatureIndex}] {fishSO.Name} ({fishSO.Id}).";
			return true;
		}

		[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
		private void ServerSetDebugFishRpc(FixedString64Bytes fishId, RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcParams rpcParams2 = rpcParams;
				RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
				{
					InvokePermission = RpcInvokePermission.Everyone
				};
				FastBufferWriter bufferWriter = __beginSendRpc(1222678u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in fishId, default(FastBufferWriter.ForFixedStrings));
				__endSendRpc(ref bufferWriter, 1222678u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!IsTrophyCatchEligible(fishId))
				{
					UtilityMethods.Log($"[FishTrophy][Debug] Request rejected. Fish cannot be displayed. FishId={fishId} " + $"ClientId={rpcParams.Receive.SenderClientId}", LogType.Warning);
					return;
				}
				ClientApplyDebugFishRpc(fishId);
				UtilityMethods.Log($"[FishTrophy][Debug] Preview applied. FishId={fishId} " + $"ClientId={rpcParams.Receive.SenderClientId}");
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientApplyDebugFishRpc(FixedString64Bytes fishId)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3548654991u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in fishId, default(FastBufferWriter.ForFixedStrings));
				__endSendRpc(ref bufferWriter, 3548654991u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				_debugFishId = fishId;
				_recorder?.Cancel();
				StopPlaybackLocal();
				if (base.IsServer)
				{
					ServerCancelRecordingState();
				}
				RefreshChampion();
			}
		}

		private void RebuildFishVisual()
		{
			if (_spawnedFishVisual != null)
			{
				UnityEngine.Object.Destroy(_spawnedFishVisual);
			}
			_spawnedFishVisual = null;
			_spawnedTrophyGraphic = null;
			SetOptionalObjectActive(_emptyVisual, !_currentChampion.IsValid);
			if (!_currentChampion.IsValid)
			{
				SetText(_fishNameText, string.Empty);
				SetText(_weightText, string.Empty);
				SetText(_recordHolderText, string.Empty);
				SetText(_trophyInfoText, string.Empty);
				RefreshOutlineTargets();
				return;
			}
			FishSO fishSO = ((_itemDatabase != null) ? (_itemDatabase.FindById(_currentChampion.FishId) as FishSO) : null);
			if (fishSO == null || !fishSO.CanDisplayAsTrophy || fishSO.TrophyGraphicPrefab == null)
			{
				UtilityMethods.Log($"[FishTrophy] Champion visual could not be resolved. FishId={_currentChampion.FishId}", LogType.Warning);
				RefreshOutlineTargets();
				return;
			}
			Transform parent = ((_fishVisualSocket != null) ? _fishVisualSocket : base.transform);
			_spawnedFishVisual = UnityEngine.Object.Instantiate(fishSO.TrophyGraphicPrefab, parent);
			_spawnedFishVisual.name = fishSO.TrophyGraphicPrefab.name;
			_spawnedFishVisual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			ApplyTrophyOffset(fishSO.TrophyOffset);
			BindSpawnedTrophyGraphic();
			SetText(_weightText, $"{_currentChampion.BestWeightKg:F2} kg");
			SetText(_recordHolderText, _currentChampion.PlayerName.ToString());
			RefreshLocalizedTrophyText();
			RefreshOutlineTargets();
		}

		private void OnSelectedLocaleChanged(Locale locale)
		{
			RefreshLocalizedTrophyText();
		}

		private void RefreshLocalizedTrophyText()
		{
			if (!_currentChampion.IsValid)
			{
				SetText(_fishNameText, string.Empty);
				SetText(_trophyInfoText, string.Empty);
				return;
			}
			FishSO fishSO = ((_itemDatabase != null) ? (_itemDatabase.FindById(_currentChampion.FishId) as FishSO) : null);
			if (!(fishSO == null))
			{
				string formattedString = LocalizationDatabase.GetFormattedString("UiTable", "ui.fish_trophy.caught_by", "Caught by {0}", _currentChampion.PlayerName.ToString());
				SetText(_fishNameText, fishSO.LocalizedName);
				SetText(_trophyInfoText, $"{fishSO.LocalizedName}\n{_currentChampion.BestWeightKg:F2} kg\n{formattedString}");
			}
		}

		private void ApplyTrophyOffset(ItemOffset offset)
		{
			if (!(_spawnedFishVisual == null) && offset != null)
			{
				Transform obj = _spawnedFishVisual.transform;
				obj.localPosition = Vector3.zero;
				obj.localRotation = Quaternion.Euler(offset.RotationOffset);
				obj.localScale = offset.ScaleOffset;
				CenterSpawnedVisual();
				obj.localPosition += offset.PositionOffset;
			}
		}

		private void CenterSpawnedVisual()
		{
			if (!(_spawnedFishVisual == null) && TryGetCombinedBounds(_spawnedFishVisual.GetComponentsInChildren<Renderer>(includeInactive: true), out var bounds))
			{
				Transform obj = _spawnedFishVisual.transform;
				Vector3 vector = ((_fishVisualSocket != null) ? _fishVisualSocket : base.transform).InverseTransformPoint(bounds.center);
				obj.localPosition -= vector;
			}
		}

		private void BindSpawnedTrophyGraphic()
		{
			if (!(_spawnedFishVisual == null))
			{
				_spawnedTrophyGraphic = _spawnedFishVisual.GetComponent<FishTrophyGraphic>();
				if (_spawnedTrophyGraphic == null || _spawnedTrophyGraphic.BendDeformer == null)
				{
					UtilityMethods.Log("[FishTrophy][Graphic] Trophy prefab setup is incomplete. Visual=" + _spawnedFishVisual.name, LogType.Warning);
				}
				else
				{
					SetTrophyBendAngle(CurrentPlaybackAmplitude);
				}
			}
		}

		private static bool TryGetCombinedBounds(Renderer[] renderers, out Bounds bounds)
		{
			bounds = default;
			bool flag = false;
			if (renderers == null)
			{
				return false;
			}
			foreach (Renderer renderer in renderers)
			{
				if (!(renderer == null))
				{
					if (!flag)
					{
						bounds = renderer.bounds;
						flag = true;
					}
					else
					{
						bounds.Encapsulate(renderer.bounds);
					}
				}
			}
			return flag;
		}

		private void RefreshOutlineTargets()
		{
			if (_outlinable == null)
			{
				return;
			}
			while (_outlinable.OutlineTargetsCount > 0)
			{
				_outlinable.RemoveTarget(_outlinable[0]);
			}
			for (int i = 0; i < _staticOutlineRenderers.Length; i++)
			{
				Renderer renderer = _staticOutlineRenderers[i];
				if (!(renderer == null) && (!(_trophyInfoRoot != null) || !renderer.transform.IsChildOf(_trophyInfoRoot.transform)))
				{
					_outlinable.AddRenderer(renderer);
				}
			}
			if (_spawnedFishVisual != null)
			{
				Renderer[] componentsInChildren = _spawnedFishVisual.GetComponentsInChildren<Renderer>(includeInactive: true);
				for (int j = 0; j < componentsInChildren.Length; j++)
				{
					if (componentsInChildren[j] != null)
					{
						_outlinable.AddRenderer(componentsInChildren[j]);
					}
				}
			}
			_outlinable.enabled = false;
		}

		[Rpc(SendTo.Server)]
		private void ServerRequestRecordingRpc(FixedString64Bytes fishId, float weightKg, ulong steamId, RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcParams rpcParams2 = rpcParams;
				RpcAttribute.RpcAttributeParams attributeParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(1941679392u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in fishId, default(FastBufferWriter.ForFixedStrings));
				bufferWriter.WriteValueSafe(in weightKg, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(bufferWriter, steamId);
				__endSendRpc(ref bufferWriter, 1941679392u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ulong senderClientId = rpcParams.Receive.SenderClientId;
				if (!ServerCanPlayerUseTrophy(senderClientId) || !DoesChampionMatch(fishId, weightKg, steamId) || _serverPlaybackEndsAt > GetServerTime() || (_serverRecordingClientId != ulong.MaxValue && _serverRecordingClientId != senderClientId))
				{
					ClientRecordingPermissionRpc(accepted: false, base.RpcTarget.Single(senderClientId, RpcTargetUse.Temp));
					return;
				}
				_serverRecordingClientId = senderClientId;
				_serverRecordingReservationExpiresAt = GetServerTime() + (double)_maximumRecordingSeconds + 3.0;
				ClientRecordingPermissionRpc(accepted: true, base.RpcTarget.Single(senderClientId, RpcTargetUse.Temp));
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientRecordingPermissionRpc(bool accepted, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3607953935u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in accepted, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3607953935u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!accepted)
				{
					UtilityMethods.Log("[FishTrophy] Recording request was rejected by the server.", LogType.Warning);
					return;
				}
				if (_recorder == null || !_recorder.TryStart(_maximumRecordingSeconds))
				{
					ServerCancelRecordingReservationRpc();
					return;
				}
				RefreshGazeFeedback();
				UtilityMethods.Log($"[FishTrophy][Recorder] Recording started. MaxSeconds={_maximumRecordingSeconds:F1}");
			}
		}

		private void FinishLocalRecordingAndUpload()
		{
			if (!(_recorder == null) && _recorder.IsRecording)
			{
				bool num = _recorder.TryFinish(out var pcm16Bytes, out var sampleRate);
				RefreshGazeFeedback();
				if (!num || pcm16Bytes.Length == 0)
				{
					ServerCancelRecordingReservationRpc();
					return;
				}
				_pendingLocalUploadBytes = pcm16Bytes;
				_pendingLocalUploadSampleRate = sampleRate;
				_pendingLocalUploadChecksum = CalculateChecksum(pcm16Bytes);
				_pendingLocalUploadChampion = _recordingChampion;
				ServerBeginAudioUploadRpc(_pendingLocalUploadChampion.FishId, _pendingLocalUploadChampion.BestWeightKg, _pendingLocalUploadChampion.SteamId, sampleRate, pcm16Bytes.Length, _pendingLocalUploadChecksum);
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerCancelRecordingReservationRpc(RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcParams rpcParams2 = rpcParams;
				RpcAttribute.RpcAttributeParams attributeParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(686419372u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 686419372u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (_serverRecordingClientId == rpcParams.Receive.SenderClientId)
				{
					ServerCancelRecordingState();
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerBeginAudioUploadRpc(FixedString64Bytes fishId, float weightKg, ulong steamId, int sampleRate, int byteLength, uint checksum, RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcParams rpcParams2 = rpcParams;
				RpcAttribute.RpcAttributeParams attributeParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(829686541u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in fishId, default(FastBufferWriter.ForFixedStrings));
				bufferWriter.WriteValueSafe(in weightKg, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(bufferWriter, steamId);
				BytePacker.WriteValueBitPacked(bufferWriter, sampleRate);
				BytePacker.WriteValueBitPacked(bufferWriter, byteLength);
				BytePacker.WriteValueBitPacked(bufferWriter, checksum);
				__endSendRpc(ref bufferWriter, 829686541u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ulong senderClientId = rpcParams.Receive.SenderClientId;
				if (_serverRecordingClientId != senderClientId || !(GetServerTime() <= _serverRecordingReservationExpiresAt) || !ServerCanPlayerUseTrophy(senderClientId) || !DoesChampionMatch(fishId, weightKg, steamId) || sampleRate != 16000 || byteLength <= 0 || byteLength > 160000 || byteLength % 2 != 0)
				{
					ServerCancelRecordingState();
					ClientAudioUploadAcceptedRpc(0, base.RpcTarget.Single(senderClientId, RpcTargetUse.Temp));
					return;
				}
				int uploadId = _nextUploadId++;
				_pendingUpload = new PendingUpload
				{
					UploadId = uploadId,
					ClientId = senderClientId,
					Bytes = new byte[byteLength],
					WrittenBytes = 0,
					SampleRate = sampleRate,
					Checksum = checksum,
					Champion = _currentChampion,
					ExpiresAt = GetServerTime() + 20.0
				};
				ClientAudioUploadAcceptedRpc(uploadId, base.RpcTarget.Single(senderClientId, RpcTargetUse.Temp));
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientAudioUploadAcceptedRpc(int uploadId, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2834046345u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, uploadId);
				__endSendRpc(ref bufferWriter, 2834046345u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (uploadId <= 0 || _pendingLocalUploadBytes == null || _pendingLocalUploadBytes.Length == 0)
			{
				ClearPendingLocalUpload();
				UtilityMethods.Log("[FishTrophy] Audio upload was rejected by the server.", LogType.Warning);
				return;
			}
			if (_localUploadCoroutine != null)
			{
				StopCoroutine(_localUploadCoroutine);
			}
			byte[] pendingLocalUploadBytes = _pendingLocalUploadBytes;
			ClearPendingLocalUpload();
			_localUploadCoroutine = StartCoroutine(CoUploadAudioChunks(uploadId, pendingLocalUploadBytes));
		}

		private IEnumerator CoUploadAudioChunks(int uploadId, byte[] uploadBytes)
		{
			for (int offset = 0; offset < uploadBytes.Length; offset += 4096)
			{
				if (!base.IsSpawned)
				{
					break;
				}
				if (!base.IsClient)
				{
					break;
				}
				int num = Mathf.Min(4096, uploadBytes.Length - offset);
				byte[] array = new byte[num];
				Buffer.BlockCopy(uploadBytes, offset, array, 0, num);
				ServerUploadAudioChunkRpc(uploadId, offset, array);
				if (offset + num < uploadBytes.Length)
				{
					yield return WaitForNextAudioChunk();
				}
			}
			_localUploadCoroutine = null;
		}

		[Rpc(SendTo.Server)]
		private void ServerUploadAudioChunkRpc(int uploadId, int offset, byte[] chunk, RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcParams rpcParams2 = rpcParams;
				RpcAttribute.RpcAttributeParams attributeParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(2023283061u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, uploadId);
				BytePacker.WriteValueBitPacked(bufferWriter, offset);
				bool value = chunk != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(chunk, default(FastBufferWriter.ForPrimitives));
				}
				__endSendRpc(ref bufferWriter, 2023283061u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			PendingUpload pendingUpload = _pendingUpload;
			if (pendingUpload == null || pendingUpload.UploadId != uploadId || pendingUpload.ClientId != senderClientId || GetServerTime() > pendingUpload.ExpiresAt || offset != pendingUpload.WrittenBytes || chunk == null || chunk.Length == 0 || chunk.Length > 4096 || offset + chunk.Length > pendingUpload.Bytes.Length)
			{
				ServerCancelRecordingState();
				return;
			}
			Buffer.BlockCopy(chunk, 0, pendingUpload.Bytes, offset, chunk.Length);
			pendingUpload.WrittenBytes += chunk.Length;
			if (pendingUpload.WrittenBytes == pendingUpload.Bytes.Length)
			{
				if (CalculateChecksum(pendingUpload.Bytes) != pendingUpload.Checksum || !ChampionMatches(pendingUpload.Champion, _currentChampion))
				{
					ServerCancelRecordingState();
				}
				else
				{
					ServerCommitRecording(pendingUpload);
				}
			}
		}

		private void ServerCommitRecording(PendingUpload upload)
		{
			_serverAudioBytes = upload.Bytes;
			_serverRecordFishId = upload.Champion.FishId.ToString();
			_serverRecordWeightKg = upload.Champion.BestWeightKg;
			_serverRecordSteamId = upload.Champion.SteamId;
			_audioSampleRate.Value = upload.SampleRate;
			_audioLength.Value = upload.Bytes.Length;
			_audioRevision.Value = Mathf.Max(1, _audioRevision.Value + 1);
			UtilityMethods.Log("[FishTrophy][Audio] Recording committed. FishId=" + _serverRecordFishId + " " + $"WeightKg={_serverRecordWeightKg:F2} Bytes={upload.Bytes.Length} Revision={_audioRevision.Value}");
			ServerCancelRecordingState();
		}

		[Rpc(SendTo.Server)]
		private void ServerRequestAudioRpc(int requestedRevision, RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcParams rpcParams2 = rpcParams;
				RpcAttribute.RpcAttributeParams attributeParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(2312129539u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, requestedRevision);
				__endSendRpc(ref bufferWriter, 2312129539u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (_serverAudioBytes != null && _serverAudioBytes.Length != 0 && requestedRevision == _audioRevision.Value)
			{
				if (_serverDownloadCoroutines.Remove(senderClientId, out var value))
				{
					StopCoroutine(value);
				}
				byte[] serverAudioBytes = _serverAudioBytes;
				Coroutine value2 = StartCoroutine(CoSendAudioChunksToClient(senderClientId, requestedRevision, serverAudioBytes));
				_serverDownloadCoroutines[senderClientId] = value2;
			}
		}

		private IEnumerator CoSendAudioChunksToClient(ulong clientId, int revision, byte[] audioBytes)
		{
			for (int offset = 0; offset < audioBytes.Length; offset += 4096)
			{
				if (!base.IsSpawned)
				{
					break;
				}
				if (!base.IsServer)
				{
					break;
				}
				if (_audioRevision.Value != revision)
				{
					break;
				}
				if (base.NetworkManager == null)
				{
					break;
				}
				if (!base.NetworkManager.ConnectedClients.ContainsKey(clientId))
				{
					break;
				}
				int num = Mathf.Min(4096, audioBytes.Length - offset);
				byte[] array = new byte[num];
				Buffer.BlockCopy(audioBytes, offset, array, 0, num);
				ClientReceiveAudioChunkRpc(revision, audioBytes.Length, offset, array, base.RpcTarget.Single(clientId, RpcTargetUse.Temp));
				if (offset + num < audioBytes.Length)
				{
					yield return WaitForNextAudioChunk();
				}
			}
			yield return null;
			_serverDownloadCoroutines.Remove(clientId);
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientReceiveAudioChunkRpc(int revision, int totalLength, int offset, byte[] chunk, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3507723817u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, revision);
				BytePacker.WriteValueBitPacked(bufferWriter, totalLength);
				BytePacker.WriteValueBitPacked(bufferWriter, offset);
				bool value = chunk != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(chunk, default(FastBufferWriter.ForPrimitives));
				}
				__endSendRpc(ref bufferWriter, 3507723817u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (revision != _audioRevision.Value || totalLength != _audioLength.Value || totalLength <= 0 || totalLength > 160000 || chunk == null || chunk.Length == 0 || chunk.Length > 4096 || offset < 0 || offset + chunk.Length > totalLength)
			{
				return;
			}
			if (_downloadBuffer == null || _downloadRevision != revision || _downloadBuffer.Length != totalLength)
			{
				_downloadBuffer = new byte[totalLength];
				_downloadRevision = revision;
				_downloadReceivedBytes = 0;
			}
			if (offset != _downloadReceivedBytes)
			{
				_downloadBuffer = null;
				_downloadReceivedBytes = 0;
				_audioRequestPending = false;
				RequestCurrentAudio();
				return;
			}
			Buffer.BlockCopy(chunk, 0, _downloadBuffer, offset, chunk.Length);
			_downloadReceivedBytes += chunk.Length;
			if (_downloadReceivedBytes == totalLength)
			{
				BuildLocalAudioClip(_downloadBuffer, _audioSampleRate.Value, revision);
				_downloadBuffer = null;
				_downloadReceivedBytes = 0;
				_audioRequestPending = false;
				TryStartScheduledPlayback();
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerRequestPlaybackRpc(RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcParams rpcParams2 = rpcParams;
				RpcAttribute.RpcAttributeParams attributeParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(358099440u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 358099440u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ulong senderClientId = rpcParams.Receive.SenderClientId;
				double serverTime = GetServerTime();
				if (ServerCanPlayerUseTrophy(senderClientId) && _serverAudioBytes != null && _serverAudioBytes.Length != 0 && _serverRecordingClientId == ulong.MaxValue && _pendingUpload == null && !(_serverPlaybackEndsAt > serverTime))
				{
					double num = (double)_serverAudioBytes.Length / (double)(2 * Mathf.Max(1, _audioSampleRate.Value));
					double num2 = serverTime + 0.15;
					_serverPlaybackEndsAt = num2 + num;
					ClientSchedulePlaybackRpc(_audioRevision.Value, num2);
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientSchedulePlaybackRpc(int revision, double serverStartTime)
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
				FastBufferWriter bufferWriter = __beginSendRpc(600904827u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, revision);
				bufferWriter.WriteValueSafe(in serverStartTime, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 600904827u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (revision == _audioRevision.Value)
			{
				_scheduledPlaybackRevision = revision;
				_scheduledPlaybackAt = serverStartTime;
				if (_localAudioRevision != revision || _localAudioClip == null)
				{
					RequestCurrentAudio();
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientScheduleFirstApproachPlaybackRpc(double serverStartTime)
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
				FastBufferWriter bufferWriter = __beginSendRpc(887136595u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in serverStartTime, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 887136595u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!(_firstApproachClip == null))
				{
					_scheduledFirstApproachPlaybackAt = serverStartTime;
				}
			}
		}

		private void TryStartScheduledFirstApproachPlayback()
		{
			if (_scheduledFirstApproachPlaybackAt < 0.0 || _audioSource == null || _firstApproachClip == null)
			{
				return;
			}
			double serverTime = GetServerTime();
			if (!(serverTime < _scheduledFirstApproachPlaybackAt))
			{
				double num = serverTime - _scheduledFirstApproachPlaybackAt;
				_scheduledFirstApproachPlaybackAt = -1.0;
				if (!(num >= (double)_firstApproachClip.length))
				{
					_audioSource.Stop();
					_audioSource.clip = _firstApproachClip;
					_audioSource.time = Mathf.Clamp((float)num, 0f, Mathf.Max(0f, _firstApproachClip.length - 0.001f));
					_audioSource.Play();
				}
			}
		}

		private void TryStartScheduledPlayback()
		{
			if (_scheduledPlaybackAt < 0.0 || _scheduledPlaybackRevision != _audioRevision.Value || _localAudioRevision != _scheduledPlaybackRevision || _localAudioClip == null || _audioSource == null)
			{
				return;
			}
			double serverTime = GetServerTime();
			if (!(serverTime < _scheduledPlaybackAt))
			{
				double num = serverTime - _scheduledPlaybackAt;
				if (num >= (double)_localAudioClip.length)
				{
					_scheduledPlaybackAt = -1.0;
					return;
				}
				_audioSource.Stop();
				_audioSource.clip = _localAudioClip;
				_audioSource.time = Mathf.Clamp((float)num, 0f, Mathf.Max(0f, _localAudioClip.length - 0.001f));
				_audioSource.Play();
				_scheduledPlaybackAt = -1.0;
			}
		}

		private void OnAudioRevisionChanged(int previousValue, int currentValue)
		{
			ClearLocalAudioClip();
			_downloadBuffer = null;
			_downloadReceivedBytes = 0;
			_audioRequestPending = false;
			if (currentValue > 0 && _audioLength.Value > 0)
			{
				RequestCurrentAudio();
			}
			RefreshGazeFeedback();
		}

		private void OnAudioLengthChanged(int previousValue, int currentValue)
		{
			if (currentValue <= 0)
			{
				StopPlaybackLocal();
				ClearLocalAudioClip();
			}
			RefreshGazeFeedback();
		}

		private void RefreshAudioFromNetworkState()
		{
			if (_audioRevision.Value > 0 && _audioLength.Value > 0)
			{
				RequestCurrentAudio();
			}
		}

		private void RequestCurrentAudio()
		{
			if (base.IsSpawned && base.IsClient && !_audioRequestPending && _audioLength.Value > 0)
			{
				_audioRequestPending = true;
				ServerRequestAudioRpc(_audioRevision.Value);
			}
		}

		private void BuildLocalAudioClip(byte[] bytes, int sampleRate, int revision)
		{
			if (bytes != null && bytes.Length != 0 && bytes.Length % 2 == 0 && sampleRate > 0)
			{
				int num = bytes.Length / 2;
				float[] array = new float[num];
				for (int i = 0; i < num; i++)
				{
					int num2 = i * 2;
					short num3 = (short)(bytes[num2] | (bytes[num2 + 1] << 8));
					array[i] = (float)num3 / 32768f;
				}
				ClearLocalAudioClip();
				_localAudioClip = AudioClip.Create($"FishTrophyRecording_{revision}", num, 1, sampleRate, stream: false);
				_localAudioClip.SetData(array, 0);
				_localAudioRevision = revision;
			}
		}

		private void ClearLocalAudioClip()
		{
			if (_audioSource != null && _audioSource.clip == _localAudioClip)
			{
				_audioSource.Stop();
				_audioSource.clip = null;
			}
			if (_localAudioClip != null)
			{
				UnityEngine.Object.Destroy(_localAudioClip);
			}
			_localAudioClip = null;
			_localAudioRevision = 0;
			SetPlaybackAmplitude(0f);
		}

		private void StopPlaybackLocal()
		{
			_scheduledPlaybackAt = -1.0;
			_scheduledFirstApproachPlaybackAt = -1.0;
			if (_audioSource != null)
			{
				_audioSource.Stop();
			}
			SetPlaybackAmplitude(0f);
		}

		private void UpdatePlaybackAmplitude()
		{
			if (_audioSource == null || !_audioSource.isPlaying)
			{
				SetPlaybackAmplitude(0f);
				return;
			}
			_audioSource.GetOutputData(_amplitudeSamples, 0);
			double num = 0.0;
			for (int i = 0; i < _amplitudeSamples.Length; i++)
			{
				num += (double)(_amplitudeSamples[i] * _amplitudeSamples[i]);
			}
			float value = (float)Math.Sqrt(num / (double)_amplitudeSamples.Length);
			float playbackAmplitude = Mathf.InverseLerp(_minimumPlaybackAmplitude, Mathf.Max(_minimumPlaybackAmplitude + 0.001f, _maximumPlaybackAmplitude), value);
			SetPlaybackAmplitude(playbackAmplitude);
		}

		private void SetPlaybackAmplitude(float value)
		{
			value = Mathf.Clamp01(value);
			SetTrophyBendAngle(value);
			if (!Mathf.Approximately(CurrentPlaybackAmplitude, value))
			{
				CurrentPlaybackAmplitude = value;
				_playbackAmplitudeChanged?.Invoke(value);
			}
		}

		private void SetTrophyBendAngle(float normalizedAmplitude)
		{
			if (!(_spawnedTrophyGraphic == null))
			{
				bool hasAudiblePlayback = _audioSource != null && _audioSource.isPlaying && normalizedAmplitude > 0f;
				_spawnedTrophyGraphic.SetPlaybackAmplitude(normalizedAmplitude, hasAudiblePlayback);
			}
		}

		private void ServerTickTransientState()
		{
			double serverTime = GetServerTime();
			if (_serverRecordingClientId != ulong.MaxValue && serverTime > _serverRecordingReservationExpiresAt)
			{
				ServerCancelRecordingState();
			}
			if (_pendingUpload != null && serverTime > _pendingUpload.ExpiresAt)
			{
				ServerCancelRecordingState();
			}
			ServerTryPlayFirstApproachSfx(serverTime);
		}

		private void RefreshFirstApproachPlaybackArm()
		{
			if (base.IsServer && _firstApproachPlaybackStateLoaded)
			{
				_firstApproachPlaybackArmed = !_firstApproachPlaybackCompleted && _currentChampion.IsValid && !IsDebugFishOverrideActive;
				if (_firstApproachPlaybackArmed)
				{
					_nextFirstApproachProbeAt = GetServerTime();
					_firstApproachProximityInitialized = false;
					_firstApproachPlayerWasNearby = false;
					_firstApproachPlaybackPending = false;
				}
			}
		}

		private void ServerTryPlayFirstApproachSfx(double now)
		{
			if (!_firstApproachPlaybackArmed || !_firstApproachPlaybackStateLoaded || _firstApproachPlaybackCompleted || !_currentChampion.IsValid || IsDebugFishOverrideActive || now < _nextFirstApproachProbeAt)
			{
				return;
			}
			_nextFirstApproachProbeAt = now + (double)Mathf.Max(0.05f, _firstApproachProbeInterval);
			bool flag = IsAnyPlayerWithinFirstApproachRange();
			if (!_firstApproachProximityInitialized)
			{
				_firstApproachProximityInitialized = true;
				_firstApproachPlayerWasNearby = flag;
				return;
			}
			if (flag && !_firstApproachPlayerWasNearby)
			{
				_firstApproachPlaybackPending = true;
			}
			_firstApproachPlayerWasNearby = flag;
			if (!_firstApproachPlaybackPending || _serverRecordingClientId != ulong.MaxValue || _pendingUpload != null || _serverPlaybackEndsAt > now)
			{
				return;
			}
			if (_firstApproachClip == null)
			{
				if (!_missingFirstApproachClipLogged)
				{
					_missingFirstApproachClipLogged = true;
					Debug.LogWarning("[FishTrophy] First approach clip is missing.", this);
				}
			}
			else
			{
				_firstApproachPlaybackCompleted = true;
				_firstApproachPlaybackArmed = false;
				_firstApproachPlaybackPending = false;
				double num = now + 0.15;
				_serverPlaybackEndsAt = num + (double)_firstApproachClip.length;
				ClientScheduleFirstApproachPlaybackRpc(num);
			}
		}

		private bool IsAnyPlayerWithinFirstApproachRange()
		{
			if (Singleton<PlayersManager>.Instance == null)
			{
				return false;
			}
			float num = _firstApproachRange * _firstApproachRange;
			foreach (KeyValuePair<ulong, PPG_Player> player in Singleton<PlayersManager>.Instance.Players)
			{
				PPG_Player value = player.Value;
				if (value != null && value.IsSpawned && Vector3.SqrMagnitude(value.transform.position - base.transform.position) <= num)
				{
					return true;
				}
			}
			return false;
		}

		private bool ServerCanPlayerUseTrophy(ulong clientId)
		{
			if (Singleton<PlayersManager>.Instance != null && Singleton<PlayersManager>.Instance.TryGetPlayer(clientId, out var player) && player != null && player.IsSpawned)
			{
				return Vector3.SqrMagnitude(player.transform.position - base.transform.position) <= _serverInteractionRange * _serverInteractionRange;
			}
			return false;
		}

		private bool DoesChampionMatch(FixedString64Bytes fishId, float weightKg, ulong steamId)
		{
			if (_currentChampion.IsValid && _currentChampion.FishId.Equals(fishId) && Mathf.Approximately(_currentChampion.BestWeightKg, weightKg))
			{
				return _currentChampion.SteamId == steamId;
			}
			return false;
		}

		private bool ServerAudioMatchesChampion()
		{
			if (_serverAudioBytes == null || _serverAudioBytes.Length == 0)
			{
				return true;
			}
			if (_currentChampion.IsValid && string.Equals(_serverRecordFishId, _currentChampion.FishId.ToString(), StringComparison.Ordinal) && Mathf.Approximately(_serverRecordWeightKg, _currentChampion.BestWeightKg))
			{
				return _serverRecordSteamId == _currentChampion.SteamId;
			}
			return false;
		}

		private static bool ChampionMatches(FishBookChampion first, FishBookChampion second)
		{
			if (first.IsValid && second.IsValid && first.FishId.Equals(second.FishId) && Mathf.Approximately(first.BestWeightKg, second.BestWeightKg))
			{
				return first.SteamId == second.SteamId;
			}
			return false;
		}

		private void ServerCancelRecordingState()
		{
			_pendingUpload = null;
			_serverRecordingClientId = ulong.MaxValue;
			_serverRecordingReservationExpiresAt = 0.0;
		}

		private void ServerClearRecording()
		{
			if (base.IsServer)
			{
				bool num = _serverAudioBytes != null && _serverAudioBytes.Length != 0;
				_serverAudioBytes = Array.Empty<byte>();
				_serverRecordFishId = string.Empty;
				_serverRecordWeightKg = 0f;
				_serverRecordSteamId = 0uL;
				_audioLength.Value = 0;
				_audioSampleRate.Value = 16000;
				if (num)
				{
					_audioRevision.Value = Mathf.Max(1, _audioRevision.Value + 1);
				}
			}
		}

		private void ClearPendingLocalUpload()
		{
			_pendingLocalUploadBytes = Array.Empty<byte>();
			_pendingLocalUploadChecksum = 0u;
			_pendingLocalUploadSampleRate = 0;
			_pendingLocalUploadChampion = default;
		}

		private void StopAudioTransferCoroutines()
		{
			if (_localUploadCoroutine != null)
			{
				StopCoroutine(_localUploadCoroutine);
				_localUploadCoroutine = null;
			}
			foreach (Coroutine value in _serverDownloadCoroutines.Values)
			{
				if (value != null)
				{
					StopCoroutine(value);
				}
			}
			_serverDownloadCoroutines.Clear();
		}

		private static IEnumerator WaitForNextAudioChunk()
		{
			float resumeAt = Time.unscaledTime + 0.025f;
			while (Time.unscaledTime < resumeAt)
			{
				yield return null;
			}
		}

		private double GetServerTime()
		{
			if (!(base.NetworkManager != null))
			{
				return Time.unscaledTimeAsDouble;
			}
			return base.NetworkManager.ServerTime.Time;
		}

		private static uint CalculateChecksum(byte[] bytes)
		{
			uint num = 2166136261u;
			if (bytes == null)
			{
				return num;
			}
			for (int i = 0; i < bytes.Length; i++)
			{
				num ^= bytes[i];
				num *= 16777619;
			}
			return num;
		}

		public FishTrophyData Save()
		{
			byte[] audioBytes = ((_serverAudioBytes != null && _serverAudioBytes.Length != 0) ? ((byte[])_serverAudioBytes.Clone()) : Array.Empty<byte>());
			return new FishTrophyData
			{
				RecordFishId = (_serverRecordFishId ?? string.Empty),
				RecordWeightKg = _serverRecordWeightKg,
				RecordSteamId = _serverRecordSteamId,
				AudioFormatVersion = 1,
				SampleRate = _audioSampleRate.Value,
				AudioRevision = _audioRevision.Value,
				AudioBytes = audioBytes,
				FirstApproachPlaybackVersion = 1,
				FirstApproachPlaybackCompleted = _firstApproachPlaybackCompleted
			};
		}

		public void Load(FishTrophyData data)
		{
			if (base.IsServer)
			{
				_pendingLoadedData = data ?? new FishTrophyData();
				_firstApproachPlaybackCompleted = _pendingLoadedData.FirstApproachPlaybackVersion == 1 && _pendingLoadedData.FirstApproachPlaybackCompleted;
				_firstApproachPlaybackStateLoaded = true;
				RefreshFirstApproachPlaybackArm();
				TryApplyLoadedAudio();
			}
		}

		private void TryApplyLoadedAudio()
		{
			if (base.IsServer && _pendingLoadedData != null && _currentChampion.IsValid)
			{
				FishTrophyData pendingLoadedData = _pendingLoadedData;
				_pendingLoadedData = null;
				if (pendingLoadedData.AudioFormatVersion != 1 || pendingLoadedData.SampleRate != 16000 || pendingLoadedData.AudioBytes == null || pendingLoadedData.AudioBytes.Length == 0 || pendingLoadedData.AudioBytes.Length > 160000 || pendingLoadedData.AudioBytes.Length % 2 != 0 || !string.Equals(pendingLoadedData.RecordFishId, _currentChampion.FishId.ToString(), StringComparison.Ordinal) || !Mathf.Approximately(pendingLoadedData.RecordWeightKg, _currentChampion.BestWeightKg) || pendingLoadedData.RecordSteamId != _currentChampion.SteamId)
				{
					ServerClearRecording();
					return;
				}
				_serverAudioBytes = (byte[])pendingLoadedData.AudioBytes.Clone();
				_serverRecordFishId = pendingLoadedData.RecordFishId;
				_serverRecordWeightKg = pendingLoadedData.RecordWeightKg;
				_serverRecordSteamId = pendingLoadedData.RecordSteamId;
				_audioSampleRate.Value = pendingLoadedData.SampleRate;
				_audioLength.Value = pendingLoadedData.AudioBytes.Length;
				_audioRevision.Value = Mathf.Max(1, pendingLoadedData.AudioRevision);
				UtilityMethods.Log("[FishTrophy][Save] Recording loaded. FishId=" + _serverRecordFishId + " " + $"Bytes={_serverAudioBytes.Length} Revision={_audioRevision.Value}");
			}
		}

		private static void SetOptionalObjectActive(GameObject target, bool active)
		{
			if (target != null && target.activeSelf != active)
			{
				target.SetActive(active);
			}
		}

		private static void SetText(TMP_Text target, string value)
		{
			if (target != null)
			{
				target.text = value ?? string.Empty;
			}
		}

		protected override void __initializeVariables()
		{
			if (_audioRevision == null)
			{
				throw new Exception("FishTrophy._audioRevision cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_audioRevision.Initialize(this);
			__nameNetworkVariable(_audioRevision, "_audioRevision");
			NetworkVariableFields.Add(_audioRevision);
			if (_audioLength == null)
			{
				throw new Exception("FishTrophy._audioLength cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_audioLength.Initialize(this);
			__nameNetworkVariable(_audioLength, "_audioLength");
			NetworkVariableFields.Add(_audioLength);
			if (_audioSampleRate == null)
			{
				throw new Exception("FishTrophy._audioSampleRate cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_audioSampleRate.Initialize(this);
			__nameNetworkVariable(_audioSampleRate, "_audioSampleRate");
			NetworkVariableFields.Add(_audioSampleRate);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(1222678u, __rpc_handler_1222678, "ServerSetDebugFishRpc", RpcInvokePermission.Everyone);
			__registerRpc(3548654991u, __rpc_handler_3548654991, "ClientApplyDebugFishRpc", RpcInvokePermission.Everyone);
			__registerRpc(1941679392u, __rpc_handler_1941679392, "ServerRequestRecordingRpc", RpcInvokePermission.Everyone);
			__registerRpc(3607953935u, __rpc_handler_3607953935, "ClientRecordingPermissionRpc", RpcInvokePermission.Everyone);
			__registerRpc(686419372u, __rpc_handler_686419372, "ServerCancelRecordingReservationRpc", RpcInvokePermission.Everyone);
			__registerRpc(829686541u, __rpc_handler_829686541, "ServerBeginAudioUploadRpc", RpcInvokePermission.Everyone);
			__registerRpc(2834046345u, __rpc_handler_2834046345, "ClientAudioUploadAcceptedRpc", RpcInvokePermission.Everyone);
			__registerRpc(2023283061u, __rpc_handler_2023283061, "ServerUploadAudioChunkRpc", RpcInvokePermission.Everyone);
			__registerRpc(2312129539u, __rpc_handler_2312129539, "ServerRequestAudioRpc", RpcInvokePermission.Everyone);
			__registerRpc(3507723817u, __rpc_handler_3507723817, "ClientReceiveAudioChunkRpc", RpcInvokePermission.Everyone);
			__registerRpc(358099440u, __rpc_handler_358099440, "ServerRequestPlaybackRpc", RpcInvokePermission.Everyone);
			__registerRpc(600904827u, __rpc_handler_600904827, "ClientSchedulePlaybackRpc", RpcInvokePermission.Everyone);
			__registerRpc(887136595u, __rpc_handler_887136595, "ClientScheduleFirstApproachPlaybackRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_1222678(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				RpcParams ext = rpcParams.Ext;
				((FishTrophy)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FishTrophy)target).ServerSetDebugFishRpc(value, ext);
				((FishTrophy)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3548654991(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				((FishTrophy)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FishTrophy)target).ClientApplyDebugFishRpc(value);
				((FishTrophy)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1941679392(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				reader.ReadValueSafe(out float value2, default(FastBufferWriter.ForPrimitives));
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value3);
				RpcParams ext = rpcParams.Ext;
				((FishTrophy)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FishTrophy)target).ServerRequestRecordingRpc(value, value2, value3, ext);
				((FishTrophy)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3607953935(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((FishTrophy)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FishTrophy)target).ClientRecordingPermissionRpc(value, ext);
				((FishTrophy)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_686419372(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((FishTrophy)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FishTrophy)target).ServerCancelRecordingReservationRpc(ext);
				((FishTrophy)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_829686541(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				reader.ReadValueSafe(out float value2, default(FastBufferWriter.ForPrimitives));
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value3);
				ByteUnpacker.ReadValueBitPacked(reader, out int value4);
				ByteUnpacker.ReadValueBitPacked(reader, out int value5);
				ByteUnpacker.ReadValueBitPacked(reader, out uint value6);
				RpcParams ext = rpcParams.Ext;
				((FishTrophy)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FishTrophy)target).ServerBeginAudioUploadRpc(value, value2, value3, value4, value5, value6, ext);
				((FishTrophy)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2834046345(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				RpcParams ext = rpcParams.Ext;
				((FishTrophy)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FishTrophy)target).ClientAudioUploadAcceptedRpc(value, ext);
				((FishTrophy)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2023283061(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				ByteUnpacker.ReadValueBitPacked(reader, out int value2);
				reader.ReadValueSafe(out bool value3, default(FastBufferWriter.ForPrimitives));
				byte[] value4 = null;
				if (value3)
				{
					reader.ReadValueSafe(out value4, default(FastBufferWriter.ForPrimitives));
				}
				RpcParams ext = rpcParams.Ext;
				((FishTrophy)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FishTrophy)target).ServerUploadAudioChunkRpc(value, value2, value4, ext);
				((FishTrophy)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2312129539(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				RpcParams ext = rpcParams.Ext;
				((FishTrophy)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FishTrophy)target).ServerRequestAudioRpc(value, ext);
				((FishTrophy)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3507723817(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				ByteUnpacker.ReadValueBitPacked(reader, out int value2);
				ByteUnpacker.ReadValueBitPacked(reader, out int value3);
				reader.ReadValueSafe(out bool value4, default(FastBufferWriter.ForPrimitives));
				byte[] value5 = null;
				if (value4)
				{
					reader.ReadValueSafe(out value5, default(FastBufferWriter.ForPrimitives));
				}
				RpcParams ext = rpcParams.Ext;
				((FishTrophy)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FishTrophy)target).ClientReceiveAudioChunkRpc(value, value2, value3, value5, ext);
				((FishTrophy)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_358099440(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((FishTrophy)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FishTrophy)target).ServerRequestPlaybackRpc(ext);
				((FishTrophy)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_600904827(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				reader.ReadValueSafe(out double value2, default(FastBufferWriter.ForPrimitives));
				((FishTrophy)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FishTrophy)target).ClientSchedulePlaybackRpc(value, value2);
				((FishTrophy)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_887136595(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out double value, default(FastBufferWriter.ForPrimitives));
				((FishTrophy)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FishTrophy)target).ClientScheduleFirstApproachPlaybackRpc(value);
				((FishTrophy)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "FishTrophy";
		}
	}
}
