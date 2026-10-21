using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PaperPixelGames.Core.Optimization
{
	[DisallowMultipleComponent]
	public abstract class UnderwaterPropChunkRendererBase : MonoBehaviour
	{
		[Serializable]
		public sealed class UnderwaterChunk
		{
			[SerializeField]
			private Vector3 _localCenter;

			[SerializeField]
			private Vector3 _localSize;

			[SerializeField]
			private List<DrawBatch> _batches = new List<DrawBatch>();

			public Vector3 LocalCenter => _localCenter;

			public Vector3 LocalSize => _localSize;

			public IReadOnlyList<DrawBatch> Batches => _batches;

			public int BatchCount => _batches?.Count ?? 0;

			public int MatrixCount
			{
				get
				{
					if (_batches == null)
					{
						return 0;
					}
					int num = 0;
					for (int i = 0; i < _batches.Count; i++)
					{
						num += _batches[i]?.LocalMatrixCount ?? 0;
					}
					return num;
				}
			}

			public UnderwaterChunk(Vector3 localCenter, Vector3 localSize, List<DrawBatch> batches)
			{
				_localCenter = localCenter;
				_localSize = localSize;
				_batches = batches ?? new List<DrawBatch>();
			}

			public void PrepareWorldMatrices(Matrix4x4 rootLocalToWorld)
			{
				if (_batches != null)
				{
					for (int i = 0; i < _batches.Count; i++)
					{
						_batches[i]?.PrepareWorldMatrices(rootLocalToWorld);
					}
				}
			}
		}

		[Serializable]
		public sealed class DrawBatch
		{
			[SerializeField]
			private Mesh _mesh;

			[SerializeField]
			private int _subMeshIndex;

			[SerializeField]
			private Material _material;

			[SerializeField]
			private int _layer;

			[SerializeField]
			private ShadowCastingMode _shadowCastingMode;

			[SerializeField]
			private bool _receiveShadows;

			[SerializeField]
			private Matrix4x4[] _localMatrices = Array.Empty<Matrix4x4>();

			[NonSerialized]
			private Matrix4x4[] _worldMatrices = Array.Empty<Matrix4x4>();

			public Mesh Mesh => _mesh;

			public int SubMeshIndex => _subMeshIndex;

			public Material Material => _material;

			public int Layer => _layer;

			public ShadowCastingMode ShadowCastingMode => _shadowCastingMode;

			public bool ReceiveShadows => _receiveShadows;

			public Matrix4x4[] WorldMatrices => _worldMatrices;

			public int LocalMatrixCount
			{
				get
				{
					Matrix4x4[] localMatrices = _localMatrices;
					if (localMatrices == null)
					{
						return 0;
					}
					return localMatrices.Length;
				}
			}

			public bool CanDraw
			{
				get
				{
					if (_mesh != null && _material != null && _worldMatrices != null && _worldMatrices.Length != 0)
					{
						return _worldMatrices.Length <= 1023;
					}
					return false;
				}
			}

			public DrawBatch(Mesh mesh, int subMeshIndex, Material material, int layer, ShadowCastingMode shadowCastingMode, bool receiveShadows, Matrix4x4[] localMatrices)
			{
				_mesh = mesh;
				_subMeshIndex = subMeshIndex;
				_material = material;
				_layer = layer;
				_shadowCastingMode = shadowCastingMode;
				_receiveShadows = receiveShadows;
				_localMatrices = localMatrices ?? Array.Empty<Matrix4x4>();
				_worldMatrices = Array.Empty<Matrix4x4>();
			}

			public void PrepareWorldMatrices(Matrix4x4 rootLocalToWorld)
			{
				if (_localMatrices == null || _localMatrices.Length == 0)
				{
					_worldMatrices = Array.Empty<Matrix4x4>();
					return;
				}
				if (_worldMatrices == null || _worldMatrices.Length != _localMatrices.Length)
				{
					_worldMatrices = new Matrix4x4[_localMatrices.Length];
				}
				for (int i = 0; i < _localMatrices.Length; i++)
				{
					_worldMatrices[i] = rootLocalToWorld * _localMatrices[i];
				}
			}
		}

		[Serializable]
		public struct SourceState
		{
			[SerializeField]
			private GameObject _gameObject;

			[SerializeField]
			private bool _wasActiveSelf;

			public GameObject GameObject => _gameObject;

			public bool WasActiveSelf => _wasActiveSelf;

			public SourceState(GameObject gameObject, bool wasActiveSelf)
			{
				_gameObject = gameObject;
				_wasActiveSelf = wasActiveSelf;
			}
		}

		private const int MaxInstancesPerDraw = 1023;

		[SerializeField]
		[Min(1f)]
		private float _cullDistance = 90f;

		[SerializeField]
		[Range(0.02f, 1f)]
		private float _cullRefreshInterval = 0.2f;

		[SerializeField]
		private List<UnderwaterChunk> _chunks = new List<UnderwaterChunk>();

		[SerializeField]
		[HideInInspector]
		private SourceState[] _sourceStates = Array.Empty<SourceState>();

		private bool[] _visibleChunks = Array.Empty<bool>();

		private float _nextCullRefreshTime;

		private Matrix4x4 _cachedLocalToWorld;

		private bool _worldMatricesDirty = true;

		private int _visibleChunkCount;

		private int _lastDrawCallCount;

		public int ChunkCount => _chunks?.Count ?? 0;

		public int SourceCount
		{
			get
			{
				SourceState[] sourceStates = _sourceStates;
				if (sourceStates == null)
				{
					return 0;
				}
				return sourceStates.Length;
			}
		}

		public int VisibleChunkCount => _visibleChunkCount;

		public int LastDrawCallCount => _lastDrawCallCount;

		public int BatchCount
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
					num += _chunks[i]?.BatchCount ?? 0;
				}
				return num;
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
					num += _chunks[i]?.MatrixCount ?? 0;
				}
				return num;
			}
		}

		private void OnEnable()
		{
			_nextCullRefreshTime = 0f;
			_worldMatricesDirty = true;
			EnsureRuntimeState();
		}

		private void OnDisable()
		{
			_visibleChunkCount = 0;
			_lastDrawCallCount = 0;
			if (_visibleChunks != null)
			{
				Array.Clear(_visibleChunks, 0, _visibleChunks.Length);
			}
		}

		private void OnValidate()
		{
			_cullDistance = Mathf.Max(1f, _cullDistance);
			_cullRefreshInterval = Mathf.Clamp(_cullRefreshInterval, 0.02f, 1f);
			_worldMatricesDirty = true;
			EnsureRuntimeState();
		}

		private void Update()
		{
			if (_chunks == null || _chunks.Count == 0)
			{
				return;
			}
			Camera main = Camera.main;
			if (!(main == null))
			{
				EnsureRuntimeState();
				RefreshWorldMatricesIfNeeded();
				if (Time.unscaledTime >= _nextCullRefreshTime)
				{
					_nextCullRefreshTime = Time.unscaledTime + _cullRefreshInterval;
					RefreshVisibleChunks(main);
				}
				DrawVisibleChunks(main);
			}
		}

		private void OnDrawGizmosSelected()
		{
			if (_chunks == null)
			{
				return;
			}
			Gizmos.color = new Color(0.1f, 0.65f, 1f, 0.25f);
			Matrix4x4 matrix = Gizmos.matrix;
			Gizmos.matrix = base.transform.localToWorldMatrix;
			for (int i = 0; i < _chunks.Count; i++)
			{
				UnderwaterChunk underwaterChunk = _chunks[i];
				if (underwaterChunk != null)
				{
					Gizmos.DrawWireCube(underwaterChunk.LocalCenter, underwaterChunk.LocalSize);
				}
			}
			Gizmos.matrix = matrix;
		}

		private void EnsureRuntimeState()
		{
			int num = _chunks?.Count ?? 0;
			if (_visibleChunks == null || _visibleChunks.Length != num)
			{
				_visibleChunks = new bool[num];
				_nextCullRefreshTime = 0f;
			}
		}

		private void RefreshWorldMatricesIfNeeded()
		{
			Matrix4x4 localToWorldMatrix = base.transform.localToWorldMatrix;
			if (_worldMatricesDirty || !(localToWorldMatrix == _cachedLocalToWorld))
			{
				_cachedLocalToWorld = localToWorldMatrix;
				_worldMatricesDirty = false;
				for (int i = 0; i < _chunks.Count; i++)
				{
					_chunks[i]?.PrepareWorldMatrices(localToWorldMatrix);
				}
			}
		}

		private void RefreshVisibleChunks(Camera camera)
		{
			Vector3 position = camera.transform.position;
			float num = _cullDistance * _cullDistance;
			_visibleChunkCount = 0;
			for (int i = 0; i < _chunks.Count; i++)
			{
				UnderwaterChunk underwaterChunk = _chunks[i];
				if (underwaterChunk == null)
				{
					_visibleChunks[i] = false;
					continue;
				}
				bool flag = TransformBounds(_cachedLocalToWorld, new Bounds(underwaterChunk.LocalCenter, underwaterChunk.LocalSize)).SqrDistance(position) <= num;
				_visibleChunks[i] = flag;
				if (flag)
				{
					_visibleChunkCount++;
				}
			}
		}

		private void DrawVisibleChunks(Camera camera)
		{
			_lastDrawCallCount = 0;
			for (int i = 0; i < _chunks.Count; i++)
			{
				if (!_visibleChunks[i])
				{
					continue;
				}
				UnderwaterChunk underwaterChunk = _chunks[i];
				if (underwaterChunk == null)
				{
					continue;
				}
				IReadOnlyList<DrawBatch> batches = underwaterChunk.Batches;
				for (int j = 0; j < batches.Count; j++)
				{
					DrawBatch drawBatch = batches[j];
					if (drawBatch != null && drawBatch.CanDraw)
					{
						Graphics.DrawMeshInstanced(drawBatch.Mesh, drawBatch.SubMeshIndex, drawBatch.Material, drawBatch.WorldMatrices, drawBatch.WorldMatrices.Length, null, drawBatch.ShadowCastingMode, drawBatch.ReceiveShadows, drawBatch.Layer, camera, LightProbeUsage.Off, null);
						_lastDrawCallCount++;
					}
				}
			}
		}

		private static Bounds TransformBounds(Matrix4x4 matrix, Bounds localBounds)
		{
			Vector3 center = matrix.MultiplyPoint3x4(localBounds.center);
			Vector3 extents = localBounds.extents;
			Vector3 vector = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
			Vector3 vector2 = matrix.MultiplyVector(new Vector3(0f, extents.y, 0f));
			Vector3 vector3 = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));
			extents = new Vector3(Mathf.Abs(vector.x) + Mathf.Abs(vector2.x) + Mathf.Abs(vector3.x), Mathf.Abs(vector.y) + Mathf.Abs(vector2.y) + Mathf.Abs(vector3.y), Mathf.Abs(vector.z) + Mathf.Abs(vector2.z) + Mathf.Abs(vector3.z));
			return new Bounds(center, extents * 2f);
		}

		public void ConfigureForEditor(float cullDistance, float cullRefreshInterval, List<UnderwaterChunk> chunks, SourceState[] sourceStates)
		{
			_cullDistance = Mathf.Max(1f, cullDistance);
			_cullRefreshInterval = Mathf.Clamp(cullRefreshInterval, 0.02f, 1f);
			_chunks = chunks ?? new List<UnderwaterChunk>();
			_sourceStates = sourceStates ?? Array.Empty<SourceState>();
			_worldMatricesDirty = true;
			EnsureRuntimeState();
		}

		public void UpdateSettingsForEditor(float cullDistance, float cullRefreshInterval)
		{
			_cullDistance = Mathf.Max(1f, cullDistance);
			_cullRefreshInterval = Mathf.Clamp(cullRefreshInterval, 0.02f, 1f);
			_nextCullRefreshTime = 0f;
		}

		public SourceState[] GetSourceStatesForEditor()
		{
			return _sourceStates ?? Array.Empty<SourceState>();
		}
	}
}
