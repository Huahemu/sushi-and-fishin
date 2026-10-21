using System;
using System.Linq;
using Assets.Scripts.Core.ProcessedIngredient;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Kitchen.Wholasale;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Restaurant;
using Unity.Collections;
using Unity.Netcode;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.AI;

namespace PaperPixelGames.Core.Staff
{
	[RequireComponent(typeof(NetworkObject), typeof(NavMeshAgent))]
	public sealed class StaffNpc : NetworkBehaviour
	{
		public Transform CarryPoint;

		public RuntimeAnimatorController AnimatorController;

		[Min(0.1f)]
		public float WalkReferenceSpeed = 3f;

		[Min(0.1f)]
		public float RunReferenceSpeed = 7f;

		[Min(0.01f)]
		public float AnimationDamping = 0.1f;

		public readonly NetworkVariable<int> EmployeeId = new NetworkVariable<int>(0);

		private readonly NetworkVariable<int> characterId = new NetworkVariable<int>(-1);

		private readonly NetworkVariable<FixedString64Bytes> heldItemId = new NetworkVariable<FixedString64Bytes>();

		private readonly NetworkVariable<int> heldContentsCount = new NetworkVariable<int>(-1);

		private readonly NetworkVariable<bool> working = new NetworkVariable<bool>(value: false);

		private readonly NetworkVariable<FixedString512Bytes> trayContents = new NetworkVariable<FixedString512Bytes>();

		private readonly NetworkVariable<float> workSpeed = new NetworkVariable<float>(1f);

		private readonly NetworkVariable<StaffRole> role = new NetworkVariable<StaffRole>(StaffRole.SushiChef);

		private readonly NetworkVariable<float> moveSpeed = new NetworkVariable<float>(0f);

		private readonly NetworkVariable<bool> running = new NetworkVariable<bool>(value: false);

		private readonly NetworkVariable<Vector2> moveDirection = new NetworkVariable<Vector2>();

		private ResidentMovementController movement;

		private NavMeshAgent agent;

		private Animator animator;

		private GameObject visual;

		private GameObject heldVisual;

		private Transform rightHand;

		private HoldableSO heldData;

		private bool heldContentsDirty;

		private bool heldVisualDirty;

		private static int lastModelBuildFrame = -1;

		private static readonly ProfilerMarker HeldVisualMarker = new ProfilerMarker("Staff.Npc.RefreshHeldVisual");

		private static readonly ProfilerMarker PathMarker = new ProfilerMarker("Staff.Npc.Path");

		private int heldType;

		private Vector3 destination;

		private float blockedSince = -1f;

		private float lastRemaining = float.MaxValue;

		private float retryAt;

		private bool moving;

		private float requestedMoveSpeed;

		private float turnSpeed = 1080f;

		private float arrivalRadius = 0.55f;

		private float turnInPlaceAngle = 75f;

		public bool IsRunning
		{
			get
			{
				if (running.Value)
				{
					return moveSpeed.Value > 0.05f;
				}
				return false;
			}
		}

		public bool Recovered { get; private set; }

		public bool InvalidTarget { get; private set; }

		public int RecoveryCount { get; private set; }

		public void SetRunning(bool value)
		{
			if (base.IsServer)
			{
				running.Value = value;
			}
		}

		private void Awake()
		{
			agent = GetComponent<NavMeshAgent>();
			agent.enabled = false;
		}

		protected override void OnNetworkPreSpawn(ref NetworkManager networkManager)
		{
			base.OnNetworkPreSpawn(ref networkManager);
			agent.enabled = false;
			movement = new ResidentMovementController(base.transform, agent);
			if (networkManager.IsServer)
			{
				movement.TrySnapToNavMesh(base.transform.position, 2f, out var _);
			}
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			NetworkVariable<int> networkVariable = characterId;
			networkVariable.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Combine(networkVariable.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(CharacterChanged));
			NetworkVariable<FixedString64Bytes> networkVariable2 = heldItemId;
			networkVariable2.OnValueChanged = (NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate)Delegate.Combine(networkVariable2.OnValueChanged, new NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate(HeldChanged));
			NetworkVariable<int> networkVariable3 = heldContentsCount;
			networkVariable3.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Combine(networkVariable3.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(HeldContentsChanged));
			NetworkVariable<FixedString512Bytes> networkVariable4 = trayContents;
			networkVariable4.OnValueChanged = (NetworkVariable<FixedString512Bytes>.OnValueChangedDelegate)Delegate.Combine(networkVariable4.OnValueChanged, new NetworkVariable<FixedString512Bytes>.OnValueChangedDelegate(TrayContentsChanged));
			TryCreateVisual();
		}

		public void Initialize(StaffEmployeeEntry employee)
		{
			EmployeeId.Value = employee.EmployeeId;
			role.Value = employee.Role;
			characterId.Value = employee.CustomizationId;
			TryCreateVisual();
		}

		private void CharacterChanged(int before, int after)
		{
			TryCreateVisual();
		}

		private void TrayContentsChanged(FixedString512Bytes before, FixedString512Bytes after)
		{
			heldVisualDirty = true;
		}

		private void HeldChanged(FixedString64Bytes before, FixedString64Bytes after)
		{
			heldVisualDirty = true;
		}

		private void HeldContentsChanged(int before, int after)
		{
			heldContentsDirty = true;
		}

		private void TryCreateVisual()
		{
			if (visual != null || StaffManager.Instance?.Catalog == null || lastModelBuildFrame == Time.frameCount)
			{
				return;
			}
			StaffCustomizationOption staffCustomizationOption = StaffManager.Instance.Catalog.ResolveCharacter(role.Value, characterId.Value);
			if (!(staffCustomizationOption?.ResidentData?.BakedVisualPrefab == null))
			{
				lastModelBuildFrame = Time.frameCount;
				visual = UnityEngine.Object.Instantiate(staffCustomizationOption.ResidentData.BakedVisualPrefab, base.transform);
				visual.transform.localPosition = Vector3.zero;
				visual.transform.localRotation = Quaternion.identity;
				animator = visual.GetComponentsInChildren<Animator>(includeInactive: true).FirstOrDefault((Animator a) => a.avatar != null && a.avatar.isHuman);
				if (animator == null)
				{
					animator = visual.AddComponent<Animator>();
				}
				if (AnimatorController != null)
				{
					animator.runtimeAnimatorController = AnimatorController;
				}
				animator.applyRootMotion = false;
				animator.enabled = true;
				animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
				animator.fireEvents = false;
				animator.Rebind();
				rightHand = (animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null);
				if (animator.layerCount > 1)
				{
					animator.SetLayerWeight(1, 0f);
				}
				Collider[] componentsInChildren = visual.GetComponentsInChildren<Collider>();
				for (int num = 0; num < componentsInChildren.Length; num++)
				{
					componentsInChildren[num].enabled = false;
				}
				heldVisualDirty = true;
			}
		}

		private void RefreshHeldVisual()
		{
			using (HeldVisualMarker.Auto())
			{
				heldVisualDirty = false;
				heldType = 0;
				heldData = null;
				if (heldVisual != null)
				{
					UnityEngine.Object.Destroy(heldVisual);
				}
				StaffWorkplaceRegistry instance = StaffWorkplaceRegistry.Instance;
				if (instance?.Database == null || heldItemId.Value.IsEmpty || !(instance.Database.FindById(heldItemId.Value.ToString()) is HoldableSO holdableSO))
				{
					return;
				}
				heldType = holdableSO.HoldType;
				heldData = holdableSO;
				GameObject graphicObjectPrefab = holdableSO.GraphicObjectPrefab;
				if (graphicObjectPrefab == null)
				{
					return;
				}
				heldVisual = UnityEngine.Object.Instantiate(graphicObjectPrefab, (rightHand != null) ? rightHand : ((CarryPoint != null) ? CarryPoint : base.transform));
				holdableSO.ApplyTpsHoldOffset(heldVisual.transform);
				if (!trayContents.Value.IsEmpty && holdableSO.Prefab != null && holdableSO.Prefab.TryGetComponent<Tray>(out var component))
				{
					string[] array = trayContents.Value.ToString().Split('|');
					Transform[] componentsInChildren = heldVisual.GetComponentsInChildren<Transform>(includeInactive: true);
					foreach (Transform transform in componentsInChildren)
					{
						if (transform.name.ToLowerInvariant().Contains("dirty"))
						{
							transform.gameObject.SetActive(array[0] == "1");
						}
					}
					byte b = 0;
					while (b < 3 && b + 1 < array.Length)
					{
						if (instance.Database.FindById(array[b + 1]) is HoldableSO holdableSO2 && !(holdableSO2.GraphicObjectPrefab == null))
						{
							SnapPoint snapPointByIndex = component.GetSnapPointByIndex(b);
							if (!(snapPointByIndex == null))
							{
								GameObject obj = UnityEngine.Object.Instantiate(holdableSO2.GraphicObjectPrefab, heldVisual.transform);
								obj.transform.localPosition = component.transform.InverseTransformPoint(snapPointByIndex.transform.position);
								obj.transform.localRotation = Quaternion.Inverse(component.transform.rotation) * snapPointByIndex.transform.rotation;
							}
						}
						b++;
					}
				}
				Collider[] componentsInChildren2 = heldVisual.GetComponentsInChildren<Collider>(includeInactive: true);
				for (int i = 0; i < componentsInChildren2.Length; i++)
				{
					componentsInChildren2[i].enabled = false;
				}
				RefreshHeldContents();
			}
		}

		private void RefreshHeldContents()
		{
			heldContentsDirty = false;
			if (!(heldVisual == null) && !(heldData?.Prefab == null) && heldContentsCount.Value >= 0)
			{
				SideDishIngredientEntity component2;
				if (heldData.Prefab.TryGetComponent<WholesaleEntity>(out var component))
				{
					component.ApplyStaffHeldContents(heldVisual, (byte)Mathf.Clamp(heldContentsCount.Value, 0, 255));
				}
				else if (heldData.Prefab.TryGetComponent<SideDishIngredientEntity>(out component2))
				{
					component2.ApplyStaffHeldContents(heldVisual, heldContentsCount.Value);
				}
			}
		}

		public void SetWorking(bool value, float speed)
		{
			if (this.CanWriteServerState())
			{
				working.Value = value;
				workSpeed.Value = speed;
			}
		}

		public void SetHeld(string id)
		{
			if (this.CanWriteServerState())
			{
				trayContents.Value = default;
				heldContentsCount.Value = -1;
				heldItemId.Value = id ?? string.Empty;
			}
		}

		public void SetHeldItem(BaseHoldable item)
		{
			if (this.CanWriteServerState())
			{
				if (item is Tray heldTray)
				{
					SetHeldTray(heldTray);
					return;
				}
				heldItemId.Value = ((item != null) ? item.ItemSO.Id : string.Empty);
				trayContents.Value = default;
				heldContentsCount.Value = ((item is WholesaleEntity wholesaleEntity) ? wholesaleEntity.CurrentCount : ((item is SideDishIngredientEntity sideDishIngredientEntity) ? sideDishIngredientEntity.IngredientCount : (-1)));
			}
		}

		public void SetHeldTray(Tray tray)
		{
			if (this.CanWriteServerState())
			{
				heldContentsCount.Value = -1;
				heldItemId.Value = ((tray != null) ? tray.ItemSO.Id : string.Empty);
				trayContents.Value = ((tray == null) ? string.Empty : ((tray.IsDirty.Value ? "1" : "0") + "|" + tray.ServerStaffDish(0)?.ItemSO?.Id + "|" + tray.ServerStaffDish(1)?.ItemSO?.Id + "|" + tray.ServerStaffDish(2)?.ItemSO?.Id));
			}
		}

		public void FacePoint(Transform point)
		{
			if (this.CanWriteServerState() && !(point == null))
			{
				Stop();
				if (agent != null)
				{
					agent.updateRotation = false;
				}
				base.transform.rotation = Quaternion.Euler(0f, point.eulerAngles.y, 0f);
			}
		}

		public bool MoveTo(Transform point, float speed)
		{
			if (point == null)
			{
				return false;
			}
			bool num = MoveTo(point.position, speed);
			if (num)
			{
				FacePoint(point);
			}
			return num;
		}

		public bool MoveTo(Vector3 target, float speed, float arrivalTolerance = -1f)
		{
			Recovered = false;
			InvalidTarget = false;
			if (!base.IsServer || agent == null)
			{
				return false;
			}
			StaffWorkplaceRegistry instance = StaffWorkplaceRegistry.Instance;
			instance?.ReleaseWaitingPointIfMovingElsewhere(EmployeeId.Value, target);
			if (instance != null && instance.OwnsWaitingDestination(EmployeeId.Value, target))
			{
				arrivalTolerance = ((arrivalTolerance > 0f) ? Mathf.Min(arrivalTolerance, instance.WaitingArrivalRadius) : instance.WaitingArrivalRadius);
			}
			ConfigureMovement(speed, arrivalTolerance);
			if (!NavMesh.SamplePosition(target, out var hit, 2f, new NavMeshQueryFilter
			{
				agentTypeID = agent.agentTypeID,
				areaMask = agent.areaMask
			}))
			{
				StaffWorkplaceRegistry.Instance?.ReleaseWaitingPoint(EmployeeId.Value);
				InvalidTarget = true;
				Stop();
				return false;
			}
			target = hit.position;
			if (ReachedDestination(target))
			{
				Stop();
				return true;
			}
			if (!moving || (destination - target).sqrMagnitude > 0.0025f)
			{
				destination = target;
				moving = true;
				blockedSince = Time.time;
				lastRemaining = float.MaxValue;
				retryAt = 0f;
			}
			float num = Vector3.Distance(base.transform.position, target);
			if (num < lastRemaining - 0.1f)
			{
				lastRemaining = num;
				blockedSince = Time.time;
			}
			if (Time.time >= retryAt)
			{
				using (PathMarker.Auto())
				{
					movement.MoveTo(target, speed, 2f, requireCompletePath: true);
					agent.updateRotation = false;
					retryAt = Time.time + Mathf.Max(0.1f, StaffManager.Instance?.Catalog?.PathRetryInterval ?? 1f);
				}
			}
			if (Time.time - blockedSince >= Mathf.Max(0.1f, StaffManager.Instance?.Catalog?.RecoveryTimeout ?? 10f))
			{
				if (movement.Warp(target, base.transform.rotation))
				{
					Stop();
					Recovered = true;
					RecoveryCount++;
					return true;
				}
				blockedSince = Time.time;
			}
			return false;
		}

		public void Stop()
		{
			moving = false;
			movement?.Stop();
			if (agent != null && agent.enabled && agent.isOnNavMesh)
			{
				agent.velocity = Vector3.zero;
			}
		}

		private void Update()
		{
			if (!base.IsSpawned)
			{
				return;
			}
			TryCreateVisual();
			if (base.IsServer && agent != null)
			{
				UpdateMovement();
				Vector3 vector = ((agent.enabled && agent.isOnNavMesh) ? Vector3.ProjectOnPlane(agent.velocity, Vector3.up) : Vector3.zero);
				moveSpeed.Value = vector.magnitude;
				Vector3 vector2 = base.transform.InverseTransformDirection(vector.normalized);
				moveDirection.Value = new Vector2(vector2.x, vector2.z);
			}
			if (!(animator == null) && !(animator.runtimeAnimatorController == null))
			{
				float value = moveSpeed.Value;
				float num = ((value <= 0.05f) ? 0f : ((value <= WalkReferenceSpeed) ? (0.5f * value / WalkReferenceSpeed) : Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(WalkReferenceSpeed, RunReferenceSpeed, value))));
				if (running.Value && value > 0.05f)
				{
					num = 1f;
				}
				float b = ((num <= 0.5f) ? (num * 4f) : Mathf.Lerp(2f, 4.5f, (num - 0.5f) * 2f));
				animator.SetFloat("MoveX", moveDirection.Value.x, AnimationDamping, Time.deltaTime);
				animator.SetFloat("MoveY", moveDirection.Value.y, AnimationDamping, Time.deltaTime);
				animator.SetFloat("Speed01", num, AnimationDamping, Time.deltaTime);
				animator.SetFloat("LocomotionPlaybackSpeed", (value <= 0.05f) ? 1f : Mathf.Clamp(value / Mathf.Max(0.001f, b), running.Value ? 0.25f : 1f, 2.5f), AnimationDamping, Time.deltaTime);
				animator.SetBool("IsHolding", working.Value || !heldItemId.Value.IsEmpty);
				animator.SetInteger("HoldType", heldType);
				if (animator.layerCount > 1)
				{
					animator.SetLayerWeight(1, Mathf.MoveTowards(animator.GetLayerWeight(1), (working.Value || !heldItemId.Value.IsEmpty) ? 1 : 0, Time.deltaTime / Mathf.Max(0.01f, AnimationDamping)));
				}
			}
		}

		private void LateUpdate()
		{
			if (heldVisualDirty && base.IsSpawned && visual != null)
			{
				RefreshHeldVisual();
			}
			else if (heldContentsDirty && base.IsSpawned && heldVisual != null)
			{
				RefreshHeldContents();
			}
			if (working.Value && !(animator == null) && animator.isHuman)
			{
				Transform boneTransform = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
				if (boneTransform != null)
				{
					boneTransform.localRotation *= Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * workSpeed.Value * 7f) * 18f);
				}
			}
		}

		public override void OnNetworkDespawn()
		{
			if (base.IsServer)
			{
				StaffWorkplaceRegistry.Instance?.ReleaseWaitingPoint(EmployeeId.Value);
			}
			agent.enabled = false;
			NetworkVariable<int> networkVariable = characterId;
			networkVariable.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Remove(networkVariable.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(CharacterChanged));
			NetworkVariable<FixedString64Bytes> networkVariable2 = heldItemId;
			networkVariable2.OnValueChanged = (NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate)Delegate.Remove(networkVariable2.OnValueChanged, new NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate(HeldChanged));
			NetworkVariable<int> networkVariable3 = heldContentsCount;
			networkVariable3.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Remove(networkVariable3.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(HeldContentsChanged));
			NetworkVariable<FixedString512Bytes> networkVariable4 = trayContents;
			networkVariable4.OnValueChanged = (NetworkVariable<FixedString512Bytes>.OnValueChangedDelegate)Delegate.Remove(networkVariable4.OnValueChanged, new NetworkVariable<FixedString512Bytes>.OnValueChangedDelegate(TrayContentsChanged));
			if (visual != null)
			{
				UnityEngine.Object.Destroy(visual);
			}
			if (heldVisual != null)
			{
				UnityEngine.Object.Destroy(heldVisual);
			}
			base.OnNetworkDespawn();
		}

		private void ConfigureMovement(float speed, float arrivalTolerance)
		{
			StaffCatalogSO staffCatalogSO = StaffManager.Instance?.Catalog;
			requestedMoveSpeed = Mathf.Max(0.1f, speed);
			turnSpeed = Mathf.Max(90f, (staffCatalogSO != null) ? staffCatalogSO.StaffTurnSpeed : 1080f);
			arrivalRadius = Mathf.Clamp((staffCatalogSO != null) ? staffCatalogSO.StaffArrivalRadius : 0.55f, 0.1f, 0.65f);
			if (arrivalTolerance > 0f)
			{
				arrivalRadius = Mathf.Clamp(arrivalTolerance, 0.03f, arrivalRadius);
			}
			turnInPlaceAngle = Mathf.Clamp((staffCatalogSO != null) ? staffCatalogSO.StaffTurnInPlaceAngle : 75f, 30f, 120f);
			agent.acceleration = Mathf.Max(1f, (staffCatalogSO != null) ? staffCatalogSO.StaffAcceleration : 30f);
			agent.angularSpeed = turnSpeed;
			agent.autoBraking = true;
			agent.stoppingDistance = arrivalRadius * 0.8f;
		}

		private bool ReachedDestination(Vector3 point)
		{
			if ((base.transform.position - point).sqrMagnitude > arrivalRadius * arrivalRadius)
			{
				return false;
			}
			if (!agent.pathPending)
			{
				if (agent.hasPath)
				{
					return agent.remainingDistance <= arrivalRadius + 0.1f;
				}
				return true;
			}
			return false;
		}

		private void UpdateMovement()
		{
			if (!moving || !agent.enabled || !agent.isOnNavMesh || agent.pathPending)
			{
				return;
			}
			if (ReachedDestination(destination))
			{
				Stop();
			}
			else
			{
				if (!agent.hasPath)
				{
					return;
				}
				agent.updateRotation = false;
				Vector3 vector = Vector3.ProjectOnPlane(agent.steeringTarget - base.transform.position, Vector3.up);
				if (vector.sqrMagnitude < 0.0001f)
				{
					return;
				}
				float num = Vector3.Angle(base.transform.forward, vector);
				base.transform.rotation = Quaternion.RotateTowards(base.transform.rotation, Quaternion.LookRotation(vector), turnSpeed * Time.deltaTime);
				if (num > turnInPlaceAngle)
				{
					agent.speed = 0f;
					agent.velocity = Vector3.zero;
					return;
				}
				float num2 = agent.remainingDistance;
				if (float.IsInfinity(num2) || float.IsNaN(num2))
				{
					num2 = Vector3.Distance(base.transform.position, destination);
				}
				float b = Mathf.Sqrt(2f * agent.acceleration * Mathf.Max(0f, num2 - agent.stoppingDistance));
				float num3 = Mathf.Lerp(0.25f, 1f, Mathf.InverseLerp(turnInPlaceAngle, 0f, num));
				agent.speed = Mathf.Min(requestedMoveSpeed, b) * num3;
			}
		}

		protected override void __initializeVariables()
		{
			if (EmployeeId == null)
			{
				throw new Exception("StaffNpc.EmployeeId cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			EmployeeId.Initialize(this);
			__nameNetworkVariable(EmployeeId, "EmployeeId");
			NetworkVariableFields.Add(EmployeeId);
			if (characterId == null)
			{
				throw new Exception("StaffNpc.characterId cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			characterId.Initialize(this);
			__nameNetworkVariable(characterId, "characterId");
			NetworkVariableFields.Add(characterId);
			if (heldItemId == null)
			{
				throw new Exception("StaffNpc.heldItemId cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			heldItemId.Initialize(this);
			__nameNetworkVariable(heldItemId, "heldItemId");
			NetworkVariableFields.Add(heldItemId);
			if (heldContentsCount == null)
			{
				throw new Exception("StaffNpc.heldContentsCount cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			heldContentsCount.Initialize(this);
			__nameNetworkVariable(heldContentsCount, "heldContentsCount");
			NetworkVariableFields.Add(heldContentsCount);
			if (working == null)
			{
				throw new Exception("StaffNpc.working cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			working.Initialize(this);
			__nameNetworkVariable(working, "working");
			NetworkVariableFields.Add(working);
			if (trayContents == null)
			{
				throw new Exception("StaffNpc.trayContents cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			trayContents.Initialize(this);
			__nameNetworkVariable(trayContents, "trayContents");
			NetworkVariableFields.Add(trayContents);
			if (workSpeed == null)
			{
				throw new Exception("StaffNpc.workSpeed cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			workSpeed.Initialize(this);
			__nameNetworkVariable(workSpeed, "workSpeed");
			NetworkVariableFields.Add(workSpeed);
			if (role == null)
			{
				throw new Exception("StaffNpc.role cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			role.Initialize(this);
			__nameNetworkVariable(role, "role");
			NetworkVariableFields.Add(role);
			if (moveSpeed == null)
			{
				throw new Exception("StaffNpc.moveSpeed cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			moveSpeed.Initialize(this);
			__nameNetworkVariable(moveSpeed, "moveSpeed");
			NetworkVariableFields.Add(moveSpeed);
			if (running == null)
			{
				throw new Exception("StaffNpc.running cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			running.Initialize(this);
			__nameNetworkVariable(running, "running");
			NetworkVariableFields.Add(running);
			if (moveDirection == null)
			{
				throw new Exception("StaffNpc.moveDirection cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			moveDirection.Initialize(this);
			__nameNetworkVariable(moveDirection, "moveDirection");
			NetworkVariableFields.Add(moveDirection);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			base.__initializeRpcs();
		}

		protected override string __getTypeName()
		{
			return "StaffNpc";
		}
	}
}
