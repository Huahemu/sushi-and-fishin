using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Player;
using StylizedWater3.UnderwaterRendering;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace PaperPixelGames.Core.Lighthouse
{
	public class Portal : MonoBehaviour
	{
		private enum PortalTeleportMode
		{
			Disabled = 0,
			Simple = 1,
			Traveller = 2
		}

		private static readonly List<Portal> activePortals = new List<Portal>();

		private static bool isRenderingPortals;

		private static readonly Matrix4x4 portalHalfTurn = Matrix4x4.Rotate(Quaternion.Euler(0f, 180f, 0f));

		[Header("Main Settings")]
		public Portal linkedPortal;

		public MeshRenderer screen;

		[Min(1f)]
		public int recursionLimit = 2;

		[SerializeField]
		[Min(1f)]
		private int maximumRuntimeRecursion = 2;

		[Header("Advanced Settings")]
		public float nearClipOffset = 0.05f;

		public float nearClipLimit = 0.2f;

		[Header("Teleport")]
		[SerializeField]
		private PortalTeleportMode teleportMode = PortalTeleportMode.Simple;

		[SerializeField]
		private bool allowSimpleTeleportFromForwardSideOnly;

		[SerializeField]
		[Min(0.05f)]
		private float teleportExitOffset = 0.15f;

		[FormerlySerializedAs("enableTravellerTeleportation")]
		[SerializeField]
		[HideInInspector]
		private bool legacyTravellerTeleportation;

		[FormerlySerializedAs("enableSimpleTeleport")]
		[SerializeField]
		[HideInInspector]
		private bool legacySimpleTeleport = true;

		[SerializeField]
		[HideInInspector]
		private bool teleportModeMigrated;

		[Header("Underwater Fog")]
		[Tooltip("Optional area whose fog changes when the local player teleports through this portal.")]
		[SerializeField]
		private UnderwaterArea teleportUnderwaterArea;

		[SerializeField]
		[Range(0.01f, 3f)]
		private float fogDensityAfterTeleport = 0.8f;

		[Header("Rendering Performance")]
		[SerializeField]
		private bool enablePortalRendering = true;

		[SerializeField]
		[Min(0f)]
		private float maxRenderDistance = 60f;

		[SerializeField]
		[Range(0.1f, 1f)]
		private float renderTextureResolutionScale = 0.75f;

		[SerializeField]
		[Min(0f)]
		private float minimumRenderInterval;

		[SerializeField]
		private bool hideScreenWhenRenderCulled = true;

		private RenderTexture viewTexture;

		private Camera portalCam;

		private Camera playerCam;

		private Material firstRecursionMat;

		private Material screenMaterial;

		private List<PPG_Player> trackedTravellers;

		private readonly Dictionary<PPG_Player, int> simpleTeleportTravellers = new Dictionary<PPG_Player, int>();

		private readonly Dictionary<PPG_Player, float> simpleTeleportCooldownUntil = new Dictionary<PPG_Player, float>();

		private MeshFilter screenMeshFilter;

		private readonly UniversalRenderPipeline.SingleCameraRequest renderRequest = new UniversalRenderPipeline.SingleCameraRequest();

		private Vector3[] renderPositions;

		private Quaternion[] renderRotations;

		private float nextAllowedRenderTime;

		private bool runtimeTeleportEnabled = true;

		private bool runtimeVisualsEnabled = true;

		private static readonly List<Portal> itemTeleportPortals = new List<Portal>();

		private bool RuntimeVisualsActive
		{
			get
			{
				if (runtimeVisualsEnabled)
				{
					return base.isActiveAndEnabled;
				}
				return false;
			}
		}

		private bool UsesSimpleTeleport => teleportMode == PortalTeleportMode.Simple;

		private bool UsesTravellerTeleport => teleportMode == PortalTeleportMode.Traveller;

		private Vector3 portalCamPos => portalCam.transform.position;

		public void SetRuntimeState(bool teleportEnabled, bool visualsEnabled)
		{
			ApplyRuntimeState(teleportEnabled, visualsEnabled);
			if (linkedPortal != null)
			{
				linkedPortal.ApplyRuntimeState(teleportEnabled, visualsEnabled);
			}
		}

		private void ApplyRuntimeState(bool teleportEnabled, bool visualsEnabled)
		{
			runtimeTeleportEnabled = teleportEnabled;
			runtimeVisualsEnabled = visualsEnabled;
			if (!runtimeTeleportEnabled)
			{
				ClearTrackedTravellers();
			}
			if (!runtimeVisualsEnabled)
			{
				ReleaseViewTexture();
			}
			SetRendererEnabled(screen, runtimeVisualsEnabled);
			RefreshRenderRegistration();
		}

		private void ClearTrackedTravellers()
		{
			if (trackedTravellers != null)
			{
				for (int num = trackedTravellers.Count - 1; num >= 0; num--)
				{
					PPG_Player pPG_Player = trackedTravellers[num];
					if (pPG_Player != null)
					{
						pPG_Player.ExitPortalThreshold();
					}
				}
				trackedTravellers.Clear();
			}
			simpleTeleportTravellers.Clear();
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
			activePortals.Clear();
			itemTeleportPortals.Clear();
			isRenderingPortals = false;
		}

		private void Awake()
		{
			MigrateLegacyTeleportMode();
			playerCam = Camera.main;
			portalCam = GetComponentInChildren<Camera>();
			if (linkedPortal == null || screen == null || portalCam == null)
			{
				Debug.LogError(base.name + ": Portal requires a linked portal, Screen MeshRenderer, and child Camera.", this);
				base.enabled = false;
				return;
			}
			portalCam.enabled = false;
			AudioListener component = portalCam.GetComponent<AudioListener>();
			if (component != null)
			{
				component.enabled = false;
			}
			trackedTravellers = new List<PPG_Player>();
			screenMeshFilter = screen.GetComponent<MeshFilter>();
			if (screenMeshFilter == null)
			{
				Debug.LogError(base.name + ": The portal Screen requires a MeshFilter.", screen);
				base.enabled = false;
			}
			else
			{
				GetScreenMaterial()?.SetInt("displayMask", 1);
			}
		}

		private void OnEnable()
		{
			if (!itemTeleportPortals.Contains(this))
			{
				itemTeleportPortals.Add(this);
			}
			if (portalCam != null)
			{
				portalCam.enabled = false;
			}
			RefreshRenderRegistration();
		}

		private void OnDisable()
		{
			itemTeleportPortals.Remove(this);
			activePortals.Remove(this);
			if (activePortals.Count == 0)
			{
				RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
			}
		}

		private void RefreshRenderRegistration()
		{
			if (RuntimeVisualsActive)
			{
				if (!activePortals.Contains(this))
				{
					activePortals.Add(this);
				}
			}
			else
			{
				activePortals.Remove(this);
			}
			RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
			if (activePortals.Count > 0)
			{
				RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
			}
		}

		private void OnDestroy()
		{
			ReleaseViewTexture();
			if (screenMaterial != null)
			{
				UnityEngine.Object.Destroy(screenMaterial);
			}
		}

		private Material GetScreenMaterial()
		{
			if (screenMaterial == null && screen != null)
			{
				screenMaterial = screen.material;
			}
			return screenMaterial;
		}

		private void ReleaseViewTexture()
		{
			if (!(viewTexture == null))
			{
				if (portalCam != null && portalCam.targetTexture == viewTexture)
				{
					portalCam.targetTexture = null;
				}
				Material material = ((linkedPortal != null) ? linkedPortal.GetScreenMaterial() : null);
				if (material != null && material.GetTexture("_MainTex") == viewTexture)
				{
					material.SetTexture("_MainTex", null);
				}
				viewTexture.Release();
				UnityEngine.Object.Destroy(viewTexture);
				viewTexture = null;
			}
		}

		private void MigrateLegacyTeleportMode()
		{
			if (!teleportModeMigrated)
			{
				teleportMode = (legacyTravellerTeleportation ? PortalTeleportMode.Traveller : (legacySimpleTeleport ? PortalTeleportMode.Simple : PortalTeleportMode.Disabled));
				teleportModeMigrated = true;
			}
		}

		private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
		{
			if (isRenderingPortals || camera == null || !camera.CompareTag("MainCamera"))
			{
				return;
			}
			for (int i = 0; i < activePortals.Count; i++)
			{
				Portal portal = activePortals[i];
				if (portal != null && portal.RuntimeVisualsActive)
				{
					portal.playerCam = camera;
				}
			}
			isRenderingPortals = true;
			try
			{
				for (int j = 0; j < activePortals.Count; j++)
				{
					if (activePortals[j] != null && activePortals[j].RuntimeVisualsActive)
					{
						activePortals[j].PrePortalRender();
					}
				}
				for (int k = 0; k < activePortals.Count; k++)
				{
					if (activePortals[k] != null && activePortals[k].RuntimeVisualsActive)
					{
						activePortals[k].Render();
					}
				}
				for (int l = 0; l < activePortals.Count; l++)
				{
					if (activePortals[l] != null && activePortals[l].RuntimeVisualsActive)
					{
						activePortals[l].PostPortalRender();
					}
				}
			}
			finally
			{
				isRenderingPortals = false;
			}
		}

		private void LateUpdate()
		{
			if (runtimeTeleportEnabled)
			{
				if (UsesTravellerTeleport)
				{
					HandleTravellers();
				}
				if (UsesSimpleTeleport)
				{
					HandleSimpleTeleport();
				}
			}
		}

		private void HandleSimpleTeleport()
		{
			if (simpleTeleportTravellers.Count == 0)
			{
				return;
			}
			PPG_Player pPG_Player = null;
			foreach (KeyValuePair<PPG_Player, int> simpleTeleportTraveller in simpleTeleportTravellers)
			{
				PPG_Player key = simpleTeleportTraveller.Key;
				if (key != null && HasCrossedPortal(key, simpleTeleportTraveller.Value))
				{
					pPG_Player = key;
					break;
				}
			}
			if (pPG_Player != null)
			{
				TeleportSimpleTraveller(pPG_Player);
			}
		}

		private bool HasCrossedPortal(PPG_Player traveller, int entrySide)
		{
			int num = SideOfPortal(traveller.transform.position);
			if (num != 0)
			{
				return num != entrySide;
			}
			return false;
		}

		private bool TryTeleportSimpleTraveller(PPG_Player traveller)
		{
			if (!simpleTeleportTravellers.TryGetValue(traveller, out var value) || !HasCrossedPortal(traveller, value))
			{
				return false;
			}
			TeleportSimpleTraveller(traveller);
			return true;
		}

		private void TeleportSimpleTraveller(PPG_Player traveller)
		{
			simpleTeleportTravellers.Remove(traveller);
			if (!(linkedPortal == null))
			{
				linkedPortal.simpleTeleportCooldownUntil[traveller] = Time.time + 0.25f;
				Vector3 vector = base.transform.InverseTransformPoint(traveller.transform.position);
				vector = Quaternion.Euler(0f, 180f, 0f) * vector;
				vector.z = Mathf.Max(vector.z, teleportExitOffset);
				Quaternion quaternion = linkedPortal.transform.rotation * Quaternion.Euler(0f, 180f, 0f) * Quaternion.Inverse(base.transform.rotation);
				Vector3 pos = linkedPortal.transform.TransformPoint(vector);
				Quaternion rotation = quaternion * traveller.transform.rotation;
				traveller.TeleportPlayer(pos, rotation, updateGround: false);
				ApplyUnderwaterFog(traveller);
			}
		}

		private void ApplyUnderwaterFog(PPG_Player traveller)
		{
			if (!(traveller == null) && traveller.IsOwner && !(teleportUnderwaterArea == null) && teleportUnderwaterArea.shadingSettings != null)
			{
				teleportUnderwaterArea.shadingSettings.fogDensity = fogDensityAfterTeleport;
			}
		}

		private void HandleTravellers()
		{
			for (int i = 0; i < trackedTravellers.Count; i++)
			{
				PPG_Player pPG_Player = trackedTravellers[i];
				Transform transform = pPG_Player.transform;
				Matrix4x4 matrix4x = linkedPortal.transform.localToWorldMatrix * base.transform.worldToLocalMatrix * transform.localToWorldMatrix;
				Vector3 vector = transform.position - base.transform.position;
				int num = Math.Sign(Vector3.Dot(vector, base.transform.forward));
				int num2 = Math.Sign(Vector3.Dot(pPG_Player.previousOffsetFromPortal, base.transform.forward));
				if (num != num2)
				{
					Vector3 position = transform.position;
					Quaternion rotation = transform.rotation;
					pPG_Player.Teleport(base.transform, linkedPortal.transform, matrix4x.GetColumn(3), matrix4x.rotation);
					ApplyUnderwaterFog(pPG_Player);
					pPG_Player.graphicsClone.transform.SetPositionAndRotation(position, rotation);
					linkedPortal.OnTravellerEnterPortal(pPG_Player);
					trackedTravellers.RemoveAt(i);
					i--;
				}
				else
				{
					pPG_Player.graphicsClone.transform.SetPositionAndRotation(matrix4x.GetColumn(3), matrix4x.rotation);
					pPG_Player.previousOffsetFromPortal = vector;
				}
			}
		}

		public void PrePortalRender()
		{
			foreach (PPG_Player trackedTraveller in trackedTravellers)
			{
				UpdateSliceParams(trackedTraveller);
			}
		}

		private void Render()
		{
			if (!ShouldRenderPortalView())
			{
				return;
			}
			CreateViewTexture();
			int num = ResolveActiveRecursionLimit();
			EnsureRenderBuffers(num);
			Matrix4x4 matrix4x = playerCam.transform.localToWorldMatrix;
			int num2 = 0;
			portalCam.projectionMatrix = playerCam.projectionMatrix;
			for (int i = 0; i < num && (i <= 0 || PortalCameraUtility.BoundsOverlap(screenMeshFilter, linkedPortal.screenMeshFilter, portalCam)); i++)
			{
				matrix4x = base.transform.localToWorldMatrix * portalHalfTurn * linkedPortal.transform.worldToLocalMatrix * matrix4x;
				int num3 = num - i - 1;
				renderPositions[num3] = matrix4x.GetColumn(3);
				renderRotations[num3] = matrix4x.rotation;
				portalCam.transform.SetPositionAndRotation(renderPositions[num3], renderRotations[num3]);
				num2 = num3;
			}
			screen.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
			linkedPortal.GetScreenMaterial()?.SetInt("displayMask", 0);
			for (int j = num2; j < num; j++)
			{
				portalCam.transform.SetPositionAndRotation(renderPositions[j], renderRotations[j]);
				SetNearClipPlane();
				HandleClipping();
				RenderPipeline.SubmitRenderRequest(portalCam, renderRequest);
				if (j == num2)
				{
					linkedPortal.GetScreenMaterial()?.SetInt("displayMask", 1);
				}
			}
			screen.shadowCastingMode = ShadowCastingMode.On;
		}

		private void HandleClipping()
		{
			float num = linkedPortal.ProtectScreenFromClipping(portalCam.transform.position);
			foreach (PPG_Player trackedTraveller in trackedTravellers)
			{
				if (SameSideOfPortal(trackedTraveller.transform.position, portalCamPos))
				{
					trackedTraveller.SetSliceOffsetDst(-1000f, clone: false);
				}
				else
				{
					trackedTraveller.SetSliceOffsetDst(1000f, clone: false);
				}
				int num2 = -SideOfPortal(trackedTraveller.transform.position);
				if (linkedPortal.SideOfPortal(portalCamPos) == num2)
				{
					trackedTraveller.SetSliceOffsetDst(num, clone: true);
				}
				else
				{
					trackedTraveller.SetSliceOffsetDst(0f - num, clone: true);
				}
			}
			_ = portalCamPos - base.transform.position;
			foreach (PPG_Player trackedTraveller2 in linkedPortal.trackedTravellers)
			{
				Vector3 position = trackedTraveller2.graphicsObject.transform.position;
				_ = trackedTraveller2.graphicsClone.transform.position;
				if (linkedPortal.SideOfPortal(position) != SideOfPortal(portalCamPos))
				{
					trackedTraveller2.SetSliceOffsetDst(-1000f, clone: true);
				}
				else
				{
					trackedTraveller2.SetSliceOffsetDst(1000f, clone: true);
				}
				if (linkedPortal.SameSideOfPortal(trackedTraveller2.transform.position, portalCamPos))
				{
					trackedTraveller2.SetSliceOffsetDst(num, clone: false);
				}
				else
				{
					trackedTraveller2.SetSliceOffsetDst(0f - num, clone: false);
				}
			}
		}

		public void PostPortalRender()
		{
			foreach (PPG_Player trackedTraveller in trackedTravellers)
			{
				UpdateSliceParams(trackedTraveller);
			}
			ProtectScreenFromClipping(playerCam.transform.position);
		}

		private void CreateViewTexture()
		{
			int num = ResolvePortalTextureSize(Screen.width);
			int num2 = ResolvePortalTextureSize(Screen.height);
			if (viewTexture == null || viewTexture.width != num || viewTexture.height != num2)
			{
				if (viewTexture != null)
				{
					viewTexture.Release();
					UnityEngine.Object.Destroy(viewTexture);
				}
				RenderTextureDescriptor renderTextureDescriptor = new RenderTextureDescriptor(num, num2);
				renderTextureDescriptor.graphicsFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.LDR);
				renderTextureDescriptor.depthStencilFormat = GraphicsFormat.D24_UNorm_S8_UInt;
				renderTextureDescriptor.msaaSamples = 1;
				renderTextureDescriptor.useMipMap = false;
				renderTextureDescriptor.autoGenerateMips = false;
				renderTextureDescriptor.sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear;
				RenderTextureDescriptor desc = renderTextureDescriptor;
				viewTexture = new RenderTexture(desc)
				{
					name = base.name + "_PortalView",
					filterMode = FilterMode.Bilinear,
					wrapMode = TextureWrapMode.Clamp
				};
				viewTexture.Create();
				portalCam.targetTexture = viewTexture;
				renderRequest.destination = viewTexture;
				linkedPortal.GetScreenMaterial()?.SetTexture("_MainTex", viewTexture);
			}
		}

		private bool ShouldRenderPortalView()
		{
			if (!runtimeVisualsEnabled || !enablePortalRendering || playerCam == null || linkedPortal == null || linkedPortal.screen == null)
			{
				SetRendererEnabled((linkedPortal != null) ? linkedPortal.screen : null, enabled: false);
				return false;
			}
			bool flag = !linkedPortal.allowSimpleTeleportFromForwardSideOnly || linkedPortal.SideOfPortal(playerCam.transform.position) > 0;
			SetRendererEnabled(linkedPortal.screen, flag);
			if (!flag)
			{
				return false;
			}
			if (!IsWithinRenderDistance(linkedPortal.screen))
			{
				if (hideScreenWhenRenderCulled)
				{
					SetRendererEnabled(linkedPortal.screen, enabled: false);
				}
				return false;
			}
			if (!PortalCameraUtility.VisibleFromCamera(linkedPortal.screen, playerCam))
			{
				return false;
			}
			if (minimumRenderInterval > 0f && Time.unscaledTime < nextAllowedRenderTime)
			{
				return false;
			}
			nextAllowedRenderTime = Time.unscaledTime + minimumRenderInterval;
			return true;
		}

		private bool IsWithinRenderDistance(Renderer targetRenderer)
		{
			if (maxRenderDistance <= 0f || targetRenderer == null || playerCam == null)
			{
				return true;
			}
			Vector3 vector = targetRenderer.bounds.ClosestPoint(playerCam.transform.position);
			float num = maxRenderDistance * maxRenderDistance;
			return (vector - playerCam.transform.position).sqrMagnitude <= num;
		}

		private int ResolvePortalTextureSize(int screenSize)
		{
			float num = Mathf.Clamp(renderTextureResolutionScale, 0.1f, 1f);
			return Mathf.Max(1, Mathf.RoundToInt((float)screenSize * num));
		}

		private static void SetRendererEnabled(Renderer targetRenderer, bool enabled)
		{
			if (targetRenderer != null && targetRenderer.enabled != enabled)
			{
				targetRenderer.enabled = enabled;
			}
		}

		private int ResolveActiveRecursionLimit()
		{
			return Mathf.Max(1, Mathf.Min(recursionLimit, maximumRuntimeRecursion));
		}

		private void EnsureRenderBuffers(int activeRecursionLimit)
		{
			if (renderPositions == null || renderPositions.Length != activeRecursionLimit)
			{
				renderPositions = new Vector3[activeRecursionLimit];
				renderRotations = new Quaternion[activeRecursionLimit];
			}
		}

		private float ProtectScreenFromClipping(Vector3 viewPoint)
		{
			float num = playerCam.nearClipPlane * Mathf.Tan(playerCam.fieldOfView * 0.5f * (MathF.PI / 180f));
			float magnitude = new Vector3(num * playerCam.aspect, num, playerCam.nearClipPlane).magnitude;
			Transform transform = screen.transform;
			bool flag = Vector3.Dot(base.transform.forward, base.transform.position - viewPoint) > 0f;
			transform.localScale = new Vector3(transform.localScale.x, transform.localScale.y, magnitude);
			float z = magnitude * (flag ? 0.5f : (-0.5f));
			transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, z);
			return magnitude;
		}

		private void UpdateSliceParams(PPG_Player traveller)
		{
			int num = SideOfPortal(traveller.transform.position);
			Vector3 vector = base.transform.forward * -num;
			Vector3 vector2 = linkedPortal.transform.forward * num;
			Vector3 position = base.transform.position;
			Vector3 position2 = linkedPortal.transform.position;
			float value = 0f;
			float value2 = 0f;
			float z = screen.transform.localScale.z;
			if (!SameSideOfPortal(playerCam.transform.position, traveller.transform.position))
			{
				value = 0f - z;
			}
			if (num == linkedPortal.SideOfPortal(playerCam.transform.position))
			{
				value2 = 0f - z;
			}
			for (int i = 0; i < traveller.originalMaterials.Length; i++)
			{
				traveller.originalMaterials[i].SetVector("sliceCentre", position);
				traveller.originalMaterials[i].SetVector("sliceNormal", vector);
				traveller.originalMaterials[i].SetFloat("sliceOffsetDst", value);
				traveller.cloneMaterials[i].SetVector("sliceCentre", position2);
				traveller.cloneMaterials[i].SetVector("sliceNormal", vector2);
				traveller.cloneMaterials[i].SetFloat("sliceOffsetDst", value2);
			}
		}

		private void SetNearClipPlane()
		{
			Transform transform = base.transform;
			int num = Math.Sign(Vector3.Dot(transform.forward, base.transform.position - portalCam.transform.position));
			Vector3 lhs = portalCam.worldToCameraMatrix.MultiplyPoint(transform.position);
			Vector3 rhs = portalCam.worldToCameraMatrix.MultiplyVector(transform.forward) * num;
			float num2 = 0f - Vector3.Dot(lhs, rhs) + nearClipOffset;
			if (Mathf.Abs(num2) > nearClipLimit)
			{
				Vector4 clipPlane = new Vector4(rhs.x, rhs.y, rhs.z, num2);
				portalCam.projectionMatrix = playerCam.CalculateObliqueMatrix(clipPlane);
			}
			else
			{
				portalCam.projectionMatrix = playerCam.projectionMatrix;
			}
		}

		private void OnTravellerEnterPortal(PPG_Player traveller)
		{
			if (!trackedTravellers.Contains(traveller))
			{
				traveller.EnterPortalThreshold();
				traveller.previousOffsetFromPortal = traveller.transform.position - base.transform.position;
				trackedTravellers.Add(traveller);
			}
		}

		private void OnTriggerEnter(Collider other)
		{
			if (!runtimeTeleportEnabled || !TryGetPrimaryTraveller(other, out var traveller))
			{
				return;
			}
			if (UsesSimpleTeleport && !IsSimpleTeleportOnCooldown(traveller))
			{
				int num = SideOfPortal(traveller.transform.position);
				if (num != 0 && (!allowSimpleTeleportFromForwardSideOnly || num > 0))
				{
					simpleTeleportTravellers[traveller] = num;
				}
			}
			if (UsesTravellerTeleport)
			{
				OnTravellerEnterPortal(traveller);
			}
		}

		private void OnTriggerExit(Collider other)
		{
			if (TryGetPrimaryTraveller(other, out var traveller) && (!UsesSimpleTeleport || !TryTeleportSimpleTraveller(traveller)))
			{
				simpleTeleportTravellers.Remove(traveller);
				if (UsesTravellerTeleport && trackedTravellers.Contains(traveller))
				{
					traveller.ExitPortalThreshold();
					trackedTravellers.Remove(traveller);
				}
			}
		}

		private bool TryGetPrimaryTraveller(Collider other, out PPG_Player traveller)
		{
			traveller = other.GetComponentInParent<PPG_Player>();
			if (traveller != null && traveller.IsOwner)
			{
				return other == traveller.GetComponent<Collider>();
			}
			return false;
		}

		private bool IsSimpleTeleportOnCooldown(PPG_Player traveller)
		{
			if (!simpleTeleportCooldownUntil.TryGetValue(traveller, out var value))
			{
				return false;
			}
			if (Time.time < value)
			{
				return true;
			}
			simpleTeleportCooldownUntil.Remove(traveller);
			return false;
		}

		private int SideOfPortal(Vector3 pos)
		{
			return Math.Sign(Vector3.Dot(pos - base.transform.position, base.transform.forward));
		}

		private bool SameSideOfPortal(Vector3 posA, Vector3 posB)
		{
			return SideOfPortal(posA) == SideOfPortal(posB);
		}

		private void OnValidate()
		{
			MigrateLegacyTeleportMode();
			if (linkedPortal != null)
			{
				linkedPortal.linkedPortal = this;
			}
		}

		public static bool TryTeleportThrownItem(Rigidbody body, Vector3 previousPosition)
		{
			if (body == null || body.isKinematic)
			{
				return false;
			}
			Portal portal = null;
			float num = float.PositiveInfinity;
			foreach (Portal itemTeleportPortal in itemTeleportPortals)
			{
				if (itemTeleportPortal.TryGetItemCrossing(previousPosition, body.position, out var fraction) && fraction < num)
				{
					portal = itemTeleportPortal;
					num = fraction;
				}
			}
			if (portal == null)
			{
				return false;
			}
			portal.TeleportItemBody(body, previousPosition);
			return true;
		}

		private bool TryGetItemCrossing(Vector3 previousPosition, Vector3 position, out float fraction)
		{
			fraction = 0f;
			if (!base.isActiveAndEnabled || !runtimeTeleportEnabled || (!UsesSimpleTeleport && !UsesTravellerTeleport) || linkedPortal == null || !linkedPortal.isActiveAndEnabled || !linkedPortal.runtimeTeleportEnabled)
			{
				return false;
			}
			Vector3 a = base.transform.InverseTransformPoint(previousPosition);
			Vector3 b = base.transform.InverseTransformPoint(position);
			if (a.z == b.z || (a.z > 0f && b.z > 0f) || (a.z < 0f && b.z < 0f) || a.z == 0f || (UsesSimpleTeleport && allowSimpleTeleportFromForwardSideOnly && a.z < 0f))
			{
				return false;
			}
			BoxCollider component = GetComponent<BoxCollider>();
			if (component == null || !component.enabled || !component.isTrigger)
			{
				return false;
			}
			fraction = a.z / (a.z - b.z);
			Vector3 vector = Vector3.Lerp(a, b, fraction) - component.center;
			Vector3 vector2 = component.size * 0.5f;
			if (Mathf.Abs(vector.x) <= vector2.x)
			{
				return Mathf.Abs(vector.y) <= vector2.y;
			}
			return false;
		}

		private void TeleportItemBody(Rigidbody body, Vector3 previousPosition)
		{
			Quaternion quaternion = (UsesSimpleTeleport ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity);
			Quaternion quaternion2 = linkedPortal.transform.rotation * quaternion * Quaternion.Inverse(base.transform.rotation);
			Vector3 position = quaternion * base.transform.InverseTransformPoint(body.position);
			float num = Mathf.Sign((quaternion * base.transform.InverseTransformVector(body.position - previousPosition)).z);
			position.z = num * Mathf.Max(Mathf.Abs(position.z), teleportExitOffset);
			Vector3 position2 = linkedPortal.transform.TransformPoint(position);
			Quaternion rotation = quaternion2 * body.rotation;
			Vector3 linearVelocity = quaternion2 * body.linearVelocity;
			Vector3 angularVelocity = quaternion2 * body.angularVelocity;
			body.position = position2;
			body.rotation = rotation;
			body.transform.SetPositionAndRotation(position2, rotation);
			body.linearVelocity = linearVelocity;
			body.angularVelocity = angularVelocity;
		}
	}
}
