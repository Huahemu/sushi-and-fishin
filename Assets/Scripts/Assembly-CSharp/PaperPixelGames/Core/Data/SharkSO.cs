using UnityEngine;

namespace PaperPixelGames.Core.Data
{
	[CreateAssetMenu(fileName = "Shark", menuName = "Data/SharkSO")]
	public class SharkSO : HostileSO
	{
		[Header("Shark Behavior")]
		public SharkTargetingMode TargetingMode = SharkTargetingMode.Aggressive;

		public SharkAttackCapabilities AttackCapabilities = SharkAttackCapabilities.All;

		[Header("Shark Patrol")]
		public bool PatrolAcrossLayerAreas = true;

		[Header("Shark Drag")]
		public bool DragAfterBite = true;

		[Min(0.01f)]
		public float MinDragDuration = 10f;

		[Min(0.01f)]
		public float DragPenaltyDamageInterval = 1f;

		public byte DragPenaltyDamageAmount = 1;

		[Min(0.01f)]
		public float DragEscapeRequiredProgress = 1f;

		[Min(0.01f)]
		public float DragEscapeProgressPerPress = 0.14f;

		[Min(0f)]
		public float DragEscapeProgressDecayPerSecond = 0.55f;

		[Min(0.01f)]
		public float DragEscapeMinimumPressInterval = 0.08f;

		[Min(0.01f)]
		public float DragEscapeFeedbackInterval = 0.4f;

		[Min(0.01f)]
		public float DragEscapePunchVisibleDuration = 0.3f;

		public AudioClipSO DragEscapePunchSfx;

		[Header("Shark Cage")]
		[Min(0.05f)]
		public float CageAttackTriggerDistance = 4.5f;

		[Header("Shark Tail Attack")]
		[Range(0.01f, 1f)]
		public float TailDamageMultiplier = 1f / 3f;

		[Min(0f)]
		public float TailKnockbackImpulse = 12f;

		[Min(0f)]
		public float TailKnockbackUpwardForce = 1f;

		[Header("Shark Cooldown")]
		[Min(0f)]
		public float PostDragChaseCooldown = 3f;

		[Min(0f)]
		public float PostAttackChaseCooldown = 2f;

		[Header("Shark Death Loot")]
		[Min(0f)]
		public int MinDeathFilletDropCount = 1;

		[Min(0f)]
		public int MaxDeathFilletDropCount = 1;
	}
}
