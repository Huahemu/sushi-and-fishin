using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.PlayerTools;
using PaperPixelGames.Framework.Utility;
using UnityEngine;

namespace PaperPixelGames.Core.Ocean
{
	public class OxyFarter : MonoBehaviour
	{
		[Header("Oxygen Visual")]
		[SerializeField]
		private ParticleSystem _bubbleStreamFX;

		[SerializeField]
		private ParticleSystem _secondaryBubbleStreamFX;

		[SerializeField]
		private LODGroup _lodGroup;

		[Header("Oxygen Settings")]
		[SerializeField]
		[Min(1f)]
		private int _oxygenAmountPerTick = 1;

		[SerializeField]
		[Min(0.05f)]
		private float _oxygenTickInterval = 0.25f;

		[Header("Fart Trigger")]
		[SerializeField]
		private BoxCollider _fartCollider;

		private readonly HashSet<Collider> _localPlayerColliders = new HashSet<Collider>();

		private readonly List<Collider> _invalidLocalPlayerColliders = new List<Collider>();

		private LOD[] _cachedLods = new LOD[0];

		private DivingEquipment _localDivingEquipmentInCloud;

		private float _oxygenTickTimer;

		private bool _particlesStoppedByLod;

		private void Awake()
		{
			RefreshLodCache();
			SetFartColliderEnabled(enabled: true);
		}

		private void OnEnable()
		{
			SetFartColliderEnabled(enabled: true);
			UpdateParticleLodState();
		}

		private void OnDisable()
		{
			ClearLocalPlayerTracking();
			StopParticles();
		}

		private void OnValidate()
		{
			RefreshLodCache();
		}

		private void Update()
		{
			UpdateParticleLodState();
			ReconcileLocalPlayerTracking();
			TickLocalOxygenGain();
		}

		private void OnTriggerEnter(Collider other)
		{
			TryTrackLocalPlayerCollider(other);
		}

		private void OnTriggerStay(Collider other)
		{
			TryTrackLocalPlayerCollider(other);
		}

		private void OnTriggerExit(Collider other)
		{
			if (!(other == null) && _localPlayerColliders.Remove(other) && _localPlayerColliders.Count <= 0)
			{
				StopLocalOxygenGain();
			}
		}

		private void TryTrackLocalPlayerCollider(Collider other)
		{
			if (!(other == null) && !(_fartCollider == null) && _fartCollider.enabled && TryGetLocalDivingEquipment(other, out var divingEquipment))
			{
				_localPlayerColliders.Add(other);
				_localDivingEquipmentInCloud = divingEquipment;
			}
		}

		private void ReconcileLocalPlayerTracking()
		{
			if (_localPlayerColliders.Count == 0)
			{
				StopLocalOxygenGain();
				return;
			}
			_invalidLocalPlayerColliders.Clear();
			DivingEquipment divingEquipment = null;
			foreach (Collider localPlayerCollider in _localPlayerColliders)
			{
				if (!TryGetLocalDivingEquipment(localPlayerCollider, out var divingEquipment2) || !IsOverlappingFartTrigger(localPlayerCollider))
				{
					_invalidLocalPlayerColliders.Add(localPlayerCollider);
				}
				else if ((object)divingEquipment == null)
				{
					divingEquipment = divingEquipment2;
				}
			}
			for (int i = 0; i < _invalidLocalPlayerColliders.Count; i++)
			{
				_localPlayerColliders.Remove(_invalidLocalPlayerColliders[i]);
			}
			if (_localPlayerColliders.Count == 0 || divingEquipment == null)
			{
				StopLocalOxygenGain();
			}
			else
			{
				_localDivingEquipmentInCloud = divingEquipment;
			}
		}

		private static bool TryGetLocalDivingEquipment(Collider playerCollider, out DivingEquipment divingEquipment)
		{
			divingEquipment = null;
			if (playerCollider == null || !playerCollider.enabled || !playerCollider.gameObject.activeInHierarchy)
			{
				return false;
			}
			PPG_Player componentInParent = playerCollider.GetComponentInParent<PPG_Player>();
			if (componentInParent == null || !componentInParent.IsOwner || componentInParent.DivingEquipment == null)
			{
				return false;
			}
			divingEquipment = componentInParent.DivingEquipment;
			return true;
		}

		private bool IsOverlappingFartTrigger(Collider playerCollider)
		{
			if (_fartCollider == null || !_fartCollider.enabled || !_fartCollider.gameObject.activeInHierarchy)
			{
				return false;
			}
			Vector3 direction;
			float distance;
			return Physics.ComputePenetration(_fartCollider, _fartCollider.transform.position, _fartCollider.transform.rotation, playerCollider, playerCollider.transform.position, playerCollider.transform.rotation, out direction, out distance);
		}

		private void TickLocalOxygenGain()
		{
			if (_fartCollider == null || !_fartCollider.enabled)
			{
				return;
			}
			if (_localDivingEquipmentInCloud == null || _localPlayerColliders.Count == 0)
			{
				_oxygenTickTimer = 0f;
				return;
			}
			float num = Mathf.Max(0.05f, _oxygenTickInterval);
			_oxygenTickTimer += Time.deltaTime;
			while (_oxygenTickTimer >= num)
			{
				_oxygenTickTimer -= num;
				int num2 = Mathf.Clamp(_oxygenAmountPerTick, 1, 255);
				_localDivingEquipmentInCloud.ApplyOxygen((byte)num2);
				if (_localDivingEquipmentInCloud == null)
				{
					break;
				}
			}
		}

		private void UpdateParticleLodState()
		{
			if (IsLodCulled())
			{
				if (!_particlesStoppedByLod)
				{
					StopParticles();
					_particlesStoppedByLod = true;
				}
			}
			else
			{
				_particlesStoppedByLod = false;
				PlayParticles();
			}
		}

		private bool IsLodCulled()
		{
			if (_lodGroup == null || !_lodGroup.enabled || !_lodGroup.gameObject.activeInHierarchy)
			{
				return false;
			}
			if (_cachedLods == null || _cachedLods.Length == 0)
			{
				RefreshLodCache();
			}
			if (_cachedLods == null || _cachedLods.Length == 0)
			{
				return false;
			}
			Camera main = Camera.main;
			if (main == null)
			{
				return false;
			}
			Vector3 worldCenter = _lodGroup.transform.TransformPoint(_lodGroup.localReferencePoint);
			float worldSize = _lodGroup.size * MaxAbsComponent(_lodGroup.transform.lossyScale);
			float num = CalculateRelativeScreenHeight(main, worldCenter, worldSize);
			for (int i = 0; i < _cachedLods.Length; i++)
			{
				if (num >= _cachedLods[i].screenRelativeTransitionHeight)
				{
					return false;
				}
			}
			return true;
		}

		private static float CalculateRelativeScreenHeight(Camera camera, Vector3 worldCenter, float worldSize)
		{
			if (camera.orthographic)
			{
				return worldSize / Mathf.Max(0.0001f, camera.orthographicSize * 2f);
			}
			float num = Vector3.Distance(camera.transform.position, worldCenter);
			float f = camera.fieldOfView * 0.5f * (MathF.PI / 180f);
			return worldSize / Mathf.Max(0.0001f, num * 2f * Mathf.Tan(f)) * Mathf.Max(0.01f, QualitySettings.lodBias);
		}

		private static float MaxAbsComponent(Vector3 value)
		{
			return Mathf.Max(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
		}

		private void PlayParticles()
		{
			if (_bubbleStreamFX != null && !_bubbleStreamFX.isPlaying)
			{
				_bubbleStreamFX.Play();
			}
			if (_secondaryBubbleStreamFX != null && !_secondaryBubbleStreamFX.isPlaying)
			{
				_secondaryBubbleStreamFX.Play();
			}
		}

		private void StopParticles()
		{
			if (_bubbleStreamFX != null && _bubbleStreamFX.isPlaying)
			{
				_bubbleStreamFX.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmitting);
			}
			if (_secondaryBubbleStreamFX != null && _secondaryBubbleStreamFX.isPlaying)
			{
				_secondaryBubbleStreamFX.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmitting);
			}
		}

		private void RefreshLodCache()
		{
			_cachedLods = ((_lodGroup != null) ? _lodGroup.GetLODs() : new LOD[0]);
		}

		private void SetFartColliderEnabled(bool enabled)
		{
			if (_fartCollider == null)
			{
				UtilityMethods.Log("[OxyFarter] Missing fart collider reference.", LogType.Warning);
				return;
			}
			_fartCollider.enabled = enabled;
			if (!enabled)
			{
				ClearLocalPlayerTracking();
			}
		}

		private void StopLocalOxygenGain()
		{
			_localDivingEquipmentInCloud = null;
			_oxygenTickTimer = 0f;
		}

		private void ClearLocalPlayerTracking()
		{
			_localPlayerColliders.Clear();
			_invalidLocalPlayerColliders.Clear();
			StopLocalOxygenGain();
		}
	}
}
