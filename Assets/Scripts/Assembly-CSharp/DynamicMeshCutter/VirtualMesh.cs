using System.Collections.Generic;
using UnityEngine;

namespace DynamicMeshCutter
{
	public class VirtualMesh
	{
		public Mesh Mesh;

		public int[] Triangles;

		public Vector3[] Vertices;

		public Vector3[] Normals;

		public Vector2[] UVs;

		public BoneWeight[] BoneWeights;

		public DynamicRagdoll DynamicRagdoll;

		public int[] Assignments;

		public Dictionary<int, Vector3[]> DynamicGroups;

		public Dictionary<int, Bounds> AdjustedBounds;

		private int _subMeshCount;

		private int[][] _subMeshIndices;

		public int UniqueVerticesCount = -1;

		private bool _hasBoneWeight;

		public int SubMeshCount => _subMeshCount;

		public bool HasBoneWeight => _hasBoneWeight;

		public VirtualMesh()
		{
		}

		public VirtualMesh(Mesh mesh)
		{
			Mesh = mesh;
			Vertices = mesh.vertices;
			Normals = mesh.normals;
			UVs = mesh.uv;
			BoneWeights = mesh.boneWeights;
			if (BoneWeights.Length != 0)
			{
				_hasBoneWeight = true;
			}
			_subMeshCount = mesh.subMeshCount;
			_subMeshIndices = new int[_subMeshCount][];
			for (int i = 0; i < mesh.subMeshCount; i++)
			{
				_subMeshIndices[i] = mesh.GetIndices(i);
			}
		}

		public int[] GetIndices(int index)
		{
			return _subMeshIndices[index];
		}

		public void SetIndices(int[][] indices)
		{
			_subMeshIndices = indices;
			_subMeshCount = indices.Length;
		}

		public void FillUVs()
		{
			int num = Vertices.Length;
			UVs = new Vector2[num];
			for (int i = 0; i < Vertices.Length; i++)
			{
				UVs[i] = Vector2.zero;
			}
		}

		public void AssignRagdoll(DynamicRagdoll dynamicRagdoll)
		{
			DynamicRagdoll = dynamicRagdoll;
			Assignments = dynamicRagdoll.Assignments;
		}

		public void SetupRagdoll()
		{
		}
	}
}
