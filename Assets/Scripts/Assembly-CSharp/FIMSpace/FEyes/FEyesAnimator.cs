using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FIMSpace.FEyes
{
	[AddComponentMenu("FImpossible Creations/Eyes Animator 2")]
	[HelpURL("https://assetstore.unity.com/packages/3d/animations/eyes-animator-137246")]
	[DefaultExecutionOrder(16)]
	public class FEyesAnimator : MonoBehaviour, IDropHandler, IEventSystemHandler, IFHierarchyIcon
	{
		public enum FERandomMovementType
		{
			Default = 0,
			Listening = 1,
			Calm = 2,
			Focused = 3,
			Nervous = 4,
			AccessingImaginedVisual = 5,
			AccessingImaginedAuditory = 6,
			AccessingFeelings = 7,
			AccessingVisualMemory = 8,
			AccessingAuditoryMemory = 9,
			AccessingInternalSelfTalk = 10
		}

		public enum FE_EyesBlinkingMode
		{
			Bones = 0,
			Blendshapes = 1,
			Bones_Position = 2,
			Bones_Scale = 3
		}

		private class BlinkTimers
		{
			public float timer;

			public float power;

			public float keepCloseTime;

			public float progress;
		}

		[Serializable]
		public class EyesAnimator_BlenshapesInfo
		{
			public int ShapeIndex;

			public float Open;

			public float Closed = 100f;

			public float PreWeight;

			[Tooltip("If this blendshape is animated and you want use blinking as additive motion, toggle this")]
			public bool Animated;

			[Tooltip("If maximum value for blendshape should be close value")]
			public bool MaxToClose;

			public float InitVal;

			public float AdditionalFactor;

			[Tooltip("If your character is using multiple meshes for animating blinking")]
			public SkinnedMeshRenderer UseOtherShape;

			public EyesAnimator_BlenshapesInfo(int initIndex)
			{
				ShapeIndex = initIndex;
			}
		}

		[Serializable]
		public class Eye
		{
			public Vector3 forward;

			public Vector3 right;

			public Vector3 up;

			public Quaternion initLocalRotation;

			public Quaternion lerpRotation;

			public Vector3 randomDir;

			public float randomTimer;

			public float lagTimer;

			public float lagProgress;

			public Quaternion lagStartRotation;

			public float changeSmoother;

			public Quaternion previousLookRotBase;

			public Quaternion mapping = Quaternion.identity;

			public void SetLagStartRotation(Transform baseTr, Quaternion worldRot)
			{
				lagStartRotation = previousLookRotBase;
			}

			public Quaternion GetLagStartRotation(Transform baseTr)
			{
				return lagStartRotation;
			}

			internal void Reset(EyeSetup setup, Transform eyeT)
			{
				randomTimer = 0f;
				randomDir = Vector3.zero;
				lagTimer = 0f;
				lagProgress = 1f;
				changeSmoother = 1f;
				if (setup.ControlType == EyeSetup.EEyeControlType.RotateBone)
				{
					if ((bool)eyeT)
					{
						lagStartRotation = eyeT.rotation;
						lerpRotation = eyeT.rotation;
						eyeT.localRotation = initLocalRotation;
					}
				}
				else if (setup.ControlType == EyeSetup.EEyeControlType.Blendshape)
				{
					setup.EyeLeftX(0f);
					setup.EyeRightX(0f);
					setup.EyeUpY(0f);
					setup.EyeDownY(0f);
				}
			}

			public void PrepareMappingFactor(FEyesAnimator eyesA, Transform t)
			{
				Transform baseTransform = eyesA.BaseTransform;
				_ = t.lossyScale;
				Vector3 b = t.InverseTransformDirection(baseTransform.forward);
				b = Vector3.Scale(t.lossyScale, b);
				Vector3 b2 = t.InverseTransformDirection(baseTransform.up);
				b2 = Vector3.Scale(t.lossyScale, b2);
				Vector3 b3 = Vector3.Cross(b, b2);
				b3 = Vector3.Scale(t.lossyScale, b3);
				if (baseTransform.InverseTransformPoint(t.position).x < 0f)
				{
					mapping = Quaternion.identity;
					mapping *= Quaternion.FromToRotation(-b3, eyesA.Right);
					mapping *= Quaternion.FromToRotation(b2, eyesA.Up);
				}
				else
				{
					mapping = Quaternion.identity;
					mapping *= Quaternion.FromToRotation(-b3, eyesA.Right);
					mapping *= Quaternion.FromToRotation(b2, eyesA.Up);
				}
			}
		}

		[Serializable]
		public class EyeSetup
		{
			public enum EEyeControlType
			{
				RotateBone = 0,
				Blendshape = 1
			}

			public SkinnedMeshRenderer BlendshapeMesh;

			public int EyeRightShape;

			public int EyeLeftShape;

			public int EyeUpShape;

			public int EyeDownShape;

			public Vector2 MinMaxValue = new Vector2(0f, 100f);

			public Vector2 IndividualClampingHorizontal = new Vector2(-60f, 60f);

			public Vector2 IndividualClampingVertical = new Vector2(-20f, 20f);

			public EEyeControlType ControlType;

			[HideInInspector]
			public bool _BlendFoldout;

			internal void EyeLeftX(float x)
			{
				float value = Mathf.Lerp(MinMaxValue.x, MinMaxValue.y, Mathf.Abs(Mathf.Min(0f, x)));
				BlendshapeMesh.SetBlendShapeWeight(EyeLeftShape, value);
			}

			internal void EyeRightX(float x)
			{
				float value = Mathf.Lerp(MinMaxValue.x, MinMaxValue.y, Mathf.Max(0f, x));
				BlendshapeMesh.SetBlendShapeWeight(EyeRightShape, value);
			}

			internal void EyeDownY(float y)
			{
				float value = Mathf.Lerp(MinMaxValue.x, MinMaxValue.y, Mathf.Abs(Mathf.Min(0f, y)));
				BlendshapeMesh.SetBlendShapeWeight(EyeDownShape, value);
			}

			internal void EyeUpY(float y)
			{
				float value = Mathf.Lerp(MinMaxValue.x, MinMaxValue.y, Mathf.Max(0f, y));
				BlendshapeMesh.SetBlendShapeWeight(EyeUpShape, value);
			}
		}

		private bool changeFlag;

		public bool UseBlinking;

		[Tooltip("If you have bones on eyelids or you want to use blenshapes")]
		public FE_EyesBlinkingMode BlinkingMode;

		[Tooltip("Eyelids game objects to animate their rotation")]
		public List<Transform> EyeLids = new List<Transform>();

		[Tooltip("Target rotations for eyelids - closed pose")]
		public List<Vector3> EyeLidsCloseRotations = new List<Vector3>();

		[Tooltip("Target positions for eyelids - closed pose")]
		public List<Vector3> EyeLidsClosePositions = new List<Vector3>();

		[Tooltip("Target scales for eyelids - closed pose")]
		public List<Vector3> EyeLidsCloseScales = new List<Vector3>();

		[Tooltip("Put here mesh of which blendshapes have to be animated")]
		public SkinnedMeshRenderer BlendShapesMesh;

		[Tooltip("Define which blendshape should be animated and how values should be animated")]
		public List<EyesAnimator_BlenshapesInfo> BlendShapes;

		public List<EyesAnimator_BlenshapesInfo> UpEyelidsBlendShapes;

		public List<EyesAnimator_BlenshapesInfo> DownEyelidsBlendShapes;

		[Tooltip("If you would need to smoothly disable blinking animation in some cases")]
		[FPD_Suffix(0f, 1f, FPD_SuffixAttribute.SuffixMode.From0to100, "%", true, 0)]
		public float BlinkingBlend = 1f;

		[Tooltip("Syncing frequency of blinking with random movement preset choosed in 'Animation Settings' Tab")]
		public bool SyncWithRandomPreset = true;

		[Range(0.35f, 4f)]
		[Tooltip("How fast should occur eyelids blinking")]
		public float BlinkFrequency = 1f;

		[Range(0.25f, 5f)]
		[Tooltip("If you want eyelids movement to be slower or quicker")]
		public float OpenCloseSpeed = 1f;

		[Tooltip("When you close eyes, for very short moment you keep them closed - higher value - keeping closed duration is longer")]
		[Range(0.05f, 0.5f)]
		public float HoldClosedTime = 0.15f;

		[Tooltip("If each eye should blink in individual random timers")]
		public bool IndividualBlinking;

		[Tooltip("If you want simply animate eyelids beeing a bit closed than opened wide for the character. For example you can easily simulate that character is tired.")]
		[Range(0f, 1f)]
		public float MinOpenValue = 1f;

		[Range(0f, 1.5f)]
		[Tooltip("When we look up in real life, our eyelids are opening a bit more, when looking down, upper lid is closing a bit and down lid is shifting a little - this option will simulate this behaviour if eyelids are setted properly (subtle effect)")]
		public float AdditionalEyelidsMotion;

		[Range(1f, 1.9f)]
		public Vector2 EyelidsMotionLimit = new Vector2(-1f, 1.5f);

		public List<Transform> UpEyelids = new List<Transform>();

		public List<Transform> DownEyelids = new List<Transform>();

		private List<int> upEyelidsIndexes;

		private List<int> downEyelidsIndexes;

		private List<float> upEyelidsFactor;

		private List<float> downEyelidsFactor;

		private Vector2 deltaV;

		private BlinkTimers[] blinking;

		private Quaternion[] initEyelidsLocalRotations;

		private Vector3[] initEyelidsLocalPositions;

		private Vector3[] initEyesLocalScales;

		private float blinkScale = 1f;

		public bool BlinkAnimatedEyelids;

		private Vector2 clampDelta;

		private bool blendshapeAnglesRequest;

		private bool? forceOutOfMaxDistance;

		public int clampedHorizontal;

		public int clampedVertical;

		public bool _gizmosDrawMaxDist = true;

		public bool DrawGizmos = true;

		private Vector3[] _editor_eyeForwards;

		public bool debugSwitch;

		[HideInInspector]
		public List<EyeSetup> EyeSetups = new List<EyeSetup>();

		[Tooltip("Main reference object for eyes, must face forward (blue-Z) axis with it's rotation")]
		[SerializeField]
		private Transform _baseTransform;

		[Tooltip("Target to look for eyes")]
		public Transform EyesTarget;

		[Tooltip("Head transform reference for look start position and also reference to limit range how much eyes can rotate")]
		public Transform HeadReference;

		[Tooltip("Sometimes head bone can be too low or too high and you can correct this with StartLookOffset (depends of head bone rotations and scale etc. position axes can behave unusual)")]
		public Vector3 StartLookOffset;

		[Tooltip("Eyes transforms / bones (origin/pivot should be in center of the sphere")]
		public List<Transform> Eyes = new List<Transform>();

		[Tooltip("If you want component to not compute it's algorithm when certain renderer is not visible, drag & drop it here")]
		public Renderer OptimizeWithMesh;

		[Tooltip("If you want component to not compute it's algorithm when being far from the Camera")]
		public float OptimizeAtDistance;

		[Tooltip("All eyes animator features usage blend slider. Change if you want to disable all look animator features simultaneously (looking, random eyes movement and blinking.\n(Optimization) Eyes Animator will internally disable itself when this value is zero.")]
		[FPD_Suffix(0f, 1f, FPD_SuffixAttribute.SuffixMode.From0to100, "%", true, 0)]
		public float EyesAnimatorAmount = 1f;

		[Tooltip("You can smoothly change it to 0 if you want to disable eyes animation looking at target behaviour")]
		[FPD_Suffix(0f, 1f, FPD_SuffixAttribute.SuffixMode.From0to100, "%", true, 0)]
		public float FollowTargetAmount = 1f;

		[Tooltip("How fast eyes should rotate to desired rotations")]
		[Range(0f, 2f)]
		public float EyesSpeed = 1f;

		protected float eyesSpeedValue = 1f;

		[Tooltip("Compensating rotations for eyes to avoid squinting (eyes crossing), sometimes you will want to have some of this")]
		[Range(0f, 1f)]
		public float SquintPreventer = 1f;

		[Tooltip("Additional random movement for eyes giving more natural feel - you can crank it up for example when there is no target for eyes, or when character is talking with someone")]
		[FPD_Suffix(0f, 1f, FPD_SuffixAttribute.SuffixMode.From0to100, "%", true, 0)]
		public float EyesRandomMovement = 0.3f;

		public Vector2 RandomMovementAxisScale = Vector2.one;

		public FERandomMovementType RandomMovementPreset;

		[Tooltip("How frequently should occur rotation change for random eyes movement")]
		[Range(0f, 3f)]
		public float RandomizingSpeed = 1f;

		[Tooltip("Option for monsters, each eye will have individual random rotation direction")]
		public bool EyesRandomMovementIndividual;

		[Tooltip("When we rotate eyes in real life, they're reaching target with kinda jumpy movement, but for more toon effect you can left this value at 0")]
		[FPD_Suffix(0f, 1f, FPD_SuffixAttribute.SuffixMode.From0to100, "%", true, 0)]
		public float EyesLagAmount = 0.65f;

		[Tooltip("Making lags a bit smaller and more frequent when setted to lower value")]
		[Range(0.1f, 1f)]
		public float LagStiffness = 1f;

		[Tooltip("Option for monsters, each eye will have individual random delay for movement")]
		public bool IndividualLags;

		[Range(0f, 1f)]
		public float GoOutFactor;

		[Tooltip("In what angle eyes should go back to deafult position")]
		[FPD_Suffix(5f, 180f, FPD_SuffixAttribute.SuffixMode.FromMinToMaxRounded, "°", true, 0)]
		public float StopLookAbove = 180f;

		[Tooltip("Maximum distance of target to look at, when exceed eyes will go back to default rotation. When max distance is equal 0, distance limit is infinite")]
		public float MaxTargetDistance;

		[Range(0.25f, 4f)]
		[Tooltip("Fading in/out eyes blend when max range is exceeded")]
		public float BlendTransitionSpeed = 1f;

		public Vector2 EyesClampHorizontal = new Vector2(-35f, 35f);

		public Vector2 EyesClampVertical = new Vector2(-35f, 35f);

		public List<Vector3> CorrectionOffsets = new List<Vector3>();

		public bool IndividualClamping;

		private Vector3 targetLookPosition;

		private float conditionalFollowBlend = 1f;

		private Vector3 targetLookPositionOffset = Vector3.zero;

		private Eye[] eyesData;

		private Vector3 headForwardFromTo;

		private Vector3 headForward;

		public Vector3 headReferenceLookForward;

		public Vector3 headReferenceUp;

		public Vector3 headUp;

		public Vector3 headUpDynamic;

		private Vector3 WorldUp = Vector3.up;

		public bool WorldUpIsBaseTransformUp;

		[Tooltip("Makes start look reference position static, making look stable, but it will not work if your character will lie in bed or do other dramatic root bone transformations.")]
		public bool StaticLookStartPosition;

		private bool optimizeUpdate;

		public float HeadToTargetAngle { get; private set; }

		public Vector3 HeadRotation { get; private set; }

		public Vector3 TargetLookDirection { get; private set; }

		public Vector3 LookRotationBase { get; private set; }

		public Vector3 DeltaVector { get; private set; }

		public Vector3 LookStartPositionBase { get; private set; }

		public Vector2 LookDeltaAngles { get; private set; }

		public Vector2 LookDeltaAnglesClamped { get; private set; }

		public bool OutOfRange { get; private set; }

		public bool OutOfDistance { get; private set; }

		public bool IsClamping { get; private set; }

		public string EditorIconPath
		{
			get
			{
				if (PlayerPrefs.GetInt("EyesH", 1) == 0)
				{
					return "";
				}
				return "Eyes Animator/EyesAnimator_IconSmall";
			}
		}

		public Transform BaseTransform
		{
			get
			{
				if (_baseTransform == null)
				{
					return base.transform;
				}
				return _baseTransform;
			}
		}

		public Transform OptimizationCameraReference { get; private set; }

		private Vector3 Forward => Vector3.forward;

		private Vector3 Up => Vector3.up;

		private Vector3 Right => Vector3.right;

		private void CalculateLagTimer(int i)
		{
			eyesData[i].lagTimer -= Time.deltaTime / LagStiffness;
			if (IsClamping)
			{
				eyesData[i].SetLagStartRotation(BaseTransform, eyesData[i].lerpRotation);
			}
			else if (eyesData[i].lagProgress > 0f)
			{
				if (eyesData[i].lagTimer < 0f)
				{
					eyesData[i].lagProgress -= UnityEngine.Random.Range(0.4f, 0.85f) * 0.7f * LagStiffness;
				}
			}
			else if (eyesData[i].lagProgress <= 0f && eyesData[i].lagTimer < 0f)
			{
				eyesData[i].lagProgress = 1f;
				eyesData[i].SetLagStartRotation(BaseTransform, eyesData[i].lerpRotation);
				changeFlag = true;
			}
			if (eyesData[i].lagTimer < 0f)
			{
				eyesData[i].lagTimer = UnityEngine.Random.Range(0.15f, 0.34f);
			}
		}

		private void CalculateLagTimerNonIndividualEvent(int i)
		{
			if (changeFlag)
			{
				eyesData[i].SetLagStartRotation(BaseTransform, eyesData[i].lerpRotation);
			}
		}

		private void CalculateRandomTimer(int i)
		{
			eyesData[i].randomTimer -= Time.deltaTime * RandomizingSpeed;
			if (!(eyesData[i].randomTimer < 0f))
			{
				return;
			}
			if (eyesData[i].randomDir.magnitude > (EyesClampHorizontal.magnitude + EyesClampVertical.magnitude) / 8f)
			{
				float num = 5f;
				eyesData[i].randomDir = new Vector3(UnityEngine.Random.Range(0f - num, num), UnityEngine.Random.Range(0f - num, num), 0f);
				eyesData[i].randomDir = Vector2.Scale(eyesData[i].randomDir, RandomMovementAxisScale);
				switch (RandomMovementPreset)
				{
				case FERandomMovementType.Default:
				case FERandomMovementType.Listening:
				case FERandomMovementType.Calm:
				case FERandomMovementType.Focused:
					eyesData[i].randomTimer = UnityEngine.Random.Range(0.9f, 2.4f);
					break;
				case FERandomMovementType.Nervous:
					eyesData[i].randomTimer = UnityEngine.Random.Range(0.2f, 0.6f);
					break;
				case FERandomMovementType.AccessingImaginedVisual:
				case FERandomMovementType.AccessingImaginedAuditory:
				case FERandomMovementType.AccessingFeelings:
				case FERandomMovementType.AccessingVisualMemory:
				case FERandomMovementType.AccessingAuditoryMemory:
				case FERandomMovementType.AccessingInternalSelfTalk:
					eyesData[i].randomTimer = UnityEngine.Random.Range(0.6f, 0.9f);
					break;
				}
			}
			else
			{
				switch (RandomMovementPreset)
				{
				case FERandomMovementType.Default:
					eyesData[i].randomTimer = UnityEngine.Random.Range(0.4f, 1.24f);
					eyesData[i].randomDir = new Vector3(UnityEngine.Random.Range(-28, 28), UnityEngine.Random.Range(-28, 28), 0f);
					break;
				case FERandomMovementType.Listening:
					eyesData[i].randomTimer = UnityEngine.Random.Range(0.4f, 1.24f);
					eyesData[i].randomDir = new Vector3(UnityEngine.Random.Range(-10, 10), UnityEngine.Random.Range(-10, 10), 0f);
					break;
				case FERandomMovementType.Calm:
					eyesData[i].randomTimer = UnityEngine.Random.Range(1.11f, 2.2f);
					eyesData[i].randomDir = new Vector3(UnityEngine.Random.Range(-25, 25), UnityEngine.Random.Range(-25, 25), 0f);
					break;
				case FERandomMovementType.Focused:
					eyesData[i].randomTimer = UnityEngine.Random.Range(1.3f, 3.2f);
					eyesData[i].randomDir = new Vector3(UnityEngine.Random.Range(-30, 30), UnityEngine.Random.Range(-30, 30), 0f);
					break;
				case FERandomMovementType.Nervous:
					eyesData[i].randomTimer = UnityEngine.Random.Range(0.165f, 0.34f);
					eyesData[i].randomDir = new Vector3(UnityEngine.Random.Range(-24, 24), UnityEngine.Random.Range(-24, 24), 0f);
					break;
				case FERandomMovementType.AccessingImaginedVisual:
					eyesData[i].randomTimer = UnityEngine.Random.Range(0.45f, 1.7f);
					eyesData[i].randomDir = new Vector3(UnityEngine.Random.Range(-50, -35), UnityEngine.Random.Range(40, 60), 0f);
					break;
				case FERandomMovementType.AccessingImaginedAuditory:
					eyesData[i].randomTimer = UnityEngine.Random.Range(0.45f, 1.7f);
					eyesData[i].randomDir = new Vector3(UnityEngine.Random.Range(-3, 3), UnityEngine.Random.Range(40, 60), 0f);
					break;
				case FERandomMovementType.AccessingFeelings:
					eyesData[i].randomTimer = UnityEngine.Random.Range(0.45f, 1.7f);
					eyesData[i].randomDir = new Vector3(UnityEngine.Random.Range(30, 40), UnityEngine.Random.Range(40, 60), 0f);
					break;
				case FERandomMovementType.AccessingVisualMemory:
					eyesData[i].randomTimer = UnityEngine.Random.Range(0.45f, 1.7f);
					eyesData[i].randomDir = new Vector3(UnityEngine.Random.Range(-60, -40), UnityEngine.Random.Range(-60, -40), 0f);
					break;
				case FERandomMovementType.AccessingAuditoryMemory:
					eyesData[i].randomTimer = UnityEngine.Random.Range(0.45f, 1.7f);
					eyesData[i].randomDir = new Vector3(UnityEngine.Random.Range(-3, 3), UnityEngine.Random.Range(-60, -40), 0f);
					break;
				case FERandomMovementType.AccessingInternalSelfTalk:
					eyesData[i].randomTimer = UnityEngine.Random.Range(0.45f, 1.7f);
					eyesData[i].randomDir = new Vector3(UnityEngine.Random.Range(40, 60), UnityEngine.Random.Range(-60, -40), 0f);
					break;
				}
				eyesData[i].randomDir = Vector2.Scale(eyesData[i].randomDir, RandomMovementAxisScale);
			}
			float num2 = Mathf.Lerp(0.4f, 1.3f, EyesLagAmount);
			eyesData[i].changeSmoother = UnityEngine.Random.Range(0.5f, 0.85f) * num2;
		}

		private void SetupBlinking()
		{
			if (BlinkingMode != FE_EyesBlinkingMode.Blendshapes)
			{
				blinking = new BlinkTimers[EyeLids.Count];
			}
			else
			{
				blinking = new BlinkTimers[BlendShapes.Count];
			}
			initEyelidsLocalRotations = new Quaternion[EyeLids.Count];
			initEyelidsLocalPositions = new Vector3[EyeLids.Count];
			initEyesLocalScales = new Vector3[EyeLids.Count];
			if (BlinkingMode != FE_EyesBlinkingMode.Blendshapes)
			{
				upEyelidsIndexes = new List<int>();
				downEyelidsIndexes = new List<int>();
				upEyelidsFactor = new List<float>();
				downEyelidsFactor = new List<float>();
			}
			for (int i = 0; i < blinking.Length; i++)
			{
				blinking[i] = new BlinkTimers
				{
					timer = 1f,
					power = 1f,
					keepCloseTime = 0f,
					progress = 0f
				};
				if (BlinkingMode == FE_EyesBlinkingMode.Blendshapes)
				{
					continue;
				}
				initEyelidsLocalRotations[i] = EyeLids[i].localRotation;
				initEyelidsLocalPositions[i] = EyeLids[i].localPosition;
				initEyesLocalScales[i] = EyeLids[i].localScale;
				for (int j = 0; j < UpEyelids.Count; j++)
				{
					if (UpEyelids[j] == EyeLids[i])
					{
						upEyelidsIndexes.Add(i);
					}
					upEyelidsFactor.Add(0f);
				}
				for (int k = 0; k < DownEyelids.Count; k++)
				{
					if (DownEyelids[k] == EyeLids[i])
					{
						downEyelidsIndexes.Add(i);
					}
					downEyelidsFactor.Add(0f);
				}
			}
		}

		private void UpdateBlinking()
		{
			blinkScale = Mathf.Lerp(0.65f, 1.5f, BlinkFrequency / 3f);
			if (!IndividualBlinking)
			{
				CalculateBlinking();
				CalculateProgress(0);
				if (BlinkingMode != FE_EyesBlinkingMode.Blendshapes)
				{
					for (int i = 0; i < blinking.Length; i++)
					{
						AnimateBlinkingBones(i, 0);
					}
				}
				else
				{
					for (int j = 0; j < blinking.Length; j++)
					{
						AnimateBlinkingBlendshapes(j, 0);
					}
					if (!OutOfRange)
					{
						for (int k = 0; k < UpEyelidsBlendShapes.Count; k++)
						{
							AnimateBlendshapedAdditionalMotion(UpEyelidsBlendShapes[k], 0, upper: true);
						}
						for (int l = 0; l < DownEyelidsBlendShapes.Count; l++)
						{
							AnimateBlendshapedAdditionalMotion(DownEyelidsBlendShapes[l], 0, upper: false);
						}
					}
				}
			}
			else if (BlinkingMode != FE_EyesBlinkingMode.Blendshapes)
			{
				for (int m = 0; m < blinking.Length; m++)
				{
					CalculateProgress(m);
					CalculateBlinking(m);
					AnimateBlinkingBones(m, m);
				}
			}
			else
			{
				for (int n = 0; n < blinking.Length; n++)
				{
					CalculateProgress(n);
					CalculateBlinking(n);
					AnimateBlinkingBlendshapes(n, n);
				}
				if (!OutOfRange)
				{
					for (int num = 0; num < UpEyelidsBlendShapes.Count; num++)
					{
						AnimateBlendshapedAdditionalMotion(UpEyelidsBlendShapes[num], num, upper: true);
					}
					for (int num2 = 0; num2 < DownEyelidsBlendShapes.Count; num2++)
					{
						AnimateBlendshapedAdditionalMotion(DownEyelidsBlendShapes[num2], num2, upper: false);
					}
				}
			}
			if (SyncWithRandomPreset)
			{
				if (RandomMovementPreset < FERandomMovementType.Calm)
				{
					BlinkFrequency = 1f;
				}
				else if (RandomMovementPreset < FERandomMovementType.AccessingImaginedVisual)
				{
					BlinkFrequency = 0.65f;
				}
				else if (RandomMovementPreset == FERandomMovementType.AccessingImaginedVisual)
				{
					BlinkFrequency = 2.75f;
				}
				else
				{
					BlinkFrequency = 1.45f;
				}
			}
		}

		private void CalculateBlinking(int i = 0)
		{
			if (blinking[i].keepCloseTime <= 0f && blinking[i].progress <= 0f)
			{
				blinking[i].timer -= Time.deltaTime * BlinkFrequency;
			}
			if (blinking[i].timer <= 0f)
			{
				blinking[i].timer = UnityEngine.Random.Range(0.75f, 1.5f);
				blinking[i].power = UnityEngine.Random.Range(3f * blinkScale, 5f * blinkScale);
				blinking[i].keepCloseTime = HoldClosedTime + HoldClosedTime * UnityEngine.Random.Range(0f, 0.125f);
			}
		}

		private void AnimateBlinkingBones(int i, int varInd)
		{
			if (BlinkingMode == FE_EyesBlinkingMode.Bones)
			{
				Quaternion a = (BlinkAnimatedEyelids ? EyeLids[i].localRotation : initEyelidsLocalRotations[i]);
				Quaternion quaternion = Quaternion.Euler(EyeLidsCloseRotations[i]);
				if (MinOpenValue < 1f)
				{
					a = Quaternion.Slerp(quaternion, BlinkAnimatedEyelids ? EyeLids[i].localRotation : initEyelidsLocalRotations[i], MinOpenValue);
				}
				if (!OutOfRange && AdditionalEyelidsMotion > 0f)
				{
					blendshapeAnglesRequest = true;
					Vector2 vector = -LookDeltaAnglesClamped;
					bool flag = false;
					if (UpEyelids.Count > 0)
					{
						int num = -1;
						for (int j = 0; j < upEyelidsIndexes.Count; j++)
						{
							if (upEyelidsIndexes[j] == i)
							{
								num = j;
							}
						}
						if (num >= 0)
						{
							int num2 = 0;
							if (IndividualLags)
							{
								num2 = i;
							}
							float b;
							if (vector.x > 1f)
							{
								float num3 = Mathf.Lerp(0f, -0.475f * AdditionalEyelidsMotion, Mathf.InverseLerp(1f, EyesClampVertical.y, vector.x));
								b = ((!(EyesLagAmount > 0f)) ? num3 : ((!(eyesData[num2].lagProgress > 0f)) ? num3 : Mathf.Lerp(num3, upEyelidsFactor[num], eyesData[num2].lagProgress * EyesLagAmount)));
							}
							else if (!(vector.x < -1f))
							{
								b = ((!(EyesLagAmount > 0f)) ? 0f : ((!(eyesData[num2].lagProgress > 0f)) ? 0f : Mathf.Lerp(0f, upEyelidsFactor[num], eyesData[num2].lagProgress * EyesLagAmount)));
							}
							else
							{
								float num4 = Mathf.Lerp(0f, 0.4f * AdditionalEyelidsMotion, Mathf.InverseLerp(-1f, EyesClampVertical.x, vector.x));
								b = ((!(EyesLagAmount > 0f)) ? num4 : ((!(eyesData[num2].lagProgress > 0f)) ? num4 : Mathf.Lerp(num4, upEyelidsFactor[num], eyesData[num2].lagProgress * EyesLagAmount)));
							}
							upEyelidsFactor[num] = Mathf.Lerp(upEyelidsFactor[num], b, Time.deltaTime * eyesSpeedValue);
							a = Quaternion.SlerpUnclamped(a, Quaternion.Euler(EyeLidsCloseRotations[i]), upEyelidsFactor[num]);
							flag = true;
						}
					}
					if (!flag && DownEyelids.Count > 0)
					{
						int num5 = -1;
						for (int k = 0; k < downEyelidsIndexes.Count; k++)
						{
							if (downEyelidsIndexes[k] == i)
							{
								num5 = k;
							}
						}
						if (num5 >= 0)
						{
							int num6 = 0;
							if (IndividualLags)
							{
								num6 = i;
							}
							float b2;
							if (vector.x > 1f)
							{
								float num7 = Mathf.Lerp(0f, 0.3f * AdditionalEyelidsMotion, Mathf.InverseLerp(1f, EyesClampVertical.y, vector.x));
								b2 = ((!(EyesLagAmount > 0f)) ? num7 : ((!(eyesData[num6].lagProgress > 0f)) ? num7 : Mathf.Lerp(num7, downEyelidsFactor[num5], eyesData[num6].lagProgress * EyesLagAmount)));
							}
							else if (!(vector.x < -1f))
							{
								b2 = ((!(EyesLagAmount > 0f)) ? 0f : ((!(eyesData[num6].lagProgress > 0f)) ? 0f : Mathf.Lerp(0f, downEyelidsFactor[num5], eyesData[num6].lagProgress * EyesLagAmount)));
							}
							else
							{
								float num8 = Mathf.Lerp(-1.9f * AdditionalEyelidsMotion, 0f, Mathf.InverseLerp(EyesClampVertical.x, -1f, vector.x));
								b2 = ((!(EyesLagAmount > 0f)) ? num8 : ((!(eyesData[num6].lagProgress > 0f)) ? num8 : Mathf.Lerp(num8, downEyelidsFactor[num5], eyesData[num6].lagProgress * EyesLagAmount)));
							}
							downEyelidsFactor[num5] = Mathf.Lerp(downEyelidsFactor[num5], b2, Time.deltaTime * eyesSpeedValue);
							downEyelidsFactor[num5] = Mathf.Clamp(downEyelidsFactor[num5], EyelidsMotionLimit.x, EyelidsMotionLimit.y);
							a = Quaternion.SlerpUnclamped(a, Quaternion.Euler(EyeLidsCloseRotations[i]), downEyelidsFactor[num5]);
						}
					}
				}
				EyeLids[i].localRotation = Quaternion.Lerp(a, quaternion, blinking[varInd].progress * BlinkingBlend);
			}
			else if (BlinkingMode == FE_EyesBlinkingMode.Bones_Position)
			{
				EyeLids[i].localPosition = Vector3.Lerp(initEyelidsLocalPositions[i], initEyelidsLocalPositions[i] + EyeLidsClosePositions[i], blinking[varInd].progress * BlinkingBlend);
			}
			else if (BlinkingMode == FE_EyesBlinkingMode.Bones_Scale)
			{
				EyeLids[i].localScale = Vector3.Lerp(initEyesLocalScales[i], EyeLidsCloseScales[i], blinking[varInd].progress * BlinkingBlend);
			}
		}

		private void CalculateProgress(int varInd)
		{
			if (blinking[varInd].keepCloseTime > 0f)
			{
				blinking[varInd].progress += Time.deltaTime * blinking[varInd].power * OpenCloseSpeed * 2.7f;
				if (blinking[varInd].progress >= 1f)
				{
					blinking[varInd].progress = 1f;
					blinking[varInd].keepCloseTime -= Time.deltaTime * blinkScale;
				}
			}
			else
			{
				blinking[varInd].progress -= Time.deltaTime * blinking[varInd].power * 0.87f * OpenCloseSpeed * 2.7f;
			}
		}

		private void AnimateBlendshapedAdditionalMotion(EyesAnimator_BlenshapesInfo lid, int lagId, bool upper)
		{
			if (!(AdditionalEyelidsMotion > 0f))
			{
				return;
			}
			blendshapeAnglesRequest = true;
			Vector2 vector = -LookDeltaAnglesClamped;
			if (upper && UpEyelidsBlendShapes.Count > 0)
			{
				float b;
				if (vector.x > 1f)
				{
					float num = Mathf.Lerp(0f, -0.475f, Mathf.InverseLerp(1f, EyesClampVertical.y, vector.x));
					b = ((!(EyesLagAmount > 0f)) ? num : ((!(eyesData[lagId].lagProgress > 0f)) ? num : Mathf.Lerp(num, lid.AdditionalFactor, eyesData[lagId].lagProgress * EyesLagAmount)));
				}
				else if (!(vector.x < -1f))
				{
					b = ((!(EyesLagAmount > 0f)) ? 0f : ((!(eyesData[lagId].lagProgress > 0f)) ? 0f : Mathf.Lerp(0f, lid.AdditionalFactor, eyesData[lagId].lagProgress * EyesLagAmount)));
				}
				else
				{
					float num2 = Mathf.Lerp(0f, 0.4f, Mathf.InverseLerp(-1f, EyesClampVertical.x, vector.x));
					b = ((!(EyesLagAmount > 0f)) ? num2 : ((!(eyesData[lagId].lagProgress > 0f)) ? num2 : Mathf.Lerp(num2, lid.AdditionalFactor, eyesData[lagId].lagProgress * EyesLagAmount)));
				}
				lid.AdditionalFactor = Mathf.Lerp(lid.AdditionalFactor, b, Time.deltaTime * 30f * eyesSpeedValue);
				if (lid.AdditionalFactor < 0f)
				{
					lid.AdditionalFactor = 0f;
				}
				if (lid.Animated)
				{
					lid.PreWeight = BlendShapesMesh.GetBlendShapeWeight(lid.ShapeIndex);
				}
				else
				{
					lid.PreWeight = 0f;
				}
				float num3 = Mathf.Lerp(lid.Open * AdditionalEyelidsMotion, lid.Closed * AdditionalEyelidsMotion, lid.AdditionalFactor);
				num3 += lid.PreWeight;
				BlendShapesMesh.SetBlendShapeWeight(lid.ShapeIndex, num3);
			}
			if (!upper && DownEyelidsBlendShapes.Count > 0)
			{
				float b2;
				if (vector.x > 1f)
				{
					float num4 = Mathf.Lerp(0f, 0.3f, Mathf.InverseLerp(1f, EyesClampVertical.y, vector.x));
					b2 = ((!(EyesLagAmount > 0f)) ? num4 : ((!(eyesData[lagId].lagProgress > 0f)) ? num4 : Mathf.Lerp(num4, lid.AdditionalFactor, eyesData[lagId].lagProgress * EyesLagAmount)));
				}
				else if (!(vector.x < -1f))
				{
					b2 = ((!(EyesLagAmount > 0f)) ? 0f : ((!(eyesData[lagId].lagProgress > 0f)) ? 0f : Mathf.Lerp(0f, lid.AdditionalFactor, eyesData[lagId].lagProgress * EyesLagAmount)));
				}
				else
				{
					float num5 = Mathf.Lerp(-1.9f, 0f, Mathf.InverseLerp(EyesClampVertical.x, -1f, vector.x));
					b2 = ((!(EyesLagAmount > 0f)) ? num5 : ((!(eyesData[lagId].lagProgress > 0f)) ? num5 : Mathf.Lerp(num5, lid.AdditionalFactor, eyesData[lagId].lagProgress * EyesLagAmount)));
				}
				lid.AdditionalFactor = Mathf.Lerp(lid.AdditionalFactor, b2, Time.deltaTime * 30f * eyesSpeedValue);
				if (lid.Animated)
				{
					lid.PreWeight = BlendShapesMesh.GetBlendShapeWeight(lid.ShapeIndex);
				}
				else
				{
					lid.PreWeight = 0f;
				}
				float num6 = Mathf.Lerp(lid.Open * AdditionalEyelidsMotion, lid.Closed * AdditionalEyelidsMotion, lid.AdditionalFactor);
				num6 += lid.PreWeight;
				BlendShapesMesh.SetBlendShapeWeight(lid.ShapeIndex, num6);
			}
		}

		private void AnimateBlinkingBlendshapes(int i, int varInd)
		{
			if (BlendShapes[i].Animated)
			{
				BlendShapes[i].PreWeight = BlendShapesMesh.GetBlendShapeWeight(BlendShapes[i].ShapeIndex);
			}
			else
			{
				BlendShapes[i].PreWeight = 0f;
			}
			float num = Mathf.Lerp(BlendShapes[i].Open, BlendShapes[i].Closed, blinking[varInd].progress * BlinkingBlend);
			num += BlendShapes[i].PreWeight;
			if (BlendShapes[i].MaxToClose && num > BlendShapes[i].Closed)
			{
				num = BlendShapes[i].Closed;
			}
			if ((bool)BlendShapes[i].UseOtherShape)
			{
				BlendShapes[i].UseOtherShape.SetBlendShapeWeight(BlendShapes[i].ShapeIndex, num);
			}
			else
			{
				BlendShapesMesh.SetBlendShapeWeight(BlendShapes[i].ShapeIndex, num);
			}
		}

		private void ComputeBaseRotations()
		{
			LookStartPositionBase = GetStartLookPosition();
			TargetLookDirection = targetLookPosition - LookStartPositionBase;
			HeadToTargetAngle = Vector3.Angle(HeadReference.transform.TransformDirection(headForward), TargetLookDirection.normalized);
			Quaternion quaternion = Quaternion.LookRotation(TargetLookDirection, WorldUp);
			if (FollowTargetAmount * conditionalFollowBlend < 1f)
			{
				Quaternion quaternion2 = Quaternion.LookRotation(HeadReference.rotation * headForward * 100f, WorldUp);
				quaternion = ((!(FollowTargetAmount * conditionalFollowBlend > 0f)) ? quaternion2 : Quaternion.SlerpUnclamped(quaternion2, Quaternion.LookRotation(targetLookPosition - LookStartPositionBase), FollowTargetAmount * conditionalFollowBlend));
			}
			LookRotationBase = quaternion.eulerAngles;
			HeadRotation = (HeadReference.rotation * Quaternion.FromToRotation(headForwardFromTo, Vector3.forward)).eulerAngles;
			CheckLookRanges();
			if (blendshapeAnglesRequest)
			{
				Vector3 vector = Quaternion.Inverse(HeadReference.rotation) * TargetLookDirection.normalized;
				Vector2 lookDeltaAngles = default;
				lookDeltaAngles.y = AngleAroundAxis(headReferenceLookForward, vector, headReferenceUp);
				Vector3 axis = Vector3.Cross(headReferenceUp, vector);
				Vector3 firstDirection = vector - Vector3.Project(vector, headReferenceUp);
				lookDeltaAngles.x = AngleAroundAxis(firstDirection, vector, axis);
				LookDeltaAngles = lookDeltaAngles;
				Vector2 lookDeltaAngles2 = LookDeltaAngles;
				if (LookDeltaAngles.x < EyesClampVertical.x)
				{
					lookDeltaAngles2.x = EyesClampVertical.x;
				}
				else if (LookDeltaAngles.x > EyesClampVertical.y)
				{
					lookDeltaAngles2.x = EyesClampVertical.y;
				}
				if (LookDeltaAngles.y < EyesClampHorizontal.x)
				{
					lookDeltaAngles2.y = EyesClampHorizontal.x;
				}
				else if (LookDeltaAngles.y > EyesClampHorizontal.y)
				{
					lookDeltaAngles2.y = EyesClampHorizontal.y;
				}
				LookDeltaAnglesClamped = lookDeltaAngles2;
			}
		}

		public static float AngleAroundAxis(Vector3 firstDirection, Vector3 secondDirection, Vector3 axis)
		{
			firstDirection -= Vector3.Project(firstDirection, axis);
			secondDirection -= Vector3.Project(secondDirection, axis);
			return Vector3.Angle(firstDirection, secondDirection) * (float)((!(Vector3.Dot(axis, Vector3.Cross(firstDirection, secondDirection)) < 0f)) ? 1 : (-1));
		}

		private void CheckLookRanges()
		{
			if (StopLookAbove >= 180f)
			{
				OutOfRange = false;
			}
			else if (Mathf.Abs(HeadToTargetAngle) < StopLookAbove)
			{
				OutOfRange = false;
			}
			else if (Mathf.Abs(HeadToTargetAngle) > StopLookAbove * 1.2f + 10f)
			{
				OutOfRange = true;
			}
			if (MaxTargetDistance > 0f)
			{
				float num = Vector3.Distance(LookStartPositionBase, targetLookPosition);
				if (num < MaxTargetDistance)
				{
					OutOfDistance = false;
				}
				else if (num > MaxTargetDistance + MaxTargetDistance * GoOutFactor)
				{
					OutOfDistance = true;
				}
			}
			else
			{
				OutOfDistance = false;
			}
			if (forceOutOfMaxDistance.HasValue ? forceOutOfMaxDistance.Value : (OutOfRange || OutOfDistance))
			{
				conditionalFollowBlend = Mathf.Max(0f, conditionalFollowBlend - Time.deltaTime * 5f);
			}
			else
			{
				conditionalFollowBlend = Mathf.Min(1f, conditionalFollowBlend + Time.deltaTime * 5f);
			}
		}

		private void ComputeLookingRotation(ref Quaternion lookRotationBase, EyeSetup eyeSetup, int randomIndex = 0, int lagId = 0)
		{
			Vector3 lookRotation = LookRotationBase;
			if (eyesData[randomIndex].randomDir != Vector3.zero)
			{
				lookRotation += Vector3.Lerp(Vector3.zero, eyesData[randomIndex].randomDir, EyesRandomMovement);
			}
			DeltaVector = new Vector3(Mathf.DeltaAngle(lookRotation.x, HeadRotation.x), Mathf.DeltaAngle(lookRotation.y, HeadRotation.y));
			if (!IndividualClamping)
			{
				ClampDetection(DeltaVector, ref lookRotation, HeadRotation, EyesClampHorizontal, EyesClampVertical);
			}
			else
			{
				ClampDetection(DeltaVector, ref lookRotation, HeadRotation, eyeSetup.IndividualClampingHorizontal, eyeSetup.IndividualClampingVertical);
			}
			lookRotationBase = Quaternion.Euler(lookRotation);
		}

		private Vector3 GetStartLookPosition()
		{
			Vector3 position;
			if (StaticLookStartPosition)
			{
				position = BaseTransform.position;
				position.y = HeadReference.position.y;
			}
			else
			{
				position = HeadReference.transform.position;
			}
			return position + HeadReference.TransformVector(StartLookOffset);
		}

		protected virtual void ClampDetection(Vector2 deltaVector, ref Vector3 lookRotation, Vector3 rootOffset, Vector2 clampHor, Vector2 clampVert)
		{
			clampDelta = deltaVector;
			if (deltaVector.y > 0f - clampHor.x)
			{
				clampDelta.y = 0f - clampHor.x;
				lookRotation.y = rootOffset.y + clampHor.x;
				clampedHorizontal = -1;
			}
			else if (deltaVector.y < 0f - clampHor.y)
			{
				clampDelta.y = 0f - clampHor.y;
				lookRotation.y = rootOffset.y + clampHor.y;
				clampedHorizontal = 1;
			}
			else
			{
				clampedHorizontal = 0;
			}
			if (deltaVector.x > clampVert.y)
			{
				clampDelta.x = clampVert.y;
				clampedVertical = 1;
				lookRotation.x = rootOffset.x - clampVert.y;
			}
			else if (deltaVector.x < clampVert.x)
			{
				clampDelta.x = clampVert.x;
				clampedVertical = -1;
				lookRotation.x = rootOffset.x - clampVert.x;
			}
			else
			{
				clampedVertical = 0;
			}
			deltaV = deltaVector;
			if (clampedHorizontal != 0 || clampedVertical != 0)
			{
				IsClamping = true;
			}
		}

		protected void ComputeReferences()
		{
			if (Application.isPlaying)
			{
				return;
			}
			_editor_eyeForwards = new Vector3[Eyes.Count];
			for (int i = 0; i < _editor_eyeForwards.Length; i++)
			{
				if (Eyes[i] == null)
				{
					_editor_eyeForwards[i] = Vector3.zero;
					continue;
				}
				Vector3 position = Eyes[i].position;
				Vector3 position2 = Eyes[i].position + Vector3.Scale(base.transform.forward, Eyes[i].transform.lossyScale);
				_editor_eyeForwards[i] = (Eyes[i].InverseTransformPoint(position2) - Eyes[i].InverseTransformPoint(position)).normalized;
			}
			headForwardFromTo = Quaternion.FromToRotation(HeadReference.InverseTransformDirection(base.transform.forward), Vector3.forward) * Vector3.forward;
		}

		protected virtual void OnDrawGizmosSelected()
		{
			if (!DrawGizmos)
			{
				return;
			}
			float num = 0.6f;
			if ((bool)HeadReference && Eyes != null && Eyes.Count > 0 && Eyes[0] != null)
			{
				num = Vector3.Distance(HeadReference.position, Eyes[0].position);
			}
			if (EyesTarget != null && HeadReference != null)
			{
				Vector3 position;
				if (StaticLookStartPosition)
				{
					position = BaseTransform.position;
					position.y = HeadReference.position.y;
				}
				else
				{
					position = HeadReference.position;
				}
				position += HeadReference.TransformVector(StartLookOffset);
				Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.7f);
				Gizmos.DrawLine(position, EyesTarget.position + targetLookPositionOffset);
			}
			if (HeadReference != null)
			{
				ComputeReferences();
				Gizmos.color = new Color(0.3f, 0.3f, 1f, 0.7f);
				Vector3 vector;
				for (int i = 0; i < Eyes.Count; i++)
				{
					if (!(Eyes[i] == null))
					{
						Vector3 fromDirection = Vector3.zero;
						if (!Application.isPlaying)
						{
							fromDirection = _editor_eyeForwards[i];
						}
						else if (eyesData != null)
						{
							fromDirection = eyesData[i].forward;
						}
						Quaternion quaternion = Eyes[i].rotation * Quaternion.Inverse(Quaternion.FromToRotation(fromDirection, Vector3.forward));
						vector = quaternion * Vector3.forward * (num * 2.75f);
						Gizmos.DrawRay(Eyes[i].position, vector);
						Gizmos.DrawLine(Eyes[i].position + vector, Eyes[i].position + vector * 0.6f + quaternion * Vector3.right * (num / 2f));
						Gizmos.DrawLine(Eyes[i].position + vector, Eyes[i].position + vector * 0.6f + quaternion * Vector3.left * (num / 2f));
					}
				}
				Vector3 vector2 = Vector3.Lerp(base.transform.position, HeadReference.position, 0.5f);
				Gizmos.DrawSphere(vector2, num * 0.25f);
				Quaternion quaternion2 = HeadReference.rotation * Quaternion.FromToRotation(headForwardFromTo, Vector3.forward);
				vector = quaternion2 * Vector3.forward * (num * 2.75f);
				Gizmos.DrawRay(vector2, vector);
				Gizmos.DrawLine(vector2 + vector, vector2 + vector * 0.6f + quaternion2 * Vector3.right * (num / 2f));
				Gizmos.DrawLine(vector2 + vector, vector2 + vector * 0.6f + quaternion2 * Vector3.left * (num / 2f));
			}
			Gizmos_DrawMaxDistance();
		}

		private void Gizmos_DrawMaxDistance()
		{
			if (!(MaxTargetDistance <= 0f) && _gizmosDrawMaxDist)
			{
				Vector3 startLookPosition = GetStartLookPosition();
				float a = 0.525f;
				Gizmos.color = new Color(0.1f, 0.835f, 0.08f, a);
				Gizmos.DrawWireSphere(GetStartLookPosition(), MaxTargetDistance);
				if (GoOutFactor > 0f)
				{
					Gizmos.color = new Color(0.835f, 0.135f, 0.08f, a);
					Gizmos.DrawWireSphere(GetStartLookPosition(), MaxTargetDistance + MaxTargetDistance * GoOutFactor);
				}
				Gizmos.color = new Color(0.02f, 0.65f, 0.2f, a);
				Gizmos.DrawLine(startLookPosition - Vector3.right * (MaxTargetDistance + MaxTargetDistance * GoOutFactor), startLookPosition + Vector3.right * (MaxTargetDistance + MaxTargetDistance * GoOutFactor));
				Gizmos.DrawLine(startLookPosition - BaseTransform.forward.normalized * (MaxTargetDistance + MaxTargetDistance * GoOutFactor), startLookPosition + Vector3.forward * (MaxTargetDistance + MaxTargetDistance * GoOutFactor));
			}
		}

		public void OnDrop(PointerEventData data)
		{
		}

		private void Reset()
		{
			_baseTransform = base.transform;
		}

		public void ResetLook()
		{
			UpdateLists();
			for (int i = 0; i < eyesData.Length; i++)
			{
				eyesData[i].Reset(EyeSetups[i], Eyes[i]);
			}
			OutOfDistance = true;
			OutOfRange = true;
		}

		public virtual void FindHeadBone()
		{
			Animator component = GetComponent<Animator>();
			Transform transform = null;
			if ((bool)component && component.isHuman)
			{
				transform = component.GetBoneTransform(HumanBodyBones.Head);
			}
			Transform transform2 = null;
			Transform transform3 = null;
			Transform[] componentsInChildren = GetComponentsInChildren<Transform>();
			foreach (Transform transform4 in componentsInChildren)
			{
				if (!(transform4 == base.transform) && transform4.name.ToLower().Contains("head"))
				{
					if (!transform4.GetComponent<SkinnedMeshRenderer>())
					{
						transform2 = transform4;
						break;
					}
					if (!(transform4.parent == base.transform))
					{
						transform3 = transform4;
					}
				}
			}
			if (!transform2)
			{
				componentsInChildren = GetComponentsInChildren<Transform>();
				foreach (Transform transform5 in componentsInChildren)
				{
					if (transform5.name.ToLower().Contains("neck"))
					{
						transform2 = transform5;
						break;
					}
				}
			}
			if (transform2 == null && transform != null)
			{
				transform2 = transform;
			}
			else if (transform2 != null && transform != null)
			{
				if (transform.name.ToLower().Contains("head"))
				{
					transform2 = transform;
				}
				else if (!transform2.name.ToLower().Contains("head"))
				{
					transform2 = transform;
				}
			}
			if ((bool)transform2)
			{
				HeadReference = transform2;
				FindingEyes();
				return;
			}
			if ((bool)transform3)
			{
				HeadReference = transform3;
			}
			Debug.LogWarning("Found " + transform3?.ToString() + " but it's probably wrong transform");
		}

		public virtual void FindingEyes()
		{
			if (HeadReference == null)
			{
				return;
			}
			Transform[] componentsInChildren = HeadReference.GetComponentsInChildren<Transform>();
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				string text = componentsInChildren[i].name.ToLower();
				if (!text.Contains("eye") || text.Contains("brow") || text.Contains("lid") || text.Contains("las"))
				{
					continue;
				}
				if (text.Contains("left"))
				{
					if (!Eyes.Contains(componentsInChildren[i]))
					{
						Eyes.Add(componentsInChildren[i]);
					}
				}
				else if (text.Contains("l"))
				{
					if (!Eyes.Contains(componentsInChildren[i]))
					{
						Eyes.Add(componentsInChildren[i]);
					}
				}
				else if (text.Contains("right"))
				{
					if (!Eyes.Contains(componentsInChildren[i]))
					{
						Eyes.Add(componentsInChildren[i]);
					}
				}
				else if (text.Contains("r") && !Eyes.Contains(componentsInChildren[i]))
				{
					Eyes.Add(componentsInChildren[i]);
				}
			}
			if (!HeadReference || HeadReference == null)
			{
				return;
			}
			if (EyeLids == null)
			{
				EyeLids = new List<Transform>();
			}
			if (DownEyelids == null)
			{
				DownEyelids = new List<Transform>();
			}
			if (UpEyelids == null)
			{
				UpEyelids = new List<Transform>();
			}
			for (int j = 0; j < Eyes.Count; j++)
			{
				componentsInChildren = Eyes[j].GetComponentsInChildren<Transform>();
				for (int k = 0; k < componentsInChildren.Length; k++)
				{
					string text2 = componentsInChildren[k].name.ToLower();
					if (!text2.Contains("lid"))
					{
						continue;
					}
					if (text2.Contains("low") || text2.Contains("down") || text2.Contains("bot"))
					{
						if (!DownEyelids.Contains(componentsInChildren[k]))
						{
							DownEyelids.Add(componentsInChildren[k]);
						}
					}
					else if (text2.Contains("up") || text2.Contains("top"))
					{
						if (!UpEyelids.Contains(componentsInChildren[k]))
						{
							UpEyelids.Add(componentsInChildren[k]);
						}
					}
					else if (!EyeLids.Contains(componentsInChildren[k]))
					{
						EyeLids.Add(componentsInChildren[k]);
					}
				}
				UpdateLists();
			}
		}

		protected virtual void Start()
		{
			UpdateLists();
			eyesData = new Eye[Eyes.Count];
			AdjustCount(EyeSetups, Eyes.Count);
			for (int i = 0; i < eyesData.Length; i++)
			{
				eyesData[i] = new Eye();
				Eye obj = eyesData[i];
				Vector3 position = Eyes[i].position;
				Vector3 position2 = Eyes[i].position + Vector3.Scale(BaseTransform.forward, Eyes[i].transform.lossyScale);
				obj.forward = (Eyes[i].InverseTransformPoint(position2) - Eyes[i].InverseTransformPoint(position)).normalized;
				position2 = Eyes[i].position + Vector3.Scale(BaseTransform.up, Eyes[i].transform.lossyScale);
				obj.up = (Eyes[i].InverseTransformPoint(position2) - Eyes[i].InverseTransformPoint(position)).normalized;
				position2 = Eyes[i].position + Vector3.Scale(BaseTransform.right, Eyes[i].transform.lossyScale);
				obj.right = (Eyes[i].InverseTransformPoint(position2) - Eyes[i].InverseTransformPoint(position)).normalized;
				obj.initLocalRotation = Eyes[i].localRotation;
				obj.lerpRotation = Eyes[i].rotation;
				obj.randomTimer = 0f;
				obj.randomDir = Vector3.zero;
				obj.lagTimer = 0f;
				obj.changeSmoother = 1f;
				obj.lagProgress = 1f;
				eyesData[i].lerpRotation = Eyes[i].parent.rotation * eyesData[i].initLocalRotation;
				eyesData[i].lagStartRotation = Eyes[i].parent.rotation * eyesData[i].initLocalRotation;
				obj.PrepareMappingFactor(this, Eyes[i]);
			}
			headForward = HeadReference.InverseTransformDirection(BaseTransform.forward);
			headForwardFromTo = Quaternion.FromToRotation(headForward, Vector3.forward) * Vector3.forward;
			headReferenceLookForward = Quaternion.Inverse(HeadReference.rotation) * BaseTransform.rotation * Forward;
			headReferenceUp = Quaternion.Inverse(HeadReference.rotation) * BaseTransform.rotation * Up;
			headUp = HeadReference.transform.InverseTransformDirection(BaseTransform.up);
			OutOfDistance = true;
			OutOfRange = true;
			OptimizationCameraReference = null;
			SetupBlinking();
			StartLookAnim();
		}

		private void Update()
		{
			optimizeUpdate = false;
			if ((bool)OptimizeWithMesh && !OptimizeWithMesh.isVisible)
			{
				optimizeUpdate = true;
				return;
			}
			if (OptimizeAtDistance > 0f)
			{
				Transform optimizationCameraReference = OptimizationCameraReference;
				if (optimizationCameraReference == null && (bool)Camera.main)
				{
					optimizationCameraReference = Camera.main.transform;
				}
				if (optimizationCameraReference != null && Vector3.Distance(optimizationCameraReference.position, HeadReference.position) > OptimizeAtDistance)
				{
					optimizeUpdate = true;
					return;
				}
			}
			if (EyesAnimatorAmount <= 0f)
			{
				optimizeUpdate = true;
				return;
			}
			if (EyesRandomMovement <= 0f && FollowTargetAmount <= 0f)
			{
				optimizeUpdate = true;
				return;
			}
			if (WorldUpIsBaseTransformUp)
			{
				WorldUp = BaseTransform.up;
			}
			for (int i = 0; i < Eyes.Count; i++)
			{
				if (EyeSetups[i].ControlType != EyeSetup.EEyeControlType.Blendshape)
				{
					Eyes[i].localRotation = eyesData[i].initLocalRotation;
				}
			}
		}

		protected virtual void LateUpdate()
		{
			if (optimizeUpdate)
			{
				return;
			}
			IsClamping = false;
			Quaternion lookRotationBase = Quaternion.identity;
			if ((bool)EyesTarget)
			{
				targetLookPosition = EyesTarget.position + targetLookPositionOffset;
			}
			else
			{
				targetLookPosition = HeadReference.position + BaseTransform.forward * 10f;
			}
			ComputeBaseRotations();
			if (UseBlinking && BlinkingBlend > 0f)
			{
				UpdateBlinking();
			}
			headUpDynamic = HeadReference.transform.TransformDirection(headUp);
			for (int i = 0; i < Eyes.Count; i++)
			{
				int num = 0;
				int num2 = 0;
				if (i == 0)
				{
					eyesData[i].changeSmoother = Mathf.Lerp(eyesData[i].changeSmoother, 1f, Time.deltaTime * 1f);
					ComputeLookingRotation(ref lookRotationBase, EyeSetups[i]);
					CalculateLagTimer(0);
					CalculateRandomTimer(0);
				}
				else
				{
					if (EyesRandomMovementIndividual)
					{
						eyesData[i].changeSmoother = Mathf.Lerp(eyesData[i].changeSmoother, 1f, Time.deltaTime * 1f);
						ComputeLookingRotation(ref lookRotationBase, EyeSetups[i], i, num);
						CalculateRandomTimer(i);
						num2 = i;
					}
					else
					{
						if (IndividualClamping)
						{
							ComputeLookingRotation(ref lookRotationBase, EyeSetups[i], 0, num);
						}
						eyesData[i].changeSmoother = eyesData[0].changeSmoother;
					}
					if (IndividualLags)
					{
						num = i;
						CalculateLagTimer(i);
					}
					else if (IsClamping)
					{
						eyesData[i].SetLagStartRotation(BaseTransform, eyesData[i].lerpRotation);
					}
					else
					{
						CalculateLagTimerNonIndividualEvent(i);
					}
				}
				EyeSetup eyeSetup = EyeSetups[i];
				if (eyeSetup.ControlType == EyeSetup.EEyeControlType.Blendshape)
				{
					if (eyeSetup.BlendshapeMesh == null)
					{
						continue;
					}
					blendshapeAnglesRequest = true;
					Vector3 euler = LookDeltaAnglesClamped;
					if (eyesData[num2].randomDir != Vector3.zero)
					{
						euler += Vector3.LerpUnclamped(Vector3.zero, eyesData[num2].randomDir, EyesRandomMovement);
						if (euler.x < EyesClampVertical.x)
						{
							euler.x = EyesClampVertical.x;
						}
						else if (euler.x > EyesClampVertical.y)
						{
							euler.x = EyesClampVertical.y;
						}
						if (euler.y < EyesClampHorizontal.x)
						{
							euler.y = EyesClampHorizontal.x;
						}
						else if (euler.y > EyesClampHorizontal.y)
						{
							euler.y = EyesClampHorizontal.y;
						}
					}
					Vector3 vector = Quaternion.Euler(euler) * Vector3.forward;
					eyeSetup.EyeLeftX(vector.x);
					eyeSetup.EyeRightX(vector.x);
					eyeSetup.EyeUpY(vector.y);
					eyeSetup.EyeDownY(vector.y);
				}
				Quaternion rotation = Eyes[i].rotation;
				eyesData[i].previousLookRotBase = lookRotationBase;
				Quaternion quaternion = lookRotationBase;
				if (EyesLagAmount > 0f && eyesData[num].lagProgress > 0f)
				{
					quaternion = Quaternion.Slerp(quaternion, eyesData[i].GetLagStartRotation(BaseTransform), eyesData[num].lagProgress * EyesLagAmount);
				}
				quaternion = Quaternion.LookRotation(quaternion * Forward, headUpDynamic) * eyesData[i].mapping;
				Quaternion b = quaternion;
				if (SquintPreventer < 1f)
				{
					Vector3 lookRotation = Quaternion.LookRotation(targetLookPosition - Eyes[i].position, WorldUp).eulerAngles;
					if (eyesData[num2].randomDir != Vector3.zero)
					{
						lookRotation += Vector3.LerpUnclamped(Vector3.zero, eyesData[num2].randomDir, EyesRandomMovement);
					}
					Vector2 deltaVector = new Vector3(Mathf.DeltaAngle(lookRotation.x, HeadRotation.x), Mathf.DeltaAngle(lookRotation.y, HeadRotation.y));
					if (!IndividualClamping)
					{
						ClampDetection(deltaVector, ref lookRotation, HeadRotation, EyesClampHorizontal, EyesClampVertical);
					}
					else
					{
						ClampDetection(deltaVector, ref lookRotation, HeadRotation, EyeSetups[i].IndividualClampingHorizontal, EyeSetups[i].IndividualClampingVertical);
					}
					b = Quaternion.SlerpUnclamped(Quaternion.LookRotation(Quaternion.Euler(lookRotation) * Vector3.forward, headUpDynamic) * eyesData[i].mapping, quaternion, SquintPreventer);
				}
				if (CorrectionOffsets[i] != Vector3.zero)
				{
					b *= Quaternion.Euler(CorrectionOffsets[i]);
				}
				eyesSpeedValue = Mathf.LerpUnclamped(2f, 60f, EyesSpeed);
				float num3 = Time.deltaTime * eyesSpeedValue * Mathf.Lerp(1f, eyesData[i].changeSmoother, EyesRandomMovement);
				if (num3 > 1f)
				{
					num3 = 1f;
				}
				if (EyeSetups[i].ControlType == EyeSetup.EEyeControlType.RotateBone)
				{
					eyesData[i].lerpRotation = Quaternion.SlerpUnclamped(eyesData[i].lerpRotation, b, num3);
					if (GetUseAmountWeight() >= 1f)
					{
						Eyes[i].rotation = eyesData[i].lerpRotation;
					}
					else
					{
						Eyes[i].rotation = Quaternion.SlerpUnclamped(rotation, eyesData[i].lerpRotation, GetUseAmountWeight());
					}
				}
			}
			changeFlag = false;
			if (UseBlinking)
			{
				UpdateBlinking();
			}
			UpdateLookAnim();
		}

		private float GetUseAmountWeight()
		{
			return EyesAnimatorAmount;
		}

		public void SetEyesTarget(Transform target, Vector3? offset = null)
		{
			EyesTarget = target;
			if (offset.HasValue)
			{
				targetLookPositionOffset = offset.Value;
			}
			else
			{
				targetLookPositionOffset = Vector3.zero;
			}
		}

		public void BlendOutEyesAnimation(float timeInSeconds = 0.5f)
		{
			StopAllCoroutines();
			StartCoroutine(BlendInOut(0f, timeInSeconds));
		}

		public void BlendInEyesAnimation(float timeInSeconds = 0.5f)
		{
			StopAllCoroutines();
			StartCoroutine(BlendInOut(1f, timeInSeconds));
		}

		private IEnumerator BlendInOut(float blendTo, float time)
		{
			float elapsed = 0f;
			float startVal = EyesAnimatorAmount;
			while (elapsed < time)
			{
				elapsed += Time.deltaTime;
				EyesAnimatorAmount = Mathf.Lerp(startVal, blendTo, elapsed / time);
				yield return null;
			}
			EyesAnimatorAmount = blendTo;
		}

		protected virtual void OnValidate()
		{
		}

		public virtual void UpdateLists()
		{
			if (Eyes == null)
			{
				Eyes = new List<Transform>();
			}
			if (CorrectionOffsets == null)
			{
				CorrectionOffsets = new List<Vector3>();
			}
			if (Eyes.Count != CorrectionOffsets.Count)
			{
				if (CorrectionOffsets.Count > Eyes.Count)
				{
					for (int i = 0; i < CorrectionOffsets.Count - Eyes.Count; i++)
					{
						CorrectionOffsets.RemoveAt(CorrectionOffsets.Count - 1);
					}
				}
				else
				{
					while (CorrectionOffsets.Count < Eyes.Count)
					{
						CorrectionOffsets.Add(Vector3.zero);
					}
				}
			}
			if (EyeSetups == null)
			{
				EyeSetups = new List<EyeSetup>();
			}
			AdjustCount(EyeSetups, Eyes.Count);
			if (EyeLids == null)
			{
				EyeLids = new List<Transform>();
			}
			if (UpEyelids != null)
			{
				for (int j = 0; j < UpEyelids.Count; j++)
				{
					if (!EyeLids.Contains(UpEyelids[j]))
					{
						EyeLids.Add(UpEyelids[j]);
					}
				}
			}
			if (DownEyelids != null)
			{
				for (int k = 0; k < DownEyelids.Count; k++)
				{
					if (!EyeLids.Contains(DownEyelids[k]))
					{
						EyeLids.Add(DownEyelids[k]);
					}
				}
			}
			if (EyeLidsCloseRotations == null)
			{
				EyeLidsCloseRotations = new List<Vector3>();
			}
			if (EyeLids.Count != EyeLidsCloseRotations.Count)
			{
				EyeLidsCloseRotations.Clear();
				for (int l = 0; l < EyeLids.Count; l++)
				{
					if (!(EyeLids[l] == null))
					{
						EyeLidsCloseRotations.Add(EyeLids[l].localRotation.eulerAngles);
					}
				}
			}
			if (EyeLidsClosePositions == null)
			{
				EyeLidsClosePositions = new List<Vector3>();
			}
			if (EyeLids.Count != EyeLidsClosePositions.Count)
			{
				EyeLidsClosePositions.Clear();
				for (int m = 0; m < EyeLids.Count; m++)
				{
					if (!(EyeLids[m] == null))
					{
						EyeLidsClosePositions.Add(Vector3.zero);
					}
				}
			}
			if (EyeLidsCloseScales == null)
			{
				EyeLidsCloseScales = new List<Vector3>();
			}
			if (EyeLids.Count != EyeLidsCloseScales.Count)
			{
				EyeLidsCloseScales.Clear();
				for (int n = 0; n < EyeLids.Count; n++)
				{
					if (!(EyeLids[n] == null))
					{
						EyeLidsCloseScales.Add(EyeLids[n].localScale);
					}
				}
			}
			if (UpEyelidsBlendShapes == null)
			{
				UpEyelidsBlendShapes = new List<EyesAnimator_BlenshapesInfo>();
			}
			if (DownEyelidsBlendShapes == null)
			{
				DownEyelidsBlendShapes = new List<EyesAnimator_BlenshapesInfo>();
			}
		}

		private void StartLookAnim()
		{
		}

		private void UpdateLookAnim()
		{
		}

		public static void AdjustCount<T>(List<T> list, int targetCount, bool addNulls = false) where T : class, new()
		{
			if (list.Count == targetCount)
			{
				return;
			}
			if (list.Count < targetCount)
			{
				if (addNulls)
				{
					while (list.Count < targetCount)
					{
						list.Add(null);
					}
				}
				else
				{
					while (list.Count < targetCount)
					{
						list.Add(new T());
					}
				}
			}
			else
			{
				while (list.Count > targetCount)
				{
					list.RemoveAt(list.Count - 1);
				}
			}
		}
	}
}
