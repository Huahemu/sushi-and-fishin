using System;
using System.Collections.Generic;
using DynamicMeshCutter;
using UnityEngine;

namespace PaperPixelGames.Core.Kitchen.ChefDesk
{
	public sealed class ChefDeskRollMeshCutController : CutterBehaviour
	{
		private sealed class SourceRendererState
		{
			public MeshRenderer Renderer;

			public bool WasEnabled;
		}

		private sealed class RuntimeMeshEntry
		{
			public MeshTarget Target;

			public bool IsOuterShell;

			public int Layer;
		}

		private sealed class PendingCut
		{
			public int Sequence;

			public Vector3 Point;

			public Vector3 Normal;

			public Action<bool> Completed;
		}

		private enum MeshPlaneRelation : byte
		{
			Keep = 0,
			Discard = 1,
			Straddling = 2
		}

		private sealed class CutTransaction
		{
			public int SessionVersion;

			public PendingCut Request;

			public int PendingOperations;

			public bool Failed;

			public readonly Dictionary<RuntimeMeshEntry, MeshPlaneRelation> Relations = new Dictionary<RuntimeMeshEntry, MeshPlaneRelation>();

			public readonly Dictionary<RuntimeMeshEntry, RuntimeMeshEntry> Replacements = new Dictionary<RuntimeMeshEntry, RuntimeMeshEntry>();

			public readonly List<GameObject> StagedObjects = new List<GameObject>();
		}

		private const float PlaneEpsilon = 1E-05f;

		private const float MinimumRemainingLength = 0.002f;

		private readonly List<SourceRendererState> _sourceRenderers = new List<SourceRendererState>();

		private readonly List<RuntimeMeshEntry> _runtimeEntries = new List<RuntimeMeshEntry>();

		private readonly Queue<PendingCut> _pendingCuts = new Queue<PendingCut>();

		private GameObject _runtimeRoot;

		private RuntimeMeshEntry _outerShellEntry;

		private CutTransaction _activeTransaction;

		private Vector3 _surfaceNormal = Vector3.up;

		private Vector3 _rollAxis = Vector3.forward;

		private Vector3 _rollLateral = Vector3.right;

		private Vector3 _keepAnchorWorld;

		private float _sliceThickness;

		private int _requiredCutCount;

		private int _nextSequence = 1;

		private int _sessionVersion;

		private bool _isCutInProgress;

		public bool IsSessionActive { get; private set; }

		public bool IsCutInProgress => _isCutInProgress;

		public Vector3 RollAxis => _rollAxis;

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

		public bool TryStartSession(GameObject cuttingRollPrefab, GameObject noriObject, GameObject riceObject, Transform ingredientRoot, GameObject presentationOuterShell, Transform presentationParent, Vector3 surfaceNormal, int requiredCutCount, out string failureReason)
		{
			failureReason = null;
			CancelSession();
			Initialize();
			if (cuttingRollPrefab == null)
			{
				failureReason = "CuttingRollPrefabMissing";
				return false;
			}
			if (presentationParent == null)
			{
				failureReason = "PresentationParentMissing";
				return false;
			}
			if (presentationOuterShell == null)
			{
				failureReason = "PresentationOuterShellMissing";
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
			_runtimeRoot = new GameObject("ChefDesk_RuntimeCutRoll");
			_runtimeRoot.transform.SetParent(presentationParent, worldPositionStays: false);
			_runtimeRoot.transform.localPosition = Vector3.zero;
			_runtimeRoot.transform.localRotation = Quaternion.identity;
			_runtimeRoot.transform.localScale = Vector3.one;
			GameObject gameObject = UnityEngine.Object.Instantiate(cuttingRollPrefab, _runtimeRoot.transform, worldPositionStays: false);
			gameObject.name = cuttingRollPrefab.name + "_ChefDeskCutSource";
			List<MeshRenderer> list = CollectValidMeshRenderers(gameObject.transform);
			MeshRenderer meshRenderer = ResolvePrimaryOuterRenderer(list);
			if (meshRenderer == null)
			{
				failureReason = "CuttingRollMeshRendererMissing";
				CancelSession();
				return false;
			}
			MeshRenderer meshRenderer2 = ResolvePrimaryOuterRenderer(CollectValidMeshRenderers(presentationOuterShell.transform));
			if (meshRenderer2 == null)
			{
				failureReason = "PresentationOuterShellRendererMissing";
				CancelSession();
				return false;
			}
			AlignCuttingRollToPresentation(gameObject.transform, meshRenderer, meshRenderer2);
			for (int i = 0; i < list.Count; i++)
			{
				MeshFilter component = list[i].GetComponent<MeshFilter>();
				if (!(component != null) || !(component.sharedMesh != null) || !component.sharedMesh.isReadable)
				{
					string text = ((component != null && component.sharedMesh != null) ? component.sharedMesh.name : "Missing");
					failureReason = "CuttingRollMeshNotReadable:" + text;
					CancelSession();
					return false;
				}
			}
			try
			{
				for (int j = 0; j < list.Count; j++)
				{
					MeshRenderer meshRenderer3 = list[j];
					RuntimeMeshEntry runtimeMeshEntry = CreateRuntimeMeshEntry(meshRenderer3, meshRenderer3 == meshRenderer);
					if (runtimeMeshEntry != null)
					{
						_runtimeEntries.Add(runtimeMeshEntry);
						if (runtimeMeshEntry.IsOuterShell)
						{
							_outerShellEntry = runtimeMeshEntry;
						}
					}
				}
			}
			catch (Exception ex)
			{
				failureReason = "RuntimeMeshSnapshotFailed:" + ex.GetType().Name;
				CancelSession();
				return false;
			}
			gameObject.SetActive(value: false);
			UnityEngine.Object.Destroy(gameObject);
			if (_outerShellEntry == null || _runtimeEntries.Count == 0)
			{
				failureReason = "RuntimeOuterShellMissing";
				CancelSession();
				return false;
			}
			if (!TryResolveRollAxis(presentationParent, _outerShellEntry.Target, out _rollAxis, out _rollLateral) || !TryGetProjectionBounds(_outerShellEntry.Target, _rollAxis, _rollLateral, _surfaceNormal, out var axisMin, out var axisMax, out var lateralMin, out var lateralMax, out var upMin, out var upMax))
			{
				failureReason = "RollAxisResolutionFailed";
				CancelSession();
				return false;
			}
			float num = axisMax - axisMin;
			if (num <= 0.002f)
			{
				failureReason = "RollLengthTooSmall";
				CancelSession();
				return false;
			}
			_sliceThickness = num / ((float)_requiredCutCount + 1f);
			_keepAnchorWorld = _rollAxis * (axisMin + num * 0.05f) + _rollLateral * ((lateralMin + lateralMax) * 0.5f) + _surfaceNormal * ((upMin + upMax) * 0.5f);
			List<MeshRenderer> list2 = new List<MeshRenderer>();
			HashSet<MeshRenderer> uniqueRenderers = new HashSet<MeshRenderer>();
			AddValidMeshRenderers((noriObject != null) ? noriObject.transform : null, list2, uniqueRenderers);
			AddValidMeshRenderers((riceObject != null) ? riceObject.transform : null, list2, uniqueRenderers);
			AddValidMeshRenderers(ingredientRoot, list2, uniqueRenderers);
			for (int k = 0; k < list2.Count; k++)
			{
				MeshRenderer meshRenderer4 = list2[k];
				_sourceRenderers.Add(new SourceRendererState
				{
					Renderer = meshRenderer4,
					WasEnabled = meshRenderer4.enabled
				});
			}
			for (int l = 0; l < _sourceRenderers.Count; l++)
			{
				MeshRenderer renderer = _sourceRenderers[l].Renderer;
				if (renderer != null)
				{
					renderer.enabled = false;
				}
			}
			_nextSequence = 1;
			_isCutInProgress = false;
			IsSessionActive = true;
			return true;
		}

		public bool TryGetNextCutPoint(out Vector3 cutPoint)
		{
			cutPoint = default;
			if (!IsSessionActive || _isCutInProgress || _outerShellEntry?.Target == null)
			{
				return false;
			}
			if (!TryGetProjectionBounds(_outerShellEntry.Target, _rollAxis, _rollLateral, _surfaceNormal, out var axisMin, out var axisMax, out var lateralMin, out var lateralMax, out var upMin, out var upMax))
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
			MeshFilter component = _outerShellEntry.Target.GetComponent<MeshFilter>();
			if (component == null || component.sharedMesh == null)
			{
				return false;
			}
			MeshCollider meshCollider = _outerShellEntry.Target.gameObject.AddComponent<MeshCollider>();
			meshCollider.sharedMesh = component.sharedMesh;
			meshCollider.convex = false;
			float num4 = (lateralMin + lateralMax) * 0.5f;
			float num5 = (lateralMax - lateralMin) * 0.5f;
			float maxDistance = Mathf.Max(0.05f, upMax - upMin) + 0.5f;
			float num6 = UnityEngine.Random.Range(-0.12f, 0.12f) * num5;
			float[] array = new float[6]
			{
				num6,
				0f,
				num5 * 0.2f,
				(0f - num5) * 0.2f,
				num5 * 0.4f,
				(0f - num5) * 0.4f
			};
			bool result = false;
			for (int i = 0; i < array.Length; i++)
			{
				Vector3 origin = _rollAxis * num3 + _rollLateral * (num4 + array[i]) + _surfaceNormal * (upMax + 0.25f);
				Ray ray = new Ray(origin, -_surfaceNormal);
				if (meshCollider.Raycast(ray, out var hitInfo, maxDistance))
				{
					cutPoint = hitInfo.point;
					result = true;
					break;
				}
			}
			meshCollider.enabled = false;
			UnityEngine.Object.Destroy(meshCollider);
			return result;
		}

		public Vector3 ConstrainPlaneNormal(Vector3 requestedNormal, float maxAngleDegrees)
		{
			Vector3 vector = Vector3.ProjectOnPlane(requestedNormal, _surfaceNormal);
			if (vector.sqrMagnitude <= 1E-05f)
			{
				return _rollAxis;
			}
			vector.Normalize();
			if (Vector3.Dot(vector, _rollAxis) < 0f)
			{
				vector = -vector;
			}
			float maxRadiansDelta = Mathf.Max(0f, maxAngleDegrees) * (MathF.PI / 180f);
			return Vector3.RotateTowards(_rollAxis, vector, maxRadiansDelta, 0f).normalized;
		}

		public bool RequestCut(int sequence, Vector3 worldPoint, Vector3 worldNormal, Action<bool> completed)
		{
			if (!IsSessionActive || sequence <= 0)
			{
				return false;
			}
			int num = _nextSequence + _pendingCuts.Count + (_isCutInProgress ? 1 : 0);
			if (sequence != num)
			{
				return false;
			}
			_pendingCuts.Enqueue(new PendingCut
			{
				Sequence = sequence,
				Point = worldPoint,
				Normal = worldNormal.normalized,
				Completed = completed
			});
			TryStartNextCut();
			return true;
		}

		public void CancelSession()
		{
			_sessionVersion++;
			IsSessionActive = false;
			_isCutInProgress = false;
			while (_pendingCuts.Count > 0)
			{
				_pendingCuts.Dequeue().Completed?.Invoke(obj: false);
			}
			if (_activeTransaction != null)
			{
				DestroyStagedObjects(_activeTransaction);
				_activeTransaction = null;
			}
			for (int i = 0; i < _runtimeEntries.Count; i++)
			{
				DestroyRuntimeEntry(_runtimeEntries[i]);
			}
			_runtimeEntries.Clear();
			_outerShellEntry = null;
			if (_runtimeRoot != null)
			{
				_runtimeRoot.SetActive(value: false);
				UnityEngine.Object.Destroy(_runtimeRoot);
				_runtimeRoot = null;
			}
			for (int j = 0; j < _sourceRenderers.Count; j++)
			{
				SourceRendererState sourceRendererState = _sourceRenderers[j];
				if (sourceRendererState.Renderer != null)
				{
					sourceRendererState.Renderer.enabled = sourceRendererState.WasEnabled;
				}
			}
			_sourceRenderers.Clear();
			_nextSequence = 1;
			_requiredCutCount = 0;
			_sliceThickness = 0f;
		}

		private void OnDestroy()
		{
			CancelSession();
		}

		private void TryStartNextCut()
		{
			if (IsSessionActive && !_isCutInProgress && _pendingCuts.Count != 0)
			{
				PendingCut pendingCut = _pendingCuts.Peek();
				if (pendingCut.Sequence == _nextSequence)
				{
					_pendingCuts.Dequeue();
					BeginCutTransaction(pendingCut);
				}
			}
		}

		private void BeginCutTransaction(PendingCut request)
		{
			CutTransaction transaction = new CutTransaction
			{
				SessionVersion = _sessionVersion,
				Request = request
			};
			List<RuntimeMeshEntry> list = new List<RuntimeMeshEntry>();
			bool flag = false;
			for (int i = 0; i < _runtimeEntries.Count; i++)
			{
				RuntimeMeshEntry runtimeMeshEntry = _runtimeEntries[i];
				MeshPlaneRelation meshPlaneRelation = ClassifyEntry(runtimeMeshEntry, request.Point, request.Normal);
				transaction.Relations.Add(runtimeMeshEntry, meshPlaneRelation);
				if (meshPlaneRelation == MeshPlaneRelation.Straddling)
				{
					list.Add(runtimeMeshEntry);
					if (runtimeMeshEntry.IsOuterShell)
					{
						flag = true;
					}
				}
			}
			if (!flag || list.Count == 0)
			{
				request.Completed?.Invoke(obj: false);
				return;
			}
			transaction.PendingOperations = list.Count;
			_activeTransaction = transaction;
			_isCutInProgress = true;
			for (int j = 0; j < list.Count; j++)
			{
				RuntimeMeshEntry entry = list[j];
				Cut(entry.Target, request.Point, request.Normal, (bool success, Info info) =>
				{
					OnCutAlgorithmCompleted(success, info, transaction, entry);
				}, (Info info, MeshCreationData creationData) =>
				{
					OnCutObjectsCreated(info, creationData, transaction, entry);
				});
			}
		}

		private MeshPlaneRelation ClassifyEntry(RuntimeMeshEntry entry, Vector3 point, Vector3 normal)
		{
			MeshFilter meshFilter = ((entry.Target != null) ? entry.Target.GetComponent<MeshFilter>() : null);
			Mesh mesh = ((meshFilter != null) ? meshFilter.sharedMesh : null);
			if (mesh == null)
			{
				return MeshPlaneRelation.Discard;
			}
			bool flag = false;
			bool flag2 = false;
			Vector3[] vertices = mesh.vertices;
			for (int i = 0; i < vertices.Length; i++)
			{
				float num = Vector3.Dot(entry.Target.transform.TransformPoint(vertices[i]) - point, normal);
				if (num > 1E-05f)
				{
					flag = true;
				}
				else if (num < -1E-05f)
				{
					flag2 = true;
				}
				if (flag & flag2)
				{
					return MeshPlaneRelation.Straddling;
				}
			}
			bool flag3 = Vector3.Dot(_keepAnchorWorld - point, normal) > 0f;
			if ((flag & flag3) || (flag2 && !flag3))
			{
				return MeshPlaneRelation.Keep;
			}
			return MeshPlaneRelation.Discard;
		}

		private void OnCutAlgorithmCompleted(bool success, Info info, CutTransaction transaction, RuntimeMeshEntry entry)
		{
			if (!IsCurrentTransaction(transaction))
			{
				if (success)
				{
					SuppressCreatedMeshes(info);
				}
				return;
			}
			if (!success)
			{
				transaction.Failed = true;
				CompleteTransactionOperation(transaction);
				return;
			}
			int side = info.Plane.GetSide(entry.Target.transform.InverseTransformPoint(_keepAnchorWorld));
			if (!KeepOnlyRequestedSide(info, side))
			{
				transaction.Failed = true;
			}
		}

		private void OnCutObjectsCreated(Info info, MeshCreationData creationData, CutTransaction transaction, RuntimeMeshEntry entry)
		{
			if (!IsCurrentTransaction(transaction))
			{
				DestroyCreationData(creationData);
				return;
			}
			RuntimeMeshEntry runtimeMeshEntry = null;
			if (creationData?.CreatedTargets != null)
			{
				for (int i = 0; i < creationData.CreatedTargets.Length; i++)
				{
					MeshTarget meshTarget = creationData.CreatedTargets[i];
					GameObject gameObject = ((creationData.CreatedObjects != null && i < creationData.CreatedObjects.Length) ? creationData.CreatedObjects[i] : null);
					if (meshTarget == null || gameObject == null || runtimeMeshEntry != null)
					{
						if (gameObject != null)
						{
							DestroyRuntimeObject(gameObject);
						}
						transaction.Failed = true;
						continue;
					}
					gameObject.transform.SetParent(_runtimeRoot.transform, worldPositionStays: true);
					gameObject.name = $"{entry.Target.name}_Retained_{transaction.Request.Sequence}";
					SetLayerRecursively(gameObject.transform, entry.Layer);
					ConfigureMeshTarget(meshTarget, gameObject);
					SetRenderersEnabled(gameObject, enabled: false);
					transaction.StagedObjects.Add(gameObject);
					runtimeMeshEntry = new RuntimeMeshEntry
					{
						Target = meshTarget,
						IsOuterShell = entry.IsOuterShell,
						Layer = entry.Layer
					};
				}
			}
			if (runtimeMeshEntry == null)
			{
				transaction.Failed = true;
			}
			else
			{
				transaction.Replacements[entry] = runtimeMeshEntry;
			}
			CompleteTransactionOperation(transaction);
		}

		private void CompleteTransactionOperation(CutTransaction transaction)
		{
			if (IsCurrentTransaction(transaction))
			{
				transaction.PendingOperations--;
				if (transaction.PendingOperations <= 0)
				{
					CompleteCutTransaction(transaction);
				}
			}
		}

		private void CompleteCutTransaction(CutTransaction transaction)
		{
			if (!IsCurrentTransaction(transaction))
			{
				return;
			}
			_activeTransaction = null;
			_isCutInProgress = false;
			if (transaction.Failed)
			{
				DestroyStagedObjects(transaction);
				transaction.Request.Completed?.Invoke(obj: false);
				FailPendingCuts();
				return;
			}
			List<RuntimeMeshEntry> list = new List<RuntimeMeshEntry>(_runtimeEntries.Count);
			RuntimeMeshEntry outerShellEntry = null;
			for (int i = 0; i < _runtimeEntries.Count; i++)
			{
				RuntimeMeshEntry runtimeMeshEntry = _runtimeEntries[i];
				switch (transaction.Relations[runtimeMeshEntry])
				{
				case MeshPlaneRelation.Keep:
					list.Add(runtimeMeshEntry);
					if (runtimeMeshEntry.IsOuterShell)
					{
						outerShellEntry = runtimeMeshEntry;
					}
					continue;
				case MeshPlaneRelation.Discard:
					DestroyRuntimeEntry(runtimeMeshEntry);
					continue;
				}
				RuntimeMeshEntry runtimeMeshEntry2 = transaction.Replacements[runtimeMeshEntry];
				DestroyRuntimeEntry(runtimeMeshEntry);
				SetRenderersEnabled(runtimeMeshEntry2.Target.GameobjectRoot, enabled: true);
				list.Add(runtimeMeshEntry2);
				if (runtimeMeshEntry2.IsOuterShell)
				{
					outerShellEntry = runtimeMeshEntry2;
				}
			}
			transaction.StagedObjects.Clear();
			_runtimeEntries.Clear();
			_runtimeEntries.AddRange(list);
			_outerShellEntry = outerShellEntry;
			_nextSequence++;
			bool flag = IsSessionActive && _outerShellEntry != null;
			transaction.Request.Completed?.Invoke(flag);
			if (flag)
			{
				TryStartNextCut();
			}
		}

		private void FailPendingCuts()
		{
			while (_pendingCuts.Count > 0)
			{
				_pendingCuts.Dequeue().Completed?.Invoke(obj: false);
			}
		}

		private bool IsCurrentTransaction(CutTransaction transaction)
		{
			if (transaction != null && transaction == _activeTransaction && transaction.SessionVersion == _sessionVersion)
			{
				return IsSessionActive;
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

		private RuntimeMeshEntry CreateRuntimeMeshEntry(MeshRenderer sourceRenderer, bool isOuterShell)
		{
			MeshFilter component = sourceRenderer.GetComponent<MeshFilter>();
			if (component == null || component.sharedMesh == null)
			{
				return null;
			}
			Mesh mesh = UnityEngine.Object.Instantiate(component.sharedMesh);
			mesh.name = component.sharedMesh.name + "_ChefDeskRuntime";
			GameObject gameObject = new GameObject(sourceRenderer.name + "_RuntimeCutMesh");
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
			MeshTarget target = gameObject.AddComponent<MeshTarget>();
			ConfigureMeshTarget(target, gameObject);
			return new RuntimeMeshEntry
			{
				Target = target,
				IsOuterShell = isOuterShell,
				Layer = sourceRenderer.gameObject.layer
			};
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

		private static List<MeshRenderer> CollectValidMeshRenderers(Transform root)
		{
			List<MeshRenderer> list = new List<MeshRenderer>();
			AddValidMeshRenderers(root, list, new HashSet<MeshRenderer>());
			return list;
		}

		private static void AddValidMeshRenderers(Transform root, List<MeshRenderer> destination, HashSet<MeshRenderer> uniqueRenderers)
		{
			if (root == null)
			{
				return;
			}
			MeshRenderer[] componentsInChildren = root.GetComponentsInChildren<MeshRenderer>(includeInactive: false);
			foreach (MeshRenderer meshRenderer in componentsInChildren)
			{
				MeshFilter meshFilter = ((meshRenderer != null) ? meshRenderer.GetComponent<MeshFilter>() : null);
				if (!(meshRenderer == null) && meshRenderer.enabled && meshRenderer.gameObject.activeInHierarchy && !(meshFilter == null) && !(meshFilter.sharedMesh == null) && uniqueRenderers.Add(meshRenderer))
				{
					destination.Add(meshRenderer);
				}
			}
		}

		private static MeshRenderer ResolvePrimaryOuterRenderer(List<MeshRenderer> renderers)
		{
			MeshRenderer result = null;
			float num = -1f;
			for (int i = 0; i < renderers.Count; i++)
			{
				MeshRenderer meshRenderer = renderers[i];
				float num2 = ((meshRenderer != null) ? meshRenderer.bounds.size.sqrMagnitude : 0f);
				if (!(num2 <= num))
				{
					result = meshRenderer;
					num = num2;
				}
			}
			return result;
		}

		private static void AlignCuttingRollToPresentation(Transform cuttingRollRoot, MeshRenderer cuttingOuterRenderer, MeshRenderer presentationOuterRenderer)
		{
			Quaternion quaternion = presentationOuterRenderer.transform.rotation * Quaternion.Inverse(cuttingOuterRenderer.transform.rotation);
			cuttingRollRoot.rotation = quaternion * cuttingRollRoot.rotation;
			cuttingRollRoot.position += presentationOuterRenderer.bounds.center - cuttingOuterRenderer.bounds.center;
		}

		private bool TryResolveRollAxis(Transform reference, MeshTarget outerTarget, out Vector3 axis, out Vector3 lateral)
		{
			axis = default;
			lateral = default;
			Vector3 vector = Vector3.ProjectOnPlane(reference.right, _surfaceNormal);
			if (vector.sqrMagnitude <= 1E-05f)
			{
				vector = Vector3.ProjectOnPlane(reference.forward, _surfaceNormal);
			}
			if (vector.sqrMagnitude <= 1E-05f)
			{
				return false;
			}
			vector.Normalize();
			Vector3 normalized = Vector3.Cross(_surfaceNormal, vector).normalized;
			MeshFilter component = outerTarget.GetComponent<MeshFilter>();
			Mesh mesh = ((component != null) ? component.sharedMesh : null);
			if (mesh == null || mesh.vertexCount < 2)
			{
				return false;
			}
			Vector3[] vertices = mesh.vertices;
			float num = 0f;
			float num2 = 0f;
			for (int i = 0; i < vertices.Length; i++)
			{
				Vector3 lhs = outerTarget.transform.TransformPoint(vertices[i]);
				num += Vector3.Dot(lhs, vector);
				num2 += Vector3.Dot(lhs, normalized);
			}
			num /= (float)vertices.Length;
			num2 /= (float)vertices.Length;
			float num3 = 0f;
			float num4 = 0f;
			float num5 = 0f;
			for (int j = 0; j < vertices.Length; j++)
			{
				Vector3 lhs2 = outerTarget.transform.TransformPoint(vertices[j]);
				float num6 = Vector3.Dot(lhs2, vector) - num;
				float num7 = Vector3.Dot(lhs2, normalized) - num2;
				num3 += num6 * num6;
				num4 += num7 * num7;
				num5 += num6 * num7;
			}
			float f = 0.5f * Mathf.Atan2(2f * num5, num3 - num4);
			axis = (vector * Mathf.Cos(f) + normalized * Mathf.Sin(f)).normalized;
			Vector3 rhs = ((Mathf.Abs(Vector3.Dot(axis, vector)) >= Mathf.Abs(Vector3.Dot(axis, normalized))) ? vector : normalized);
			if (Vector3.Dot(axis, rhs) < 0f)
			{
				axis = -axis;
			}
			lateral = Vector3.Cross(_surfaceNormal, axis).normalized;
			return lateral.sqrMagnitude > 1E-05f;
		}

		private static bool TryGetProjectionBounds(MeshTarget target, Vector3 axis, Vector3 lateral, Vector3 up, out float axisMin, out float axisMax, out float lateralMin, out float lateralMax, out float upMin, out float upMax)
		{
			axisMin = (lateralMin = (upMin = float.PositiveInfinity));
			axisMax = (lateralMax = (upMax = float.NegativeInfinity));
			MeshFilter meshFilter = ((target != null) ? target.GetComponent<MeshFilter>() : null);
			Mesh mesh = ((meshFilter != null) ? meshFilter.sharedMesh : null);
			if (mesh == null || mesh.vertexCount == 0)
			{
				return false;
			}
			Vector3[] vertices = mesh.vertices;
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
			return true;
		}

		private static void SetLayerRecursively(Transform root, int layer)
		{
			if (!(root == null))
			{
				root.gameObject.layer = layer;
				for (int i = 0; i < root.childCount; i++)
				{
					SetLayerRecursively(root.GetChild(i), layer);
				}
			}
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

		private void DestroyStagedObjects(CutTransaction transaction)
		{
			for (int i = 0; i < transaction.StagedObjects.Count; i++)
			{
				DestroyRuntimeObject(transaction.StagedObjects[i]);
			}
			transaction.StagedObjects.Clear();
		}

		private void DestroyCreationData(MeshCreationData creationData)
		{
			if (creationData?.CreatedObjects != null)
			{
				for (int i = 0; i < creationData.CreatedObjects.Length; i++)
				{
					DestroyRuntimeObject(creationData.CreatedObjects[i]);
				}
			}
		}

		private void DestroyRuntimeEntry(RuntimeMeshEntry entry)
		{
			if (!(entry?.Target == null))
			{
				GameObject root = ((entry.Target.GameobjectRoot != null) ? entry.Target.GameobjectRoot : entry.Target.gameObject);
				DestroyRuntimeObject(root);
			}
		}

		private void DestroyRuntimeObject(GameObject root)
		{
			if (root == null)
			{
				return;
			}
			root.SetActive(value: false);
			MeshFilter[] componentsInChildren = root.GetComponentsInChildren<MeshFilter>(includeInactive: true);
			HashSet<Mesh> hashSet = new HashSet<Mesh>();
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				Mesh sharedMesh = componentsInChildren[i].sharedMesh;
				componentsInChildren[i].sharedMesh = null;
				if (sharedMesh != null && hashSet.Add(sharedMesh))
				{
					UnityEngine.Object.Destroy(sharedMesh);
				}
			}
			UnityEngine.Object.Destroy(root);
		}
	}
}
