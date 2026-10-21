using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace PaperPixelGames.Core.Kitchen.GastronormPanIngredients
{
	public sealed class BakedPanIngredientGraphics : PanIngredientGraphics
	{
		private readonly struct CacheKey : IEquatable<CacheKey>
		{
			private readonly string _layout;

			private readonly int _count;

			public CacheKey(string layout, int count)
			{
				_layout = layout;
				_count = count;
			}

			public bool Equals(CacheKey other)
			{
				if (_count == other._count)
				{
					return string.Equals(_layout, other._layout, StringComparison.Ordinal);
				}
				return false;
			}

			public override bool Equals(object obj)
			{
				if (obj is CacheKey other)
				{
					return Equals(other);
				}
				return false;
			}

			public override int GetHashCode()
			{
				return HashCode.Combine(_layout, _count);
			}
		}

		private struct CacheEntry
		{
			public Mesh Mesh;

			public Material[] Materials;

			public int ReferenceCount;

			public long Bytes;

			public LinkedListNode<CacheKey> IdleNode;
		}

		[SerializeField]
		private string _layoutKey;

		[SerializeField]
		private Mesh[] _sourceMeshes = Array.Empty<Mesh>();

		[SerializeField]
		private Matrix4x4[] _sourceTransforms = Array.Empty<Matrix4x4>();

		[SerializeField]
		private int[] _subMeshSlots = Array.Empty<int>();

		[SerializeField]
		private Material[] _subMeshMaterials = Array.Empty<Material>();

		[SerializeField]
		private MeshFilter _combinedMeshFilter;

		[SerializeField]
		private MeshRenderer _combinedMeshRenderer;

		private static readonly Dictionary<CacheKey, CacheEntry> MeshCache = new Dictionary<CacheKey, CacheEntry>();

		private static readonly LinkedList<CacheKey> IdleMeshes = new LinkedList<CacheKey>();

		private const int MaxIdleMeshes = 64;

		private const long MaxIdleMeshBytes = 33554432L;

		private static long idleMeshBytes;

		private static readonly ProfilerMarker BuildMarker = new ProfilerMarker("Kitchen.Pan.BuildMesh");

		private CacheKey _activeCacheKey;

		private bool _hasActiveCacheEntry;

		private int _lastVisibleCount = -1;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetRuntimeCache()
		{
			foreach (CacheEntry value in MeshCache.Values)
			{
				DestroyRuntimeMesh(value.Mesh);
			}
			MeshCache.Clear();
			IdleMeshes.Clear();
			idleMeshBytes = 0L;
			SceneManager.sceneUnloaded -= OnSceneUnloaded;
			SceneManager.sceneUnloaded += OnSceneUnloaded;
		}

		private static void OnSceneUnloaded(Scene _)
		{
			TrimIdleMeshes(all: true);
		}

		public override void Initialize()
		{
			ReleaseActiveMesh();
			base.Initialize();
			_lastVisibleCount = -1;
			ApplyCombinedVisual(0);
		}

		public override void SyncToCount(int target)
		{
			int num = Mathf.Clamp(target, 0, base.IngredientObjects.Count);
			base.SyncToCount(num);
			ApplyCombinedVisual(num);
		}

		public override void ResetAll()
		{
			base.ResetAll();
			ApplyCombinedVisual(0);
		}

		private void OnDestroy()
		{
			ReleaseActiveMesh();
		}

		private void ApplyCombinedVisual(int visibleCount)
		{
			if (_lastVisibleCount == visibleCount)
			{
				return;
			}
			ReleaseActiveMesh();
			_lastVisibleCount = visibleCount;
			if (_combinedMeshFilter == null || _combinedMeshRenderer == null || visibleCount <= 0)
			{
				if (_combinedMeshFilter != null)
				{
					_combinedMeshFilter.sharedMesh = null;
				}
				if (_combinedMeshRenderer != null)
				{
					_combinedMeshRenderer.enabled = false;
				}
				return;
			}
			if (_sourceMeshes.Length == 0 || _sourceTransforms.Length != _sourceMeshes.Length || _subMeshSlots.Length != _sourceMeshes.Length || _subMeshMaterials.Length != _sourceMeshes.Length)
			{
				Debug.LogError("[BakedPanIngredientGraphics] Invalid baked layout on " + base.name + ".", this);
				_combinedMeshFilter.sharedMesh = null;
				_combinedMeshRenderer.enabled = false;
				return;
			}
			CacheKey cacheKey = new CacheKey(_layoutKey, visibleCount);
			if (!MeshCache.TryGetValue(cacheKey, out var value))
			{
				value = BuildCacheEntry(visibleCount);
				if (value.Mesh == null)
				{
					_combinedMeshFilter.sharedMesh = null;
					_combinedMeshRenderer.enabled = false;
					return;
				}
				MeshCache.Add(cacheKey, value);
			}
			if (value.IdleNode != null)
			{
				IdleMeshes.Remove(value.IdleNode);
				idleMeshBytes -= value.Bytes;
				value.IdleNode = null;
			}
			value.ReferenceCount++;
			MeshCache[cacheKey] = value;
			_activeCacheKey = cacheKey;
			_hasActiveCacheEntry = true;
			_combinedMeshFilter.sharedMesh = value.Mesh;
			_combinedMeshRenderer.sharedMaterials = value.Materials;
			_combinedMeshRenderer.enabled = true;
		}

		private CacheEntry BuildCacheEntry(int visibleCount)
		{
			using (BuildMarker.Auto())
			{
				List<Material> list = new List<Material>();
				Dictionary<Material, List<CombineInstance>> dictionary = new Dictionary<Material, List<CombineInstance>>();
				int num = 0;
				for (int i = 0; i < _sourceMeshes.Length; i++)
				{
					if (_subMeshSlots[i] >= visibleCount)
					{
						continue;
					}
					Mesh mesh = _sourceMeshes[i];
					Material material = _subMeshMaterials[i];
					if (!(mesh == null) && !(material == null))
					{
						num += mesh.vertexCount;
						if (!dictionary.TryGetValue(material, out var value))
						{
							value = new List<CombineInstance>();
							dictionary.Add(material, value);
							list.Add(material);
						}
						value.Add(new CombineInstance
						{
							mesh = mesh,
							subMeshIndex = 0,
							transform = _sourceTransforms[i]
						});
					}
				}
				if (list.Count == 0)
				{
					return default;
				}
				List<Mesh> list2 = new List<Mesh>(list.Count);
				try
				{
					foreach (Material item in list)
					{
						Mesh mesh2 = new Mesh
						{
							name = base.name + "_MaterialGroup",
							indexFormat = ((num > 65535) ? IndexFormat.UInt32 : IndexFormat.UInt16)
						};
						mesh2.CombineMeshes(dictionary[item].ToArray(), mergeSubMeshes: true, useMatrices: true, hasLightmapData: false);
						list2.Add(mesh2);
					}
					CombineInstance[] array = new CombineInstance[list2.Count];
					for (int j = 0; j < list2.Count; j++)
					{
						array[j] = new CombineInstance
						{
							mesh = list2[j],
							subMeshIndex = 0,
							transform = Matrix4x4.identity
						};
					}
					Mesh mesh3 = new Mesh
					{
						name = $"{base.name}_Count_{visibleCount}",
						indexFormat = ((num > 65535) ? IndexFormat.UInt32 : IndexFormat.UInt16)
					};
					mesh3.CombineMeshes(array, mergeSubMeshes: false, useMatrices: false, hasLightmapData: false);
					mesh3.RecalculateBounds();
					mesh3.UploadMeshData(markNoLongerReadable: true);
					return new CacheEntry
					{
						Mesh = mesh3,
						Materials = list.ToArray(),
						Bytes = Profiler.GetRuntimeMemorySizeLong(mesh3),
						ReferenceCount = 0
					};
				}
				catch (Exception arg)
				{
					Debug.LogError($"[BakedPanIngredientGraphics] Could not build count {visibleCount} for {base.name}.\n{arg}", this);
					return default;
				}
				finally
				{
					foreach (Mesh item2 in list2)
					{
						DestroyRuntimeMesh(item2);
					}
				}
			}
		}

		private void ReleaseActiveMesh()
		{
			if (!_hasActiveCacheEntry)
			{
				return;
			}
			if (MeshCache.TryGetValue(_activeCacheKey, out var value))
			{
				value.ReferenceCount--;
				if (value.ReferenceCount <= 0)
				{
					if (Application.isPlaying)
					{
						value.IdleNode = IdleMeshes.AddLast(_activeCacheKey);
						idleMeshBytes += value.Bytes;
						MeshCache[_activeCacheKey] = value;
						TrimIdleMeshes(all: false);
					}
					else
					{
						MeshCache.Remove(_activeCacheKey);
						DestroyRuntimeMesh(value.Mesh);
					}
				}
				else
				{
					MeshCache[_activeCacheKey] = value;
				}
			}
			_hasActiveCacheEntry = false;
		}

		private static void TrimIdleMeshes(bool all)
		{
			while (IdleMeshes.Count > 0 && (all || IdleMeshes.Count > 64 || idleMeshBytes > 33554432))
			{
				CacheKey value = IdleMeshes.First.Value;
				IdleMeshes.RemoveFirst();
				CacheEntry cacheEntry = MeshCache[value];
				idleMeshBytes -= cacheEntry.Bytes;
				MeshCache.Remove(value);
				DestroyRuntimeMesh(cacheEntry.Mesh);
			}
		}

		private static void DestroyRuntimeMesh(Mesh mesh)
		{
			if (!(mesh == null))
			{
				if (Application.isPlaying)
				{
					UnityEngine.Object.Destroy(mesh);
				}
				else
				{
					UnityEngine.Object.DestroyImmediate(mesh);
				}
			}
		}
	}
}
