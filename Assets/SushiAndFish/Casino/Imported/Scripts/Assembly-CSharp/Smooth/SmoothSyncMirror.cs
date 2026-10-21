namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;
using global::UnityEngine.SceneManagement;

namespace Smooth
{
	public class SmoothSyncMirror : NetworkBehaviour
	{
		public enum ExtrapolationMode
		{
			None = 0,
			Limited = 1,
			Unlimited = 2
		}

		public enum TransformSource
		{
			Owner = 0,
			Server = 1
		}

		public enum WhenToUpdateTransform
		{
			Update = 0,
			FixedUpdate = 1
		}

		public delegate bool validateStateDelegate(StateMirror receivedState, StateMirror latestVerifiedState);

		private enum RestState
		{
			AT_REST = 0,
			JUST_STARTED_MOVING = 1,
			MOVING = 2
		}

		public float interpolationBackTime = 0.1f;

		public ExtrapolationMode extrapolationMode = ExtrapolationMode.Limited;

		public bool useExtrapolationTimeLimit = true;

		public float extrapolationTimeLimit = 5f;

		public bool useExtrapolationDistanceLimit;

		public float extrapolationDistanceLimit = 20f;

		public float sendPositionThreshold;

		public float sendRotationThreshold;

		public float sendScaleThreshold;

		public float sendVelocityThreshold;

		public float sendAngularVelocityThreshold;

		public float receivedPositionThreshold;

		public float receivedRotationThreshold;

		public float snapPositionThreshold;

		public float snapRotationThreshold;

		public float snapScaleThreshold;

		[Range(0f, 1f)]
		public float positionLerpSpeed = 0.85f;

		[Range(0f, 1f)]
		public float rotationLerpSpeed = 0.85f;

		[Range(0f, 1f)]
		public float scaleLerpSpeed = 0.85f;

		[Range(0f, 5f)]
		public float timeCorrectionSpeed = 0.1f;

		public float snapTimeThreshold = 0.3f;

		public SyncMode syncPosition;

		public SyncMode syncRotation;

		public SyncMode syncScale;

		public SyncMode syncVelocity;

		public SyncMode syncAngularVelocity;

		public bool isPositionCompressed;

		public bool isRotationCompressed;

		public bool isScaleCompressed;

		public bool isVelocityCompressed;

		public bool isAngularVelocityCompressed;

		public bool automaticallyResetTime = true;

		private const int maxTimePower = 12;

		private readonly float maxLocalTime = Mathf.Pow(2f, 12f);

		private readonly float minTimePrecision = Mathf.Pow(2f, -12f);

		[NonSerialized]
		public int localTimeResetIndicator;

		public bool isSmoothingAuthorityChanges;

		public TransformSource transformSource;

		public WhenToUpdateTransform whenToUpdateTransform;

		public float sendRate = 30f;

		public int networkChannel = 1;

		public GameObject childObjectToSync;

		[NonSerialized]
		public bool isSyncingChild;

		[NonSerialized]
		public validateStateDelegate validateStateMethod = validateState;

		private StateMirror latestValidatedState;

		public bool setVelocityInsteadOfPositionOnNonOwners;

		public float maxPositionDifferenceForVelocitySyncing = 10f;

		public bool useLocalTransformOnly;

		[NonSerialized]
		public StateMirror[] stateBuffer;

		[NonSerialized]
		public int stateCount;

		[NonSerialized]
		public Rigidbody rb;

		[NonSerialized]
		public bool hasRigidbody;

		[NonSerialized]
		public Rigidbody2D rb2D;

		[NonSerialized]
		public bool hasRigidbody2D;

		private bool dontEasePosition;

		private bool dontEaseScale;

		private bool dontEaseRotation;

		private float firstReceivedMessageZeroTime;

		[NonSerialized]
		public float lastTimeStateWasSent;

		[NonSerialized]
		public Vector3 lastPositionWhenStateWasSent;

		[NonSerialized]
		public Quaternion lastRotationWhenStateWasSent = Quaternion.identity;

		[NonSerialized]
		public Vector3 lastScaleWhenStateWasSent;

		[NonSerialized]
		public Vector3 lastVelocityWhenStateWasSent;

		[NonSerialized]
		public Vector3 lastAngularVelocityWhenStateWasSent;

		[NonSerialized]
		public NetworkIdentity netID;

		[NonSerialized]
		public GameObject realObjectToSync;

		[NonSerialized]
		public int syncIndex;

		[NonSerialized]
		public SmoothSyncMirror[] childObjectSmoothSyncs = new SmoothSyncMirror[0];

		[NonSerialized]
		public bool forceStateSend;

		[NonSerialized]
		public bool sendAtPositionalRestMessage;

		[NonSerialized]
		public bool sendAtRotationalRestMessage;

		[NonSerialized]
		public bool sendPosition;

		[NonSerialized]
		public bool sendRotation;

		[NonSerialized]
		public bool sendScale;

		[NonSerialized]
		public bool sendVelocity;

		[NonSerialized]
		public bool sendAngularVelocity;

		private StateMirror targetTempState;

		private NetworkStateMirror sendingTempState;

		[NonSerialized]
		public Vector3 latestReceivedVelocity;

		[NonSerialized]
		public Vector3 latestReceivedAngularVelocity;

		private float timeSpentExtrapolating;

		private bool extrapolatedLastFrame;

		private Vector3 positionLastFrame;

		private bool changedPositionLastFrame;

		private Quaternion rotationLastFrame;

		private bool changedRotationLastFrame;

		private int atRestThresholdCount = 3;

		private int samePositionCount;

		private int sameRotationCount;

		private RestState restStatePosition = RestState.MOVING;

		private RestState restStateRotation = RestState.MOVING;

		private bool hadAuthorityLastFrame;

		private StateMirror latestEndStateUsed;

		private Vector3 latestTeleportedFromPosition;

		private Quaternion latestTeleportedFromRotation;

		private bool hasCachedNetID;

		private NetworkIdentity cachedNetIdentity;

		private bool triedToExtrapolateTooFar;

		private float _ownerTime;

		private float lastTimeOwnerTimeWasSet;

		private float latestAuthorityChangeZeroTime;

		private int previousReceivedOwnerInt = 1;

		public int ownerChangeIndicator = 1;

		public int receivedStatesCounter;

		public float localTime { get; private set; }

		public new NetworkIdentity netIdentity
		{
			get
			{
				if (!hasCachedNetID)
				{
					cachedNetIdentity = GetComponent<NetworkIdentity>();
					hasCachedNetID = true;
				}
				return cachedNetIdentity;
			}
		}

		public bool hasAuthorityOrUnownedOnServer
		{
			get
			{
				if (!netIdentity.isOwned)
				{
					if (NetworkServer.active)
					{
						return netIdentity.connectionToClient == null;
					}
					return false;
				}
				return true;
			}
		}

		public bool hasControl
		{
			get
			{
				if ((transformSource == TransformSource.Owner && hasAuthorityOrUnownedOnServer) || (transformSource == TransformSource.Server && NetworkServer.active))
				{
					return true;
				}
				return false;
			}
		}

		public bool isSyncingXPosition
		{
			get
			{
				if (syncPosition != SyncMode.XYZ && syncPosition != SyncMode.XY && syncPosition != SyncMode.XZ)
				{
					return syncPosition == SyncMode.X;
				}
				return true;
			}
		}

		public bool isSyncingYPosition
		{
			get
			{
				if (syncPosition != SyncMode.XYZ && syncPosition != SyncMode.XY && syncPosition != SyncMode.YZ)
				{
					return syncPosition == SyncMode.Y;
				}
				return true;
			}
		}

		public bool isSyncingZPosition
		{
			get
			{
				if (syncPosition != SyncMode.XYZ && syncPosition != SyncMode.XZ && syncPosition != SyncMode.YZ)
				{
					return syncPosition == SyncMode.Z;
				}
				return true;
			}
		}

		public bool isSyncingXRotation
		{
			get
			{
				if (syncRotation != SyncMode.XYZ && syncRotation != SyncMode.XY && syncRotation != SyncMode.XZ)
				{
					return syncRotation == SyncMode.X;
				}
				return true;
			}
		}

		public bool isSyncingYRotation
		{
			get
			{
				if (syncRotation != SyncMode.XYZ && syncRotation != SyncMode.XY && syncRotation != SyncMode.YZ)
				{
					return syncRotation == SyncMode.Y;
				}
				return true;
			}
		}

		public bool isSyncingZRotation
		{
			get
			{
				if (syncRotation != SyncMode.XYZ && syncRotation != SyncMode.XZ && syncRotation != SyncMode.YZ)
				{
					return syncRotation == SyncMode.Z;
				}
				return true;
			}
		}

		public bool isSyncingXScale
		{
			get
			{
				if (syncScale != SyncMode.XYZ && syncScale != SyncMode.XY && syncScale != SyncMode.XZ)
				{
					return syncScale == SyncMode.X;
				}
				return true;
			}
		}

		public bool isSyncingYScale
		{
			get
			{
				if (syncScale != SyncMode.XYZ && syncScale != SyncMode.XY && syncScale != SyncMode.YZ)
				{
					return syncScale == SyncMode.Y;
				}
				return true;
			}
		}

		public bool isSyncingZScale
		{
			get
			{
				if (syncScale != SyncMode.XYZ && syncScale != SyncMode.XZ && syncScale != SyncMode.YZ)
				{
					return syncScale == SyncMode.Z;
				}
				return true;
			}
		}

		public bool isSyncingXVelocity
		{
			get
			{
				if (syncVelocity != SyncMode.XYZ && syncVelocity != SyncMode.XY && syncVelocity != SyncMode.XZ)
				{
					return syncVelocity == SyncMode.X;
				}
				return true;
			}
		}

		public bool isSyncingYVelocity
		{
			get
			{
				if (syncVelocity != SyncMode.XYZ && syncVelocity != SyncMode.XY && syncVelocity != SyncMode.YZ)
				{
					return syncVelocity == SyncMode.Y;
				}
				return true;
			}
		}

		public bool isSyncingZVelocity
		{
			get
			{
				if (syncVelocity != SyncMode.XYZ && syncVelocity != SyncMode.XZ && syncVelocity != SyncMode.YZ)
				{
					return syncVelocity == SyncMode.Z;
				}
				return true;
			}
		}

		public bool isSyncingXAngularVelocity
		{
			get
			{
				if (syncAngularVelocity != SyncMode.XYZ && syncAngularVelocity != SyncMode.XY && syncAngularVelocity != SyncMode.XZ)
				{
					return syncAngularVelocity == SyncMode.X;
				}
				return true;
			}
		}

		public bool isSyncingYAngularVelocity
		{
			get
			{
				if (syncAngularVelocity != SyncMode.XYZ && syncAngularVelocity != SyncMode.XY && syncAngularVelocity != SyncMode.YZ)
				{
					return syncAngularVelocity == SyncMode.Y;
				}
				return true;
			}
		}

		public bool isSyncingZAngularVelocity
		{
			get
			{
				if (syncAngularVelocity != SyncMode.XYZ && syncAngularVelocity != SyncMode.XZ && syncAngularVelocity != SyncMode.YZ)
				{
					return syncAngularVelocity == SyncMode.Z;
				}
				return true;
			}
		}

		public float approximateNetworkTimeOnOwner
		{
			get
			{
				return _ownerTime + (localTime - lastTimeOwnerTimeWasSet);
			}
			set
			{
				_ownerTime = value;
				lastTimeOwnerTimeWasSet = localTime;
			}
		}

		public static bool validateState(StateMirror latestReceivedState, StateMirror latestValidatedState)
		{
			return true;
		}

		public void Awake()
		{
			int a = ((int)(sendRate * interpolationBackTime) + 1) * 2;
			stateBuffer = new StateMirror[Mathf.Max(a, 30)];
			SetObjectToSync(childObjectToSync);
			if (extrapolationMode == ExtrapolationMode.Unlimited)
			{
				useExtrapolationDistanceLimit = false;
				useExtrapolationTimeLimit = false;
			}
			targetTempState = new StateMirror();
			sendingTempState = default;
			sendingTempState.state = new StateMirror();
			NetworkIdentity.clientAuthorityCallback += AssignAuthorityCallback;
		}

		public void OnDestroy()
		{
			NetworkIdentity.clientAuthorityCallback -= AssignAuthorityCallback;
		}

		public void SetObjectToSync(GameObject childObjectToSync)
		{
			this.childObjectToSync = childObjectToSync;
			if ((bool)childObjectToSync)
			{
				realObjectToSync = childObjectToSync;
				isSyncingChild = true;
				bool flag = false;
				childObjectSmoothSyncs = GetComponents<SmoothSyncMirror>();
				for (int i = 0; i < childObjectSmoothSyncs.Length; i++)
				{
					if (!childObjectSmoothSyncs[i].childObjectToSync)
					{
						flag = true;
					}
				}
				if (!flag)
				{
					Debug.LogError("You must have one SmoothSyncMirror script with unassigned childObjectToSync in order to sync the parent object");
				}
			}
			else
			{
				realObjectToSync = base.gameObject;
				childObjectSmoothSyncs = GetComponents<SmoothSyncMirror>();
				for (int j = 0; j < childObjectSmoothSyncs.Length && !(childObjectSmoothSyncs[j] == this); j++)
				{
					if (childObjectSmoothSyncs[j].childObjectToSync == null)
					{
						Debug.LogWarning("More than one SmoothSync instance with no childObjectToSync on " + base.gameObject?.ToString() + ". Disabling all but one.");
						base.enabled = false;
						return;
					}
				}
				int num = 0;
				for (int k = 0; k < childObjectSmoothSyncs.Length; k++)
				{
					childObjectSmoothSyncs[k].syncIndex = num;
					num++;
				}
			}
			netID = GetComponent<NetworkIdentity>();
			rb = realObjectToSync.GetComponent<Rigidbody>();
			rb2D = realObjectToSync.GetComponent<Rigidbody2D>();
			if ((bool)rb)
			{
				hasRigidbody = true;
			}
			else if ((bool)rb2D)
			{
				hasRigidbody2D = true;
				if (syncVelocity != SyncMode.NONE)
				{
					syncVelocity = SyncMode.XY;
				}
				if (syncAngularVelocity != SyncMode.NONE)
				{
					syncAngularVelocity = SyncMode.Z;
				}
			}
			if (!rb && !rb2D)
			{
				syncVelocity = SyncMode.NONE;
				syncAngularVelocity = SyncMode.NONE;
			}
		}

		private void Update()
		{
			if (whenToUpdateTransform == WhenToUpdateTransform.Update)
			{
				SmoothSyncUpdate();
			}
			if (isSmoothingAuthorityChanges)
			{
				authorityChangeUpdate();
			}
		}

		private void FixedUpdate()
		{
			if (whenToUpdateTransform == WhenToUpdateTransform.FixedUpdate)
			{
				SmoothSyncUpdate();
			}
			sendState();
			positionLastFrame = getPosition();
			rotationLastFrame = getRotation();
			resetFlags();
		}

		private void SmoothSyncUpdate()
		{
			localTime += Time.deltaTime;
			if (automaticallyResetTime && localTime > maxLocalTime)
			{
				ResetLocalTime();
			}
			if (!hasControl)
			{
				adjustOwnerTime();
				applyInterpolationOrExtrapolation();
			}
		}

		public void OnEnable()
		{
			SceneManager.sceneLoaded += OnSceneLoaded;
			if (!NetworkServer.active)
			{
				registerClientHandlers();
			}
			clearBuffer();
		}

		public void OnDisable()
		{
			SceneManager.sceneLoaded -= OnSceneLoaded;
		}

		public void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			if (automaticallyResetTime)
			{
				ResetLocalTime();
			}
		}

		public override void OnStartAuthority()
		{
			base.OnStartAuthority();
			teleportOwnedObjectFromOwner();
		}

		public void ResetLocalTime()
		{
			localTimeResetIndicator++;
			if (localTimeResetIndicator >= 128)
			{
				localTimeResetIndicator = 0;
			}
			lastTimeStateWasSent -= localTime;
			lastTimeOwnerTimeWasSet -= localTime;
			latestAuthorityChangeZeroTime -= localTime;
			for (int i = 0; i < stateCount; i++)
			{
				stateBuffer[i].receivedTimestamp -= localTime;
			}
			localTime = 0f;
			forceStateSendNextFixedUpdate();
		}

		public void OnRemoteTimeReset()
		{
			approximateNetworkTimeOnOwner -= maxLocalTime;
			targetTempState.ownerTimestamp -= maxLocalTime;
			for (int num = stateCount - 1; num >= 0; num--)
			{
				stateBuffer[num].ownerTimestamp -= maxLocalTime;
			}
		}

		private void sendState()
		{
			if ((NetworkServer.active && (netIdentity.observers == null || netIdentity.observers.Count == 0 || (netIdentity.observers.Count == 1 && NetworkServer.localConnection != null && netIdentity.observers.ContainsKey(NetworkServer.localConnection.connectionId)))) || !hasControl || (!NetworkServer.active && !NetworkClient.ready) || sendRate == 0f)
			{
				return;
			}
			if (syncPosition != SyncMode.NONE)
			{
				if (positionLastFrame == getPosition())
				{
					if (restStatePosition != RestState.AT_REST)
					{
						samePositionCount++;
					}
					if (samePositionCount == atRestThresholdCount)
					{
						samePositionCount = 0;
						restStatePosition = RestState.AT_REST;
						forceStateSendNextFixedUpdate();
					}
				}
				else if (restStatePosition == RestState.AT_REST && getPosition() != latestTeleportedFromPosition)
				{
					restStatePosition = RestState.JUST_STARTED_MOVING;
					forceStateSendNextFixedUpdate();
				}
				else if (restStatePosition == RestState.JUST_STARTED_MOVING)
				{
					restStatePosition = RestState.MOVING;
				}
				else
				{
					samePositionCount = 0;
				}
			}
			else
			{
				restStatePosition = RestState.AT_REST;
			}
			if (syncRotation != SyncMode.NONE)
			{
				if (rotationLastFrame == getRotation())
				{
					if (restStateRotation != RestState.AT_REST)
					{
						sameRotationCount++;
					}
					if (sameRotationCount == atRestThresholdCount)
					{
						sameRotationCount = 0;
						restStateRotation = RestState.AT_REST;
						forceStateSendNextFixedUpdate();
					}
				}
				else if (restStateRotation == RestState.AT_REST && getRotation() != latestTeleportedFromRotation)
				{
					restStateRotation = RestState.JUST_STARTED_MOVING;
					forceStateSendNextFixedUpdate();
				}
				else if (restStateRotation == RestState.JUST_STARTED_MOVING)
				{
					restStateRotation = RestState.MOVING;
				}
				else
				{
					sameRotationCount = 0;
				}
			}
			else
			{
				restStateRotation = RestState.AT_REST;
			}
			if (localTime - lastTimeStateWasSent < GetNetworkSendInterval() && !forceStateSend)
			{
				return;
			}
			sendPosition = shouldSendPosition();
			sendRotation = shouldSendRotation();
			sendScale = shouldSendScale();
			sendVelocity = shouldSendVelocity();
			sendAngularVelocity = shouldSendAngularVelocity();
			if (!sendPosition && !sendRotation && !sendScale && !sendVelocity && !sendAngularVelocity)
			{
				return;
			}
			sendingTempState.copyFromSmoothSync(this);
			if (restStatePosition == RestState.AT_REST)
			{
				sendAtPositionalRestMessage = true;
			}
			if (restStateRotation == RestState.AT_REST)
			{
				sendAtRotationalRestMessage = true;
			}
			if (restStatePosition == RestState.JUST_STARTED_MOVING)
			{
				sendingTempState.state.position = lastPositionWhenStateWasSent;
			}
			if (restStateRotation == RestState.JUST_STARTED_MOVING)
			{
				sendingTempState.state.rotation = lastRotationWhenStateWasSent;
			}
			if (restStatePosition == RestState.JUST_STARTED_MOVING || restStateRotation == RestState.JUST_STARTED_MOVING)
			{
				sendingTempState.state.ownerTimestamp = localTime - Time.deltaTime;
				if (restStatePosition != RestState.JUST_STARTED_MOVING)
				{
					sendingTempState.state.position = positionLastFrame;
				}
				if (restStateRotation != RestState.JUST_STARTED_MOVING)
				{
					sendingTempState.state.rotation = rotationLastFrame;
				}
			}
			lastTimeStateWasSent = localTime;
			if (NetworkServer.active)
			{
				SendStateToNonOwners(sendingTempState);
				if (sendPosition)
				{
					lastPositionWhenStateWasSent = sendingTempState.state.position;
				}
				if (sendRotation)
				{
					lastRotationWhenStateWasSent = sendingTempState.state.rotation;
				}
				if (sendScale)
				{
					lastScaleWhenStateWasSent = sendingTempState.state.scale;
				}
				if (sendVelocity)
				{
					lastVelocityWhenStateWasSent = sendingTempState.state.velocity;
				}
				if (sendAngularVelocity)
				{
					lastAngularVelocityWhenStateWasSent = sendingTempState.state.angularVelocity;
				}
			}
			else if (NetworkClient.active)
			{
				NetworkClient.Send(sendingTempState, networkChannel);
			}
		}

		private void authorityChangeUpdate()
		{
			if (hasAuthorityOrUnownedOnServer && !hadAuthorityLastFrame && stateBuffer[0] != null)
			{
				if (hasRigidbody)
				{
					rb.linearVelocity = stateBuffer[0].velocity;
					rb.angularVelocity = stateBuffer[0].angularVelocity * (MathF.PI / 180f);
				}
				else if (hasRigidbody2D)
				{
					rb2D.linearVelocity = stateBuffer[0].velocity;
					rb2D.angularVelocity = stateBuffer[0].angularVelocity.z * (MathF.PI / 180f);
				}
				clearBuffer();
			}
			hadAuthorityLastFrame = hasAuthorityOrUnownedOnServer;
		}

		private void applyInterpolationOrExtrapolation()
		{
			if (stateCount == 0)
			{
				return;
			}
			if (!extrapolatedLastFrame)
			{
				targetTempState.resetTheVariables();
			}
			triedToExtrapolateTooFar = false;
			float num = approximateNetworkTimeOnOwner - interpolationBackTime;
			if (stateCount > 1 && stateBuffer[0].ownerTimestamp > num)
			{
				interpolate(num);
				extrapolatedLastFrame = false;
			}
			else if (stateBuffer[0].atPositionalRest && stateBuffer[0].atRotationalRest)
			{
				targetTempState.copyFromState(stateBuffer[0]);
				extrapolatedLastFrame = false;
				if (setVelocityInsteadOfPositionOnNonOwners)
				{
					triedToExtrapolateTooFar = true;
				}
			}
			else
			{
				if ((!isSmoothingAuthorityChanges || !(localTime - latestAuthorityChangeZeroTime > interpolationBackTime * 2f)) && isSmoothingAuthorityChanges)
				{
					return;
				}
				bool flag = extrapolate(num);
				extrapolatedLastFrame = true;
				triedToExtrapolateTooFar = !flag;
				if (setVelocityInsteadOfPositionOnNonOwners)
				{
					float num2 = num - stateBuffer[0].ownerTimestamp;
					targetTempState.velocity = stateBuffer[0].velocity;
					targetTempState.position = stateBuffer[0].position + targetTempState.velocity * num2;
					Vector3 vector = base.transform.position + targetTempState.velocity * Time.deltaTime;
					float t = (targetTempState.position - vector).sqrMagnitude / (maxPositionDifferenceForVelocitySyncing * maxPositionDifferenceForVelocitySyncing);
					targetTempState.velocity = Vector3.Lerp(targetTempState.velocity, (targetTempState.position - base.transform.position) / Time.deltaTime, t);
				}
			}
			float t2 = positionLerpSpeed;
			float t3 = rotationLerpSpeed;
			float t4 = scaleLerpSpeed;
			bool flag2 = false;
			bool isTeleporting = false;
			if (dontEasePosition)
			{
				t2 = 1f;
				flag2 = true;
				dontEasePosition = false;
			}
			if (dontEaseRotation)
			{
				t3 = 1f;
				isTeleporting = true;
				dontEaseRotation = false;
			}
			if (dontEaseScale)
			{
				t4 = 1f;
				dontEaseScale = false;
			}
			if (!triedToExtrapolateTooFar)
			{
				bool flag3 = false;
				float num3 = 0f;
				if (getPosition() != targetTempState.position && receivedPositionThreshold != 0f)
				{
					num3 = Vector3.Distance(getPosition(), targetTempState.position);
				}
				if (receivedPositionThreshold != 0f)
				{
					if (num3 > receivedPositionThreshold)
					{
						flag3 = true;
					}
				}
				else
				{
					flag3 = true;
				}
				bool flag4 = false;
				float num4 = 0f;
				if (getRotation() != targetTempState.rotation && receivedRotationThreshold != 0f)
				{
					num4 = Quaternion.Angle(getRotation(), targetTempState.rotation);
				}
				if (receivedRotationThreshold != 0f)
				{
					if (num4 > receivedRotationThreshold)
					{
						flag4 = true;
					}
				}
				else
				{
					flag4 = true;
				}
				bool flag5 = false;
				if (getScale() != targetTempState.scale)
				{
					flag5 = true;
				}
				if (syncPosition != SyncMode.NONE && flag3)
				{
					Vector3 position = getPosition();
					if (isSyncingXPosition)
					{
						position.x = targetTempState.position.x;
					}
					if (isSyncingYPosition)
					{
						position.y = targetTempState.position.y;
					}
					if (isSyncingZPosition)
					{
						position.z = targetTempState.position.z;
					}
					if (setVelocityInsteadOfPositionOnNonOwners && !flag2)
					{
						if (hasRigidbody)
						{
							rb.linearVelocity = targetTempState.velocity;
						}
						if (hasRigidbody2D)
						{
							rb2D.linearVelocity = targetTempState.velocity;
						}
					}
					else
					{
						setPosition(Vector3.Lerp(getPosition(), position, t2), flag2);
					}
				}
				if (syncRotation != SyncMode.NONE && flag4)
				{
					Vector3 eulerAngles = getRotation().eulerAngles;
					if (isSyncingXRotation)
					{
						eulerAngles.x = targetTempState.rotation.eulerAngles.x;
					}
					if (isSyncingYRotation)
					{
						eulerAngles.y = targetTempState.rotation.eulerAngles.y;
					}
					if (isSyncingZRotation)
					{
						eulerAngles.z = targetTempState.rotation.eulerAngles.z;
					}
					Quaternion b = Quaternion.Euler(eulerAngles);
					setRotation(Quaternion.Lerp(getRotation(), b, t3), isTeleporting);
				}
				if (syncScale != SyncMode.NONE && flag5)
				{
					Vector3 scale = getScale();
					if (isSyncingXScale)
					{
						scale.x = targetTempState.scale.x;
					}
					if (isSyncingYScale)
					{
						scale.y = targetTempState.scale.y;
					}
					if (isSyncingZScale)
					{
						scale.z = targetTempState.scale.z;
					}
					setScale(Vector3.Lerp(getScale(), scale, t4));
				}
			}
			else if (triedToExtrapolateTooFar)
			{
				if (hasRigidbody)
				{
					rb.linearVelocity = Vector3.zero;
					rb.angularVelocity = Vector3.zero;
				}
				if (hasRigidbody2D)
				{
					rb2D.linearVelocity = Vector2.zero;
					rb2D.angularVelocity = 0f;
				}
			}
		}

		private void interpolate(float interpolationTime)
		{
			int i;
			for (i = 0; i < stateCount && !(stateBuffer[i].ownerTimestamp <= interpolationTime); i++)
			{
			}
			if (i == stateCount)
			{
				i--;
			}
			StateMirror end = stateBuffer[Mathf.Max(i - 1, 0)];
			StateMirror stateMirror = stateBuffer[i];
			float t = (interpolationTime - stateMirror.ownerTimestamp) / (end.ownerTimestamp - stateMirror.ownerTimestamp);
			shouldTeleport(stateMirror, ref end, interpolationTime, ref t);
			targetTempState = StateMirror.Lerp(targetTempState, stateMirror, end, t);
			if (snapPositionThreshold != 0f)
			{
				if ((end.position - stateMirror.position).magnitude > snapPositionThreshold)
				{
					targetTempState.position = end.position;
				}
				dontEasePosition = true;
			}
			if (snapScaleThreshold != 0f)
			{
				if ((end.scale - stateMirror.scale).magnitude > snapScaleThreshold)
				{
					targetTempState.scale = end.scale;
				}
				dontEaseScale = true;
			}
			if (snapRotationThreshold != 0f)
			{
				if (Quaternion.Angle(end.rotation, stateMirror.rotation) > snapRotationThreshold)
				{
					targetTempState.rotation = end.rotation;
				}
				dontEaseRotation = true;
			}
			if (setVelocityInsteadOfPositionOnNonOwners)
			{
				Vector3 vector = base.transform.position + targetTempState.velocity * Time.deltaTime;
				float t2 = (targetTempState.position - vector).sqrMagnitude / (maxPositionDifferenceForVelocitySyncing * maxPositionDifferenceForVelocitySyncing);
				targetTempState.velocity = Vector3.Lerp(targetTempState.velocity, (targetTempState.position - base.transform.position) / Time.deltaTime, t2);
			}
		}

		private bool extrapolate(float interpolationTime)
		{
			if (!extrapolatedLastFrame || targetTempState.ownerTimestamp < stateBuffer[0].ownerTimestamp)
			{
				targetTempState.copyFromState(stateBuffer[0]);
				timeSpentExtrapolating = 0f;
			}
			if (extrapolationMode != ExtrapolationMode.None && stateCount >= 2)
			{
				if (syncVelocity == SyncMode.NONE && !stateBuffer[0].atPositionalRest)
				{
					bool flag = false;
					for (int i = 1; i < stateCount; i++)
					{
						if (stateBuffer[0].ownerTimestamp != stateBuffer[i].ownerTimestamp)
						{
							targetTempState.velocity = (stateBuffer[0].position - stateBuffer[i].position) / (stateBuffer[0].ownerTimestamp - stateBuffer[i].ownerTimestamp);
							flag = true;
							break;
						}
					}
					if (!flag)
					{
						targetTempState.velocity = Vector3.zero;
					}
				}
				if (syncAngularVelocity == SyncMode.NONE && !stateBuffer[0].atRotationalRest)
				{
					bool flag2 = false;
					for (int j = 1; j < stateCount; j++)
					{
						if (stateBuffer[0].ownerTimestamp != stateBuffer[j].ownerTimestamp)
						{
							Quaternion quaternion = stateBuffer[0].rotation * Quaternion.Inverse(stateBuffer[j].rotation);
							Vector3 angularVelocity = new Vector3(Mathf.DeltaAngle(0f, quaternion.eulerAngles.x), Mathf.DeltaAngle(0f, quaternion.eulerAngles.y), Mathf.DeltaAngle(0f, quaternion.eulerAngles.z)) / (stateBuffer[0].ownerTimestamp - stateBuffer[j].ownerTimestamp);
							targetTempState.angularVelocity = angularVelocity;
							flag2 = true;
							break;
						}
					}
					if (!flag2)
					{
						targetTempState.angularVelocity = Vector3.zero;
					}
				}
			}
			if (extrapolationMode == ExtrapolationMode.None)
			{
				return false;
			}
			if (useExtrapolationTimeLimit && timeSpentExtrapolating > extrapolationTimeLimit)
			{
				return false;
			}
			bool flag3 = Mathf.Abs(targetTempState.velocity.x) >= 0.01f || Mathf.Abs(targetTempState.velocity.y) >= 0.01f || Mathf.Abs(targetTempState.velocity.z) >= 0.01f;
			bool flag4 = Mathf.Abs(targetTempState.angularVelocity.x) >= 0.01f || Mathf.Abs(targetTempState.angularVelocity.y) >= 0.01f || Mathf.Abs(targetTempState.angularVelocity.z) >= 0.01f;
			if (!flag3 && !flag4)
			{
				return false;
			}
			float num = 0f;
			num = ((timeSpentExtrapolating != 0f) ? Time.deltaTime : (interpolationTime - targetTempState.ownerTimestamp));
			timeSpentExtrapolating += num;
			if (flag3)
			{
				if (!rb)
				{
					targetTempState.position += targetTempState.velocity * num;
				}
				if (Mathf.Abs(targetTempState.velocity.y) >= 0.01f)
				{
					if (hasRigidbody && rb.useGravity)
					{
						targetTempState.velocity += Physics.gravity * num;
					}
					else if (hasRigidbody2D)
					{
						targetTempState.velocity += Physics.gravity * rb2D.gravityScale * num;
					}
				}
				if (hasRigidbody)
				{
					targetTempState.velocity -= targetTempState.velocity * num * rb.linearDamping;
				}
				else if (hasRigidbody2D)
				{
					targetTempState.velocity -= targetTempState.velocity * num * rb2D.linearDamping;
				}
			}
			if (flag4)
			{
				Quaternion quaternion2 = Quaternion.AngleAxis(num * targetTempState.angularVelocity.magnitude, targetTempState.angularVelocity);
				targetTempState.rotation = quaternion2 * targetTempState.rotation;
				float num2 = 0f;
				if (hasRigidbody)
				{
					num2 = rb.angularDamping;
				}
				if (hasRigidbody2D)
				{
					num2 = rb2D.angularDamping;
				}
				if ((hasRigidbody || hasRigidbody2D) && num2 > 0f)
				{
					targetTempState.angularVelocity -= targetTempState.angularVelocity * num * num2;
				}
			}
			if (useExtrapolationDistanceLimit && Vector3.Distance(stateBuffer[0].position, targetTempState.position) >= extrapolationDistanceLimit)
			{
				return false;
			}
			return true;
		}

		private void shouldTeleport(StateMirror start, ref StateMirror end, float interpolationTime, ref float t)
		{
			if (start.ownerTimestamp > interpolationTime && start.teleport && stateCount == 2)
			{
				end = start;
				t = 1f;
				stopEasing();
			}
			for (int i = 0; i < stateCount; i++)
			{
				if (stateBuffer[i] != latestEndStateUsed || latestEndStateUsed == end || latestEndStateUsed == start)
				{
					continue;
				}
				for (int num = i - 1; num >= 0; num--)
				{
					if (stateBuffer[num].teleport)
					{
						t = 1f;
						stopEasing();
					}
					if (stateBuffer[num] == start)
					{
						break;
					}
				}
				break;
			}
			latestEndStateUsed = end;
			if (end.teleport)
			{
				t = 1f;
				stopEasing();
			}
		}

		public Vector3 getPosition()
		{
			if (isSyncingChild || useLocalTransformOnly)
			{
				return realObjectToSync.transform.localPosition;
			}
			return realObjectToSync.transform.position;
		}

		public Quaternion getRotation()
		{
			if (isSyncingChild || useLocalTransformOnly)
			{
				return realObjectToSync.transform.localRotation;
			}
			return realObjectToSync.transform.rotation;
		}

		public Vector3 getScale()
		{
			return realObjectToSync.transform.localScale;
		}

		public void setPosition(Vector3 position, bool isTeleporting)
		{
			if (position.x != float.NaN && position.y != float.NaN && position.z != float.NaN && !float.IsInfinity(position.x) && !float.IsInfinity(position.y) && !float.IsInfinity(position.z))
			{
				if (isSyncingChild || useLocalTransformOnly)
				{
					realObjectToSync.transform.localPosition = position;
				}
				else if (hasRigidbody && !isTeleporting && whenToUpdateTransform == WhenToUpdateTransform.FixedUpdate)
				{
					rb.MovePosition(position);
				}
				else if (hasRigidbody2D && !isTeleporting && whenToUpdateTransform == WhenToUpdateTransform.FixedUpdate)
				{
					rb2D.MovePosition(position);
				}
				else
				{
					realObjectToSync.transform.position = position;
				}
			}
		}

		public void setRotation(Quaternion rotation, bool isTeleporting)
		{
			if (rotation.x != float.NaN && rotation.y != float.NaN && rotation.z != float.NaN && rotation.w != float.NaN && !float.IsInfinity(rotation.x) && !float.IsInfinity(rotation.y) && !float.IsInfinity(rotation.z) && !float.IsInfinity(rotation.w))
			{
				if (isSyncingChild || useLocalTransformOnly)
				{
					realObjectToSync.transform.localRotation = rotation;
				}
				else if (hasRigidbody && !isTeleporting && whenToUpdateTransform == WhenToUpdateTransform.FixedUpdate)
				{
					rb.MoveRotation(rotation);
				}
				else if (hasRigidbody2D && !isTeleporting && whenToUpdateTransform == WhenToUpdateTransform.FixedUpdate)
				{
					rb2D.MoveRotation(rotation.eulerAngles.z);
				}
				else
				{
					realObjectToSync.transform.rotation = rotation;
				}
			}
		}

		public void setScale(Vector3 scale)
		{
			realObjectToSync.transform.localScale = scale;
		}

		private void resetFlags()
		{
			forceStateSend = false;
			sendAtPositionalRestMessage = false;
			sendAtRotationalRestMessage = false;
		}

		public void addState(StateMirror state)
		{
			if (stateCount > 1)
			{
				bool num = state.ownerTimestamp - stateBuffer[0].ownerTimestamp <= 0f;
				bool flag = state.localTimeResetIndicator != stateBuffer[0].localTimeResetIndicator;
				if (num && !flag)
				{
					return;
				}
				if (flag)
				{
					OnRemoteTimeReset();
				}
			}
			for (int num2 = stateBuffer.Length - 1; num2 >= 1; num2--)
			{
				stateBuffer[num2] = stateBuffer[num2 - 1];
			}
			stateBuffer[0] = state;
			stateCount = Mathf.Min(stateCount + 1, stateBuffer.Length);
		}

		public void stopEasing()
		{
			dontEasePosition = true;
			dontEaseRotation = true;
			dontEaseScale = true;
		}

		public void clearBuffer()
		{
			stateCount = 0;
			firstReceivedMessageZeroTime = 0f;
			restStatePosition = RestState.MOVING;
			restStateRotation = RestState.MOVING;
		}

		public void teleport()
		{
			teleportOwnedObjectFromOwner();
		}

		public void teleportOwnedObjectFromOwner()
		{
			if (!hasControl)
			{
				if (NetworkServer.active)
				{
					Debug.LogWarning("Use teleportAnyObjectFromServer() since you are the server, do not own the object, and you will need to choose the new transform.");
				}
				else
				{
					Debug.LogWarning("Only owners of objects or the server can send messages out. Teleport from the owner or the server instead.");
				}
				return;
			}
			latestTeleportedFromPosition = getPosition();
			latestTeleportedFromRotation = getRotation();
			if (NetworkServer.active)
			{
				RpcTeleport(getPosition(), getRotation().eulerAngles, getScale(), localTime);
			}
			else if (base.isOwned)
			{
				CmdTeleport(getPosition(), getRotation().eulerAngles, getScale(), localTime);
			}
		}

		public void teleportAnyObjectFromServer(Vector3 newPosition, Quaternion newRotation, Vector3 newScale)
		{
			if (hasControl)
			{
				setPosition(newPosition, isTeleporting: true);
				setRotation(newRotation, isTeleporting: true);
				setScale(newScale);
				teleportOwnedObjectFromOwner();
			}
			else if (NetworkServer.active)
			{
				RpcNonServerOwnedTeleportFromServer(newPosition, newRotation.eulerAngles, newScale);
			}
			else
			{
				Debug.LogWarning("Call this from the server.");
			}
		}

		[ClientRpc]
		public void RpcNonServerOwnedTeleportFromServer(Vector3 newPosition, Vector3 newRotation, Vector3 newScale)
		{
			NetworkWriterPooled writer = NetworkWriterPool.Get();
			writer.WriteVector3(newPosition);
			writer.WriteVector3(newRotation);
			writer.WriteVector3(newScale);
			SendRPCInternal("System.Void Smooth.SmoothSyncMirror::RpcNonServerOwnedTeleportFromServer(UnityEngine.Vector3,UnityEngine.Vector3,UnityEngine.Vector3)", -16266588, writer, 0, includeOwner: true);
			NetworkWriterPool.Return(writer);
		}

		[Command]
		public void CmdTeleport(Vector3 position, Vector3 rotation, Vector3 scale, float tempOwnerTime)
		{
			NetworkWriterPooled writer = NetworkWriterPool.Get();
			writer.WriteVector3(position);
			writer.WriteVector3(rotation);
			writer.WriteVector3(scale);
			writer.WriteFloat(tempOwnerTime);
			SendCommandInternal("System.Void Smooth.SmoothSyncMirror::CmdTeleport(UnityEngine.Vector3,UnityEngine.Vector3,UnityEngine.Vector3,System.Single)", 1505460848, writer, 0);
			NetworkWriterPool.Return(writer);
		}

		[ClientRpc]
		public void RpcTeleport(Vector3 position, Vector3 rotation, Vector3 scale, float tempOwnerTime)
		{
			NetworkWriterPooled writer = NetworkWriterPool.Get();
			writer.WriteVector3(position);
			writer.WriteVector3(rotation);
			writer.WriteVector3(scale);
			writer.WriteFloat(tempOwnerTime);
			SendRPCInternal("System.Void Smooth.SmoothSyncMirror::RpcTeleport(UnityEngine.Vector3,UnityEngine.Vector3,UnityEngine.Vector3,System.Single)", -386256399, writer, 0, includeOwner: true);
			NetworkWriterPool.Return(writer);
		}

		private void addTeleportState(StateMirror teleportState)
		{
			if (teleportState != null)
			{
				teleportState.atPositionalRest = true;
				teleportState.atRotationalRest = true;
			}
			if (stateCount == 0)
			{
				approximateNetworkTimeOnOwner = teleportState.ownerTimestamp;
			}
			if (stateCount == 0 || teleportState.ownerTimestamp >= stateBuffer[0].ownerTimestamp)
			{
				for (int num = stateBuffer.Length - 1; num >= 1; num--)
				{
					stateBuffer[num] = stateBuffer[num - 1];
				}
				stateBuffer[0] = teleportState;
			}
			else
			{
				if (stateCount == stateBuffer.Length && stateBuffer[stateCount - 1].ownerTimestamp > teleportState.ownerTimestamp)
				{
					return;
				}
				for (int num2 = stateCount - 1; num2 >= 0; num2--)
				{
					if (stateBuffer[num2].ownerTimestamp > teleportState.ownerTimestamp)
					{
						for (int num3 = stateBuffer.Length - 1; num3 > num2 + 1; num3--)
						{
							stateBuffer[num3] = stateBuffer[num3 - 1];
						}
						stateBuffer[num2 + 1] = teleportState;
						break;
					}
				}
			}
			stateCount = Mathf.Min(stateCount + 1, stateBuffer.Length);
		}

		public void forceStateSendNextFixedUpdate()
		{
			forceStateSend = true;
		}

		public void AssignAuthorityCallback(NetworkConnection conn, NetworkIdentity theNetID, bool authorityState)
		{
			NetworkIdentity networkIdentity = NetworkServer.spawned[theNetID.netId];
			if (networkIdentity == null)
			{
				Debug.LogWarning("Smooth Sync: Cannot find target for authority change.");
				return;
			}
			SmoothSyncMirror component = networkIdentity.GetComponent<SmoothSyncMirror>();
			if (!(component != null) || !(component == this))
			{
				return;
			}
			SmoothSyncMirror[] array = component.childObjectSmoothSyncs;
			for (int i = 0; i < array.Length; i++)
			{
				if (authorityState)
				{
					array[i].ownerChangeIndicator++;
					if (array[i].ownerChangeIndicator > 127)
					{
						array[i].ownerChangeIndicator = 1;
					}
				}
			}
		}

		public override void OnStartServer()
		{
			NetworkServer.ReplaceHandler<NetworkStateMirror>(HandleSyncServer);
		}

		public override void OnStartClient()
		{
			registerClientHandlers();
		}

		public void registerClientHandlers()
		{
			if (!NetworkServer.active)
			{
				NetworkClient.ReplaceHandler<NetworkStateMirror>(HandleSyncClient);
			}
		}

		public bool shouldSendPosition()
		{
			if (syncPosition != SyncMode.NONE && (forceStateSend || (getPosition() != lastPositionWhenStateWasSent && (sendPositionThreshold == 0f || Vector3.Distance(lastPositionWhenStateWasSent, getPosition()) > sendPositionThreshold))))
			{
				return true;
			}
			return false;
		}

		public bool shouldSendRotation()
		{
			if (syncRotation != SyncMode.NONE && (forceStateSend || (getRotation() != lastRotationWhenStateWasSent && (sendRotationThreshold == 0f || Quaternion.Angle(lastRotationWhenStateWasSent, getRotation()) > sendRotationThreshold))))
			{
				return true;
			}
			return false;
		}

		public bool shouldSendScale()
		{
			if (syncScale != SyncMode.NONE && (forceStateSend || (getScale() != lastScaleWhenStateWasSent && (sendScaleThreshold == 0f || Vector3.Distance(lastScaleWhenStateWasSent, getScale()) > sendScaleThreshold))))
			{
				return true;
			}
			return false;
		}

		public bool shouldSendVelocity()
		{
			if (hasRigidbody)
			{
				if (syncVelocity != SyncMode.NONE && (forceStateSend || (rb.linearVelocity != lastVelocityWhenStateWasSent && (sendVelocityThreshold == 0f || Vector3.Distance(lastVelocityWhenStateWasSent, rb.linearVelocity) > sendVelocityThreshold))))
				{
					return true;
				}
				return false;
			}
			if (hasRigidbody2D)
			{
				if (syncVelocity != SyncMode.NONE && (forceStateSend || ((rb2D.linearVelocity.x != lastVelocityWhenStateWasSent.x || rb2D.linearVelocity.y != lastVelocityWhenStateWasSent.y) && (sendVelocityThreshold == 0f || Vector2.Distance(lastVelocityWhenStateWasSent, rb2D.linearVelocity) > sendVelocityThreshold))))
				{
					return true;
				}
				return false;
			}
			return false;
		}

		public bool shouldSendAngularVelocity()
		{
			if (hasRigidbody)
			{
				if (syncAngularVelocity != SyncMode.NONE && (forceStateSend || (rb.angularVelocity != lastAngularVelocityWhenStateWasSent && (sendAngularVelocityThreshold == 0f || Vector3.Distance(lastAngularVelocityWhenStateWasSent, rb.angularVelocity * 57.29578f) > sendAngularVelocityThreshold))))
				{
					return true;
				}
				return false;
			}
			if (hasRigidbody2D)
			{
				if (syncAngularVelocity != SyncMode.NONE && (forceStateSend || (rb2D.angularVelocity != lastAngularVelocityWhenStateWasSent.z && (sendAngularVelocityThreshold == 0f || Mathf.Abs(lastAngularVelocityWhenStateWasSent.z - rb2D.angularVelocity) > sendAngularVelocityThreshold))))
				{
					return true;
				}
				return false;
			}
			return false;
		}

		[Server]
		private void SendStateToNonOwners(NetworkStateMirror state)
		{
			if (!NetworkServer.active)
			{
				Debug.LogWarning("[Server] function 'System.Void Smooth.SmoothSyncMirror::SendStateToNonOwners(Smooth.NetworkStateMirror)' called when server was not active");
			}
			else
			{
				if (netID.observers == null)
				{
					return;
				}
				foreach (KeyValuePair<int, NetworkConnectionToClient> observer in netID.observers)
				{
					NetworkConnection value = observer.Value;
					if (value != null && (transformSource == TransformSource.Server || value != netID.connectionToClient) && value.GetType() == typeof(NetworkConnectionToClient) && value.isReady)
					{
						value.Send(state, networkChannel);
					}
				}
			}
		}

		public static void HandleSyncServer(NetworkConnectionToClient conn, NetworkStateMirror networkState)
		{
			if (!(networkState.smoothSync == null) && networkState.smoothSync.netID.connectionToClient == conn && (networkState.smoothSync.latestValidatedState == null || networkState.smoothSync.validateStateMethod(networkState.state, networkState.smoothSync.latestValidatedState)))
			{
				networkState.smoothSync.latestValidatedState = networkState.state;
				networkState.smoothSync.latestValidatedState.receivedOnServerTimestamp = networkState.smoothSync.localTime;
				networkState.smoothSync.SendStateToNonOwners(networkState);
				networkState.smoothSync.addState(networkState.state);
				networkState.smoothSync.checkIfOwnerHasChanged(networkState.state);
			}
		}

		public static void HandleSyncClient(NetworkStateMirror networkState)
		{
			if (networkState.smoothSync != null && !networkState.smoothSync.hasControl)
			{
				networkState.smoothSync.addState(networkState.state);
				networkState.smoothSync.checkIfOwnerHasChanged(networkState.state);
			}
		}

		public void checkIfOwnerHasChanged(StateMirror newState)
		{
			if (isSmoothingAuthorityChanges && ownerChangeIndicator != previousReceivedOwnerInt)
			{
				approximateNetworkTimeOnOwner = newState.ownerTimestamp;
				latestAuthorityChangeZeroTime = localTime;
				stateCount = 0;
				firstReceivedMessageZeroTime = 1f;
				restStatePosition = RestState.MOVING;
				restStateRotation = RestState.MOVING;
				StateMirror stateMirror = new StateMirror();
				stateMirror.position = getPosition();
				stateMirror.rotation = getRotation();
				stateMirror.scale = getScale();
				stateMirror.ownerTimestamp = newState.ownerTimestamp - interpolationBackTime;
				stateMirror.receivedTimestamp = newState.receivedTimestamp;
				addState(stateMirror);
				previousReceivedOwnerInt = ownerChangeIndicator;
			}
		}

		public float GetNetworkSendInterval()
		{
			if (sendRate == 0f)
			{
				return 0f;
			}
			return 1f / sendRate;
		}

		private void adjustOwnerTime()
		{
			if (stateBuffer[0] != null && (!stateBuffer[0].atPositionalRest || !stateBuffer[0].atRotationalRest))
			{
				float num = stateBuffer[0].ownerTimestamp + (localTime - stateBuffer[0].receivedTimestamp);
				float num2 = Mathf.Max(timeCorrectionSpeed * Time.deltaTime, minTimePrecision);
				if (firstReceivedMessageZeroTime == 0f)
				{
					firstReceivedMessageZeroTime = localTime;
				}
				float num3 = Mathf.Abs(approximateNetworkTimeOnOwner - num);
				if ((float)receivedStatesCounter < sendRate || num3 < num2 || num3 > snapTimeThreshold)
				{
					approximateNetworkTimeOnOwner = num;
				}
				else if (approximateNetworkTimeOnOwner < num)
				{
					approximateNetworkTimeOnOwner += num2;
				}
				else
				{
					approximateNetworkTimeOnOwner -= num2;
				}
			}
		}

		public override bool Weaved()
		{
			return true;
		}

		protected void UserCode_RpcNonServerOwnedTeleportFromServer__Vector3__Vector3__Vector3(Vector3 newPosition, Vector3 newRotation, Vector3 newScale)
		{
			if (hasAuthorityOrUnownedOnServer)
			{
				setPosition(newPosition, isTeleporting: true);
				setRotation(Quaternion.Euler(newRotation), isTeleporting: true);
				setScale(newScale);
				teleportOwnedObjectFromOwner();
			}
		}

		protected static void InvokeUserCode_RpcNonServerOwnedTeleportFromServer__Vector3__Vector3__Vector3(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
		{
			if (!NetworkClient.active)
			{
				Debug.LogError("RPC RpcNonServerOwnedTeleportFromServer called on server.");
			}
			else
			{
				((SmoothSyncMirror)obj).UserCode_RpcNonServerOwnedTeleportFromServer__Vector3__Vector3__Vector3(reader.ReadVector3(), reader.ReadVector3(), reader.ReadVector3());
			}
		}

		protected void UserCode_CmdTeleport__Vector3__Vector3__Vector3__Single(Vector3 position, Vector3 rotation, Vector3 scale, float tempOwnerTime)
		{
			RpcTeleport(position, rotation, scale, tempOwnerTime);
			StateMirror stateMirror = new StateMirror();
			stateMirror.copyFromSmoothSync(this);
			stateMirror.position = position;
			stateMirror.rotation = Quaternion.Euler(rotation);
			stateMirror.ownerTimestamp = tempOwnerTime;
			stateMirror.receivedTimestamp = localTime;
			stateMirror.teleport = true;
			addTeleportState(stateMirror);
		}

		protected static void InvokeUserCode_CmdTeleport__Vector3__Vector3__Vector3__Single(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
		{
			if (!NetworkServer.active)
			{
				Debug.LogError("Command CmdTeleport called on client.");
			}
			else
			{
				((SmoothSyncMirror)obj).UserCode_CmdTeleport__Vector3__Vector3__Vector3__Single(reader.ReadVector3(), reader.ReadVector3(), reader.ReadVector3(), reader.ReadFloat());
			}
		}

		protected void UserCode_RpcTeleport__Vector3__Vector3__Vector3__Single(Vector3 position, Vector3 rotation, Vector3 scale, float tempOwnerTime)
		{
			if (!hasAuthorityOrUnownedOnServer && !NetworkServer.active)
			{
				StateMirror stateMirror = new StateMirror();
				stateMirror.copyFromSmoothSync(this);
				stateMirror.position = position;
				stateMirror.rotation = Quaternion.Euler(rotation);
				stateMirror.ownerTimestamp = tempOwnerTime;
				stateMirror.receivedTimestamp = localTime;
				stateMirror.teleport = true;
				addTeleportState(stateMirror);
			}
		}

		protected static void InvokeUserCode_RpcTeleport__Vector3__Vector3__Vector3__Single(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
		{
			if (!NetworkClient.active)
			{
				Debug.LogError("RPC RpcTeleport called on server.");
			}
			else
			{
				((SmoothSyncMirror)obj).UserCode_RpcTeleport__Vector3__Vector3__Vector3__Single(reader.ReadVector3(), reader.ReadVector3(), reader.ReadVector3(), reader.ReadFloat());
			}
		}

		static SmoothSyncMirror()
		{
			RemoteProcedureCalls.RegisterCommand(typeof(SmoothSyncMirror), "System.Void Smooth.SmoothSyncMirror::CmdTeleport(UnityEngine.Vector3,UnityEngine.Vector3,UnityEngine.Vector3,System.Single)", InvokeUserCode_CmdTeleport__Vector3__Vector3__Vector3__Single, requiresAuthority: true);
			RemoteProcedureCalls.RegisterRpc(typeof(SmoothSyncMirror), "System.Void Smooth.SmoothSyncMirror::RpcNonServerOwnedTeleportFromServer(UnityEngine.Vector3,UnityEngine.Vector3,UnityEngine.Vector3)", InvokeUserCode_RpcNonServerOwnedTeleportFromServer__Vector3__Vector3__Vector3);
			RemoteProcedureCalls.RegisterRpc(typeof(SmoothSyncMirror), "System.Void Smooth.SmoothSyncMirror::RpcTeleport(UnityEngine.Vector3,UnityEngine.Vector3,UnityEngine.Vector3,System.Single)", InvokeUserCode_RpcTeleport__Vector3__Vector3__Vector3__Single);
		}
	}
}

}
