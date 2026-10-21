using PaperPixelGames.Core.Interaction;
using UnityEngine;

namespace PaperPixelGames.Core.Data.Tools
{
	[CreateAssetMenu(fileName = "PoseidonTridentData", menuName = "Data/PlayerTools/Poseidon Trident")]
	public sealed class PoseidonTridentData : BaseToolData
	{
		private const int DefaultProjectileHitMask = 203967425;

		private const int DefaultWaterProbeMask = 65552;

		public ToolType ToolType = ToolType.PoseidonTrident;

		public GameObject GraphicPrefab;

		[Header("Projectile")]
		public GameObject ProjectilePrefab;

		public LayerMask ProjectileHitMask = 203967425;

		public Vector3 ProjectileSpawnOffset;

		[Min(0.01f)]
		public float ChargeDuration = 1f;

		[Min(0f)]
		public float ThrowReleaseDelay = 1f;

		[Min(0.01f)]
		public float CollisionRadius = 0.35f;

		[Min(0f)]
		public float AutoReturnDelay = 4f;

		[Min(0.01f)]
		public float ReturnSpeed = 35f;

		[Min(0.01f)]
		public float ReturnArrivalDistance = 0.4f;

		[Header("Throw Physics")]
		[Min(0.01f)]
		public float MinThrowImpulse = 15f;

		[Min(0.01f)]
		public float MaxThrowImpulse = 35f;

		[Min(0f)]
		public float MinUpwardImpulse = 2f;

		[Min(0f)]
		public float MaxUpwardImpulse = 5f;

		[Min(0.01f)]
		public float ProjectileMass = 1f;

		public bool UseGravity = true;

		[Min(0f)]
		public float AirLinearDamping = 0.35f;

		[Min(0f)]
		public float AirAngularDamping = 3f;

		[Min(1f)]
		public float MaxSafetyDistance = 60f;

		[Min(0f)]
		public float AlignToVelocitySpeed = 12f;

		[Header("Underwater Throw Physics")]
		[Range(0.01f, 1f)]
		public float UnderwaterForceMultiplier = 1f;

		[Range(0f, 1f)]
		public float UnderwaterUpwardForceMultiplier = 0.5f;

		[Min(0.01f)]
		public float WaterEffectsActivationVelocity = 10f;

		[Min(0f)]
		public float WaterLinearDamping = 1.5f;

		[Min(0f)]
		public float WaterAngularDamping = 3f;

		[Min(0.01f)]
		public float WaterVelocityCap = 12f;

		[Min(0.01f)]
		public float WaterProbeRadius = 0.15f;

		public LayerMask WaterProbeMask = 65552;

		public bool PlayWaterImpactFx = true;

		[Header("Held Action Offsets")]
		public ItemOffset ThrowOffsetFps = new ItemOffset(Vector3.back * 0.25f, Vector3.zero);

		public ItemOffset ThrowOffsetTps = new ItemOffset(Vector3.back * 0.25f, Vector3.zero);

		public ItemOffset DashOffsetFps = new ItemOffset(Vector3.back * 0.15f, Vector3.zero);

		public ItemOffset DashOffsetTps = new ItemOffset(Vector3.back * 0.15f, Vector3.zero);

		[Header("Projectile Visual Offsets")]
		public ItemOffset OutboundOffset = new ItemOffset(Vector3.zero, Vector3.zero);

		public ItemOffset ReturningOffset = new ItemOffset(Vector3.zero, new Vector3(0f, 180f, 0f));

		[Header("Dash")]
		[Min(0f)]
		public float MinDashImpulse = 3f;

		[Min(0f)]
		public float MaxDashImpulse = 10f;

		[Min(0f)]
		public float DashCooldown = 1.5f;

		[Range(0.1f, 1f)]
		public float SwimSpeedMultiplier = 0.65f;

		[Header("Audio")]
		public AudioClipSO ThrowSfx;

		[Min(0f)]
		public float ThrowSfxDelay = 0.3f;

		public AudioClipSO ReturnSfx;

		[Min(0f)]
		public float ReturnSfxDelay = 0.5f;

		public AudioClipSO DashSfx;

		[Min(0f)]
		public float DashSfxDelay = 0.3f;
	}
}
