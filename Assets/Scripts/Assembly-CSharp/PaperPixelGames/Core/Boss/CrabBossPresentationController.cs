using System;
using System.Collections;
using System.Collections.Generic;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using PixPlays.ElementalVFX;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Boss
{
	[RequireComponent(typeof(CrabBossController))]
	public sealed class CrabBossPresentationController : MonoBehaviour
	{
		[Header("Animator")]
		[SerializeField]
		private Animator _animator;

		[SerializeField]
		private string _idleStateName;

		[SerializeField]
		private string _phase1IdleStateName;

		[SerializeField]
		private string _phase2IdleStateName;

		[SerializeField]
		private string _phase3IdleStateName;

		[SerializeField]
		private string _sleepStateName;

		[SerializeField]
		private CrabBossAnimatorTrigger _wakeTriggerName;

		[SerializeField]
		private CrabBossAnimatorTrigger _shieldHitTriggerName;

		[SerializeField]
		private CrabBossAnimatorTrigger _bodyHitTriggerName;

		[SerializeField]
		private CrabBossAnimatorTrigger _shieldBreakTriggerName;

		[SerializeField]
		private CrabBossAnimatorTrigger _phaseTransitionTriggerName;

		[SerializeField]
		private CrabBossAnimatorTrigger _defeatedTriggerName;

		[SerializeField]
		private float _crossFadeDuration;

		[Header("Boss UI")]
		[SerializeField]
		private GameObject _uiRoot;

		[SerializeField]
		private GameObject _healthBarRoot;

		[SerializeField]
		private Image _healthFill;

		[SerializeField]
		private Image _healthPositiveFill;

		[SerializeField]
		private Image _healthNegativeFill;

		[SerializeField]
		private RectTransform _phase1HealthDivider;

		[SerializeField]
		private RectTransform _phase2HealthDivider;

		[SerializeField]
		private GameObject _shieldBarRoot;

		[SerializeField]
		private Image _shieldFill;

		[SerializeField]
		private Image _shieldPositiveFill;

		[SerializeField]
		private Image _shieldNegativeFill;

		[SerializeField]
		[Min(0f)]
		private float _barFeedbackLerpSpeed = 6f;

		[Header("Boss Audio")]
		[Tooltip("Boss sunumundaki tum AudioClip'ler bu tek AudioSource uzerinden PlayOneShot ile oynatilir.")]
		[SerializeField]
		private AudioSource _audioSource;

		[Header("Boss Idle SFX")]
		[Tooltip("Combat devam ederken, saldiri aralarinda boss AudioSource'undan rastgele oynatilir.")]
		[SerializeField]
		private AudioClip[] _idleSfx;

		[SerializeField]
		[Min(0.1f)]
		private float _idleSfxMinimumInterval = 5f;

		[SerializeField]
		[Min(0.1f)]
		private float _idleSfxMaximumInterval = 6f;

		[SerializeField]
		[Range(0f, 1f)]
		private float _idleSfxVolume = 0.7f;

		[Header("Boss Music")]
		[Tooltip("Normal sualti ambience'i bu sisteme dahil degildir. Oyuncu boss odasindayken phase'e gore bu muzik loop'lari crossfade ile calar.")]
		[SerializeField]
		private AudioClip _phase1MusicLoop;

		[SerializeField]
		private AudioClip _phase2MusicLoop;

		[SerializeField]
		private AudioClip _phase3MusicLoop;

		[SerializeField]
		[Range(0f, 1f)]
		private float _bossMusicVolume = 0.35f;

		[SerializeField]
		[Min(0.01f)]
		private float _musicCrossfadeDuration = 1.5f;

		[Header("Body Hit")]
		[SerializeField]
		private ParticleSystem _bodyHitVfx;

		[SerializeField]
		private AudioClip _bodyHitSfx;

		[Header("Creature Spawn Roar Animation Event")]
		[SerializeField]
		private GameObject _creatureSpawnRoarVisualRoot;

		[SerializeField]
		private AudioClip _creatureSpawnRoarSfx;

		[SerializeField]
		[Min(0.01f)]
		private float _creatureSpawnRoarVisualDuration = 2f;

		[SerializeField]
		private bool _creatureSpawnRoarCameraShake = true;

		[SerializeField]
		private ShakeIntensity _creatureSpawnRoarShakeIntensity;

		[SerializeField]
		private ShakeDuration _creatureSpawnRoarShakeDuration;

		[Header("ProjectTile Launch Animation Event")]
		[SerializeField]
		private GameObject _projectTileLaunchVisualRoot;

		[SerializeField]
		private AudioClip _projectTileLaunchRockSfx;

		[SerializeField]
		[Range(0f, 1f)]
		private float _projectTileLaunchRockSfxVolume = 0.75f;

		[SerializeField]
		private AudioClip _projectTileLaunchWhooshSfx;

		[SerializeField]
		[Range(0f, 1f)]
		private float _projectTileLaunchWhooshSfxVolume = 0.55f;

		[SerializeField]
		[Min(0.01f)]
		private float _projectTileLaunchVisualDuration = 1f;

		[Header("Grab Throw Animation Events")]
		[SerializeField]
		private AudioClip _grabPlayerSfx;

		[SerializeField]
		[Range(0f, 1f)]
		private float _grabPlayerSfxVolume = 0.75f;

		[SerializeField]
		private AudioClip _throwPlayerSfx;

		[SerializeField]
		[Range(0f, 1f)]
		private float _throwPlayerSfxVolume = 0.65f;

		[Header("Shield Hit")]
		[SerializeField]
		private ParticleSystem _shieldHitVfx;

		[SerializeField]
		private AudioClip _shieldHitSfx;

		[Header("Shield Break")]
		[SerializeField]
		private ParticleSystem _shieldBreakVfx;

		[SerializeField]
		private AudioClip _shieldBreakSfx;

		[Header("Phase Materials")]
		[Tooltip("Boş bırakılırsa Animator altındaki tüm SkinnedMeshRenderer bileşenleri otomatik bulunur.")]
		[SerializeField]
		private Renderer[] _phaseMaterialRenderers;

		[SerializeField]
		private Material _phase1ShieldMaterial;

		[SerializeField]
		private Material _phase1VulnerableMaterial;

		[SerializeField]
		private Material _phase2ShieldMaterial;

		[SerializeField]
		private Material _phase2VulnerableMaterial;

		[SerializeField]
		private Material _phase3ShieldMaterial;

		[SerializeField]
		private Material _phase3VulnerableMaterial;

		[Header("Phase Transition")]
		[SerializeField]
		private GameObject _phaseTransitionVisualRoot;

		[SerializeField]
		[Min(0.01f)]
		private float _phaseTransitionVisualDuration = 3f;

		[SerializeField]
		private EarthShield _phaseTransitionEarthShieldPrefab;

		[SerializeField]
		private Transform _phaseTransitionEarthShieldParent;

		[SerializeField]
		private AudioClip _phaseTransitionSfx;

		[Header("Rock Rain")]
		[SerializeField]
		private ParticleSystem _rockRainLandingVfx;

		[Header("Stalactite Drop Animation Event")]
		[SerializeField]
		private GameObject _stalactiteDropLandingVisualRoot;

		[SerializeField]
		private Transform _stalactiteDropLandingAnchor;

		[SerializeField]
		private Vector3 _stalactiteDropLandingLocalPosition;

		[SerializeField]
		private Vector3 _stalactiteDropLandingLocalEulerAngles;

		[SerializeField]
		private AudioClip _stalactiteDropLandingSfx;

		[SerializeField]
		[Min(0.01f)]
		private float _stalactiteDropLandingVisualDuration = 2f;

		[SerializeField]
		private bool _stalactiteDropLandingCameraShake = true;

		[SerializeField]
		private ShakeIntensity _stalactiteDropLandingShakeIntensity = ShakeIntensity.Medium;

		[SerializeField]
		private ShakeDuration _stalactiteDropLandingShakeDuration = ShakeDuration.Mid;

		[Header("Rock Impact")]
		[SerializeField]
		private AudioClip _rockImpactSfx;

		[SerializeField]
		[Range(0f, 1f)]
		private float _rockImpactSfxVolume = 0.75f;

		[Header("Defeated")]
		[SerializeField]
		private ParticleSystem _defeatedVfx;

		[SerializeField]
		private AudioClip _defeatedSfx;

		[SerializeField]
		[Min(0f)]
		private float _dissolveDelay = 0.5f;

		[SerializeField]
		[Min(0.01f)]
		private float _dissolveDuration = 2f;

		[SerializeField]
		[Range(0.001f, 0.25f)]
		private float _dissolveEdgeWidth = 0.08f;

		[SerializeField]
		private Color _dissolveEdgeColor = new Color(0.1f, 1.2f, 2.5f, 1f);

		[SerializeField]
		[Min(0.05f)]
		private float _dissolveNoiseScale = 0.65f;

		[Header("Debug")]
		[SerializeField]
		private bool _log;

		private CrabBossController _boss;

		private CrabBossEncounterState _previousState;

		private float _previousHealth;

		private float _previousShieldHealth;

		private bool _previousIdle;

		private bool _isLocalPlayerInRoom;

		private bool _uiDirty;

		private bool _initialized;

		private bool _healthBarInitialized;

		private bool _shieldBarInitialized;

		private readonly List<Renderer> _dissolveRenderers = new List<Renderer>();

		private readonly List<Material[]> _originalRendererMaterials = new List<Material[]>();

		private readonly List<bool> _originalRendererEnabledStates = new List<bool>();

		private readonly List<Material> _runtimeDissolveMaterials = new List<Material>();

		private Coroutine _dissolveCoroutine;

		private Coroutine _creatureSpawnRoarVisualCoroutine;

		private Coroutine _projectTileLaunchVisualCoroutine;

		private Coroutine _phaseTransitionVisualCoroutine;

		private EarthShield _phaseTransitionEarthShieldInstance;

		private Coroutine _stalactiteDropLandingVisualCoroutine;

		private Transform _stalactiteDropLandingStableParent;

		private Renderer[] _resolvedPhaseMaterialRenderers;

		private bool _dissolvePrepared;

		private bool _animatorWasActiveBeforeDissolve;

		private bool _animatorWasEnabledBeforeDissolve;

		private AudioSource _musicSourceA;

		private AudioSource _musicSourceB;

		private AudioSource _activeMusicSource;

		private float _nextIdleSfxTime;

		private int _lastIdleSfxIndex = -1;

		[Header("Editor Real Damage Test")]
		[SerializeField]
		[Min(0.01f)]
		private float _editorTestHitMultiplier = 1f;

		private void Awake()
		{
			_boss = GetComponent<CrabBossController>();
			if (_animator != null)
			{
				_animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
			}
			if (_audioSource == null)
			{
				_audioSource = GetComponent<AudioSource>();
			}
			if (_audioSource != null)
			{
				_audioSource.spatialBlend = 1f;
			}
			if (_stalactiteDropLandingVisualRoot != null)
			{
				_stalactiteDropLandingStableParent = _stalactiteDropLandingVisualRoot.transform.parent;
			}
			CreateMusicSourcesIfNeeded();
			if (_boss == null)
			{
				UtilityMethods.Log("[CrabBoss][Presentation] CrabBossController is missing.", LogType.Error);
				base.enabled = false;
			}
		}

		private void OnEnable()
		{
			_initialized = false;
			_healthBarInitialized = false;
			_shieldBarInitialized = false;
			_uiDirty = true;
			SetBossUiVisible(visible: false);
			SetActiveIfNeeded(_creatureSpawnRoarVisualRoot, active: false);
			SetActiveIfNeeded(_projectTileLaunchVisualRoot, active: false);
			SetActiveIfNeeded(_phaseTransitionVisualRoot, active: false);
			SetActiveIfNeeded(_stalactiteDropLandingVisualRoot, active: false);
			ResetDissolvePresentation();
			StopBossAudioLoops();
		}

		private void OnDisable()
		{
			StopBossAudioLoops();
			if (_audioSource != null)
			{
				_audioSource.Stop();
			}
			SetBossUiVisible(visible: false);
			StopTimedVisualRoot(_creatureSpawnRoarVisualRoot, ref _creatureSpawnRoarVisualCoroutine);
			StopTimedVisualRoot(_projectTileLaunchVisualRoot, ref _projectTileLaunchVisualCoroutine);
			StopTimedVisualRoot(_phaseTransitionVisualRoot, ref _phaseTransitionVisualCoroutine);
			StopTimedVisualRoot(_stalactiteDropLandingVisualRoot, ref _stalactiteDropLandingVisualCoroutine);
			StopPhaseTransitionEarthShield();
			ResetDissolvePresentation();
		}

		private void OnDestroy()
		{
			if (_creatureSpawnRoarVisualCoroutine != null)
			{
				StopCoroutine(_creatureSpawnRoarVisualCoroutine);
			}
			if (_projectTileLaunchVisualCoroutine != null)
			{
				StopCoroutine(_projectTileLaunchVisualCoroutine);
			}
			if (_phaseTransitionVisualCoroutine != null)
			{
				StopCoroutine(_phaseTransitionVisualCoroutine);
			}
			if (_stalactiteDropLandingVisualCoroutine != null)
			{
				StopCoroutine(_stalactiteDropLandingVisualCoroutine);
			}
			DisposeDissolveMaterials();
		}

		private void Update()
		{
			if (_boss == null || !_boss.IsSpawned)
			{
				return;
			}
			if (!_initialized)
			{
				CacheCurrentState();
				RefreshUi();
				ApplyCurrentAnimationState();
				_initialized = true;
				return;
			}
			ProcessStateChange();
			ProcessIdleChange();
			ProcessShieldChange();
			ProcessHealthChange();
			UpdateBossAudio();
			if (_uiDirty || _boss.EncounterState != _previousState || !Mathf.Approximately(_boss.CurrentHealth, _previousHealth) || !Mathf.Approximately(_boss.CurrentShieldHealth, _previousShieldHealth) || HasPendingBarFeedback())
			{
				RefreshUi();
			}
			CacheCurrentState();
		}

		private void ProcessStateChange()
		{
			CrabBossEncounterState encounterState = _boss.EncounterState;
			if (encounterState == _previousState)
			{
				return;
			}
			if (_previousState == CrabBossEncounterState.Defeated)
			{
				ResetDissolvePresentation();
			}
			ApplyCurrentPhaseMaterial();
			if (_previousState == CrabBossEncounterState.PhaseTransition && encounterState != CrabBossEncounterState.PhaseTransition)
			{
				StopTimedVisualRoot(_phaseTransitionVisualRoot, ref _phaseTransitionVisualCoroutine);
				StopPhaseTransitionEarthShield();
			}
			if (encounterState == CrabBossEncounterState.Sleeping)
			{
				CrossFadeState(_sleepStateName);
				return;
			}
			if (_previousState == CrabBossEncounterState.Sleeping)
			{
				PlayAnimatorTrigger(_wakeTriggerName);
			}
			switch (encounterState)
			{
			case CrabBossEncounterState.PhaseTransition:
				PlayAnimatorTrigger(_phaseTransitionTriggerName);
				PlayTimedVisualRoot(_phaseTransitionVisualRoot, _phaseTransitionVisualDuration, ref _phaseTransitionVisualCoroutine);
				PlayPhaseTransitionEarthShield();
				PlayOneShot(_phaseTransitionSfx);
				break;
			case CrabBossEncounterState.Defeated:
				PlayAnimatorTrigger(_defeatedTriggerName);
				PlayParticle(_defeatedVfx);
				PlayOneShot(_defeatedSfx);
				BeginDissolve();
				break;
			}
		}

		private void BeginDissolve()
		{
			if (_dissolveCoroutine == null)
			{
				if (!PrepareDissolveMaterials())
				{
					Log("Dissolve could not start. Shader or model renderers are missing.");
				}
				else
				{
					_dissolveCoroutine = StartCoroutine(DissolveRoutine());
				}
			}
		}

		private bool PrepareDissolveMaterials()
		{
			if (_dissolvePrepared)
			{
				return _dissolveRenderers.Count > 0;
			}
			Shader shader = Resources.Load<Shader>("CrabBossDissolve");
			if (shader == null)
			{
				shader = Shader.Find("PaperPixelGames/CrabBossDissolve");
			}
			Transform transform = ((_animator != null) ? _animator.transform : null);
			if (shader == null || transform == null)
			{
				return false;
			}
			_animatorWasActiveBeforeDissolve = _animator.gameObject.activeSelf;
			_animatorWasEnabledBeforeDissolve = _animator.enabled;
			Renderer[] componentsInChildren = transform.GetComponentsInChildren<Renderer>(includeInactive: true);
			foreach (Renderer renderer in componentsInChildren)
			{
				if (renderer == null || renderer is ParticleSystemRenderer)
				{
					continue;
				}
				Material[] sharedMaterials = renderer.sharedMaterials;
				Material[] array = new Material[sharedMaterials.Length];
				for (int j = 0; j < sharedMaterials.Length; j++)
				{
					Material material = sharedMaterials[j];
					if (!(material == null))
					{
						Material material2 = new Material(material)
						{
							name = material.name + " (Crab Boss Dissolve)",
							shader = shader
						};
						material2.SetFloat("_DissolveAmount", 0f);
						material2.SetFloat("_DissolveEdgeWidth", _dissolveEdgeWidth);
						material2.SetColor("_DissolveEdgeColor", _dissolveEdgeColor);
						material2.SetFloat("_DissolveNoiseScale", _dissolveNoiseScale);
						array[j] = material2;
						_runtimeDissolveMaterials.Add(material2);
					}
				}
				_dissolveRenderers.Add(renderer);
				_originalRendererMaterials.Add(sharedMaterials);
				_originalRendererEnabledStates.Add(renderer.enabled);
				renderer.sharedMaterials = array;
			}
			_dissolvePrepared = true;
			return _dissolveRenderers.Count > 0;
		}

		private IEnumerator DissolveRoutine()
		{
			if (_dissolveDelay > 0f)
			{
				yield return new WaitForSeconds(_dissolveDelay);
			}
			float elapsed = 0f;
			while (elapsed < _dissolveDuration)
			{
				elapsed += Time.deltaTime;
				SetDissolveAmount(Mathf.Clamp01(elapsed / _dissolveDuration));
				yield return null;
			}
			SetDissolveAmount(1.05f);
			for (int i = 0; i < _dissolveRenderers.Count; i++)
			{
				if (_dissolveRenderers[i] != null)
				{
					_dissolveRenderers[i].enabled = false;
				}
			}
			if (_animator != null)
			{
				_animator.enabled = false;
				if (_animator.gameObject != base.gameObject)
				{
					_animator.gameObject.SetActive(value: false);
				}
			}
			_dissolveCoroutine = null;
		}

		private void SetDissolveAmount(float amount)
		{
			for (int i = 0; i < _runtimeDissolveMaterials.Count; i++)
			{
				Material material = _runtimeDissolveMaterials[i];
				if (material != null)
				{
					material.SetFloat("_DissolveAmount", amount);
				}
			}
		}

		private void ResetDissolvePresentation()
		{
			if (_dissolveCoroutine != null)
			{
				StopCoroutine(_dissolveCoroutine);
				_dissolveCoroutine = null;
			}
			for (int i = 0; i < _dissolveRenderers.Count; i++)
			{
				Renderer renderer = _dissolveRenderers[i];
				if (!(renderer == null))
				{
					if (i < _originalRendererMaterials.Count)
					{
						renderer.sharedMaterials = _originalRendererMaterials[i];
					}
					if (i < _originalRendererEnabledStates.Count)
					{
						renderer.enabled = _originalRendererEnabledStates[i];
					}
				}
			}
			if (_dissolvePrepared && _animator != null)
			{
				if (_animator.gameObject != base.gameObject)
				{
					_animator.gameObject.SetActive(_animatorWasActiveBeforeDissolve);
				}
				_animator.enabled = _animatorWasEnabledBeforeDissolve;
			}
			DisposeDissolveMaterials();
		}

		private void DisposeDissolveMaterials()
		{
			for (int i = 0; i < _runtimeDissolveMaterials.Count; i++)
			{
				Material material = _runtimeDissolveMaterials[i];
				if (material != null)
				{
					if (Application.isPlaying)
					{
						UnityEngine.Object.Destroy(material);
					}
					else
					{
						UnityEngine.Object.DestroyImmediate(material);
					}
				}
			}
			_runtimeDissolveMaterials.Clear();
			_dissolveRenderers.Clear();
			_originalRendererMaterials.Clear();
			_originalRendererEnabledStates.Clear();
			_dissolvePrepared = false;
		}

		private void ProcessIdleChange()
		{
			if (_boss.IsIdle != _previousIdle && _boss.IsIdle && (_previousState != CrabBossEncounterState.Sleeping || _wakeTriggerName == CrabBossAnimatorTrigger.None))
			{
				CrossFadeState(ResolveIdleStateName(_boss.CurrentPhase));
			}
		}

		private void ProcessShieldChange()
		{
			float currentShieldHealth = _boss.CurrentShieldHealth;
			if (!(currentShieldHealth >= _previousShieldHealth))
			{
				if (_previousShieldHealth > 0f && currentShieldHealth <= 0f)
				{
					PlayShieldBreakPresentation();
				}
				else
				{
					PlayShieldHitPresentation();
				}
			}
		}

		private void ProcessHealthChange()
		{
			if (!(_boss.CurrentHealth >= _previousHealth) && _boss.EncounterState != CrabBossEncounterState.PhaseTransition && _boss.EncounterState != CrabBossEncounterState.Defeated)
			{
				PlayAnimatorTrigger(_bodyHitTriggerName);
				PlayParticle(_bodyHitVfx);
				PlayOneShot(_bodyHitSfx);
				Log("Body hit presentation played.");
			}
		}

		private void RefreshUi()
		{
			CrabBossEncounterState encounterState = _boss.EncounterState;
			bool flag = _isLocalPlayerInRoom && encounterState != CrabBossEncounterState.Inactive && encounterState != CrabBossEncounterState.Sleeping && encounterState != CrabBossEncounterState.Defeated;
			SetBossUiVisible(flag);
			float normalizedValue = ResolveHealthFill();
			float num = ResolveShieldFill();
			UpdateHealthPhaseDividers();
			UpdateBarFeedback(normalizedValue, _healthFill, _healthPositiveFill, _healthNegativeFill, ref _healthBarInitialized);
			bool flag2 = flag && num > 0f;
			bool active = flag && !flag2;
			SetActiveIfNeeded(_healthBarRoot, active);
			SetActiveIfNeeded(_shieldBarRoot, flag2);
			UpdateBarFeedback(num, _shieldFill, _shieldPositiveFill, _shieldNegativeFill, ref _shieldBarInitialized);
			_uiDirty = false;
		}

		private void ApplyCurrentAnimationState()
		{
			ApplyCurrentPhaseMaterial();
			if (_boss.EncounterState == CrabBossEncounterState.Sleeping)
			{
				CrossFadeState(_sleepStateName);
			}
			else if (_boss.EncounterState == CrabBossEncounterState.Defeated)
			{
				BeginDissolve();
			}
			else if (_boss.IsIdle)
			{
				CrossFadeState(ResolveIdleStateName(_boss.CurrentPhase));
			}
		}

		private void CacheCurrentState()
		{
			_previousState = _boss.EncounterState;
			_previousHealth = _boss.CurrentHealth;
			_previousShieldHealth = _boss.CurrentShieldHealth;
			_previousIdle = _boss.IsIdle;
		}

		private void CrossFadeState(string stateName)
		{
			if (!(_animator == null) && !string.IsNullOrWhiteSpace(stateName))
			{
				_animator.CrossFade(stateName, _crossFadeDuration);
			}
		}

		private string ResolveIdleStateName(int phase)
		{
			string text = phase switch
			{
				1 => _phase1IdleStateName, 
				2 => _phase2IdleStateName, 
				3 => _phase3IdleStateName, 
				_ => null, 
			};
			if (string.IsNullOrWhiteSpace(text))
			{
				return _idleStateName;
			}
			return text;
		}

		public bool PlayAnimatorTrigger(string triggerName)
		{
			if (_animator == null || string.IsNullOrWhiteSpace(triggerName))
			{
				return false;
			}
			_animator.SetTrigger(triggerName);
			return true;
		}

		public bool PlayAnimatorTrigger(CrabBossAnimatorTrigger trigger)
		{
			return PlayAnimatorTrigger(trigger.ToParameterName());
		}

		public void ResetAnimatorTrigger(string triggerName)
		{
			if (!(_animator == null) && !string.IsNullOrWhiteSpace(triggerName))
			{
				_animator.ResetTrigger(triggerName);
			}
		}

		public void ResetAnimatorTrigger(CrabBossAnimatorTrigger trigger)
		{
			ResetAnimatorTrigger(trigger.ToParameterName());
		}

		public bool SetAnimatorBool(string parameterName, bool value)
		{
			if (_animator == null || string.IsNullOrWhiteSpace(parameterName))
			{
				return false;
			}
			_animator.SetBool(parameterName, value);
			return true;
		}

		public bool SetAnimatorBool(CrabBossAnimatorBool parameter, bool value)
		{
			return SetAnimatorBool(parameter.ToParameterName(), value);
		}

		public bool PlayAttackAnimation(CrabBossAttackAnimation animation)
		{
			if (animation == CrabBossAttackAnimation.None)
			{
				return true;
			}
			string text = animation.ToParameterName();
			if (animation.ToParameterType() == AnimatorControllerParameterType.Bool)
			{
				return SetAnimatorBool(text, value: true);
			}
			ResetAnimatorTrigger(text);
			return PlayAnimatorTrigger(text);
		}

		public void PlayRockRainLandingVfx()
		{
			PlayParticle(_rockRainLandingVfx);
		}

		public void PlayStalactiteDropLandingPresentation()
		{
			if (base.isActiveAndEnabled)
			{
				AlignStalactiteDropLandingVisual();
				PlayTimedVisualRoot(_stalactiteDropLandingVisualRoot, _stalactiteDropLandingVisualDuration, ref _stalactiteDropLandingVisualCoroutine);
				PlayOneShot(_stalactiteDropLandingSfx);
				if (_stalactiteDropLandingCameraShake && _isLocalPlayerInRoom)
				{
					Singleton<PlayersManager>.Instance?.LocalPlayer?.CameraShake(_stalactiteDropLandingShakeIntensity, _stalactiteDropLandingShakeDuration);
				}
			}
		}

		private void AlignStalactiteDropLandingVisual()
		{
			if (!(_stalactiteDropLandingVisualRoot == null))
			{
				Transform obj = ((_stalactiteDropLandingAnchor != null) ? _stalactiteDropLandingAnchor : base.transform);
				Transform transform = _stalactiteDropLandingVisualRoot.transform;
				Vector3 position = obj.TransformPoint(_stalactiteDropLandingLocalPosition);
				Quaternion rotation = obj.rotation * Quaternion.Euler(_stalactiteDropLandingLocalEulerAngles);
				if (transform.parent != _stalactiteDropLandingStableParent)
				{
					transform.SetParent(_stalactiteDropLandingStableParent, worldPositionStays: true);
				}
				transform.SetPositionAndRotation(position, rotation);
			}
		}

		public void PlayCreatureSpawnRoarPresentation()
		{
			if (base.isActiveAndEnabled)
			{
				PlayTimedVisualRoot(_creatureSpawnRoarVisualRoot, _creatureSpawnRoarVisualDuration, ref _creatureSpawnRoarVisualCoroutine);
				PlayOneShot(_creatureSpawnRoarSfx);
				if (_creatureSpawnRoarCameraShake && _isLocalPlayerInRoom)
				{
					Singleton<PlayersManager>.Instance?.LocalPlayer?.CameraShake(_creatureSpawnRoarShakeIntensity, _creatureSpawnRoarShakeDuration);
				}
			}
		}

		public void PlayProjectTileLaunchPresentation()
		{
			PlayTimedVisualRoot(_projectTileLaunchVisualRoot, _projectTileLaunchVisualDuration, ref _projectTileLaunchVisualCoroutine);
			PlayOneShot(_projectTileLaunchRockSfx, _projectTileLaunchRockSfxVolume);
			PlayOneShot(_projectTileLaunchWhooshSfx, _projectTileLaunchWhooshSfxVolume);
		}

		public void PlayGrabPlayerSfx()
		{
			PlayOneShot(_grabPlayerSfx, _grabPlayerSfxVolume);
		}

		public void PlayThrowPlayerSfx()
		{
			PlayOneShot(_throwPlayerSfx, _throwPlayerSfxVolume);
		}

		public void PlayRockImpactSfx()
		{
			PlayOneShot(_rockImpactSfx, _rockImpactSfxVolume);
		}

		public void StopAttackAnimation(CrabBossAttackAnimation animation)
		{
			if (animation != CrabBossAttackAnimation.None)
			{
				string text = animation.ToParameterName();
				if (animation.ToParameterType() == AnimatorControllerParameterType.Bool)
				{
					SetAnimatorBool(text, value: false);
				}
				else
				{
					ResetAnimatorTrigger(text);
				}
			}
		}

		public bool ValidateAttackAnimation(CrabBossAttackAnimation animation, out string error)
		{
			if (animation == CrabBossAttackAnimation.None)
			{
				error = null;
				return true;
			}
			if (_animator == null)
			{
				error = "Crab Animator is missing.";
				return false;
			}
			string text = animation.ToParameterName();
			AnimatorControllerParameterType animatorControllerParameterType = animation.ToParameterType();
			AnimatorControllerParameter[] parameters = _animator.parameters;
			foreach (AnimatorControllerParameter animatorControllerParameter in parameters)
			{
				if (!(animatorControllerParameter.name != text))
				{
					if (animatorControllerParameter.type != animatorControllerParameterType)
					{
						error = $"Animator parameter '{text}' must be {animatorControllerParameterType}, " + $"but is {animatorControllerParameter.type}.";
						return false;
					}
					error = null;
					return true;
				}
			}
			error = $"Animator parameter '{text}' ({animatorControllerParameterType}) is missing.";
			return false;
		}

		public void SetLocalPlayerInRoom(bool isInRoom)
		{
			if (_isLocalPlayerInRoom == isInRoom)
			{
				return;
			}
			_isLocalPlayerInRoom = isInRoom;
			_uiDirty = true;
			if (!base.isActiveAndEnabled)
			{
				SetBossUiVisible(visible: false);
				return;
			}
			UpdateBossAudio();
			if (_boss != null && _boss.IsSpawned)
			{
				RefreshUi();
			}
			else
			{
				SetBossUiVisible(visible: false);
			}
		}

		private void SetBossUiVisible(bool visible)
		{
			SetActiveIfNeeded(_uiRoot, visible);
			if (!visible)
			{
				SetActiveIfNeeded(_healthBarRoot, active: false);
				SetActiveIfNeeded(_shieldBarRoot, active: false);
			}
		}

		private static void SetActiveIfNeeded(GameObject target, bool active)
		{
			if (target != null && target.activeSelf != active)
			{
				target.SetActive(active);
			}
		}

		private static float ResolveFill(float current, float maximum)
		{
			if (maximum <= 0f)
			{
				return 0f;
			}
			return Mathf.Clamp01(current / maximum);
		}

		private void UpdateBarFeedback(float normalizedValue, Image mainFill, Image positiveFill, Image negativeFill, ref bool initialized)
		{
			if (mainFill == null)
			{
				return;
			}
			normalizedValue = Mathf.Clamp01(normalizedValue);
			if (positiveFill == null || negativeFill == null)
			{
				SetBarValue(mainFill, normalizedValue);
				return;
			}
			if (!initialized)
			{
				SetBarValue(mainFill, normalizedValue);
				SetBarValue(positiveFill, normalizedValue);
				SetBarValue(negativeFill, normalizedValue);
				initialized = true;
				return;
			}
			float barValue = GetBarValue(mainFill);
			if (normalizedValue > barValue + 0.0001f)
			{
				SetBarValue(positiveFill, normalizedValue);
				SetBarValue(mainFill, ExpSmooth(barValue, normalizedValue));
				SetBarValue(negativeFill, GetBarValue(mainFill));
			}
			else if (normalizedValue < barValue - 0.0001f)
			{
				SetBarValue(mainFill, normalizedValue);
				SetBarValue(positiveFill, normalizedValue);
				if (GetBarValue(negativeFill) < barValue)
				{
					SetBarValue(negativeFill, barValue);
				}
				SetBarValue(negativeFill, ExpSmooth(GetBarValue(negativeFill), normalizedValue));
			}
			else
			{
				SetBarValue(mainFill, ExpSmooth(GetBarValue(mainFill), normalizedValue));
				SetBarValue(positiveFill, ExpSmooth(GetBarValue(positiveFill), normalizedValue));
				SetBarValue(negativeFill, ExpSmooth(GetBarValue(negativeFill), normalizedValue));
			}
		}

		private float ExpSmooth(float current, float target)
		{
			if (_barFeedbackLerpSpeed <= 0f || Mathf.Abs(target - current) <= 0.0001f)
			{
				return target;
			}
			float num = 1f - Mathf.Exp((0f - _barFeedbackLerpSpeed) * Time.deltaTime);
			float num2 = current + (target - current) * num;
			if (!(Mathf.Abs(target - num2) <= 0.0001f))
			{
				return num2;
			}
			return target;
		}

		private bool HasPendingBarFeedback()
		{
			if (!HasPendingBarFeedback(ResolveHealthFill(), _healthFill, _healthPositiveFill, _healthNegativeFill))
			{
				return HasPendingBarFeedback(ResolveShieldFill(), _shieldFill, _shieldPositiveFill, _shieldNegativeFill);
			}
			return true;
		}

		private static bool HasPendingBarFeedback(float targetValue, Image mainFill, Image positiveFill, Image negativeFill)
		{
			if (mainFill == null || positiveFill == null || negativeFill == null)
			{
				return false;
			}
			if (Mathf.Approximately(GetBarValue(mainFill), targetValue) && Mathf.Approximately(GetBarValue(positiveFill), targetValue))
			{
				return !Mathf.Approximately(GetBarValue(negativeFill), targetValue);
			}
			return true;
		}

		private static float GetBarValue(Image fill)
		{
			if (fill == null)
			{
				return 0f;
			}
			Slider componentInParent = fill.GetComponentInParent<Slider>();
			if (!(componentInParent != null))
			{
				return fill.fillAmount;
			}
			return componentInParent.value;
		}

		private static void SetBarValue(Image fill, float value)
		{
			if (!(fill == null))
			{
				value = Mathf.Clamp01(value);
				Slider componentInParent = fill.GetComponentInParent<Slider>();
				if (componentInParent != null)
				{
					componentInParent.value = value;
				}
				fill.fillAmount = value;
			}
		}

		private float ResolveHealthFill()
		{
			return _boss.GetEncounterHealth01();
		}

		private float ResolveShieldFill()
		{
			return ResolveFill(_boss.CurrentShieldHealth, _boss.MaximumShieldHealth);
		}

		private void UpdateHealthPhaseDividers()
		{
			SetHealthDividerPosition(_phase1HealthDivider, _boss.GetEncounterHealthBoundary01(1));
			SetHealthDividerPosition(_phase2HealthDivider, _boss.GetEncounterHealthBoundary01(2));
		}

		private static void SetHealthDividerPosition(RectTransform divider, float normalizedPosition)
		{
			if (!(divider == null))
			{
				normalizedPosition = Mathf.Clamp01(normalizedPosition);
				divider.anchorMin = new Vector2(normalizedPosition, 0f);
				divider.anchorMax = new Vector2(normalizedPosition, 1f);
				divider.anchoredPosition = Vector2.zero;
			}
		}

		private void PlayShieldHitPresentation()
		{
			PlayAnimatorTrigger(_shieldHitTriggerName);
			PlayParticle(_shieldHitVfx);
			PlayOneShot(_shieldHitSfx);
			Log("Shield hit presentation played.");
		}

		private void PlayShieldBreakPresentation()
		{
			PlayAnimatorTrigger(_shieldBreakTriggerName);
			PlayParticle(_shieldBreakVfx);
			PlayOneShot(_shieldBreakSfx);
			Log("Shield break presentation played.");
		}

		private void PlayPhaseTransitionEarthShield()
		{
			if (!(_phaseTransitionEarthShieldPrefab == null))
			{
				StopPhaseTransitionEarthShield();
				Transform transform = ((_phaseTransitionEarthShieldParent != null) ? _phaseTransitionEarthShieldParent : ((_animator != null) ? _animator.transform : base.transform));
				EarthShield earthShield = (_phaseTransitionEarthShieldInstance = UnityEngine.Object.Instantiate(_phaseTransitionEarthShieldPrefab, transform));
				VfxData vfxData = new VfxData(transform, transform.position, _phaseTransitionVisualDuration, 1f);
				vfxData.SetGround(transform.position);
				earthShield.Play(vfxData);
			}
		}

		private void StopPhaseTransitionEarthShield()
		{
			if (!(_phaseTransitionEarthShieldInstance == null))
			{
				GameObject gameObject = _phaseTransitionEarthShieldInstance.gameObject;
				_phaseTransitionEarthShieldInstance = null;
				gameObject.SetActive(value: false);
				if (Application.isPlaying)
				{
					UnityEngine.Object.Destroy(gameObject);
				}
				else
				{
					UnityEngine.Object.DestroyImmediate(gameObject);
				}
			}
		}

		private void ApplyCurrentPhaseMaterial()
		{
			if (_boss == null)
			{
				return;
			}
			bool flag = _boss.EncounterState == CrabBossEncounterState.Shielded;
			Material material = ResolvePhaseMaterial(_boss.CurrentPhase, flag);
			if (material == null)
			{
				Log($"Phase material is missing. Phase={_boss.CurrentPhase}, " + $"Shielded={flag}.");
				return;
			}
			Renderer[] array = ResolvePhaseMaterialRenderers();
			foreach (Renderer renderer in array)
			{
				if (!(renderer == null))
				{
					Material[] sharedMaterials = renderer.sharedMaterials;
					for (int j = 0; j < sharedMaterials.Length; j++)
					{
						sharedMaterials[j] = material;
					}
					renderer.sharedMaterials = sharedMaterials;
				}
			}
			Log($"Phase material applied. Phase={_boss.CurrentPhase}, " + $"Shielded={flag}, Material={material.name}, Renderers={array.Length}.");
		}

		private Material ResolvePhaseMaterial(int phase, bool shielded)
		{
			return phase switch
			{
				1 => shielded ? _phase1ShieldMaterial : _phase1VulnerableMaterial, 
				2 => shielded ? _phase2ShieldMaterial : _phase2VulnerableMaterial, 
				3 => shielded ? _phase3ShieldMaterial : _phase3VulnerableMaterial, 
				_ => null, 
			};
		}

		private Renderer[] ResolvePhaseMaterialRenderers()
		{
			if (_phaseMaterialRenderers != null && _phaseMaterialRenderers.Length != 0)
			{
				return _phaseMaterialRenderers;
			}
			if (_resolvedPhaseMaterialRenderers == null)
			{
				if (_animator == null)
				{
					_resolvedPhaseMaterialRenderers = Array.Empty<Renderer>();
				}
				else
				{
					Renderer[] componentsInChildren = _animator.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true);
					_resolvedPhaseMaterialRenderers = componentsInChildren;
				}
			}
			return _resolvedPhaseMaterialRenderers;
		}

		private void PlayParticle(ParticleSystem particleSystem)
		{
			if (base.isActiveAndEnabled && !(particleSystem == null))
			{
				particleSystem.Play(withChildren: true);
			}
		}

		private void PlayTimedVisualRoot(GameObject visualRoot, float duration, ref Coroutine activeCoroutine)
		{
			if (base.isActiveAndEnabled && !(visualRoot == null))
			{
				if (activeCoroutine != null)
				{
					StopCoroutine(activeCoroutine);
				}
				visualRoot.SetActive(value: true);
				ParticleSystem[] componentsInChildren = visualRoot.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
				foreach (ParticleSystem obj in componentsInChildren)
				{
					obj.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
					obj.Play(withChildren: true);
				}
				activeCoroutine = StartCoroutine(DisableVisualRootAfterDelay(visualRoot, duration));
			}
		}

		private void StopTimedVisualRoot(GameObject visualRoot, ref Coroutine activeCoroutine)
		{
			if (activeCoroutine != null)
			{
				StopCoroutine(activeCoroutine);
				activeCoroutine = null;
			}
			if (!(visualRoot == null))
			{
				ParticleSystem[] componentsInChildren = visualRoot.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
				for (int i = 0; i < componentsInChildren.Length; i++)
				{
					componentsInChildren[i].Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
				}
				visualRoot.SetActive(value: false);
			}
		}

		private IEnumerator DisableVisualRootAfterDelay(GameObject visualRoot, float duration)
		{
			yield return new WaitForSeconds(duration);
			if (visualRoot != null)
			{
				visualRoot.SetActive(value: false);
			}
			if (visualRoot == _phaseTransitionVisualRoot)
			{
				_phaseTransitionVisualCoroutine = null;
			}
			if (visualRoot == _creatureSpawnRoarVisualRoot)
			{
				_creatureSpawnRoarVisualCoroutine = null;
			}
			if (visualRoot == _projectTileLaunchVisualRoot)
			{
				_projectTileLaunchVisualCoroutine = null;
			}
			if (visualRoot == _stalactiteDropLandingVisualRoot)
			{
				_stalactiteDropLandingVisualCoroutine = null;
			}
		}

		private void PlayOneShot(AudioClip clip)
		{
			PlayOneShot(clip, 1f);
		}

		private void PlayOneShot(AudioClip clip, float volumeScale)
		{
			if (base.isActiveAndEnabled && !(_audioSource == null) && !(clip == null) && !(volumeScale <= 0f))
			{
				_audioSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
			}
		}

		private void CreateMusicSourcesIfNeeded()
		{
			_musicSourceA = EnsureMusicSource(_musicSourceA, "Boss Music A");
			_musicSourceB = EnsureMusicSource(_musicSourceB, "Boss Music B");
		}

		private AudioSource EnsureMusicSource(AudioSource source, string sourceName)
		{
			if (source == null)
			{
				GameObject obj = new GameObject(sourceName);
				obj.transform.SetParent(base.transform, worldPositionStays: false);
				source = obj.AddComponent<AudioSource>();
				source.playOnAwake = false;
				source.loop = true;
				source.spatialBlend = 1f;
				source.volume = 0f;
			}
			return source;
		}

		private void UpdateBossAudio()
		{
			if (!(_boss == null))
			{
				CreateMusicSourcesIfNeeded();
				bool flag = _isLocalPlayerInRoom && IsArenaAudioState(_boss.EncounterState);
				UpdateBossMusic(flag ? ResolveMusicForPhase(_boss.CurrentPhase) : null);
				if (!flag || _boss.IsAttackRunning || !IsCombatState(_boss.EncounterState))
				{
					_nextIdleSfxTime = 0f;
				}
				else if (_nextIdleSfxTime <= 0f)
				{
					ScheduleNextIdleSfx();
				}
				else if (Time.time >= _nextIdleSfxTime)
				{
					PlayRandomIdleSfx();
				}
			}
		}

		private AudioClip ResolveMusicForPhase(int phase)
		{
			if (phase >= 3 && _phase3MusicLoop != null)
			{
				return _phase3MusicLoop;
			}
			if (phase >= 2 && _phase2MusicLoop != null)
			{
				return _phase2MusicLoop;
			}
			return _phase1MusicLoop;
		}

		private void UpdateBossMusic(AudioClip targetClip)
		{
			if (targetClip != null && (_activeMusicSource == null || _activeMusicSource.clip != targetClip))
			{
				AudioSource audioSource = ((_activeMusicSource == _musicSourceA) ? _musicSourceB : _musicSourceA);
				audioSource.Stop();
				audioSource.clip = targetClip;
				audioSource.volume = 0f;
				audioSource.Play();
				_activeMusicSource = audioSource;
			}
			FadeMusicSource(_musicSourceA, (_activeMusicSource == _musicSourceA && targetClip != null) ? _bossMusicVolume : 0f);
			FadeMusicSource(_musicSourceB, (_activeMusicSource == _musicSourceB && targetClip != null) ? _bossMusicVolume : 0f);
		}

		private void FadeMusicSource(AudioSource source, float targetVolume)
		{
			if (source == null)
			{
				return;
			}
			source.volume = Mathf.MoveTowards(source.volume, Mathf.Clamp01(targetVolume), Time.unscaledDeltaTime / _musicCrossfadeDuration);
			if (source.volume <= 0.0001f && targetVolume <= 0f && source.isPlaying)
			{
				source.Stop();
				if (source == _activeMusicSource)
				{
					_activeMusicSource = null;
				}
			}
		}

		private void StopBossAudioLoops()
		{
			StopMusicSource(_musicSourceA);
			StopMusicSource(_musicSourceB);
			_activeMusicSource = null;
			_nextIdleSfxTime = 0f;
		}

		private static void StopMusicSource(AudioSource source)
		{
			if (!(source == null))
			{
				source.Stop();
				source.volume = 0f;
			}
		}

		private void PlayRandomIdleSfx()
		{
			ScheduleNextIdleSfx();
			if (_idleSfx == null || _idleSfx.Length == 0 || _audioSource == null)
			{
				return;
			}
			int num = 0;
			for (int i = 0; i < _idleSfx.Length; i++)
			{
				if (_idleSfx[i] != null)
				{
					num++;
				}
			}
			if (num == 0)
			{
				return;
			}
			int num2 = -1;
			for (int j = 0; j < 8; j++)
			{
				int num3 = UnityEngine.Random.Range(0, _idleSfx.Length);
				if (!(_idleSfx[num3] == null) && (num <= 1 || num3 != _lastIdleSfxIndex))
				{
					num2 = num3;
					break;
				}
			}
			if (num2 < 0)
			{
				for (int k = 0; k < _idleSfx.Length; k++)
				{
					if (_idleSfx[k] != null)
					{
						num2 = k;
						break;
					}
				}
			}
			if (num2 >= 0)
			{
				_lastIdleSfxIndex = num2;
				_audioSource.PlayOneShot(_idleSfx[num2], _idleSfxVolume);
			}
		}

		private void ScheduleNextIdleSfx()
		{
			_nextIdleSfxTime = Time.time + UnityEngine.Random.Range(Mathf.Min(_idleSfxMinimumInterval, _idleSfxMaximumInterval), Mathf.Max(_idleSfxMinimumInterval, _idleSfxMaximumInterval));
		}

		private static bool IsArenaAudioState(CrabBossEncounterState state)
		{
			if (state != CrabBossEncounterState.Shielded && state != CrabBossEncounterState.Vulnerable)
			{
				return state == CrabBossEncounterState.PhaseTransition;
			}
			return true;
		}

		private static bool IsCombatState(CrabBossEncounterState state)
		{
			if (state != CrabBossEncounterState.Shielded)
			{
				return state == CrabBossEncounterState.Vulnerable;
			}
			return true;
		}

		private void Log(string message)
		{
			if (_log)
			{
				UtilityMethods.Log("[CrabBoss][Presentation] " + message);
			}
		}
	}
}
