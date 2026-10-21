using UnityEngine;

namespace PaperPixelGames.Core.Data
{
	[CreateAssetMenu(fileName = "Hostile", menuName = "Data/HostileSO")]
	public class HostileSO : CreatureSO
	{
		[Header("Patrol")]
		[Min(1f)]
		public int PatrolPathPointCount = 4;

		public bool PatrolAllowRepeats;

		public bool PatrolLoop;

		public bool PatrolStartFromClosest = true;

		public bool PatrolSkipUnpassableTargets = true;

		[Min(0.01f)]
		public float PatrolReachDistance = 0.35f;

		[Header("Movement")]
		[Min(0.01f)]
		public float Speed = 2f;

		[Min(0.01f)]
		public float ChaseSpeed = 4f;

		[Min(0.01f)]
		public float EscapeSpeed = 4f;

		[Min(0.01f)]
		public float DragSpeed = 3f;

		[Header("Distance")]
		[Min(0.05f)]
		public float FollowReachDistance = 0.9f;

		[Min(0.05f)]
		public float AttackTriggerDistance = 1.5f;

		[Min(0.01f)]
		public float TargetOffsetUpdate = 0.2f;

		[Header("Chase")]
		[Min(0f)]
		public float ChaseCooldown = 2f;

		[Min(0.1f)]
		public float ChaseDuration = 8f;

		[Header("Attack")]
		[Min(0.05f)]
		public float AttackTimeout = 1.25f;

		public byte DamageAmount = 10;

		[Header("Drag")]
		[Min(1f)]
		public int DragPathPointCount = 4;

		public byte DragBreakHitAmount = 2;

		[Header("Combat")]
		[Tooltip("Accumulated harpoon damage required to leave chase. Zero disables hit-driven escape.")]
		[Min(0f)]
		public int EscapeHarpoonDamage = 3;

		[Min(1f)]
		public int HarpoonDurability = 9;

		[Header("Hit Reaction")]
		[Min(0f)]
		public float HitSlowDuration = 0.75f;

		[Range(0.05f, 1f)]
		public float HitSlowMultiplier = 0.45f;

		[Header("Lifecycle")]
		[Min(0f)]
		public float DeathDespawnDelay = 10f;
	}
}
