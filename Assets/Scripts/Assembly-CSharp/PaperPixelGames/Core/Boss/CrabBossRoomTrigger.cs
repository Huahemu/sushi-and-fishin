using System.Collections.Generic;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using UnityEngine;

namespace PaperPixelGames.Core.Boss
{
	[RequireComponent(typeof(Collider))]
	public sealed class CrabBossRoomTrigger : MonoBehaviour
	{
		[SerializeField]
		private bool _log;

		private static CrabBossRoomTrigger _localActiveRoom;

		private static readonly HashSet<CrabBossRoomTrigger> ActiveRooms = new HashSet<CrabBossRoomTrigger>();

		private const float PresenceReconcileInterval = 0.25f;

		private readonly HashSet<ulong> _trackedClientIds = new HashSet<ulong>();

		private readonly List<ulong> _exitedClientIds = new List<ulong>();

		private readonly HashSet<Collider> _localPlayerCollidersInRoom = new HashSet<Collider>();

		private readonly List<Collider> _playerColliderBuffer = new List<Collider>();

		[SerializeField]
		private CrabBossController _boss;

		[SerializeField]
		private CrabBossPresentationController _presentation;

		[SerializeField]
		private Transform _bossFightRespawnPointsRoot;

		private Collider _triggerCollider;

		private float _nextPresenceReconcileTime;

		internal bool IsForBoss(CrabBossController boss)
		{
			return _boss == boss;
		}

		internal void SetEncounterAvailable(bool available)
		{
			if (_triggerCollider != null)
			{
				_triggerCollider.enabled = available;
			}
			base.enabled = available;
		}

		private void Awake()
		{
			if (_boss == null)
			{
				UtilityMethods.Log("[CrabBoss][RoomTrigger] CrabBossController could not be found in the parent hierarchy.", LogType.Error);
				base.enabled = false;
				return;
			}
			if (_presentation == null)
			{
				_presentation = _boss.GetComponent<CrabBossPresentationController>();
			}
			if (!TryGetComponent<Collider>(out _triggerCollider) || !_triggerCollider.isTrigger)
			{
				UtilityMethods.Log("[CrabBoss][RoomTrigger] Collider is missing or Is Trigger is disabled.", LogType.Error);
				base.enabled = false;
			}
		}

		private void OnDisable()
		{
			ActiveRooms.Remove(this);
			_localPlayerCollidersInRoom.Clear();
			ClearLocalBossUiIfActive();
			if (_boss != null && _boss.IsServer)
			{
				foreach (ulong trackedClientId in _trackedClientIds)
				{
					NotifyServerExitIfOutsideAllRooms(trackedClientId);
				}
			}
			_trackedClientIds.Clear();
		}

		private void OnEnable()
		{
			ActiveRooms.Add(this);
			_nextPresenceReconcileTime = 0f;
		}

		public static bool ServerIsPlayerInsideBossRoom(CrabBossController boss, PPG_Player player)
		{
			if (boss == null || player == null)
			{
				return false;
			}
			foreach (CrabBossRoomTrigger activeRoom in ActiveRooms)
			{
				if (activeRoom != null && activeRoom._boss == boss && activeRoom.IsPlayerStillInsideTrigger(player))
				{
					return true;
				}
			}
			return false;
		}

		private void Update()
		{
			if (Time.unscaledTime >= _nextPresenceReconcileTime)
			{
				_nextPresenceReconcileTime = Time.unscaledTime + 0.25f;
				ReconcileLocalPresence();
				ReconcileServerPresence();
			}
			if (_localActiveRoom == this && _boss != null && _boss.EncounterState == CrabBossEncounterState.Defeated)
			{
				Singleton<GameUIManager>.Instance?.SetBossRoomHudSuppressed(suppressed: false);
			}
		}

		private void ReconcileLocalPresence()
		{
			if (_boss == null || !_boss.IsSpawned || !_boss.IsClient)
			{
				_localPlayerCollidersInRoom.Clear();
				ClearLocalBossUiIfActive();
				return;
			}
			PPG_Player pPG_Player = Singleton<PlayersManager>.Instance?.LocalPlayer;
			if (pPG_Player == null || !pPG_Player.IsSpawned || !pPG_Player.IsOwner || !IsPlayerStillInsideTrigger(pPG_Player))
			{
				if (_localActiveRoom == this)
				{
					_localPlayerCollidersInRoom.Clear();
					ClearLocalBossUiIfActive();
					_boss.RequestPlayerExitedRoomFromLocalTrigger();
				}
			}
			else if (!(_localActiveRoom == this) && (!(_localActiveRoom != null) || !_localActiveRoom.IsPlayerStillInsideTrigger(pPG_Player)))
			{
				ActivateLocalBossUi();
				_boss.RequestPlayerEnteredRoomFromLocalTrigger();
			}
		}

		private void ActivateLocalBossUi()
		{
			if (_localActiveRoom != null && _localActiveRoom != this)
			{
				_localActiveRoom.ClearLocalBossUiIfActive();
			}
			_localActiveRoom = this;
			_presentation?.SetLocalPlayerInRoom(isInRoom: true);
			Singleton<GameUIManager>.Instance?.SetBossRoomHudSuppressed(_boss != null && _boss.EncounterState != CrabBossEncounterState.Defeated);
		}

		private void ReconcileServerPresence()
		{
			if (!CanProcessServerTrigger() || Singleton<PlayersManager>.Instance == null)
			{
				return;
			}
			foreach (PPG_Player value in Singleton<PlayersManager>.Instance.Players.Values)
			{
				if (value != null && value.IsSpawned && IsPlayerStillInsideTrigger(value))
				{
					ServerTrackPlayerEnter(value, "presence reconciliation");
				}
				else if (value != null && _boss.ServerIsPlayerInRoom(value.OwnerClientId))
				{
					_trackedClientIds.Add(value.OwnerClientId);
				}
			}
			_exitedClientIds.Clear();
			foreach (ulong trackedClientId in _trackedClientIds)
			{
				if (!Singleton<PlayersManager>.Instance.TryGetPlayer(trackedClientId, out var player) || player == null || !player.IsSpawned || !IsPlayerStillInsideTrigger(player))
				{
					_exitedClientIds.Add(trackedClientId);
				}
			}
			for (int i = 0; i < _exitedClientIds.Count; i++)
			{
				ServerTrackPlayerExit(_exitedClientIds[i], "presence reconciliation");
			}
		}

		private void OnTriggerEnter(Collider other)
		{
			if (!base.isActiveAndEnabled)
			{
				return;
			}
			bool flag = ProcessLocalPlayerUiEnter(other);
			PPG_Player pPG_Player = ((other != null) ? other.GetComponentInParent<PPG_Player>() : null);
			if (!(pPG_Player == null) && pPG_Player.IsSpawned)
			{
				if (CanProcessServerTrigger())
				{
					ServerTrackPlayerEnter(pPG_Player, "server physics trigger");
				}
				else if (flag && pPG_Player.IsOwner && _boss != null && _boss.IsSpawned && _boss.IsClient)
				{
					_boss.RequestPlayerEnteredRoomFromLocalTrigger();
					Log($"Local owner trigger entered; server notified. ClientId={pPG_Player.OwnerClientId}.");
				}
			}
		}

		private void OnTriggerExit(Collider other)
		{
			if (!base.isActiveAndEnabled)
			{
				return;
			}
			bool flag = ProcessLocalPlayerUiExit(other);
			if (!CanProcessServerTrigger())
			{
				if (flag && _boss != null && _boss.IsSpawned && _boss.IsClient)
				{
					_boss.RequestPlayerExitedRoomFromLocalTrigger();
					Log("Local owner trigger exited; server notified.");
				}
				return;
			}
			PPG_Player pPG_Player = ((other != null) ? other.GetComponentInParent<PPG_Player>() : null);
			if (!(pPG_Player == null) && !IsPlayerStillInsideTrigger(pPG_Player))
			{
				ulong ownerClientId = pPG_Player.OwnerClientId;
				ServerTrackPlayerExit(ownerClientId, "server physics trigger");
			}
		}

		private void ServerTrackPlayerEnter(PPG_Player player, string source)
		{
			if (!(player == null) && player.IsSpawned)
			{
				ulong ownerClientId = player.OwnerClientId;
				bool num = _trackedClientIds.Add(ownerClientId);
				_boss.ServerPlayerEnteredRoom(player);
				if (num)
				{
					Log($"Player entered. ClientId={ownerClientId} Source={source}.");
				}
			}
		}

		private void ServerTrackPlayerExit(ulong clientId, string source)
		{
			if (_trackedClientIds.Remove(clientId))
			{
				NotifyServerExitIfOutsideAllRooms(clientId);
				Log($"Player exited. ClientId={clientId} Source={source}.");
			}
		}

		private void NotifyServerExitIfOutsideAllRooms(ulong clientId)
		{
			if (!(Singleton<PlayersManager>.Instance != null) || !Singleton<PlayersManager>.Instance.TryGetPlayer(clientId, out var player) || !(player != null) || !player.IsSpawned || !ServerIsPlayerInsideBossRoom(_boss, player))
			{
				_boss.ServerPlayerExitedRoom(clientId);
			}
		}

		private bool ProcessLocalPlayerUiEnter(Collider other)
		{
			if (!TryGetLocalPlayer(other, out var player))
			{
				return false;
			}
			if (other == null || !_localPlayerCollidersInRoom.Add(other))
			{
				return false;
			}
			if (_localPlayerCollidersInRoom.Count == 1)
			{
				if (_localActiveRoom == null || _localActiveRoom == this || !_localActiveRoom.IsPlayerStillInsideTrigger(player))
				{
					ActivateLocalBossUi();
				}
				return true;
			}
			return false;
		}

		private bool ProcessLocalPlayerUiExit(Collider other)
		{
			if (!TryGetLocalPlayer(other, out var player))
			{
				return false;
			}
			if (IsPlayerStillInsideTrigger(player))
			{
				return false;
			}
			_localPlayerCollidersInRoom.Clear();
			ClearLocalBossUiIfActive();
			return true;
		}

		private static bool TryGetLocalPlayer(Collider other, out PPG_Player player)
		{
			player = ((other != null) ? other.GetComponentInParent<PPG_Player>() : null);
			if (player != null && player.IsSpawned)
			{
				return player.IsOwner;
			}
			return false;
		}

		public static bool TryGetLocalBossRespawnPoint(out Transform respawnPoint)
		{
			respawnPoint = null;
			if (_localActiveRoom == null)
			{
				return false;
			}
			return _localActiveRoom.TryResolveBossRespawnPoint(out respawnPoint);
		}

		public static void ClearLocalBossUiForRespawn()
		{
			if (!(_localActiveRoom == null))
			{
				_localActiveRoom._localPlayerCollidersInRoom.Clear();
				_localActiveRoom.ClearLocalBossUiIfActive();
			}
		}

		private void ClearLocalBossUiIfActive()
		{
			if (!(_localActiveRoom != this))
			{
				_localActiveRoom = null;
				_presentation?.SetLocalPlayerInRoom(isInRoom: false);
				Singleton<GameUIManager>.Instance?.SetBossRoomHudSuppressed(suppressed: false);
			}
		}

		private bool TryResolveBossRespawnPoint(out Transform respawnPoint)
		{
			respawnPoint = null;
			if (_bossFightRespawnPointsRoot == null)
			{
				return false;
			}
			int childCount = _bossFightRespawnPointsRoot.childCount;
			if (childCount <= 0)
			{
				return false;
			}
			int index = Random.Range(0, childCount);
			respawnPoint = _bossFightRespawnPointsRoot.GetChild(index);
			return respawnPoint != null;
		}

		private bool IsPlayerStillInsideTrigger(PPG_Player player)
		{
			if (!base.isActiveAndEnabled || _triggerCollider == null || player == null)
			{
				return false;
			}
			if (IsPointInsideTrigger(player.transform.position))
			{
				return true;
			}
			_playerColliderBuffer.Clear();
			player.GetComponentsInChildren(includeInactive: false, _playerColliderBuffer);
			for (int i = 0; i < _playerColliderBuffer.Count; i++)
			{
				Collider collider = _playerColliderBuffer[i];
				if (!(collider == null) && collider.enabled && !(collider == _triggerCollider) && IsPointInsideTrigger(collider.bounds.center))
				{
					return true;
				}
			}
			return false;
		}

		private bool IsPointInsideTrigger(Vector3 point)
		{
			if (_triggerCollider == null || !_triggerCollider.enabled)
			{
				return false;
			}
			return (_triggerCollider.ClosestPoint(point) - point).sqrMagnitude <= 1E-06f;
		}

		private bool CanProcessServerTrigger()
		{
			if (base.isActiveAndEnabled && _boss != null && _boss.IsSpawned)
			{
				return _boss.IsServer;
			}
			return false;
		}

		private void Log(string message)
		{
			if (_log)
			{
				UtilityMethods.Log("[CrabBoss][RoomTrigger] " + message);
			}
		}
	}
}
