using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CableSystem
{
	public class Cable : MonoBehaviour
	{
		public enum GuidePathSortMode
		{
			HierarchyOrder = 0,
			DistanceFromInitial = 1,
			NearestPath = 2
		}

		[Header("-- Settings --")]
		public GameObject InitialPoint;

		public GameObject DestinyPoint;

		private LineRenderer lineRenderer;

		public Material CableMaterial;

		public int Segments = 20;

		[Header("-- Runtime Extend Cable --")]
		public bool Extend;

		public int QuantityExtend;

		[Header("-- Runtime Retract Cable--")]
		public bool Retract;

		public int QuantityRetract;

		[Header("-- Runtime Cut Cable--")]
		public bool CutCableSegment;

		public GameObject CableSegment;

		[Header("-- Runtime --")]
		[Range(0f, 50f)]
		public int smoothSteps = 5;

		public bool UseGravity;

		public float CableWidth = 0.03f;

		public float Spring = 300f;

		public float Damping = 1f;

		public float MinDamping;

		public float MaxDamping = 0.01f;

		public float CableMass = 1f;

		public float CableColisionWidth = 0.07f;

		public float LinealFriccion = 1f;

		public float AngularFriccion = 1f;

		[Header("-- Collision --")]
		public bool DisableSegmentColliders = true;

		public bool ApplyGeneratedSegmentLayer = true;

		public string GeneratedSegmentLayerName = "DivingHose";

		public LayerMask LinecastMask = -1;

		[Header("-- Visual Only --")]
		public bool VisualOnly;

		[Range(1f, 12f)]
		public int VisualOnlyConstraintIterations = 5;

		[Range(0f, 1f)]
		public float VisualOnlyDamping = 0.92f;

		public float VisualOnlyGravityScale = 1f;

		[Header("-- Guide Path --")]
		public bool UseGuidePath;

		public List<Transform> GuidePoints = new List<Transform>();

		public GuidePathSortMode GuidePointSortMode = GuidePathSortMode.NearestPath;

		public bool GuidePathUseSag = true;

		public float GuidePathSag = 0.035f;

		public float GuidePathMaxSag = 0.14f;

		public List<GameObject> CableSegments;

		private int _generatedSegmentLayer = -1;

		private bool _createdDestinyPoint;

		private bool _initialized;

		private bool _visualOnlyStateInitialized;

		private float _visualOnlyRestDistance;

		private readonly List<Vector3> _visualOnlyPreviousPositions = new List<Vector3>();

		private readonly List<Transform> _guideOrderBuffer = new List<Transform>();

		private readonly List<Vector3> _guidePathBuffer = new List<Vector3>();

		private readonly List<Vector3> _guideSagPathBuffer = new List<Vector3>();

		private void Start()
		{
			if (CableSegments == null)
			{
				CableSegments = new List<GameObject>();
			}
			ResolveGeneratedSegmentLayer();
			CreateAllSegments();
			CutCableSegment = false;
			CableSegment = null;
			EnsureLineRenderer();
			ApplyLineRendererSettings();
			_initialized = true;
			ApplyCableSimulationMode();
		}

		private void Update()
		{
			if (!UseGuidePath && !VisualOnly)
			{
				SetupLineRenderer();
				UpdateParameters();
				if (Extend)
				{
					ExtendCable(QuantityExtend);
					Extend = false;
				}
				if (Retract)
				{
					RetractCable(QuantityRetract);
					Retract = false;
				}
				if (CutCableSegment)
				{
					CutCable(CableSegment);
					CutCableSegment = false;
				}
			}
		}

		private void LateUpdate()
		{
			if (UseGuidePath)
			{
				ApplyLineRendererSettings();
				SetupGuidePathRenderer();
			}
			else if (VisualOnly)
			{
				SimulateVisualOnlyCable(Time.deltaTime);
				ApplyLineRendererSettings();
				SetupLineRenderer();
			}
		}

		private void CreateAllSegments()
		{
			if (!DestinyPoint)
			{
				DestinyPoint = new GameObject("DestinyPoint");
				DestinyPoint.transform.position = base.transform.position;
				_createdDestinyPoint = true;
			}
			if ((bool)InitialPoint.GetComponent<Rigidbody>())
			{
				InitialPoint.GetComponent<Rigidbody>();
			}
			else
			{
				Rigidbody rigidbody = InitialPoint.AddComponent<Rigidbody>();
				rigidbody.isKinematic = true;
				rigidbody.useGravity = false;
			}
			Vector3 normalized = (DestinyPoint.transform.position - InitialPoint.transform.position).normalized;
			float num = Vector3.Distance(InitialPoint.transform.position, DestinyPoint.transform.position) / (float)(Segments + 1);
			GameObject gameObject = InitialPoint;
			for (int i = 0; i < Segments; i++)
			{
				Vector3 position = InitialPoint.transform.position + normalized * num * (i + 1);
				GameObject gameObject2 = CreateSegment(position);
				gameObject2.GetComponent<SpringJoint>().connectedBody = gameObject.GetComponent<Rigidbody>();
				gameObject = gameObject2;
				CableSegments.Add(gameObject2);
			}
			CreateFinalSegment(gameObject.GetComponent<Rigidbody>());
			for (int j = 0; j < CableSegments.Count; j++)
			{
				CableSegments[j].name = "CableSegment" + j;
			}
			DestinyPoint.name = "DestinyCable";
			OrderCableHierarchy();
		}

		private GameObject CreateSegment(Vector3 position)
		{
			GameObject gameObject = new GameObject("CableSegment");
			gameObject.transform.SetParent(base.transform);
			gameObject.transform.position = position;
			Rigidbody rigidbody = gameObject.AddComponent<Rigidbody>();
			rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
			rigidbody.mass = CableMass;
			rigidbody.linearDamping = LinealFriccion;
			rigidbody.angularDamping = AngularFriccion;
			SphereCollider sphereCollider = gameObject.AddComponent<SphereCollider>();
			sphereCollider.radius = CableColisionWidth;
			ApplySegmentCollisionSettings(gameObject, sphereCollider, applyLayer: true);
			gameObject.AddComponent<SpringJoint>().autoConfigureConnectedAnchor = false;
			return gameObject;
		}

		private void CreateFinalSegment(Rigidbody cuerpoConectado)
		{
			if (!DestinyPoint.GetComponent<SpringJoint>())
			{
				DestinyPoint.AddComponent<SpringJoint>();
				DestinyPoint.GetComponent<SpringJoint>().connectedBody = cuerpoConectado;
			}
			SpringJoint component = DestinyPoint.GetComponent<SpringJoint>();
			component.autoConfigureConnectedAnchor = false;
			component.anchor = Vector3.zero;
			component.connectedAnchor = Vector3.zero;
			SphereCollider sphereCollider = DestinyPoint.AddComponent<SphereCollider>();
			sphereCollider.radius = CableColisionWidth;
			ApplySegmentCollisionSettings(DestinyPoint, sphereCollider, _createdDestinyPoint);
		}

		public void SetupLineRenderer()
		{
			if (UseGuidePath)
			{
				SetupGuidePathRenderer();
				return;
			}
			List<Vector3> list = new List<Vector3>();
			list.Add(InitialPoint.transform.position);
			foreach (GameObject cableSegment in CableSegments)
			{
				list.Add(cableSegment.transform.position);
			}
			list.Add(DestinyPoint.transform.position);
			List<Vector3> list2 = new List<Vector3>();
			for (int i = 0; i < list.Count - 1; i++)
			{
				Vector3 vector = list[i];
				Vector3 vector2 = list[i + 1];
				list2.Add(vector);
				if (Physics.Linecast(vector, vector2, out var hitInfo, LinecastMask, QueryTriggerInteraction.Ignore) && !hitInfo.transform.IsChildOf(base.transform))
				{
					Vector3 preciseEdge = GetPreciseEdge(hitInfo, vector, vector2);
					if (Vector3.Distance(preciseEdge, vector) > 0.3f && Vector3.Distance(preciseEdge, vector2) > 0.3f)
					{
						list2.Add(preciseEdge);
					}
				}
			}
			list2.Add(list[list.Count - 1]);
			DrawSmoothCable(list2);
		}

		private Vector3 GetPreciseEdge(RaycastHit hit, Vector3 start, Vector3 end)
		{
			Bounds bounds = hit.collider.bounds;
			Vector3 point = hit.point;
			float num = Mathf.Abs(point.y - bounds.max.y);
			float num2 = Mathf.Abs(point.y - bounds.min.y);
			float num3 = Mathf.Abs(point.x - bounds.max.x);
			float num4 = Mathf.Abs(point.x - bounds.min.x);
			Vector3 vector = point;
			float num5 = Mathf.Min(num, num2, num3, num4);
			if (num5 == num)
			{
				vector.y = bounds.max.y;
			}
			else if (num5 == num2)
			{
				vector.y = bounds.min.y;
			}
			else if (num5 == num3)
			{
				vector.x = bounds.max.x;
			}
			else
			{
				vector.x = bounds.min.x;
			}
			return vector + hit.normal * 0.06f;
		}

		private void DrawSmoothCable(List<Vector3> points)
		{
			if (points.Count < 2 || !EnsureLineRenderer())
			{
				return;
			}
			int num = points.Count - 1;
			lineRenderer.positionCount = num * smoothSteps + 1;
			int num2 = 0;
			for (int i = 0; i < num; i++)
			{
				Vector3 vector = points[i];
				Vector3 vector2 = points[i + 1];
				Vector3 p = ((i == 0) ? vector : points[i - 1]);
				Vector3 p2 = ((i + 2 < points.Count) ? points[i + 2] : vector2);
				for (int j = 0; j < smoothSteps; j++)
				{
					float t = (float)j / (float)smoothSteps;
					lineRenderer.SetPosition(num2, GetCatmullRomPoint(p, vector, vector2, p2, t));
					num2++;
				}
			}
			lineRenderer.SetPosition(lineRenderer.positionCount - 1, points[points.Count - 1]);
		}

		private Vector3 GetCatmullRomPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
		{
			float num = t * t;
			float num2 = num * t;
			return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * num + (-p0 + 3f * p1 - 3f * p2 + p3) * num2);
		}

		private void UpdateParameters()
		{
			foreach (GameObject cableSegment in CableSegments)
			{
				SpringJoint component = cableSegment.GetComponent<SpringJoint>();
				Rigidbody component2 = cableSegment.GetComponent<Rigidbody>();
				SphereCollider component3 = cableSegment.GetComponent<SphereCollider>();
				component.spring = Spring;
				component.damper = Damping;
				component.minDistance = MinDamping;
				component.maxDistance = MaxDamping;
				component2.mass = CableMass;
				component2.linearDamping = LinealFriccion;
				component2.angularDamping = AngularFriccion;
				component2.useGravity = UseGravity;
				if (component3 != null)
				{
					component3.radius = CableColisionWidth;
					component3.enabled = !DisableSegmentColliders;
				}
				ApplyGeneratedLayer(cableSegment);
			}
			SpringJoint component4 = DestinyPoint.GetComponent<SpringJoint>();
			SphereCollider component5 = DestinyPoint.GetComponent<SphereCollider>();
			component4.spring = Spring;
			component4.damper = Damping;
			component4.minDistance = MinDamping;
			component4.maxDistance = MaxDamping;
			if (component5 != null)
			{
				component5.radius = CableColisionWidth;
				component5.enabled = !DisableSegmentColliders;
			}
			if (_createdDestinyPoint)
			{
				ApplyGeneratedLayer(DestinyPoint);
			}
			ApplyLineRendererSettings();
		}

		private void ApplyLineRendererSettings()
		{
			if (EnsureLineRenderer())
			{
				lineRenderer.startWidth = CableWidth;
				lineRenderer.endWidth = CableWidth;
				if (CableMaterial != null && lineRenderer.material != CableMaterial)
				{
					lineRenderer.material = CableMaterial;
				}
			}
		}

		private bool EnsureLineRenderer()
		{
			if (lineRenderer != null)
			{
				return true;
			}
			if (!TryGetComponent<LineRenderer>(out lineRenderer))
			{
				lineRenderer = base.gameObject.AddComponent<LineRenderer>();
				lineRenderer.shadowCastingMode = ShadowCastingMode.TwoSided;
			}
			if (CableMaterial != null)
			{
				lineRenderer.material = CableMaterial;
			}
			return lineRenderer != null;
		}

		private void ResolveGeneratedSegmentLayer()
		{
			_generatedSegmentLayer = -1;
			if (ApplyGeneratedSegmentLayer && !string.IsNullOrWhiteSpace(GeneratedSegmentLayerName))
			{
				int num = LayerMask.NameToLayer(GeneratedSegmentLayerName);
				if (num < 0)
				{
					Debug.LogWarning("[Cable] Layer '" + GeneratedSegmentLayerName + "' was not found. Generated cable segments will keep their current layer.", this);
				}
				else
				{
					_generatedSegmentLayer = num;
				}
			}
		}

		private void ApplySegmentCollisionSettings(GameObject segment, SphereCollider sphere, bool applyLayer)
		{
			if (sphere != null)
			{
				sphere.enabled = !DisableSegmentColliders;
			}
			if (applyLayer)
			{
				ApplyGeneratedLayer(segment);
			}
		}

		private void ApplyGeneratedLayer(GameObject segment)
		{
			if (_generatedSegmentLayer >= 0 && !(segment == null))
			{
				segment.layer = _generatedSegmentLayer;
			}
		}

		public void SetVisualOnlyMode(bool visualOnly)
		{
			VisualOnly = visualOnly;
			ApplyCableSimulationMode();
		}

		public void SetGuidePathMode(bool useGuidePath, IList<Transform> guidePoints)
		{
			UseGuidePath = useGuidePath;
			GuidePoints.Clear();
			if (guidePoints != null)
			{
				for (int i = 0; i < guidePoints.Count; i++)
				{
					Transform transform = guidePoints[i];
					if (!(transform == null) && !GuidePoints.Contains(transform))
					{
						GuidePoints.Add(transform);
					}
				}
			}
			ApplyCableSimulationMode();
		}

		private void ApplyCableSimulationMode()
		{
			if (CableSegments == null)
			{
				return;
			}
			bool flag = !UseGuidePath && !VisualOnly;
			for (int i = 0; i < CableSegments.Count; i++)
			{
				GameObject gameObject = CableSegments[i];
				if (gameObject == null)
				{
					continue;
				}
				Rigidbody component = gameObject.GetComponent<Rigidbody>();
				if (component != null)
				{
					component.isKinematic = !flag;
					component.useGravity = flag && UseGravity;
					if (!flag)
					{
						component.linearVelocity = Vector3.zero;
						component.angularVelocity = Vector3.zero;
					}
				}
				SpringJoint component2 = gameObject.GetComponent<SpringJoint>();
				if (component2 != null)
				{
					component2.enableCollision = flag;
				}
				SphereCollider component3 = gameObject.GetComponent<SphereCollider>();
				if (component3 != null)
				{
					component3.enabled = flag && !DisableSegmentColliders;
				}
			}
			SpringJoint springJoint = ((DestinyPoint != null) ? DestinyPoint.GetComponent<SpringJoint>() : null);
			if (springJoint != null)
			{
				springJoint.enableCollision = flag;
			}
			if (UseGuidePath)
			{
				_visualOnlyStateInitialized = false;
				SetupGuidePathRenderer();
			}
			else if (VisualOnly)
			{
				InitializeVisualOnlyState(preserveCurrentPositions: false);
			}
			else if (_initialized)
			{
				_visualOnlyStateInitialized = false;
				UpdateParameters();
			}
		}

		private void SetupGuidePathRenderer()
		{
			if (InitialPoint == null || DestinyPoint == null)
			{
				return;
			}
			_guidePathBuffer.Clear();
			AddGuidePathPoint(InitialPoint.transform.position);
			AddOrderedGuidePointsToPath();
			AddGuidePathPoint(DestinyPoint.transform.position);
			if (_guidePathBuffer.Count < 2)
			{
				return;
			}
			if (!GuidePathUseSag || GuidePathSag <= 0f)
			{
				DrawSmoothCable(_guidePathBuffer);
				return;
			}
			_guideSagPathBuffer.Clear();
			for (int i = 0; i < _guidePathBuffer.Count - 1; i++)
			{
				Vector3 vector = _guidePathBuffer[i];
				Vector3 vector2 = _guidePathBuffer[i + 1];
				_guideSagPathBuffer.Add(vector);
				float num = Vector3.Distance(vector, vector2);
				if (num > 0.05f)
				{
					float num2 = Mathf.Min(GuidePathMaxSag, num * GuidePathSag);
					_guideSagPathBuffer.Add((vector + vector2) * 0.5f + Vector3.down * num2);
				}
			}
			_guideSagPathBuffer.Add(_guidePathBuffer[_guidePathBuffer.Count - 1]);
			DrawSmoothCable(_guideSagPathBuffer);
		}

		private void AddOrderedGuidePointsToPath()
		{
			_guideOrderBuffer.Clear();
			for (int i = 0; i < GuidePoints.Count; i++)
			{
				Transform transform = GuidePoints[i];
				if (!(transform == null) && !_guideOrderBuffer.Contains(transform))
				{
					_guideOrderBuffer.Add(transform);
				}
			}
			if (_guideOrderBuffer.Count == 0)
			{
				return;
			}
			if (GuidePointSortMode == GuidePathSortMode.DistanceFromInitial)
			{
				Vector3 start = InitialPoint.transform.position;
				_guideOrderBuffer.Sort((Transform a, Transform b) => Vector3.SqrMagnitude(a.position - start).CompareTo(Vector3.SqrMagnitude(b.position - start)));
			}
			else if (GuidePointSortMode == GuidePathSortMode.NearestPath)
			{
				SortGuidePointsByNearestPath();
			}
			for (int num = 0; num < _guideOrderBuffer.Count; num++)
			{
				AddGuidePathPoint(_guideOrderBuffer[num].position);
			}
		}

		private void SortGuidePointsByNearestPath()
		{
			List<Transform> list = new List<Transform>(_guideOrderBuffer);
			_guideOrderBuffer.Clear();
			Vector3 position = InitialPoint.transform.position;
			Vector3 position2 = DestinyPoint.transform.position;
			while (list.Count > 0)
			{
				int index = 0;
				float num = float.MaxValue;
				for (int i = 0; i < list.Count; i++)
				{
					Vector3 position3 = list[i].position;
					float num2 = Vector3.SqrMagnitude(position3 - position) + Vector3.SqrMagnitude(position3 - position2) * 0.1f;
					if (!(num2 >= num))
					{
						num = num2;
						index = i;
					}
				}
				Transform transform = list[index];
				list.RemoveAt(index);
				_guideOrderBuffer.Add(transform);
				position = transform.position;
			}
		}

		private void AddGuidePathPoint(Vector3 point)
		{
			if (_guidePathBuffer.Count <= 0 || !(Vector3.SqrMagnitude(_guidePathBuffer[_guidePathBuffer.Count - 1] - point) < 0.0001f))
			{
				_guidePathBuffer.Add(point);
			}
		}

		private void SimulateVisualOnlyCable(float deltaTime)
		{
			if (InitialPoint == null || DestinyPoint == null || CableSegments == null)
			{
				return;
			}
			int count = CableSegments.Count;
			if (count <= 0)
			{
				return;
			}
			InitializeVisualOnlyState(preserveCurrentPositions: true);
			float num = Mathf.Min(deltaTime, 0.033f);
			Vector3 vector = (UseGravity ? (Physics.gravity * VisualOnlyGravityScale * num * num) : Vector3.zero);
			for (int i = 0; i < count; i++)
			{
				GameObject gameObject = CableSegments[i];
				if (!(gameObject == null))
				{
					Vector3 position = gameObject.transform.position;
					Vector3 vector2 = _visualOnlyPreviousPositions[i];
					Vector3 vector3 = (position - vector2) * VisualOnlyDamping;
					_visualOnlyPreviousPositions[i] = position;
					gameObject.transform.position = position + vector3 + vector;
				}
			}
			int num2 = Mathf.Max(1, VisualOnlyConstraintIterations);
			for (int j = 0; j < num2; j++)
			{
				ApplyVisualOnlyDistanceConstraints();
			}
		}

		private void InitializeVisualOnlyState(bool preserveCurrentPositions)
		{
			if (_visualOnlyStateInitialized && _visualOnlyPreviousPositions.Count == CableSegments.Count)
			{
				return;
			}
			_visualOnlyPreviousPositions.Clear();
			for (int i = 0; i < CableSegments.Count; i++)
			{
				GameObject gameObject = CableSegments[i];
				if (gameObject == null)
				{
					_visualOnlyPreviousPositions.Add(Vector3.zero);
				}
				else
				{
					_visualOnlyPreviousPositions.Add(gameObject.transform.position);
				}
			}
			_visualOnlyRestDistance = CalculateVisualOnlyRestDistance();
			if (!preserveCurrentPositions)
			{
				SeedVisualOnlySegmentPositions();
			}
			_visualOnlyStateInitialized = true;
		}

		private float CalculateVisualOnlyRestDistance()
		{
			if (InitialPoint == null || DestinyPoint == null || CableSegments == null || CableSegments.Count == 0)
			{
				return 0.1f;
			}
			float num = 0f;
			int num2 = 0;
			Vector3 position = InitialPoint.transform.position;
			for (int i = 0; i < CableSegments.Count; i++)
			{
				GameObject gameObject = CableSegments[i];
				if (!(gameObject == null))
				{
					num += Vector3.Distance(position, gameObject.transform.position);
					position = gameObject.transform.position;
					num2++;
				}
			}
			num += Vector3.Distance(position, DestinyPoint.transform.position);
			num2++;
			if (num2 <= 0 || num <= 0.0001f)
			{
				return Vector3.Distance(InitialPoint.transform.position, DestinyPoint.transform.position) / ((float)CableSegments.Count + 1f);
			}
			return num / (float)num2;
		}

		private void SeedVisualOnlySegmentPositions()
		{
			Vector3 position = InitialPoint.transform.position;
			Vector3 position2 = DestinyPoint.transform.position;
			for (int i = 0; i < CableSegments.Count; i++)
			{
				GameObject gameObject = CableSegments[i];
				if (!(gameObject == null))
				{
					float t = ((float)i + 1f) / ((float)CableSegments.Count + 1f);
					Vector3 vector = Vector3.Lerp(position, position2, t);
					gameObject.transform.position = vector;
					_visualOnlyPreviousPositions[i] = vector;
				}
			}
		}

		private void ApplyVisualOnlyDistanceConstraints()
		{
			int num = CableSegments.Count + 1;
			for (int i = 0; i < num; i++)
			{
				bool flag = i == 0;
				bool flag2 = i == num - 1;
				GameObject gameObject = (flag ? null : CableSegments[i - 1]);
				GameObject gameObject2 = (flag2 ? null : CableSegments[i]);
				if ((!flag && gameObject == null) || (!flag2 && gameObject2 == null))
				{
					continue;
				}
				Vector3 vector = (flag ? InitialPoint.transform.position : gameObject.transform.position);
				Vector3 vector2 = (flag2 ? DestinyPoint.transform.position : gameObject2.transform.position) - vector;
				float magnitude = vector2.magnitude;
				if (!(magnitude <= 0.0001f))
				{
					Vector3 vector3 = vector2 * ((magnitude - _visualOnlyRestDistance) / magnitude);
					if (flag && !flag2)
					{
						gameObject2.transform.position -= vector3;
					}
					else if (!flag & flag2)
					{
						gameObject.transform.position += vector3;
					}
					else if (!flag && !flag2)
					{
						gameObject.transform.position += vector3 * 0.5f;
						gameObject2.transform.position -= vector3 * 0.5f;
					}
				}
			}
		}

		public void ExtendCable(int additionalSegments)
		{
			Vector3 normalized;
			float num;
			if (CableSegments.Count > 0)
			{
				GameObject gameObject = CableSegments[0];
				normalized = (gameObject.transform.position - InitialPoint.transform.position).normalized;
				num = Vector3.Distance(InitialPoint.transform.position, gameObject.transform.position);
			}
			else
			{
				normalized = (DestinyPoint.transform.position - InitialPoint.transform.position).normalized;
				num = 0.5f;
			}
			float num2 = num / (float)(additionalSegments + 1);
			GameObject[] array = new GameObject[additionalSegments + CableSegments.Count];
			int num3 = 0;
			for (int i = 0; i < additionalSegments; i++)
			{
				Vector3 position = InitialPoint.transform.position + normalized * num2 * (i + 1);
				GameObject gameObject2 = CreateSegment(position);
				gameObject2.name = "CableSegmentTemp";
				Rigidbody connectedBody = ((num3 == 0) ? InitialPoint.GetComponent<Rigidbody>() : array[num3 - 1].GetComponent<Rigidbody>());
				gameObject2.GetComponent<SpringJoint>().connectedBody = connectedBody;
				array[num3++] = gameObject2;
			}
			for (int j = 0; j < CableSegments.Count; j++)
			{
				array[num3] = CableSegments[j];
				if (num3 > 0)
				{
					array[num3].GetComponent<SpringJoint>().connectedBody = array[num3 - 1].GetComponent<Rigidbody>();
				}
				num3++;
			}
			CableSegments = new List<GameObject>(array);
			for (int k = 0; k < CableSegments.Count; k++)
			{
				CableSegments[k].name = "CableSegment" + k;
			}
			if (CableSegments.Count > 0)
			{
				DestinyPoint.GetComponent<SpringJoint>().connectedBody = CableSegments[CableSegments.Count - 1].GetComponent<Rigidbody>();
			}
			OrderCableHierarchy();
			SetupLineRenderer();
		}

		public void RetractCable(int segmentsToRemove)
		{
			if (segmentsToRemove > CableSegments.Count)
			{
				segmentsToRemove = CableSegments.Count;
			}
			for (int i = 0; i < segmentsToRemove; i++)
			{
				if (CableSegments[i] != null)
				{
					Object.Destroy(CableSegments[i]);
				}
			}
			CableSegments.RemoveRange(0, segmentsToRemove);
			if (CableSegments.Count > 0)
			{
				CableSegments[0].GetComponent<SpringJoint>().connectedBody = InitialPoint.GetComponent<Rigidbody>();
				for (int j = 0; j < CableSegments.Count; j++)
				{
					CableSegments[j].name = "CableSegment" + j;
					if (j > 0)
					{
						CableSegments[j].GetComponent<SpringJoint>().connectedBody = CableSegments[j - 1].GetComponent<Rigidbody>();
					}
				}
				DestinyPoint.GetComponent<SpringJoint>().connectedBody = CableSegments[CableSegments.Count - 1].GetComponent<Rigidbody>();
			}
			else
			{
				DestinyPoint.GetComponent<SpringJoint>().connectedBody = InitialPoint.GetComponent<Rigidbody>();
			}
			OrderCableHierarchy();
			SetupLineRenderer();
		}

		public void OrderCableHierarchy()
		{
			InitialPoint.transform.SetSiblingIndex(0);
			for (int i = 0; i < CableSegments.Count; i++)
			{
				CableSegments[i].transform.SetSiblingIndex(i + 1);
			}
			DestinyPoint.transform.SetSiblingIndex(CableSegments.Count + 1);
		}

		public void CutCable(GameObject CableSegment)
		{
			if (CableSegments.Count < 2 || !CableSegments.Contains(CableSegment) || CableSegment == InitialPoint || CableSegment == DestinyPoint)
			{
				return;
			}
			List<GameObject> list = new List<GameObject>();
			List<GameObject> list2 = new List<GameObject>();
			list.Clear();
			list2.Clear();
			bool flag = false;
			foreach (GameObject cableSegment in CableSegments)
			{
				if (CableSegment == cableSegment)
				{
					flag = true;
				}
				if (flag)
				{
					if (cableSegment != CableSegment)
					{
						list2.Add(cableSegment);
					}
				}
				else
				{
					list.Add(cableSegment);
				}
			}
			list2.Reverse();
			CableSegments = list;
			GameObject gameObject = new GameObject("CableCutted");
			gameObject.transform.position = CableSegment.transform.position;
			gameObject.transform.rotation = CableSegment.transform.rotation;
			foreach (GameObject item in list2)
			{
				item.transform.SetParent(gameObject.transform);
			}
			GameObject gameObject2 = Object.Instantiate(CableSegment, base.transform);
			gameObject2.name = "DestinyPointOrigin";
			GameObject destinyPoint = DestinyPoint;
			destinyPoint.name = "InitialPointCutted";
			destinyPoint.transform.SetParent(gameObject.transform);
			destinyPoint.transform.SetAsFirstSibling();
			DestinyPoint = gameObject2;
			CableSegment.transform.SetParent(gameObject.transform);
			if (list2.Count > 0)
			{
				CableSegment.GetComponent<SpringJoint>().connectedBody = list2[list2.Count - 1].GetComponent<Rigidbody>();
				for (int num = list2.Count - 1; num >= 0; num--)
				{
					if (num <= 0)
					{
						list2[num].GetComponent<SpringJoint>().connectedBody = destinyPoint.GetComponent<Rigidbody>();
					}
					else
					{
						list2[num].GetComponent<SpringJoint>().connectedBody = list2[num - 1].GetComponent<Rigidbody>();
					}
				}
			}
			else
			{
				CableSegment.GetComponent<SpringJoint>().connectedBody = destinyPoint.GetComponent<Rigidbody>();
			}
			Cable cable = gameObject.AddComponent<Cable>();
			cable.Segments = 0;
			cable.InitialPoint = destinyPoint;
			cable.DestinyPoint = CableSegment;
			cable.CableSegments = list2;
			cable.CableMaterial = CableMaterial;
			cable.UseGravity = UseGravity;
			cable.smoothSteps = smoothSteps;
		}
	}
}
