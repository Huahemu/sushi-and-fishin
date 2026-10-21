using System;
using System.Collections.Generic;
using DynamicMeshCutter;
using UnityEngine;

namespace PaperPixelGames.Core.Kitchen.PreparationStation.Station
{
	public sealed class PrepStationIngredientMeshCutController : CutterBehaviour
	{
		private sealed class SourceRendererState
		{
			public MeshRenderer Renderer;

			public bool WasEnabled;
		}

		private sealed class PendingCut
		{
			public int SessionVersion;

			public int Sequence;

			public Vector3 Point;

			public Vector3 Normal;

			public Action<bool> Completed;
		}

		private const float PlaneEpsilon = 1E-05f;

		private const float MinimumRemainingLength = 0.002f;

		private readonly List<SourceRendererState> _sourceRenderers = new List<SourceRendererState>();

		private GameObject _runtimeRoot;

		private MeshTarget _activeTarget;

		private PendingCut _activeCut;

		private GameObject _stagedReplacementObject;

		private MeshTarget _stagedReplacementTarget;

		private Vector3 _surfaceNormal = Vector3.up;

		private Vector3 _cutAxis = Vector3.forward;

		private Vector3 _lateralAxis = Vector3.right;

		private Vector3 _keepAnchorWorld;

		private float _sliceThickness;

		private int _requiredCutCount;

		private int _nextSequence = 1;

		private int _sessionVersion;

		private bool _algorithmCompletedSuccessfully;

		public bool IsSessionActive { get; private set; }

		public bool IsCutInProgress => _activeCut != null;

		public Vector3 CutAxis => _cutAxis;

		public Transform PresentationRoot
		{
			get
			{
				if (!(_runtimeRoot != null))
				{
					return null;
				}
				return _runtimeRoot.transform;
			}
		}

		public void HidePresentationIfFinalCut(int sequence)
		{
			if (IsSessionActive && sequence == _requiredCutCount && !(_runtimeRoot == null))
			{
				_runtimeRoot.SetActive(value: false);
			}
		}

		public void Initialize()
		{
			DestroyTargets = false;
			UseAsync = true;
			AsyncThreadAmount = 1;
			Separation = 0f;
			VertexCreationThreshold = 0;
		}

		public bool TryStartSession(GameObject cuttingPrefab, GameObject sourcePresentation, Transform presentationParent, Vector3 surfaceNormal, int requiredCutCount, out string failureReason)
		{
			failureReason = null;
			CancelSession();
			Initialize();
			if (cuttingPrefab == null)
			{
				failureReason = "CuttingPrefabMissing";
				return false;
			}
			if (sourcePresentation == null)
			{
				failureReason = "SourcePresentationMissing";
				return false;
			}
			if (presentationParent == null)
			{
				failureReason = "PresentationParentMissing";
				return false;
			}
			if (requiredCutCount <= 0)
			{
				failureReason = "RequiredCutCountInvalid";
				return false;
			}
			_surfaceNormal = ((surfaceNormal.sqrMagnitude > 1E-05f) ? surfaceNormal.normalized : Vector3.up);
			_requiredCutCount = requiredCutCount;
			_sessionVersion++;
			_runtimeRoot = new GameObject("PrepStation_RuntimeCutIngredient");
			_runtimeRoot.transform.SetParent(presentationParent, worldPositionStays: false);
			_runtimeRoot.transform.localPosition = Vector3.zero;
			_runtimeRoot.transform.localRotation = Quaternion.identity;
			_runtimeRoot.transform.localScale = Vector3.one;
			GameObject gameObject = UnityEngine.Object.Instantiate(cuttingPrefab, _runtimeRoot.transform, worldPositionStays: false);
			gameObject.name = cuttingPrefab.name + "_PrepStationCutSource";
			gameObject.transform.localPosition = sourcePresentation.transform.localPosition;
			gameObject.transform.localRotation = sourcePresentation.transform.localRotation;
			gameObject.transform.localScale = sourcePresentation.transform.localScale;
			MeshRenderer[] componentsInChildren = gameObject.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
			if (componentsInChildren.Length != 1)
			{
				failureReason = $"CuttingPrefabRequiresSingleMeshRenderer:{componentsInChildren.Length}";
				UnityEngine.Object.Destroy(gameObject);
				CancelSession();
				return false;
			}
			MeshRenderer meshRenderer = componentsInChildren[0];
			MeshFilter component = meshRenderer.GetComponent<MeshFilter>();
			if (component == null || component.sharedMesh == null)
			{
				failureReason = "CuttingMeshMissing";
				UnityEngine.Object.Destroy(gameObject);
				CancelSession();
				return false;
			}
			if (!component.sharedMesh.isReadable)
			{
				failureReason = "CuttingMeshNotReadable:" + component.sharedMesh.name;
				UnityEngine.Object.Destroy(gameObject);
				CancelSession();
				return false;
			}
			try
			{
				_activeTarget = CreateRuntimeTarget(meshRenderer);
			}
			catch (Exception ex)
			{
				failureReason = "RuntimeMeshSnapshotFailed:" + ex.GetType().Name;
				UnityEngine.Object.Destroy(gameObject);
				CancelSession();
				return false;
			}
			gameObject.SetActive(value: false);
			UnityEngine.Object.Destroy(gameObject);
			if (_activeTarget == null || !TryResolveCutAxes(presentationParent, _activeTarget, out _cutAxis, out _lateralAxis) || !TryGetProjectionBounds(_activeTarget, _cutAxis, _lateralAxis, _surfaceNormal, out var axisMin, out var axisMax, out var lateralMin, out var lateralMax, out var upMin, out var upMax))
			{
				failureReason = "CutAxisResolutionFailed";
				CancelSession();
				return false;
			}
			float num = axisMax - axisMin;
			if (num <= 0.002f)
			{
				failureReason = "CuttingMeshLengthTooSmall";
				CancelSession();
				return false;
			}
			_sliceThickness = num / ((float)_requiredCutCount + 1f);
			_keepAnchorWorld = _cutAxis * (axisMin + num * 0.05f) + _lateralAxis * ((lateralMin + lateralMax) * 0.5f) + _surfaceNormal * ((upMin + upMax) * 0.5f);
			MeshRenderer[] componentsInChildren2 = sourcePresentation.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
			foreach (MeshRenderer meshRenderer2 in componentsInChildren2)
			{
				_sourceRenderers.Add(new SourceRendererState
				{
					Renderer = meshRenderer2,
					WasEnabled = meshRenderer2.enabled
				});
				meshRenderer2.enabled = false;
			}
			_nextSequence = 1;
			IsSessionActive = true;
			return true;
		}

		public bool TryGetNextCutPoint(out Vector3 cutPoint, out Vector3 displayPosition, out float displayRotationZ)
		{
			cutPoint = default;
			displayPosition = default;
			displayRotationZ = 0f;
			if (!IsSessionActive || IsCutInProgress || _activeTarget == null)
			{
				return false;
			}
			if (!TryGetProjectionBounds(_activeTarget, _cutAxis, _lateralAxis, _surfaceNormal, out var axisMin, out var axisMax, out var lateralMin, out var lateralMax, out var upMin, out var upMax))
			{
				return false;
			}
			float num = axisMax - axisMin;
			if (num <= 0.002f)
			{
				return false;
			}
			float num2 = Mathf.Min(_sliceThickness, num * 0.45f);
			float num3 = axisMax - num2;
			if (num3 <= axisMin + 0.002f)
			{
				return false;
			}
			MeshFilter component = _activeTarget.GetComponent<MeshFilter>();
			if (component == null || component.sharedMesh == null)
			{
				return false;
			}
			MeshCollider meshCollider = _activeTarget.gameObject.AddComponent<MeshCollider>();
			meshCollider.sharedMesh = component.sharedMesh;
			meshCollider.convex = false;
			float num4 = (lateralMin + lateralMax) * 0.5f;
			float num5 = (lateralMax - lateralMin) * 0.5f;
			float maxDistance = Mathf.Max(0.05f, upMax - upMin) + 0.5f;
			float num6 = UnityEngine.Random.Range(-0.08f, 0.08f) * num5;
			float[] array = new float[6]
			{
				num6,
				0f,
				num5 * 0.2f,
				(0f - num5) * 0.2f,
				num5 * 0.4f,
				(0f - num5) * 0.4f
			};
			bool flag = false;
			for (int i = 0; i < array.Length; i++)
			{
				Vector3 origin = _cutAxis * num3 + _lateralAxis * (num4 + array[i]) + _surfaceNormal * (upMax + 0.25f);
				Ray ray = new Ray(origin, -_surfaceNormal);
				if (meshCollider.Raycast(ray, out var hitInfo, maxDistance))
				{
					cutPoint = hitInfo.point;
					flag = true;
					break;
				}
			}
			meshCollider.enabled = false;
			UnityEngine.Object.Destroy(meshCollider);
			if (!flag)
			{
				return false;
			}
			displayPosition = cutPoint + _surfaceNormal * 0.05f;
			displayRotationZ = UnityEngine.Random.Range(-12f, 12f);
			return true;
		}

		public Vector3 ConstrainPlaneNormal(Vector3 requestedNormal, float maxAngleDegrees)
		{
			Vector3 vector = Vector3.ProjectOnPlane(requestedNormal, _surfaceNormal);
			if (vector.sqrMagnitude <= 1E-05f)
			{
				return _cutAxis;
			}
			vector.Normalize();
			if (Vector3.Dot(vector, _cutAxis) < 0f)
			{
				vector = -vector;
			}
			float maxRadiansDelta = Mathf.Max(0f, maxAngleDegrees) * (MathF.PI / 180f);
			return Vector3.RotateTowards(_cutAxis, vector, maxRadiansDelta, 0f).normalized;
		}

		public bool RequestCut(int sequence, Vector3 worldPoint, Vector3 worldNormal, Action<bool> completed)
		{
			if (!IsSessionActive || IsCutInProgress || _activeTarget == null)
			{
				return false;
			}
			if (sequence != _nextSequence || worldNormal.sqrMagnitude <= 1E-05f)
			{
				return false;
			}
			_activeCut = new PendingCut
			{
				SessionVersion = _sessionVersion,
				Sequence = sequence,
				Point = worldPoint,
				Normal = worldNormal.normalized,
				Completed = completed
			};
			_algorithmCompletedSuccessfully = false;
			Cut(_activeTarget, worldPoint, worldNormal.normalized, OnCutAlgorithmCompleted, OnCutObjectsCreated, _activeCut);
			return true;
		}

		public void CompleteSession()
		{
			CleanupSession(restoreSourceRenderers: false);
		}

		public void CancelSession()
		{
			CleanupSession(restoreSourceRenderers: true);
		}

		private void CleanupSession(bool restoreSourceRenderers)
		{
			_sessionVersion++;
			IsSessionActive = false;
			PendingCut activeCut = _activeCut;
			_activeCut = null;
			activeCut?.Completed?.Invoke(obj: false);
			DestroyStagedReplacement();
			DestroyRuntimeTarget(_activeTarget);
			_activeTarget = null;
			if (_runtimeRoot != null)
			{
				_runtimeRoot.SetActive(value: false);
				UnityEngine.Object.Destroy(_runtimeRoot);
				_runtimeRoot = null;
			}
			if (restoreSourceRenderers)
			{
				for (int i = 0; i < _sourceRenderers.Count; i++)
				{
					SourceRendererState sourceRendererState = _sourceRenderers[i];
					if (sourceRendererState.Renderer != null)
					{
						sourceRendererState.Renderer.enabled = sourceRendererState.WasEnabled;
					}
				}
			}
			_sourceRenderers.Clear();
			_requiredCutCount = 0;
			_sliceThickness = 0f;
			_nextSequence = 1;
			_algorithmCompletedSuccessfully = false;
		}

		private void OnDestroy()
		{
			CancelSession();
		}

		private void OnCutAlgorithmCompleted(bool success, Info info)
		{
			PendingCut request = info?.BoxedUserData as PendingCut;
			if (!IsCurrentRequest(request))
			{
				if (success)
				{
					SuppressCreatedMeshes(info);
				}
				return;
			}
			if (!success)
			{
				CompleteActiveCut(success: false);
				return;
			}
			int side = info.Plane.GetSide(_activeTarget.transform.InverseTransformPoint(_keepAnchorWorld));
			_algorithmCompletedSuccessfully = KeepOnlyRequestedSide(info, side);
			if (!_algorithmCompletedSuccessfully)
			{
				CompleteActiveCut(success: false);
			}
		}

		private void OnCutObjectsCreated(Info info, MeshCreationData creationData)
		{
			PendingCut pendingCut = info?.BoxedUserData as PendingCut;
			if (!IsCurrentRequest(pendingCut) || !_algorithmCompletedSuccessfully)
			{
				DestroyCreationData(creationData);
				return;
			}
			if (creationData?.CreatedTargets == null || creationData.CreatedObjects == null || creationData.CreatedTargets.Length != 1 || creationData.CreatedObjects.Length != 1)
			{
				DestroyCreationData(creationData);
				CompleteActiveCut(success: false);
				return;
			}
			_stagedReplacementTarget = creationData.CreatedTargets[0];
			_stagedReplacementObject = creationData.CreatedObjects[0];
			if (_stagedReplacementTarget == null || _stagedReplacementObject == null)
			{
				DestroyStagedReplacement();
				CompleteActiveCut(success: false);
				return;
			}
			_stagedReplacementObject.transform.SetParent(_runtimeRoot.transform, worldPositionStays: true);
			_stagedReplacementObject.name = $"PrepStation_RetainedIngredient_{pendingCut.Sequence}";
			ConfigureMeshTarget(_stagedReplacementTarget, _stagedReplacementObject);
			SetRenderersEnabled(_stagedReplacementObject, enabled: false);
			MeshTarget activeTarget = _activeTarget;
			_activeTarget = _stagedReplacementTarget;
			_stagedReplacementTarget = null;
			GameObject stagedReplacementObject = _stagedReplacementObject;
			_stagedReplacementObject = null;
			DestroyRuntimeTarget(activeTarget);
			SetRenderersEnabled(stagedReplacementObject, enabled: true);
			_nextSequence++;
			CompleteActiveCut(success: true);
		}

		private void CompleteActiveCut(bool success)
		{
			PendingCut activeCut = _activeCut;
			_activeCut = null;
			_algorithmCompletedSuccessfully = false;
			if (!success)
			{
				DestroyStagedReplacement();
			}
			activeCut?.Completed?.Invoke(success && IsSessionActive && _activeTarget != null);
		}

		private bool IsCurrentRequest(PendingCut request)
		{
			if (request != null && request == _activeCut && request.SessionVersion == _sessionVersion)
			{
				return IsSessionActive;
			}
			return false;
		}

		private MeshTarget CreateRuntimeTarget(MeshRenderer sourceRenderer)
		{
			MeshFilter component = sourceRenderer.GetComponent<MeshFilter>();
			if (component == null || component.sharedMesh == null)
			{
				return null;
			}
			Mesh mesh = UnityEngine.Object.Instantiate(component.sharedMesh);
			mesh.name = component.sharedMesh.name + "_PrepStationRuntime";
			GameObject gameObject = new GameObject(sourceRenderer.name + "_PrepStationRuntimeMesh");
			gameObject.layer = sourceRenderer.gameObject.layer;
			gameObject.transform.SetPositionAndRotation(sourceRenderer.transform.position, sourceRenderer.transform.rotation);
			gameObject.transform.localScale = sourceRenderer.transform.lossyScale;
			gameObject.transform.SetParent(_runtimeRoot.transform, worldPositionStays: true);
			gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
			MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
			meshRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
			meshRenderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
			meshRenderer.receiveShadows = sourceRenderer.receiveShadows;
			meshRenderer.lightProbeUsage = sourceRenderer.lightProbeUsage;
			meshRenderer.reflectionProbeUsage = sourceRenderer.reflectionProbeUsage;
			meshRenderer.motionVectorGenerationMode = sourceRenderer.motionVectorGenerationMode;
			meshRenderer.allowOcclusionWhenDynamic = sourceRenderer.allowOcclusionWhenDynamic;
			meshRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
			meshRenderer.sortingOrder = sourceRenderer.sortingOrder;
			MeshTarget meshTarget = gameObject.AddComponent<MeshTarget>();
			ConfigureMeshTarget(meshTarget, gameObject);
			return meshTarget;
		}

		private static void ConfigureMeshTarget(MeshTarget target, GameObject root)
		{
			target.GameobjectRoot = root;
			target.CreateCutFaces = false;
			target.SeparateMeshes = false;
			target.ApplyTranslation = false;
			for (int i = 0; i < 2; i++)
			{
				target.DefaultBehaviour[i] = DynamicMeshCutter.Behaviour.Stone;
				target.CreateRigidbody[i] = false;
				target.CreateMeshCollider[i] = false;
				target.Inherit[i] = true;
			}
		}

		private bool TryResolveCutAxes(Transform presentationParent, MeshTarget target, out Vector3 cutAxis, out Vector3 lateralAxis)
		{
			cutAxis = default;
			lateralAxis = default;
			Vector3 normalized = Vector3.ProjectOnPlane(presentationParent.right, _surfaceNormal).normalized;
			Vector3 normalized2 = Vector3.ProjectOnPlane(presentationParent.forward, _surfaceNormal).normalized;
			if (normalized.sqrMagnitude <= 1E-05f || normalized2.sqrMagnitude <= 1E-05f)
			{
				return false;
			}
			if (!TryGetAxisSpan(target, normalized, out var span) || !TryGetAxisSpan(target, normalized2, out var span2))
			{
				return false;
			}
			cutAxis = ((span >= span2) ? normalized : normalized2);
			lateralAxis = ((span >= span2) ? normalized2 : normalized);
			return true;
		}

		private static bool TryGetAxisSpan(MeshTarget target, Vector3 axis, out float span)
		{
			span = 0f;
			MeshFilter meshFilter = ((target != null) ? target.GetComponent<MeshFilter>() : null);
			Mesh mesh = ((meshFilter != null) ? meshFilter.sharedMesh : null);
			if (mesh == null || !mesh.isReadable)
			{
				return false;
			}
			Vector3[] vertices = mesh.vertices;
			if (vertices.Length == 0)
			{
				return false;
			}
			float num = float.PositiveInfinity;
			float num2 = float.NegativeInfinity;
			for (int i = 0; i < vertices.Length; i++)
			{
				float b = Vector3.Dot(target.transform.TransformPoint(vertices[i]), axis);
				num = Mathf.Min(num, b);
				num2 = Mathf.Max(num2, b);
			}
			span = num2 - num;
			return span > 0.002f;
		}

		private static bool TryGetProjectionBounds(MeshTarget target, Vector3 axis, Vector3 lateral, Vector3 up, out float axisMin, out float axisMax, out float lateralMin, out float lateralMax, out float upMin, out float upMax)
		{
			axisMin = (lateralMin = (upMin = float.PositiveInfinity));
			axisMax = (lateralMax = (upMax = float.NegativeInfinity));
			MeshFilter meshFilter = ((target != null) ? target.GetComponent<MeshFilter>() : null);
			Mesh mesh = ((meshFilter != null) ? meshFilter.sharedMesh : null);
			if (mesh == null || !mesh.isReadable)
			{
				return false;
			}
			Vector3[] vertices = mesh.vertices;
			if (vertices.Length == 0)
			{
				return false;
			}
			for (int i = 0; i < vertices.Length; i++)
			{
				Vector3 lhs = target.transform.TransformPoint(vertices[i]);
				float b = Vector3.Dot(lhs, axis);
				float b2 = Vector3.Dot(lhs, lateral);
				float b3 = Vector3.Dot(lhs, up);
				axisMin = Mathf.Min(axisMin, b);
				axisMax = Mathf.Max(axisMax, b);
				lateralMin = Mathf.Min(lateralMin, b2);
				lateralMax = Mathf.Max(lateralMax, b2);
				upMin = Mathf.Min(upMin, b3);
				upMax = Mathf.Max(upMax, b3);
			}
			if (axisMax > axisMin && lateralMax >= lateralMin)
			{
				return upMax >= upMin;
			}
			return false;
		}

		private static bool KeepOnlyRequestedSide(Info info, int keepSide)
		{
			if (info?.CreatedMeshes == null || info.Sides == null || info.BT == null)
			{
				return false;
			}
			List<VirtualMesh> list = new List<VirtualMesh>();
			List<int> list2 = new List<int>();
			List<int> list3 = new List<int>();
			int num = Mathf.Min(info.CreatedMeshes.Length, Mathf.Min(info.Sides.Length, info.BT.Length));
			for (int i = 0; i < num; i++)
			{
				if (info.Sides[i] == keepSide)
				{
					list.Add(info.CreatedMeshes[i]);
					list2.Add(info.Sides[i]);
					list3.Add(info.BT[i]);
				}
			}
			info.CreatedMeshes = list.ToArray();
			info.Sides = list2.ToArray();
			info.BT = list3.ToArray();
			return list.Count == 1;
		}

		private static void SuppressCreatedMeshes(Info info)
		{
			if (info != null)
			{
				info.CreatedMeshes = Array.Empty<VirtualMesh>();
				info.Sides = Array.Empty<int>();
				info.BT = Array.Empty<int>();
			}
		}

		private void DestroyStagedReplacement()
		{
			if (_stagedReplacementObject != null)
			{
				DestroyRuntimeObject(_stagedReplacementObject);
			}
			_stagedReplacementObject = null;
			_stagedReplacementTarget = null;
		}

		private static void DestroyCreationData(MeshCreationData creationData)
		{
			if (creationData?.CreatedObjects == null)
			{
				return;
			}
			for (int i = 0; i < creationData.CreatedObjects.Length; i++)
			{
				GameObject gameObject = creationData.CreatedObjects[i];
				if (gameObject != null)
				{
					DestroyRuntimeObject(gameObject);
				}
			}
		}

		private static void DestroyRuntimeTarget(MeshTarget target)
		{
			if (!(target == null))
			{
				DestroyRuntimeObject((target.GameobjectRoot != null) ? target.GameobjectRoot : target.gameObject);
			}
		}

		private static void DestroyRuntimeObject(GameObject runtimeObject)
		{
			if (runtimeObject == null)
			{
				return;
			}
			MeshFilter[] componentsInChildren = runtimeObject.GetComponentsInChildren<MeshFilter>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				Mesh sharedMesh = componentsInChildren[i].sharedMesh;
				componentsInChildren[i].sharedMesh = null;
				if (sharedMesh != null)
				{
					UnityEngine.Object.Destroy(sharedMesh);
				}
			}
			runtimeObject.SetActive(value: false);
			UnityEngine.Object.Destroy(runtimeObject);
		}

		private static void SetRenderersEnabled(GameObject root, bool enabled)
		{
			if (!(root == null))
			{
				Renderer[] componentsInChildren = root.GetComponentsInChildren<Renderer>(includeInactive: true);
				for (int i = 0; i < componentsInChildren.Length; i++)
				{
					componentsInChildren[i].enabled = enabled;
				}
			}
		}
	}
}
