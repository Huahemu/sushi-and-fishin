using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HoaxGames
{
	public class FootIK : MonoBehaviour
	{
		[Serializable]
		public class AnimatorStateEntry
		{
			public int layer;

			public string animatorState;
		}

		public enum IkHintComputationStrategy
		{
			IK_HINT_POSITION_BASED = 0,
			BONE_BASED = 1,
			BONE_BASED_WITH_FEET_DIRECTION_INFLUENCE = 2
		}

		public enum InterpolationMethod
		{
			LERP = 0,
			SMOOTH_DAMP = 1
		}

		public enum SlopeLeaningType
		{
			DISABLED = 0,
			SURFACE_ANGLE_DETECTED_BY_GROUNDED_CHECK = 1,
			AVERAGE_SURFACE_ANGLE_DETECTED_BY_FEETS = 2,
			HEIGHT_OFFSET_DETECTED_BY_FEETS = 3
		}

		public enum Axis
		{
			X_AXIS = 0,
			Y_AXIS = 1,
			Z_AXIS = 2
		}

		public enum ValidationType
		{
			CHECK_IS_GROUNDED = 0,
			FORCE_VALID = 1,
			FORCE_INVALID = 2
		}

		[Header("INFO: Please activate IK pass in the Animator Controller (base layer recommended)")]
		[Header("NOTE 1: Please make sure you don't use BoxColliders with Size near ~0 in any axis because it breaks BoxCasts.")]
		[Header("NOTE 2: It is recommended to set \"Compute Ik Hint Position Strategy\" to \"BONE_BASED\" when used with Animation-Layer-Blending and Avatar Masks)")]
		[Header("General")]
		[SerializeField]
		[Tooltip("Please activate the animator IK pass on a specific layer - typically it is most likely best to activate it for the base layer. This is an requirement for getting OnAnimatorIK called on this script (otherwise the script is not going to work). This variable defines the index of the specific layer where the IK pass is activated and will be used by the script.")]
		protected int m_animatorIkPassLayerIndex;

		[SerializeField]
		[Tooltip("The Collision-Layer for foot and body placement detection.")]
		protected LayerMask m_collisionLayerMask = 1;

		[SerializeField]
		[Tooltip("The collision-setting for trigger-interactions for foot and body placement detection. Default setting is 'Ignore'.")]
		protected QueryTriggerInteraction m_triggerCollisionInteraction = QueryTriggerInteraction.Ignore;

		[SerializeField]
		[Range(0.1f, 10f)]
		[Tooltip("The speed at which the positions and rotations interpolate to the respective default values inbetween valid / grounded and invalid / not grounded state changes.")]
		protected float m_resetToDefaultValuesSpeed = 1f;

		[SerializeField]
		[Tooltip("This is a special behaviour that takes into consideration that the character controller is crouching. This behaviour adapts both feet and body placement accordingly to this condition.")]
		protected bool m_activateCrouchSpecificBehaviour;

		[SerializeField]
		[Range(0f, 1f)]
		[Tooltip("A value of 0 means that your character has no correction tolerance for feet IKs when he crouches therefore he balances the computed IK-corrections using his body position. A value of 1 means 100% tolerance and basically disables the crouching specific behaviour.")]
		protected float m_crouchCorrectionTolerance;

		[SerializeField]
		[Tooltip("A list of all animator states (in the animator controller), that will force activate the crouching behaviour when they are active. This is a very handy feature to activate the crouching specific behaviour when your character starts crouching without writing a line of code. Layer == animator layer. Animator state == animator state name.")]
		protected List<AnimatorStateEntry> m_forceActivateCrouchingBehaviour = new List<AnimatorStateEntry>();

		[SerializeField]
		[Tooltip("A list of all animator states (in the animator controller), that will disable this effect when they are active. Layer == animator layer. Animator state == animator state name.")]
		protected List<AnimatorStateEntry> m_forceInvalidOnAnimatorStates = new List<AnimatorStateEntry>();

		[SerializeField]
		[Tooltip("This parameter shifts the characters whole body and IK positions along the characters local Up-axis. Use this to correct root position offsets between feets and root position \"globally\". The same can be done per animation file under the corresponding Animation import-settings tab by the Offset variable of Root Transform Position (Y).")]
		protected float m_globalCharacterYOffset;

		[SerializeField]
		[Tooltip("If false, Global Character Offset won't affect Hand IKs and Hand Hint IKs. If true, it will apply the Global Character Y Offset to those IKs as well, however it won't affect their weight. If you use custom HandIKs for your character this setting may be relevant to the overall behaviour.")]
		protected bool m_applyGlobalCharacterYOffsetToOtherIKsToo = true;

		[Header("Foot IK placement")]
		[SerializeField]
		[Range(0f, 1f)]
		[Tooltip("The strength that the effects will be applyed to.")]
		protected float m_ikPlacementWeight = 1f;

		[SerializeField]
		[Range(1f, 50f)]
		[Tooltip("This value is used to smoothly lerp IK positions. The higher the value, the stiffer the transition. The smaller the value, the smoother the transition.")]
		protected float m_ikPlacementStiffness = 8f;

		[SerializeField]
		[Range(1f, 50f)]
		[Tooltip("This value is used to smoothly slerp IK rotations. The higher the value, the stiffer the transition. The smaller the value, the smoother the transition.")]
		protected float m_ikRotationStiffness = 8f;

		[SerializeField]
		[Tooltip("With this parameter you can choose between different interpolation methods for IK foot placement.")]
		protected InterpolationMethod m_ikInterpolationMethod;

		[SerializeField]
		[Range(0f, 5f)]
		[Tooltip("The IK upwards extrapolation defines how much the feet overshoots when trying to achive the IK target position. Upwards in this context means that the player moves upwards. A value > 0 will lead to a more robust and solid transition. A too high value may lead to weird looking transitions.")]
		protected float m_ikUpwardsExtrapolation = 1f;

		[SerializeField]
		[Range(0f, 10f)]
		[Tooltip("With this parameter you can improve the behaviour when going up on slopes. A higher value will pull the feet faster to the ground when they are in the air. A too high value may cause too much stretching on the legs when going upwards.")]
		protected float m_ikUpwardsPullBackDownExtrapolation;

		[SerializeField]
		[Range(0f, 10f)]
		[Tooltip("The IK downwards extrapolation defines how much the feet overshoots when trying to achive the IK target position. Downwards in this context means that the player moves downwards. A value > 0 will lead to a more robust and solid transition. A too high value may cause minor vibrations.")]
		protected float m_ikDownwardsExtrapolation = 2.5f;

		[SerializeField]
		[Range(0f, 1f)]
		[Tooltip("A value of 1 will use the foots length and height parameters and adapt them accordingly depending on the internal rotation of the foot.")]
		protected float m_automaticFootRotationBasedHeightAdaption = 0.7f;

		[SerializeField]
		[Tooltip("The maximum IK correction for placing the feet.")]
		protected float m_ikMaxCorrection = 0.5f;

		[SerializeField]
		[Tooltip("An additional raycast margin to find the correct IK position.")]
		protected float m_ikCorrectionRaycastMargin = 0.05f;

		[SerializeField]
		[Tooltip("This value defines the distance between IK position and the foot ground-point. It's like the height of the foot. This value is not allowed to be zero or smaller zero.")]
		protected float m_ikFootHeight = 0.1f;

		[SerializeField]
		[Tooltip("The IK foot size defines the length of the foot used for the simulation model. This value is not allowed to be zero or smaller zero.")]
		protected float m_ikFootLength = 0.24f;

		[SerializeField]
		[Tooltip("The IK foot width defines the width of the foot used for the simulation model. This value is not allowed to be zero or smaller zero.")]
		protected float m_ikFootWidth = 0.085f;

		[SerializeField]
		[Tooltip("The IK foot forward bias defines the offset for the foots raycast origin in relation to the given IK position")]
		protected float m_ikFootForwardBias = 0.065f;

		[SerializeField]
		[Range(0f, 90f)]
		[Tooltip("With this parameter you can clamp the maximum allowed ankle adaption angle.")]
		protected float m_maxAnkleRotationAdaptionAngle = 90f;

		[SerializeField]
		[Range(0f, 250f)]
		[Tooltip("This is an advanced setting with which intersections, when the feet move downwards, are stopped. A low value will let the feet bump inside the ground for a short period of time.")]
		protected float m_antiDownwardsIntersectionStiffness = 50f;

		[SerializeField]
		[Range(0f, 90f)]
		[Tooltip("This is an advanced setting where if this value is exceeded, the detected ground won't be handled as detected anymore.")]
		protected float m_maxGroundAngleAtWhichTheGroundIsDetectedAsGround = 81f;

		[SerializeField]
		[Range(0f, 90f)]
		[Tooltip("This is an advanced setting with which you can setup the maximum allowed angle the feet will adapt to.")]
		protected float m_maxGroundAngleTheFeetsCanAdaptTo = 79f;

		[SerializeField]
		[Range(0f, 0.15f)]
		[Tooltip("This is an advanced setting with which you can set the minimum threshold required of the IK to move before a new IK position will be calculated.")]
		protected float m_minimumMovementThreshold = 0.003f;

		[SerializeField]
		[Tooltip("This is an advanced setting. When enabled, iStep will check if the underneath ground moved. If the underneath ground moved, iStep will ignore minimum movement threshold and force calculate a new IK position.")]
		protected bool m_accountMinimumMovementThresholdForMovingGroundObjects = true;

		[SerializeField]
		[Range(0f, 1f)]
		[Tooltip("This value defines when to start gluing the feets (applying ground-slope rotation to the feet) to the ground in relation to the distance to the current and target feet positions.")]
		protected float m_isGluedToGroundNormalizedThreshold = 0.7f;

		[SerializeField]
		[Tooltip("This value defines when to start gluing the feets (applying ground-slope rotation to the feet) to the ground based on the distance of the feet from the ground.")]
		protected float m_isGluedToGroundDistanceThreshold = 0.055f;

		[SerializeField]
		[Tooltip("If this value is true, rotations will be \"unlocked\" to their original rotation when not glued to the ground. If this value is false the foot rotation will try to adapt to the ground whenever grounded. The recommended value for having better transitions is typically true. [Default: true]")]
		protected bool m_onlyAdaptRotationWhenGluedToGround = true;

		[SerializeField]
		[Tooltip("IK_HINT_POSITION_BASED takes the given IK Hint Positions through animator.GetIKHintPosition(..) and calculates the new adapted hint positions based on the given value. BONE_BASED uses the given bone-structure and direct bone values instead to calculate the correct hint positions. When using multiple layers + Avatar Masks, BONE_BASED could be the preferred value to use because Unity has difficulties giving correctly blended IK hint position values when using multiple layers + masks.")]
		protected IkHintComputationStrategy m_computeIkHintPositionStrategy;

		[Header("Body placement")]
		[SerializeField]
		[Range(0f, 1f)]
		[Tooltip("The strength that the effects will be applyed to.")]
		protected float m_bodyPlacementWeight = 1f;

		[SerializeField]
		[Range(1f, 50f)]
		[Tooltip("This value is used to smoothly lerp body position transitions. The higher the value, the stiffer the transition. The smaller the value, the smoother the transition.")]
		protected float m_bodyStiffness = 8f;

		[SerializeField]
		[Tooltip("With this parameter you can choose between different interpolation methods for body position placement.")]
		protected InterpolationMethod m_bodyPositionInterpolationMethod;

		[SerializeField]
		[Tooltip("The maximum possible body position correction.")]
		protected float m_bodyPositionMaxCorrection = 0.7f;

		[SerializeField]
		[Range(0f, 0.3f)]
		[Tooltip("The tolerance is used to avoid stuttering on edge-cases where your characters position-height is at about the same distance away from the adjacent ground as defined by BodyPositionMaxCorrection. A tolerance value of 0 would in the explained edge-case lead to trying to correctly place and adapt the body position in one frame and avoid placing the body position in the next frame - causing stuttering. By increasing the tolerance value you increase the buffer used to avoid stuttering. However the higher the value the less likely it is that your character will start adapting its body position in regard to BodyPositionMaxCorrection since the tolerance is uses subtractive.")]
		protected float m_bodyPositionMaxCorrectionTolerance = 0.05f;

		[SerializeField]
		[Range(0f, 1f)]
		[Tooltip("The strength at which the maximum possible body position that can be corrected will increase with the planar forwrad distance of the left and right feet.")]
		protected float m_increaseBodyPositionMaxCorrectionWithForwardFootDistance;

		[SerializeField]
		[Tooltip("If enabled and the collision layers for FootIK for example differs from your character controllers collision layers, iStep will adapt the body position upwards as well if necessary to prevent bending over. [Default: true]")]
		protected bool m_canBodyPositionMoveHigherThanGroundedPosition = true;

		[SerializeField]
		[Tooltip("This parameter applys an offset in the Up-axis to the bodys center of mass position. This is similar to GlobalCharacterYOffset however this value doesn't shift the whole body by affecting the IKs in addition like GlobalCharacterYOffset does. Therefore use this value for cool, dynamic special effects, like crouching or simple shifts, however use GlobalCharacterYOffset to fix feet-zero-position offsets with your animations and character. If this parameter is used for crouching or dynamic crouch-like behaviours you may think about activating ActivateCrouchSpecificBehaviour additionally.")]
		protected float m_bodyPositionOffset;

		[Header("Grounded-Check")]
		[SerializeField]
		[Tooltip("The maximum raydistance to check for being grounded.")]
		protected float m_checkGroundedDistance = 0.7f;

		[SerializeField]
		[Tooltip("The radius used to check if grounded.")]
		protected float m_checkGroundedRadius = 0.2f;

		[SerializeField]
		[Tooltip("Automatically unground when the upwards-velocity exceeds the given value. If the value is < 0 (for example -1), this effect is disabled. Be cautios when using this effect because moving up-stairs and up-hill may trigger this effect even if you don't want or don't expect it.")]
		protected float m_autoUngroundUpwardsVelocity = -1f;

		[Header("Slope-based spine bending/leaning")]
		[SerializeField]
		[Range(-100f, 100f)]
		[Tooltip("Using leaning is not fully supported when used with Animation Rigging! This parameter defines how much the characters spine should bend forward when running up on slopes.")]
		protected float m_slopeBendingUpwardsStrength;

		[SerializeField]
		[Range(-100f, 100f)]
		[Tooltip("Using leaning is not fully supported when used with Animation Rigging! This parameter defines how much the characters spine should bend backward when running down on slopes.")]
		protected float m_slopeBendingDownwardsStrength;

		[SerializeField]
		[Range(0.2f, 20f)]
		[Tooltip("This value is used to smoothly lerp slope-bending transitions. The higher the value, the stiffer the transition. The smaller the value, the smoother the transition.")]
		protected float m_slopeBendingTransitionStiffness = 10f;

		[SerializeField]
		[Range(1f, 50f)]
		[Tooltip("This value defines the number of slope-bending values to calculate a running average. It is synced with the FixedUpdate. If your FixedUpdate-Rate is set to the default 50, a queue count value of 20 would lead to using the running average of the last 20/50 secs --> 0.4 seconds. This value is used to smooth spiky terrain-surfaces or staircase-like steps your character has to take. A too low value leads to spiky transitions. A too high value leads to unresponsive transitions.")]
		protected int m_averageSlopeLeaningFixedQueueCount = 20;

		[SerializeField]
		[Range(0f, 90f)]
		[Tooltip("This parameter defines the maximum absolute value (in degree) slope-based spine bending can reach. It basically clamps the bending values.")]
		protected float m_maxAbsoluteSlopeSpineBending = 90f;

		[SerializeField]
		[Tooltip("Select your prefered slope-based bending behaviour with this setting.")]
		protected SlopeLeaningType m_slopeLeaningType = SlopeLeaningType.SURFACE_ANGLE_DETECTED_BY_GROUNDED_CHECK;

		[SerializeField]
		[Tooltip("If enabled the characters shoulders will counteract the spine-bending. If you use custom HandIKs for your character this setting may be relevant to the overall behaviour.")]
		protected bool m_shouldersCounteractSpineBending = true;

		[SerializeField]
		[Tooltip("If enabled velocity will be forced to 5 independent from the real characters velocity making the effect being played whenever on slopes indipendent from movement.")]
		protected bool m_ignoreVelocityDependency;

		[SerializeField]
		protected Axis m_relativeSpineRotationAxisAffectedBySlopeLeaning;

		[Header("Full-body exaggerated leaning")]
		[SerializeField]
		[Range(0f, 20f)]
		[Tooltip("Using leaning is not fully supported when used with Animation Rigging! This parameter defines how much the characters body should lean towards the moving direction when moving. This affects the whole body from the bottom of the feet to the top of the head and is always applied when the character moves.")]
		protected float m_moveLeaningStrength;

		[SerializeField]
		[Range(0f, 2f)]
		[Tooltip("Using leaning is not fully supported when used with Animation Rigging! This parameter defines how much the characters body should kneel when leaning towards the moving direction when moving. This effect is only applied if MoveLeaningStrength is bigger 0.01 and the character simultaneously moves.")]
		protected float m_moveLeaningKneelFactor;

		[SerializeField]
		[Range(0f, 40f)]
		[Tooltip("Using leaning is not fully supported when used with Animation Rigging! This parameter defines how much the characters spine should lean towards the moving direction when moving and is always applied when the character moves.")]
		protected float m_moveSpineLeaningStrength;

		[SerializeField]
		[Range(1f, 100f)]
		[Tooltip("This value is used to smoothly lerp move-leaning transitions. The higher the value, the stiffer the transition. The smaller the value, the smoother the transition.")]
		protected float m_moveLeaningStiffness = 12f;

		[SerializeField]
		[Range(0f, 1f)]
		[Tooltip("With this value you can change how much the leaning is applied as start-stop behaviour instead of a continued leaning.")]
		protected float m_moveLeaningIsStartStopPercentage;

		[SerializeField]
		[Range(1f, 100f)]
		[Tooltip("The higher the value the longer the Start leaning will be.")]
		protected float m_moveLeaningIsStartStopStartDurationStrength = 20f;

		[SerializeField]
		[Range(1f, 100f)]
		[Tooltip("The higher the value the longer the Stop leaning transition will take until it finishs.")]
		protected float m_moveLeaningIsStartStopStopDurationStrength = 15f;

		[SerializeField]
		[Tooltip("Leans when the actor is grounded - like walking and running. [Default: true]")]
		protected bool m_moveLeanWhenGrounded = true;

		[SerializeField]
		[Tooltip("Leans when the actor is not grounded - like jumping or flying. [Default: true]")]
		protected bool m_moveLeanWhenNotGrounded = true;

		[SerializeField]
		[Tooltip("Flips the left/right leaning direction. [Default: false]")]
		protected bool m_flipRightMoveLean;

		[SerializeField]
		[Tooltip("Flips the forward/backward leaning direction. [Default: false]")]
		protected bool m_flipForwardMoveLean;

		[Header("Footstep Events")]
		[SerializeField]
		[Tooltip("The distance from foot to ground used to try to trigger a footstep event in regard to the characters up-axis.")]
		protected float m_eventCheckResetDistance = 0.05f;

		[SerializeField]
		[Tooltip("The step length used to fire step events.")]
		protected float m_stepLength = 0.7f;

		[SerializeField]
		[Tooltip("A delay that can be applied for when a footstep event is being recognised and when it is being triggered.")]
		protected float m_footstepEventDelay;

		[Space]
		[SerializeField]
		[Tooltip("Whenever the left foot steps on the ground")]
		protected IKResultEvent m_onFootstepLeftStart;

		[SerializeField]
		[Tooltip("Whenever the right foot steps on the ground")]
		protected IKResultEvent m_onFootstepRightStart;

		[SerializeField]
		[Tooltip("Whenever the left foot stops stepping on the ground")]
		protected UnityEvent m_onFootstepLeftStop;

		[SerializeField]
		[Tooltip("Whenever the right foot stops stepping on the ground")]
		protected UnityEvent m_onFootstepRightStop;

		[SerializeField]
		[Tooltip("Fires whenever the grounded state is entered. Depends on if grounded check is enabled.")]
		protected GroundedResultEvent m_onGroundedEntered;

		[SerializeField]
		[Tooltip("Fires whenever the grounded state is leaved. Depends on if grounded check is enabled.")]
		protected GroundedResultEvent m_onGroundedExited;

		[Header("Debugging")]
		[SerializeField]
		protected bool m_isGrounded;

		[SerializeField]
		protected float m_currBodyPositionOffset;

		[SerializeField]
		protected float m_maxBodyPositionOffset;

		[SerializeField]
		protected float m_currIkCorrectionOffset;

		[SerializeField]
		protected float m_maxIkCorrectionOffset;

		[SerializeField]
		protected float m_upwardsVelocity;

		[SerializeField]
		protected float m_maxUpwardsVelocity;

		protected bool m_isWaitingForPlayingLeftFootEvent = true;

		protected bool m_isWaitingForPlayingRightFootEvent = true;

		protected Vector3 m_lastRightIKStepEventPos;

		protected Vector3 m_lastLeftIKStepEventPos;

		protected Animator m_animator;

		protected Transform m_transform;

		protected Vector3 m_prevTransformPosition;

		protected Vector3 m_prevBodyPosition;

		protected Vector3 m_bodyOffset;

		protected Vector3 m_rawbodyOffsetVec;

		protected Vector3 m_ikLeftOffset;

		protected Vector3 m_ikRightOffset;

		protected Quaternion m_ikLeftDeltaRotation;

		protected Quaternion m_ikRightDeltaRotation;

		protected Vector3 m_ikLeftOffsetSmoothDampVelocity;

		protected Vector3 m_ikRightOffsetSmoothDampVelocity;

		protected Vector3 m_ikBodyPositionSmoothDampVelocity;

		protected float m_feetResetLerp;

		protected float m_bodyResetLerp;

		protected Vector3 m_rawVelocityPlanarMoveLean = Vector3.zero;

		protected Vector3 m_velocityPlanarMoveLean = Vector3.zero;

		protected Vector3 m_velocityPlanarSlopeLean = Vector3.zero;

		protected Vector3 m_extrapolationLeft = Vector3.zero;

		protected Vector3 m_extrapolationRight = Vector3.zero;

		protected float m_ikFootHeightToUseLeft;

		protected float m_ikFootHeightToUseRight;

		protected float m_ikFootLengthToUseLeft;

		protected float m_ikFootLengthToUseRight;

		protected float m_currSlopeLeaning;

		protected Queue<float> m_averageSlopeLeaningValuesQueue = new Queue<float>();

		protected bool m_updateSlopeLeaning = true;

		protected IKResult m_resultLeft;

		protected IKResult m_resultRight;

		protected Vector3 m_resultLeftLastTransformPosition;

		protected Quaternion m_resultLeftLastTransformRotation;

		protected Vector3 m_resultRightLastTransformPosition;

		protected Quaternion m_resultRightLastTransformRotation;

		protected Vector3 m_leftHandStartPos;

		protected Vector3 m_leftShoulderStartPos;

		protected Vector3 m_rightHandStartPos;

		protected Vector3 m_rightShoulderStartPos;

		protected Vector3 m_leftHandOffsetVec;

		protected Vector3 m_leftShoulderOffsetVec;

		protected Vector3 m_rightHandOffsetVec;

		protected Vector3 m_rightShoulderOffsetVec;

		protected Transform m_spineTransform;

		protected Transform m_leftShoulderTransform;

		protected Transform m_rightShoulderTransform;

		protected Transform m_leftUpperLeg;

		protected Transform m_rightUpperLeg;

		protected Transform m_leftLowerLeg;

		protected Transform m_rightLowerLeg;

		protected Transform m_leftFoot;

		protected Transform m_rightFoot;

		protected Transform m_rightHandTransform;

		protected Transform m_leftHandTransform;

		protected Vector3 m_crouchOffset;

		protected Vector3 m_prevIkLeft;

		protected Vector3 m_prevIkRight;

		protected Vector3 m_lastFootstepPosLeft;

		protected Vector3 m_lastFootstepPosRight;

		protected float m_elapsedFootstepLeftDistance;

		protected float m_elapsedFootstepRightDistance;

		protected bool m_lastEventFiredWasLeft = true;

		protected bool m_ignoreLastEventFiredWasLeft = true;

		protected bool m_canIgnoreLastEventFiredWasLeft = true;

		protected float m_prevIkFootHeight;

		protected float m_prevIkFootForwardBias;

		protected float m_prevIkMaxCorrection;

		protected float m_prevIkFootLength;

		protected float m_prevIkFootWidth;

		protected float m_prevBodyPositionMaxCorrection;

		protected float m_prevCheckGroundedDistance;

		protected LayerMask m_prevCollisionLayerMask;

		protected GroundedResult m_groundedResult = new GroundedResult(_isValid: false, _isGrounded: false, null, Vector3.zero, Vector3.zero, Vector3.zero);

		protected GroundedResult m_lastPotentiallyValidGroundedResult = new GroundedResult(_isValid: false, _isGrounded: false, null, Vector3.zero, Vector3.zero, Vector3.zero);

		protected Transform m_groundedResultLastTransform;

		protected Vector3 m_groundedResultLastTransformPosition;

		protected Quaternion m_groundedResultLastTransformRotation;

		protected ValidationType m_validationType;

		protected Axis m_leftAxisHint;

		protected bool m_isLeftAxisHintNegative;

		protected Axis m_rightAxisHint;

		protected bool m_isRightAxisHintNegative;

		protected List<KeyValuePair<int, int>> m_deactiveOnAnimatorState = new List<KeyValuePair<int, int>>();

		protected List<KeyValuePair<int, int>> m_activateCrouchingOnAnimatorState = new List<KeyValuePair<int, int>>();

		protected Vector3 m_characterExternalMovingPlatformOffsetVec = Vector3.zero;

		protected float m_prevFootstepEventDelay;

		protected WaitForSeconds m_footstepEventWaitForSeconds;

		protected Vector3 m_moveLeanChangeVec;

		protected Vector3 m_velocityPlanarMoveLeanChange;

		public int animatorIkPassLayerIndex => m_animatorIkPassLayerIndex;

		public LayerMask collisionLayerMask
		{
			get
			{
				return m_collisionLayerMask;
			}
			set
			{
				m_collisionLayerMask = value;
			}
		}

		public QueryTriggerInteraction triggerCollisionInteraction
		{
			get
			{
				return m_triggerCollisionInteraction;
			}
			set
			{
				m_triggerCollisionInteraction = value;
			}
		}

		public float globalCharacterYOffset
		{
			get
			{
				return m_globalCharacterYOffset;
			}
			set
			{
				m_globalCharacterYOffset = value;
			}
		}

		public float ikUpwardsPullBackDownExtrapolation
		{
			get
			{
				return m_ikUpwardsPullBackDownExtrapolation;
			}
			set
			{
				m_ikUpwardsPullBackDownExtrapolation = value;
			}
		}

		public float ikMaxCorrection
		{
			get
			{
				return m_ikMaxCorrection;
			}
			set
			{
				m_ikMaxCorrection = value;
			}
		}

		public float ikCorrectionRaycastMargin
		{
			get
			{
				return m_ikCorrectionRaycastMargin;
			}
			set
			{
				m_ikCorrectionRaycastMargin = value;
			}
		}

		public float maxGroundAngleAtWhichTheGroundIsDetectedAsGround
		{
			get
			{
				return m_maxGroundAngleAtWhichTheGroundIsDetectedAsGround;
			}
			set
			{
				m_maxGroundAngleAtWhichTheGroundIsDetectedAsGround = value;
			}
		}

		public float maxGroundAngleTheFeetsCanAdaptTo
		{
			get
			{
				return m_maxGroundAngleTheFeetsCanAdaptTo;
			}
			set
			{
				m_maxGroundAngleTheFeetsCanAdaptTo = value;
			}
		}

		public IkHintComputationStrategy computeIkHintPositionStrategy
		{
			get
			{
				return m_computeIkHintPositionStrategy;
			}
			set
			{
				m_computeIkHintPositionStrategy = value;
			}
		}

		public float bodyPlacementWeight
		{
			get
			{
				return m_bodyPlacementWeight;
			}
			set
			{
				m_bodyPlacementWeight = value;
			}
		}

		public float bodyPositionMaxCorrection
		{
			get
			{
				return m_bodyPositionMaxCorrection;
			}
			set
			{
				m_bodyPositionMaxCorrection = value;
			}
		}

		public float bodyPositionOffset
		{
			get
			{
				return m_bodyPositionOffset;
			}
			set
			{
				m_bodyPositionOffset = value;
			}
		}

		public float checkGroundedDistance
		{
			get
			{
				return m_checkGroundedDistance;
			}
			set
			{
				m_checkGroundedDistance = value;
			}
		}

		public float checkGroundedRadius
		{
			get
			{
				return m_checkGroundedRadius;
			}
			set
			{
				m_checkGroundedRadius = value;
			}
		}

		public float autoUngroundUpwardsVelocity
		{
			get
			{
				return m_autoUngroundUpwardsVelocity;
			}
			set
			{
				m_autoUngroundUpwardsVelocity = value;
			}
		}

		public float eventCheckResetDistance
		{
			get
			{
				return m_eventCheckResetDistance;
			}
			set
			{
				m_eventCheckResetDistance = value;
			}
		}

		public float stepLength
		{
			get
			{
				return m_stepLength;
			}
			set
			{
				m_stepLength = value;
			}
		}

		public float footstepEventDelay
		{
			get
			{
				return m_footstepEventDelay;
			}
			set
			{
				m_footstepEventDelay = value;
			}
		}

		public IKResultEvent onFootstepLeftStart => m_onFootstepLeftStart;

		public IKResultEvent onFootstepRightStart => m_onFootstepRightStart;

		public UnityEvent onFootstepLeftStop => m_onFootstepLeftStop;

		public UnityEvent onFootstepRightStop => m_onFootstepRightStop;

		public GroundedResultEvent onGroundedEntered => m_onGroundedEntered;

		public GroundedResultEvent onGroundedExited => m_onGroundedExited;

		public Transform targetTransform
		{
			get
			{
				return m_transform;
			}
			set
			{
				m_transform = value;
			}
		}

		public Vector3 bodyOffset => m_bodyOffset;

		public Vector3 fullBodyOffset
		{
			get
			{
				if (m_transform == null)
				{
					return Vector3.zero;
				}
				return m_globalCharacterYOffset * m_transform.up + (m_bodyOffset + m_bodyPositionOffset * m_transform.up) * m_bodyPlacementWeight * m_bodyResetLerp + m_crouchOffset;
			}
		}

		public Vector3 crouchOffset => m_crouchOffset;

		public Vector3 characterExternalMovingPlatformOffsetVec
		{
			get
			{
				return m_characterExternalMovingPlatformOffsetVec;
			}
			set
			{
				m_characterExternalMovingPlatformOffsetVec = value;
			}
		}

		public Vector3 getLeftHandOffsetVecIncludingLeaning()
		{
			return m_leftHandOffsetVec;
		}

		public Vector3 getLeftShoulderOffsetVecIncludingLeaning()
		{
			return m_leftShoulderOffsetVec;
		}

		public Vector3 getRightHandOffsetVecIncludingLeaning()
		{
			return m_rightHandOffsetVec;
		}

		public Vector3 getRightShoulderOffsetVecIncludingLeaning()
		{
			return m_rightShoulderOffsetVec;
		}

		public GroundedResult getGroundedResult()
		{
			return m_groundedResult;
		}

		public virtual InterpolationMethod getIkInterpolationMethod()
		{
			return m_ikInterpolationMethod;
		}

		public virtual void setIkInterpolationMethod(InterpolationMethod method)
		{
			m_ikInterpolationMethod = method;
		}

		public virtual InterpolationMethod getBodyPositionInterpolationMethod()
		{
			return m_bodyPositionInterpolationMethod;
		}

		public virtual void setBodyPositionInterpolationMethod(InterpolationMethod method)
		{
			m_bodyPositionInterpolationMethod = method;
		}

		public virtual void setInterpolationMethod(int method)
		{
			m_ikInterpolationMethod = (InterpolationMethod)method;
			m_bodyPositionInterpolationMethod = (InterpolationMethod)method;
		}

		public virtual float getMinimumMovementThreshold()
		{
			return m_minimumMovementThreshold;
		}

		public virtual void setMinimumMovementThreshold(float value)
		{
			m_minimumMovementThreshold = value;
		}

		public virtual void setBodyAndIkStiffnessAlltogether(float value)
		{
			m_ikPlacementStiffness = value;
			m_ikRotationStiffness = value;
			m_bodyStiffness = value;
		}

		public virtual void setIsGluedToGroundDistanceThreshold(float value)
		{
			m_isGluedToGroundDistanceThreshold = value;
		}

		public virtual float getIsGluedToGroundDistanceThreshold()
		{
			return m_isGluedToGroundDistanceThreshold;
		}

		public virtual void setAntiDownwardsIntersectionStiffness(float value)
		{
			m_antiDownwardsIntersectionStiffness = value;
		}

		public virtual float getAntiDownwardsIntersectionStiffness()
		{
			return m_antiDownwardsIntersectionStiffness;
		}

		public virtual void setIkRotationStiffness(float value)
		{
			m_ikRotationStiffness = value;
		}

		public virtual float getIkRotationStiffness()
		{
			return m_ikRotationStiffness;
		}

		public virtual void setIkPlacementStiffness(float value)
		{
			m_ikPlacementStiffness = value;
		}

		public virtual float getIkPlacementStiffness()
		{
			return m_ikPlacementStiffness;
		}

		public virtual void setBodyStiffness(float value)
		{
			m_bodyStiffness = value;
		}

		public virtual float getBodyStiffness()
		{
			return m_bodyStiffness;
		}

		public virtual void setActivateCrouchingBehaviour(bool isCrouching)
		{
			m_activateCrouchSpecificBehaviour = isCrouching;
		}

		public virtual void setIsValidAndShouldCheckForGrounded(ValidationType validation)
		{
			m_validationType = validation;
		}

		public virtual void setMoveLeaningStrength(float value)
		{
			m_moveLeaningStrength = value;
		}

		public virtual void setMoveLeaningKneelFactor(float value)
		{
			m_moveLeaningKneelFactor = value;
		}

		public virtual void setMoveLeaningKneelFactorWithMultiplier005(float value)
		{
			m_moveLeaningKneelFactor = value * 0.05f;
		}

		public virtual void setSlopeBendingUpwardsStrength(float value)
		{
			m_slopeBendingUpwardsStrength = value;
		}

		public virtual void setSlopeBendingDownwardsStrength(float value)
		{
			m_slopeBendingDownwardsStrength = value;
		}

		public virtual void setShouldersCounteractSpineBending(bool value)
		{
			m_shouldersCounteractSpineBending = value;
		}

		public virtual void setBodyPlacementWeight(float weight)
		{
			m_bodyPlacementWeight = weight;
		}

		public virtual void setFootPlacementWeight(float weight)
		{
			m_ikPlacementWeight = weight;
		}

		public virtual void setBodyPositionOffset(float offset)
		{
			m_bodyPositionOffset = offset;
		}

		public virtual void setCanBodyPositionMoveHigherThanGroundedPosition(bool value)
		{
			m_canBodyPositionMoveHigherThanGroundedPosition = value;
		}

		public virtual void setIKExtrapolation(float extrapolation)
		{
			m_ikUpwardsExtrapolation = extrapolation;
			m_ikDownwardsExtrapolation = extrapolation * 2.5f;
		}

		public virtual void setIKUpwardsExtrapolation(float extrapolation)
		{
			m_ikUpwardsExtrapolation = extrapolation;
		}

		public virtual void setIKDownwardsExtrapolation(float extrapolation)
		{
			m_ikDownwardsExtrapolation = extrapolation;
		}

		public virtual void tryAddToForceInvalidOnAnimatorState(AnimatorStateEntry newEntry)
		{
			foreach (AnimatorStateEntry forceInvalidOnAnimatorState in m_forceInvalidOnAnimatorStates)
			{
				if (forceInvalidOnAnimatorState.animatorState == newEntry.animatorState)
				{
					return;
				}
			}
			m_forceInvalidOnAnimatorStates.Add(newEntry);
			int num = Animator.StringToHash(newEntry.animatorState);
			foreach (KeyValuePair<int, int> item in m_deactiveOnAnimatorState)
			{
				if (item.Value == num)
				{
					return;
				}
			}
			m_deactiveOnAnimatorState.Add(new KeyValuePair<int, int>(newEntry.layer, num));
		}

		protected virtual void Awake()
		{
			m_transform = base.transform;
			m_animator = GetComponent<Animator>();
			if (m_animator != null)
			{
				m_spineTransform = m_animator.GetBoneTransform(HumanBodyBones.Spine);
				m_leftShoulderTransform = m_animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
				m_rightShoulderTransform = m_animator.GetBoneTransform(HumanBodyBones.RightShoulder);
				m_prevBodyPosition = m_animator.GetBoneTransform(HumanBodyBones.Hips).position;
				m_leftUpperLeg = m_animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
				m_rightUpperLeg = m_animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
				m_leftLowerLeg = m_animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
				m_rightLowerLeg = m_animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
				m_leftFoot = m_animator.GetBoneTransform(HumanBodyBones.LeftFoot);
				m_rightFoot = m_animator.GetBoneTransform(HumanBodyBones.RightFoot);
				m_rightHandTransform = m_animator.GetBoneTransform(HumanBodyBones.RightHand);
				m_leftHandTransform = m_animator.GetBoneTransform(HumanBodyBones.LeftHand);
				findLegsForwardAxes(out m_leftAxisHint, out m_isLeftAxisHintNegative, out m_rightAxisHint, out m_isRightAxisHintNegative);
			}
			m_prevTransformPosition = m_transform.position;
			m_resultLeft = new IKResult(Vector3.zero, Vector3.zero, _isGlued: false, Vector3.zero, Vector3.zero, Vector3.up, null, null);
			m_resultRight = new IKResult(Vector3.zero, Vector3.zero, _isGlued: false, Vector3.zero, Vector3.zero, Vector3.up, null, null);
			foreach (AnimatorStateEntry forceInvalidOnAnimatorState in m_forceInvalidOnAnimatorStates)
			{
				m_deactiveOnAnimatorState.Add(new KeyValuePair<int, int>(forceInvalidOnAnimatorState.layer, Animator.StringToHash(forceInvalidOnAnimatorState.animatorState)));
			}
			foreach (AnimatorStateEntry item in m_forceActivateCrouchingBehaviour)
			{
				m_activateCrouchingOnAnimatorState.Add(new KeyValuePair<int, int>(item.layer, Animator.StringToHash(item.animatorState)));
			}
			for (int i = 0; i < m_averageSlopeLeaningFixedQueueCount; i++)
			{
				m_averageSlopeLeaningValuesQueue.Enqueue(0f);
			}
			m_footstepEventWaitForSeconds = new WaitForSeconds(m_footstepEventDelay);
			m_prevFootstepEventDelay = m_footstepEventDelay;
		}

		protected virtual void Start()
		{
			m_prevTransformPosition = m_transform.position;
			m_ikFootHeightToUseLeft = m_ikFootHeight;
			m_ikFootHeightToUseRight = m_ikFootHeight;
			m_ikFootLengthToUseLeft = m_ikFootLength;
			m_ikFootLengthToUseRight = m_ikFootLength;
		}

		public virtual void reinitialize()
		{
			Awake();
			Start();
		}

		protected virtual void FixedUpdate()
		{
			m_updateSlopeLeaning = true;
		}

		protected virtual void OnAnimatorIK(int layerIdx)
		{
			if (layerIdx != m_animatorIkPassLayerIndex)
			{
				return;
			}
			if (m_ikFootHeight < 0.001f)
			{
				m_ikFootHeight = 0.001f;
			}
			if (m_ikFootLength < 0.001f)
			{
				m_ikFootLength = 0.001f;
			}
			if (m_ikFootWidth < 0.001f)
			{
				m_ikFootWidth = 0.001f;
			}
			Vector3 position = m_transform.position;
			isValidAndGrounded(m_groundedResult, position, m_transform.up, m_collisionLayerMask);
			if (m_groundedResult.isGrounded && m_groundedResult.isValid)
			{
				if (!m_isGrounded)
				{
					if (m_onGroundedEntered != null)
					{
						m_onGroundedEntered.Invoke(m_groundedResult);
					}
					m_isGrounded = true;
					m_ikLeftOffset = Vector3.Lerp(Vector3.zero, m_ikLeftOffset, m_feetResetLerp);
					m_ikRightOffset = Vector3.Lerp(Vector3.zero, m_ikRightOffset, m_feetResetLerp);
					m_extrapolationLeft = Vector3.Lerp(Vector3.zero, m_extrapolationLeft, m_feetResetLerp);
					m_extrapolationRight = Vector3.Lerp(Vector3.zero, m_extrapolationRight, m_feetResetLerp);
				}
				updateIK(position);
				Vector3 vector = calculatePlanarVelocity(position);
				if (m_ignoreVelocityDependency)
				{
					m_velocityPlanarSlopeLean = m_transform.forward * 5f;
				}
				else
				{
					m_velocityPlanarSlopeLean = vector;
				}
				if (m_moveLeanWhenGrounded)
				{
					m_rawVelocityPlanarMoveLean = vector;
				}
				else
				{
					m_rawVelocityPlanarMoveLean = Vector3.zero;
				}
			}
			else
			{
				if (m_isGrounded)
				{
					if (m_onGroundedExited != null)
					{
						m_onGroundedExited.Invoke(m_lastPotentiallyValidGroundedResult);
					}
					m_isGrounded = false;
					m_lastPotentiallyValidGroundedResult.init(_isValid: false, _isGrounded: false, null, Vector3.zero, Vector3.zero, Vector3.zero);
				}
				revertUpdateIK();
				if (m_moveLeanWhenNotGrounded)
				{
					Vector3 rawVelocityPlanarMoveLean = calculatePlanarVelocity(position);
					m_rawVelocityPlanarMoveLean = rawVelocityPlanarMoveLean;
				}
				else
				{
					m_rawVelocityPlanarMoveLean = Vector3.zero;
				}
			}
			if ((bool)m_rightHandTransform)
			{
				m_rightHandStartPos = m_rightHandTransform.position + fullBodyOffset;
			}
			if ((bool)m_leftHandTransform)
			{
				m_leftHandStartPos = m_leftHandTransform.position + fullBodyOffset;
			}
			if ((bool)m_rightShoulderTransform)
			{
				m_rightShoulderStartPos = m_rightShoulderTransform.position + fullBodyOffset;
			}
			if ((bool)m_leftShoulderTransform)
			{
				m_leftShoulderStartPos = m_leftShoulderTransform.position + fullBodyOffset;
			}
			m_rightHandOffsetVec = Vector3.zero;
			m_leftHandOffsetVec = Vector3.zero;
			m_rightShoulderOffsetVec = Vector3.zero;
			m_leftShoulderOffsetVec = Vector3.zero;
			updateSlopeLeaning(m_groundedResult);
			updateLeaning(position);
			m_rightHandOffsetVec += fullBodyOffset;
			m_leftHandOffsetVec += fullBodyOffset;
			m_rightShoulderOffsetVec += fullBodyOffset;
			m_leftShoulderOffsetVec += fullBodyOffset;
			m_prevTransformPosition = position;
			m_characterExternalMovingPlatformOffsetVec = Vector3.zero;
			if (m_groundedResult.groundedTransform != null)
			{
				m_groundedResultLastTransform = m_groundedResult.groundedTransform;
				m_groundedResultLastTransformPosition = m_groundedResultLastTransform.position;
				m_groundedResultLastTransformRotation = m_groundedResultLastTransform.rotation;
			}
		}

		protected virtual void updateSlopeLeaning(GroundedResult groundedResult)
		{
			if (m_slopeLeaningType == SlopeLeaningType.DISABLED)
			{
				return;
			}
			if (m_updateSlopeLeaning)
			{
				float item = 0f;
				if (groundedResult.isGrounded && groundedResult.isValid)
				{
					if (m_slopeLeaningType == SlopeLeaningType.SURFACE_ANGLE_DETECTED_BY_GROUNDED_CHECK)
					{
						float num = 0f - Vector3.Dot(groundedResult.groundedNormal, m_transform.forward);
						item = ((!(num > 0f)) ? Mathf.Clamp(num * m_slopeBendingDownwardsStrength * m_velocityPlanarSlopeLean.magnitude, 0f - m_maxAbsoluteSlopeSpineBending, m_maxAbsoluteSlopeSpineBending) : Mathf.Clamp(num * m_slopeBendingUpwardsStrength * m_velocityPlanarSlopeLean.magnitude, 0f - m_maxAbsoluteSlopeSpineBending, m_maxAbsoluteSlopeSpineBending));
					}
					else if (m_slopeLeaningType == SlopeLeaningType.HEIGHT_OFFSET_DETECTED_BY_FEETS)
					{
						float num2 = 0f - Vector3.Dot(groundedResult.groundedNormal, m_transform.forward);
						float magnitude = (m_ikLeftOffset - m_ikRightOffset).magnitude;
						item = ((!(num2 > 0f)) ? Mathf.Clamp((0f - magnitude) * m_slopeBendingDownwardsStrength * m_velocityPlanarSlopeLean.magnitude, 0f - m_maxAbsoluteSlopeSpineBending, m_maxAbsoluteSlopeSpineBending) : Mathf.Clamp(magnitude * m_slopeBendingUpwardsStrength * m_velocityPlanarSlopeLean.magnitude, 0f - m_maxAbsoluteSlopeSpineBending, m_maxAbsoluteSlopeSpineBending));
					}
					else if (m_slopeLeaningType == SlopeLeaningType.AVERAGE_SURFACE_ANGLE_DETECTED_BY_FEETS)
					{
						float num3 = 0f - Vector3.Dot((m_resultLeft.normal + m_resultRight.normal) * 0.5f, m_transform.forward);
						item = ((!(num3 > 0f)) ? Mathf.Clamp(num3 * m_slopeBendingDownwardsStrength * m_velocityPlanarSlopeLean.magnitude, 0f - m_maxAbsoluteSlopeSpineBending, m_maxAbsoluteSlopeSpineBending) : Mathf.Clamp(num3 * m_slopeBendingUpwardsStrength * m_velocityPlanarSlopeLean.magnitude, 0f - m_maxAbsoluteSlopeSpineBending, m_maxAbsoluteSlopeSpineBending));
					}
				}
				m_averageSlopeLeaningValuesQueue.Dequeue();
				m_averageSlopeLeaningValuesQueue.Enqueue(item);
				m_updateSlopeLeaning = false;
			}
			float num4 = 0f;
			foreach (float item2 in m_averageSlopeLeaningValuesQueue)
			{
				num4 += item2;
			}
			if (m_averageSlopeLeaningValuesQueue.Count > 0)
			{
				num4 /= (float)m_averageSlopeLeaningValuesQueue.Count;
			}
			m_currSlopeLeaning = Mathf.Lerp(m_currSlopeLeaning, num4, Time.deltaTime * m_slopeBendingTransitionStiffness);
			if (m_relativeSpineRotationAxisAffectedBySlopeLeaning == Axis.X_AXIS)
			{
				m_spineTransform.localRotation = Quaternion.Euler(m_currSlopeLeaning, 0f, 0f) * m_spineTransform.localRotation;
				if (m_shouldersCounteractSpineBending)
				{
					m_animator.SetBoneLocalRotation(HumanBodyBones.LeftShoulder, Quaternion.Euler(0f - m_currSlopeLeaning, 0f, 0f) * m_leftShoulderTransform.localRotation);
					m_animator.SetBoneLocalRotation(HumanBodyBones.RightShoulder, Quaternion.Euler(0f - m_currSlopeLeaning, 0f, 0f) * m_rightShoulderTransform.localRotation);
				}
			}
			else if (m_relativeSpineRotationAxisAffectedBySlopeLeaning == Axis.Y_AXIS)
			{
				m_spineTransform.localRotation = Quaternion.Euler(0f, m_currSlopeLeaning, 0f) * m_spineTransform.localRotation;
				if (m_shouldersCounteractSpineBending)
				{
					m_animator.SetBoneLocalRotation(HumanBodyBones.LeftShoulder, Quaternion.Euler(0f, 0f - m_currSlopeLeaning, 0f) * m_leftShoulderTransform.localRotation);
					m_animator.SetBoneLocalRotation(HumanBodyBones.RightShoulder, Quaternion.Euler(0f, 0f - m_currSlopeLeaning, 0f) * m_rightShoulderTransform.localRotation);
				}
			}
			else
			{
				m_spineTransform.localRotation = Quaternion.Euler(0f, 0f, m_currSlopeLeaning) * m_spineTransform.localRotation;
				if (m_shouldersCounteractSpineBending)
				{
					m_animator.SetBoneLocalRotation(HumanBodyBones.LeftShoulder, Quaternion.Euler(0f, 0f, 0f - m_currSlopeLeaning) * m_leftShoulderTransform.localRotation);
					m_animator.SetBoneLocalRotation(HumanBodyBones.RightShoulder, Quaternion.Euler(0f, 0f, 0f - m_currSlopeLeaning) * m_rightShoulderTransform.localRotation);
				}
			}
			m_animator.SetBoneLocalRotation(HumanBodyBones.Spine, m_spineTransform.localRotation);
		}

		protected virtual Vector3 calculatePlanarVelocity(Vector3 feetZeroPos)
		{
			if (m_moveLeaningStrength < 0.001f && m_moveSpineLeaningStrength < 0.001f && m_slopeLeaningType == SlopeLeaningType.DISABLED)
			{
				return Vector3.zero;
			}
			Vector3 vector = Vector3.zero;
			if (Time.deltaTime > 0f)
			{
				vector = (feetZeroPos - (m_prevTransformPosition + m_characterExternalMovingPlatformOffsetVec)) / Time.deltaTime;
			}
			Vector3 vector2 = Vector3.Project(vector, m_transform.up);
			return vector - vector2;
		}

		protected virtual void updateLeaning(Vector3 feetZeroPos)
		{
			if (m_moveLeaningStrength < 0.001f && m_moveSpineLeaningStrength < 0.001f)
			{
				return;
			}
			Vector3 vector = m_animator.bodyPosition - feetZeroPos;
			m_velocityPlanarMoveLean = Vector3.Lerp(m_velocityPlanarMoveLean, m_rawVelocityPlanarMoveLean, Time.deltaTime * m_moveLeaningStiffness);
			if (Vector3.Dot((m_rawVelocityPlanarMoveLean - m_velocityPlanarMoveLeanChange).normalized, m_rawVelocityPlanarMoveLean.normalized) < 0f)
			{
				m_velocityPlanarMoveLeanChange = Vector3.Lerp(m_velocityPlanarMoveLeanChange, m_rawVelocityPlanarMoveLean, Time.deltaTime * 100f / Mathf.Max(m_moveLeaningIsStartStopStopDurationStrength, 1f));
			}
			else
			{
				m_velocityPlanarMoveLeanChange = Vector3.Lerp(m_velocityPlanarMoveLeanChange, m_rawVelocityPlanarMoveLean, Time.deltaTime * 100f / Mathf.Max(m_moveLeaningIsStartStopStartDurationStrength, 1f));
			}
			Vector3 b = m_rawVelocityPlanarMoveLean - m_velocityPlanarMoveLeanChange;
			m_moveLeanChangeVec = Vector3.Lerp(m_moveLeanChangeVec, b, Time.deltaTime * 10f);
			Vector3 vector2 = Vector3.Lerp(m_velocityPlanarMoveLean, m_moveLeanChangeVec, m_moveLeaningIsStartStopPercentage);
			Vector3 lhs = Vector3.Project(vector2, m_transform.right);
			Vector3 lhs2 = Vector3.Project(vector2, m_transform.forward);
			float num = 1f;
			float num2 = 1f;
			if (Vector3.Dot(lhs, m_transform.right) > 0f)
			{
				num = -1f;
			}
			if (Vector3.Dot(lhs2, m_transform.forward) < 0f)
			{
				num2 = -1f;
			}
			if (m_flipForwardMoveLean)
			{
				num2 *= -1f;
			}
			if (m_flipRightMoveLean)
			{
				num *= -1f;
			}
			if (m_moveSpineLeaningStrength >= 0.001f)
			{
				Quaternion quaternion = Quaternion.AngleAxis(lhs2.magnitude * num2 * m_moveSpineLeaningStrength, m_transform.right);
				Quaternion quaternion2 = Quaternion.AngleAxis(lhs.magnitude * num * m_moveSpineLeaningStrength, m_transform.forward) * quaternion;
				m_spineTransform.rotation = quaternion2 * m_spineTransform.rotation;
				m_animator.SetBoneLocalRotation(HumanBodyBones.Spine, m_spineTransform.localRotation);
			}
			if (!(m_moveLeaningStrength < 0.001f))
			{
				Quaternion quaternion3 = Quaternion.AngleAxis(lhs2.magnitude * num2 * m_moveLeaningStrength, m_transform.right);
				Quaternion quaternion4 = Quaternion.AngleAxis(lhs.magnitude * num * m_moveLeaningStrength, m_transform.forward) * quaternion3;
				Vector3 vector3 = quaternion4 * vector;
				Vector3 vector4 = Mathf.Min(vector2.magnitude * m_moveLeaningKneelFactor * 0.25f, 1f) * vector3;
				m_animator.bodyPosition = feetZeroPos + vector3 - vector4;
				m_animator.bodyRotation = quaternion4 * m_animator.bodyRotation;
				if ((bool)m_rightHandTransform)
				{
					Vector3 vector5 = m_rightHandStartPos - feetZeroPos;
					Vector3 vector6 = quaternion4 * vector5;
					m_rightHandOffsetVec += vector6 - vector5 - vector4;
				}
				if ((bool)m_leftHandTransform)
				{
					Vector3 vector7 = m_leftHandStartPos - feetZeroPos;
					Vector3 vector8 = quaternion4 * vector7;
					m_leftHandOffsetVec += vector8 - vector7 - vector4;
				}
				if ((bool)m_rightShoulderTransform)
				{
					Vector3 vector9 = m_rightShoulderStartPos - feetZeroPos;
					Vector3 vector10 = quaternion4 * vector9;
					m_rightShoulderOffsetVec += vector10 - vector9 - vector4;
				}
				if ((bool)m_leftShoulderTransform)
				{
					Vector3 vector11 = m_leftShoulderStartPos - feetZeroPos;
					Vector3 vector12 = quaternion4 * vector11;
					m_leftShoulderOffsetVec += vector12 - vector11 - vector4;
				}
				if (m_moveLeaningKneelFactor < 0.001f)
				{
					Vector3 vector13 = m_animator.GetIKHintPosition(AvatarIKHint.LeftKnee) - feetZeroPos;
					Vector3 vector14 = m_animator.GetIKHintPosition(AvatarIKHint.RightKnee) - feetZeroPos;
					m_animator.SetIKHintPosition(AvatarIKHint.LeftKnee, feetZeroPos + quaternion4 * vector13);
					m_animator.SetIKHintPosition(AvatarIKHint.RightKnee, feetZeroPos + quaternion4 * vector14);
					return;
				}
				Vector3 iKPosition = m_animator.GetIKPosition(AvatarIKGoal.LeftFoot);
				Vector3 iKPosition2 = m_animator.GetIKPosition(AvatarIKGoal.RightFoot);
				Vector3 iKHintPosition = m_animator.GetIKHintPosition(AvatarIKHint.LeftKnee);
				Vector3 iKHintPosition2 = m_animator.GetIKHintPosition(AvatarIKHint.RightKnee);
				Vector3 position = m_leftUpperLeg.position;
				Vector3 position2 = m_rightUpperLeg.position;
				Vector3 vector15 = position - iKPosition;
				Vector3 vector16 = position2 - iKPosition2;
				Vector3 vector17 = iKHintPosition - iKPosition;
				Vector3 vector18 = iKHintPosition2 - iKPosition2;
				float magnitude = vector17.magnitude;
				float magnitude2 = (iKHintPosition - position).magnitude;
				float magnitude3 = vector18.magnitude;
				float magnitude4 = (iKHintPosition2 - position2).magnitude;
				position = iKPosition + quaternion4 * vector15;
				position2 = iKPosition2 + quaternion4 * vector16;
				Vector3 vector19 = iKPosition + quaternion4 * vector17;
				iKHintPosition2 = iKPosition2 + quaternion4 * vector18;
				vector15 = position - iKPosition;
				vector16 = position2 - iKPosition2;
				vector17 = vector19 - iKPosition;
				vector18 = iKHintPosition2 - iKPosition2;
				Vector3 hintDirectionVec = vector17 - Vector3.Project(vector17, vector15.normalized);
				Vector3 hintDirectionVec2 = vector18 - Vector3.Project(vector18, vector16.normalized);
				Vector3 hintPosition = calculateHintPos(position - vector4, iKPosition, magnitude, magnitude2, vector15, hintDirectionVec);
				Vector3 hintPosition2 = calculateHintPos(position2 - vector4, iKPosition2, magnitude3, magnitude4, vector16, hintDirectionVec2);
				m_animator.SetIKHintPosition(AvatarIKHint.LeftKnee, hintPosition);
				m_animator.SetIKHintPosition(AvatarIKHint.RightKnee, hintPosition2);
			}
		}

		protected virtual bool wasGroundMovingForGrounded()
		{
			if (m_groundedResult.groundedTransform != m_groundedResultLastTransform)
			{
				return true;
			}
			if (m_groundedResult.groundedTransform == null)
			{
				return false;
			}
			if ((m_groundedResult.groundedTransform.position - m_groundedResultLastTransformPosition).magnitude > Mathf.Epsilon)
			{
				return true;
			}
			if (Quaternion.Angle(m_groundedResultLastTransformRotation, m_groundedResult.groundedTransform.rotation) > Mathf.Epsilon)
			{
				return true;
			}
			return false;
		}

		protected virtual bool wasGroundMovingForIkLeft()
		{
			if (m_resultLeft.primaryHitTransform == null)
			{
				return false;
			}
			if ((m_resultLeft.primaryHitTransform.position - m_resultLeftLastTransformPosition).magnitude > Mathf.Epsilon)
			{
				return true;
			}
			if (Quaternion.Angle(m_resultLeftLastTransformRotation, m_resultLeft.primaryHitTransform.rotation) > Mathf.Epsilon)
			{
				return true;
			}
			return false;
		}

		protected virtual bool wasGroundMovingForIkRight()
		{
			if (m_resultRight.primaryHitTransform == null)
			{
				return false;
			}
			if ((m_resultRight.primaryHitTransform.position - m_resultRightLastTransformPosition).magnitude > Mathf.Epsilon)
			{
				return true;
			}
			if (Quaternion.Angle(m_resultRightLastTransformRotation, m_resultRight.primaryHitTransform.rotation) > Mathf.Epsilon)
			{
				return true;
			}
			return false;
		}

		protected void findLegsForwardAxes(out Axis leftLegAxis, out bool isNegativeLeftLegAxis, out Axis rightLegAxis, out bool isNegativeRightLegAxis)
		{
			Transform boneTransform = m_animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
			Transform boneTransform2 = m_animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
			Vector3 rhs = m_animator.GetBoneTransform(HumanBodyBones.Head).position - m_animator.GetBoneTransform(HumanBodyBones.Hips).position;
			Vector3 lhs = boneTransform.position - boneTransform2.position;
			rhs.Normalize();
			lhs.Normalize();
			Vector3 normalized = Vector3.Cross(lhs, rhs).normalized;
			float num = Vector3.Dot(normalized, boneTransform2.right);
			float num2 = Vector3.Dot(normalized, boneTransform2.up);
			float num3 = Vector3.Dot(normalized, boneTransform2.forward);
			leftLegAxis = Axis.Z_AXIS;
			isNegativeLeftLegAxis = false;
			if (Mathf.Abs(num) > Mathf.Abs(num2) && Mathf.Abs(num) > Mathf.Abs(num3))
			{
				leftLegAxis = Axis.X_AXIS;
				if (num < 0f)
				{
					isNegativeLeftLegAxis = true;
				}
			}
			else if (Mathf.Abs(num2) > Mathf.Abs(num) && Mathf.Abs(num2) > Mathf.Abs(num3))
			{
				leftLegAxis = Axis.Y_AXIS;
				if (num2 < 0f)
				{
					isNegativeLeftLegAxis = true;
				}
			}
			else if (num3 < 0f)
			{
				isNegativeLeftLegAxis = true;
			}
			num = Vector3.Dot(normalized, boneTransform.right);
			num2 = Vector3.Dot(normalized, boneTransform.up);
			num3 = Vector3.Dot(normalized, boneTransform.forward);
			rightLegAxis = Axis.Z_AXIS;
			isNegativeRightLegAxis = false;
			if (Mathf.Abs(num) > Mathf.Abs(num2) && Mathf.Abs(num) > Mathf.Abs(num3))
			{
				rightLegAxis = Axis.X_AXIS;
				if (num < 0f)
				{
					isNegativeRightLegAxis = true;
				}
			}
			else if (Mathf.Abs(num2) > Mathf.Abs(num) && Mathf.Abs(num2) > Mathf.Abs(num3))
			{
				rightLegAxis = Axis.Y_AXIS;
				if (num2 < 0f)
				{
					isNegativeRightLegAxis = true;
				}
			}
			else if (num3 < 0f)
			{
				isNegativeRightLegAxis = true;
			}
		}

		protected virtual void updateIK(Vector3 feetZeroPos)
		{
			m_feetResetLerp = Mathf.Lerp(m_feetResetLerp, 1f, Time.deltaTime * m_ikPlacementStiffness * m_resetToDefaultValuesSpeed);
			m_bodyResetLerp = Mathf.Lerp(m_bodyResetLerp, 1f, Time.deltaTime * m_bodyStiffness * m_resetToDefaultValuesSpeed);
			Vector3 bodyPosition = m_animator.bodyPosition;
			Vector3 iKPosition = m_animator.GetIKPosition(AvatarIKGoal.LeftFoot);
			Vector3 iKPosition2 = m_animator.GetIKPosition(AvatarIKGoal.RightFoot);
			Vector3 vector = m_leftLowerLeg.position;
			Vector3 vector2 = m_rightLowerLeg.position;
			if (m_computeIkHintPositionStrategy == IkHintComputationStrategy.IK_HINT_POSITION_BASED)
			{
				vector = m_animator.GetIKHintPosition(AvatarIKHint.LeftKnee);
				vector2 = m_animator.GetIKHintPosition(AvatarIKHint.RightKnee);
			}
			Vector3 position = m_leftUpperLeg.position;
			Vector3 position2 = m_rightUpperLeg.position;
			Vector3 ikPosToBodyPosOriginalVec = position - iKPosition;
			Vector3 ikPosToBodyPosOriginalVec2 = position2 - iKPosition2;
			Vector3 vector3 = vector - iKPosition;
			Vector3 vector4 = vector2 - iKPosition2;
			Vector3 hintDirectionVec = vector3 - Vector3.Project(vector3, ikPosToBodyPosOriginalVec.normalized);
			Vector3 hintDirectionVec2 = vector4 - Vector3.Project(vector4, ikPosToBodyPosOriginalVec2.normalized);
			float magnitude = vector3.magnitude;
			float magnitude2 = (vector - position).magnitude;
			float magnitude3 = vector4.magnitude;
			float magnitude4 = (vector2 - position2).magnitude;
			if (m_computeIkHintPositionStrategy == IkHintComputationStrategy.BONE_BASED)
			{
				hintDirectionVec = ((m_leftAxisHint == Axis.X_AXIS) ? (m_leftLowerLeg.right * magnitude + m_leftUpperLeg.right * magnitude2) : ((m_leftAxisHint != Axis.Y_AXIS) ? (m_leftLowerLeg.forward * magnitude + m_leftUpperLeg.forward * magnitude2) : (m_leftLowerLeg.up * magnitude + m_leftUpperLeg.up * magnitude2)));
				if (m_isLeftAxisHintNegative)
				{
					hintDirectionVec *= -1f;
				}
				hintDirectionVec2 = ((m_rightAxisHint == Axis.X_AXIS) ? (m_rightLowerLeg.right * magnitude3 + m_rightUpperLeg.right * magnitude4) : ((m_rightAxisHint != Axis.Y_AXIS) ? (m_rightLowerLeg.forward * magnitude3 + m_rightUpperLeg.forward * magnitude4) : (m_rightLowerLeg.up * magnitude3 + m_rightUpperLeg.up * magnitude4)));
				if (m_isRightAxisHintNegative)
				{
					hintDirectionVec2 *= -1f;
				}
			}
			else if (m_computeIkHintPositionStrategy == IkHintComputationStrategy.BONE_BASED_WITH_FEET_DIRECTION_INFLUENCE)
			{
				hintDirectionVec = ((m_leftAxisHint == Axis.X_AXIS) ? (m_leftLowerLeg.right * magnitude + m_leftUpperLeg.right * magnitude2 + m_leftFoot.right * magnitude) : ((m_leftAxisHint != Axis.Y_AXIS) ? (m_leftLowerLeg.forward * magnitude + m_leftUpperLeg.forward * magnitude2 + m_leftFoot.forward * magnitude) : (m_leftLowerLeg.up * magnitude + m_leftUpperLeg.up * magnitude2 + m_leftFoot.up * magnitude)));
				if (m_isLeftAxisHintNegative)
				{
					hintDirectionVec *= -1f;
				}
				hintDirectionVec2 = ((m_rightAxisHint == Axis.X_AXIS) ? (m_rightLowerLeg.right * magnitude3 + m_rightUpperLeg.right * magnitude4 + m_rightFoot.right * magnitude3) : ((m_rightAxisHint != Axis.Y_AXIS) ? (m_rightLowerLeg.forward * magnitude3 + m_rightUpperLeg.forward * magnitude4 + m_rightFoot.forward * magnitude3) : (m_rightLowerLeg.up * magnitude3 + m_rightUpperLeg.up * magnitude4 + m_rightFoot.up * magnitude3)));
				if (m_isRightAxisHintNegative)
				{
					hintDirectionVec2 *= -1f;
				}
			}
			Vector3 vector5 = m_globalCharacterYOffset * m_transform.up;
			if (m_globalCharacterYOffset > 0f || m_globalCharacterYOffset < 0f)
			{
				bodyPosition += vector5;
				iKPosition += vector5;
				iKPosition2 += vector5;
				position += vector5;
				position2 += vector5;
				if (m_applyGlobalCharacterYOffsetToOtherIKsToo)
				{
					m_animator.SetIKPosition(AvatarIKGoal.LeftHand, m_animator.GetIKPosition(AvatarIKGoal.LeftHand) + vector5);
					m_animator.SetIKPosition(AvatarIKGoal.RightHand, m_animator.GetIKPosition(AvatarIKGoal.RightHand) + vector5);
					m_animator.SetIKHintPosition(AvatarIKHint.LeftElbow, m_animator.GetIKHintPosition(AvatarIKHint.LeftElbow) + vector5);
					m_animator.SetIKHintPosition(AvatarIKHint.RightElbow, m_animator.GetIKHintPosition(AvatarIKHint.RightElbow) + vector5);
				}
			}
			Vector3 vector6 = iKPosition - (m_prevIkLeft + m_characterExternalMovingPlatformOffsetVec);
			Vector3 vector7 = iKPosition2 - (m_prevIkRight + m_characterExternalMovingPlatformOffsetVec);
			float magnitude5 = vector6.magnitude;
			float magnitude6 = vector7.magnitude;
			Quaternion iKRotation = m_animator.GetIKRotation(AvatarIKGoal.LeftFoot);
			Quaternion iKRotation2 = m_animator.GetIKRotation(AvatarIKGoal.RightFoot);
			Vector3 vector8 = m_transform.position - (m_prevTransformPosition + m_characterExternalMovingPlatformOffsetVec);
			Vector3 vector9 = Vector3.Project(vector8, m_transform.up);
			Vector3 vector10 = iKRotation * Vector3.forward;
			Vector3 vector11 = iKRotation2 * Vector3.forward;
			float num = Vector3.Dot(vector9.normalized, m_transform.up);
			bool flag = num >= 0f;
			if (!flag)
			{
				float num2 = (m_ikFootForwardBias + m_ikFootLength * 0.5f) * m_automaticFootRotationBasedHeightAdaption;
				Vector3 vector12 = Vector3.Project(vector10 * num2, m_transform.up);
				Vector3 vector13 = Vector3.Project(vector11 * num2, m_transform.up);
				float num3 = 1f;
				float num4 = 1f;
				if (Vector3.Dot(vector12.normalized, m_transform.up) >= 0f)
				{
					num3 = 0f;
				}
				if (Vector3.Dot(vector13.normalized, m_transform.up) >= 0f)
				{
					num4 = 0f;
				}
				m_ikFootHeightToUseLeft = m_ikFootHeight + num3 * vector12.magnitude;
				m_ikFootHeightToUseRight = m_ikFootHeight + num4 * vector13.magnitude;
				Vector3 vector14 = Vector3.Project(vector10 * m_ikFootLength, m_transform.forward);
				Vector3 vector15 = Vector3.Project(vector11 * m_ikFootLength, m_transform.forward);
				m_ikFootLengthToUseLeft = Mathf.Max(vector14.magnitude, m_ikFootHeight);
				m_ikFootLengthToUseRight = Mathf.Max(vector15.magnitude, m_ikFootHeight);
			}
			else
			{
				m_ikFootHeightToUseLeft = m_ikFootHeight;
				m_ikFootHeightToUseRight = m_ikFootHeight;
				m_ikFootLengthToUseLeft = m_ikFootLength;
				m_ikFootLengthToUseRight = m_ikFootLength;
			}
			Vector3 vector16 = Vector3.Project(iKPosition - feetZeroPos, -m_transform.up);
			Vector3 vector17 = iKPosition - vector16;
			Vector3 vector18 = Vector3.Project(iKPosition2 - feetZeroPos, -m_transform.up);
			Vector3 vector19 = iKPosition2 - vector18;
			float num5 = (Vector3.Project(vector19, m_transform.forward) - Vector3.Project(vector17, m_transform.forward)).magnitude * m_increaseBodyPositionMaxCorrectionWithForwardFootDistance * 0.5f;
			float num6 = m_bodyPositionMaxCorrection + num5;
			float num7 = Mathf.Max(num6, m_checkGroundedDistance + num5);
			float magnitude7 = m_rawbodyOffsetVec.magnitude;
			if (magnitude7 < 0.01f)
			{
				float num8 = m_bodyPositionMaxCorrectionTolerance * m_bodyPositionMaxCorrection;
				num6 -= num8;
				num7 -= num8;
			}
			bool flag2 = false;
			bool flag3 = false;
			int num9;
			int num10;
			if (m_accountMinimumMovementThresholdForMovingGroundObjects)
			{
				float magnitude8 = (m_rawbodyOffsetVec - m_bodyOffset).magnitude;
				if (!wasGroundMovingForGrounded())
				{
					if (magnitude8 > 0.001f)
					{
						num9 = ((magnitude7 > 0.001f) ? 1 : 0);
						if (num9 != 0)
						{
							goto IL_0933;
						}
					}
					else
					{
						num9 = 0;
					}
					num10 = (wasGroundMovingForIkLeft() ? 1 : 0);
					goto IL_0934;
				}
				num9 = 1;
				goto IL_0933;
			}
			goto IL_0943;
			IL_0943:
			if (Mathf.Abs(m_ikFootHeight - m_prevIkFootHeight) > Mathf.Epsilon || Mathf.Abs(m_ikMaxCorrection - m_prevIkMaxCorrection) > Mathf.Epsilon || Mathf.Abs(m_ikFootForwardBias - m_prevIkFootForwardBias) > Mathf.Epsilon || Mathf.Abs(m_ikFootLength - m_prevIkFootLength) > Mathf.Epsilon || Mathf.Abs(m_ikFootWidth - m_prevIkFootWidth) > Mathf.Epsilon || Mathf.Abs(m_bodyPositionMaxCorrection - m_prevBodyPositionMaxCorrection) > Mathf.Epsilon || Mathf.Abs(m_checkGroundedDistance - m_prevCheckGroundedDistance) > Mathf.Epsilon || m_collisionLayerMask.value != m_prevCollisionLayerMask.value)
			{
				flag2 = true;
				flag3 = true;
				m_prevIkFootHeight = m_ikFootHeight;
				m_prevIkMaxCorrection = m_ikMaxCorrection;
				m_prevIkFootForwardBias = m_ikFootForwardBias;
				m_prevIkFootLength = m_ikFootLength;
				m_prevIkFootWidth = m_ikFootWidth;
				m_prevBodyPositionMaxCorrection = m_bodyPositionMaxCorrection;
				m_prevCheckGroundedDistance = m_checkGroundedDistance;
				m_prevCollisionLayerMask.value = m_collisionLayerMask.value;
			}
			if ((magnitude5 > m_minimumMovementThreshold) | flag2)
			{
				m_prevIkLeft = iKPosition;
				findNewIKPos(m_resultLeft, iKPosition, vector17, m_ikFootHeightToUseLeft, m_ikFootForwardBias, m_transform.forward, m_transform.rotation, feetZeroPos, -m_transform.up, m_ikMaxCorrection, m_ikCorrectionRaycastMargin, num6, num7, m_ikFootLengthToUseLeft, m_ikFootWidth, m_collisionLayerMask);
				if (m_resultLeft.primaryHitTransform != null)
				{
					m_resultLeftLastTransformPosition = m_resultLeft.primaryHitTransform.position;
					m_resultLeftLastTransformRotation = m_resultLeft.primaryHitTransform.rotation;
				}
			}
			if ((magnitude6 > m_minimumMovementThreshold) | flag3)
			{
				m_prevIkRight = iKPosition2;
				findNewIKPos(m_resultRight, iKPosition2, vector19, m_ikFootHeightToUseRight, m_ikFootForwardBias, m_transform.forward, m_transform.rotation, feetZeroPos, -m_transform.up, m_ikMaxCorrection, m_ikCorrectionRaycastMargin, num6, num7, m_ikFootLengthToUseRight, m_ikFootWidth, m_collisionLayerMask);
				if (m_resultRight.primaryHitTransform != null)
				{
					m_resultRightLastTransformPosition = m_resultRight.primaryHitTransform.position;
					m_resultRightLastTransformRotation = m_resultRight.primaryHitTransform.rotation;
				}
			}
			if (m_resultLeft.primaryHitTransform == null && m_resultRight.primaryHitTransform != null)
			{
				Vector3 vector20 = Vector3.Project(m_resultRight.surfacePoint - feetZeroPos, m_transform.up);
				if (Vector3.Dot(vector20.normalized, m_transform.up) < 0f)
				{
					m_resultLeft.init(iKPosition, iKPosition + vector20, _isGlued: false, vector17 + vector20, vector17 + vector20, m_transform.up, null, null);
				}
			}
			else if (m_resultLeft.primaryHitTransform != null && m_resultRight.primaryHitTransform == null)
			{
				Vector3 vector21 = Vector3.Project(m_resultLeft.surfacePoint - feetZeroPos, m_transform.up);
				if (Vector3.Dot(vector21.normalized, m_transform.up) < 0f)
				{
					m_resultRight.init(iKPosition2, iKPosition2 + vector21, _isGlued: false, vector19 + vector21, vector19 + vector21, m_transform.up, null, null);
				}
			}
			Vector3 vector22 = Vector3.Project(m_resultRight.ikPos - m_resultLeft.ikPos, m_transform.up);
			Vector3 vector23 = Vector3.Project(m_resultLeft.surfacePoint - feetZeroPos, m_transform.up);
			Vector3 vector24 = Vector3.Project(m_resultRight.surfacePoint - feetZeroPos, m_transform.up);
			if (vector22.magnitude > num6)
			{
				if (Vector3.Dot(vector24.normalized, m_transform.up) < 0f)
				{
					m_resultRight.init(iKPosition2, iKPosition2, _isGlued: false, vector19, vector19, m_transform.up, null, null);
					vector24 = Vector3.zero;
				}
				else
				{
					m_resultLeft.init(iKPosition, iKPosition, _isGlued: false, vector17, vector17, m_transform.up, null, null);
					vector23 = Vector3.zero;
				}
			}
			m_rawbodyOffsetVec = Vector3.zero;
			Vector3 vector25 = Vector3.zero;
			Vector3 vector26 = Vector3.zero;
			if (Vector3.Dot(vector23.normalized, m_transform.up) < 0f)
			{
				vector25 = vector23;
				if (Vector3.Dot(vector24.normalized, m_transform.up) < 0f)
				{
					vector26 = vector24;
					if (vector24.magnitude > vector23.magnitude)
					{
						m_rawbodyOffsetVec = vector24;
					}
					else
					{
						m_rawbodyOffsetVec = vector23;
					}
				}
				else
				{
					m_rawbodyOffsetVec = vector23;
				}
			}
			else if (Vector3.Dot(vector24.normalized, m_transform.up) < 0f)
			{
				vector26 = vector24;
				m_rawbodyOffsetVec = vector24;
			}
			else if (m_canBodyPositionMoveHigherThanGroundedPosition)
			{
				if (vector24.magnitude < vector23.magnitude)
				{
					m_rawbodyOffsetVec = vector24;
				}
				else
				{
					m_rawbodyOffsetVec = vector23;
				}
				Vector3 vector27 = iKPosition + m_rawbodyOffsetVec;
				if (Vector3.Dot((m_resultLeft.gluedIKPos - vector27).normalized, -m_transform.up) > 0f)
				{
					m_resultLeft.ikPos = vector27;
					m_resultLeft.isGlued = false;
				}
				vector27 = iKPosition2 + m_rawbodyOffsetVec;
				if (Vector3.Dot((m_resultRight.gluedIKPos - vector27).normalized, -m_transform.up) > 0f)
				{
					m_resultRight.ikPos = vector27;
					m_resultRight.isGlued = false;
				}
			}
			if (m_bodyPositionInterpolationMethod == InterpolationMethod.LERP)
			{
				m_bodyOffset = Vector3.Lerp(m_bodyOffset, m_rawbodyOffsetVec, Time.deltaTime * m_bodyStiffness);
			}
			else
			{
				m_bodyOffset = Vector3.SmoothDamp(m_bodyOffset, m_rawbodyOffsetVec, ref m_ikBodyPositionSmoothDampVelocity, 1f / m_bodyStiffness);
			}
			m_animator.bodyPosition = bodyPosition + (m_bodyOffset + m_bodyPositionOffset * m_transform.up) * m_bodyPlacementWeight * m_bodyResetLerp;
			m_currBodyPositionOffset = m_bodyOffset.magnitude;
			m_maxBodyPositionOffset = Mathf.Max(m_maxBodyPositionOffset, m_currBodyPositionOffset);
			Vector3 vector28 = m_resultLeft.ikPos + vector25 * m_bodyPlacementWeight;
			Vector3 vector29 = m_resultRight.ikPos + vector26 * m_bodyPlacementWeight;
			Vector3 vector30 = vector28 - m_resultLeft.gluedIKPos;
			Vector3 vector31 = vector29 - m_resultRight.gluedIKPos;
			if (Vector3.Dot(vector30.normalized, -m_transform.up) > 0f)
			{
				vector28 = m_resultLeft.gluedIKPos;
				m_resultLeft.isGlued = true;
				m_resultLeft.ikPos = vector28;
			}
			if (Vector3.Dot(vector31.normalized, -m_transform.up) > 0f)
			{
				vector29 = m_resultRight.gluedIKPos;
				m_resultRight.isGlued = true;
				m_resultRight.ikPos = vector29;
			}
			debugDrawPoint(vector28, Color.magenta);
			debugDrawPoint(vector29, Color.magenta);
			Vector3 vector32 = vector28 - iKPosition;
			Vector3 vector33 = vector29 - iKPosition2;
			Vector3 vector34 = vector32 - m_ikLeftOffset;
			Vector3 vector35 = vector33 - m_ikRightOffset;
			if (m_resultLeft.primaryHitTransform != null && vector34.magnitude > 0.01f)
			{
				if (Vector3.Dot(vector34.normalized, m_transform.up) > 0f)
				{
					if (flag)
					{
						m_extrapolationLeft = vector34 * m_ikUpwardsExtrapolation;
					}
					else
					{
						m_extrapolationLeft = vector34 * m_ikDownwardsExtrapolation;
					}
				}
				else if (num > 0.01f)
				{
					m_extrapolationLeft = m_bodyOffset.magnitude * vector34 * m_ikUpwardsPullBackDownExtrapolation;
				}
				else
				{
					m_extrapolationLeft = Vector3.zero;
				}
			}
			else
			{
				m_extrapolationLeft = Vector3.zero;
			}
			if (m_resultRight.primaryHitTransform != null && vector35.magnitude > 0.01f)
			{
				if (Vector3.Dot(vector35.normalized, m_transform.up) > 0f)
				{
					if (flag)
					{
						m_extrapolationRight = vector35 * m_ikUpwardsExtrapolation;
					}
					else
					{
						m_extrapolationRight = vector35 * m_ikDownwardsExtrapolation;
					}
				}
				else if (num > 0.01f)
				{
					m_extrapolationRight = m_bodyOffset.magnitude * vector35 * m_ikUpwardsPullBackDownExtrapolation;
				}
				else
				{
					m_extrapolationRight = Vector3.zero;
				}
			}
			else
			{
				m_extrapolationRight = Vector3.zero;
			}
			Vector3 vector36 = vector32 + m_extrapolationLeft;
			Vector3 vector37 = vector33 + m_extrapolationRight;
			if (m_ikInterpolationMethod == InterpolationMethod.LERP)
			{
				float t = Time.deltaTime * m_ikPlacementStiffness;
				m_ikLeftOffset = Vector3.Lerp(m_ikLeftOffset, vector36, t);
				m_ikRightOffset = Vector3.Lerp(m_ikRightOffset, vector37, t);
			}
			else
			{
				m_ikLeftOffset = Vector3.SmoothDamp(m_ikLeftOffset, vector36, ref m_ikLeftOffsetSmoothDampVelocity, 1f / m_ikPlacementStiffness);
				m_ikRightOffset = Vector3.SmoothDamp(m_ikRightOffset, vector37, ref m_ikRightOffsetSmoothDampVelocity, 1f / m_ikPlacementStiffness);
			}
			if (m_antiDownwardsIntersectionStiffness > 0.01f)
			{
				Vector3 vector38 = m_animator.bodyPosition - (m_prevBodyPosition + m_characterExternalMovingPlatformOffsetVec);
				if (vector38.magnitude > 0f && Vector3.Dot(Vector3.Project(vector38, -m_transform.up).normalized, -m_transform.up) > 0f)
				{
					float t2 = Mathf.Min(Time.deltaTime * m_antiDownwardsIntersectionStiffness, 1f);
					if (vector32.magnitude > 0f && m_ikLeftOffset.magnitude > vector32.magnitude && Vector3.Dot(vector32.normalized, -m_transform.up) > 0f && Vector3.Dot(m_ikLeftOffset.normalized, -m_transform.up) > 0f)
					{
						m_ikLeftOffset = Vector3.Lerp(m_ikLeftOffset, vector32, t2);
						m_extrapolationLeft = Vector3.Lerp(m_extrapolationLeft, Vector3.zero, t2);
					}
					if (vector33.magnitude > 0f && m_ikRightOffset.magnitude > vector33.magnitude && Vector3.Dot(vector33.normalized, -m_transform.up) > 0f && Vector3.Dot(m_ikRightOffset.normalized, -m_transform.up) > 0f)
					{
						m_ikRightOffset = Vector3.Lerp(m_ikRightOffset, vector33, t2);
						m_extrapolationRight = Vector3.Lerp(m_extrapolationRight, Vector3.zero, t2);
					}
				}
			}
			m_prevBodyPosition = m_animator.bodyPosition;
			Vector3 vector39 = iKPosition + m_ikLeftOffset;
			Vector3 vector40 = iKPosition2 + m_ikRightOffset;
			m_animator.SetIKPosition(AvatarIKGoal.LeftFoot, vector39);
			m_animator.SetIKPosition(AvatarIKGoal.RightFoot, vector40);
			m_animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, m_ikPlacementWeight * m_feetResetLerp);
			m_animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, m_ikPlacementWeight * m_feetResetLerp);
			m_currIkCorrectionOffset = m_ikLeftOffset.magnitude;
			m_maxIkCorrectionOffset = Mathf.Max(m_maxIkCorrectionOffset, m_currIkCorrectionOffset);
			if (isCrouching())
			{
				float num11 = 0f;
				if (Vector3.Dot(m_ikLeftOffset, m_transform.up) > 0f)
				{
					num11 = Mathf.Max(num11, m_ikLeftOffset.magnitude);
				}
				if (Vector2.Dot(m_ikRightOffset, m_transform.up) > 0f)
				{
					num11 = Mathf.Max(num11, m_ikRightOffset.magnitude);
				}
				m_crouchOffset = Vector3.Lerp(m_crouchOffset, (num11 * m_transform.up - m_bodyOffset) * (1f - m_crouchCorrectionTolerance) * m_bodyPlacementWeight, Time.deltaTime * m_bodyStiffness);
			}
			else
			{
				m_crouchOffset = Vector3.Lerp(m_crouchOffset, Vector3.zero, Time.deltaTime * m_bodyStiffness);
			}
			m_animator.bodyPosition += m_crouchOffset;
			position += m_animator.bodyPosition - bodyPosition;
			position2 += m_animator.bodyPosition - bodyPosition;
			Vector3 hintPosition = calculateHintPos(position, vector39, magnitude, magnitude2, ikPosToBodyPosOriginalVec, hintDirectionVec);
			Vector3 hintPosition2 = calculateHintPos(position2, vector40, magnitude3, magnitude4, ikPosToBodyPosOriginalVec2, hintDirectionVec2);
			m_animator.SetIKHintPosition(AvatarIKHint.LeftKnee, hintPosition);
			m_animator.SetIKHintPosition(AvatarIKHint.RightKnee, hintPosition2);
			m_animator.SetIKHintPositionWeight(AvatarIKHint.LeftKnee, m_ikPlacementWeight * m_feetResetLerp);
			m_animator.SetIKHintPositionWeight(AvatarIKHint.RightKnee, m_ikPlacementWeight * m_feetResetLerp);
			if (m_ikLeftOffset.magnitude / vector36.magnitude > m_isGluedToGroundNormalizedThreshold || m_ikLeftOffset.magnitude < m_isGluedToGroundDistanceThreshold)
			{
				m_resultLeft.isGlued = true;
			}
			if (m_ikRightOffset.magnitude / vector37.magnitude > m_isGluedToGroundNormalizedThreshold || m_ikRightOffset.magnitude < m_isGluedToGroundDistanceThreshold)
			{
				m_resultRight.isGlued = true;
			}
			float t3 = Time.deltaTime * m_ikRotationStiffness;
			if (m_resultLeft.isGlued || !m_onlyAdaptRotationWhenGluedToGround)
			{
				Vector3 normal = m_resultLeft.normal;
				Vector3 axis = Vector3.Cross(m_transform.up, normal);
				Quaternion b = Quaternion.AngleAxis(Mathf.Clamp(Vector3.SignedAngle(m_transform.up, normal, axis), 0f - m_maxAnkleRotationAdaptionAngle, m_maxAnkleRotationAdaptionAngle), axis);
				m_ikLeftDeltaRotation = Quaternion.Slerp(m_ikLeftDeltaRotation, b, t3);
			}
			else
			{
				m_ikLeftDeltaRotation = Quaternion.Slerp(m_ikLeftDeltaRotation, Quaternion.identity, t3);
			}
			if (m_resultRight.isGlued || !m_onlyAdaptRotationWhenGluedToGround)
			{
				Vector3 normal2 = m_resultRight.normal;
				Vector3 axis2 = Vector3.Cross(m_transform.up, normal2);
				Quaternion b2 = Quaternion.AngleAxis(Mathf.Clamp(Vector3.SignedAngle(m_transform.up, normal2, axis2), 0f - m_maxAnkleRotationAdaptionAngle, m_maxAnkleRotationAdaptionAngle), axis2);
				m_ikRightDeltaRotation = Quaternion.Slerp(m_ikRightDeltaRotation, b2, t3);
			}
			else
			{
				m_ikRightDeltaRotation = Quaternion.Slerp(m_ikRightDeltaRotation, Quaternion.identity, t3);
			}
			Quaternion goalRotation = m_ikLeftDeltaRotation * iKRotation;
			Quaternion goalRotation2 = m_ikRightDeltaRotation * iKRotation2;
			m_animator.SetIKRotation(AvatarIKGoal.LeftFoot, goalRotation);
			m_animator.SetIKRotation(AvatarIKGoal.RightFoot, goalRotation2);
			m_animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, m_ikPlacementWeight * m_feetResetLerp);
			m_animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, m_ikPlacementWeight * m_feetResetLerp);
			updateEvents(vector28, vector29, vector8 - vector9);
			return;
			IL_0934:
			flag2 = (byte)num10 != 0;
			flag3 = num9 != 0 || wasGroundMovingForIkRight();
			goto IL_0943;
			IL_0933:
			num10 = 1;
			goto IL_0934;
		}

		protected Vector3 calculateHintPos(Vector3 bodyPos, Vector3 nextIkPos, float lowerLength, float upperLength, Vector3 ikPosToBodyPosOriginalVec, Vector3 hintDirectionVec)
		{
			Vector3 vector = bodyPos - nextIkPos;
			Vector3 normalized = vector.normalized;
			float num = upperLength;
			float num2 = lowerLength;
			float num3 = vector.magnitude;
			if (num2 > num && num2 > num3)
			{
				num2 = Mathf.Min(num2, (num3 + num) * 0.995f);
			}
			else if (num > num2 && num > num3)
			{
				num = Mathf.Min(num, (num3 + num2) * 0.995f);
			}
			else
			{
				num3 = Mathf.Min(num3, (num + num2) * 0.995f);
			}
			float f = Mathf.Acos((num2 * num2 + num3 * num3 - num * num) / (2f * num2 * num3));
			float num4 = 0.5f;
			float num5 = 0.5f;
			if (!float.IsNaN(f))
			{
				num4 = Mathf.Cos(f) * num2;
				num5 = Mathf.Sin(f) * num2;
			}
			hintDirectionVec = Quaternion.FromToRotation(ikPosToBodyPosOriginalVec.normalized, normalized) * hintDirectionVec;
			Vector3 vector2 = hintDirectionVec - Vector3.Project(hintDirectionVec, normalized);
			return nextIkPos + num4 * normalized + num5 * vector2.normalized;
		}

		protected virtual bool isCrouching()
		{
			if (m_activateCrouchSpecificBehaviour)
			{
				return true;
			}
			foreach (KeyValuePair<int, int> item in m_activateCrouchingOnAnimatorState)
			{
				if (m_animator.layerCount > item.Key)
				{
					AnimatorStateInfo nextAnimatorStateInfo = m_animator.GetNextAnimatorStateInfo(item.Key);
					if (nextAnimatorStateInfo.shortNameHash == item.Value && nextAnimatorStateInfo.normalizedTime > 0f)
					{
						return true;
					}
					if ((nextAnimatorStateInfo.shortNameHash == item.Value || !(nextAnimatorStateInfo.normalizedTime > 0f)) && m_animator.GetCurrentAnimatorStateInfo(item.Key).shortNameHash == item.Value)
					{
						return true;
					}
				}
			}
			return false;
		}

		protected virtual void updateEvents(Vector3 ikLeftTargetPos, Vector3 ikRightTargetPos, Vector3 transformMovementPlanarVec)
		{
			float magnitude = Vector3.Project(m_resultLeft.surfacePoint - ikLeftTargetPos, m_transform.up).magnitude;
			float magnitude2 = Vector3.Project(m_resultRight.surfacePoint - ikRightTargetPos, m_transform.up).magnitude;
			Vector3 vector = m_resultLeft.surfacePoint - (m_lastFootstepPosLeft + m_characterExternalMovingPlatformOffsetVec);
			Vector3 vector2 = m_resultRight.surfacePoint - (m_lastFootstepPosRight + m_characterExternalMovingPlatformOffsetVec);
			Vector3 vector3 = Vector3.Project(vector, m_transform.up);
			Vector3 vector4 = Vector3.Project(vector2, m_transform.up);
			vector -= vector3;
			vector2 -= vector4;
			m_elapsedFootstepLeftDistance += vector.magnitude;
			m_elapsedFootstepRightDistance += vector2.magnitude;
			bool flag = true;
			if (Time.deltaTime > 0f)
			{
				flag = transformMovementPlanarVec.magnitude / Time.deltaTime < 0.01f;
			}
			if ((m_canIgnoreLastEventFiredWasLeft && !m_ignoreLastEventFiredWasLeft) & flag)
			{
				m_ignoreLastEventFiredWasLeft = true;
				m_canIgnoreLastEventFiredWasLeft = false;
				m_elapsedFootstepLeftDistance = 0f;
				m_elapsedFootstepRightDistance = 0f;
			}
			else if (!flag)
			{
				m_canIgnoreLastEventFiredWasLeft = true;
			}
			if ((m_ignoreLastEventFiredWasLeft || !m_lastEventFiredWasLeft) && magnitude < m_ikFootHeight + m_eventCheckResetDistance && m_elapsedFootstepLeftDistance > m_stepLength)
			{
				if (m_footstepEventDelay > 0.001f)
				{
					StartCoroutine(playFootstepLeftEventDelayed(m_footstepEventDelay));
				}
				else
				{
					if (m_onFootstepLeftStart != null)
					{
						m_onFootstepLeftStart.Invoke(m_resultLeft);
					}
					if (m_onFootstepRightStop != null)
					{
						m_onFootstepRightStop.Invoke();
					}
				}
				m_elapsedFootstepLeftDistance = 0f;
				m_elapsedFootstepRightDistance = 0f;
				m_lastEventFiredWasLeft = true;
				m_ignoreLastEventFiredWasLeft = false;
			}
			else if ((m_ignoreLastEventFiredWasLeft || m_lastEventFiredWasLeft) && magnitude2 < m_ikFootHeight + m_eventCheckResetDistance && m_elapsedFootstepRightDistance > m_stepLength)
			{
				if (m_footstepEventDelay > 0.001f)
				{
					StartCoroutine(playFootstepRightEventDelayed(m_footstepEventDelay));
				}
				else
				{
					if (m_onFootstepRightStart != null)
					{
						m_onFootstepRightStart.Invoke(m_resultRight);
					}
					if (m_onFootstepLeftStop != null)
					{
						m_onFootstepLeftStop.Invoke();
					}
				}
				m_elapsedFootstepLeftDistance = 0f;
				m_elapsedFootstepRightDistance = 0f;
				m_lastEventFiredWasLeft = false;
				m_ignoreLastEventFiredWasLeft = false;
			}
			m_lastFootstepPosLeft = m_resultLeft.surfacePoint;
			m_lastFootstepPosRight = m_resultRight.surfacePoint;
		}

		private IEnumerator playFootstepLeftEventDelayed(float delay)
		{
			if (!Mathf.Approximately(delay, m_prevFootstepEventDelay))
			{
				m_prevFootstepEventDelay = delay;
				m_footstepEventWaitForSeconds = new WaitForSeconds(delay);
			}
			yield return m_footstepEventWaitForSeconds;
			if (m_onFootstepLeftStart != null)
			{
				m_onFootstepLeftStart.Invoke(m_resultLeft);
			}
			if (m_onFootstepRightStop != null)
			{
				m_onFootstepRightStop.Invoke();
			}
		}

		private IEnumerator playFootstepRightEventDelayed(float delay)
		{
			if (!Mathf.Approximately(delay, m_prevFootstepEventDelay))
			{
				m_prevFootstepEventDelay = delay;
				m_footstepEventWaitForSeconds = new WaitForSeconds(delay);
			}
			yield return m_footstepEventWaitForSeconds;
			if (m_onFootstepRightStart != null)
			{
				m_onFootstepRightStart.Invoke(m_resultRight);
			}
			if (m_onFootstepLeftStop != null)
			{
				m_onFootstepLeftStop.Invoke();
			}
		}

		protected virtual void revertUpdateIK()
		{
			m_feetResetLerp = Mathf.Lerp(m_feetResetLerp, 0f, Time.deltaTime * m_ikPlacementStiffness * m_resetToDefaultValuesSpeed);
			m_bodyResetLerp = Mathf.Lerp(m_bodyResetLerp, 0f, Time.deltaTime * m_bodyStiffness * m_resetToDefaultValuesSpeed);
			m_animator.bodyPosition += (m_bodyOffset + m_bodyPositionOffset * m_transform.up) * m_bodyPlacementWeight * m_bodyResetLerp;
			m_crouchOffset = Vector3.Lerp(m_crouchOffset, Vector3.zero, Time.deltaTime * m_bodyStiffness);
			m_animator.bodyPosition += m_crouchOffset;
			Vector3 iKPosition = m_animator.GetIKPosition(AvatarIKGoal.LeftFoot);
			Vector3 iKPosition2 = m_animator.GetIKPosition(AvatarIKGoal.RightFoot);
			Vector3 vector = Vector3.Lerp(Vector3.zero, m_ikLeftOffset, m_feetResetLerp);
			Vector3 vector2 = Vector3.Lerp(Vector3.zero, m_ikRightOffset, m_feetResetLerp);
			Vector3 vector3 = Vector3.Lerp(Vector3.zero, m_extrapolationLeft, m_feetResetLerp);
			Vector3 vector4 = Vector3.Lerp(Vector3.zero, m_extrapolationRight, m_feetResetLerp);
			if (m_globalCharacterYOffset > 0f || m_globalCharacterYOffset < 0f)
			{
				Vector3 vector5 = m_globalCharacterYOffset * m_transform.up;
				iKPosition += vector5;
				iKPosition2 += vector5;
				m_animator.bodyPosition += vector5;
				if (m_applyGlobalCharacterYOffsetToOtherIKsToo)
				{
					m_animator.SetIKPosition(AvatarIKGoal.LeftHand, m_animator.GetIKPosition(AvatarIKGoal.LeftHand) + vector5);
					m_animator.SetIKPosition(AvatarIKGoal.RightHand, m_animator.GetIKPosition(AvatarIKGoal.RightHand) + vector5);
					m_animator.SetIKHintPosition(AvatarIKHint.LeftElbow, m_animator.GetIKHintPosition(AvatarIKHint.LeftElbow) + vector5);
					m_animator.SetIKHintPosition(AvatarIKHint.RightElbow, m_animator.GetIKHintPosition(AvatarIKHint.RightElbow) + vector5);
				}
				m_animator.SetIKHintPosition(AvatarIKHint.LeftKnee, m_animator.GetIKHintPosition(AvatarIKHint.LeftKnee) + vector5);
				m_animator.SetIKHintPosition(AvatarIKHint.RightKnee, m_animator.GetIKHintPosition(AvatarIKHint.RightKnee) + vector5);
			}
			Vector3 vector6 = iKPosition + vector + vector3;
			Vector3 vector7 = iKPosition2 + vector2 + vector4;
			m_animator.SetIKPosition(AvatarIKGoal.LeftFoot, vector6);
			m_animator.SetIKPosition(AvatarIKGoal.RightFoot, vector7);
			m_animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, m_feetResetLerp * m_ikPlacementWeight);
			m_animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, m_feetResetLerp * m_ikPlacementWeight);
			m_animator.SetIKHintPositionWeight(AvatarIKHint.LeftKnee, m_ikPlacementWeight * m_feetResetLerp);
			m_animator.SetIKHintPositionWeight(AvatarIKHint.RightKnee, m_ikPlacementWeight * m_feetResetLerp);
			Quaternion iKRotation = m_animator.GetIKRotation(AvatarIKGoal.LeftFoot);
			Quaternion iKRotation2 = m_animator.GetIKRotation(AvatarIKGoal.RightFoot);
			m_ikLeftDeltaRotation = Quaternion.Slerp(Quaternion.identity, m_ikLeftDeltaRotation, m_feetResetLerp);
			m_ikRightDeltaRotation = Quaternion.Slerp(Quaternion.identity, m_ikRightDeltaRotation, m_feetResetLerp);
			m_animator.SetIKRotation(AvatarIKGoal.LeftFoot, m_ikLeftDeltaRotation * iKRotation);
			m_animator.SetIKRotation(AvatarIKGoal.RightFoot, m_ikRightDeltaRotation * iKRotation2);
			m_animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, m_feetResetLerp * m_ikPlacementWeight);
			m_animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, m_feetResetLerp * m_ikPlacementWeight);
			if (!m_isWaitingForPlayingLeftFootEvent && m_onFootstepLeftStop != null)
			{
				m_onFootstepLeftStop.Invoke();
			}
			if (!m_isWaitingForPlayingRightFootEvent && m_onFootstepRightStop != null)
			{
				m_onFootstepRightStop.Invoke();
			}
			m_isWaitingForPlayingLeftFootEvent = true;
			m_isWaitingForPlayingRightFootEvent = true;
			m_lastLeftIKStepEventPos = vector6;
			m_lastRightIKStepEventPos = vector7;
		}

		protected virtual void findNewIKPos(IKResult ikResult, Vector3 ikPos, Vector3 ikBottomPoint, float heightOffset, float forwardBias, Vector3 forwardDir, Quaternion rotation, Vector3 origin, Vector3 rayDirection, float maxIKCorrection, float ikCorrectionRaycastMargin, float maxFootCorrection, float maxCenterOfMassCorrection, float raycastLength, float raycastWidth, int raycastLayer)
		{
			float num = Mathf.Max(heightOffset * 0.01f, 0.01f);
			Vector3 vector = ikBottomPoint - rayDirection * (maxIKCorrection + num + ikCorrectionRaycastMargin);
			Vector3 vector2 = vector + forwardBias * forwardDir;
			debugDrawPoint(vector2, Color.blue);
			debugDrawPoint(ikPos, Color.yellow);
			Ray ray = new Ray(vector2, rayDirection);
			float maxDistance = maxIKCorrection + maxCenterOfMassCorrection + ikCorrectionRaycastMargin;
			if (Physics.BoxCast(ray.origin, new Vector3(raycastWidth * 0.5f, num, raycastLength * 0.5f), ray.direction, out var hitInfo, rotation, maxDistance, raycastLayer, m_triggerCollisionInteraction))
			{
				Vector3 vector3 = vector2 + hitInfo.distance * rayDirection + num * rayDirection;
				Vector3 vector4 = vector3 - forwardDir * forwardBias;
				Vector3 vector5 = vector4 - rayDirection * heightOffset;
				if (float.IsNaN(hitInfo.normal.x) || float.IsNaN(hitInfo.normal.y) || float.IsNaN(hitInfo.normal.z))
				{
					hitInfo.normal = -rayDirection;
				}
				Vector3 vector6 = hitInfo.normal;
				float num2 = Vector3.Angle(-rayDirection, vector6);
				Vector3 vector7 = vector - heightOffset * rayDirection;
				Vector3? vector8 = null;
				if (Physics.SphereCast(vector7, heightOffset, rayDirection, out var hitInfo2, maxDistance, raycastLayer, m_triggerCollisionInteraction))
				{
					vector8 = vector7 + hitInfo2.distance * rayDirection;
					float num3 = Vector3.Angle(-rayDirection, hitInfo2.normal);
					if (Mathf.Abs(num3 - num2) > 1f)
					{
						Vector3 vector9 = hitInfo2.point - hitInfo.point;
						if (vector9.magnitude > 0.001f)
						{
							vector9.Normalize();
							Vector3 normalized = Vector3.Cross(Vector3.Cross(vector9, -rayDirection).normalized, vector9).normalized;
							float num4 = Vector3.Angle(-rayDirection, normalized);
							if (Mathf.Abs(num4) < Mathf.Abs(num2))
							{
								vector6 = normalized;
								num2 = num4;
								if (Mathf.Abs(num3) > Mathf.Abs(num2))
								{
									num2 = num3;
									vector6 = hitInfo2.normal;
								}
							}
						}
					}
				}
				if (Mathf.Abs(num2) > m_maxGroundAngleAtWhichTheGroundIsDetectedAsGround)
				{
					ikResult.init(ikPos, ikPos, _isGlued: false, ikBottomPoint, ikBottomPoint, -rayDirection, null, null);
					return;
				}
				num2 = Mathf.Clamp(num2, 0f - m_maxGroundAngleTheFeetsCanAdaptTo, m_maxGroundAngleTheFeetsCanAdaptTo);
				Vector3 vector10 = vector4 - hitInfo.point;
				float magnitude = vector10.magnitude;
				Vector3 normalized2 = Vector3.Cross(vector10.normalized, vector6).normalized;
				Vector3 normalized3 = Vector3.Cross(vector6, normalized2).normalized;
				float num5 = Mathf.Cos(num2 * (MathF.PI / 180f));
				if (num5 > 0f && num5 < 0.01f)
				{
					num5 = 0.01f;
				}
				else if (num5 < 0f && num5 > -0.01f)
				{
					num5 = -0.01f;
				}
				float num6 = magnitude / num5;
				Vector3 vector11 = Vector3.Project(normalized3 * num6, -rayDirection);
				float num7 = heightOffset / num5;
				vector4 += vector11;
				Vector3 vector12 = vector4 - rayDirection * num7;
				Vector3 vector13 = vector12 + rayDirection * heightOffset;
				Vector3 vector14 = vector12 - vector5;
				if (vector8.HasValue && Vector3.Dot(vector14.normalized, -rayDirection) > 0f)
				{
					Vector3 vector15 = vector8.Value - vector12;
					if (Vector3.Dot(vector15.normalized, rayDirection) > 0f)
					{
						vector12 += vector15;
						vector13 += vector15;
						vector4 += vector15;
					}
				}
				bool isGlued = true;
				Vector3 gluedIkPos = vector12;
				Vector3 vector16 = vector12 - ikPos;
				if (Vector3.Dot(vector16.normalized, rayDirection) > 0f)
				{
					vector12 = ikPos;
					isGlued = false;
					if (vector16.magnitude > maxFootCorrection)
					{
						Vector3 vector17 = -rayDirection * (vector16.magnitude - maxFootCorrection);
						vector13 += vector17;
						vector4 += vector17;
					}
				}
				else
				{
					vector16 = vector12 - ikBottomPoint;
					if (vector16.magnitude > maxIKCorrection + heightOffset)
					{
						vector12 = ikBottomPoint + vector16.normalized * (maxIKCorrection + heightOffset);
						gluedIkPos = vector12;
					}
				}
				debugDrawPoint(vector3, Color.blue);
				debugDrawPoint(vector13, Color.black);
				debugDrawPoint(vector4, Color.red);
				debugDrawPoint(vector12, Color.cyan);
				debugDrawArrow(vector2, vector3, Color.blue);
				ikResult.init(vector12, gluedIkPos, isGlued, vector4, vector13, vector6, hitInfo.transform, hitInfo2.transform);
			}
			else
			{
				ikResult.init(ikPos, ikPos, _isGlued: false, ikBottomPoint, ikBottomPoint, -rayDirection, null, null);
			}
		}

		private void debugDrawPoint(Vector3 point, Color color)
		{
		}

		private void debugDrawArrow(Vector3 start, Vector3 end, Color color)
		{
		}

		protected virtual void isValidAndGrounded(GroundedResult groundedResult, Vector3 feetZeroPos, Vector3 upVec, int collisionLayer)
		{
			if (m_animator == null)
			{
				groundedResult.init(_isValid: false, _isGrounded: false, null, Vector3.zero, Vector3.zero, Vector3.zero);
				return;
			}
			if (!m_animator.enabled)
			{
				groundedResult.init(_isValid: false, _isGrounded: false, null, Vector3.zero, Vector3.zero, Vector3.zero);
				return;
			}
			bool isValid = true;
			if (m_validationType == ValidationType.FORCE_INVALID)
			{
				isValid = false;
			}
			else if (m_validationType == ValidationType.FORCE_VALID)
			{
				groundedResult.init(_isValid: true, _isGrounded: true, m_transform, feetZeroPos, feetZeroPos, upVec);
				m_lastPotentiallyValidGroundedResult.init(_isValid: true, _isGrounded: true, m_transform, feetZeroPos, feetZeroPos, upVec);
				return;
			}
			foreach (KeyValuePair<int, int> item in m_deactiveOnAnimatorState)
			{
				if (m_animator.layerCount > item.Key)
				{
					AnimatorStateInfo nextAnimatorStateInfo = m_animator.GetNextAnimatorStateInfo(item.Key);
					if (nextAnimatorStateInfo.shortNameHash == item.Value && nextAnimatorStateInfo.normalizedTime > 0f)
					{
						isValid = false;
					}
					if ((nextAnimatorStateInfo.shortNameHash == item.Value || !(nextAnimatorStateInfo.normalizedTime > 0f)) && m_animator.GetCurrentAnimatorStateInfo(item.Key).shortNameHash == item.Value)
					{
						isValid = false;
					}
				}
			}
			if (m_autoUngroundUpwardsVelocity > 0f)
			{
				Vector3 vector = Vector3.Project(feetZeroPos - (m_prevTransformPosition + m_characterExternalMovingPlatformOffsetVec), upVec);
				if (Vector3.Dot(vector.normalized, upVec) > 0.975f)
				{
					m_upwardsVelocity = 0f;
					if (Time.deltaTime > 0f)
					{
						m_upwardsVelocity = vector.magnitude / Time.deltaTime;
					}
					m_maxUpwardsVelocity = Mathf.Max(m_upwardsVelocity, m_maxUpwardsVelocity);
					if (m_upwardsVelocity > m_autoUngroundUpwardsVelocity)
					{
						groundedResult.init(isValid, _isGrounded: false, null, Vector3.zero, Vector3.zero, Vector3.zero);
						return;
					}
				}
			}
			float num = m_checkGroundedRadius + m_checkGroundedDistance;
			float maxDistance = num + m_checkGroundedDistance - m_checkGroundedRadius;
			Vector3 vector2 = feetZeroPos + upVec * num;
			if (Physics.SphereCast(vector2, m_checkGroundedRadius, -upVec, out var hitInfo, maxDistance, collisionLayer, m_triggerCollisionInteraction))
			{
				groundedResult.init(isValid, _isGrounded: true, hitInfo.transform, hitInfo.point, vector2 - upVec * (hitInfo.distance + m_checkGroundedRadius), hitInfo.normal);
				m_lastPotentiallyValidGroundedResult.init(isValid, _isGrounded: true, hitInfo.transform, hitInfo.point, groundedResult.groundedPosition, hitInfo.normal);
			}
			else
			{
				groundedResult.init(isValid, _isGrounded: false, null, Vector3.zero, Vector3.zero, Vector3.zero);
			}
		}
	}
}
