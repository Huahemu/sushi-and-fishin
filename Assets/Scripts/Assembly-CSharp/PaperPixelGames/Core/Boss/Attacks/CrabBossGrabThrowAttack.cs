using System;
using System.Collections;
using System.Collections.Generic;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Boss.Attacks
{
	[RequireComponent(typeof(CrabBossPresentationController))]
	public sealed class CrabBossGrabThrowAttack : CrabBossAttackBase, IAttachableObject
	{
		private const int GrabOverlapCapacity = 32;

		[Header("Grab References")]
		[SerializeField]
		private Transform _grabPoint;

		[SerializeField]
		private string _grabBoneName = "lowerarm_r";

		[SerializeField]
		private Transform _forwardReference;

		[SerializeField]
		private CrabBossContactDamage _contactDamage;

		[Header("Targeting")]
		[SerializeField]
		private LayerMask _playerLayers = -1;

		[SerializeField]
		[Min(0.01f)]
		private float _activationRange = 6f;

		[SerializeField]
		[Range(0f, 180f)]
		private float _activationHalfAngle = 45f;

		[SerializeField]
		[Min(0.01f)]
		private float _grabRadius = 1f;

		[SerializeField]
		private float _playerHitCenterOffset = 1f;

		[Header("Throw Response")]
		[SerializeField]
		private byte _oxygenDamage = 20;

		[SerializeField]
		[Min(0.01f)]
		private float _throwImpulse = 12f;

		[SerializeField]
		[Min(0f)]
		private float _throwUpwardBias = 0.2f;

		[Header("Debug")]
		[SerializeField]
		private bool _log;

		private readonly List<PPG_Player> _roomPlayers = new List<PPG_Player>();

		private readonly List<AttachSeat> _seats = new List<AttachSeat>(1);

		private readonly Collider[] _grabOverlap = new Collider[32];

		private Coroutine _serverRoutine;

		private PPG_Player _heldPlayer;

		private PPG_Player _lastReleasedPlayer;

		private bool _grabEventConsumed;

		private bool _throwEventConsumed;

		public PlayerAttachmentMode AttachmentMode => PlayerAttachmentMode.BossGrab;

		public List<AttachSeat> Seats
		{
			get
			{
				return _seats;
			}
			set
			{
				_seats.Clear();
				if (value != null && value.Count > 0)
				{
					_seats.Add(value[0]);
				}
			}
		}

		public bool ServerTryReleasePlayerForRecovery(PPG_Player player, string reason)
		{
			if (!base.IsServer || player == null || _heldPlayer != player)
			{
				return false;
			}
			uint activeToken = base.ActiveToken;
			ReleaseHeldPlayerWithoutThrow();
			if (activeToken != 0)
			{
				CompleteAttack(activeToken);
			}
			Log($"Player released by recovery. ClientId={player.OwnerClientId} Reason={reason}.");
			return true;
		}

		protected override void Awake()
		{
			base.Awake();
			BindGrabPointToAnimatedBone();
			RebuildGrabSeat();
		}

		public override bool ServerCanSelectAttack()
		{
			if (base.ServerCanSelectAttack() && base.Boss != null)
			{
				return HasActivationTarget();
			}
			return false;
		}

		public override void ServerSyncLateJoinClient(ulong clientId)
		{
			if (base.IsServer && base.ActiveToken != 0 && IsValidHeldPlayer(_heldPlayer))
			{
				_heldPlayer.ClientAssignParentConstraintSeatRpc(base.NetworkObject.ToReference(), 0, base.ActiveToken, base.RpcTarget.Single(clientId, RpcTargetUse.Temp));
			}
		}

		public override void OnNetworkDespawn()
		{
			StopServerRoutine();
			if (base.IsServer)
			{
				CleanupServerGrab(base.ActiveToken, notifyPlayer: true);
			}
			base.OnNetworkDespawn();
		}

		protected override void OnServerAttackStarted(CrabBossAttackCommand command)
		{
			StopServerRoutine();
			CleanupServerGrab(base.ActiveToken, notifyPlayer: true);
			RebuildGrabSeat();
			_grabEventConsumed = false;
			_throwEventConsumed = false;
			if (!HasActivationTarget())
			{
				Log($"Completed without a target in the activation cone. Token={command.Token}.");
				CompleteAttack(command.Token);
			}
			else
			{
				_contactDamage.ServerBeginSuppression(command.Token);
				_serverRoutine = StartCoroutine(ServerAttackRoutine(command.Token));
				Log($"Started. Token={command.Token}.");
			}
		}

		protected override void OnServerAttackEnded(CrabBossAttackCommand command, CrabBossAttackEndReason reason)
		{
			StopServerRoutine();
			CleanupServerGrab(command.Token, notifyPlayer: true);
			Log($"Ended. Token={command.Token} Reason={reason}.");
		}

		protected override void OnServerForceCleanup(uint activeToken)
		{
			StopServerRoutine();
			CleanupServerGrab(activeToken, notifyPlayer: true);
		}

		protected override bool ValidateSpecificSettings(out string error)
		{
			if (base.AttackId != CrabBossAttackId.GrabThrow)
			{
				error = "Attack ID must be GrabThrow.";
				return false;
			}
			if (_grabPoint == null)
			{
				error = "Grab point is missing.";
				return false;
			}
			if (string.IsNullOrWhiteSpace(_grabBoneName))
			{
				error = "Grab bone name is empty.";
				return false;
			}
			if (_forwardReference == null)
			{
				error = "Forward reference is missing.";
				return false;
			}
			if (_contactDamage == null)
			{
				error = "CrabBossContactDamage reference is missing.";
				return false;
			}
			if (_playerLayers.value == 0)
			{
				error = "Player layer mask must contain at least one layer.";
				return false;
			}
			if (!IsPositiveFinite(_activationRange))
			{
				error = "Activation range must be greater than zero and finite.";
				return false;
			}
			if (!IsFinite(_activationHalfAngle) || _activationHalfAngle < 0f || _activationHalfAngle > 180f)
			{
				error = "Activation half angle must be finite and between 0 and 180 degrees.";
				return false;
			}
			if (!IsPositiveFinite(_grabRadius))
			{
				error = "Grab radius must be greater than zero and finite.";
				return false;
			}
			if (!IsFinite(_playerHitCenterOffset))
			{
				error = "Player hit center offset must be finite.";
				return false;
			}
			if (_oxygenDamage == 0)
			{
				error = "Oxygen damage must be greater than zero.";
				return false;
			}
			if (!IsPositiveFinite(_throwImpulse))
			{
				error = "Throw impulse must be greater than zero and finite.";
				return false;
			}
			if (!IsNonNegativeFinite(_throwUpwardBias))
			{
				error = "Throw upward bias must be non-negative and finite.";
				return false;
			}
			error = null;
			return true;
		}

		private IEnumerator ServerAttackRoutine(uint token)
		{
			while (IsTokenActive(token))
			{
				if (_heldPlayer != null && !IsValidHeldPlayer(_heldPlayer))
				{
					ReleaseHeldPlayerWithoutThrow();
				}
				yield return null;
			}
			_serverRoutine = null;
		}

		public void AnimationEventGrabPlayer()
		{
			if (base.IsSpawned && base.IsServer)
			{
				ServerConsumeGrabAnimationEvent(base.Boss.LocalAttackAnimationToken);
			}
		}

		public void AnimationEventThrowPlayer()
		{
			if (base.IsSpawned && base.IsServer)
			{
				ServerConsumeThrowAnimationEvent(base.Boss.LocalAttackAnimationToken);
			}
		}

		private void ServerConsumeGrabAnimationEvent(uint token)
		{
			if (IsTokenActive(token) && !_grabEventConsumed)
			{
				_grabEventConsumed = true;
				TryGrabPlayer(token);
			}
		}

		private void ServerConsumeThrowAnimationEvent(uint token)
		{
			if (IsTokenActive(token) && _grabEventConsumed && !_throwEventConsumed)
			{
				_throwEventConsumed = true;
				ThrowHeldPlayer();
				CompleteAttack(token);
			}
		}

		private bool HasActivationTarget()
		{
			int num = base.Boss.ServerCollectPlayersInRoom(_roomPlayers);
			Vector3 position = base.transform.position;
			Vector3 vector = ResolveForward();
			float num2 = Mathf.Min(_activationRange, _grabRadius);
			float num3 = Mathf.Cos(_activationHalfAngle * (MathF.PI / 180f));
			float num4 = float.PositiveInfinity;
			float num5 = float.PositiveInfinity;
			float num6 = -1f;
			int num7 = 0;
			bool flag = false;
			for (int i = 0; i < num; i++)
			{
				PPG_Player player = _roomPlayers[i];
				if (IsValidGrabCandidate(player))
				{
					num7++;
					Vector3 vector2 = ResolvePlayerHitCenter(player) - position;
					float sqrMagnitude = vector2.sqrMagnitude;
					float num8 = ((sqrMagnitude > Mathf.Epsilon) ? Vector3.Dot(vector, vector2 / Mathf.Sqrt(sqrMagnitude)) : 1f);
					if (sqrMagnitude < num5)
					{
						num5 = sqrMagnitude;
					}
					if (num8 > num6)
					{
						num6 = num8;
					}
					if (IsWithinGrabTargeting(sqrMagnitude, num8) && !(sqrMagnitude >= num4))
					{
						num4 = sqrMagnitude;
						flag = true;
					}
				}
			}
			if (flag)
			{
				Log($"Activation target found. RoomPlayers={num} " + $"ValidCandidates={num7} Distance={Mathf.Sqrt(num4):F2}.");
			}
			else
			{
				string arg = (float.IsPositiveInfinity(num5) ? "none" : Mathf.Sqrt(num5).ToString("F2"));
				Log($"Activation target rejected. RoomPlayers={num} " + $"ValidCandidates={num7} ClosestDistance={arg} " + $"Range={num2:F2} BestDot={num6:F3} " + $"RequiredDot={num3:F3} Forward={vector}.");
			}
			return flag;
		}

		private void TryGrabPlayer(uint token)
		{
			int num = CollectGrabOverlaps(out var overlaps);
			PPG_Player pPG_Player = null;
			float num2 = float.PositiveInfinity;
			for (int i = 0; i < num; i++)
			{
				Collider collider = overlaps[i];
				overlaps[i] = null;
				if (collider == null)
				{
					continue;
				}
				PPG_Player componentInParent = collider.GetComponentInParent<PPG_Player>();
				if (IsValidGrabCandidate(componentInParent))
				{
					Vector3 vector = ResolvePlayerHitCenter(componentInParent) - base.transform.position;
					float sqrMagnitude = vector.sqrMagnitude;
					float forwardDot = ((sqrMagnitude > Mathf.Epsilon) ? Vector3.Dot(ResolveForward(), vector / Mathf.Sqrt(sqrMagnitude)) : 1f);
					if (IsWithinGrabTargeting(sqrMagnitude, forwardDot) && !(sqrMagnitude >= num2))
					{
						num2 = sqrMagnitude;
						pPG_Player = componentInParent;
					}
				}
			}
			if (pPG_Player == null || !IsTokenActive(token))
			{
				Log($"Grab frame missed. Token={token}.");
				return;
			}
			_heldPlayer = pPG_Player;
			_heldPlayer.IsTrapped = true;
			_heldPlayer.ClientAssignParentConstraintSeatRpc(base.NetworkObject.ToReference(), 0, token);
			Log($"Player grabbed. Token={token} ClientId={_heldPlayer.OwnerClientId}.");
		}

		private int CollectGrabOverlaps(out Collider[] overlaps)
		{
			int num = Physics.OverlapSphereNonAlloc(base.transform.position, _grabRadius, _grabOverlap, _playerLayers, QueryTriggerInteraction.Collide);
			overlaps = _grabOverlap;
			if (num == _grabOverlap.Length)
			{
				overlaps = Physics.OverlapSphere(base.transform.position, _grabRadius, _playerLayers, QueryTriggerInteraction.Collide);
				num = overlaps.Length;
				Array.Clear(_grabOverlap, 0, _grabOverlap.Length);
			}
			return num;
		}

		private void ThrowHeldPlayer()
		{
			if (!(_heldPlayer == null))
			{
				if (!IsValidHeldPlayer(_heldPlayer))
				{
					ReleaseHeldPlayerWithoutThrow();
					return;
				}
				PPG_Player heldPlayer = _heldPlayer;
				_heldPlayer = null;
				_lastReleasedPlayer = heldPlayer;
				Vector3 normalized = (ResolveForward() + Vector3.up * _throwUpwardBias).normalized;
				heldPlayer.ClientReleaseBossGrabRpc(normalized, _throwImpulse, _oxygenDamage);
				heldPlayer.IsTrapped = false;
				Log($"Player thrown. ClientId={heldPlayer.OwnerClientId}.");
			}
		}

		private void ReleaseHeldPlayerWithoutThrow()
		{
			if (!(_heldPlayer == null))
			{
				PPG_Player heldPlayer = _heldPlayer;
				_heldPlayer = null;
				_lastReleasedPlayer = heldPlayer;
				if (heldPlayer.IsSpawned)
				{
					heldPlayer.ClientDeAssignParentConstraintRpc();
				}
				heldPlayer.IsTrapped = false;
			}
		}

		private void CleanupServerGrab(uint token, bool notifyPlayer)
		{
			PPG_Player cooldownPlayer = _lastReleasedPlayer;
			if (_heldPlayer != null)
			{
				PPG_Player heldPlayer = _heldPlayer;
				_heldPlayer = null;
				cooldownPlayer = heldPlayer;
				if (notifyPlayer && heldPlayer.IsSpawned)
				{
					heldPlayer.ClientDeAssignParentConstraintRpc();
				}
				heldPlayer.IsTrapped = false;
			}
			if (_contactDamage != null)
			{
				_contactDamage.ServerEndSuppression(token, cooldownPlayer);
			}
			_lastReleasedPlayer = null;
			_grabEventConsumed = false;
			_throwEventConsumed = false;
		}

		private bool IsValidGrabCandidate(PPG_Player player)
		{
			if (player != null && player.IsSpawned && base.Boss.ServerIsPlayerInRoom(player.OwnerClientId) && !player.IsTrapped)
			{
				if (!(player.DivingEquipment == null))
				{
					return !player.DivingEquipment.IsKnocked;
				}
				return true;
			}
			return false;
		}

		private bool IsValidHeldPlayer(PPG_Player player)
		{
			if (player != null && player.IsSpawned && base.Boss.ServerIsPlayerInRoom(player.OwnerClientId))
			{
				if (!(player.DivingEquipment == null))
				{
					return !player.DivingEquipment.IsKnocked;
				}
				return true;
			}
			return false;
		}

		private bool IsWithinGrabTargeting(float distanceSqr, float forwardDot)
		{
			float num = Mathf.Min(_activationRange, _grabRadius);
			if (distanceSqr <= num * num)
			{
				return forwardDot >= Mathf.Cos(_activationHalfAngle * (MathF.PI / 180f));
			}
			return false;
		}

		private Vector3 ResolvePlayerHitCenter(PPG_Player player)
		{
			Transform transform = ((player != null && player.FirstPersonCharacter != null) ? player.FirstPersonCharacter.transform : ((player != null) ? player.transform : null));
			if (!(transform != null))
			{
				return base.transform.position;
			}
			return transform.position + Vector3.up * _playerHitCenterOffset;
		}

		private Vector3 ResolveForward()
		{
			Vector3 vector = ((_forwardReference != null) ? _forwardReference.forward : base.transform.forward);
			if (!(vector.sqrMagnitude > Mathf.Epsilon))
			{
				return Vector3.forward;
			}
			return vector.normalized;
		}

		private void RebuildGrabSeat()
		{
			_seats.Clear();
			if (_grabPoint != null)
			{
				_seats.Add(new AttachSeat(_grabPoint, Vector3.zero, Vector3.zero, Vector3.zero));
			}
		}

		private void BindGrabPointToAnimatedBone()
		{
			if (!(_grabPoint == null) && !string.IsNullOrWhiteSpace(_grabBoneName))
			{
				Transform transform = FindDescendantByName(base.transform, _grabBoneName);
				if (transform != null && _grabPoint.parent != transform)
				{
					_grabPoint.SetParent(transform, worldPositionStays: false);
				}
			}
		}

		private static Transform FindDescendantByName(Transform root, string targetName)
		{
			if (root == null)
			{
				return null;
			}
			for (int i = 0; i < root.childCount; i++)
			{
				Transform child = root.GetChild(i);
				if (child.name == targetName)
				{
					return child;
				}
				Transform transform = FindDescendantByName(child, targetName);
				if (transform != null)
				{
					return transform;
				}
			}
			return null;
		}

		private bool IsTokenActive(uint token)
		{
			if (base.IsServer && token != 0)
			{
				return token == base.ActiveToken;
			}
			return false;
		}

		private void StopServerRoutine()
		{
			if (_serverRoutine != null)
			{
				StopCoroutine(_serverRoutine);
				_serverRoutine = null;
			}
		}

		private static bool IsPositiveFinite(float value)
		{
			if (value > 0f)
			{
				return IsFinite(value);
			}
			return false;
		}

		private static bool IsNonNegativeFinite(float value)
		{
			if (value >= 0f)
			{
				return IsFinite(value);
			}
			return false;
		}

		private static bool IsFinite(float value)
		{
			if (!float.IsNaN(value))
			{
				return !float.IsInfinity(value);
			}
			return false;
		}

		private void Log(string message)
		{
			if (_log)
			{
				UtilityMethods.Log("[CrabBoss][GrabThrowAttack] " + message);
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
			return "CrabBossGrabThrowAttack";
		}
	}
}
