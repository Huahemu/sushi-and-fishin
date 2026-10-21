using System.Collections.Generic;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Ocean
{
	public sealed class SharkCage : NetworkBehaviour
	{
		[Header("Shark Cage")]
		[SerializeField]
		private Collider _occupancyTrigger;

		[SerializeField]
		private DoorBehaviour _door;

		private readonly Dictionary<ulong, HashSet<int>> _colliderIdsByClient = new Dictionary<ulong, HashSet<int>>();

		private readonly Dictionary<ulong, PPG_Player> _playersByClient = new Dictionary<ulong, PPG_Player>();

		private readonly Dictionary<int, ulong> _clientIdsByColliderId = new Dictionary<int, ulong>();

		public bool IsDoorOpen
		{
			get
			{
				if (_door != null)
				{
					return _door.IsOpen;
				}
				return false;
			}
		}

		private void Awake()
		{
			if (_occupancyTrigger == null || !_occupancyTrigger.isTrigger)
			{
				UtilityMethods.Log("[SharkCage] Occupancy trigger is missing or Is Trigger is disabled.", LogType.Error);
				base.enabled = false;
			}
			else if (_door == null)
			{
				UtilityMethods.Log("[SharkCage] DoorBehaviour is not assigned.", LogType.Error);
				base.enabled = false;
			}
		}

		public override void OnNetworkDespawn()
		{
			if (base.IsServer)
			{
				ServerClearOccupants();
			}
			base.OnNetworkDespawn();
		}

		private void OnTriggerEnter(Collider other)
		{
			if (!base.IsServer || !TryResolvePlayer(other, out var player))
			{
				return;
			}
			ulong ownerClientId = player.OwnerClientId;
			if (!_colliderIdsByClient.TryGetValue(ownerClientId, out var value))
			{
				value = new HashSet<int>();
				_colliderIdsByClient[ownerClientId] = value;
			}
			int instanceID = other.GetInstanceID();
			bool flag = value.Count == 0;
			if (value.Add(instanceID))
			{
				_clientIdsByColliderId[instanceID] = ownerClientId;
				if (flag)
				{
					_playersByClient[ownerClientId] = player;
					player.ServerEnterSharkCage(this);
				}
			}
		}

		private void OnTriggerExit(Collider other)
		{
			if (!base.IsServer || other == null)
			{
				return;
			}
			int instanceID = other.GetInstanceID();
			if (_clientIdsByColliderId.Remove(instanceID, out var value) && _colliderIdsByClient.TryGetValue(value, out var value2) && value2.Remove(instanceID) && value2.Count <= 0)
			{
				_colliderIdsByClient.Remove(value);
				if (_playersByClient.Remove(value, out var value3) && value3 != null)
				{
					value3.ServerExitSharkCage(this);
				}
			}
		}

		internal void ServerRemovePlayer(PPG_Player player)
		{
			if (!base.IsServer || player == null)
			{
				return;
			}
			ulong ownerClientId = player.OwnerClientId;
			if (_colliderIdsByClient.Remove(ownerClientId, out var value))
			{
				foreach (int item in value)
				{
					_clientIdsByColliderId.Remove(item);
				}
			}
			_playersByClient.Remove(ownerClientId);
			player.ServerExitSharkCage(this);
		}

		private void ServerClearOccupants()
		{
			foreach (PPG_Player value in _playersByClient.Values)
			{
				if (value != null)
				{
					value.ServerExitSharkCage(this);
				}
			}
			_colliderIdsByClient.Clear();
			_playersByClient.Clear();
			_clientIdsByColliderId.Clear();
		}

		private static bool TryResolvePlayer(Collider other, out PPG_Player player)
		{
			player = ((other != null) ? other.GetComponentInParent<PPG_Player>() : null);
			if (player != null)
			{
				return player.IsSpawned;
			}
			return false;
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
			return "SharkCage";
		}
	}
}
