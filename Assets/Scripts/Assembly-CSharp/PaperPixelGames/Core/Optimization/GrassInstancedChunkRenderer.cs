using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PaperPixelGames.Core.Optimization
{
	[DisallowMultipleComponent]
	public sealed class GrassInstancedChunkRenderer : MonoBehaviour
	{
		[Serializable]
		public sealed class GrassChunk
		{
			[SerializeField]
			private Vector3 _center;

			[SerializeField]
			private Vector3 _size;

			[SerializeField]
			private Matrix4x4[] _matrices;

			public Vector3 Center => _center;

			public Vector3 Size => _size;

			public Matrix4x4[] Matrices => _matrices;

			public GrassChunk(Vector3 center, Vector3 size, Matrix4x4[] matrices)
			{
				_center = center;
				_size = size;
				_matrices = matrices ?? Array.Empty<Matrix4x4>();
			}
		}

		private readonly struct Batch
		{
			public readonly int ChunkIndex;

			public readonly Matrix4x4[] Matrices;

			public Batch(int chunkIndex, Matrix4x4[] matrices)
			{
				ChunkIndex = chunkIndex;
				Matrices = matrices;
			}
		}

		private const int MaxInstancesPerDraw = 1023;

		[SerializeField]
		private Mesh _nearMesh;

		[SerializeField]
		private Mesh _farMesh;

		[SerializeField]
		private Material _material;

		[SerializeField]
		[Min(1f)]
		private float _nearDistance = 45f;

		[SerializeField]
		[Min(1f)]
		private float _farDistance = 85f;

		[SerializeField]
		[Min(1f)]
		private float _cullDistance = 100f;

		[SerializeField]
		[Range(0.02f, 1f)]
		private float _lodRefreshInterval = 0.2f;

		[SerializeField]
		private List<GrassChunk> _chunks = new List<GrassChunk>();

		[SerializeField]
		[HideInInspector]
		private GameObject[] _sourceObjects;

		private readonly List<Batch> _batches = new List<Batch>();

		private readonly List<Batch> _nearBatches = new List<Batch>();

		private readonly List<Batch> _farBatches = new List<Batch>();

		private readonly List<Camera> _cameras = new List<Camera>();

		private int[] _chunkLods;

		private float _nextLodRefreshTime;

		private bool _batchesDirty = true;

		public int ChunkCount => _chunks?.Count ?? 0;

		public int SourceObjectCount
		{
			get
			{
				GameObject[] sourceObjects = _sourceObjects;
				if (sourceObjects == null)
				{
					return 0;
				}
				return sourceObjects.Length;
			}
		}

		public int MatrixCount
		{
			get
			{
				if (_chunks == null)
				{
					return 0;
				}
				int num = 0;
				for (int i = 0; i < _chunks.Count; i++)
				{
					num += (_chunks[i]?.Matrices?.Length).GetValueOrDefault();
				}
				return num;
			}
		}

		private void OnEnable()
		{
			_batchesDirty = true;
			_nextLodRefreshTime = 0f;
		}

		private void OnValidate()
		{
			_nearDistance = Mathf.Max(1f, _nearDistance);
			_farDistance = Mathf.Max(_nearDistance + 1f, _farDistance);
			_cullDistance = Mathf.Max(_farDistance + 1f, _cullDistance);
			_lodRefreshInterval = Mathf.Clamp(_lodRefreshInterval, 0.02f, 1f);
			_batchesDirty = true;
		}

		private void Update()
		{
			if (_material == null || _nearMesh == null || _chunks == null || _chunks.Count == 0)
			{
				return;
			}
			if (_batchesDirty || _chunkLods == null || _chunkLods.Length != _chunks.Count)
			{
				RebuildBatches();
			}
			Camera main = Camera.main;
			if (!(main == null))
			{
				if (Time.unscaledTime >= _nextLodRefreshTime)
				{
					_nextLodRefreshTime = Time.unscaledTime + _lodRefreshInterval;
					RefreshChunkLods(main);
				}
				DrawForCamera(main);
			}
		}

		private void OnDrawGizmosSelected()
		{
			if (_chunks == null)
			{
				return;
			}
			Gizmos.color = new Color(0.3f, 0.9f, 0.35f, 0.18f);
			for (int i = 0; i < _chunks.Count; i++)
			{
				GrassChunk grassChunk = _chunks[i];
				if (grassChunk != null)
				{
					Gizmos.DrawWireCube(grassChunk.Center, grassChunk.Size);
				}
			}
		}

		private void RebuildBatches()
		{
			_batches.Clear();
			_nearBatches.Clear();
			_farBatches.Clear();
			if (_chunks == null)
			{
				_chunkLods = Array.Empty<int>();
				_batchesDirty = false;
				return;
			}
			_chunkLods = new int[_chunks.Count];
			for (int i = 0; i < _chunkLods.Length; i++)
			{
				_chunkLods[i] = -1;
			}
			for (int j = 0; j < _chunks.Count; j++)
			{
				Matrix4x4[] array = _chunks[j]?.Matrices;
				if (array != null && array.Length != 0)
				{
					for (int k = 0; k < array.Length; k += 1023)
					{
						int num = Mathf.Min(1023, array.Length - k);
						Matrix4x4[] array2 = new Matrix4x4[num];
						Array.Copy(array, k, array2, 0, num);
						_batches.Add(new Batch(j, array2));
					}
				}
			}
			_batchesDirty = false;
		}

		private void RefreshChunkLods(Camera camera)
		{
			if (_chunkLods == null || _chunkLods.Length != _chunks.Count)
			{
				_chunkLods = new int[_chunks.Count];
			}
			Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
			Vector3 position = camera.transform.position;
			float num = _nearDistance * _nearDistance;
			float num2 = _farDistance * _farDistance;
			float num3 = _cullDistance * _cullDistance;
			for (int i = 0; i < _chunks.Count; i++)
			{
				GrassChunk grassChunk = _chunks[i];
				if (grassChunk == null)
				{
					_chunkLods[i] = -1;
					continue;
				}
				Bounds bounds = new Bounds(grassChunk.Center, grassChunk.Size);
				if (!GeometryUtility.TestPlanesAABB(planes, bounds))
				{
					_chunkLods[i] = -1;
					continue;
				}
				float num4 = bounds.SqrDistance(position);
				if (num4 > num3)
				{
					_chunkLods[i] = -1;
				}
				else if (num4 <= num || _farMesh == null)
				{
					_chunkLods[i] = 0;
				}
				else if (num4 <= num2)
				{
					_chunkLods[i] = 1;
				}
				else
				{
					_chunkLods[i] = -1;
				}
			}
			_nearBatches.Clear();
			_farBatches.Clear();
			for (int j = 0; j < _batches.Count; j++)
			{
				Batch item = _batches[j];
				switch ((item.ChunkIndex >= 0 && item.ChunkIndex < _chunkLods.Length) ? _chunkLods[item.ChunkIndex] : (-1))
				{
				case 0:
					_nearBatches.Add(item);
					break;
				case 1:
					_farBatches.Add(item);
					break;
				}
			}
		}

		private void DrawForCamera(Camera camera)
		{
			DrawBatches(_nearMesh, _nearBatches, camera);
			if (_farMesh != null)
			{
				DrawBatches(_farMesh, _farBatches, camera);
			}
		}

		private void DrawBatches(Mesh mesh, List<Batch> batches, Camera camera)
		{
			if (!(mesh == null) && batches != null)
			{
				for (int i = 0; i < batches.Count; i++)
				{
					Matrix4x4[] matrices = batches[i].Matrices;
					Graphics.DrawMeshInstanced(mesh, 0, _material, matrices, matrices.Length, null, ShadowCastingMode.Off, receiveShadows: false, base.gameObject.layer, camera, LightProbeUsage.Off, null);
				}
			}
		}

		public void Configure(Mesh nearMesh, Mesh farMesh, Material material, float nearDistance, float farDistance, float cullDistance, float lodRefreshInterval, List<GrassChunk> chunks, GameObject[] sourceObjects)
		{
			_nearMesh = nearMesh;
			_farMesh = farMesh;
			_material = material;
			_nearDistance = Mathf.Max(1f, nearDistance);
			_farDistance = Mathf.Max(_nearDistance + 1f, farDistance);
			_cullDistance = Mathf.Max(_farDistance + 1f, cullDistance);
			_lodRefreshInterval = Mathf.Clamp(lodRefreshInterval, 0.02f, 1f);
			_chunks = chunks ?? new List<GrassChunk>();
			_sourceObjects = sourceObjects ?? Array.Empty<GameObject>();
			_batchesDirty = true;
		}

		public void UpdateDistanceSettingsForEditor(float nearDistance, float farDistance, float cullDistance, float lodRefreshInterval)
		{
			_nearDistance = Mathf.Max(1f, nearDistance);
			_farDistance = Mathf.Max(_nearDistance + 1f, farDistance);
			_cullDistance = Mathf.Max(_farDistance + 1f, cullDistance);
			_lodRefreshInterval = Mathf.Clamp(lodRefreshInterval, 0.02f, 1f);
			_nextLodRefreshTime = 0f;
		}

		public GameObject[] GetSourceObjectsForEditor()
		{
			return _sourceObjects ?? Array.Empty<GameObject>();
		}
	}
}
