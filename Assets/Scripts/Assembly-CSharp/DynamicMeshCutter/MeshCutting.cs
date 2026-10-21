using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DynamicMeshCutter
{
	public class MeshCutting
	{
		private class InternalData
		{
			public VirtualMesh MeshTarget;

			public VirtualPlane Plane;

			public DynamicMesh[] Sides = new DynamicMesh[2];

			public List<Vector3> AddedVertices = new List<Vector3>();

			public List<BoneWeight> AddedBoneweights = new List<BoneWeight>();

			public readonly Vector3[,] Vertices = new Vector3[3, 2];

			public readonly Vector3[,] Normals = new Vector3[3, 2];

			public readonly Vector2[,] UVs = new Vector2[3, 2];

			public readonly BoneWeight[,] BoneWeights = new BoneWeight[3, 2];

			public readonly int[,] RagdollParts = new int[3, 2];

			public readonly bool[] DidSet = new bool[2];

			private readonly Vector3[] triangleVertices = new Vector3[3];

			private readonly Vector3[] triangleNormals = new Vector3[3];

			private readonly Vector2[] triangleUVs = new Vector2[3];

			private readonly BoneWeight[] triangleBoneWeights = new BoneWeight[3];

			private readonly int[] triangleRagdollParts = new int[3];

			public InternalData(VirtualMesh meshTarget, VirtualPlane plane)
			{
				MeshTarget = meshTarget;
				Plane = plane;
				for (int i = 0; i < 2; i++)
				{
					Sides[i] = new DynamicMesh();
					Sides[i].SetTargetMesh(meshTarget);
					if (MeshTarget.DynamicRagdoll != null)
					{
						Sides[i].DynamicRagdoll = MeshTarget.DynamicRagdoll;
					}
				}
			}

			public void AddCutTriangle(int side, int submesh, int aSide, int aVertex, int bSide, int bVertex, int cSide, int cVertex, Vector3 faceNormal)
			{
				CopyVertex(0, aSide, aVertex);
				CopyVertex(1, bSide, bVertex);
				CopyVertex(2, cSide, cVertex);
				Sides[side].AddTriangle(triangleVertices, triangleNormals, triangleUVs, triangleBoneWeights, triangleRagdollParts, faceNormal, submesh);
			}

			private void CopyVertex(int destination, int side, int vertex)
			{
				triangleVertices[destination] = Vertices[side, vertex];
				triangleNormals[destination] = Normals[side, vertex];
				triangleUVs[destination] = UVs[side, vertex];
				triangleBoneWeights[destination] = BoneWeights[side, vertex];
				triangleRagdollParts[destination] = RagdollParts[side, vertex];
			}
		}

		public VirtualMesh[] Cut(ref Info info)
		{
			VirtualPlane plane = info.Plane;
			bool separateMeshes = info.MeshTarget.SeparateMeshes;
			VirtualMesh targetVirtualMesh = info.TargetVirtualMesh;
			if (targetVirtualMesh.UVs.Length == 0)
			{
				targetVirtualMesh.FillUVs();
			}
			InternalData internalData = new InternalData(targetVirtualMesh, plane);
			int subMeshCount = info.TargetVirtualMesh.SubMeshCount;
			int[] array = new int[3];
			int[] array2 = new int[3];
			for (int i = 0; i < subMeshCount; i++)
			{
				int[] indices = targetVirtualMesh.GetIndices(i);
				for (int j = 0; j < 2; j++)
				{
					internalData.Sides[j].SubIndices.Add(new List<int>());
				}
				for (int k = 0; k < indices.Length; k += 3)
				{
					array2[0] = indices[k];
					array2[1] = indices[k + 1];
					array2[2] = indices[k + 2];
					array[0] = plane.GetSide(internalData.MeshTarget.Vertices[array2[0]]);
					array[1] = plane.GetSide(internalData.MeshTarget.Vertices[array2[1]]);
					array[2] = plane.GetSide(internalData.MeshTarget.Vertices[array2[2]]);
					if (array[0] == array[1] && array[0] == array[2])
					{
						internalData.Sides[array[0]].AddTriangle(array2, i);
					}
					else
					{
						CutTriangle(i, array, array2, internalData);
					}
				}
			}
			if (internalData.Sides[0].Vertices.Count == 0 || internalData.Sides[1].Vertices.Count == 0)
			{
				return null;
			}
			if (info.MeshTarget.CreateCutFaces)
			{
				for (int l = 0; l < 2; l++)
				{
					internalData.Sides[l].SubIndices.Add(new List<int>());
				}
				CreateFaces(internalData, ref info);
			}
			int[] array3 = new int[2];
			VirtualMesh[][] array4 = new VirtualMesh[2][];
			for (int m = 0; m < 2; m++)
			{
				array4[m] = MeshSeperation.GetVirtualMeshes(internalData.Sides[m], separateMeshes);
				array3[m] = array4.Length;
			}
			VirtualMesh[] array5 = array4[0].Concat(array4[1]).ToArray();
			info.Sides = new int[array5.Length];
			info.BT = new int[array5.Length];
			int num = ((info.Plane.WorldNormal.y < 0f) ? 1 : 0);
			for (int n = 0; n < array5.Length; n++)
			{
				info.Sides[n] = ((n >= array4[0].Length) ? 1 : 0);
				info.BT[n] = ((info.Sides[n] == num) ? 1 : 0);
			}
			return array5;
		}

		private void CutTriangle(int submesh, int[] sides, int[] triangle, InternalData data)
		{
			Vector3[,] vertices = data.Vertices;
			Vector3[,] normals = data.Normals;
			Vector2[,] uVs = data.UVs;
			BoneWeight[,] boneWeights = data.BoneWeights;
			int[,] ragdollParts = data.RagdollParts;
			bool[] didSet = data.DidSet;
			didSet[0] = (didSet[1] = false);
			bool hasBoneWeight = data.MeshTarget.HasBoneWeight;
			bool flag = data.MeshTarget.Assignments != null;
			for (int i = 0; i < 3; i++)
			{
				int num = triangle[i];
				int num2 = sides[i];
				if (!didSet[num2])
				{
					didSet[num2] = true;
					for (int j = 0; j < 2; j++)
					{
						vertices[num2, j] = data.MeshTarget.Vertices[num];
						normals[num2, j] = data.MeshTarget.Normals[num];
						uVs[num2, j] = data.MeshTarget.UVs[num];
						if (hasBoneWeight)
						{
							boneWeights[num2, j] = data.MeshTarget.BoneWeights[num];
						}
						if (flag)
						{
							ragdollParts[num2, j] = data.MeshTarget.Assignments[num];
						}
					}
				}
				else
				{
					vertices[num2, 1] = data.MeshTarget.Vertices[num];
					normals[num2, 1] = data.MeshTarget.Normals[num];
					uVs[num2, 1] = data.MeshTarget.UVs[num];
					if (hasBoneWeight)
					{
						boneWeights[num2, 1] = data.MeshTarget.BoneWeights[num];
					}
					if (flag)
					{
						ragdollParts[num2, 1] = data.MeshTarget.Assignments[num];
					}
				}
			}
			float distance = 0f;
			float num3 = 0f;
			int num4 = -1;
			if (flag)
			{
				for (int k = 0; k < 2; k++)
				{
					if (ragdollParts[0, k] == ragdollParts[1, k])
					{
						num4 = ragdollParts[0, k];
						break;
					}
				}
			}
			for (int l = 0; l < 2; l++)
			{
				data.Plane.Raycast(new Ray(vertices[0, l], (vertices[1, l] - vertices[0, l]).normalized), out distance);
				num3 = distance / (vertices[1, l] - vertices[0, l]).magnitude;
				vertices[2, l] = Vector3.Lerp(vertices[0, l], vertices[1, l], num3);
				uVs[2, l] = Vector2.Lerp(uVs[0, l], uVs[1, l], num3);
				normals[2, l] = Vector3.Lerp(normals[0, l], normals[1, l], num3);
				boneWeights[2, l] = default;
				ragdollParts[2, l] = -1;
				if (hasBoneWeight)
				{
					boneWeights[2, l].boneIndex0 = boneWeights[0, l].boneIndex0;
					boneWeights[2, l].boneIndex1 = boneWeights[0, l].boneIndex1;
					boneWeights[2, l].boneIndex2 = boneWeights[0, l].boneIndex2;
					boneWeights[2, l].boneIndex3 = boneWeights[0, l].boneIndex3;
					boneWeights[2, l].weight0 = Mathf.Lerp(boneWeights[0, l].weight0, boneWeights[1, l].weight0, num3);
					boneWeights[2, l].weight1 = Mathf.Lerp(boneWeights[0, l].weight1, boneWeights[1, l].weight1, num3);
					boneWeights[2, l].weight2 = Mathf.Lerp(boneWeights[0, l].weight2, boneWeights[1, l].weight2, num3);
					boneWeights[2, l].weight3 = Mathf.Lerp(boneWeights[0, l].weight3, boneWeights[1, l].weight3, num3);
					data.AddedBoneweights.Add(boneWeights[2, l]);
				}
				if (flag)
				{
					ragdollParts[2, l] = num4;
				}
				data.AddedVertices.Add(vertices[2, l]);
			}
			int num5 = 0;
			if (vertices[1, 0] == vertices[1, 1])
			{
				num5 = 1;
			}
			for (int m = 0; m < 2; m++)
			{
				data.AddCutTriangle(m, submesh, m, 0, 2, 0, 2, 1, normals[2, 0]);
				if (num5 != m)
				{
					data.AddCutTriangle(m, submesh, m, 0, m, 1, 2, 1, normals[2, 1]);
				}
			}
		}

		private void CreateFaces(InternalData data, ref Info info)
		{
			bool hasBoneWeight = data.MeshTarget.HasBoneWeight;
			List<Vector3> list = new List<Vector3>();
			int count = data.AddedVertices.Count;
			try
			{
				for (int i = 0; i < count; i++)
				{
					if (list.Contains(data.AddedVertices[i]))
					{
						continue;
					}
					List<Vector3> list2 = new List<Vector3>();
					List<BoneWeight> list3 = new List<BoneWeight>();
					new List<int>();
					for (int j = 0; j < 2; j++)
					{
						int index = (i + j) % count;
						list.Add(data.AddedVertices[index]);
						list2.Add(data.AddedVertices[index]);
						if (hasBoneWeight)
						{
							list3.Add(data.AddedBoneweights[index]);
						}
					}
					bool flag = false;
					while (!flag)
					{
						flag = true;
						for (int k = 0; k < data.AddedVertices.Count; k += 2)
						{
							if (data.AddedVertices[k] == list2[list2.Count - 1] && !list.Contains(data.AddedVertices[k + 1]))
							{
								flag = false;
								list.Add(data.AddedVertices[k + 1]);
								list2.Add(data.AddedVertices[k + 1]);
								if (hasBoneWeight)
								{
									list3.Add(data.AddedBoneweights[k + 1]);
								}
							}
							else if (data.AddedVertices[k + 1] == list2[list2.Count - 1] && !list.Contains(data.AddedVertices[k]))
							{
								flag = false;
								list.Add(data.AddedVertices[k]);
								list2.Add(data.AddedVertices[k]);
								if (hasBoneWeight)
								{
									list3.Add(data.AddedBoneweights[k]);
								}
							}
						}
					}
					FillFace(data, list2, list3, ref info);
				}
			}
			catch (ArgumentOutOfRangeException message)
			{
				Debug.LogError(message);
			}
		}

		private void FillFace(InternalData data, List<Vector3> fVertices, List<BoneWeight> fBoneWeights, ref Info info)
		{
			bool hasBoneWeight = data.MeshTarget.HasBoneWeight;
			Vector3 verticeCenter = GetVerticeCenter(fVertices);
			BoneWeight boneWeight = (hasBoneWeight ? GetBoneweightCenter(fBoneWeights) : default(BoneWeight));
			info.LocalFaceCenters.Add(verticeCenter);
			Vector3 zero = Vector3.zero;
			zero.x = data.Plane.LocalNormal.y;
			zero.y = 0f - data.Plane.LocalNormal.x;
			zero.z = data.Plane.LocalNormal.z;
			Vector3 rhs = Vector3.Cross(data.Plane.LocalNormal, zero);
			Vector3[] array = new Vector3[2];
			for (int i = 0; i < fVertices.Count; i++)
			{
				Vector3 lhs = fVertices[i] - verticeCenter;
				int index = (i + 1) % fVertices.Count;
				for (int j = 0; j < 2; j++)
				{
					array[j] = Vector3.zero;
					array[j].x = 0.5f + Vector3.Dot(lhs, rhs);
					array[j].y = 0.5f + Vector3.Dot(lhs, zero);
					array[j].z = 0.5f + Vector3.Dot(lhs, data.Plane.LocalNormal);
					lhs = fVertices[index] - verticeCenter;
				}
				int num = -1;
				for (int k = 0; k < 2; k++)
				{
					if (k == 1)
					{
						num = 1;
					}
					BoneWeight[] array2 = new BoneWeight[3];
					if (!hasBoneWeight)
					{
						for (int l = 0; l < 3; l++)
						{
							array2[l] = default;
						}
					}
					else
					{
						array2[0] = fBoneWeights[i];
						array2[1] = fBoneWeights[index];
						array2[2] = boneWeight;
					}
					data.Sides[k].AddTriangle(new Vector3[3]
					{
						fVertices[i],
						fVertices[index],
						verticeCenter
					}, new Vector3[3]
					{
						num * data.Plane.LocalNormal,
						num * data.Plane.LocalNormal,
						num * data.Plane.LocalNormal
					}, new Vector2[3]
					{
						array[0],
						array[1],
						new Vector2(0.5f, 0.5f)
					}, array2, new int[3] { -1, -1, -1 }, num * data.Plane.LocalNormal, data.Sides[k].SubIndices.Count - 1);
				}
			}
		}

		public Vector3 GetVerticeCenter(List<Vector3> fVertices)
		{
			Vector3 vector = default;
			int count = fVertices.Count;
			for (int i = 0; i < count; i++)
			{
				vector += fVertices[i];
			}
			return vector / count;
		}

		public BoneWeight GetBoneweightCenter(List<BoneWeight> fBoneWeights)
		{
			BoneWeight result = default;
			Dictionary<int, float> dictionary = new Dictionary<int, float>();
			for (int i = 0; i < fBoneWeights.Count; i++)
			{
				BoneWeight boneWeight = fBoneWeights[i];
				dictionary[boneWeight.boneIndex0] = (dictionary.ContainsKey(boneWeight.boneIndex0) ? (dictionary[boneWeight.boneIndex0] + boneWeight.weight0) : boneWeight.weight0);
				dictionary[boneWeight.boneIndex1] = (dictionary.ContainsKey(boneWeight.boneIndex1) ? (dictionary[boneWeight.boneIndex1] + boneWeight.weight1) : boneWeight.weight1);
				dictionary[boneWeight.boneIndex2] = (dictionary.ContainsKey(boneWeight.boneIndex2) ? (dictionary[boneWeight.boneIndex2] + boneWeight.weight2) : boneWeight.weight2);
				dictionary[boneWeight.boneIndex3] = (dictionary.ContainsKey(boneWeight.boneIndex3) ? (dictionary[boneWeight.boneIndex3] + boneWeight.weight3) : boneWeight.weight3);
			}
			KeyValuePair<int, float>[] array = dictionary.OrderByDescending((KeyValuePair<int, float> b) => b.Value).ToArray();
			float num = 0f;
			int num2 = ((array.Length >= 4) ? 4 : array.Length);
			for (int num3 = 0; num3 < num2; num3++)
			{
				num += array[num3].Value;
			}
			if (num2 > 0)
			{
				result.boneIndex0 = array[0].Key;
				result.weight0 = array[0].Value / num;
			}
			if (num2 > 1)
			{
				result.boneIndex1 = array[1].Key;
				result.weight1 = array[1].Value / num;
			}
			if (num2 > 2)
			{
				result.boneIndex2 = array[2].Key;
				result.weight2 = array[2].Value / num;
			}
			if (num2 > 3)
			{
				result.boneIndex3 = array[3].Key;
				result.weight3 = array[3].Value / num;
			}
			return result;
		}
	}
}
