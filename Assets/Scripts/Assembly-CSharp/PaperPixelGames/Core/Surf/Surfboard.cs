using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Patterns;
using StylizedWater3;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace PaperPixelGames.Core.Surf
{
	[DefaultExecutionOrder(-100)]
	[RequireComponent(typeof(NetworkObject), typeof(NetworkTransform), typeof(Rigidbody))]
	public sealed class Surfboard : BaseHoldable, IAttachableObject
	{
		public static readonly HashSet<Surfboard> Instances = new HashSet<Surfboard>();

		public Transform graphics;

		public Transform riderAnchor;

		public BoxCollider boardCollider;

		public ParticleSystem wake;

		public SurfFeedbackProfile feedback;

		private readonly NetworkVariable<int> _spawnIndex = new NetworkVariable<int>(0);

		private readonly NetworkVariable<SurfboardState> _state = new NetworkVariable<SurfboardState>(SurfboardState.Beached);

		private readonly NetworkVariable<ulong> _holder = new NetworkVariable<ulong>(ulong.MaxValue);

		private readonly NetworkVariable<uint> _session = new NetworkVariable<uint>(0u);

		private readonly NetworkVariable<double> _poseTime = new NetworkVariable<double>(0.0);

		private readonly NetworkVariable<bool> _paddling = new NetworkVariable<bool>(value: false);

		private SurfFeedback _feedback;

		private Vector3 _presentationPreviousPosition;

		private float _presentationPreviousYaw;

		private bool _presentationTracking;

		private Rigidbody _body;

		private NetworkTransform _networkTransform;

		private PPG_Player _boundPlayer;

		private Vector2 _input;

		private Vector3 _cameraForward = Vector3.forward;

		private bool _steeringActive;

		private float _proneYawVelocity;

		private SurfGpuSurface _gpuSurface;

		private Transform _surfaceRoot;

		private bool _surfaceInitialized;

		private float _visualYaw;

		private float _visualHeight;

		private float _visualHeightVelocity;

		private Vector3 _visualNormal;

		private Vector3 _wakePreviousPosition;

		private float _wakeDistance;

		private bool _wakeTracking;

		private Vector3 _velocity;

		private Vector3 _homePosition;

		private Quaternion _homeRotation;

		private double _lastInput;

		private double _lastPose;

		private double _mountAfter;

		private double _freeSince;

		private double _lastRequest;

		private float _shallowTime;

		private bool _materialApplied;

		private readonly RaycastHit[] _hits = new RaycastHit[32];

		private readonly Collider[] _overlaps = new Collider[32];

		private int _environmentMask;

		public List<AttachSeat> Seats { get; set; } = new List<AttachSeat>();

		public PlayerAttachmentMode AttachmentMode => PlayerAttachmentMode.Surfboard;

		public int SpawnIndex
		{
			get
			{
				return _spawnIndex.Value;
			}
			set
			{
				_spawnIndex.Value = value;
			}
		}

		public override bool RequiresHeldSlot => true;

		public override bool CanPersistInQuickSlotSave => false;

		public bool IsRiding => SurfRules.IsRiding(_state.Value);

		public bool IsCarried => _state.Value == SurfboardState.Carried;

		public bool Standing => _state.Value == SurfboardState.Standing;

		public uint Session => _session.Value;

		public ulong Holder => _holder.Value;

		public Vector3 HomePosition => _homePosition;

		public float Speed => _velocity.magnitude;

		public Vector3 SurfacePosition
		{
			get
			{
				if (!(_surfaceRoot != null))
				{
					return base.transform.position;
				}
				return _surfaceRoot.position;
			}
		}

		public bool UsingGpuSurface
		{
			get
			{
				float height;
				Vector3 normal;
				if (_gpuSurface != null)
				{
					return _gpuSurface.TrySample(base.transform.position, out height, out normal);
				}
				return false;
			}
		}

		public double PoseTime => _poseTime.Value;

		public bool Paddling
		{
			get
			{
				if (IsRiding && !Standing)
				{
					return _paddling.Value;
				}
				return false;
			}
		}

		public float PaddleBlend { get; private set; }

		public float PresentationSpeed { get; private set; }

		public float TurnLean { get; private set; }

		public float PaddlePhase => SurfPosePlacement.PaddlePhase(TimeNow - PoseTime);

		public float HalfWidth
		{
			get
			{
				if (!(boardCollider != null))
				{
					return 0.23f;
				}
				return boardCollider.size.x * Mathf.Abs(base.transform.lossyScale.x) * 0.5f;
			}
		}

		public float HalfLength
		{
			get
			{
				if (!(boardCollider != null))
				{
					return 0.85f;
				}
				return boardCollider.size.z * Mathf.Abs(base.transform.lossyScale.z) * 0.5f;
			}
		}

		private double TimeNow
		{
			get
			{
				if (!(SurfboardWorld.Instance != null))
				{
					return Time.timeAsDouble;
				}
				return SurfboardWorld.Instance.TimeNow;
			}
		}

		public override bool ServerTryStaffPickup(int employeeId)
		{
			return false;
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			Instances.Clear();
		}

		protected override void Awake()
		{
			base.Awake();
			_body = GetComponent<Rigidbody>();
			_networkTransform = GetComponent<NetworkTransform>();
			_environmentMask = ~LayerMask.GetMask("Water", "Player", "ClientPlayer", "Ignore Raycast", "BoidsCollider");
			int mask = LayerMask.GetMask("BoidsCollider");
			Rigidbody body = _body;
			body.excludeLayers = (int)body.excludeLayers | mask;
			if (boardCollider != null)
			{
				BoxCollider boxCollider = boardCollider;
				boxCollider.excludeLayers = (int)boxCollider.excludeLayers | mask;
			}
			if (Application.isPlaying && graphics != null && _surfaceRoot == null)
			{
				_surfaceRoot = new GameObject("Rendered water alignment").transform;
				_surfaceRoot.SetParent(base.transform, worldPositionStays: false);
				graphics.SetParent(_surfaceRoot, worldPositionStays: false);
				if (riderAnchor != null)
				{
					riderAnchor.SetParent(_surfaceRoot, worldPositionStays: false);
				}
				if (wake != null)
				{
					wake.transform.SetParent(_surfaceRoot, worldPositionStays: false);
				}
			}
			if (wake != null)
			{
				ParticleTrailEmitter component = wake.GetComponent<ParticleTrailEmitter>();
				if (component != null)
				{
					component.enabled = false;
				}
				ParticleSystem.MainModule main = wake.main;
				main.simulationSpace = ParticleSystemSimulationSpace.World;
				ParticleSystem.EmissionModule emission = wake.emission;
				emission.rateOverTime = 0f;
				emission.rateOverDistance = 0f;
				emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
				wake.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
			}
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			Instances.Add(this);
			_feedback = new SurfFeedback(this);
			_homePosition = base.transform.position;
			_homeRotation = base.transform.rotation;
			if (base.IsServer)
			{
				base.NetworkManager.OnClientDisconnectCallback += OnDisconnected;
			}
			NetworkVariable<SurfboardState> state = _state;
			state.OnValueChanged = (NetworkVariable<SurfboardState>.OnValueChangedDelegate)Delegate.Combine(state.OnValueChanged, new NetworkVariable<SurfboardState>.OnValueChangedDelegate(StateChanged));
			Seats = new List<AttachSeat>
			{
				new AttachSeat(riderAnchor, Vector3.zero, Vector3.zero, Vector3.zero)
			};
			ApplyPresentation();
		}

		public override void OnNetworkDespawn()
		{
			DisposeSurfaceQuery();
			_feedback?.Dispose();
			_feedback = null;
			Instances.Remove(this);
			if (base.NetworkManager != null)
			{
				base.NetworkManager.OnClientDisconnectCallback -= OnDisconnected;
			}
			NetworkVariable<SurfboardState> state = _state;
			state.OnValueChanged = (NetworkVariable<SurfboardState>.OnValueChangedDelegate)Delegate.Remove(state.OnValueChanged, new NetworkVariable<SurfboardState>.OnValueChangedDelegate(StateChanged));
			if (_boundPlayer != null)
			{
				_boundPlayer.EndSurfLocal(this, carry: false);
			}
			_boundPlayer = null;
			base.OnNetworkDespawn();
		}

		private void StateChanged(SurfboardState previous, SurfboardState current)
		{
			_proneYawVelocity = 0f;
			_wakeTracking = false;
			_wakeDistance = 0f;
			if (!SurfRules.IsRiding(current))
			{
				_feedback?.Dispose();
				_presentationTracking = false;
				float num = (TurnLean = 0f);
				float paddleBlend = (PresentationSpeed = num);
				PaddleBlend = paddleBlend;
			}
			if (!SurfRules.IsRiding(current) && current != SurfboardState.Floating)
			{
				DisposeSurfaceQuery();
			}
			ApplyPresentation();
			if (current == SurfboardState.Carried)
			{
				base.ClientSetVisibility(visible: true);
			}
			if (wake != null && SurfRules.IsRiding(previous) != SurfRules.IsRiding(current))
			{
				wake.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
				if (SurfRules.IsRiding(current))
				{
					wake.Play();
				}
			}
		}

		private void LateUpdate()
		{
			if (!base.IsSpawned)
			{
				return;
			}
			UpdateSurfacePresentation();
			UpdateMotionPresentation();
			_feedback?.Tick(TimeNow, Time.deltaTime);
			if (!_materialApplied && SurfboardWorld.Instance != null)
			{
				Material[] spawnMaterials = SurfboardWorld.Instance.spawnMaterials;
				if (SpawnIndex >= 0 && SpawnIndex < spawnMaterials.Length && spawnMaterials[SpawnIndex] != null)
				{
					ApplyMaterial(graphics.gameObject, spawnMaterials[SpawnIndex]);
				}
				_materialApplied = true;
			}
			if (IsRiding && Singleton<PlayersManager>.Instance != null && Singleton<PlayersManager>.Instance.TryGetPlayer(Holder, out var player))
			{
				if (_boundPlayer != null && _boundPlayer != player)
				{
					_boundPlayer.EndSurfLocal(this, carry: false);
				}
				_boundPlayer = player;
				player.BeginSurfLocal(this);
			}
			else if (_boundPlayer != null)
			{
				_boundPlayer.EndSurfLocal(this, _state.Value == SurfboardState.Carried);
				_boundPlayer = null;
			}
			UpdateWake();
			if (_state.Value == SurfboardState.Carried && TryHolder(out var player2))
			{
				AlignCarriedVisual(_tpsShadowObject, player2.RigBones.TpsRig.RightHand, player2.RigBones.TpsRig.LeftHand, player2.transform);
				AlignCarriedVisual(_fpsShadowObject, player2.RigBones.FpsRig.RightHand, player2.RigBones.FpsRig.LeftHand, player2.transform);
			}
		}

		private void OnDisable()
		{
			DisposeSurfaceQuery();
			_feedback?.Dispose();
			_presentationTracking = false;
		}

		private void UpdateMotionPresentation()
		{
			Vector3 surfacePosition = SurfacePosition;
			Vector3 vector = ((_surfaceRoot != null) ? _surfaceRoot.forward : base.transform.forward);
			float num = Mathf.Atan2(vector.x, vector.z) * 57.29578f;
			float num2 = Mathf.Max(0.0001f, Time.deltaTime);
			float magnitude = Vector3.ProjectOnPlane(surfacePosition - _presentationPreviousPosition, Vector3.up).magnitude;
			int num3;
			float num4;
			if (IsRiding && _presentationTracking)
			{
				num3 = ((magnitude < 3f) ? 1 : 0);
				if (num3 != 0)
				{
					num4 = Mathf.Min(8f, magnitude / num2);
					goto IL_00ab;
				}
			}
			else
			{
				num3 = 0;
			}
			num4 = 0f;
			goto IL_00ab;
			IL_00ab:
			float b = num4;
			float b2 = ((num3 != 0) ? Mathf.Clamp(Mathf.DeltaAngle(_presentationPreviousYaw, num) / num2 / 100f, -1f, 1f) : 0f);
			PresentationSpeed = Mathf.Lerp(PresentationSpeed, b, 1f - Mathf.Exp(-8f * num2));
			TurnLean = Mathf.Lerp(TurnLean, b2, 1f - Mathf.Exp(-6f * num2));
			PaddleBlend = Mathf.Lerp(PaddleBlend, (Paddling && TimeNow - PoseTime > 0.6) ? 1 : 0, 1f - Mathf.Exp(-10f * num2));
			_presentationPreviousPosition = surfacePosition;
			_presentationPreviousYaw = num;
			_presentationTracking = IsRiding;
		}

		private void DisposeSurfaceQuery()
		{
			_gpuSurface?.Dispose();
			_gpuSurface = null;
			_surfaceInitialized = false;
			_visualHeightVelocity = 0f;
			if (_surfaceRoot != null)
			{
				_surfaceRoot.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			}
		}

		private void UpdateSurfacePresentation()
		{
			if (!IsRiding && _state.Value != SurfboardState.Floating)
			{
				return;
			}
			if (_gpuSurface == null && SurfGpuSurface.Supported)
			{
				_gpuSurface = new SurfGpuSurface(GetInstanceID(), base.transform.position);
			}
			_gpuSurface?.SetPosition(base.transform.position);
			if (!(_surfaceRoot == null))
			{
				Vector3 position = base.transform.position;
				Vector3 normal = base.transform.up;
				float height = position.y;
				if (_gpuSurface != null && _gpuSurface.TrySample(position, out height, out normal))
				{
					position.y = height + 0.08f;
				}
				else
				{
					normal = base.transform.up;
				}
				ApplySurfacePose(position, normal, base.transform.eulerAngles.y, Time.deltaTime);
			}
		}

		private void ApplySurfacePose(Vector3 position, Vector3 normal, float yaw, float dt)
		{
			_visualYaw = ((!_surfaceInitialized) ? yaw : Mathf.LerpAngle(_visualYaw, yaw, 1f - Mathf.Exp(-20f * dt)));
			_visualHeight = ((!_surfaceInitialized) ? position.y : Mathf.SmoothDamp(_visualHeight, position.y, ref _visualHeightVelocity, 0.09f, float.PositiveInfinity, dt));
			_visualNormal = ((!_surfaceInitialized) ? normal : Vector3.Slerp(_visualNormal, normal, 1f - Mathf.Exp(-12f * dt)));
			position.y = _visualHeight;
			normal = _visualNormal;
			Vector3 vector = Quaternion.Euler(0f, _visualYaw, 0f) * Vector3.forward;
			vector -= Vector3.up * (Vector3.Dot(vector, normal) / Mathf.Max(0.2f, normal.y));
			_surfaceRoot.SetPositionAndRotation(position, Quaternion.LookRotation(vector, normal));
			_surfaceInitialized = true;
		}

		private void UpdateWake()
		{
			if (wake == null)
			{
				return;
			}
			Vector3 position = base.transform.position;
			float magnitude = Vector3.ProjectOnPlane(position - _wakePreviousPosition, Vector3.up).magnitude;
			_wakePreviousPosition = position;
			if (!IsRiding || !_wakeTracking || magnitude > 3f)
			{
				_wakeTracking = IsRiding;
				_wakeDistance = 0f;
				return;
			}
			if (!wake.isPlaying)
			{
				wake.Play();
			}
			_wakeDistance += magnitude * (Standing ? 2f : 0.35f);
			int num = Mathf.FloorToInt(_wakeDistance);
			if (num > 0)
			{
				_wakeDistance -= num;
				ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
				{
					startSize = (Standing ? 0.6f : 0.28f),
					startLifetime = (Standing ? 1.2f : 0.65f)
				};
				wake.Emit(emitParams, Mathf.Min(num, 4));
			}
		}

		public float SampleSurfaceHeight(SurfboardWorld world, Vector3 position)
		{
			if (_gpuSurface == null || !_gpuSurface.TrySample(position, out var height, out var _))
			{
				return world.SampleHeight(position);
			}
			return height;
		}

		public static void AlignCarriedVisual(GameObject visual, Transform rightHand, Transform leftHand, Transform player)
		{
			if (!(visual == null) && !(rightHand == null) && !(leftHand == null))
			{
				visual.transform.SetPositionAndRotation((rightHand.position + leftHand.position) * 0.5f + Vector3.up * 0.08f, Quaternion.Euler(0f, player.eulerAngles.y + 90f, 0f));
			}
		}

		private void FixedUpdate()
		{
			if (!base.IsServer || !base.IsSpawned || SurfboardWorld.Instance == null)
			{
				return;
			}
			SurfboardWorld instance = SurfboardWorld.Instance;
			double timeNow = instance.TimeNow;
			if (!Finite(base.transform.position) || base.transform.position.y < instance.WaterLevel - 15f || Vector3.Distance(base.transform.position, _homePosition) > 300f)
			{
				ServerRelease(SurfExitReason.OutOfBounds);
			}
			else if (_state.Value == SurfboardState.Carried)
			{
				if (!TryHolder(out var player))
				{
					ServerRelease(SurfExitReason.Disconnect);
					return;
				}
				_body.position = player.transform.position;
				if (player.IsTrapped || (player.DivingEquipment != null && player.DivingEquipment.IsKnocked))
				{
					ServerRelease(SurfExitReason.Knocked);
					return;
				}
				float num = instance.SampleHeight(player.transform.position);
				if (SurfRules.CanMount(Depth(player.transform.position, num), player.transform.position.y - num, (float)(_mountAfter - timeNow)) && player.CanBeginSurf && ClearForRider(new Vector3(player.transform.position.x, num, player.transform.position.z)))
				{
					Vector3 position = player.transform.position;
					position.y = num + 0.08f;
					_body.position = position;
					_body.rotation = Quaternion.Euler(0f, player.transform.eulerAngles.y, 0f);
					_networkTransform.Teleport(position, Quaternion.Euler(0f, player.transform.eulerAngles.y, 0f), base.transform.localScale);
					_session.Value++;
					_input = Vector2.zero;
					_velocity = Vector3.zero;
					_steeringActive = false;
					_state.Value = SurfboardState.Prone;
					_poseTime.Value = timeNow;
					_lastPose = timeNow;
					ServerSetVisibility(visible: false);
				}
			}
			else if (IsRiding)
			{
				if (!TryHolder(out var player2))
				{
					ServerRelease(SurfExitReason.Disconnect);
					return;
				}
				if (player2.IsTrapped || (player2.DivingEquipment != null && player2.DivingEquipment.IsKnocked))
				{
					ServerRelease(SurfExitReason.Knocked);
					return;
				}
				if (timeNow - _lastInput > 0.3)
				{
					_input = Vector2.zero;
					_steeringActive = false;
					_paddling.Value = false;
				}
				Simulate(instance, Time.fixedDeltaTime);
				float num2 = Depth(_body.position, SampleSurfaceHeight(instance, _body.position));
				_shallowTime = ((num2 < 0.45f) ? (_shallowTime + Time.fixedDeltaTime) : 0f);
				if (_shallowTime > 0.4f)
				{
					ServerRelease(SurfExitReason.Shore);
				}
			}
			else if (_state.Value == SurfboardState.Floating)
			{
				FollowSurface(instance, Time.fixedDeltaTime, _body.position, _body.rotation * Vector3.forward);
				if (Depth(base.transform.position, instance.SampleHeight(base.transform.position)) < 0.15f)
				{
					_state.Value = SurfboardState.Grounded;
				}
				if (timeNow - _freeSince > 120.0 && !PlayerNearby(15f))
				{
					ServerRelease(SurfExitReason.OutOfBounds);
				}
			}
			else if (_state.Value == SurfboardState.Grounded)
			{
				float num3 = instance.SampleHeight(base.transform.position);
				if (base.transform.position.y <= num3 + 0.12f && Depth(base.transform.position, num3) > 0.3f)
				{
					_state.Value = SurfboardState.Floating;
					ApplyPresentation();
				}
				if (timeNow - _freeSince > 120.0 && !PlayerNearby(15f))
				{
					ServerRelease(SurfExitReason.OutOfBounds);
				}
			}
		}

		private bool TryHolder(out PPG_Player player)
		{
			player = null;
			if (Singleton<PlayersManager>.Instance != null && Singleton<PlayersManager>.Instance.TryGetPlayer(Holder, out player) && player != null)
			{
				return player.IsSpawned;
			}
			return false;
		}

		public override void ServerHold(Interactor interactor)
		{
			if (base.IsServer && !(interactor == null) && CanHold.Value && !IsRiding && !(TimeNow < _mountAfter) && !(Vector3.Distance(interactor.transform.position, base.transform.position) > 5f) && !interactor.Player.HasSurfboard && interactor.Player.CanBeginSurf && (!(interactor.CurrentHeld != null) || !interactor.CurrentHeld.RequiresHeldSlot))
			{
				base.ServerHold(interactor);
				if (!CanHold.Value && base.OwnerClientId == interactor.OwnerClientId)
				{
					_holder.Value = interactor.OwnerClientId;
					_state.Value = SurfboardState.Carried;
					_mountAfter = TimeNow + 0.8;
					_velocity = Vector3.zero;
					ApplyPresentation();
				}
			}
		}

		protected override void ServerDrop(Vector3 position, Quaternion rotation)
		{
			if (base.IsServer && Holder != ulong.MaxValue)
			{
				ServerRelease(SurfExitReason.Drop);
			}
		}

		protected override void ClientDrop(Vector3 position, Quaternion rotation)
		{
			base.ClientDrop(position, rotation);
			ApplyPresentation();
		}

		public override void RequestThrow(Thrower.ThrowPhysicsRequestData data)
		{
			RequestReleaseRpc(Session);
		}

		public override void ClientRefreshRuntimeRepresentationState()
		{
			ApplyPresentation();
		}

		public override void ClientSetVisibility(bool visible)
		{
			base.ClientSetVisibility(visible && _state.Value == SurfboardState.Carried);
			ApplyPresentation();
		}

		protected override void ClientHoldEveryone()
		{
			SurfboardWorld instance = SurfboardWorld.Instance;
			if (instance != null && SpawnIndex >= 0 && SpawnIndex < instance.spawnMaterials.Length)
			{
				ApplyMaterial(_tpsShadowObject, instance.spawnMaterials[SpawnIndex]);
				ApplyMaterial(_fpsShadowObject, instance.spawnMaterials[SpawnIndex]);
				if (_tpsShadowObject != null)
				{
					_tpsShadowObject.transform.localScale = Vector3.Scale(base.ItemSO.TPSHoldOffset.ScaleOffset, instance.spawnScales[SpawnIndex]);
				}
				if (_fpsShadowObject != null)
				{
					_fpsShadowObject.transform.localScale = Vector3.Scale(base.ItemSO.FPSHoldOffset.ScaleOffset, instance.spawnScales[SpawnIndex]);
				}
			}
			ApplyPresentation();
		}

		private static void ApplyMaterial(GameObject visual, Material material)
		{
			if (!(visual == null) && !(material == null))
			{
				Renderer[] componentsInChildren = visual.GetComponentsInChildren<Renderer>(includeInactive: true);
				for (int i = 0; i < componentsInChildren.Length; i++)
				{
					componentsInChildren[i].sharedMaterial = material;
				}
			}
		}

		private void ApplyPresentation()
		{
			bool flag = _state.Value == SurfboardState.Carried;
			ObjectsStatus(!flag);
			CollidersStatus(!flag);
			if (!flag)
			{
				FpsShadowStatus(status: false);
				TpsShadowStatus(status: false);
			}
			if (!(_body == null))
			{
				bool flag2 = base.IsServer && _state.Value == SurfboardState.Grounded;
				if (!flag2 && !_body.isKinematic)
				{
					_body.linearVelocity = Vector3.zero;
					_body.angularVelocity = Vector3.zero;
				}
				_body.isKinematic = !flag2;
				_body.useGravity = flag2;
				_body.interpolation = (base.IsServer ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None);
				if (boardCollider != null)
				{
					boardCollider.enabled = !flag;
				}
			}
		}

		public override void EnableGazeHints()
		{
			Singleton<HintsManager>.Instance?.AddHint("LeftMouseMechanic", "key_hold");
		}

		public override void DisableGazeHints()
		{
			Singleton<HintsManager>.Instance?.RemoveHint("LeftMouseMechanic");
		}

		public override void EnableHeldHints()
		{
			AddHeldThrowHint();
		}

		public override void DisableHeldHints()
		{
			RemoveHeldThrowHint();
		}

		[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone, Delivery = RpcDelivery.Unreliable)]
		public void SubmitInputRpc(uint session, Vector2 input, Vector3 cameraForward, bool steeringActive, RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcParams rpcParams2 = rpcParams;
				RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
				{
					InvokePermission = RpcInvokePermission.Everyone,
					Delivery = RpcDelivery.Unreliable
				};
				FastBufferWriter bufferWriter = __beginSendRpc(700156896u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Unreliable);
				BytePacker.WriteValueBitPacked(bufferWriter, session);
				bufferWriter.WriteValueSafe(in input);
				bufferWriter.WriteValueSafe(in cameraForward);
				bufferWriter.WriteValueSafe(in steeringActive, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 700156896u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Unreliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (IsRiding && SurfRules.AcceptInput(Session, session, Holder, rpcParams.Receive.SenderClientId) && float.IsFinite(input.x) && float.IsFinite(input.y) && Finite(cameraForward))
			{
				_steeringActive = steeringActive;
				_input = (steeringActive ? new Vector2(0f, Mathf.Clamp01(input.y)) : Vector2.zero);
				_paddling.Value = (!Standing & steeringActive) && _input.y > 0.01f;
				_cameraForward = Vector3.ProjectOnPlane(cameraForward, Vector3.up).normalized;
				if (_cameraForward.sqrMagnitude < 0.1f)
				{
					_cameraForward = base.transform.forward;
				}
				_lastInput = TimeNow;
			}
		}

		[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
		public void TogglePoseRpc(uint session, RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcParams rpcParams2 = rpcParams;
				RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
				{
					InvokePermission = RpcInvokePermission.Everyone
				};
				FastBufferWriter bufferWriter = __beginSendRpc(3728721216u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, session);
				__endSendRpc(ref bufferWriter, 3728721216u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (IsRiding && SurfRules.AcceptInput(Session, session, Holder, rpcParams.Receive.SenderClientId) && !(TimeNow - _lastPose < 0.55) && (Standing || ClearForRider(base.transform.position)))
				{
					_lastPose = TimeNow;
					_poseTime.Value = TimeNow;
					_state.Value = (Standing ? SurfboardState.Prone : SurfboardState.Standing);
					_paddling.Value = !Standing && _steeringActive && _input.y > 0.01f;
				}
			}
		}

		[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
		public void RequestReleaseRpc(uint session, RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcParams rpcParams2 = rpcParams;
				RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
				{
					InvokePermission = RpcInvokePermission.Everyone
				};
				FastBufferWriter bufferWriter = __beginSendRpc(3041603206u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, session);
				__endSendRpc(ref bufferWriter, 3041603206u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (SurfRules.AcceptInput(Session, session, Holder, rpcParams.Receive.SenderClientId) && !(TimeNow - _lastRequest < 0.2))
				{
					_lastRequest = TimeNow;
					ServerRelease(SurfExitReason.Drop);
				}
			}
		}

		public bool ServerTryReleasePlayerForRecovery(PPG_Player player, string reason)
		{
			if (!base.IsServer || player == null || Holder != player.OwnerClientId)
			{
				return false;
			}
			ServerRelease((reason == "StuckRecovery") ? SurfExitReason.Stuck : SurfExitReason.Knocked);
			return true;
		}

		public static void ReleasePlayer(PPG_Player player, SurfExitReason reason)
		{
			if (player == null || !player.IsServer)
			{
				return;
			}
			foreach (Surfboard item in new List<Surfboard>(Instances))
			{
				if (item != null && item.IsSpawned && item.Holder == player.OwnerClientId)
				{
					item.ServerRelease(reason);
				}
			}
		}

		public void ServerRelease(SurfExitReason reason)
		{
			if (!base.IsServer || !base.IsSpawned || base.NetworkManager == null || base.NetworkManager.ShutdownInProgress)
			{
				return;
			}
			bool flag = SurfRules.ReturnHome(reason);
			TryHolder(out var player);
			bool flag2 = reason == SurfExitReason.Shore && player != null;
			uint session = Session;
			Vector3 vector = (flag ? _homePosition : base.transform.position);
			if (_state.Value == SurfboardState.Carried && player != null && !flag)
			{
				vector = player.transform.position + player.transform.forward * 1.5f + Vector3.up * 0.3f;
			}
			Quaternion quaternion = (flag ? _homeRotation : Quaternion.Euler(0f, base.transform.eulerAngles.y, 0f));
			if (player != null)
			{
				player.ClientEndSurfRpc(base.NetworkObject, session, flag2);
			}
			_session.Value++;
			_velocity = Vector3.zero;
			_input = Vector2.zero;
			_shallowTime = 0f;
			_steeringActive = false;
			_paddling.Value = false;
			_mountAfter = TimeNow + 1.5;
			_freeSince = TimeNow;
			if (flag2)
			{
				_state.Value = SurfboardState.Carried;
				ServerSetVisibility(visible: true);
				ApplyPresentation();
				return;
			}
			float num = ((SurfboardWorld.Instance != null) ? SurfboardWorld.Instance.SampleHeight(vector) : 0f);
			bool flag3 = !flag && SurfboardWorld.Instance != null && vector.y <= num + 0.7f && Depth(vector, num) > 0.25f;
			_state.Value = ((!flag) ? (flag3 ? SurfboardState.Floating : SurfboardState.Grounded) : SurfboardState.Beached);
			if (flag3)
			{
				vector.y = SurfboardWorld.Instance.SampleHeight(vector) + 0.08f;
			}
			if (Holder != ulong.MaxValue)
			{
				base.ServerDrop(vector, quaternion);
			}
			_holder.Value = ulong.MaxValue;
			CanHold.Value = true;
			CanGaze.Value = true;
			_body.position = vector;
			_body.rotation = quaternion;
			_networkTransform.Teleport(vector, quaternion, base.transform.localScale);
			ApplyPresentation();
		}

		private void OnDisconnected(ulong clientId)
		{
			if (Holder == clientId)
			{
				ServerRelease(SurfExitReason.Disconnect);
			}
		}

		private bool PlayerNearby(float distance)
		{
			if (Singleton<PlayersManager>.Instance == null)
			{
				return false;
			}
			foreach (PPG_Player value in Singleton<PlayersManager>.Instance.Players.Values)
			{
				if (value != null && Vector3.Distance(value.transform.position, base.transform.position) < distance)
				{
					return true;
				}
			}
			return false;
		}

		public float Depth(Vector3 position, float height)
		{
			float num = Mathf.Max(position.y, height) + 2f;
			int num2 = Physics.RaycastNonAlloc(new Vector3(position.x, num, position.z), Vector3.down, _hits, num - height + 30f, _environmentMask, QueryTriggerInteraction.Ignore);
			float num3 = height - 30f;
			for (int i = 0; i < num2; i++)
			{
				if (!Ignore(_hits[i].collider) && _hits[i].point.y > num3)
				{
					num3 = _hits[i].point.y;
				}
			}
			return height - num3;
		}

		private bool Ignore(Collider collider)
		{
			if (!(collider == null) && !collider.transform.IsChildOf(base.transform) && !(collider.GetComponentInParent<PPG_Player>() != null))
			{
				return collider.GetComponentInParent<Surfboard>() != null;
			}
			return true;
		}

		private bool ClearForRider(Vector3 position)
		{
			int num = Physics.OverlapCapsuleNonAlloc(position + Vector3.up * 0.4f, position + Vector3.up * 1.65f, 0.3f, _overlaps, _environmentMask, QueryTriggerInteraction.Ignore);
			if (num == _overlaps.Length)
			{
				return false;
			}
			for (int i = 0; i < num; i++)
			{
				if (!Ignore(_overlaps[i]))
				{
					return false;
				}
			}
			return true;
		}

		public Vector3 SafeExitPosition(Vector3 fallback)
		{
			Vector3[] array = new Vector3[3]
			{
				base.transform.right,
				-base.transform.right,
				-base.transform.forward
			};
			foreach (Vector3 vector in array)
			{
				Vector3 vector2 = base.transform.position + Vector3.ProjectOnPlane(vector, Vector3.up).normalized * 1.25f + Vector3.up * 0.25f;
				if (ClearForRider(vector2))
				{
					return vector2;
				}
			}
			return fallback + Vector3.up * 0.35f;
		}

		private void Simulate(SurfboardWorld world, float dt)
		{
			Vector3 position = _body.position;
			Vector3 normalized = Vector3.ProjectOnPlane(_body.rotation * Vector3.forward, Vector3.up).normalized;
			float t = Mathf.Clamp01(_velocity.magnitude / 8f);
			float num = (Standing ? Mathf.Lerp(45f, 150f, t) : 480f);
			float angle = (_steeringActive ? Mathf.Clamp(Vector3.SignedAngle(normalized, _cameraForward, Vector3.up), (0f - num) * dt, num * dt) : 0f);
			if (!Standing && _steeringActive)
			{
				float current = Mathf.Atan2(normalized.x, normalized.z) * 57.29578f;
				float target = Mathf.Atan2(_cameraForward.x, _cameraForward.z) * 57.29578f;
				float target2 = Mathf.SmoothDampAngle(current, target, ref _proneYawVelocity, 0.12f, num, dt);
				angle = Mathf.DeltaAngle(current, target2);
			}
			else
			{
				_proneYawVelocity = 0f;
			}
			Quaternion quaternion = Quaternion.AngleAxis(angle, Vector3.up);
			normalized = quaternion * normalized;
			if (!Standing)
			{
				Vector3 target3 = ((_steeringActive && _input.y > 0.01f) ? (normalized * 3.8f) : Vector3.zero);
				_velocity = Vector3.MoveTowards(_velocity, target3, (float)((target3.sqrMagnitude > 0.001f) ? 18 : 12) * dt);
			}
			else
			{
				_velocity = quaternion * _velocity;
				Vector3 planeNormal = SurfaceNormal(world, position, normalized);
				_velocity += Vector3.ProjectOnPlane(Vector3.down * 9.81f, planeNormal) * dt;
				_velocity.y = 0f;
				SurfWave activeWave = world.ActiveWave;
				float num2 = activeWave.CatchStrength(position, normalized, world.TimeNow);
				float num3 = Vector3.Dot(position - activeWave.Center(world.TimeNow), activeWave.Direction);
				float num4 = Mathf.Clamp((Mathf.Max(0f, activeWave.Speed + (activeWave.Length * 0.18f - num3) * 0.8f) - Vector3.Dot(_velocity, activeWave.Direction)) * 4f, -12f, 12f);
				_velocity += activeWave.Direction * num4 * num2 * dt;
				float magnitude = _velocity.magnitude;
				_velocity = Vector3.RotateTowards(_velocity, normalized * magnitude, Mathf.Lerp(2f, 5f, t) * dt, 0f);
				_velocity *= Mathf.Exp(-0.18f * dt);
				_velocity = Vector3.ClampMagnitude(_velocity, 8f);
			}
			Vector3 vector = _velocity * dt;
			if (vector.sqrMagnitude > 1E-05f)
			{
				int num5 = Physics.BoxCastNonAlloc(position + Vector3.up * (Standing ? 0.95f : 0.45f), new Vector3(0.45f, Standing ? 0.85f : 0.35f, 1.1f), vector.normalized, _hits, Quaternion.LookRotation(normalized), vector.magnitude + 0.08f, _environmentMask, QueryTriggerInteraction.Ignore);
				for (int i = 0; i < num5; i++)
				{
					if (!Ignore(_hits[i].collider))
					{
						vector = Vector3.zero;
						_velocity *= 0.3f;
						break;
					}
				}
			}
			FollowSurface(world, dt, position + vector, normalized);
		}

		private Vector3 SurfaceNormal(SurfboardWorld world, Vector3 position, Vector3 forward)
		{
			if (_gpuSurface != null && _gpuSurface.TrySample(position, out var _, out var normal))
			{
				return normal;
			}
			Vector3 vector = Vector3.Cross(Vector3.up, forward);
			Vector3 lhs = forward * 2f;
			lhs.y = world.SampleHeight(position + forward) - world.SampleHeight(position - forward);
			Vector3 rhs = vector * 0.7f;
			rhs.y = world.SampleHeight(position + vector * 0.35f) - world.SampleHeight(position - vector * 0.35f);
			return Vector3.Cross(lhs, rhs).normalized;
		}

		private void FollowSurface(SurfboardWorld world, float dt, Vector3 position, Vector3 forward)
		{
			Vector3 b = SurfaceNormal(world, position, forward);
			position.y = Mathf.Lerp(_body.position.y, SampleSurfaceHeight(world, position) + 0.08f, 1f - Mathf.Exp(-35f * dt));
			b = Vector3.Slerp(_body.rotation * Vector3.up, b, 1f - Mathf.Exp(-8f * dt)).normalized;
			forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
			Vector3 forward2 = forward - Vector3.up * (Vector3.Dot(forward, b) / Mathf.Max(0.2f, b.y));
			_body.MovePosition(position);
			_body.MoveRotation(Quaternion.LookRotation(forward2, b));
		}

		private static bool Finite(Vector3 value)
		{
			if (float.IsFinite(value.x) && float.IsFinite(value.y))
			{
				return float.IsFinite(value.z);
			}
			return false;
		}

		protected override void __initializeVariables()
		{
			if (_spawnIndex == null)
			{
				throw new Exception("Surfboard._spawnIndex cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_spawnIndex.Initialize(this);
			__nameNetworkVariable(_spawnIndex, "_spawnIndex");
			NetworkVariableFields.Add(_spawnIndex);
			if (_state == null)
			{
				throw new Exception("Surfboard._state cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_state.Initialize(this);
			__nameNetworkVariable(_state, "_state");
			NetworkVariableFields.Add(_state);
			if (_holder == null)
			{
				throw new Exception("Surfboard._holder cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_holder.Initialize(this);
			__nameNetworkVariable(_holder, "_holder");
			NetworkVariableFields.Add(_holder);
			if (_session == null)
			{
				throw new Exception("Surfboard._session cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_session.Initialize(this);
			__nameNetworkVariable(_session, "_session");
			NetworkVariableFields.Add(_session);
			if (_poseTime == null)
			{
				throw new Exception("Surfboard._poseTime cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_poseTime.Initialize(this);
			__nameNetworkVariable(_poseTime, "_poseTime");
			NetworkVariableFields.Add(_poseTime);
			if (_paddling == null)
			{
				throw new Exception("Surfboard._paddling cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_paddling.Initialize(this);
			__nameNetworkVariable(_paddling, "_paddling");
			NetworkVariableFields.Add(_paddling);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(700156896u, __rpc_handler_700156896, "SubmitInputRpc", RpcInvokePermission.Everyone);
			__registerRpc(3728721216u, __rpc_handler_3728721216, "TogglePoseRpc", RpcInvokePermission.Everyone);
			__registerRpc(3041603206u, __rpc_handler_3041603206, "RequestReleaseRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_700156896(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				reader.ReadValueSafe(out Vector2 value2);
				reader.ReadValueSafe(out Vector3 value3);
				reader.ReadValueSafe(out bool value4, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((Surfboard)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Surfboard)target).SubmitInputRpc(value, value2, value3, value4, ext);
				((Surfboard)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3728721216(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				RpcParams ext = rpcParams.Ext;
				((Surfboard)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Surfboard)target).TogglePoseRpc(value, ext);
				((Surfboard)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3041603206(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				RpcParams ext = rpcParams.Ext;
				((Surfboard)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Surfboard)target).RequestReleaseRpc(value, ext);
				((Surfboard)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "Surfboard";
		}
	}
}
