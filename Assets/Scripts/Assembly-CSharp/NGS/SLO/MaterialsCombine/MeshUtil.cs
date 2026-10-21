using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NGS.SLO.MaterialsCombine
{
	public static class MeshUtil
	{
		private struct MeshAdjustOperation : IEquatable<MeshAdjustOperation>
		{
			public Mesh mesh;

			public MaterialCombineInstance instance;

			public Rect rect;

			public MeshAdjustOperation(Mesh mesh, MaterialCombineInstance instance, Rect rect)
			{
				this.mesh = mesh;
				this.instance = instance;
				this.rect = rect;
			}

			public override bool Equals(object obj)
			{
				if (obj is MeshAdjustOperation other)
				{
					return Equals(other);
				}
				return false;
			}

			public bool Equals(MeshAdjustOperation other)
			{
				if (mesh != other.mesh)
				{
					return false;
				}
				if (instance.MaterialIndex != other.instance.MaterialIndex)
				{
					return false;
				}
				if (Mathf.Abs(rect.x - other.rect.x) > 0.001f)
				{
					return false;
				}
				if (Mathf.Abs(rect.y - other.rect.y) > 0.001f)
				{
					return false;
				}
				if (Mathf.Abs(rect.width - other.rect.width) > 0.001f)
				{
					return false;
				}
				if (Mathf.Abs(rect.height - other.rect.height) > 0.001f)
				{
					return false;
				}
				if (Mathf.Abs(instance.Data.MainTextureOffset.x - other.instance.Data.MainTextureOffset.x) > 0.001f)
				{
					return false;
				}
				if (Mathf.Abs(instance.Data.MainTextureOffset.y - other.instance.Data.MainTextureOffset.y) > 0.001f)
				{
					return false;
				}
				if (Mathf.Abs(instance.Data.MainTextureScale.x - other.instance.Data.MainTextureScale.x) > 0.001f)
				{
					return false;
				}
				if (Mathf.Abs(instance.Data.MainTextureScale.y - other.instance.Data.MainTextureScale.y) > 0.001f)
				{
					return false;
				}
				return true;
			}

			public override int GetHashCode()
			{
				return ((((((((((((((((((mesh ? mesh.GetInstanceID() : 0) * 397) ^ Quantize(rect.x)) * 397) ^ Quantize(rect.y)) * 397) ^ Quantize(rect.width)) * 397) ^ Quantize(rect.height)) * 397) ^ Quantize(instance.Data.MainTextureOffset.x)) * 397) ^ Quantize(instance.Data.MainTextureOffset.y)) * 397) ^ Quantize(instance.Data.MainTextureScale.x)) * 397) ^ Quantize(instance.Data.MainTextureScale.y)) * 397) ^ instance.MaterialIndex;
			}

			private int Quantize(float v)
			{
				return Mathf.RoundToInt(v / 0.001f);
			}
		}

		private const int MAX_CACHED_MESHES_COUNT = 30;

		private static Dictionary<MeshAdjustOperation, Mesh> _adjustedMeshesCache;

		public static Mesh GetUVAdjustedMesh(Mesh mesh, MaterialCombineInstance instance, Rect rect)
		{
			if (_adjustedMeshesCache == null)
			{
				_adjustedMeshesCache = new Dictionary<MeshAdjustOperation, Mesh>();
			}
			MeshAdjustOperation key = new MeshAdjustOperation(mesh, instance, rect);
			if (_adjustedMeshesCache.TryGetValue(key, out var value))
			{
				if (!(value == null))
				{
					return value;
				}
				_adjustedMeshesCache.Remove(key);
			}
			value = UnityEngine.Object.Instantiate(mesh);
			AdjustUV(value, instance, rect);
			_adjustedMeshesCache.Add(key, value);
			if (_adjustedMeshesCache.Count > 30)
			{
				ClearCache();
			}
			return value;
		}

		public static void AdjustUV(Mesh mesh, MaterialCombineInstance instance, Rect rect)
		{
			int[] array = mesh.GetIndices(instance.MaterialIndex).Distinct().ToArray();
			Vector2[] uv = mesh.uv;
			int[] array2 = array;
			foreach (int num in array2)
			{
				Vector2 vector = uv[num];
				vector.x -= Mathf.Floor(instance.Data.UVOffset.x);
				vector.y -= Mathf.Floor(instance.Data.UVOffset.y);
				vector.Scale(instance.Data.MainTextureScale);
				vector += instance.Data.MainTextureOffset;
				vector.x /= instance.Data.MainTextureRepeats.x;
				vector.y /= instance.Data.MainTextureRepeats.y;
				vector.x = rect.x + rect.width * vector.x;
				vector.y = rect.y + rect.height * vector.y;
				uv[num] = vector;
			}
			mesh.uv = uv;
		}

		public static void ClearCache()
		{
			_adjustedMeshesCache?.Clear();
		}
	}
}
