using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using CableSystem;
using PaperPixelGames.Core.Boss;
using PaperPixelGames.Core.DailyReport;
using PaperPixelGames.Core.Data.Tools;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Ocean;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Core.Streaming;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Networking.Chat;
using PaperPixelGames.Wheel;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityServiceLocator;

namespace PaperPixelGames.Core.PlayerTools
{
	public class DivingEquipment : BaseTool
	{
		private enum DeepPenaltyReason : byte
		{
			None = 0,
			Depth = 1,
			SeaLayerEquipmentRequirement = 2
		}

		private const string DIVING_HOSE_GUIDE_LAYER = "DivingHoseGuide";

		private const string DIVING_HOSE_GUIDE_PREFIX = "HoseGuide_";

		private const int CABLE_REFRESH_SEGMENT_COUNT = 1;

		private const float CABLE_REFRESH_RETRACT_DELAY_SECONDS = 0.5f;

		private const byte FLIPPERS_FIRST_LEVEL = 2;

		private const int MaxLatchedPiranhas = 5;

		[Header("Debug HUD (No Canvas)")]
		[SerializeField]
		private bool _debugHudEnabled;

		[SerializeField]
		private KeyCode _debugHudToggleKey = KeyCode.F4;

		[SerializeField]
		[Range(0.02f, 1f)]
		private float _debugHudRefreshInterval = 0.1f;

		[SerializeField]
		private Vector2 _debugHudPos = new Vector2(12f, 12f);

		[SerializeField]
		private float _debugHudWidth = 560f;

		[SerializeField]
		private float _debugHudMaxHeight = 900f;

		[SerializeField]
		[Range(10f, 26f)]
		private int _debugHudFontSize = 14;

		public Action SwimmingUpdate;

		public Action<float> DepthUpdate;

		[Header("Oxygen System")]
		[SerializeField]
		private DivingEquipmentData _data;

		[SerializeField]
		private DivingEquipmentUI _divingEquipmentUI;

		[SerializeField]
		private float _oxygenRegenRate;

		[SerializeField]
		[Min(0.05f)]
		private float _deepPenaltyTickInterval = 0.3f;

		[SerializeField]
		[Min(1f)]
		private int _deepPenaltyOxygenCost = 1;

		[SerializeField]
		private float _underwaterConsumeOffset = 1f;

		[SerializeField]
		private float _underwaterAreaDepthThreshold = 6f;

		[SerializeField]
		private float _underwaterAreaDefaultOffset = 0.3f;

		[SerializeField]
		private float _underwaterAreaBelowOffset = 10f;

		[Header("FPS_Equipments")]
		[SerializeField]
		private GameObject _fpsHead;

		[SerializeField]
		private GameObject _fpsSpine;

		[SerializeField]
		private GameObject _fpsLeftFoot;

		[SerializeField]
		private GameObject _fpsRightFoot;

		[Header("TPS_Equipments")]
		[SerializeField]
		private GameObject _tpsHead;

		[SerializeField]
		private GameObject _tpsSpine;

		[SerializeField]
		private GameObject _tpsLeftFoot;

		[SerializeField]
		private GameObject _tpsRightFoot;

		[Header("Knockdown")]
		[SerializeField]
		[Min(1f)]
		private float _knockDurationSeconds = 12f;

		[SerializeField]
		[Min(1f)]
		private float _giveUpAcceleration = 3f;

		[SerializeField]
		private float _friendDistanceRefreshHz = 5f;

		[SerializeField]
		[Min(0f)]
		private float _friendAssistRange = 3f;

		[Header("FX")]
		[SerializeField]
		private AudioSource _audioSource;

		[SerializeField]
		private AudioClip _gainSFX;

		[SerializeField]
		private AudioClip _consumeSFX;

		[SerializeField]
		private AudioClip _drowningSFX;

		[Space]
		[SerializeField]
		private ParticleSystem _gainVFX;

		[SerializeField]
		private ParticleSystem _consumeVFX;

		[SerializeField]
		private ParticleSystem _drowningVFX;

		[Header("FX Audio Tuning")]
		[SerializeField]
		[Range(0f, 1f)]
		private float _gainBaseVolume = 0.8f;

		[SerializeField]
		[Range(0f, 1f)]
		private float _consumeBaseVolume = 0.8f;

		[SerializeField]
		private float _gainBasePitch = 1f;

		[SerializeField]
		private float _consumeBasePitch = 1f;

		[Header("Flashlight")]
		[SerializeField]
		private Light _flashlightSpot;

		[SerializeField]
		private Transform _flashlightRayOrigin;

		[SerializeField]
		private Transform _flashlightBeamRoot;

		[SerializeField]
		private bool _flashlightStartEnabled = true;

		[SerializeField]
		private float _flashlightMaxDistance = 12f;

		[SerializeField]
		private LayerMask _flashlightHitMask = -1;

		[Header("Debug")]
		[SerializeField]
		private bool _o2PreventLoss;

		[Header("PufferManager")]
		[SerializeField]
		private SillyPufferManager _sillyPufferManager;

		[Header("UI Visibility")]
		[SerializeField]
		private float _hideUIAfterOutOfWaterSeconds = 2f;

		[SerializeField]
		private bool _hideUIWhenOutOfWater = true;

		[SerializeField]
		private GameObject _deadIconPrefab;

		private float _outOfWaterUITimer;

		private bool _isDivingEquipmentUIVisible;

		private float _deepPenaltyTimer;

		private float _openWaterPenaltyTimer;

		private float _depthPenaltyStart = 50f;

		private bool _isUnderwater;

		private float _waterSurfaceY;

		private bool _isKnocked;

		private bool _finishKnocked;

		private bool _ownsKnockMovementPause;

		private float _knockTimeLeft;

		private float _friendDistanceRefreshTimer;

		private float _dataMaxOxygen;

		private float _maxOxygen;

		private float _baseDrainPerSec;

		private float _currentOxygen;

		private float _currentDepth;

		private float _dailyMaxDepth;

		private bool _isAccelerating;

		private int _latchPiranhaCount;

		private int _lockedAmount;

		private readonly HashSet<int> _latchedPiranhaIndexes = new HashSet<int>();

		private bool _isFlashlightOn;

		private float _flashlightCurrentHitDistance;

		private byte _spawnGraphicLevel;

		private bool _isDeepPenaltyAlertShown;

		private DeepPenaltyReason _activeDeepPenaltyAlertReason;

		private string _activeDeepPenaltyAlertDescription = string.Empty;

		private bool _wasEquipmentActive;

		private bool _wasInWaterMovementMode;

		private bool _tpsHeadGraphicVisible = true;

		private float _lastReportedDailyMaxDepth;

		private bool _abyssDepthReportedThisDive;

		private bool _serverAbyssDepthReached;

		private Coroutine _cableRefreshRoutine;

		private readonly List<Renderer> _tpsHeadGraphicRenderers = new List<Renderer>();

		private readonly Dictionary<Renderer, bool> _tpsHeadGraphicRendererStates = new Dictionary<Renderer, bool>();

		private readonly List<Cable> _extendedRefreshCables = new List<Cable>();

		private readonly List<Transform> _hoseGuidePoints = new List<Transform>();

		private const float DailyDepthReportMinDelta = 0.25f;

		private const float AbyssAchievementDepthMeters = 100f;

		private const byte FlashlightUnlockLevel = 3;

		[SerializeField]
		private List<LatchPiranha> _latchPiranhas;

		private bool _latchPiranhasCached;

		public override byte Level => _player.ToolsData.DivingEquipmentLevel;

		public override bool IsUnlocked => true;

		public bool PreventOxygenLoss { get; set; }

		public bool IsOxygenLossPrevented
		{
			get
			{
				if (!PreventOxygenLoss)
				{
					if (_player != null && _player.PoseidonTrident != null)
					{
						return _player.PoseidonTrident.ProvidesOxygenProtection;
					}
					return false;
				}
				return true;
			}
		}

		public bool IsKnocked => _isKnocked;

		public float CurrentDepth => _currentDepth;

		public InputHandler InputHandler => Singleton<InputManager>.Instance.InputHandler;

		public float DailyMaxDepth => _dailyMaxDepth;

		public GameObject FpsHead => _fpsHead;

		public GameObject TpsHead => _tpsHead;

		protected override bool ShouldShowCloseToolHint => !IsPlayerInWaterMovementMode();

		public bool HasLatchedPiranhas => _latchedPiranhaIndexes.Count > 0;

		private void InitializeFlashlight()
		{
			InputHandler.Flashlight += OnFlashLight;
			_isFlashlightOn = false;
			_flashlightCurrentHitDistance = _flashlightMaxDistance;
			ApplyFlashlightVisualState();
		}

		public void SetFlashlight(bool value)
		{
			if (Level < 3)
			{
				value = false;
			}
			_isFlashlightOn = value;
			ApplyFlashlightVisualState();
		}

		private void ApplyFlashlightVisualState()
		{
			bool active = base.IsOwner && base.ToolStatus && !_isKnocked && _isFlashlightOn && Level >= 3;
			if (_flashlightSpot != null)
			{
				_flashlightSpot.enabled = active;
			}
			if (_flashlightBeamRoot != null)
			{
				_flashlightBeamRoot.gameObject.SetActive(active);
			}
		}

		private void UpdateFlashlight()
		{
			if (!base.IsOwner)
			{
				return;
			}
			if (Level < 3)
			{
				if (_isFlashlightOn)
				{
					_isFlashlightOn = false;
					ApplyFlashlightVisualState();
				}
				_flashlightCurrentHitDistance = _flashlightMaxDistance;
			}
			else if (!_isFlashlightOn)
			{
				_flashlightCurrentHitDistance = _flashlightMaxDistance;
			}
			else
			{
				UpdateFlashlightRaycast();
			}
		}

		private void UpdateFlashlightRaycast()
		{
			if (_flashlightBeamRoot == null && _flashlightSpot == null)
			{
				return;
			}
			float value = _flashlightMaxDistance;
			if (_flashlightRayOrigin != null)
			{
				Vector3 position = _flashlightRayOrigin.position;
				Vector3 forward = _flashlightRayOrigin.forward;
				if (Physics.Raycast(position, forward, out var hitInfo, _flashlightMaxDistance, _flashlightHitMask, QueryTriggerInteraction.Ignore))
				{
					value = hitInfo.distance;
					Debug.DrawLine(position, hitInfo.point, Color.green);
				}
				else
				{
					Debug.DrawLine(position, position + forward * _flashlightMaxDistance, Color.red);
				}
			}
			_flashlightCurrentHitDistance = Mathf.Clamp(value, 0.15f, _flashlightMaxDistance);
			if (_flashlightBeamRoot != null)
			{
				Vector3 localScale = _flashlightBeamRoot.localScale;
				localScale.z = _flashlightCurrentHitDistance;
				_flashlightBeamRoot.localScale = localScale;
			}
			if (_flashlightSpot != null)
			{
				_flashlightSpot.range = _flashlightCurrentHitDistance;
			}
		}

		private void Start()
		{
			CacheLatchPiranhas();
			ResetLatchedPiranhasLocal(playRemoveSfx: false);
			_tpsGraphicParent.gameObject.SetActive(value: false);
			_fpsGraphicParent.gameObject.SetActive(value: false);
			SetEquipmentActive(active: false);
			if (base.IsOwner)
			{
				InitializeFlashlight();
				PreventOxygenLoss = _o2PreventLoss;
				_currentOxygen = _maxOxygen;
				_divingEquipmentUI.SetDepthUIImmediate(0f);
				SetDivingEquipmentUIVisibility(visible: false, force: true);
				Singleton<OceanFXManager>.Instance.SetUnderwater(isUnderwater: false);
				Singleton<OceanFXManager>.Instance.SetLocomotionState(OceanLocomotionState.Idle, 0f);
				if (Singleton<DressingSceneManager>.Instance != null)
				{
					DepthUpdate = (Action<float>)Delegate.Combine(DepthUpdate, new Action<float>(Singleton<DressingSceneManager>.Instance.OnDepthUpdate));
				}
			}
		}

		private void Update()
		{
			if (!base.IsOwner)
			{
				return;
			}
			bool isUnderwater = _isUnderwater;
			_isUnderwater = ComputeIsUnderwater();
			UpdateAbyssDiveBoundary(isUnderwater, _isUnderwater);
			UpdateDivingEquipmentUIVisibility();
			RefreshCloseToolHintOnWaterMovementChange();
			if (_player.FirstPersonCharacterInput.UnderwaterArea != null)
			{
				bool flag = _currentDepth > _underwaterAreaDepthThreshold;
				_player.FirstPersonCharacterInput.UnderwaterArea.waterlineOffset = (flag ? _underwaterAreaBelowOffset : _underwaterAreaDefaultOffset);
			}
			UpdateFlashlight();
			if (base.ToolStatus)
			{
				if (_isKnocked)
				{
					UpdateKnock();
					return;
				}
				UpdateDepth();
				UpdateOxygen();
				UpdateLocomotionFx();
				float o2Normalized = ((_maxOxygen > 0.0001f) ? (_currentOxygen / _maxOxygen) : 0f);
				Singleton<GlobalVolumeManager>.Instance.TickDivingO2(GetDominantUnderwaterVolume(), o2Normalized);
			}
		}

		public override void OnDestroy()
		{
			ResetLatchedPiranhasLocal(playRemoveSfx: false);
			if (base.IsOwner && Singleton<InputManager>.TryGetInstance(out var destroyInputManager) && destroyInputManager.InputHandler != null)
			{
				destroyInputManager.InputHandler.Flashlight -= OnFlashLight;
			}
			if (Singleton<DressingSceneManager>.Instance != null)
			{
				DepthUpdate = (Action<float>)Delegate.Remove(DepthUpdate, new Action<float>(Singleton<DressingSceneManager>.Instance.OnDepthUpdate));
			}
			base.OnDestroy();
		}

		private void OnFlashLight(InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started && _isUnderwater && !_isKnocked && Level >= 3)
			{
				SetFlashlight(!_isFlashlightOn);
			}
		}

		public override void Mouse1(InputActionPhase phase)
		{
			UtilityMethods.Log("[Divingequipment] Mouse1");
		}

		public override void Mouse2(InputActionPhase phase)
		{
			UtilityMethods.Log("[Divingequipment] Mouse2");
		}

		public override bool DisableIf()
		{
			return true;
		}

		public override void LoadLevel(ToolDataNet data)
		{
			UtilityMethods.Log($"[HarpoonTool-{base.OwnerClientId}] LoadLevel:{data.DivingEquipmentLevel}");
			ApplyLevel(data.DivingEquipmentLevel);
			if (base.IsOwner)
			{
				SetUnlock(true);
			}
		}

		protected override void SetUnlock(bool isUnlocked)
		{
			UtilityMethods.Log($"[DivingEquipment] SetUnlock: {isUnlocked}");
			Singleton<PPG_WheelManager>.Instance.UnlockDivingEquipment(isUnlocked);
		}

		public override void ApplyLevel(byte newLevel)
		{
			_spawnGraphicLevel = newLevel;
			base.ApplyLevel(newLevel);
			DivingEquipmentLevelData divingEquipmentLevelData = _data.Levels.Find((DivingEquipmentLevelData l) => l.Level == newLevel);
			if (divingEquipmentLevelData == null)
			{
				UtilityMethods.Log($"[DivingEquipment] No diving data found for level {newLevel}", LogType.Warning);
				return;
			}
			_dataMaxOxygen = divingEquipmentLevelData.MaxOxygen;
			UpdateMaxOxygen();
			_baseDrainPerSec = Mathf.Max(0f, divingEquipmentLevelData.BaseDrainPerSec);
			_depthPenaltyStart = Mathf.Max(0.01f, divingEquipmentLevelData.DepthPenaltyStart);
			_deepPenaltyTickInterval = Mathf.Max(0.05f, divingEquipmentLevelData.DeepPenaltyTickInterval);
			_deepPenaltyOxygenCost = Mathf.Max(1, divingEquipmentLevelData.DeepPenaltyOxygenCost);
			_currentOxygen = _maxOxygen;
			_deepPenaltyTimer = 0f;
			if (newLevel < 3)
			{
				SetFlashlight(value: false);
			}
			else
			{
				ApplyFlashlightVisualState();
			}
			float oxygenLevel = ((_maxOxygen > 0.0001f) ? (_currentOxygen / _maxOxygen) : 0f);
			_divingEquipmentUI.SetOxygenLevel(oxygenLevel);
			float num = (float)_deepPenaltyOxygenCost / _deepPenaltyTickInterval;
			UtilityMethods.Log($"<b><color=#55DDFF>[Diving LEVEL]</color></b> Lv<b>{newLevel}</b> applied | " + $"MaxO2=<b>{_maxOxygen:0.##}</b> | " + $"BaseDrain=<b>{_baseDrainPerSec:0.###}</b>/s | " + $"DepthPenaltyStart=<b>{_depthPenaltyStart:0.#}</b>m | " + $"PenaltyTick=<b>{_deepPenaltyTickInterval:0.##}</b>s | " + $"PenaltyCost=<b>{_deepPenaltyOxygenCost}</b> | " + $"PenaltyDrain=<b>{num:0.###}</b>/s");
		}

		private void UpdateMaxOxygen()
		{
			_maxOxygen = Mathf.Max(0f, _dataMaxOxygen) - _dataMaxOxygen / 10f * (float)_lockedAmount;
			if (_isUnderwater)
			{
				if (_currentOxygen > _maxOxygen)
				{
					_currentOxygen = _maxOxygen;
				}
			}
			else
			{
				_currentOxygen = _maxOxygen;
			}
		}

		private void UpdateLockedAmount(int amount)
		{
			_lockedAmount = Mathf.Clamp(amount, 0, 5);
			if (_divingEquipmentUI != null)
			{
				_divingEquipmentUI.UpdateLockBar(_lockedAmount);
			}
			UpdateMaxOxygen();
			RefreshOxygenUI();
		}

		private void RefreshOxygenUI()
		{
			if (!(_divingEquipmentUI == null))
			{
				float oxygenLevel = ((_maxOxygen > 0.0001f) ? Mathf.Clamp01(_currentOxygen / _maxOxygen) : 0f);
				_divingEquipmentUI.SetOxygenLevel(oxygenLevel);
			}
		}

		protected override void ApplyToolStatus(bool status)
		{
			bool wasEquipmentActive = _wasEquipmentActive;
			base.ApplyToolStatus(status);
			SetEquipmentActive(status);
			_wasEquipmentActive = status;
			if (status && base.IsOwner)
			{
				StartCableRefreshPulse();
			}
			else
			{
				CancelCableRefreshPulse(retractExtendedSegments: true);
			}
			if (wasEquipmentActive && !status)
			{
				_sillyPufferManager?.ResetPuffers();
			}
			UpdateDivingEquipmentFeetVisibility(status);
			if (base.IsOwner)
			{
				_outOfWaterUITimer = 0f;
				if (status)
				{
					bool visible = !_hideUIWhenOutOfWater || _isUnderwater;
					SetDivingEquipmentUIVisibility(visible, force: true);
				}
				else
				{
					SetDivingEquipmentUIVisibility(visible: false, force: true);
					_divingEquipmentUI.SetDepthUIImmediate(0f);
				}
				if (_fpsGraphic != null)
				{
					_fpsGraphic.SetActive(value: false);
				}
				_player.Sway.EnableGogglesUI(status);
			}
		}

		protected override void NetworkDespawn()
		{
			ResetLatchedPiranhasLocal(playRemoveSfx: false);
			base.NetworkDespawn();
		}

		private void UpdateDivingEquipmentFeetVisibility(bool equipmentActive)
		{
			bool flag = equipmentActive && _spawnGraphicLevel >= 2;
			_player.CharacterVisuals?.SetDivingEquipmentFeetMeshesVisible(!flag);
		}

		public void SetTpsHeadGraphicVisible(bool visible)
		{
			_tpsHeadGraphicVisible = visible;
			RebuildTpsHeadGraphicRendererCache();
			ApplyTpsHeadGraphicVisibility();
		}

		private void SetEquipmentActive(bool active)
		{
			_tpsHead.SetActive(active);
			_tpsSpine.SetActive(active);
			_tpsLeftFoot.SetActive(active);
			_tpsRightFoot.SetActive(active);
			ApplyTpsHeadGraphicVisibility();
		}

		private void StartCableRefreshPulse()
		{
			CancelCableRefreshPulse(retractExtendedSegments: true);
			_cableRefreshRoutine = StartCoroutine(CableRefreshPulseRoutine());
		}

		private void CancelCableRefreshPulse(bool retractExtendedSegments)
		{
			if (_cableRefreshRoutine != null)
			{
				StopCoroutine(_cableRefreshRoutine);
				_cableRefreshRoutine = null;
			}
			if (retractExtendedSegments)
			{
				RetractExtendedRefreshCables();
			}
			else
			{
				_extendedRefreshCables.Clear();
			}
		}

		private IEnumerator CableRefreshPulseRoutine()
		{
			yield return null;
			_extendedRefreshCables.Clear();
			CollectCableRefreshTargets(_extendedRefreshCables);
			for (int i = 0; i < _extendedRefreshCables.Count; i++)
			{
				Cable cable = _extendedRefreshCables[i];
				if (cable == null || !cable.isActiveAndEnabled)
				{
					_extendedRefreshCables.RemoveAt(i);
					i--;
				}
				else
				{
					cable.ExtendCable(1);
				}
			}
			yield return new WaitForSeconds(0.5f);
			RetractExtendedRefreshCables();
			_cableRefreshRoutine = null;
		}

		private void RetractExtendedRefreshCables()
		{
			for (int i = 0; i < _extendedRefreshCables.Count; i++)
			{
				Cable cable = _extendedRefreshCables[i];
				if (!(cable == null))
				{
					cable.RetractCable(1);
				}
			}
			_extendedRefreshCables.Clear();
		}

		private void CollectCableRefreshTargets(List<Cable> targets)
		{
			targets.Clear();
			AddCableRefreshTargets(_tpsHead, targets);
			AddCableRefreshTargets(_tpsSpine, targets);
			AddCableRefreshTargets(_tpsLeftFoot, targets);
			AddCableRefreshTargets(_tpsRightFoot, targets);
			AddCableRefreshTargets(_fpsHead, targets);
			AddCableRefreshTargets(_fpsSpine, targets);
			AddCableRefreshTargets(_fpsLeftFoot, targets);
			AddCableRefreshTargets(_fpsRightFoot, targets);
		}

		private void AddCableRefreshTargets(GameObject root, List<Cable> targets)
		{
			if (root == null)
			{
				return;
			}
			Cable[] componentsInChildren = root.GetComponentsInChildren<Cable>(includeInactive: true);
			foreach (Cable cable in componentsInChildren)
			{
				if (!(cable == null) && !cable.UseGuidePath && !targets.Contains(cable))
				{
					targets.Add(cable);
				}
			}
		}

		private bool ComputeIsUnderwater()
		{
			bool num = IsPlayerInWaterMovementMode();
			bool flag = _player.RigBones.TpsRig.Head.position.y + 0.1f < _waterSurfaceY;
			if (num)
			{
				SwimmingUpdate?.Invoke();
			}
			return num & flag;
		}

		private bool IsPlayerInWaterMovementMode()
		{
			if (_player != null && _player.FirstPersonCharacter != null)
			{
				if (!_player.FirstPersonCharacter.IsSwimming())
				{
					return _player.FirstPersonCharacter.IsSeaScooter();
				}
				return true;
			}
			return false;
		}

		private void RefreshCloseToolHintOnWaterMovementChange()
		{
			bool flag = IsPlayerInWaterMovementMode();
			if (_wasInWaterMovementMode != flag)
			{
				_wasInWaterMovementMode = flag;
				RefreshCloseToolHint();
			}
		}

		protected override void SpawnGraphics()
		{
			UtilityMethods.Log($"[DivingEquipment] SpawnGraphics Level {_spawnGraphicLevel}");
			GameObject graphicPrefabForLevel = GetGraphicPrefabForLevel(_spawnGraphicLevel);
			if (graphicPrefabForLevel == null)
			{
				UtilityMethods.Log($"[DivingEquipment] Graphic prefab is null for level {_spawnGraphicLevel}", LogType.Warning);
				return;
			}
			GameObject gameObject = UnityEngine.Object.Instantiate(graphicPrefabForLevel);
			Transform transform = gameObject.transform;
			if (transform.childCount < 4)
			{
				UtilityMethods.Log($"[DivingEquipment] Graphic prefab '{graphicPrefabForLevel.name}' must contain at least 4 children. Found: {transform.childCount}", LogType.Error);
				UnityEngine.Object.Destroy(gameObject);
				return;
			}
			Transform child = transform.GetChild(0);
			Transform child2 = transform.GetChild(1);
			Transform child3 = transform.GetChild(2);
			Transform child4 = transform.GetChild(3);
			MovePartToSlot(child, _tpsHead.transform);
			MovePartToSlot(child2, _tpsSpine.transform);
			MovePartToSlot(child3, _tpsLeftFoot.transform);
			MovePartToSlot(child4, _tpsRightFoot.transform);
			_tpsGraphic = _tpsHead;
			_fpsGraphic = null;
			RebuildTpsHeadGraphicRendererCache();
			ApplyTpsHeadGraphicVisibility();
			ConfigureCableSimulationMode();
		}

		protected override GameObject GetGraphicByLevel()
		{
			return GetGraphicPrefabForLevel(_spawnGraphicLevel);
		}

		private GameObject GetGraphicPrefabForLevel(byte level)
		{
			DivingEquipmentLevelData divingEquipmentLevelData = _data.Levels.Find((DivingEquipmentLevelData l) => l.Level == level);
			if (!(divingEquipmentLevelData != null))
			{
				return null;
			}
			return divingEquipmentLevelData.GraphicPrefab;
		}

		private void ConfigureCableSimulationMode()
		{
			CollectDivingHoseGuidePoints(_hoseGuidePoints);
			SetCableGuidePathMode(_tpsHead, _hoseGuidePoints);
			SetCableGuidePathMode(_tpsSpine, _hoseGuidePoints);
			SetCableGuidePathMode(_tpsLeftFoot, _hoseGuidePoints);
			SetCableGuidePathMode(_tpsRightFoot, _hoseGuidePoints);
			SetCableGuidePathMode(_fpsHead, _hoseGuidePoints);
			SetCableGuidePathMode(_fpsSpine, _hoseGuidePoints);
			SetCableGuidePathMode(_fpsLeftFoot, _hoseGuidePoints);
			SetCableGuidePathMode(_fpsRightFoot, _hoseGuidePoints);
		}

		private void CollectDivingHoseGuidePoints(List<Transform> guidePoints)
		{
			guidePoints.Clear();
			AddDivingHoseGuidePoints(_tpsHead, guidePoints);
			AddDivingHoseGuidePoints(_tpsSpine, guidePoints);
			AddDivingHoseGuidePoints(_tpsLeftFoot, guidePoints);
			AddDivingHoseGuidePoints(_tpsRightFoot, guidePoints);
		}

		private void AddDivingHoseGuidePoints(GameObject root, List<Transform> guidePoints)
		{
			if (root == null)
			{
				return;
			}
			Transform[] componentsInChildren = root.GetComponentsInChildren<Transform>(includeInactive: true);
			foreach (Transform transform in componentsInChildren)
			{
				if (!(transform == null) && transform.name.StartsWith("HoseGuide_", StringComparison.Ordinal) && !guidePoints.Contains(transform))
				{
					guidePoints.Add(transform);
				}
			}
		}

		private void SetCableGuidePathMode(GameObject root, List<Transform> guidePoints)
		{
			if (root == null)
			{
				return;
			}
			Cable[] componentsInChildren = root.GetComponentsInChildren<Cable>(includeInactive: true);
			foreach (Cable cable in componentsInChildren)
			{
				if (!(cable == null))
				{
					bool useGuidePath = guidePoints != null && guidePoints.Count > 0;
					cable.SetVisualOnlyMode(visualOnly: false);
					cable.SetGuidePathMode(useGuidePath, guidePoints);
				}
			}
		}

		protected override void SetEquipmentLayer()
		{
			string layer = (base.IsOwner ? "Player" : "ClientPlayer");
			UtilityMethods.SetLayerRecursive(_tpsHead, layer, includeChildren: true);
			UtilityMethods.SetLayerRecursive(_tpsSpine, layer, includeChildren: true);
			UtilityMethods.SetLayerRecursive(_tpsLeftFoot, layer, includeChildren: true);
			UtilityMethods.SetLayerRecursive(_tpsRightFoot, layer, includeChildren: true);
			if (!base.IsOwner)
			{
				RefreshDivingHoseGuideLayers();
				return;
			}
			layer = "ViewModel";
			UtilityMethods.SetLayerRecursive(_fpsHead, layer, includeChildren: true);
			UtilityMethods.SetLayerRecursive(_fpsSpine, layer, includeChildren: true);
			UtilityMethods.SetLayerRecursive(_fpsLeftFoot, layer, includeChildren: true);
			UtilityMethods.SetLayerRecursive(_fpsRightFoot, layer, includeChildren: true);
			RefreshDivingHoseGuideLayers();
		}

		public void RefreshDivingHoseGuideLayers()
		{
			int num = LayerMask.NameToLayer("DivingHoseGuide");
			if (num >= 0)
			{
				ApplyDivingHoseGuideLayer(base.transform, num);
				ApplyDivingHoseGuideLayer((_tpsHead != null) ? _tpsHead.transform : null, num);
				ApplyDivingHoseGuideLayer((_tpsSpine != null) ? _tpsSpine.transform : null, num);
				ApplyDivingHoseGuideLayer((_tpsLeftFoot != null) ? _tpsLeftFoot.transform : null, num);
				ApplyDivingHoseGuideLayer((_tpsRightFoot != null) ? _tpsRightFoot.transform : null, num);
				ApplyDivingHoseGuideLayer((_fpsHead != null) ? _fpsHead.transform : null, num);
				ApplyDivingHoseGuideLayer((_fpsSpine != null) ? _fpsSpine.transform : null, num);
				ApplyDivingHoseGuideLayer((_fpsLeftFoot != null) ? _fpsLeftFoot.transform : null, num);
				ApplyDivingHoseGuideLayer((_fpsRightFoot != null) ? _fpsRightFoot.transform : null, num);
			}
		}

		private void ApplyDivingHoseGuideLayer(Transform root, int guideLayer)
		{
			if (root == null)
			{
				return;
			}
			Transform[] componentsInChildren = root.GetComponentsInChildren<Transform>(includeInactive: true);
			foreach (Transform transform in componentsInChildren)
			{
				if (!(transform == null) && transform.name.StartsWith("HoseGuide_", StringComparison.Ordinal))
				{
					transform.gameObject.layer = guideLayer;
				}
			}
		}

		protected override int GetLevelCount()
		{
			return _data.Levels.Count;
		}

		protected override void DestroyGraphics()
		{
			ClearSlot(_tpsHead.transform);
			ClearSlot(_tpsSpine.transform);
			ClearSlot(_tpsLeftFoot.transform);
			ClearSlot(_tpsRightFoot.transform);
			ClearSlot(_fpsHead.transform);
			ClearSlot(_fpsSpine.transform);
			ClearSlot(_fpsLeftFoot.transform);
			ClearSlot(_fpsRightFoot.transform);
			_tpsGraphic = null;
			_fpsGraphic = null;
			_tpsHeadGraphicRenderers.Clear();
			_tpsHeadGraphicRendererStates.Clear();
		}

		private void MovePartToSlot(Transform part, Transform slot)
		{
			if (!(part == null) && !(slot == null))
			{
				part.SetParent(slot, worldPositionStays: false);
				part.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
				part.localScale = Vector3.one;
				part.gameObject.SetActive(value: true);
			}
		}

		private void ClearSlot(Transform slot)
		{
			if (!(slot == null))
			{
				for (int num = slot.childCount - 1; num >= 0; num--)
				{
					UnityEngine.Object.Destroy(slot.GetChild(num).gameObject);
				}
			}
		}

		private void RebuildTpsHeadGraphicRendererCache()
		{
			_tpsHeadGraphicRenderers.Clear();
			if (_tpsHead == null)
			{
				return;
			}
			Renderer[] componentsInChildren = _tpsHead.GetComponentsInChildren<Renderer>(includeInactive: true);
			foreach (Renderer renderer in componentsInChildren)
			{
				if (!(renderer == null) && !_tpsHeadGraphicRenderers.Contains(renderer))
				{
					_tpsHeadGraphicRenderers.Add(renderer);
				}
			}
		}

		private void ApplyTpsHeadGraphicVisibility()
		{
			if (_tpsHeadGraphicRenderers.Count == 0)
			{
				RebuildTpsHeadGraphicRendererCache();
			}
			for (int i = 0; i < _tpsHeadGraphicRenderers.Count; i++)
			{
				Renderer renderer = _tpsHeadGraphicRenderers[i];
				if (renderer == null)
				{
					continue;
				}
				if (_tpsHeadGraphicVisible)
				{
					if (_tpsHeadGraphicRendererStates.TryGetValue(renderer, out var value))
					{
						renderer.enabled = value;
					}
					continue;
				}
				if (!_tpsHeadGraphicRendererStates.ContainsKey(renderer))
				{
					_tpsHeadGraphicRendererStates.Add(renderer, renderer.enabled);
				}
				renderer.enabled = false;
			}
			if (_tpsHeadGraphicVisible)
			{
				_tpsHeadGraphicRendererStates.Clear();
			}
		}

		private void UpdateDivingEquipmentUIVisibility()
		{
			if (_divingEquipmentUI == null)
			{
				return;
			}
			if (!base.ToolStatus)
			{
				_outOfWaterUITimer = 0f;
				SetDivingEquipmentUIVisibility(visible: false);
				return;
			}
			if (!_hideUIWhenOutOfWater)
			{
				_outOfWaterUITimer = 0f;
				SetDivingEquipmentUIVisibility(visible: true);
				return;
			}
			if (_isUnderwater)
			{
				_outOfWaterUITimer = 0f;
				SetDivingEquipmentUIVisibility(visible: true);
				return;
			}
			_outOfWaterUITimer += Time.deltaTime;
			if (_outOfWaterUITimer >= _hideUIAfterOutOfWaterSeconds)
			{
				SetDivingEquipmentUIVisibility(visible: false);
			}
		}

		private void SetDivingEquipmentUIVisibility(bool visible, bool force = false)
		{
			if (!(_divingEquipmentUI == null) && (force || _isDivingEquipmentUIVisible != visible))
			{
				_isDivingEquipmentUIVisible = visible;
				_divingEquipmentUI.SetVisibility(visible);
			}
		}

		public void SetUIVisibilitySuppressed(bool suppressed)
		{
			if (_divingEquipmentUI != null)
			{
				_divingEquipmentUI.SetVisibilitySuppressed(suppressed);
			}
		}

		public void SetDepthUIVisibilitySuppressed(bool suppressed)
		{
			if (_divingEquipmentUI != null)
			{
				_divingEquipmentUI.SetDepthVisibilitySuppressed(suppressed);
			}
		}

		public void SetSurface(float surface)
		{
			_waterSurfaceY = surface;
		}

		private void UpdateDepth()
		{
			float y = _player.RigBones.TpsRig.Head.position.y;
			_currentDepth = (IsPlayerInWaterMovementMode() ? Mathf.Max(0f, _waterSurfaceY - y) : 0f);
			if (!_abyssDepthReportedThisDive && _currentDepth >= 100f)
			{
				_abyssDepthReportedThisDive = true;
				ServerReportAbyssDepthReachedRpc(_currentDepth);
			}
			if (_currentDepth > _dailyMaxDepth)
			{
				_dailyMaxDepth = _currentDepth;
				ReportDailyMaxDepthIfNeeded();
			}
			_divingEquipmentUI.SetDepthUI(_currentDepth);
			DepthUpdate?.Invoke(_currentDepth);
		}

		private void UpdateAbyssDiveBoundary(bool wasUnderwater, bool isUnderwater)
		{
			if (!wasUnderwater & isUnderwater)
			{
				_abyssDepthReportedThisDive = false;
			}
			else if (!(!wasUnderwater | isUnderwater) && _abyssDepthReportedThisDive)
			{
				_abyssDepthReportedThisDive = false;
				ServerReportAbyssSurfaceRpc();
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerReportAbyssDepthReachedRpc(float depthMeters, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1487025329u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in depthMeters, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1487025329u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsServer && !(_player == null) && _player.OwnerClientId == rpcParams.Receive.SenderClientId && float.IsFinite(depthMeters) && !(depthMeters < 100f))
				{
					_serverAbyssDepthReached = true;
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerReportAbyssSurfaceRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2810751191u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 2810751191u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsServer && !(_player == null) && _player.OwnerClientId == rpcParams.Receive.SenderClientId && _serverAbyssDepthReached)
				{
					_serverAbyssDepthReached = false;
					Singleton<GameProgressManager>.Instance?.RpcRelay?.ServerUnlockSteamAchievementForClient("ACH_THE_ABYSS_STARES_BACK", _player.OwnerClientId);
				}
			}
		}

		public void ResetDailyMaxDepth()
		{
			_dailyMaxDepth = 0f;
			_lastReportedDailyMaxDepth = 0f;
		}

		private void ReportDailyMaxDepthIfNeeded()
		{
			if (base.IsOwner && !(_dailyMaxDepth < _lastReportedDailyMaxDepth + 0.25f))
			{
				_lastReportedDailyMaxDepth = _dailyMaxDepth;
				ServerRecordDailyDiveDepthRpc(_dailyMaxDepth);
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerRecordDailyDiveDepthRpc(float depthMeters, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1909868834u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in depthMeters, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1909868834u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsServer && !(_player == null) && _player.OwnerClientId == rpcParams.Receive.SenderClientId && ServiceLocator.ForSceneOf(this).TryGet<DailyReportBroadcaster>(out var service) && service != null)
				{
					service.ServerRecordDiveDepth(_player.SteamID.Value, depthMeters);
				}
			}
		}

		private void UpdateOxygen()
		{
			if (_maxOxygen <= 0.0001f)
			{
				_divingEquipmentUI.SetOxygenLevel(0f);
				return;
			}
			bool flag = _player != null && _player.FirstPersonCharacterInput != null && _player.FirstPersonCharacterInput.IsHeadOut;
			if (!IsOxygenLossPrevented && _isUnderwater && _currentDepth > _underwaterConsumeOffset && !flag && base.ToolStatus && !_isKnocked)
			{
				UpdateBaseOxygenConsume();
				UpdateDeepPenaltyConsume();
			}
			else
			{
				_deepPenaltyTimer = 0f;
				if (_currentOxygen < _maxOxygen)
				{
					_currentOxygen += _oxygenRegenRate * Time.deltaTime;
					_currentOxygen = Mathf.Min(_currentOxygen, _maxOxygen);
				}
				RefreshDeepPenaltyAlert(includeDepthPenalty: false);
			}
			_divingEquipmentUI.SetOxygenLevel(_currentOxygen / _maxOxygen);
		}

		private void UpdateBaseOxygenConsume()
		{
			float baseDrainPerSec = _baseDrainPerSec;
			_currentOxygen -= baseDrainPerSec * Time.deltaTime;
			if (_currentOxygen <= 0f)
			{
				_currentOxygen = 0f;
				OnOxygenDepleted();
			}
		}

		private void UpdateDeepPenaltyConsume()
		{
			DeepPenaltyReason deepPenaltyReason = GetDeepPenaltyReason(out var requiredEquipmentLevelIndex);
			RefreshDeepPenaltyAlert(deepPenaltyReason, requiredEquipmentLevelIndex);
			if (deepPenaltyReason == DeepPenaltyReason.None)
			{
				_deepPenaltyTimer = 0f;
				return;
			}
			_deepPenaltyTimer += Time.deltaTime;
			while (_deepPenaltyTimer >= _deepPenaltyTickInterval)
			{
				_deepPenaltyTimer -= _deepPenaltyTickInterval;
				ConsumeOxygen((byte)_deepPenaltyOxygenCost, withFeedback: true);
				if (_currentOxygen <= 0f || _isKnocked)
				{
					break;
				}
			}
		}

		internal void HandleOwnerPrimarySeaLayerChanged()
		{
			if (base.IsOwner)
			{
				RefreshDeepPenaltyAlert(includeDepthPenalty: false);
			}
		}

		private void RefreshDeepPenaltyAlert(bool includeDepthPenalty)
		{
			DeepPenaltyReason deepPenaltyReason = GetDeepPenaltyReason(out var requiredEquipmentLevelIndex);
			if (!includeDepthPenalty && deepPenaltyReason == DeepPenaltyReason.Depth)
			{
				deepPenaltyReason = DeepPenaltyReason.None;
			}
			RefreshDeepPenaltyAlert(deepPenaltyReason, requiredEquipmentLevelIndex);
		}

		private void RefreshDeepPenaltyAlert(DeepPenaltyReason penaltyReason, byte requiredEquipmentLevelIndex)
		{
			if (penaltyReason == DeepPenaltyReason.None)
			{
				ClearDeepPenaltyAlert();
				return;
			}
			string deepPenaltyAlertDescription = GetDeepPenaltyAlertDescription(penaltyReason, requiredEquipmentLevelIndex);
			if (!_isDeepPenaltyAlertShown || _activeDeepPenaltyAlertReason != penaltyReason || !(_activeDeepPenaltyAlertDescription == deepPenaltyAlertDescription))
			{
				NotifyManager instance = Singleton<NotifyManager>.Instance;
				if (!(instance == null))
				{
					UtilityMethods.Log($"[DivingEquipment] Showing pressure alert. Reason={penaltyReason}");
					_isDeepPenaltyAlertShown = true;
					_activeDeepPenaltyAlertReason = penaltyReason;
					_activeDeepPenaltyAlertDescription = deepPenaltyAlertDescription;
					instance.ShowAlert("Pressure", deepPenaltyAlertDescription, -1f, AlertPriority.Critical, autoClose: false);
				}
			}
		}

		private void ClearDeepPenaltyAlert()
		{
			if (_isDeepPenaltyAlertShown)
			{
				UtilityMethods.Log($"[DivingEquipment] Hiding pressure alert. Reason={_activeDeepPenaltyAlertReason}");
				Singleton<NotifyManager>.Instance?.TryHideAlert("Pressure", _activeDeepPenaltyAlertDescription);
				_isDeepPenaltyAlertShown = false;
				_activeDeepPenaltyAlertReason = DeepPenaltyReason.None;
				_activeDeepPenaltyAlertDescription = string.Empty;
			}
		}

		private bool IsDeepPenaltyActive()
		{
			byte requiredEquipmentLevelIndex;
			return GetDeepPenaltyReason(out requiredEquipmentLevelIndex) != DeepPenaltyReason.None;
		}

		private DeepPenaltyReason GetDeepPenaltyReason(out byte requiredEquipmentLevelIndex)
		{
			requiredEquipmentLevelIndex = 0;
			SeaLayer seaLayer = ((_player != null) ? _player.CurrentSeaLayer : null);
			byte equipmentLevelIndex = (byte)((_player != null) ? _player.EquippedDivingEquipmentLevel : 0);
			if (seaLayer != null && seaLayer.AppliesDivingEquipmentLevelPenalty(equipmentLevelIndex))
			{
				requiredEquipmentLevelIndex = seaLayer.RequiredDivingEquipmentLevelIndex;
				return DeepPenaltyReason.SeaLayerEquipmentRequirement;
			}
			if (!(_currentDepth > _depthPenaltyStart))
			{
				return DeepPenaltyReason.None;
			}
			return DeepPenaltyReason.Depth;
		}

		private static string GetDeepPenaltyAlertDescription(DeepPenaltyReason penaltyReason, byte requiredEquipmentLevelIndex)
		{
			if (penaltyReason != DeepPenaltyReason.SeaLayerEquipmentRequirement)
			{
				return "Depth too deep for your equipment!";
			}
			int num = requiredEquipmentLevelIndex + 1;
			return LocalizationDatabase.GetFormattedString("UiTable", "notification.diving_suit_level_required_for_cave", "Your Diving Suit must be Level {0} in this cave!", num);
		}

		public void ResetOpenWaterPenalty()
		{
			if (base.IsOwner)
			{
				_openWaterPenaltyTimer = 0f;
			}
		}

		public void TickOpenWaterPenalty(float deltaTime)
		{
			if (!base.IsOwner || deltaTime <= 0f || IsOxygenLossPrevented || _isKnocked || _maxOxygen <= 0.0001f)
			{
				return;
			}
			_openWaterPenaltyTimer += deltaTime;
			while (_openWaterPenaltyTimer >= _deepPenaltyTickInterval)
			{
				_openWaterPenaltyTimer -= _deepPenaltyTickInterval;
				ConsumeOxygen((byte)_deepPenaltyOxygenCost, withFeedback: true);
				if (_currentOxygen <= 0f || _isKnocked)
				{
					break;
				}
			}
		}

		private void UpdateLocomotionFx()
		{
			if (_isUnderwater && !_isKnocked)
			{
				float speed = _player.FirstPersonCharacter.speed;
				OceanLocomotionState newState;
				float normalizedSpeed;
				if (speed < 0.1f)
				{
					newState = OceanLocomotionState.Idle;
					normalizedSpeed = 0f;
				}
				else if (!_player.FirstPersonCharacter.IsSprinting)
				{
					newState = OceanLocomotionState.Move;
					normalizedSpeed = Mathf.InverseLerp(0.1f, _player.FirstPersonCharacter.maxSwimSpeed * 2f, speed);
				}
				else
				{
					newState = OceanLocomotionState.Sprint;
					normalizedSpeed = 1f;
				}
				Singleton<OceanFXManager>.Instance.SetLocomotionState(newState, normalizedSpeed);
			}
		}

		public void RefillOxygenFull()
		{
			_player.AnimSetTrigger(PPG_Player.DROWN_END);
			_player.SetActionCamera(status: false);
			_player.ResumeConstraints();
			Singleton<OceanFXManager>.Instance.SetLocomotionState(OceanLocomotionState.Idle, 0f);
			_currentOxygen = _maxOxygen;
			_divingEquipmentUI.SetOxygenLevel(_currentOxygen / _maxOxygen);
			PlayOxygenGainFx(_maxOxygen);
			UtilityMethods.Log("[O₂ REFILL] Oxygen fully restored.");
		}

		public void ApplyOxygen(byte amount)
		{
			if (!((float)(int)amount <= 0f))
			{
				_currentOxygen += (int)amount;
				_currentOxygen = Mathf.Min(_currentOxygen, _maxOxygen);
				_divingEquipmentUI.SetOxygenLevel(_currentOxygen / _maxOxygen);
				UtilityMethods.Log($"[O₂ BOOST] +{amount} applied. Current O₂: {_currentOxygen:F1}");
				PlayOxygenGainFx((int)amount);
				if (_isKnocked && _currentOxygen > _maxOxygen * 0.05f)
				{
					CancelKnockAndRecover(_currentOxygen / _maxOxygen);
				}
			}
		}

		public void ConsumeOxygen(byte amount, bool withFeedback = false, bool showExpandingPulseFeedback = false)
		{
			if (amount > 0 && !IsOxygenLossPrevented)
			{
				_currentOxygen -= (int)amount;
				_currentOxygen = Mathf.Max(0f, _currentOxygen);
				_divingEquipmentUI.SetOxygenLevel(_currentOxygen / _maxOxygen);
				UtilityMethods.Log($"[DivingEquipment - CONSUMED] -{amount} used. Current O2: {_currentOxygen:F1}");
				PlayOxygenConsumeFx((int)amount);
				if (_currentOxygen <= 0f)
				{
					OnOxygenDepleted();
				}
				if (withFeedback)
				{
					_player.Sway.PlayDamageHitFeedback();
					_player.DamageFeedback();
				}
				if (showExpandingPulseFeedback)
				{
					_player.RefreshExpandingPulseFeedback();
				}
			}
		}

		private void OnOxygenDepleted()
		{
			if (!_isKnocked)
			{
				UtilityMethods.Log("[DivingEquipment - DEPLETED] Entering knock state.");
				EventBus<PlayerDrownedEvent>.Raise(new PlayerDrownedEvent
				{
					_drownedPlayer = _player
				});
				_abyssDepthReportedThisDive = false;
				ServerReportDrownRpc();
				_player.Harpoon.Challenge_Fail();
				Singleton<PPG_WheelManager>.Instance.DeactiveWheel();
				_player.TabletController.CloseTablet();
				_isAccelerating = false;
				_isKnocked = true;
				AcquireKnockMovementPause();
				Singleton<GlobalVolumeManager>.Instance.EnterDivingKnock(GetDominantUnderwaterVolume());
				_player.PauseConstraints();
				_player.SetActionCamera(status: true);
				_player.SetSharkDragAnimator(isDragging: false);
				_player.AnimResetTrigger(PPG_Player.DROWN_END);
				_player.AnimSetTrigger(PPG_Player.DROWN);
				_player.PauseInputs();
				_player.PauseLook();
				_player.DisableActiveTool();
				_knockTimeLeft = Mathf.Max(1f, _knockDurationSeconds);
				_friendDistanceRefreshTimer = 0f;
				_divingEquipmentUI.ShowKnock();
				Singleton<OceanFXManager>.Instance.SetLocomotionState(OceanLocomotionState.Drown, 0f);
				Singleton<QuickSlotHandler>.Instance.DeselectSlot();
				Singleton<HintsManager>.Instance.ClearAllHints();
				PlayDrowningFx();
				_sillyPufferManager.PlayMovementPuffers();
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerReportDrownRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3873082017u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3873082017u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (base.IsServer && !(_player == null) && _player.OwnerClientId == rpcParams.Receive.SenderClientId)
			{
				_serverAbyssDepthReached = false;
				Singleton<GameProgressManager>.Instance?.RpcRelay?.ServerUnlockSteamAchievementForClient("ACH_FIRST_DROWN", _player.OwnerClientId);
				string text = _player.PlayerName.Value.ToString();
				if (string.IsNullOrWhiteSpace(text))
				{
					text = $"Client {rpcParams.Receive.SenderClientId}";
				}
				string text2 = ((_player.CurrentSeaLayer != null) ? _player.CurrentSeaLayer.DisplayName : "Unknown Sea Layer");
				string text3 = "tablet.map.habitat_name." + LocalizationDatabase.ToSnakeCase(text2);
				GlobalChatManager.BroadcastLocalizedInfoFromServer(this, "chat.info.player_drowned", "{0} drowned in {1}.", text, "@" + text3 + "|" + text2);
			}
		}

		public void ServerApplyOxygenFromPlayerTransfer(byte amount)
		{
			if (base.IsServer && amount != 0)
			{
				ClientApplyOxygenRpc(amount);
			}
		}

		public void ServerConsumeOxygenForPlayerTransfer(byte amount)
		{
			if (base.IsServer && amount != 0)
			{
				ClientConsumeOxygenRpc(amount, withFeedback: false, showExpandingPulseFeedback: false);
			}
		}

		[Rpc(SendTo.Owner)]
		private void ClientApplyOxygenRpc(byte amount)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3104615557u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in amount, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3104615557u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ApplyOxygen(amount);
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerConsumeOxygenRpc(byte amount, bool withFeedback = false, bool showExpandingPulseFeedback = false)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3105626484u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in amount, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in withFeedback, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in showExpandingPulseFeedback, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3105626484u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ClientConsumeOxygenRpc(amount, withFeedback, showExpandingPulseFeedback);
			}
		}

		[Rpc(SendTo.Owner)]
		private void ClientConsumeOxygenRpc(byte amount, bool withFeedback, bool showExpandingPulseFeedback)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1515749663u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in amount, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in withFeedback, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in showExpandingPulseFeedback, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1515749663u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ConsumeOxygen(amount, withFeedback, showExpandingPulseFeedback);
			}
		}

		public override void Interact(InputActionPhase phase)
		{
			switch (phase)
			{
			case InputActionPhase.Started:
				_isAccelerating = true;
				break;
			case InputActionPhase.Canceled:
				_isAccelerating = false;
				break;
			}
		}

		private void UpdateKnock()
		{
			if (!_isKnocked || _finishKnocked)
			{
				return;
			}
			if (!_isUnderwater)
			{
				CancelKnockAndRecover();
				return;
			}
			float num = (_isAccelerating ? _giveUpAcceleration : 1f);
			_knockTimeLeft -= Time.deltaTime * num;
			_knockTimeLeft = Mathf.Max(0f, _knockTimeLeft);
			_friendDistanceRefreshTimer -= Time.deltaTime;
			string friendMetersOrNull = null;
			if (_friendDistanceRefreshTimer <= 0f)
			{
				_friendDistanceRefreshTimer = 1f / Mathf.Max(0.1f, _friendDistanceRefreshHz);
				friendMetersOrNull = BuildFriendMeters();
			}
			float num2 = Mathf.Max(1f, _knockDurationSeconds);
			float num3 = _knockTimeLeft / num2;
			_divingEquipmentUI.UpdateKnock(num3, _isAccelerating, friendMetersOrNull);
			Singleton<GlobalVolumeManager>.Instance.TickDivingKnockGiveUp(GetDominantUnderwaterVolume(), num3);
			float drownIntensity = 1f - num3;
			Singleton<OceanFXManager>.Instance.SetDrownIntensity(drownIntensity);
			_player.EndEmote();
			_player.EndStaticEmote();
			if (_knockTimeLeft <= 0f)
			{
				FinishKnockAndTeleport();
			}
		}

		private string BuildFriendMeters()
		{
			IReadOnlyDictionary<ulong, PPG_Player> players = Singleton<PlayersManager>.Instance.Players;
			if (players == null)
			{
				return "";
			}
			StringBuilder stringBuilder = new StringBuilder();
			foreach (PPG_Player value in players.Values)
			{
				if (!(value == null) && !(value == _player))
				{
					float num = Mathf.Max(0f, Vector3.Distance(_player.transform.position, value.transform.position) - _friendAssistRange);
					string arg = value.PlayerName.Value.ToString() ?? "Friend";
					stringBuilder.AppendLine($"{arg}  {num:F1} m");
				}
			}
			return stringBuilder.ToString();
		}

		private Volume GetDominantUnderwaterVolume()
		{
			return Singleton<GameProgressManager>.Instance?.CozyWeatherManager?.GetDominantUnderwaterVolume();
		}

		private void FinishKnockAndTeleport()
		{
			if (_finishKnocked)
			{
				return;
			}
			UtilityMethods.Log("[DivingEquipment] FinishKnockAndTeleport");
			_player.Interactor.ResetEmptyAnims();
			_isAccelerating = false;
			Singleton<PPG_WheelManager>.Instance.DisableToolWheel = false;
			Singleton<PPG_WheelManager>.Instance.DisableEmoteWheel = false;
			UnlatchAllPiranhas();
			_finishKnocked = true;
			_divingEquipmentUI.HideKnock();
			SetFlashlight(value: false);
			bool num = Singleton<QuickSlotHandler>.Instance.DropAllItems(_player.transform.position);
			Vector3 pos = _player.transform.position;
			if (num)
			{
				Singleton<DelayHelper>.Instance.DelayAction(1f, () =>
				{
					UnityEngine.Object.Instantiate(_deadIconPrefab, pos, Quaternion.identity);
				});
			}
			Singleton<DelayHelper>.Instance.DelayAction(0.05f, () =>
			{
				_player.FirstPersonCharacterInput.OutUnderWater();
				_isKnocked = false;
				ReleaseKnockMovementPause();
				_player.ResumeInputs();
				_player.ResumeLook();
				Transform transform = ResolveKnockRespawnPoint();
				if (transform != Singleton<PlayersManager>.Instance.PlayerSpawnPoint)
				{
					CrabBossRoomTrigger.ClearLocalBossUiForRespawn();
				}
				_player.TeleportPlayer(transform.position, updateGround: false);
				RefillOxygenFull();
				Singleton<GlobalVolumeManager>.Instance.ExitDivingKnock();
				_sillyPufferManager.ResetPuffers();
				_finishKnocked = false;
				ServerRecordDailyDrownRpc();
			});
		}

		private static Transform ResolveKnockRespawnPoint()
		{
			if (CrabBossRoomTrigger.TryGetLocalBossRespawnPoint(out var respawnPoint))
			{
				return respawnPoint;
			}
			return Singleton<PlayersManager>.Instance.PlayerSpawnPoint;
		}

		[Rpc(SendTo.Server)]
		private void ServerRecordDailyDrownRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3801812258u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3801812258u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsServer && !(_player == null) && _player.OwnerClientId == rpcParams.Receive.SenderClientId && ServiceLocator.ForSceneOf(this).TryGet<DailyReportBroadcaster>(out var service) && service != null)
				{
					service.ServerRecordDrown(_player.PlayerName.Value, _player.SteamID.Value);
				}
			}
		}

		public void CancelKnockAndRecover(float refillRatio = 0.25f)
		{
			if (_isKnocked)
			{
				UnlatchAllPiranhas();
				Singleton<PPG_WheelManager>.Instance.DisableToolWheel = false;
				Singleton<PPG_WheelManager>.Instance.DisableEmoteWheel = false;
				_player.AnimSetTrigger(PPG_Player.DROWN_END);
				_player.SetActionCamera(status: false);
				Singleton<OceanFXManager>.Instance.SetLocomotionState(OceanLocomotionState.Idle, 0f);
				_isKnocked = false;
				_divingEquipmentUI.HideKnock();
				ReleaseKnockMovementPause();
				_player.TryRestoreCharacterControlAfterModeExit();
				UtilityMethods.Log("[O₂ RECOVER] Knock canceled, player recovered.");
				_sillyPufferManager.ResetPuffers();
				_currentOxygen = Mathf.Max(_currentOxygen, _maxOxygen * Mathf.Clamp01(refillRatio));
				_divingEquipmentUI.SetOxygenLevel(_currentOxygen / _maxOxygen);
				Singleton<GlobalVolumeManager>.Instance.ExitDivingKnock();
			}
		}

		private void AcquireKnockMovementPause()
		{
			if (!_ownsKnockMovementPause)
			{
				_player.PauseMovement();
				_ownsKnockMovementPause = true;
			}
		}

		private void ReleaseKnockMovementPause()
		{
			if (_ownsKnockMovementPause)
			{
				_ownsKnockMovementPause = false;
				_player.ResumeMovement();
			}
		}

		private void PlayDrowningFx()
		{
			if (_audioSource != null && _drowningSFX != null)
			{
				_audioSource.pitch = 1f;
				_audioSource.PlayOneShot(_drowningSFX, 1f);
			}
			if (_drowningVFX != null)
			{
				_drowningVFX.Play();
			}
		}

		private void PlayOxygenGainFx(float amount)
		{
			if (base.IsOwner)
			{
				float num = Mathf.Max(1f, _maxOxygen);
				float num2 = num * 0.05f;
				float num3 = num * 0.75f;
				float t = NormalizeAmount(amount, num2, num3);
				float num4 = Mathf.Lerp(0.4f, 1f, t);
				float volumeScale = _gainBaseVolume * num4;
				float value = Mathf.Lerp(0.95f, 1.12f, t);
				value = AddJitter(value, 0.03f);
				if (_audioSource != null && _gainSFX != null)
				{
					_audioSource.pitch = _gainBasePitch * value;
					_audioSource.PlayOneShot(_gainSFX, volumeScale);
				}
				if (_gainVFX != null)
				{
					float minAmount = num2;
					float maxAmount = num3;
					int particleCount = ComputeParticleCount(amount, minAmount, maxAmount, 6, 30);
					SetBurstCountAndPlay(_gainVFX, particleCount);
				}
			}
		}

		private void PlayOxygenConsumeFx(float amount)
		{
			if (base.IsOwner)
			{
				float num = Mathf.Max(1f, _maxOxygen);
				float minAmount = num * 0.05f;
				float maxAmount = num * 0.5f;
				float t = NormalizeAmount(amount, minAmount, maxAmount);
				float num2 = Mathf.Lerp(0.35f, 0.9f, t);
				float volumeScale = _consumeBaseVolume * num2;
				float value = Mathf.Lerp(1.05f, 0.85f, t);
				value = AddJitter(value, 0.02f);
				if (_audioSource != null && _consumeSFX != null)
				{
					_audioSource.pitch = _consumeBasePitch * value;
					_audioSource.PlayOneShot(_consumeSFX, volumeScale);
				}
				if (_consumeVFX != null)
				{
					int particleCount = ComputeParticleCount(amount, minAmount, maxAmount, 4, 24);
					SetBurstCountAndPlay(_consumeVFX, particleCount);
				}
			}
		}

		private int ComputeParticleCount(float amount, float minAmount, float maxAmount, int minParticles, int maxParticles)
		{
			if (maxAmount <= minAmount)
			{
				return minParticles;
			}
			float value = Mathf.InverseLerp(minAmount, maxAmount, amount);
			value = Mathf.Clamp01(value);
			return Mathf.RoundToInt(Mathf.Lerp(minParticles, maxParticles, value));
		}

		private void SetBurstCountAndPlay(ParticleSystem ps, int particleCount)
		{
			if (!(ps == null))
			{
				ParticleSystem.EmissionModule emission = ps.emission;
				if (emission.burstCount > 0)
				{
					ParticleSystem.Burst burst = emission.GetBurst(0);
					burst.count = new ParticleSystem.MinMaxCurve(particleCount);
					emission.SetBurst(0, burst);
				}
				ps.Play();
			}
		}

		private float NormalizeAmount(float amount, float minAmount, float maxAmount)
		{
			if (maxAmount <= minAmount)
			{
				return 0f;
			}
			return Mathf.Clamp01(Mathf.InverseLerp(minAmount, maxAmount, amount));
		}

		private float AddJitter(float value, float jitterRange)
		{
			if (jitterRange <= 0f)
			{
				return value;
			}
			return value + UnityEngine.Random.Range(0f - jitterRange, jitterRange);
		}

		public void SetDebugPreventOxygenLoss(bool prevent)
		{
			_o2PreventLoss = prevent;
			PreventOxygenLoss = prevent;
			UtilityMethods.Log($"<b><color=#55DDFF>[Diving DEBUG]</color></b> PreventOxygenLoss set to <b>{prevent}</b>");
		}

		public void UnlatchPiranha(int index = -1)
		{
			if (base.IsServer)
			{
				ServerRequestUnlatchPiranha(index);
			}
			else
			{
				ServerRequestUnlatchPiranhaRpc(index);
			}
		}

		public void UnlatchAllPiranhas()
		{
			if (base.IsServer)
			{
				ServerRequestUnlatchAllPiranhas();
			}
			else
			{
				ServerRequestUnlatchAllPiranhasRpc();
			}
		}

		public void ResetLatchedPiranhasLocal(bool playRemoveSfx)
		{
			CacheLatchPiranhas();
			_latchedPiranhaIndexes.Clear();
			if (_latchPiranhas != null)
			{
				for (int i = 0; i < _latchPiranhas.Count; i++)
				{
					LatchPiranha latchPiranha = _latchPiranhas[i];
					if (!(latchPiranha == null))
					{
						bool isLatched = latchPiranha.IsLatched;
						latchPiranha.SetVisualStatus(status: false);
						if (playRemoveSfx & isLatched)
						{
							latchPiranha.PlayRemoveSfxLocal();
						}
					}
				}
			}
			SetLatchCountLocal(0);
		}

		public void LatchPiranha()
		{
			if (base.IsServer)
			{
				ServerRequestLatchPiranha();
			}
			else
			{
				ServerRequestLatchPiranhaRpc();
			}
		}

		[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
		private void ServerRequestUnlatchPiranhaRpc(int index)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
				{
					InvokePermission = RpcInvokePermission.Everyone
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(4151667763u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, index);
				__endSendRpc(ref bufferWriter, 4151667763u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ServerRequestUnlatchPiranha(index);
			}
		}

		private void ServerRequestUnlatchPiranha(int index)
		{
			CacheLatchPiranhas();
			if (!TryGetLatchedPiranhaIndex(index, out var resolvedIndex))
			{
				UtilityMethods.Log($"[DivingEquipment] ServerRequestUnlatchPiranhaRpc failed. RequestedIndex: {index}", LogType.Warning);
			}
			else
			{
				ServerSetPiranhaLatched(resolvedIndex, isLatched: false);
			}
		}

		[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
		private void ServerRequestUnlatchAllPiranhasRpc()
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
				{
					InvokePermission = RpcInvokePermission.Everyone
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(1984665354u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1984665354u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ServerRequestUnlatchAllPiranhas();
			}
		}

		private void ServerRequestUnlatchAllPiranhas()
		{
			CacheLatchPiranhas();
			if (_latchPiranhas == null || _latchPiranhas.Count == 0)
			{
				return;
			}
			PruneInvalidLatchedPiranhaIndexes();
			if (_latchedPiranhaIndexes.Count == 0)
			{
				ResetLatchedPiranhasLocal(playRemoveSfx: false);
				ClientResetLatchedPiranhasRpc(playRemoveSfx: false);
				SyncLatchCountFromCurrentState();
				return;
			}
			List<int> list = new List<int>(_latchedPiranhaIndexes);
			bool flag = false;
			for (int i = 0; i < list.Count; i++)
			{
				int num = list[i];
				if (!TryGetPiranhaByIndex(num, out var result))
				{
					_latchedPiranhaIndexes.Remove(num);
					continue;
				}
				SetPiranhaLatchStateLocal(result.Index, isLatched: false);
				ApplyPiranhaVisualStateLocal(result.Index, isLatched: false);
				ClientSetPiranhaLatchedRpc((byte)result.Index, isLatched: false);
				flag = true;
			}
			SyncLatchCountFromCurrentState();
		}

		[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
		private void ServerRequestLatchPiranhaRpc()
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
				{
					InvokePermission = RpcInvokePermission.Everyone
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(4261432796u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 4261432796u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ServerRequestLatchPiranha();
			}
		}

		private void ServerRequestLatchPiranha()
		{
			CacheLatchPiranhas();
			if (!TryGetAvailablePiranhaIndex(out var resolvedIndex))
			{
				UtilityMethods.Log("[DivingEquipment] ServerRequestLatchPiranhaRpc failed. No available piranha.", LogType.Warning);
			}
			else
			{
				ServerSetPiranhaLatched(resolvedIndex, isLatched: true);
			}
		}

		private void ServerSetPiranhaLatched(int index, bool isLatched)
		{
			if (TryGetPiranhaByIndex(index, out var _))
			{
				SetPiranhaLatchStateLocal(index, isLatched);
				ApplyPiranhaVisualStateLocal(index, isLatched);
				ClientSetPiranhaLatchedRpc((byte)index, isLatched);
				SyncLatchCountFromCurrentState();
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientResetLatchedPiranhasRpc(bool playRemoveSfx)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2689896931u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in playRemoveSfx, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2689896931u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ResetLatchedPiranhasLocal(playRemoveSfx);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientSetPiranhaLatchedRpc(byte index, bool isLatched)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2373532477u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in index, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in isLatched, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2373532477u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			SetPiranhaLatchStateLocal(index, isLatched);
			if (TryGetPiranhaByIndex(index, out var result))
			{
				result.SetVisualStatus(isLatched);
				if (!isLatched)
				{
					result.PlayRemoveSfxLocal();
				}
				if (base.IsOwner)
				{
					SetLatchCountLocal(CountLatchedPiranhas());
				}
			}
		}

		[Rpc(SendTo.Owner)]
		private void OwnerSyncLatchCountRpc(byte count)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3640211389u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in count, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3640211389u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				SetLatchCountLocal(count);
			}
		}

		private void SyncLatchCountFromCurrentState()
		{
			int num = CountLatchedPiranhas();
			SetLatchCountLocal(num);
			OwnerSyncLatchCountRpc((byte)num);
		}

		private void SetLatchCountLocal(int count)
		{
			_latchPiranhaCount = Mathf.Clamp(count, 0, 5);
			if (base.IsOwner)
			{
				UpdateLockedAmount(_latchPiranhaCount);
			}
		}

		private void ApplyPiranhaVisualStateLocal(int index, bool isLatched)
		{
			if (TryGetPiranhaByIndex(index, out var result))
			{
				result.SetVisualStatus(isLatched);
			}
		}

		private bool TryGetLatchedPiranhaIndex(int requestedIndex, out int resolvedIndex)
		{
			resolvedIndex = -1;
			if (_latchPiranhas == null || _latchPiranhas.Count == 0)
			{
				return false;
			}
			PruneInvalidLatchedPiranhaIndexes();
			if (requestedIndex >= 0)
			{
				if (!TryGetPiranhaByIndex(requestedIndex, out var result))
				{
					return false;
				}
				if (!IsPiranhaIndexLatched(result.Index))
				{
					return false;
				}
				resolvedIndex = result.Index;
				return true;
			}
			List<int> list = new List<int>(_latchedPiranhaIndexes);
			if (list.Count == 0)
			{
				return false;
			}
			resolvedIndex = list[UnityEngine.Random.Range(0, list.Count)];
			return true;
		}

		private bool TryGetAvailablePiranhaIndex(out int resolvedIndex)
		{
			resolvedIndex = -1;
			if (_latchPiranhas == null || _latchPiranhas.Count == 0)
			{
				return false;
			}
			PruneInvalidLatchedPiranhaIndexes();
			if (_latchedPiranhaIndexes.Count >= 5)
			{
				return false;
			}
			List<int> list = new List<int>();
			for (int i = 0; i < _latchPiranhas.Count; i++)
			{
				LatchPiranha latchPiranha = _latchPiranhas[i];
				if (!(latchPiranha == null) && !IsPiranhaIndexLatched(latchPiranha.Index))
				{
					list.Add(latchPiranha.Index);
				}
			}
			if (list.Count == 0)
			{
				return false;
			}
			resolvedIndex = list[UnityEngine.Random.Range(0, list.Count)];
			return true;
		}

		private bool TryGetPiranhaByIndex(int index, out LatchPiranha result)
		{
			result = null;
			CacheLatchPiranhas();
			if (_latchPiranhas == null)
			{
				return false;
			}
			for (int i = 0; i < _latchPiranhas.Count; i++)
			{
				LatchPiranha latchPiranha = _latchPiranhas[i];
				if (!(latchPiranha == null) && latchPiranha.Index == index)
				{
					result = latchPiranha;
					return true;
				}
			}
			return false;
		}

		private void CacheLatchPiranhas()
		{
			if (_latchPiranhas == null)
			{
				_latchPiranhas = new List<LatchPiranha>();
			}
			for (int num = _latchPiranhas.Count - 1; num >= 0; num--)
			{
				if (_latchPiranhas[num] == null)
				{
					_latchPiranhas.RemoveAt(num);
				}
			}
			if (_latchPiranhas.Count >= 5)
			{
				BindAndSortCachedLatchPiranhas();
				_latchPiranhasCached = true;
			}
			else
			{
				if (_latchPiranhasCached && _latchPiranhas.Count > 0)
				{
					return;
				}
				LatchPiranha[] array = null;
				if (_player != null)
				{
					array = _player.GetComponentsInChildren<LatchPiranha>(includeInactive: true);
				}
				if ((array == null || array.Length == 0) && base.transform.root != null)
				{
					array = base.transform.root.GetComponentsInChildren<LatchPiranha>(includeInactive: true);
				}
				if (array == null || array.Length == 0)
				{
					array = GetComponentsInChildren<LatchPiranha>(includeInactive: true);
				}
				if (array != null)
				{
					foreach (LatchPiranha latchPiranha in array)
					{
						if (!(latchPiranha == null))
						{
							latchPiranha.BindDivingEquipment(this);
							if (!_latchPiranhas.Contains(latchPiranha))
							{
								_latchPiranhas.Add(latchPiranha);
							}
						}
					}
				}
				BindAndSortCachedLatchPiranhas();
				_latchPiranhasCached = _latchPiranhas.Count > 0;
			}
		}

		private void BindAndSortCachedLatchPiranhas()
		{
			for (int i = 0; i < _latchPiranhas.Count; i++)
			{
				LatchPiranha latchPiranha = _latchPiranhas[i];
				if (!(latchPiranha == null))
				{
					latchPiranha.BindDivingEquipment(this);
				}
			}
			_latchPiranhas.Sort((LatchPiranha a, LatchPiranha b) => a.Index.CompareTo(b.Index));
		}

		private int CountLatchedPiranhas()
		{
			PruneInvalidLatchedPiranhaIndexes();
			return Mathf.Min(_latchedPiranhaIndexes.Count, 5);
		}

		private bool IsPiranhaIndexLatched(int index)
		{
			return _latchedPiranhaIndexes.Contains(index);
		}

		private void SetPiranhaLatchStateLocal(int index, bool isLatched)
		{
			if (isLatched)
			{
				_latchedPiranhaIndexes.Add(index);
			}
			else
			{
				_latchedPiranhaIndexes.Remove(index);
			}
		}

		private void PruneInvalidLatchedPiranhaIndexes()
		{
			if (_latchedPiranhaIndexes.Count == 0)
			{
				return;
			}
			if (_latchPiranhas == null || _latchPiranhas.Count == 0)
			{
				_latchedPiranhaIndexes.Clear();
				return;
			}
			List<int> list = null;
			foreach (int latchedPiranhaIndex in _latchedPiranhaIndexes)
			{
				if (!TryGetPiranhaByIndex(latchedPiranhaIndex, out var _))
				{
					if (list == null)
					{
						list = new List<int>();
					}
					list.Add(latchedPiranhaIndex);
				}
			}
			if (list != null)
			{
				for (int i = 0; i < list.Count; i++)
				{
					_latchedPiranhaIndexes.Remove(list[i]);
				}
			}
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(1487025329u, __rpc_handler_1487025329, "ServerReportAbyssDepthReachedRpc", RpcInvokePermission.Everyone);
			__registerRpc(2810751191u, __rpc_handler_2810751191, "ServerReportAbyssSurfaceRpc", RpcInvokePermission.Everyone);
			__registerRpc(1909868834u, __rpc_handler_1909868834, "ServerRecordDailyDiveDepthRpc", RpcInvokePermission.Everyone);
			__registerRpc(3873082017u, __rpc_handler_3873082017, "ServerReportDrownRpc", RpcInvokePermission.Everyone);
			__registerRpc(3104615557u, __rpc_handler_3104615557, "ClientApplyOxygenRpc", RpcInvokePermission.Everyone);
			__registerRpc(3105626484u, __rpc_handler_3105626484, "ServerConsumeOxygenRpc", RpcInvokePermission.Everyone);
			__registerRpc(1515749663u, __rpc_handler_1515749663, "ClientConsumeOxygenRpc", RpcInvokePermission.Everyone);
			__registerRpc(3801812258u, __rpc_handler_3801812258, "ServerRecordDailyDrownRpc", RpcInvokePermission.Everyone);
			__registerRpc(4151667763u, __rpc_handler_4151667763, "ServerRequestUnlatchPiranhaRpc", RpcInvokePermission.Everyone);
			__registerRpc(1984665354u, __rpc_handler_1984665354, "ServerRequestUnlatchAllPiranhasRpc", RpcInvokePermission.Everyone);
			__registerRpc(4261432796u, __rpc_handler_4261432796, "ServerRequestLatchPiranhaRpc", RpcInvokePermission.Everyone);
			__registerRpc(2689896931u, __rpc_handler_2689896931, "ClientResetLatchedPiranhasRpc", RpcInvokePermission.Everyone);
			__registerRpc(2373532477u, __rpc_handler_2373532477, "ClientSetPiranhaLatchedRpc", RpcInvokePermission.Everyone);
			__registerRpc(3640211389u, __rpc_handler_3640211389, "OwnerSyncLatchCountRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_1487025329(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out float value, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((DivingEquipment)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((DivingEquipment)target).ServerReportAbyssDepthReachedRpc(value, ext);
				((DivingEquipment)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2810751191(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((DivingEquipment)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((DivingEquipment)target).ServerReportAbyssSurfaceRpc(ext);
				((DivingEquipment)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1909868834(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out float value, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((DivingEquipment)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((DivingEquipment)target).ServerRecordDailyDiveDepthRpc(value, ext);
				((DivingEquipment)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3873082017(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((DivingEquipment)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((DivingEquipment)target).ServerReportDrownRpc(ext);
				((DivingEquipment)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3104615557(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				((DivingEquipment)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((DivingEquipment)target).ClientApplyOxygenRpc(value);
				((DivingEquipment)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3105626484(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out bool value2, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out bool value3, default(FastBufferWriter.ForPrimitives));
				((DivingEquipment)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((DivingEquipment)target).ServerConsumeOxygenRpc(value, value2, value3);
				((DivingEquipment)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1515749663(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out bool value2, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out bool value3, default(FastBufferWriter.ForPrimitives));
				((DivingEquipment)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((DivingEquipment)target).ClientConsumeOxygenRpc(value, value2, value3);
				((DivingEquipment)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3801812258(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((DivingEquipment)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((DivingEquipment)target).ServerRecordDailyDrownRpc(ext);
				((DivingEquipment)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4151667763(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				((DivingEquipment)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((DivingEquipment)target).ServerRequestUnlatchPiranhaRpc(value);
				((DivingEquipment)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1984665354(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((DivingEquipment)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((DivingEquipment)target).ServerRequestUnlatchAllPiranhasRpc();
				((DivingEquipment)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4261432796(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((DivingEquipment)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((DivingEquipment)target).ServerRequestLatchPiranhaRpc();
				((DivingEquipment)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2689896931(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				((DivingEquipment)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((DivingEquipment)target).ClientResetLatchedPiranhasRpc(value);
				((DivingEquipment)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2373532477(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out bool value2, default(FastBufferWriter.ForPrimitives));
				((DivingEquipment)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((DivingEquipment)target).ClientSetPiranhaLatchedRpc(value, value2);
				((DivingEquipment)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3640211389(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				((DivingEquipment)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((DivingEquipment)target).OwnerSyncLatchCountRpc(value);
				((DivingEquipment)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "DivingEquipment";
		}
	}
}
