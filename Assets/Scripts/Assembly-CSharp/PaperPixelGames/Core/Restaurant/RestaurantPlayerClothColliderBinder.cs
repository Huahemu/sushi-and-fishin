using System.Collections.Generic;
using MagicaCloth2;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using UnityEngine;

namespace PaperPixelGames.Core.Restaurant
{
	[DisallowMultipleComponent]
	public class RestaurantPlayerClothColliderBinder : MonoBehaviour
	{
		private sealed class PlayerColliderBinding
		{
			public Transform PlayerTransform;

			public List<ColliderComponent> Colliders;

			public bool IsBound;
		}

		[SerializeField]
		private List<MagicaCloth> _targetCloths = new List<MagicaCloth>();

		[SerializeField]
		[Min(0f)]
		private float _colliderBindDistance = 8f;

		[SerializeField]
		[Min(0f)]
		private float _colliderUnbindDistance = 11f;

		[SerializeField]
		[Min(0.05f)]
		private float _proximityRefreshInterval = 0.2f;

		private readonly Dictionary<ulong, PlayerColliderBinding> _playerBindings = new Dictionary<ulong, PlayerColliderBinding>();

		private readonly List<Renderer> _clothRenderers = new List<Renderer>();

		private PlayersManager _playersManager;

		private bool _hasStarted;

		private float _nextProximityRefreshTime;

		private void OnEnable()
		{
			if (_hasStarted)
			{
				BindPlayersManager();
			}
		}

		private void Start()
		{
			_hasStarted = true;
			CacheClothRenderers();
			BindPlayersManager();
		}

		private void Update()
		{
			if (_playersManager == null)
			{
				BindPlayersManager();
			}
			if (!(Time.unscaledTime < _nextProximityRefreshTime))
			{
				_nextProximityRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, _proximityRefreshInterval);
				RefreshPlayerColliderBindings();
			}
		}

		private void OnDisable()
		{
			UnbindPlayersManager();
			ClearPlayerColliders();
		}

		private void BindPlayersManager()
		{
			if (_playersManager != null)
			{
				return;
			}
			_playersManager = Singleton<PlayersManager>.Instance;
			if (_playersManager == null)
			{
				UtilityMethods.Log("[RestaurantPlayerClothColliderBinder] PlayersManager was not available during initialization.", LogType.Error);
				return;
			}
			_playersManager.PlayerRegistered += OnPlayerRegistered;
			_playersManager.PlayerUnregistered += OnPlayerUnregistered;
			foreach (PPG_Player value in _playersManager.Players.Values)
			{
				RegisterPlayerColliders(value);
			}
		}

		private void UnbindPlayersManager()
		{
			if (!(_playersManager == null))
			{
				_playersManager.PlayerRegistered -= OnPlayerRegistered;
				_playersManager.PlayerUnregistered -= OnPlayerUnregistered;
				_playersManager = null;
			}
		}

		private void OnPlayerRegistered(PPG_Player player)
		{
			RegisterPlayerColliders(player);
		}

		private void OnPlayerUnregistered(PPG_Player player)
		{
			if (player != null)
			{
				UnregisterPlayerColliders(player.OwnerClientId);
			}
		}

		private void RegisterPlayerColliders(PPG_Player player)
		{
			if (player == null)
			{
				return;
			}
			ulong ownerClientId = player.OwnerClientId;
			if (_playerBindings.ContainsKey(ownerClientId))
			{
				return;
			}
			PlayerRigBoneReferences rigBones = player.RigBones;
			if (rigBones == null)
			{
				UtilityMethods.Log($"[RestaurantPlayerClothColliderBinder] Player rig bones are missing. ClientId={ownerClientId}", LogType.Warning);
				return;
			}
			ColliderComponent tpsHeadMagicaCollider = rigBones.TpsHeadMagicaCollider;
			ColliderComponent tpsSpineMagicaCollider = rigBones.TpsSpineMagicaCollider;
			List<ColliderComponent> list = new List<ColliderComponent>(2);
			AddUniqueCollider(list, tpsHeadMagicaCollider);
			AddUniqueCollider(list, tpsSpineMagicaCollider);
			if (tpsHeadMagicaCollider == null || tpsSpineMagicaCollider == null)
			{
				UtilityMethods.Log("[RestaurantPlayerClothColliderBinder] TPS Magica collider resolution is incomplete. " + $"ClientId={ownerClientId}, Head={GetColliderName(tpsHeadMagicaCollider)}, " + "Spine=" + GetColliderName(tpsSpineMagicaCollider), LogType.Warning);
			}
			if (list.Count != 0)
			{
				PlayerColliderBinding playerColliderBinding = new PlayerColliderBinding
				{
					PlayerTransform = player.transform,
					Colliders = list
				};
				_playerBindings.Add(ownerClientId, playerColliderBinding);
				RefreshPlayerColliderBinding(playerColliderBinding);
			}
		}

		private void UnregisterPlayerColliders(ulong clientId)
		{
			if (_playerBindings.Remove(clientId, out var value) && value.IsBound)
			{
				RemoveCollidersFromTargetCloths(value.Colliders);
			}
		}

		private void ClearPlayerColliders()
		{
			if (_playerBindings.Count == 0)
			{
				return;
			}
			List<ColliderComponent> colliders = new List<ColliderComponent>(_playerBindings.Count * 2);
			foreach (PlayerColliderBinding value in _playerBindings.Values)
			{
				if (value.IsBound)
				{
					for (int i = 0; i < value.Colliders.Count; i++)
					{
						AddUniqueTrackedCollider(colliders, value.Colliders[i]);
					}
				}
			}
			_playerBindings.Clear();
			RemoveCollidersFromTargetCloths(colliders);
		}

		private void RefreshPlayerColliderBindings()
		{
			foreach (PlayerColliderBinding value in _playerBindings.Values)
			{
				RefreshPlayerColliderBinding(value);
			}
		}

		private void RefreshPlayerColliderBinding(PlayerColliderBinding binding)
		{
			if (binding.PlayerTransform == null)
			{
				if (binding.IsBound)
				{
					RemoveCollidersFromTargetCloths(binding.Colliders);
					binding.IsBound = false;
				}
				return;
			}
			float distance = (binding.IsBound ? Mathf.Max(_colliderBindDistance, _colliderUnbindDistance) : Mathf.Max(0f, _colliderBindDistance));
			bool flag = IsNearTargetCloth(binding.PlayerTransform.position, distance);
			if (flag != binding.IsBound)
			{
				if (flag)
				{
					AddCollidersToTargetCloths(binding.Colliders);
				}
				else
				{
					RemoveCollidersFromTargetCloths(binding.Colliders);
				}
				binding.IsBound = flag;
			}
		}

		private bool IsNearTargetCloth(Vector3 playerPosition, float distance)
		{
			float num = distance * distance;
			for (int i = 0; i < _clothRenderers.Count; i++)
			{
				Renderer renderer = _clothRenderers[i];
				if (renderer != null && renderer.bounds.SqrDistance(playerPosition) <= num)
				{
					return true;
				}
			}
			if (_clothRenderers.Count == 0)
			{
				for (int j = 0; j < _targetCloths.Count; j++)
				{
					MagicaCloth magicaCloth = _targetCloths[j];
					if (magicaCloth != null && (magicaCloth.transform.position - playerPosition).sqrMagnitude <= num)
					{
						return true;
					}
				}
			}
			return false;
		}

		private void CacheClothRenderers()
		{
			_clothRenderers.Clear();
			for (int i = 0; i < _targetCloths.Count; i++)
			{
				MagicaCloth magicaCloth = _targetCloths[i];
				if (!(magicaCloth == null))
				{
					Renderer component = magicaCloth.GetComponent<Renderer>();
					AddUniqueRenderer(component);
					Renderer[] componentsInChildren = magicaCloth.GetComponentsInChildren<Renderer>(includeInactive: true);
					for (int j = 0; j < componentsInChildren.Length; j++)
					{
						AddUniqueRenderer(componentsInChildren[j]);
					}
				}
			}
		}

		private void AddUniqueRenderer(Renderer renderer)
		{
			if (renderer != null && !_clothRenderers.Contains(renderer))
			{
				_clothRenderers.Add(renderer);
			}
		}

		private void AddCollidersToTargetCloths(List<ColliderComponent> colliders)
		{
			for (int i = 0; i < _targetCloths.Count; i++)
			{
				MagicaCloth magicaCloth = _targetCloths[i];
				if (magicaCloth == null)
				{
					continue;
				}
				List<ColliderComponent> colliderList = magicaCloth.SerializeData.colliderCollisionConstraint.colliderList;
				bool flag = false;
				for (int j = 0; j < colliders.Count; j++)
				{
					ColliderComponent colliderComponent = colliders[j];
					if (!(colliderComponent == null) && !colliderList.Contains(colliderComponent))
					{
						colliderList.Add(colliderComponent);
						flag = true;
					}
				}
				if (flag)
				{
					magicaCloth.SetParameterChange();
				}
			}
		}

		private void RemoveCollidersFromTargetCloths(List<ColliderComponent> colliders)
		{
			for (int i = 0; i < _targetCloths.Count; i++)
			{
				MagicaCloth magicaCloth = _targetCloths[i];
				if (magicaCloth == null)
				{
					continue;
				}
				List<ColliderComponent> colliderList = magicaCloth.SerializeData.colliderCollisionConstraint.colliderList;
				bool flag = false;
				for (int j = 0; j < colliders.Count; j++)
				{
					ColliderComponent item = colliders[j];
					while (colliderList.Remove(item))
					{
						flag = true;
					}
				}
				if (flag)
				{
					magicaCloth.SetParameterChange();
				}
			}
		}

		private static void AddUniqueCollider(List<ColliderComponent> colliders, ColliderComponent collider)
		{
			if (collider != null && !colliders.Contains(collider))
			{
				colliders.Add(collider);
			}
		}

		private static void AddUniqueTrackedCollider(List<ColliderComponent> colliders, ColliderComponent collider)
		{
			if ((object)collider != null && !colliders.Contains(collider))
			{
				colliders.Add(collider);
			}
		}

		private static string GetColliderName(ColliderComponent collider)
		{
			if (!(collider != null))
			{
				return "Missing";
			}
			return collider.name;
		}
	}
}
