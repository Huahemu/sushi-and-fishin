using UnityEngine;

namespace PaperPixelGames.Core.Data
{
	[CreateAssetMenu(fileName = "Octopus", menuName = "Data/OctopusSO")]
	public sealed class OctopusSO : FishSO
	{
		private const string MovementGroup = "Octopus AI/Movement";

		private const string TargetingGroup = "Octopus AI/Targeting";

		private const string AttackGroup = "Octopus AI/Attack";

		private const string StunGroup = "Octopus AI/Stun";

		private const string CombatGroup = "Octopus AI/Combat";

		[Tooltip("Multiplies the octopus base movement speed while patrolling.")]
		public float PatrolSpeedMultiplier = 1f;

		[Tooltip("Multiplies the octopus base movement speed while chasing the player.")]
		public float ChaseSpeedMultiplier = 1.6f;

		[Tooltip("Distance at which the octopus switches from chase to attack.")]
		public float AttackDistance = 1.5f;

		[Tooltip("Distance at which the octopus gives up chasing the player.")]
		public float LoseTargetDistance = 8f;

		[Tooltip("How often the chase target position is refreshed.")]
		public float FollowTargetUpdateInterval = 0.12f;

		[Tooltip("Extra distance added to AttackDistance before leaving attack state.")]
		public float AttackExitDistanceOffset = 1f;

		[Tooltip("Minimum allowed reach distance for Nav3D follow behavior.")]
		public float MinReachDistance = 0.35f;

		[Tooltip("Maximum angle allowed between octopus forward direction and target before attacking.")]
		public float AttackFacingAngleThreshold = 20f;

		[Tooltip("How quickly the octopus rotates toward the player while attacking.")]
		public float AttackFaceTurnSpeed = 8f;

		[Tooltip("Time after attack starts before returning to patrol.")]
		public float AttackTimeout = 1.25f;

		[Tooltip("Delay after an attack before the octopus can detect and chase again.")]
		public float PostAttackChaseCooldown = 2f;

		[Tooltip("Number of harpoon hits required to stun the octopus.")]
		public int HitsToStun = 3;

		[Tooltip("How long the octopus stays stunned and catchable.")]
		public float StunDuration = 5f;

		[Tooltip("Oxygen damage dealt when the octopus attack hits the player.")]
		public byte DamageAmount = 10;

		[Tooltip("Horizontal push force applied to the player on hit.")]
		public float KnockbackImpulse = 8f;

		[Tooltip("Upward force applied to the player on hit.")]
		public float KnockbackUpwardForce = 2f;
	}
}
