using DG.Tweening;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Framework.Indicator;
using UnityEngine;

namespace PaperPixelGames.Core.Ocean.Creatures
{
	public sealed class NormalFishCreature : CatchableSeaCreature
	{
		[Header("Normal Fish Refs")]
		[SerializeField]
		private Animator _animator;

		[SerializeField]
		private FishDeformerHandler _fishDeformerHandler;

		[SerializeField]
		private OffscreenIndicatorTarget _indicator;

		[Header("Spawn Visual")]
		[SerializeField]
		private bool _playSpawnScaleTween = true;

		[SerializeField]
		[Min(0.01f)]
		private float _spawnScaleDuration = 0.5f;

		[SerializeField]
		private Vector3 _spawnStartScale = new Vector3(0.1f, 0.1f, 0.1f);

		private Vector3 _originalGraphicScale = Vector3.one;

		private NormalFishServerLodTier _serverLodTier;

		private bool _networkThresholdsCached;

		private float _originalPositionThreshold;

		private float _originalRotationThreshold;

		public NormalFishServerLodTier ServerLodTier => _serverLodTier;

		public bool CanUseServerDistanceLod
		{
			get
			{
				if (base.IsServer && base.IsSpawned && base.IsServerSimulationActive && !base.IsStandalone && !base.IsLayerRetreating && base.CatchState == CatchableCreatureState.Free && !base.IsHooked.Value && !base.AssistCatchActive)
				{
					return !base.IsPanic;
				}
				return false;
			}
		}

		public void ServerApplyDistanceLod(NormalFishServerLodTier tier, float simulationTickInterval, float positionThreshold, float rotationThreshold)
		{
			if (!base.IsServer)
			{
				return;
			}
			CacheNetworkThresholds();
			if (!CanUseServerDistanceLod)
			{
				tier = NormalFishServerLodTier.Full;
				simulationTickInterval = 0f;
				positionThreshold = _originalPositionThreshold;
				rotationThreshold = _originalRotationThreshold;
			}
			_serverLodTier = tier;
			SetServerSimulationTickInterval(simulationTickInterval);
			if (_networkTransform != null && _networkThresholdsCached)
			{
				float num = Mathf.Max(_originalPositionThreshold, positionThreshold);
				float num2 = Mathf.Max(_originalRotationThreshold, rotationThreshold);
				if (!Mathf.Approximately(_networkTransform.PositionThreshold, num))
				{
					_networkTransform.PositionThreshold = num;
				}
				if (!Mathf.Approximately(_networkTransform.RotAngleThreshold, num2))
				{
					_networkTransform.RotAngleThreshold = num2;
				}
			}
			bool flag = tier == NormalFishServerLodTier.Full && base.IsServerSimulationActive;
			if (_creatureSensor != null && _creatureSensor.IsActive != flag)
			{
				SetSensorActive(flag);
			}
		}

		public void ServerRestoreFullSimulationLod()
		{
			if (base.IsServer)
			{
				CacheNetworkThresholds();
				ServerApplyDistanceLod(NormalFishServerLodTier.Full, 0f, _originalPositionThreshold, _originalRotationThreshold);
			}
		}

		protected override void OnHookedStateChanged(bool hooked)
		{
			if (base.IsServer)
			{
				ServerRestoreFullSimulationLod();
			}
			base.OnHookedStateChanged(hooked);
		}

		private void CacheNetworkThresholds()
		{
			if (!_networkThresholdsCached && !(_networkTransform == null))
			{
				_originalPositionThreshold = _networkTransform.PositionThreshold;
				_originalRotationThreshold = _networkTransform.RotAngleThreshold;
				_networkThresholdsCached = true;
			}
		}

		protected override void OnGraphicSpawned(GameObject graphic)
		{
			base.OnGraphicSpawned(graphic);
			if (!(graphic == null))
			{
				CacheGraphicReferences(graphic);
				ConfigureFishGraphic(graphic);
				PlaySpawnVisual(graphic);
			}
		}

		protected override void OnClientVisualInitialized()
		{
			base.OnClientVisualInitialized();
			if (_activeGraphic != null)
			{
				CacheGraphicReferences(_activeGraphic);
				ConfigureFishGraphic(_activeGraphic);
			}
		}

		private void CacheGraphicReferences(GameObject graphic)
		{
			if (!(graphic == null))
			{
				if (_animator == null)
				{
					_animator = graphic.GetComponentInChildren<Animator>(includeInactive: true);
				}
				if (_fishDeformerHandler == null)
				{
					_fishDeformerHandler = GetComponentInChildren<FishDeformerHandler>(includeInactive: true);
				}
				if (_indicator == null)
				{
					_indicator = GetComponentInChildren<OffscreenIndicatorTarget>(includeInactive: true);
				}
			}
		}

		private void ConfigureFishGraphic(GameObject graphic)
		{
			if (graphic == null)
			{
				return;
			}
			FishSO fishSO = base.FishSO;
			if (!(fishSO == null))
			{
				if (_fishDeformerHandler != null)
				{
					_fishDeformerHandler.CacheDeformer(fishSO, graphic);
				}
				if (_indicator != null)
				{
					_indicator.UpdateIcon(fishSO.Sprite);
				}
				if (_hookedPoint != null)
				{
					_hookedPoint.localPosition = fishSO.HookPointOffset;
				}
				if (TryGetComponent<CapsuleCollider>(out var component))
				{
					component.center = fishSO.ColliderCenter;
					component.radius = fishSO.ColliderRadius;
					component.height = fishSO.ColliderHeight;
					component.direction = Mathf.Clamp(fishSO.CapsuleDirection, 0, 2);
				}
			}
		}

		private void PlaySpawnVisual(GameObject graphic)
		{
			if (_playSpawnScaleTween && !(graphic == null))
			{
				Transform transform = graphic.transform;
				_originalGraphicScale = transform.localScale;
				transform.DOScale(_originalGraphicScale, _spawnScaleDuration).From(_spawnStartScale).SetEase(Ease.OutQuad);
			}
		}

		protected override void ApplyAnim(CreatureAnim anim)
		{
			if (_fishDeformerHandler != null)
			{
				switch (anim)
				{
				case CreatureAnim.Idle:
				case CreatureAnim.Move:
					_fishDeformerHandler.ChangePreset(FishAnim.Move);
					return;
				case CreatureAnim.Sprint:
					_fishDeformerHandler.ChangePreset(FishAnim.Sprint);
					return;
				case CreatureAnim.Struggle:
					_fishDeformerHandler.ChangePreset(FishAnim.Struggle);
					return;
				case CreatureAnim.Attack:
					_fishDeformerHandler.ChangePreset(FishAnim.Attack);
					return;
				case CreatureAnim.Dead:
					_fishDeformerHandler.ChangePreset(FishAnim.Dead);
					return;
				case CreatureAnim.Hit:
					_fishDeformerHandler.ChangePreset(FishAnim.Hit);
					return;
				case CreatureAnim.CriticalHit:
					_fishDeformerHandler.ChangePreset(FishAnim.CriticalHit);
					return;
				case CreatureAnim.AttackSwim:
					_fishDeformerHandler.ChangePreset(FishAnim.AttackSwim);
					return;
				case CreatureAnim.SpecialStart:
					_fishDeformerHandler.ChangePreset(FishAnim.SpeelStart);
					return;
				case CreatureAnim.SpecialEnd:
					_fishDeformerHandler.ChangePreset(FishAnim.SpeelEnd);
					return;
				}
			}
			if (!(_animator == null))
			{
				switch (anim)
				{
				case CreatureAnim.Idle:
				case CreatureAnim.Move:
					_animator.SetBool("Run", value: false);
					_animator.SetBool("Struggle", value: false);
					break;
				case CreatureAnim.Sprint:
					_animator.SetBool("Run", value: true);
					_animator.SetBool("Struggle", value: false);
					break;
				case CreatureAnim.Struggle:
					_animator.SetBool("Struggle", value: true);
					break;
				case CreatureAnim.Hit:
					_animator.SetTrigger("Hit");
					break;
				case CreatureAnim.CriticalHit:
					_animator.SetTrigger("CriticalHit");
					break;
				case CreatureAnim.Dead:
					_animator.SetBool("Dead", value: true);
					break;
				case CreatureAnim.Attack:
					break;
				}
			}
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			base.__initializeRpcs();
		}

		protected override string __getTypeName()
		{
			return "NormalFishCreature";
		}
	}
}
