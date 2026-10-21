using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace NGS.SLO.MeshesCombine
{
	public class SkinnedMeshCombiner : ISkinnedMeshCombiner
	{
		private const int MAX_UV_CHANNELS = 8;

		public MeshCombineResult Combine(IList<MeshCombineInstance> instances, out Transform[] bones)
		{
			try
			{
				if (instances == null || instances.Count == 0)
				{
					bones = null;
					return new MeshCombineResult(instances.ToArray(), "SkinnedMeshCombiner::instances is empty");
				}
				foreach (MeshCombineInstance instance in instances)
				{
					if (!instance.ReadyForCombine)
					{
						bones = null;
						return new MeshCombineResult(instances.ToArray(), "Not all instances ready for combine");
					}
				}
				int[] verticesRemap = null;
				int usedVertexCount = 0;
				int[] indices = CombineIndices(instances, out verticesRemap, out usedVertexCount);
				Vector3[] array = CombineVertices(instances, verticesRemap, usedVertexCount);
				Mesh mesh = new Mesh();
				mesh.name = CreateCombinedMeshName(instances);
				if (array.Length >= 65535)
				{
					mesh.indexFormat = IndexFormat.UInt32;
				}
				mesh.subMeshCount = 1;
				mesh.SetVertices(array);
				mesh.SetIndices(indices, MeshTopology.Triangles, 0);
				if (AnyHasNormals(instances))
				{
					mesh.SetNormals(CombineNormals(instances, verticesRemap, usedVertexCount));
				}
				if (AnyHasTangents(instances))
				{
					mesh.SetTangents(CombineTangents(instances, verticesRemap, usedVertexCount));
				}
				for (int i = 0; i < 8; i++)
				{
					if (AnyHasUV(instances, i))
					{
						mesh.SetUVs(i, CombineUVs(instances, i, verticesRemap, usedVertexCount));
					}
				}
				Matrix4x4[] bindposes = null;
				Dictionary<int, int> oldToNewBones = null;
				bones = CombineBones(instances, out bindposes, out oldToNewBones);
				mesh.boneWeights = CombineBoneWeights(instances, oldToNewBones, verticesRemap, usedVertexCount);
				mesh.bindposes = bindposes;
				return new MeshCombineResult(instances.ToArray(), mesh);
			}
			catch (Exception ex)
			{
				bones = null;
				return new MeshCombineResult(instances.ToArray(), ex.Message + " \n " + ex.StackTrace);
			}
		}

		private int[] CombineIndices(IList<MeshCombineInstance> instances, out int[] verticesRemap, out int usedVertexCount)
		{
			int num = instances.Sum((MeshCombineInstance i) => i.indicesCount);
			int count = instances.Sum((MeshCombineInstance i) => i.vertexCount);
			int[] array = new int[num];
			verticesRemap = Enumerable.Repeat(-1, count).ToArray();
			List<int> list = new List<int>();
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			foreach (MeshCombineInstance instance in instances)
			{
				instance.mesh.GetIndices(list, instance.submeshIndex);
				for (int num5 = 0; num5 < list.Count; num5++)
				{
					int num6 = list[num5] + num3;
					int num7 = verticesRemap[num6];
					if (num7 == -1)
					{
						num7 = num4;
						verticesRemap[num6] = num7;
						num4++;
					}
					array[num5 + num2] = num7;
				}
				num3 += instance.vertexCount;
				num2 += instance.indicesCount;
			}
			usedVertexCount = num4;
			return array;
		}

		private Vector3[] CombineVertices(IList<MeshCombineInstance> instances, int[] verticesRemap, int vertexCount)
		{
			Vector3[] array = new Vector3[vertexCount];
			List<Vector3> list = new List<Vector3>();
			int num = 0;
			foreach (MeshCombineInstance instance in instances)
			{
				Mesh mesh = instance.mesh;
				mesh.GetVertices(list);
				RemapVertexAttribute(list, array, verticesRemap, num);
				num += mesh.vertexCount;
			}
			return array;
		}

		private Vector3[] CombineNormals(IList<MeshCombineInstance> instances, int[] verticesRemap, int vertexCount)
		{
			Vector3[] array = new Vector3[vertexCount];
			List<Vector3> list = new List<Vector3>();
			int num = 0;
			foreach (MeshCombineInstance instance in instances)
			{
				Mesh mesh = instance.mesh;
				mesh.GetNormals(list);
				if (list.Count == 0)
				{
					FillListWithValues(list, new Vector3(0f, 1f, 0f), mesh.vertexCount);
				}
				RemapVertexAttribute(list, array, verticesRemap, num);
				num += mesh.vertexCount;
			}
			return array;
		}

		private Vector4[] CombineTangents(IList<MeshCombineInstance> instances, int[] verticesRemap, int vertexCount)
		{
			Vector4[] array = new Vector4[vertexCount];
			List<Vector4> list = new List<Vector4>();
			int num = 0;
			foreach (MeshCombineInstance instance in instances)
			{
				Mesh mesh = instance.mesh;
				mesh.GetTangents(list);
				if (list.Count == 0)
				{
					FillListWithValues(list, new Vector4(1f, 0f, 0f, 1f), mesh.vertexCount);
				}
				RemapVertexAttribute(list, array, verticesRemap, num);
				num += mesh.vertexCount;
			}
			return array;
		}

		private Vector2[] CombineUVs(IList<MeshCombineInstance> instances, int channel, int[] verticesRemap, int vertexCount)
		{
			Vector2[] array = new Vector2[vertexCount];
			List<Vector2> list = new List<Vector2>();
			int num = 0;
			foreach (MeshCombineInstance instance in instances)
			{
				Mesh mesh = instance.mesh;
				mesh.GetUVs(channel, list);
				if (list.Count == 0)
				{
					FillListWithValues(list, new Vector2(0f, 0f), mesh.vertexCount);
				}
				RemapVertexAttribute(list, array, verticesRemap, num);
				num += mesh.vertexCount;
			}
			return array;
		}

		private Transform[] CombineBones(IList<MeshCombineInstance> instances, out Matrix4x4[] bindposes, out Dictionary<int, int> oldToNewBones)
		{
			List<Transform> list = new List<Transform>();
			List<Matrix4x4> list2 = new List<Matrix4x4>();
			oldToNewBones = new Dictionary<int, int>();
			int num = 0;
			for (int i = 0; i < instances.Count; i++)
			{
				Transform[] bones = (instances[i].renderer as SkinnedMeshRenderer).bones;
				Matrix4x4[] bindposes2 = instances[i].mesh.bindposes;
				for (int j = 0; j < bones.Length; j++)
				{
					Transform transform = bones[j];
					Matrix4x4 other = bindposes2[j];
					int key = num + j;
					int num2 = -1;
					for (int k = 0; k < list.Count; k++)
					{
						if (list[k] == transform && list2[k].Equals(other))
						{
							num2 = k;
							break;
						}
					}
					if (num2 < 0)
					{
						list.Add(transform);
						list2.Add(bindposes2[j]);
						num2 = list.Count - 1;
					}
					oldToNewBones.Add(key, num2);
				}
				num += bones.Length;
			}
			bindposes = list2.ToArray();
			return list.ToArray();
		}

		private BoneWeight[] CombineBoneWeights(IList<MeshCombineInstance> instances, Dictionary<int, int> oldToNewBones, int[] verticesRemap, int vertexCount)
		{
			BoneWeight[] array = new BoneWeight[vertexCount];
			List<BoneWeight> list = new List<BoneWeight>();
			int num = 0;
			int num2 = 0;
			foreach (MeshCombineInstance instance in instances)
			{
				Mesh mesh = instance.mesh;
				mesh.GetBoneWeights(list);
				if (list.Count == 0)
				{
					FillListWithValues(list, default, mesh.vertexCount);
				}
				for (int i = 0; i < list.Count; i++)
				{
					BoneWeight value = list[i];
					value.boneIndex0 = oldToNewBones[value.boneIndex0 + num2];
					value.boneIndex1 = oldToNewBones[value.boneIndex1 + num2];
					value.boneIndex2 = oldToNewBones[value.boneIndex2 + num2];
					value.boneIndex3 = oldToNewBones[value.boneIndex3 + num2];
					list[i] = value;
				}
				RemapVertexAttribute(list, array, verticesRemap, num);
				num += instance.vertexCount;
				num2 += (instance.renderer as SkinnedMeshRenderer).bones.Length;
			}
			return array;
		}

		private string CreateCombinedMeshName(IList<MeshCombineInstance> instances)
		{
			string text = "";
			int num = Mathf.Min(3, instances.Count);
			for (int i = 0; i < num; i++)
			{
				string text2 = instances[i].renderer.name;
				if (text2.Length > 5)
				{
					text2 = text2.Remove(5);
				}
				text += $"{text2}_sub_{instances[i].submeshIndex}_";
			}
			return text.Remove(text.Length - 1);
		}

		private bool AnyHasNormals(IList<MeshCombineInstance> instances)
		{
			List<Vector3> list = new List<Vector3>();
			foreach (MeshCombineInstance instance in instances)
			{
				instance.mesh.GetNormals(list);
				if (list.Count > 0)
				{
					return true;
				}
			}
			return false;
		}

		private bool AnyHasTangents(IList<MeshCombineInstance> instances)
		{
			List<Vector4> list = new List<Vector4>();
			foreach (MeshCombineInstance instance in instances)
			{
				instance.mesh.GetTangents(list);
				if (list.Count > 0)
				{
					return true;
				}
			}
			return false;
		}

		private bool AnyHasUV(IList<MeshCombineInstance> instances, int channel)
		{
			List<Vector2> list = new List<Vector2>();
			foreach (MeshCombineInstance instance in instances)
			{
				instance.mesh.GetUVs(channel, list);
				if (list.Count > 0)
				{
					return true;
				}
			}
			return false;
		}

		private void FillListWithValues<T>(List<T> list, T value, int count)
		{
			for (int i = 0; i < count; i++)
			{
				list.Add(value);
			}
		}

		private void RemapVertexAttribute<T>(IList<T> source, IList<T> destination, int[] verticesRemap, int vertexOffset)
		{
			for (int i = 0; i < source.Count; i++)
			{
				int num = verticesRemap[i + vertexOffset];
				if (num != -1)
				{
					destination[num] = source[i];
				}
			}
		}
	}
}
