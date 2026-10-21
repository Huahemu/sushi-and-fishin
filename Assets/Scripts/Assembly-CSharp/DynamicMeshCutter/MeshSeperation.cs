using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace DynamicMeshCutter
{
	public static class MeshSeperation
	{
		public static void FloodFillClusters(DynamicMesh mesh)
		{
			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();
			int[][] array = new int[mesh.SubIndices.Count][];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = mesh.SubIndices[i].ToArray();
			}
			Vector3[] array2 = mesh.Vertices.ToArray();
			Dictionary<Vector3, VertexInfo> uniqueVertices = new Dictionary<Vector3, VertexInfo>();
			for (int j = 0; j < array.Length; j++)
			{
				int[] array3 = array[j];
				foreach (int num in array3)
				{
					Vector3 key = array2[num];
					if (!uniqueVertices.ContainsKey(key))
					{
						uniqueVertices.Add(key, new VertexInfo(num, j));
						continue;
					}
					uniqueVertices[key].Occasions_Vertex.Add(num);
					uniqueVertices[key].Occasions_Submesh.Add(j);
				}
			}
			Vector3 item = default;
			Vector3 item2 = default;
			foreach (KeyValuePair<Vector3, VertexInfo> item3 in uniqueVertices)
			{
				List<int> occasions_Vertex = item3.Value.Occasions_Vertex;
				List<Vector3> neighbors = item3.Value.Neighbors;
				for (int l = 0; l < occasions_Vertex.Count; l++)
				{
					int num2 = occasions_Vertex[l];
					switch (num2 % 3)
					{
					case 0:
						item = array2[num2 + 1];
						item2 = array2[num2 + 2];
						break;
					case 1:
						item = array2[num2 - 1];
						item2 = array2[num2 + 1];
						break;
					case 2:
						item = array2[num2 - 2];
						item2 = array2[num2 - 1];
						break;
					}
					if (!neighbors.Contains(item))
					{
						neighbors.Add(item);
					}
					if (!neighbors.Contains(item2))
					{
						neighbors.Add(item2);
					}
				}
			}
			int[] array4 = new int[array2.Length];
			for (int m = 0; m < array4.Length; m++)
			{
				array4[m] = -1;
			}
			int[] array5 = new int[array2.Length];
			for (int n = 0; n < array5.Length; n++)
			{
				array5[n] = -1;
			}
			List<Vector3> list = uniqueVertices.Keys.ToList();
			List<Vector3> visited = new List<Vector3>();
			List<Vector3> gray = new List<Vector3>();
			int num3 = 0;
			int num4 = 0;
			while (list.Count > 0)
			{
				gray.Add(list[0]);
				while (gray.Count > 0)
				{
					Vector3 vector = gray[0];
					num4++;
					gray.RemoveAt(0);
					list.Remove(vector);
					visited.Add(vector);
					PopulateGrey(vector);
					VertexInfo vertexInfo = uniqueVertices[vector];
					List<int> occasions_Vertex2 = vertexInfo.Occasions_Vertex;
					List<int> occasions_Submesh = vertexInfo.Occasions_Submesh;
					int count = occasions_Vertex2.Count;
					for (int num5 = 0; num5 < count; num5++)
					{
						array4[occasions_Vertex2[num5]] = num3;
						array5[occasions_Vertex2[num5]] = occasions_Submesh[num5];
					}
				}
				num3++;
				if (list.Count > 0)
				{
					visited.Clear();
					gray.Clear();
				}
			}
			stopwatch.Stop();
			mesh.AmountOfClusters = num3;
			mesh.AmountOfUniqueVertices = visited.Count;
			mesh.Cluster = array4;
			mesh.SubMesh = array5;
			void PopulateGrey(Vector3 vertex)
			{
				foreach (Vector3 neighbor in uniqueVertices[vertex].Neighbors)
				{
					if (!visited.Contains(neighbor) && !gray.Contains(neighbor))
					{
						gray.Add(neighbor);
					}
				}
			}
		}

		public static VirtualMesh[] GetVirtualMeshes(DynamicMesh dynamicMesh, bool seperateByClusters)
		{
			if (!seperateByClusters)
			{
				return new VirtualMesh[1] { ConstructVirtualMeshFromDynamic(dynamicMesh) };
			}
			FloodFillClusters(dynamicMesh);
			int amountOfClusters = dynamicMesh.AmountOfClusters;
			int count = dynamicMesh.SubIndices.Count;
			DynamicMesh[] array = new DynamicMesh[amountOfClusters];
			VirtualMesh[] array2 = new VirtualMesh[amountOfClusters];
			for (int i = 0; i < amountOfClusters; i++)
			{
				array[i] = new DynamicMesh();
				for (int j = 0; j < count; j++)
				{
					array[i].SubIndices.Add(new List<int>());
				}
				if (dynamicMesh.DynamicRagdoll != null)
				{
					array[i].DynamicRagdoll = dynamicMesh.DynamicRagdoll;
				}
			}
			List<int> triangles = dynamicMesh.Triangles;
			int[] array3 = new int[3];
			for (int k = 0; k < triangles.Count; k += 3)
			{
				array3[0] = triangles[k];
				array3[1] = triangles[k + 1];
				array3[2] = triangles[k + 2];
				int num = dynamicMesh.Cluster[array3[0]];
				int sub = dynamicMesh.SubMesh[array3[0]];
				try
				{
					array[num].AddTriangle(array3, sub, dynamicMesh);
				}
				catch (IndexOutOfRangeException message)
				{
					UnityEngine.Debug.LogError(message);
				}
			}
			for (int l = 0; l < amountOfClusters; l++)
			{
				array2[l] = ConstructVirtualMeshFromDynamic(array[l]);
			}
			return array2;
		}

		private static VirtualMesh ConstructVirtualMeshFromDynamic(DynamicMesh dynamicMesh)
		{
			VirtualMesh virtualMesh = new VirtualMesh
			{
				Vertices = dynamicMesh.Vertices.ToArray(),
				Triangles = dynamicMesh.Triangles.ToArray(),
				Normals = dynamicMesh.Normals.ToArray(),
				UVs = dynamicMesh.UVs.ToArray(),
				BoneWeights = dynamicMesh.BoneWeights.ToArray(),
				UniqueVerticesCount = dynamicMesh.AmountOfUniqueVertices
			};
			if (dynamicMesh.DynamicRagdoll != null)
			{
				virtualMesh.DynamicRagdoll = dynamicMesh.DynamicRagdoll;
				virtualMesh.Assignments = dynamicMesh.RD.ToArray();
				virtualMesh.DynamicGroups = dynamicMesh.ColliderGroups.ToDictionary((KeyValuePair<int, List<Vector3>> entry) => entry.Key, (KeyValuePair<int, List<Vector3>> entry) => entry.Value.ToArray());
			}
			int[][] array = new int[dynamicMesh.SubIndices.Count][];
			for (int num = 0; num < dynamicMesh.SubIndices.Count; num++)
			{
				array[num] = dynamicMesh.SubIndices[num].ToArray();
			}
			virtualMesh.SetIndices(array);
			return virtualMesh;
		}
	}
}
