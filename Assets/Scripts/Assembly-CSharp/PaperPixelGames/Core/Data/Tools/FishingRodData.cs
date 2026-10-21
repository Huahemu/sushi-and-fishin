using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Localization;
using UnityEngine;

namespace PaperPixelGames.Core.Data.Tools
{
	[CreateAssetMenu(fileName = "FishingRodData", menuName = "Data/PlayerTools/FishingRod")]
	public class FishingRodData : BaseToolData
	{
		public AudioClipSO BiteSfx;

		public AudioClipSO CounterSuccessSfx;

		public const int BendBoneCount = 8;

		public ToolType ToolType;

		[Tooltip("Random local wait duration after the cast lands, in seconds.")]
		public Vector2 WaitDurationRange = new Vector2(5f, 10f);

		[Tooltip("Unscaled Sunlight Zone limit. Cast Distance Multiplier converts it to world meters.")]
		[Min(0f)]
		public float SunlightZoneMaxDistance = 4f;

		[Tooltip("Unscaled Mid Zone limit. Cast Distance Multiplier converts it to world meters.")]
		[Min(0f)]
		public float MidZoneMaxDistance = 7f;

		[Tooltip("Unscaled Deep Zone limit. Cast Distance Multiplier converts it to world meters.")]
		[Min(0f)]
		public float DeepZoneMaxDistance = 10f;

		[Tooltip("Unscaled Abyssal Rift limit. Longer casts select Forgotten Depth.")]
		[Min(0f)]
		public float AbyssalRiftMaxDistance = 13f;

		[Tooltip("Minimum water depth required for a fishing challenge to start.")]
		[Min(0f)]
		public float MinimumFishableWaterDepth = 2.5f;

		[Tooltip("Maximum downward distance checked for shallow ground below the bobber.")]
		[Min(0.1f)]
		public float WaterDepthProbeDistance = 25f;

		[Tooltip("Solid layers considered ground when measuring water depth.")]
		public LayerMask WaterDepthGroundMask = -5;

		[Tooltip("Played locally when the fishing rod is equipped.")]
		public AudioClipSO EquipSfx;

		[Tooltip("Played locally when the fishing rod is unequipped.")]
		public AudioClipSO UnequipSfx;

		[Tooltip("Played when a valid cast is released.")]
		public AudioClipSO CastSfx;

		[Tooltip("Played at the selected water point when the cast lands.")]
		public AudioClipSO CastWaterSplashSfx;

		[Tooltip("Played at the fishing point when the fish starts a movement.")]
		public AudioClipSO FishSplashSfx;

		[Tooltip("Loop played while the player holds reel during the challenge.")]
		public AudioClipSO ReelLoopSfx;

		[Tooltip("Played once when reeling against an escaping fish.")]
		public AudioClipSO ReelStruggleSfx;

		[Tooltip("Loop played while line tension is in the warning range.")]
		public AudioClipSO LineTensionLoopSfx;

		[Tooltip("Played when maximum tension breaks the line.")]
		public AudioClipSO LineBreakSfx;

		[Tooltip("Played when catch progress reaches completion.")]
		public AudioClipSO CatchSuccessSfx;

		[Tooltip("Maximum distance at which other clients hear fishing rod sounds.")]
		[Min(0.1f)]
		public float RemoteSfxHearDistance = 5f;

		[Tooltip("Tension value that starts the warning loop.")]
		[Range(0f, 1f)]
		public float TensionLoopStartThreshold = 0.7f;

		[Tooltip("Tension value below which the warning loop stops.")]
		[Range(0f, 1f)]
		public float TensionLoopStopThreshold = 0.55f;

		[Tooltip("Warning loop pitch from its start threshold to maximum tension.")]
		public Vector2 TensionLoopPitchRange = new Vector2(0.9f, 1.15f);

		[Tooltip("Warning loop volume multiplier from its start threshold to maximum tension.")]
		public Vector2 TensionLoopVolumeMultiplierRange = new Vector2(0.5f, 1f);

		[Tooltip("Pure visual prefab shown hanging from the rod tip before casting.")]
		public GameObject IdleBobberGraphic;

		[Tooltip("Local visual offset applied at the hanging cable endpoint.")]
		public ItemOffset IdleBobberGraphicOffset = new ItemOffset(Vector3.zero, Vector3.zero);

		[Tooltip("Extra FPS rod offset used only while the caught fish waits for collection.")]
		public ItemOffset SuccessOffsetFps = new ItemOffset(Vector3.zero, Vector3.zero);

		[Tooltip("Minimum and maximum forward rod bend used across caught-fish weights.")]
		public Vector2 SuccessWeightBendRange = new Vector2(0.1f, 1f);

		[Tooltip("Caught-fish weight in kilograms that produces maximum success bend.")]
		[Min(0.01f)]
		public float SuccessFullBendWeight = 40f;

		[Tooltip("World-space hanging distance from the rod tip to the caught fish.")]
		[Min(0.01f)]
		public float CaughtFishCableLength = 0.35f;

		[Tooltip("Rendered width of the short cable holding the caught fish.")]
		[Min(0.001f)]
		public float CaughtFishCableWidth = 0.008f;

		[Tooltip("Number of simulated sections in the short cable holding the fish.")]
		[Range(3f, 16f)]
		public int CaughtFishCableSegmentCount = 5;

		[Tooltip("Extra cable length used to create visible sag while the fish hangs.")]
		[Range(0f, 0.5f)]
		public float CaughtFishCableSlack = 0.12f;

		[Tooltip("Spring strength used by the physical caught-fish cable joints.")]
		[Min(0.01f)]
		public float CaughtFishCableSpring = 300f;

		[Tooltip("Damper used by the physical caught-fish cable joints.")]
		[Min(0f)]
		public float CaughtFishCableDamper = 8f;

		[Tooltip("Mass of each cable segment and the caught-fish attachment body.")]
		[Min(0.01f)]
		public float CaughtFishCableMass = 0.25f;

		[Tooltip("Linear drag applied to the physical caught-fish cable.")]
		[Min(0f)]
		public float CaughtFishCableLinearDamping = 3f;

		[Tooltip("Angular drag applied to the physical caught-fish cable.")]
		[Min(0f)]
		public float CaughtFishCableAngularDamping = 3f;

		[Tooltip("Local velocity applied when the caught fish first appears.")]
		public Vector3 CaughtFishInitialSwingVelocity = Vector3.zero;

		[Tooltip("How much the caught fish tilts with the hanging cable direction.")]
		[Range(0f, 1f)]
		public float CaughtFishSwingRotationInfluence = 0.35f;

		[Tooltip("How quickly the caught fish rotation follows its swing.")]
		[Min(0.01f)]
		public float CaughtFishSwingRotationResponse = 8f;

		[Tooltip("Extra local offset applied to the FPS line origin.")]
		public Vector3 LineOriginOffsetFps;

		[Tooltip("Extra local offset applied to the TPS line origin.")]
		public Vector3 LineOriginOffsetTps;

		[Tooltip("Rendered fishing line width.")]
		[Min(0.001f)]
		public float LineWidth = 0.008f;

		[Tooltip("Number of simulated sections used by the main fishing line.")]
		[Range(1f, 32f)]
		public int LineSegmentCount = 8;

		[Tooltip("Multiplies held cast charge before distance, duration, and arc are calculated.")]
		[Min(0.1f)]
		public float CastChargeMultiplier = 1f;

		[Tooltip("Cast distance used with no charge.")]
		[Min(0f)]
		public float MinimumCastDistance = 3f;

		[Tooltip("Converts rod-level and zone distances into physical world distance.")]
		[Min(0.1f)]
		public float CastDistanceMultiplier = 1f;

		[Tooltip("Travel time used with no charge.")]
		[Min(0.05f)]
		public float MinimumCastTravelDuration = 0.25f;

		[Tooltip("Travel time used at full charge.")]
		[Min(0.05f)]
		public float CastTravelDuration = 0.65f;

		[Tooltip("Arc height used with no charge.")]
		[Min(0f)]
		public float MinimumCastArcHeight = 0.5f;

		[Tooltip("Arc height used at full charge.")]
		[Min(0f)]
		public float CastArcHeight = 2.5f;

		[Tooltip("Time used to turn the owner toward the bobber when the challenge starts.")]
		[Min(0f)]
		public float ChallengeLookAtDuration = 0.35f;

		[Tooltip("Maximum distance from which another player may pull the bobber.")]
		[Min(0.1f)]
		public float BobberInteractionRange = 3f;

		[Tooltip("Minimum impulse applied to a player who pulls another player's bobber.")]
		[Min(0f)]
		public float BobberLaunchMinimumImpulse = 15f;

		[Tooltip("Maximum impulse applied to a player who pulls another player's bobber.")]
		[Min(0f)]
		public float BobberLaunchMaximumImpulse = 32f;

		[Tooltip("Additional launch impulse added for each meter between the player and rod owner.")]
		[Min(0f)]
		public float BobberLaunchImpulsePerMeter = 0.5f;

		[Tooltip("Upward bias added to the launch direction before normalization.")]
		[Min(0f)]
		public float BobberLaunchUpwardBias = 0.55f;

		[Tooltip("Strength applied to each mouse movement during a counter gesture.")]
		[Min(0.001f)]
		public float MouseCounterSensitivity = 0.08f;

		[Tooltip("Minimum gesture strength required to count as a counter.")]
		[Range(0.01f, 1f)]
		public float MouseCounterThreshold = 0.35f;

		[Tooltip("How quickly mouse gesture strength returns to zero.")]
		[Min(0.01f)]
		public float MouseCounterReturnSpeed = 3f;

		[Tooltip("Maximum sideways line movement for a difficulty 5 fish.")]
		[Min(0f)]
		public float MaxHorizontalLinePull = 2.4f;

		[Tooltip("Maximum forward line movement for a difficulty 5 fish.")]
		[Min(0f)]
		public float MaxForwardLinePull = 3.2f;

		[Tooltip("How quickly the line follows a new fish direction. Higher is faster.")]
		[Min(0.01f)]
		public float LineDirectionTweenResponse = 5f;

		[Tooltip("How quickly the line returns to center. Higher is faster.")]
		[Min(0.01f)]
		public float LineReturnTweenResponse = 7f;

		[Tooltip("Line and rod movement strength used by difficulty 1 fish.")]
		[Range(0f, 1f)]
		public float EasyFishPresentationStrength = 0.333f;

		[Tooltip("Line and rod movement strength used by difficulty 5 fish.")]
		[Range(0f, 1f)]
		public float HardFishPresentationStrength = 1f;

		[Tooltip("Root rotation for fish left. Right mirrors only Y, preserving X/Z lift.")]
		public Vector3 RodHorizontalTipEuler = new Vector3(0f, -16f, 8f);

		[Tooltip("Root rotation used when the fish dashes forward.")]
		public Vector3 RodForwardTipEuler = new Vector3(20f, -12f, 0f);

		[Tooltip("Maximum body curve used for forward pull and reel tension.")]
		public Vector3 MaxRodBendEuler = new Vector3(-24f, 0f, 16f);

		[Tooltip("Body counter-curve for fish left. Right mirrors Y/Z while preserving X lift.")]
		public Vector3 HorizontalRodBendEuler = new Vector3(0f, -60f, -30f);

		[Tooltip("Fallback curve used to build a horizontal pose when no saved eight-bone pose exists.")]
		public AnimationCurve RodBendProfile = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.45f, 1f), new Keyframe(1f, 0f));

		[HideInInspector]
		public Vector3[] LeftRodBoneEulerOffsets = Array.Empty<Vector3>();

		[HideInInspector]
		public Vector3[] ForwardRodBoneEulerOffsets = Array.Empty<Vector3>();

		[Tooltip("Minimum bend fraction applied while the challenge is active.")]
		[Range(0f, 1f)]
		public float MinimumChallengeBend = 0.4f;

		[Tooltip("Fraction of the forward curve kept while reeling in the center.")]
		[Range(0f, 1f)]
		public float ReelCurveMultiplier = 0.2f;

		[Tooltip("How quickly rod bones reach their target curve. Higher is faster.")]
		[Min(0.01f)]
		public float RodBendResponse = 12f;

		[Tooltip("Rotation axis of the Unity-imported Reel_Crank bone.")]
		public Vector3 ReelCrankLocalAxis = Vector3.forward;

		[Tooltip("Fallback crank speed used when the reel animation phase is unavailable.")]
		public float ReelCrankDegreesPerSecond = 360f;

		[Tooltip("Crank turns completed during one player reel animation loop. Use a negative value to reverse direction.")]
		public float ReelCrankTurnsPerAnimationLoop = 1f;

		[Tooltip("Crank angle added to the player reel animation phase.")]
		public float ReelCrankPhaseOffsetDegrees;

		[Tooltip("Local offset added to the automatically resolved crank handle center.")]
		public Vector3 ReelHandTargetLocalPosition;

		[Tooltip("Local rotation offset added to the automatically resolved crank handle pose.")]
		public Vector3 ReelHandTargetLocalEuler;

		[Tooltip("Maximum Two Bone IK weight applied to the hand while reeling.")]
		[Range(0f, 1f)]
		public float ReelHandIkWeight = 1f;

		[Tooltip("How strongly IK locks the hand position to the crank handle.")]
		[Range(0f, 1f)]
		public float ReelHandIkPositionWeight = 1f;

		[Tooltip("How strongly IK matches the hand rotation to the crank handle.")]
		[Range(0f, 1f)]
		public float ReelHandIkRotationWeight = 1f;

		[Tooltip("How strongly the elbow follows its automatically created bend hint.")]
		[Range(0f, 1f)]
		public float ReelHandIkHintWeight = 0.75f;

		[Tooltip("Additional player-local offset applied to the automatic elbow hint.")]
		public Vector3 ReelHandIkHintOffset;

		[Tooltip("Maximum shoulder-to-hand reach as a fraction of total arm length. Lower values keep the elbow bent.")]
		[Range(0.5f, 0.99f)]
		public float ReelHandIkMaxReach = 0.97f;

		[Tooltip("Fraction of total arm length used to smoothly ease into the maximum reach.")]
		[Range(0f, 0.25f)]
		public float ReelHandIkSoftReach = 0.05f;

		[Tooltip("How quickly reel hand IK blends in and out.")]
		[Min(0.01f)]
		public float ReelHandIkBlendSpeed = 12f;

		[Tooltip("Overall strength of input-driven rod challenge sway.")]
		[Range(0f, 1f)]
		public float FishingSwayWeight = 0.5f;

		[Tooltip("Amount of normal idle sway kept during the rod challenge.")]
		[Range(0f, 1f)]
		public float FishingIdleSwayWeight = 0.15f;

		[Tooltip("Local position offset for horizontal mouse counters. Left mirrors X.")]
		public Vector3 FishingHorizontalSwayPosition = new Vector3(0.025f, -0.002f, -0.006f);

		[Tooltip("Local rotation for horizontal mouse counters. Left mirrors yaw and roll.")]
		public Vector3 FishingHorizontalSwayRotation = new Vector3(-1.5f, 6f, -5f);

		[Tooltip("Local position offset used while pulling the mouse backward.")]
		public Vector3 FishingBackwardSwayPosition = new Vector3(0f, -0.012f, -0.025f);

		[Tooltip("Local rotation used while pulling the mouse backward.")]
		public Vector3 FishingBackwardSwayRotation = new Vector3(7f, 0f, 0f);

		[Tooltip("Time used to reach and release an input sway pose. Lower is faster.")]
		[Min(0.001f)]
		public float FishingSwaySmoothTime = 0.1f;

		[Tooltip("Small local position vibration added while a directional steer pose is active.")]
		public Vector3 FishingStruggleSwayPosition = new Vector3(0.0015f, 0.001f, 0.001f);

		[Tooltip("Small local rotation vibration added while a directional steer pose is active.")]
		public Vector3 FishingStruggleSwayRotation = new Vector3(0.22f, 0.3f, 0.4f);

		[Tooltip("Speed of the irregular struggle vibration while steering the fish.")]
		[Min(0.01f)]
		public float FishingStruggleSwayFrequency = 8f;

		[Tooltip("Level-specific rod data assets, ordered from UI level 1 to level 5. Asset Level values use indexes 0 to 4.")]
		public List<FishingRodLevelData> Levels = new List<FishingRodLevelData>();

		public float GetSuccessWeightBend(float caughtWeight)
		{
			float a = Mathf.Clamp01(Mathf.Min(SuccessWeightBendRange.x, SuccessWeightBendRange.y));
			float b = Mathf.Clamp01(Mathf.Max(SuccessWeightBendRange.x, SuccessWeightBendRange.y));
			float t = Mathf.Clamp01(Mathf.Max(0f, caughtWeight) / Mathf.Max(0.01f, SuccessFullBendWeight));
			return Mathf.Lerp(a, b, t);
		}

		public FishingRodLevelData GetLevel(byte levelIndex)
		{
			if (Levels == null || levelIndex >= Levels.Count)
			{
				return null;
			}
			return Levels[levelIndex];
		}

		public float GetMaximumCastDistance(FishingRodLevelData levelData)
		{
			if (levelData == null)
			{
				return 0f;
			}
			return Mathf.Max(0f, levelData.MaxCastDistance * Mathf.Max(0.1f, CastDistanceMultiplier));
		}

		public float GetScaledCastCharge(float normalizedCharge)
		{
			return Mathf.Clamp01(Mathf.Clamp01(normalizedCharge) * Mathf.Max(0.1f, CastChargeMultiplier));
		}

		public float GetCastDistance(FishingRodLevelData levelData, float normalizedCharge)
		{
			float a = Mathf.Max(0f, MinimumCastDistance);
			float b = Mathf.Max(a, GetMaximumCastDistance(levelData));
			return Mathf.Lerp(a, b, GetScaledCastCharge(normalizedCharge));
		}

		public float GetCastTravelDuration(float normalizedCharge)
		{
			float a = Mathf.Max(0.05f, MinimumCastTravelDuration);
			float b = Mathf.Max(a, CastTravelDuration);
			return Mathf.Lerp(a, b, GetScaledCastCharge(normalizedCharge));
		}

		public float GetCastArcHeight(float normalizedCharge)
		{
			float a = Mathf.Max(0f, MinimumCastArcHeight);
			float b = Mathf.Max(a, CastArcHeight);
			return Mathf.Lerp(a, b, GetScaledCastCharge(normalizedCharge));
		}

		public string GetCastZoneFeedback(float castDistance)
		{
			OceanLayer zone = GetZone(castDistance);
			return $"{GetZoneDisplayName(zone)}\n{castDistance:0.0} m";
		}

		private static string GetZoneDisplayName(OceanLayer zone)
		{
			var (key, fallback) = zone switch
			{
				OceanLayer.SunlightZone => ("gameplay.fishing.zone.sunlight", "SUNLIGHT ZONE"), 
				OceanLayer.MidZone => ("gameplay.fishing.zone.mid", "MID ZONE"), 
				OceanLayer.DeepZone => ("gameplay.fishing.zone.deep", "DEEP ZONE"), 
				OceanLayer.AbyssalRift => ("gameplay.fishing.zone.abyssal_rift", "ABYSSAL RIFT"), 
				OceanLayer.ForgottenDepth => ("gameplay.fishing.zone.forgotten_depth", "FORGOTTEN DEPTH"), 
				_ => ("gameplay.fishing.zone.unknown", "UNKNOWN ZONE"), 
			};
			return LocalizationDatabase.GetString("UiTable", key, fallback);
		}

		public OceanLayer GetZone(float castDistance)
		{
			float num = Mathf.Max(0f, castDistance) / GetCastDistanceScale();
			if (num <= SunlightZoneMaxDistance)
			{
				return OceanLayer.SunlightZone;
			}
			if (num <= MidZoneMaxDistance)
			{
				return OceanLayer.MidZone;
			}
			if (num <= DeepZoneMaxDistance)
			{
				return OceanLayer.DeepZone;
			}
			if (!(num <= AbyssalRiftMaxDistance))
			{
				return OceanLayer.ForgottenDepth;
			}
			return OceanLayer.AbyssalRift;
		}

		public float GetZoneMaximumDistance(OceanLayer zone)
		{
			return zone switch
			{
				OceanLayer.SunlightZone => GetScaledZoneDistance(SunlightZoneMaxDistance), 
				OceanLayer.MidZone => GetScaledZoneDistance(MidZoneMaxDistance), 
				OceanLayer.DeepZone => GetScaledZoneDistance(DeepZoneMaxDistance), 
				OceanLayer.AbyssalRift => GetScaledZoneDistance(AbyssalRiftMaxDistance), 
				OceanLayer.ForgottenDepth => float.PositiveInfinity, 
				_ => 0f, 
			};
		}

		private float GetScaledZoneDistance(float unscaledDistance)
		{
			return Mathf.Max(0f, unscaledDistance) * GetCastDistanceScale();
		}

		private float GetCastDistanceScale()
		{
			return Mathf.Max(0.1f, CastDistanceMultiplier);
		}

		public float GetRandomWaitDuration()
		{
			float num = Mathf.Max(0.1f, Mathf.Min(WaitDurationRange.x, WaitDurationRange.y));
			float maxInclusive = Mathf.Max(num, Mathf.Max(WaitDurationRange.x, WaitDurationRange.y));
			return UnityEngine.Random.Range(num, maxInclusive);
		}

		public Vector3 GetLeftRodBoneEulerOffset(int index)
		{
			if (index < 0 || index >= 8)
			{
				return Vector3.zero;
			}
			Vector3[] leftRodBoneEulerOffsets = LeftRodBoneEulerOffsets;
			if (leftRodBoneEulerOffsets != null && leftRodBoneEulerOffsets.Length == 8)
			{
				return LeftRodBoneEulerOffsets[index];
			}
			AnimationCurve animationCurve = ((RodBendProfile != null && RodBendProfile.length >= 2) ? RodBendProfile : AnimationCurve.Linear(0f, 0f, 1f, 0f));
			float time = (float)index / 8f;
			float time2 = ((float)index + 1f) / 8f;
			float num = animationCurve.Evaluate(time2) - animationCurve.Evaluate(time);
			return HorizontalRodBendEuler * num;
		}

		public void SetLeftRodBoneEulerOffsets(IReadOnlyList<Vector3> offsets)
		{
			if (offsets == null || offsets.Count != 8)
			{
				throw new ArgumentException($"Left rod pose requires exactly {8} bone offsets.", "offsets");
			}
			LeftRodBoneEulerOffsets = new Vector3[8];
			for (int i = 0; i < 8; i++)
			{
				LeftRodBoneEulerOffsets[i] = offsets[i];
			}
		}

		public Vector3 GetForwardRodBoneEulerOffset(int index)
		{
			if (index < 0 || index >= 8)
			{
				return Vector3.zero;
			}
			Vector3[] forwardRodBoneEulerOffsets = ForwardRodBoneEulerOffsets;
			if (forwardRodBoneEulerOffsets != null && forwardRodBoneEulerOffsets.Length == 8)
			{
				return ForwardRodBoneEulerOffsets[index];
			}
			AnimationCurve animationCurve = ((RodBendProfile != null && RodBendProfile.length >= 2) ? RodBendProfile : AnimationCurve.Linear(0f, 0f, 1f, 0f));
			float time = (float)index / 8f;
			float time2 = ((float)index + 1f) / 8f;
			float num = animationCurve.Evaluate(time2) - animationCurve.Evaluate(time);
			return MaxRodBendEuler * num;
		}

		public void SetForwardRodBoneEulerOffsets(IReadOnlyList<Vector3> offsets)
		{
			if (offsets == null || offsets.Count != 8)
			{
				throw new ArgumentException($"Forward rod pose requires exactly {8} bone offsets.", "offsets");
			}
			ForwardRodBoneEulerOffsets = new Vector3[8];
			for (int i = 0; i < 8; i++)
			{
				ForwardRodBoneEulerOffsets[i] = offsets[i];
			}
		}

		public float GetDifficultyPresentationStrength(FishingRodDifficulty difficulty)
		{
			float value = (float)(int)(difficulty - 1) / 4f;
			return Mathf.Lerp(EasyFishPresentationStrength, HardFishPresentationStrength, Mathf.Clamp01(value));
		}

		public bool TryValidate(out string error)
		{
			if (Levels == null || Levels.Count != 5)
			{
				error = "Fishing Rod requires exactly five level entries.";
				return false;
			}
			for (int i = 0; i < 5; i++)
			{
				FishingRodLevelData fishingRodLevelData = Levels[i];
				if (fishingRodLevelData == null)
				{
					error = $"Fishing Rod level {i + 1} is missing.";
					return false;
				}
				if (fishingRodLevelData.Level != i)
				{
					error = $"Fishing Rod level {i + 1} must use level index {i}.";
					return false;
				}
				if (fishingRodLevelData.GraphicPrefab == null)
				{
					error = $"Fishing Rod level {i + 1} has no graphic prefab.";
					return false;
				}
			}
			if (WaitDurationRange.x <= 0f || WaitDurationRange.y <= 0f)
			{
				error = "Fishing Rod wait duration range must stay above zero.";
				return false;
			}
			if (SunlightZoneMaxDistance <= 0f || MidZoneMaxDistance <= SunlightZoneMaxDistance || DeepZoneMaxDistance <= MidZoneMaxDistance || AbyssalRiftMaxDistance <= DeepZoneMaxDistance)
			{
				error = "Fishing Rod zone distances must be positive and strictly ascending.";
				return false;
			}
			if (WaterDepthProbeDistance <= 0f)
			{
				error = "Fishing Rod water depth probe distance must stay above zero.";
				return false;
			}
			if (WaterDepthGroundMask.value == 0)
			{
				error = "Fishing Rod water depth ground mask cannot be empty.";
				return false;
			}
			OceanLayer[] array = new OceanLayer[5]
			{
				OceanLayer.MidZone,
				OceanLayer.DeepZone,
				OceanLayer.AbyssalRift,
				OceanLayer.ForgottenDepth,
				OceanLayer.ForgottenDepth
			};
			for (int j = 0; j < Levels.Count; j++)
			{
				OceanLayer zone = GetZone(GetMaximumCastDistance(Levels[j]));
				if (zone != array[j])
				{
					error = $"Fishing Rod level {j + 1} must reach " + $"{array[j]} at full charge, but reaches {zone}.";
					return false;
				}
			}
			error = string.Empty;
			return true;
		}
	}
}
