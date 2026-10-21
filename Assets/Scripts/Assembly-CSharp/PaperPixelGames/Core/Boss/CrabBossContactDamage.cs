using System.Collections.Generic;
using PaperPixelGames.Core.Player;
using UnityEngine;

namespace PaperPixelGames.Core.Boss
{
	[DisallowMultipleComponent]
	public sealed class CrabBossContactDamage : MonoBehaviour
	{
		[Header("References")]
		[SerializeField]
		private CrabBossController _boss;

		[SerializeField]
		private Transform _pushOriginOverride;

		[Header("Hit Filter")]
		[SerializeField]
		private LayerMask _playerLayers = -1;

		[SerializeField]
		private bool _ignoreKnockedPlayers = true;

		[SerializeField]
		[Min(0f)]
		private float _hitCooldownSeconds = 1f;

		[Header("Hit Response")]
		[SerializeField]
		private byte _oxygenDamage = 10;

		[SerializeField]
		[Min(0f)]
		private float _knockbackImpulse = 9f;

		[SerializeField]
		[Min(0f)]
		private float _knockbackUpwardForce = 1f;

		private readonly Dictionary<ulong, float> _nextHitTimeByClientId = new Dictionary<ulong, float>();

		private uint _suppressionToken;

		public void ServerBeginSuppression(uint token)
		{
			if (!(_boss == null) && _boss.IsServer && token != 0)
			{
				_suppressionToken = token;
			}
		}

		public void ServerEndSuppression(uint token, PPG_Player cooldownPlayer = null)
		{
			if (!(_boss == null) && _boss.IsServer && token != 0 && token == _suppressionToken)
			{
				_suppressionToken = 0u;
				if (cooldownPlayer != null && cooldownPlayer.IsSpawned && _hitCooldownSeconds > 0f)
				{
					_nextHitTimeByClientId[cooldownPlayer.OwnerClientId] = Time.time + _hitCooldownSeconds;
				}
			}
		}

		private void Reset()
		{
			_boss = GetComponentInParent<CrabBossController>();
		}

		private void Awake()
		{
			if (_boss == null)
			{
				_boss = GetComponentInParent<CrabBossController>();
			}
		}

		private void OnTriggerEnter(Collider other)
		{
			TryHandlePlayerContact(other);
		}

		private void OnTriggerStay(Collider other)
		{
			TryHandlePlayerContact(other);
		}

		private void OnCollisionEnter(Collision collision)
		{
			TryHandlePlayerContact(collision?.collider);
		}

		private void OnCollisionStay(Collision collision)
		{
			TryHandlePlayerContact(collision?.collider);
		}

		private void TryHandlePlayerContact(Collider other)
		{
			if (_boss == null || !_boss.IsServer || other == null || _suppressionToken != 0)
			{
				return;
			}
			PPG_Player componentInParent = other.GetComponentInParent<PPG_Player>();
			if (componentInParent == null || !componentInParent.IsSpawned || !IsAllowedLayer(other, componentInParent) || (_ignoreKnockedPlayers && componentInParent.DivingEquipment != null && componentInParent.DivingEquipment.IsKnocked))
			{
				return;
			}
			ulong ownerClientId = componentInParent.OwnerClientId;
			float time = Time.time;
			if (!(_hitCooldownSeconds > 0f) || !_nextHitTimeByClientId.TryGetValue(ownerClientId, out var value) || !(time < value))
			{
				Vector3 pushOrigin = ((_pushOriginOverride != null) ? _pushOriginOverride.position : _boss.transform.position);
				if (_boss.ServerTryApplyContactHit(componentInParent, pushOrigin, _oxygenDamage, _knockbackImpulse, _knockbackUpwardForce) && _hitCooldownSeconds > 0f)
				{
					_nextHitTimeByClientId[ownerClientId] = time + _hitCooldownSeconds;
				}
			}
		}

		private bool IsAllowedLayer(Collider other, PPG_Player player)
		{
			int value = _playerLayers.value;
			if (value == 0)
			{
				return true;
			}
			if (other != null && (value & (1 << other.gameObject.layer)) != 0)
			{
				return true;
			}
			if (player != null)
			{
				return (value & (1 << player.gameObject.layer)) != 0;
			}
			return false;
		}
	}
}
