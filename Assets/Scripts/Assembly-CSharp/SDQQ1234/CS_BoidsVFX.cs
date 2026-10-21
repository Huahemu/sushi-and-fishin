using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.VFX;

namespace SDQQ1234
{
	public class CS_BoidsVFX : MonoBehaviour
	{
		[Serializable]
		private struct BoidData
		{
			public Vector3 velocity;

			public Vector3 position;

			public float scale;
		}

		private struct SphereColliderData
		{
			public Vector3 position;

			public float radius;
		}

		private struct BoxColliderData
		{
			public Vector3 center;

			public float pad1;

			public Vector3 size;

			public float pad2;

			public Vector4 rotation;
		}

		private struct CapsuleColliderData
		{
			public Vector3 start;

			public float radius;

			public Vector3 end;

			public float pad;
		}

		public enum FishShape
		{
			Box = 0,
			Target = 1
		}

		public FishShape shape;

		public VisualEffect boidsVFX;

		public float bornRadius = 5f;

		private GraphicsBuffer BoidDataGraphicsPositionBuffer;

		private GraphicsBuffer BoidDataGraphicsVelocityBuffer;

		private GraphicsBuffer BoidDataGraphicsScaleBuffer;

		public ComputeShader boidsComputeShader;

		[Range(0.1f, 0.5f)]
		[SerializeField]
		private float _obstacleUpdateInterval = 0.15f;

		[Tooltip("FishCount")]
		[Range(16f, 256f)]
		[SerializeField]
		private int boidsCount = 1000;

		[Header("Boid Size")]
		[SerializeField]
		private float minScale = 0.5f;

		[SerializeField]
		private float maxScale = 1.5f;

		[Header("CAS Radius")]
		[SerializeField]
		private float cohesionRadius = 5f;

		[SerializeField]
		private float alignmentRadius = 3f;

		[SerializeField]
		private float separationRadius = 2f;

		[Header("CAS Forces")]
		[SerializeField]
		private float cohesionWeight = 1f;

		[SerializeField]
		private float alignmentWeight = 1f;

		[SerializeField]
		private float separationWeight = 2f;

		[Header("Boid")]
		[SerializeField]
		private float boidMaximumSpeed = 20f;

		[SerializeField]
		private float boidMaxSteeringForce = 10f;

		[Header("Simulation")]
		[SerializeField]
		private Vector3 simulationCenter = Vector3.zero;

		[SerializeField]
		private Vector3 simulationDimensions = new Vector3(32f, 32f, 32f);

		[SerializeField]
		private float simulationBoundsAvoidWeight = 10f;

		[Header("Obstacle Settings")]
		[SerializeField]
		private float obstacleAvoidWeight = 20f;

		[SerializeField]
		private LayerMask obstacleLayerMask = -1;

		[Header("FollowTarget")]
		[SerializeField]
		private Transform targetPos;

		private readonly List<SphereColliderData> _sphereData = new List<SphereColliderData>();

		private readonly List<BoxColliderData> _boxData = new List<BoxColliderData>();

		private readonly List<CapsuleColliderData> _capsuleData = new List<CapsuleColliderData>();

		private float _nextObstacleUpdateTime;

		private ComputeBuffer _boidsSteeringForcesBuffer;

		private ComputeBuffer _boidsDataBuffer;

		private ComputeBuffer _sphereObstacleBuffer;

		private ComputeBuffer _boxObstacleBuffer;

		private ComputeBuffer _capsuleObstacleBuffer;

		private uint _storedThreadGroupSize;

		private int _dispatchedThreadGroupSize;

		private int _steeringForcesKernelId;

		private int _boidsDataKernelId;

		private bool _isSimulating;

		private bool _isInitialized;

		private float _revealProgress;

		private void Start()
		{
			InitKernels();
			InitBuffers();
			BindVFXBuffers();
			ApplyVFXSettings();
			boidsVFX.Reinit();
			boidsVFX.enabled = _isSimulating;
			_isInitialized = true;
		}

		private void BindVFXBuffers()
		{
			if (!(boidsVFX == null))
			{
				boidsVFX.SetGraphicsBuffer("PositionBuffer", BoidDataGraphicsPositionBuffer);
				boidsVFX.SetGraphicsBuffer("VelocityBuffer", BoidDataGraphicsVelocityBuffer);
				boidsVFX.SetGraphicsBuffer("ScaleBuffer", BoidDataGraphicsScaleBuffer);
			}
		}

		private void ApplyVFXSettings()
		{
			if (!(boidsVFX == null))
			{
				boidsVFX.SetInt("ParticleCount", boidsCount);
				boidsVFX.SetVector3("BoundsCenter", simulationCenter);
				boidsVFX.SetVector3("BoundsSize", simulationDimensions);
			}
		}

		private void InitBuffers()
		{
			boidsVFX.Reinit();
			_boidsDataBuffer = new ComputeBuffer(boidsCount, 28);
			_boidsSteeringForcesBuffer = new ComputeBuffer(boidsCount, 12);
			BoidDataGraphicsPositionBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, boidsCount, Marshal.SizeOf(typeof(Vector3)));
			BoidDataGraphicsVelocityBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, boidsCount, Marshal.SizeOf(typeof(Vector3)));
			BoidDataGraphicsScaleBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, boidsCount, 4);
			Vector3[] array = new Vector3[boidsCount];
			BoidData[] array2 = new BoidData[boidsCount];
			Vector3[] array3 = new Vector3[boidsCount];
			Vector3[] array4 = new Vector3[boidsCount];
			float[] array5 = new float[boidsCount];
			for (int i = 0; i < boidsCount; i++)
			{
				array[i] = Vector3.zero;
				float scale = UnityEngine.Random.Range(minScale, maxScale);
				array5[i] = 0f;
				switch (shape)
				{
				case FishShape.Box:
					array3[i] = UnityEngine.Random.insideUnitSphere * bornRadius - simulationDimensions.z * 0.5f * Vector3.forward;
					array4[i] = Vector3.forward;
					break;
				case FishShape.Target:
					array3[i] = UnityEngine.Random.insideUnitSphere * bornRadius;
					array4[i] = (targetPos ? targetPos.localPosition : Vector3.zero) - array3[i];
					break;
				}
				array2[i].position = array3[i];
				array2[i].velocity = array4[i];
				array2[i].scale = scale;
			}
			_boidsSteeringForcesBuffer.SetData(array);
			_boidsDataBuffer.SetData(array2);
			BoidDataGraphicsPositionBuffer.SetData(array3);
			BoidDataGraphicsVelocityBuffer.SetData(array4);
			BoidDataGraphicsScaleBuffer.SetData(array5);
		}

		private void InitKernels()
		{
			_steeringForcesKernelId = boidsComputeShader.FindKernel("SteeringForcesCS");
			_boidsDataKernelId = boidsComputeShader.FindKernel("BoidsDataCS");
			boidsComputeShader.GetKernelThreadGroupSizes(_steeringForcesKernelId, out _storedThreadGroupSize, out var _, out var _);
			_dispatchedThreadGroupSize = Mathf.CeilToInt((float)boidsCount / (float)_storedThreadGroupSize);
		}

		private void UpdateObstacleBuffers()
		{
			List<BoidsCollider> allObstacles = BoidsCollider.AllObstacles;
			_sphereData.Clear();
			_boxData.Clear();
			_capsuleData.Clear();
			foreach (BoidsCollider item in allObstacles)
			{
				if (item == null || item.col == null || !item.gameObject.activeInHierarchy || ((1 << item.gameObject.layer) & (int)obstacleLayerMask) == 0)
				{
					continue;
				}
				Transform transform = item.transform;
				switch (item.type)
				{
				case BoidsCollider.ColliderType.Sphere:
				{
					SphereCollider sphereCollider = (SphereCollider)item.col;
					float num4 = Mathf.Max(transform.lossyScale.x, Mathf.Max(transform.lossyScale.y, transform.lossyScale.z));
					_sphereData.Add(new SphereColliderData
					{
						position = transform.TransformPoint(sphereCollider.center) - base.transform.position,
						radius = sphereCollider.radius * num4
					});
					break;
				}
				case BoidsCollider.ColliderType.Box:
				{
					BoxCollider boxCollider = (BoxCollider)item.col;
					Quaternion rotation = transform.rotation;
					Vector3 center = transform.TransformPoint(boxCollider.center) - base.transform.position;
					Vector3 size = Vector3.Scale(boxCollider.size, transform.lossyScale);
					_boxData.Add(new BoxColliderData
					{
						center = center,
						size = size,
						rotation = new Vector4(rotation.x, rotation.y, rotation.z, rotation.w),
						pad1 = 0f,
						pad2 = 0f
					});
					break;
				}
				case BoidsCollider.ColliderType.Capsule:
				{
					CapsuleCollider capsuleCollider = (CapsuleCollider)item.col;
					float num = capsuleCollider.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
					Vector3 vector = Vector3.up;
					float num2 = transform.lossyScale.y;
					if (capsuleCollider.direction == 0)
					{
						vector = Vector3.right;
						num2 = transform.lossyScale.x;
						num = capsuleCollider.radius * Mathf.Max(transform.lossyScale.y, transform.lossyScale.z);
					}
					else if (capsuleCollider.direction == 2)
					{
						vector = Vector3.forward;
						num2 = transform.lossyScale.z;
						num = capsuleCollider.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
					}
					float num3 = Mathf.Max(0f, capsuleCollider.height * num2 - 2f * num);
					Vector3 vector2 = transform.TransformPoint(capsuleCollider.center) - base.transform.position;
					Vector3 vector3 = transform.rotation * vector;
					_capsuleData.Add(new CapsuleColliderData
					{
						start = vector2 - vector3 * num3 * 0.5f,
						end = vector2 + vector3 * num3 * 0.5f,
						radius = num,
						pad = 0f
					});
					break;
				}
				}
			}
			SetBufferData(ref _sphereObstacleBuffer, _sphereData);
			boidsComputeShader.SetBuffer(_boidsDataKernelId, "_SphereBuffer", _sphereObstacleBuffer);
			boidsComputeShader.SetInt("_SphereCount", _sphereData.Count);
			SetBufferData(ref _boxObstacleBuffer, _boxData);
			boidsComputeShader.SetBuffer(_boidsDataKernelId, "_BoxBuffer", _boxObstacleBuffer);
			boidsComputeShader.SetInt("_BoxCount", _boxData.Count);
			SetBufferData(ref _capsuleObstacleBuffer, _capsuleData);
			boidsComputeShader.SetBuffer(_boidsDataKernelId, "_CapsuleBuffer", _capsuleObstacleBuffer);
			boidsComputeShader.SetInt("_CapsuleCount", _capsuleData.Count);
		}

		private void SetBufferData<T>(ref ComputeBuffer buffer, List<T> data) where T : struct
		{
			int num = ((data.Count <= 0) ? 1 : data.Count);
			if (buffer == null || buffer.count != num)
			{
				if (buffer != null)
				{
					buffer.Release();
				}
				buffer = new ComputeBuffer(num, Marshal.SizeOf(typeof(T)));
			}
			if (data.Count > 0)
			{
				buffer.SetData(data);
			}
		}

		private void Simulation(int steeringForcesKernelId, int boidsDataKernelId)
		{
			if (!(boidsComputeShader == null) && _boidsDataBuffer != null)
			{
				if (Time.time >= _nextObstacleUpdateTime)
				{
					_nextObstacleUpdateTime = Time.time + _obstacleUpdateInterval;
					UpdateObstacleBuffers();
				}
				boidsComputeShader.SetInt("_BoidsCount", boidsCount);
				boidsComputeShader.SetInt("_Shape", (int)shape);
				boidsComputeShader.SetBuffer(steeringForcesKernelId, "_BoidsDataBuffer", _boidsDataBuffer);
				boidsComputeShader.SetBuffer(steeringForcesKernelId, "_BoidsSteeringForcesBufferRw", _boidsSteeringForcesBuffer);
				boidsComputeShader.SetBuffer(boidsDataKernelId, "_BoidsSteeringForcesBuffer", _boidsSteeringForcesBuffer);
				boidsComputeShader.SetBuffer(boidsDataKernelId, "_BoidsDataBufferRw", _boidsDataBuffer);
				boidsComputeShader.SetBuffer(boidsDataKernelId, "_PositionBufferRW", BoidDataGraphicsPositionBuffer);
				boidsComputeShader.SetBuffer(boidsDataKernelId, "_VelocityBufferRW", BoidDataGraphicsVelocityBuffer);
				boidsComputeShader.SetBuffer(boidsDataKernelId, "_ScaleBufferRW", BoidDataGraphicsScaleBuffer);
				boidsComputeShader.SetFloat("_CohesionRadius", cohesionRadius);
				boidsComputeShader.SetFloat("_AlignmentRadius", alignmentRadius);
				boidsComputeShader.SetFloat("_SeparationRadius", separationRadius);
				boidsComputeShader.SetFloat("_BoidMaximumSpeed", boidMaximumSpeed);
				boidsComputeShader.SetFloat("_BoidMaximumSteeringForce", boidMaxSteeringForce);
				boidsComputeShader.SetFloat("_SeparationWeight", separationWeight);
				boidsComputeShader.SetFloat("_CohesionWeight", cohesionWeight);
				boidsComputeShader.SetFloat("_AlignmentWeight", alignmentWeight);
				boidsComputeShader.SetFloat("_SimulationBoundsAvoidWeight", simulationBoundsAvoidWeight);
				boidsComputeShader.SetFloat("_ObstacleAvoidWeight", obstacleAvoidWeight);
				boidsComputeShader.SetVector("_SimulationCenter", simulationCenter);
				boidsComputeShader.SetVector("_SimulationDimensions", simulationDimensions);
				boidsComputeShader.SetFloat("_DeltaTime", Time.deltaTime);
				boidsComputeShader.SetFloat("_RevealProgress", _revealProgress);
				if ((bool)targetPos)
				{
					boidsComputeShader.SetVector("_targetPos", targetPos.position - base.transform.position);
				}
				boidsComputeShader.Dispatch(steeringForcesKernelId, _dispatchedThreadGroupSize, 1, 1);
				boidsComputeShader.Dispatch(boidsDataKernelId, _dispatchedThreadGroupSize, 1, 1);
			}
		}

		public void StartSimulation(float revealProgress = 1f)
		{
			bool isSimulating = _isSimulating;
			_isSimulating = true;
			_revealProgress = Mathf.Clamp01(revealProgress);
			if (_isInitialized && boidsVFX != null)
			{
				boidsVFX.enabled = true;
				ApplyVFXSettings();
				BindVFXBuffers();
				if (!isSimulating)
				{
					boidsVFX.Reinit();
				}
			}
		}

		public void SetRevealProgress(float progress)
		{
			_revealProgress = Mathf.Clamp01(progress);
		}

		public void StopSimulation()
		{
			_isSimulating = false;
			_revealProgress = 0f;
			if (boidsVFX != null)
			{
				boidsVFX.enabled = false;
			}
		}

		private void Update()
		{
			if (_isSimulating)
			{
				Simulation(_steeringForcesKernelId, _boidsDataKernelId);
			}
		}

		private void SafeReleaseBuffer(ref ComputeBuffer buffer)
		{
			if (buffer != null)
			{
				buffer.Release();
				buffer = null;
			}
		}

		private void ReleaseBuffer()
		{
			SafeReleaseBuffer(ref _boidsDataBuffer);
			SafeReleaseBuffer(ref _boidsSteeringForcesBuffer);
			SafeReleaseBuffer(ref _sphereObstacleBuffer);
			SafeReleaseBuffer(ref _boxObstacleBuffer);
			SafeReleaseBuffer(ref _capsuleObstacleBuffer);
			if (BoidDataGraphicsPositionBuffer != null)
			{
				BoidDataGraphicsPositionBuffer.Release();
				BoidDataGraphicsPositionBuffer = null;
			}
			if (BoidDataGraphicsVelocityBuffer != null)
			{
				BoidDataGraphicsVelocityBuffer.Release();
				BoidDataGraphicsVelocityBuffer = null;
			}
			if (BoidDataGraphicsScaleBuffer != null)
			{
				BoidDataGraphicsScaleBuffer.Release();
				BoidDataGraphicsScaleBuffer = null;
			}
		}

		private void OnDestroy()
		{
			ReleaseBuffer();
		}

		private void OnDrawGizmos()
		{
			switch (shape)
			{
			case FishShape.Box:
			{
				Vector3 vector = simulationCenter + base.transform.position;
				Vector3 vector2 = vector - new Vector3(0f, 0f, simulationDimensions.z * 0.5f);
				Vector3 to = vector + new Vector3(0f, 0f, simulationDimensions.z * 0.5f);
				Gizmos.color = Color.green;
				Gizmos.DrawWireSphere(vector2, bornRadius);
				Gizmos.DrawLine(vector2, to);
				Gizmos.color = Color.cyan;
				Gizmos.DrawWireCube(vector, simulationDimensions);
				break;
			}
			case FishShape.Target:
				if ((bool)targetPos)
				{
					Gizmos.color = Color.yellow;
					Gizmos.DrawSphere(targetPos.position, targetPos.localScale.magnitude);
				}
				Gizmos.color = Color.green;
				Gizmos.DrawWireSphere(base.transform.position, bornRadius);
				break;
			}
		}
	}
}
