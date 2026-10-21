using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Player;
using UnityEngine;

namespace PaperPixelGames.Core.Ocean.Creatures
{
	public sealed class ElectricalCreature : CatchableSeaCreature
	{
		[Header("Electrical Refs")]
		[SerializeField]
		private FishDeformerHandler _fishDeformerHandler;

		[SerializeField]
		private CreatureShieldAbility _shieldAbility;

		[SerializeField]
		private OverlayFX _electricOverlayFx;

		[Header("Electrical Response")]
		[SerializeField]
		private bool _shieldBlocksCatch = true;

		[SerializeField]
		private byte _electricalDamage = 10;

		[SerializeField]
		private byte _stunDuration = 3;

		[Header("Embedded Graphic")]
		[SerializeField]
		private GameObject _embeddedGraphicRoot;

		private bool _hasPendingAnim;

		private CreatureAnim _pendingAnim = CreatureAnim.Move;

		protected override bool UseEmbeddedGraphic => true;

		protected override GameObject EmbeddedGraphicRoot => _embeddedGraphicRoot;

		protected override void CacheComponents()
		{
			base.CacheComponents();
			if (_shieldAbility == null)
			{
				TryGetComponent<CreatureShieldAbility>(out _shieldAbility);
			}
			if (_fishDeformerHandler == null)
			{
				_fishDeformerHandler = GetComponentInChildren<FishDeformerHandler>(includeInactive: true);
			}
			if (_embeddedGraphicRoot == null && _fishDeformerHandler != null)
			{
				_embeddedGraphicRoot = _fishDeformerHandler.gameObject;
			}
			if (_electricOverlayFx == null)
			{
				_electricOverlayFx = GetComponentInChildren<OverlayFX>(includeInactive: true);
			}
		}

		protected override void OnGraphicSpawned(GameObject graphic)
		{
			base.OnGraphicSpawned(graphic);
			ConfigureFishGraphic(graphic);
			ApplyPendingAnimIfReady();
		}

		protected override void OnClientVisualInitialized()
		{
			base.OnClientVisualInitialized();
			if (_activeGraphic != null)
			{
				ConfigureFishGraphic(_activeGraphic);
			}
			ApplyPendingAnimIfReady();
		}

		private void ConfigureFishGraphic(GameObject graphic)
		{
			if (!(graphic == null))
			{
				if (_fishDeformerHandler == null)
				{
					_fishDeformerHandler = graphic.GetComponentInChildren<FishDeformerHandler>(includeInactive: true);
				}
				if (_fishDeformerHandler == null)
				{
					_fishDeformerHandler = GetComponentInChildren<FishDeformerHandler>(includeInactive: true);
				}
				FishSO fishSO = base.FishSO;
				if (fishSO == null)
				{
					Log("ConfigureFishGraphic failed. FishSO is null.", LogType.Warning);
				}
				else if (_fishDeformerHandler == null)
				{
					Log("ConfigureFishGraphic failed. FishDeformerHandler is null.", LogType.Warning);
				}
				else
				{
					_fishDeformerHandler.CacheDeformer(fishSO, graphic);
				}
			}
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			if (_shieldAbility != null)
			{
				_shieldAbility.ShieldChanged += OnShieldChanged;
			}
			ApplyElectricVisual(_shieldAbility != null && _shieldAbility.IsShieldActive);
		}

		public override void OnNetworkDespawn()
		{
			if (_shieldAbility != null)
			{
				_shieldAbility.ShieldChanged -= OnShieldChanged;
			}
			base.OnNetworkDespawn();
		}

		protected override void OnServerCatchableHit(PPG_Player player)
		{
			if (base.IsServer && !(player == null))
			{
				if (ShouldBlockCatchWithShield())
				{
					RunElectricCounter(player);
				}
				else
				{
					base.OnServerCatchableHit(player);
				}
			}
		}

		protected override bool ServerCanInstantCatch(PPG_Player owner)
		{
			if (!ShouldBlockCatchWithShield())
			{
				return true;
			}
			RunElectricCounter(owner);
			return false;
		}

		private bool ShouldBlockCatchWithShield()
		{
			if (_shieldBlocksCatch && _shieldAbility != null)
			{
				return _shieldAbility.CanBlockHit();
			}
			return false;
		}

		private void RunElectricCounter(PPG_Player target)
		{
			if (base.IsServer && !(target == null))
			{
				if (_shieldAbility != null)
				{
					_shieldAbility.TryBreakShield();
				}
				target.ClientElectrocutePlayerRpc(_electricalDamage, _stunDuration);
				ChangeAnim(CreatureAnim.SpecialStart);
				Log($"Electric counter triggered. Target={target.name} Damage={_electricalDamage} Stun={_stunDuration}");
			}
		}

		private void OnShieldChanged(bool isActive)
		{
			ApplyElectricVisual(isActive);
		}

		private void ApplyElectricVisual(bool active)
		{
			if (!(_electricOverlayFx == null) && _electricOverlayFx.gameObject.activeSelf != active)
			{
				_electricOverlayFx.gameObject.SetActive(active);
			}
		}

		protected override void ApplyAnim(CreatureAnim anim)
		{
			_pendingAnim = anim;
			_hasPendingAnim = true;
			if (!TryApplyAnim(anim))
			{
				Log($"ApplyAnim deferred. Anim={anim} DeformerReady={_fishDeformerHandler != null}", LogType.Warning);
			}
		}

		private void ApplyPendingAnimIfReady()
		{
			if (_hasPendingAnim)
			{
				TryApplyAnim(_pendingAnim);
			}
		}

		private bool TryApplyAnim(CreatureAnim anim)
		{
			if (_fishDeformerHandler == null)
			{
				_fishDeformerHandler = GetComponentInChildren<FishDeformerHandler>(includeInactive: true);
			}
			if (_fishDeformerHandler == null)
			{
				return false;
			}
			switch (anim)
			{
			case CreatureAnim.Idle:
			case CreatureAnim.Move:
				_fishDeformerHandler.ChangePreset(FishAnim.Move);
				return true;
			case CreatureAnim.Sprint:
				_fishDeformerHandler.ChangePreset(FishAnim.Sprint);
				return true;
			case CreatureAnim.Struggle:
				_fishDeformerHandler.ChangePreset(FishAnim.Struggle);
				return true;
			case CreatureAnim.Attack:
				_fishDeformerHandler.ChangePreset(FishAnim.Attack);
				return true;
			case CreatureAnim.Dead:
				_fishDeformerHandler.ChangePreset(FishAnim.Dead);
				return true;
			case CreatureAnim.Hit:
				_fishDeformerHandler.ChangePreset(FishAnim.Hit);
				return true;
			case CreatureAnim.CriticalHit:
				_fishDeformerHandler.ChangePreset(FishAnim.CriticalHit);
				return true;
			case CreatureAnim.AttackSwim:
				_fishDeformerHandler.ChangePreset(FishAnim.AttackSwim);
				return true;
			case CreatureAnim.SpecialStart:
				_fishDeformerHandler.ChangePreset(FishAnim.SpeelStart);
				return true;
			case CreatureAnim.SpecialEnd:
				_fishDeformerHandler.ChangePreset(FishAnim.SpeelEnd);
				return true;
			default:
				return false;
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
			return "ElectricalCreature";
		}
	}
}
