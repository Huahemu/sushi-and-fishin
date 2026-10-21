using System.Collections.Generic;
using UnityEngine;

namespace DynamicMeshCutter
{
	public class DynamicMesh
	{
		public List<int> Triangles = new List<int>();

		public List<Vector3> Vertices = new List<Vector3>();

		public List<Vector3> Normals = new List<Vector3>();

		public List<Vector2> UVs = new List<Vector2>();

		public List<BoneWeight> BoneWeights = new List<BoneWeight>();

		public List<List<int>> SubIndices = new List<List<int>>();

		public int AmountOfClusters;

		public int AmountOfUniqueVertices = -1;

		public int[] Cluster;

		public int[] SubMesh;

		public DynamicRagdoll DynamicRagdoll;

		public List<int> RD = new List<int>();

		public Dictionary<int, List<Vector3>> ColliderGroups = new Dictionary<int, List<Vector3>>();

		private VirtualMesh _targetMesh;

		public void SetTargetMesh(VirtualMesh targetMesh)
		{
			_targetMesh = targetMesh;
		}

		public void AddTriangle(int[] triangle, int sub, DynamicMesh parent)
		{
			int count = Vertices.Count;
			for (int i = 0; i < 3; i++)
			{
				SubIndices[sub].Add(count + i);
				Triangles.Add(count + i);
				int index = triangle[i];
				Vertices.Add(parent.Vertices[index]);
				Normals.Add(parent.Normals[index]);
				UVs.Add(parent.UVs[index]);
				if (parent._targetMesh.HasBoneWeight)
				{
					BoneWeights.Add(parent.BoneWeights[index]);
				}
				if (!(parent.DynamicRagdoll != null))
				{
					continue;
				}
				int num = parent.RD[index];
				RD.Add(num);
				if (num > -1)
				{
					if (!ColliderGroups.ContainsKey(num))
					{
						ColliderGroups.Add(num, new List<Vector3>());
					}
					ColliderGroups[num].Add(parent.Vertices[index]);
				}
			}
		}

		public void AddTriangle(int[] triangle, int sub)
		{
			int count = Vertices.Count;
			for (int i = 0; i < 3; i++)
			{
				SubIndices[sub].Add(count + i);
				Triangles.Add(count + i);
				int num = triangle[i];
				Vertices.Add(_targetMesh.Vertices[num]);
				Normals.Add(_targetMesh.Normals[num]);
				UVs.Add(_targetMesh.UVs[num]);
				if (_targetMesh.HasBoneWeight)
				{
					BoneWeights.Add(_targetMesh.BoneWeights[num]);
				}
				if (!(_targetMesh.DynamicRagdoll != null))
				{
					continue;
				}
				int num2 = _targetMesh.Assignments[num];
				RD.Add(num2);
				if (num2 > -1)
				{
					if (!ColliderGroups.ContainsKey(num2))
					{
						ColliderGroups.Add(num2, new List<Vector3>());
					}
					ColliderGroups[num2].Add(_targetMesh.Vertices[num]);
				}
			}
		}

		public void AddTriangle(Vector3[] vertices, Vector3[] normals, Vector2[] uvs, BoneWeight[] boneWeights, int[] rd, Vector3 faceNormal, int submesh)
		{
			int count = Vertices.Count;
			bool flag = Vector3.Dot(Vector3.Cross((vertices[1] - vertices[0]).normalized, (vertices[2] - vertices[0]).normalized), faceNormal) >= 0f;
			for (int i = 0; i < 3; i++)
			{
				SubIndices[submesh].Add(count + i);
				Triangles.Add(count + i);
				int num = (flag ? i : (2 - i));
				Vertices.Add(vertices[num]);
				Normals.Add(normals[num]);
				UVs.Add(uvs[num]);
				if (_targetMesh.HasBoneWeight)
				{
					BoneWeights.Add(boneWeights[num]);
				}
				if (_targetMesh.Assignments == null)
				{
					continue;
				}
				int num2 = rd[num];
				RD.Add(num2);
				if (num2 > -1)
				{
					if (!ColliderGroups.ContainsKey(num2))
					{
						ColliderGroups.Add(num2, new List<Vector3>());
					}
					ColliderGroups[num2].Add(_targetMesh.Vertices[num]);
				}
			}
		}
	}
}
