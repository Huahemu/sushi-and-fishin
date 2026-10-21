using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Patterns;
using StylizedWater3.UnderwaterRendering;
using UnityEngine;

namespace PaperPixelGames.Core.Boss
{
	[DisallowMultipleComponent]
	[RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
	public sealed class CrabBossReturnPortal : MonoBehaviour
	{
		[SerializeField]
		private SphereCollider _triggerCollider;

		[SerializeField]
		private GameObject _loopVfx;

		[Tooltip("Optional destination override. Otherwise uses PlayersManager.RestaurantSpawnPoint.")]
		[SerializeField]
		private Transform _restaurantDestination;

		private const float TeleportCooldownSeconds = 1f;

		private bool _isAvailable;

		private PPG_Player _pendingPlayer;

		private float _nextTeleportTime;

		private void Awake()
		{
			InitializePhysics();
			ApplyAvailability();
		}

		private void OnEnable()
		{
			ApplyAvailability();
		}

		private void OnDisable()
		{
			_pendingPlayer = null;
			ApplyAvailability();
		}

		public void Configure(Vector3 worldPosition, float radius)
		{
			InitializePhysics();
			base.transform.position = worldPosition;
			_triggerCollider.center = Vector3.zero;
			Vector3 lossyScale = base.transform.lossyScale;
			float b = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z));
			_triggerCollider.radius = Mathf.Max(0.1f, radius) / Mathf.Max(0.0001f, b);
		}

		public void SetAvailable(bool available)
		{
			InitializePhysics();
			if (_isAvailable != available)
			{
				_pendingPlayer = null;
				_nextTeleportTime = 0f;
			}
			_isAvailable = available;
			ApplyAvailability();
		}

		private void InitializePhysics()
		{
			if (_triggerCollider == null)
			{
				_triggerCollider = GetComponent<SphereCollider>();
			}
			_triggerCollider.isTrigger = true;
			Rigidbody component = GetComponent<Rigidbody>();
			component.isKinematic = true;
			component.useGravity = false;
		}

		private void ApplyAvailability()
		{
			bool flag = _isAvailable && base.isActiveAndEnabled;
			if (_triggerCollider != null)
			{
				_triggerCollider.enabled = flag;
			}
			if (_loopVfx != null && _loopVfx.activeSelf != flag)
			{
				_loopVfx.SetActive(flag);
			}
		}

		private void OnTriggerEnter(Collider other)
		{
			QueueLocalPlayer(other);
		}

		private void OnTriggerStay(Collider other)
		{
			QueueLocalPlayer(other);
		}

		private void QueueLocalPlayer(Collider other)
		{
			if (_isAvailable && base.isActiveAndEnabled && !(other == null) && !(Time.unscaledTime < _nextTeleportTime) && !(_pendingPlayer != null))
			{
				PPG_Player componentInParent = other.GetComponentInParent<PPG_Player>();
				if (CanTeleport(componentInParent) && other is CapsuleCollider && other == componentInParent.FirstPersonCharacter.characterMovement.collider)
				{
					_pendingPlayer = componentInParent;
				}
			}
		}

		private void LateUpdate()
		{
			PPG_Player pendingPlayer = _pendingPlayer;
			_pendingPlayer = null;
			if (!_isAvailable || !CanTeleport(pendingPlayer) || Time.unscaledTime < _nextTeleportTime)
			{
				return;
			}
			Transform transform = ((_restaurantDestination != null) ? _restaurantDestination : Singleton<PlayersManager>.Instance?.RestaurantSpawnPoint);
			if (!(transform == null))
			{
				_nextTeleportTime = Time.unscaledTime + 1f;
				CrabBossRoomTrigger.ClearLocalBossUiForRespawn();
				pendingPlayer.TeleportPlayer(transform.position, transform.rotation, updateGround: false);
				pendingPlayer.FirstPersonCharacter.characterMovement.velocity = Vector3.zero;
				UnderwaterArea underwaterArea = pendingPlayer.FirstPersonCharacterInput?.UnderwaterArea;
				if (underwaterArea != null && underwaterArea.shadingSettings != null)
				{
					underwaterArea.shadingSettings.fogDensity = 0.8f;
				}
			}
		}

		private static bool CanTeleport(PPG_Player player)
		{
			if (player != null && player.isActiveAndEnabled && player.IsSpawned && player.IsOwner && player.FirstPersonCharacter != null && player.FirstPersonCharacter.characterMovement != null)
			{
				if (!(player.DivingEquipment == null))
				{
					return !player.DivingEquipment.IsKnocked;
				}
				return true;
			}
			return false;
		}
	}
}
