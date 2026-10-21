using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bozo.ModularCharacters
{
	public class BoZo_CharacterOptimizer
	{
		private class boneData
		{
			public Transform bone;

			public int index;
		}

		private struct BlendshapeData
		{
			public string name;

			public float weight;

			public float currentWeight;

			public Vector3[] deltaVertices;

			public Vector3[] deltaNormals;

			public Vector3[] deltaTangents;

			public int vertexOffset;
		}

		private static string path = "/BoZo_StylizedModularCharacters/CustomCharacters/Prefabs";

		private MergedMaterialData[] mergedMaterialDatas;

		private OutfitSystem sourceSystem;

		public async Task<GameObject> OptimizeCharacter(OutfitSystem source, CharacterData data, bool save = false)
		{
			if (source == null)
			{
				return null;
			}
			if (source.mergeMaterial == null)
			{
				Debug.Log("Merge Material required, please assign one in the inspector");
				return null;
			}
			sourceSystem = source;
			return await Merge(source, save);
		}

		public void MergeButTheBetterOne(OutfitSystem outfitSystem)
		{
			SkinnedMeshRenderer characterBody = outfitSystem.GetCharacterBody();
			List<Outfit> outfits = outfitSystem.GetOutfits();
			List<Renderer> list = new List<Renderer>();
			Transform rootBone = characterBody.rootBone;
			Dictionary<string, boneData> dictionary = new Dictionary<string, boneData>();
			List<Transform> list2 = outfitSystem.GetBones().Values.ToList();
			for (int i = 0; i < list2.Count; i++)
			{
				try
				{
					boneData boneData2 = new boneData();
					boneData2.bone = list2[i];
					boneData2.index = i;
					dictionary.Add(list2[i].name, boneData2);
				}
				catch
				{
					Debug.LogError("Missing Bone In Bone Map");
				}
			}
			List<Vector3> list3 = new List<Vector3>();
			List<Vector3> list4 = new List<Vector3>();
			List<Vector4> list5 = new List<Vector4>();
			List<Vector2> list6 = new List<Vector2>();
			List<Color> list7 = new List<Color>();
			List<BoneWeight> list8 = new List<BoneWeight>();
			Matrix4x4[] array = new Matrix4x4[list2.Count];
			bool[] array2 = new bool[list2.Count];
			bool[] array3 = new bool[list2.Count];
			bool[] array4 = new bool[list2.Count];
			List<List<int>> list9 = new List<List<int>>();
			List<Material> list10 = new List<Material>();
			list10.Add(outfitSystem.GetCharacterBody().sharedMaterial);
			new List<string>();
			Dictionary<string, List<BlendshapeData>> dictionary2 = new Dictionary<string, List<BlendshapeData>>();
			int num = 0;
			list9.Add(new List<int>());
			try
			{
				foreach (Outfit item2 in outfits)
				{
					if (item2 == null || !item2.mergeMesh || outfitSystem.hiddenTypes.ContainsKey(item2.Type))
					{
						continue;
					}
					SkinnedMeshRenderer[] skinnedRenderers = item2.skinnedRenderers;
					foreach (SkinnedMeshRenderer skinnedMeshRenderer in skinnedRenderers)
					{
						if (skinnedMeshRenderer.gameObject.activeSelf)
						{
							list.Add(skinnedMeshRenderer);
						}
					}
					Animator componentInChildren = item2.GetComponentInChildren<Animator>();
					if (componentInChildren != null)
					{
						componentInChildren.enabled = false;
						componentInChildren.Rebind();
					}
					item2.gameObject.SetActive(value: false);
				}
			}
			catch
			{
				Debug.LogError("Mesh Merge Initalization failed");
				return;
			}
			try
			{
				for (int k = 0; k < list.Count; k++)
				{
					Outfit componentInParent = list[k].GetComponentInParent<Outfit>(includeInactive: true);
					SkinnedMeshRenderer componentInChildren2 = list[k].GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true);
					if (componentInChildren2 == null || componentInChildren2.sharedMesh == null)
					{
						Debug.LogError($"[BoZoCharacterOptimizer] Source renderer or mesh is missing. RendererIndex={k}");
						return;
					}
					Mesh sharedMesh = componentInChildren2.sharedMesh;
					Transform[] bones = componentInChildren2.bones;
					Dictionary<int, int> dictionary3 = new Dictionary<int, int>();
					try
					{
						if (componentInChildren2.rootBone != null)
						{
							for (int l = 0; l < bones.Length; l++)
							{
								Transform transform = bones[l];
								int num2 = -1;
								if (!(transform == null))
								{
									if (dictionary.ContainsKey(transform.name))
									{
										num2 = dictionary[transform.name].index;
									}
									else
									{
										Debug.LogWarning(transform.name + " Is not in BoneMap");
									}
									if (num2 != -1)
									{
										dictionary3[l] = num2;
									}
								}
							}
							Matrix4x4[] bindposes = sharedMesh.bindposes;
							Matrix4x4 matrix4x = componentInChildren2.transform.worldToLocalMatrix * characterBody.transform.localToWorldMatrix;
							bool flag = componentInParent != null && componentInParent.Type != null && componentInParent.Type.name == "Body";
							int num3 = Mathf.Min(bones.Length, bindposes.Length);
							for (int m = 0; m < num3; m++)
							{
								if (dictionary3.TryGetValue(m, out var value))
								{
									Matrix4x4 matrix4x2 = bindposes[m] * matrix4x;
									if (!array2[value] || (flag && !array3[value]))
									{
										array[value] = matrix4x2;
										array2[value] = true;
										array3[value] = flag;
									}
								}
							}
						}
					}
					catch (Exception ex)
					{
						Debug.LogError("[BoZoCharacterOptimizer] Bone mapping failed. Renderer=" + componentInChildren2.name + ", Error=" + ex.Message);
						return;
					}
					Matrix4x4 matrix4x3 = characterBody.transform.worldToLocalMatrix * componentInChildren2.transform.localToWorldMatrix;
					Matrix4x4 transpose = matrix4x3.inverse.transpose;
					Vector3[] vertices = sharedMesh.vertices;
					Vector3[] normals = sharedMesh.normals;
					Vector4[] tangents = sharedMesh.tangents;
					Vector3[] array5 = new Vector3[sharedMesh.vertexCount];
					Vector3[] array6 = new Vector3[sharedMesh.vertexCount];
					Vector4[] array7 = new Vector4[sharedMesh.vertexCount];
					for (int n = 0; n < sharedMesh.vertexCount; n++)
					{
						array5[n] = matrix4x3.MultiplyPoint3x4(vertices[n]);
						if (normals != null && normals.Length == sharedMesh.vertexCount)
						{
							array6[n] = transpose.MultiplyVector(normals[n]).normalized;
						}
						else
						{
							array6[n] = Vector3.up;
						}
						if (tangents != null && tangents.Length == sharedMesh.vertexCount)
						{
							Vector3 vector = new Vector3(tangents[n].x, tangents[n].y, tangents[n].z);
							vector = matrix4x3.MultiplyVector(vector).normalized;
							array7[n] = new Vector4(vector.x, vector.y, vector.z, tangents[n].w);
						}
						else
						{
							array7[n] = new Vector4(1f, 0f, 0f, 1f);
						}
					}
					list3.AddRange(array5);
					list4.AddRange(array6);
					list5.AddRange(array7);
					list6.AddRange(sharedMesh.uv);
					Color[] colors = sharedMesh.colors;
					if (colors != null && colors.Length == sharedMesh.vertexCount)
					{
						list7.AddRange(colors);
					}
					else
					{
						for (int num4 = 0; num4 < sharedMesh.vertexCount; num4++)
						{
							list7.Add(Color.red);
						}
					}
					try
					{
						BoneWeight[] boneWeights = sharedMesh.boneWeights;
						int fallbackBoneIndex;
						if (componentInChildren2.rootBone != null && dictionary.TryGetValue(componentInChildren2.rootBone.name, out var value2))
						{
							fallbackBoneIndex = value2.index;
						}
						else
						{
							if (dictionary3.Count <= 0)
							{
								throw new InvalidOperationException("No mapped bones are available for renderer " + componentInChildren2.name + ".");
							}
							fallbackBoneIndex = dictionary3.Values.First();
						}
						BoneWeight[] array8 = boneWeights;
						for (int j = 0; j < array8.Length; j++)
						{
							BoneWeight boneWeight = array8[j];
							BoneWeight item = new BoneWeight
							{
								boneIndex0 = ResolveMappedBoneIndex(dictionary3, boneWeight.boneIndex0, boneWeight.weight0, fallbackBoneIndex, componentInChildren2),
								boneIndex1 = ResolveMappedBoneIndex(dictionary3, boneWeight.boneIndex1, boneWeight.weight1, fallbackBoneIndex, componentInChildren2),
								boneIndex2 = ResolveMappedBoneIndex(dictionary3, boneWeight.boneIndex2, boneWeight.weight2, fallbackBoneIndex, componentInChildren2),
								boneIndex3 = ResolveMappedBoneIndex(dictionary3, boneWeight.boneIndex3, boneWeight.weight3, fallbackBoneIndex, componentInChildren2),
								weight0 = boneWeight.weight0,
								weight1 = boneWeight.weight1,
								weight2 = boneWeight.weight2,
								weight3 = boneWeight.weight3
							};
							if (boneWeight.weight0 > 0f)
							{
								array4[item.boneIndex0] = true;
							}
							if (boneWeight.weight1 > 0f)
							{
								array4[item.boneIndex1] = true;
							}
							if (boneWeight.weight2 > 0f)
							{
								array4[item.boneIndex2] = true;
							}
							if (boneWeight.weight3 > 0f)
							{
								array4[item.boneIndex3] = true;
							}
							list8.Add(item);
						}
					}
					catch (Exception ex2)
					{
						Debug.LogError("[BoZoCharacterOptimizer] Bone weight remap failed. Renderer=" + componentInChildren2.name + ", Error=" + ex2.Message);
						return;
					}
					List<int> list11 = list9[0];
					if (!componentInParent.mergeTexture)
					{
						list9.Add(new List<int>());
						list11 = list9[list9.Count - 1];
						list10.Add(componentInParent.material);
					}
					for (int num5 = 0; num5 < sharedMesh.subMeshCount; num5++)
					{
						List<int> list12 = sharedMesh.GetTriangles(num5).ToList();
						for (int num6 = 0; num6 < list12.Count; num6++)
						{
							list12[num6] += num;
						}
						list11.AddRange(list12);
					}
					int blendShapeCount = sharedMesh.blendShapeCount;
					for (int num7 = 0; num7 < blendShapeCount; num7++)
					{
						string text = sharedMesh.GetBlendShapeName(num7);
						string[] array9 = text.Split(".");
						if (array9.Length != 0)
						{
							text = text.Replace(array9[0] + ".", "");
						}
						int blendShapeFrameCount = sharedMesh.GetBlendShapeFrameCount(num7);
						for (int num8 = 0; num8 < blendShapeFrameCount; num8++)
						{
							float blendShapeFrameWeight = sharedMesh.GetBlendShapeFrameWeight(num7, num8);
							float blendShapeWeight = componentInChildren2.GetBlendShapeWeight(num7);
							Vector3[] array10 = new Vector3[sharedMesh.vertexCount];
							Vector3[] array11 = new Vector3[sharedMesh.vertexCount];
							Vector3[] array12 = new Vector3[sharedMesh.vertexCount];
							sharedMesh.GetBlendShapeFrameVertices(num7, num8, array10, array11, array12);
							for (int num9 = 0; num9 < sharedMesh.vertexCount; num9++)
							{
								array10[num9] = matrix4x3.MultiplyVector(array10[num9]);
								array11[num9] = transpose.MultiplyVector(array11[num9]);
								array12[num9] = matrix4x3.MultiplyVector(array12[num9]);
							}
							if (!dictionary2.ContainsKey(text))
							{
								dictionary2[text] = new List<BlendshapeData>();
							}
							dictionary2[text].Add(new BlendshapeData
							{
								name = text,
								weight = blendShapeFrameWeight,
								currentWeight = blendShapeWeight,
								deltaVertices = array10,
								deltaNormals = array11,
								deltaTangents = array12,
								vertexOffset = num
							});
						}
					}
					num += sharedMesh.vertexCount;
				}
			}
			catch
			{
				Debug.LogError("Merge Loop has failed");
				return;
			}
			for (int num10 = 0; num10 < list2.Count; num10++)
			{
				if (!array2[num10])
				{
					if (array4[num10])
					{
						Debug.LogError($"[BoZoCharacterOptimizer] Authored bindpose is missing for weighted bone. Bone={list2[num10].name}, MasterIndex={num10}");
						return;
					}
					array[num10] = list2[num10].worldToLocalMatrix * characterBody.transform.localToWorldMatrix;
				}
			}
			try
			{
				if (outfitSystem.combinedMesh != null)
				{
					UnityEngine.Object.Destroy(outfitSystem.combinedMesh);
				}
				Mesh mesh = (outfitSystem.combinedMesh = new Mesh());
				mesh.name = "MergedMesh";
				mesh.SetVertices(list3);
				mesh.SetNormals(list4);
				mesh.SetTangents(list5);
				mesh.SetUVs(0, list6);
				mesh.SetColors(list7);
				mesh.boneWeights = list8.ToArray();
				mesh.bindposes = array;
				mesh.subMeshCount = list9.Count;
				for (int num11 = 0; num11 < list9.Count; num11++)
				{
					mesh.SetTriangles(list9[num11], num11);
				}
				characterBody.sharedMesh = mesh;
				characterBody.bones = list2.ToArray();
				characterBody.rootBone = rootBone;
				characterBody.sharedMaterials = list10.ToArray();
				foreach (string item3 in dictionary2.Keys.OrderBy((string x) => x))
				{
					List<BlendshapeData> list13 = dictionary2[item3];
					float weight = list13[0].weight;
					float num12 = 0f;
					int vertexCount = mesh.vertexCount;
					Vector3[] array13 = new Vector3[vertexCount];
					Vector3[] array14 = new Vector3[vertexCount];
					Vector3[] array15 = new Vector3[vertexCount];
					foreach (BlendshapeData item4 in list13)
					{
						for (int num13 = 0; num13 < item4.deltaVertices.Length; num13++)
						{
							int num14 = item4.vertexOffset + num13;
							array13[num14] = item4.deltaVertices[num13];
							array14[num14] = item4.deltaNormals[num13];
							array15[num14] = item4.deltaTangents[num13];
						}
						num12 = Mathf.Max(num12, item4.currentWeight);
					}
					mesh.AddBlendShapeFrame(item3, weight, array13, array14, array15);
					int blendShapeIndex = mesh.GetBlendShapeIndex(item3);
					if (blendShapeIndex >= 0)
					{
						characterBody.SetBlendShapeWeight(blendShapeIndex, num12);
					}
				}
				mesh.RecalculateBounds();
				Bounds bounds = mesh.bounds;
				Vector3 vector2 = Vector3.Max(bounds.extents * 0.25f, Vector3.one * 0.15f);
				bounds.Expand(vector2 * 2f);
				characterBody.localBounds = bounds;
				characterBody.gameObject.SetActive(value: true);
			}
			catch
			{
				Debug.Log("Mesh Creation Failed");
			}
		}

		private static int ResolveMappedBoneIndex(Dictionary<int, int> boneIndexMap, int sourceBoneIndex, float weight, int fallbackBoneIndex, SkinnedMeshRenderer renderer)
		{
			if (boneIndexMap.TryGetValue(sourceBoneIndex, out var value))
			{
				return value;
			}
			if (weight <= 0f)
			{
				return fallbackBoneIndex;
			}
			throw new InvalidOperationException($"Weighted source bone is missing from the master rig. Renderer={renderer.name}, SourceBoneIndex={sourceBoneIndex}, Weight={weight}");
		}

		public async Task<GameObject> Merge(OutfitSystem outfitSystem, bool saveAsPrefab = false)
		{
			if (!Application.isPlaying)
			{
				return null;
			}
			List<Outfit> outfits = outfitSystem.GetOutfits();
			SkinnedMeshRenderer characterBody = outfitSystem.GetCharacterBody();
			Material mergeMaterial = outfitSystem.mergeMaterial;
			if (outfits == null || outfits.Count == 0)
			{
				Debug.LogError("No Skinned Mesh Renderers assigned.");
				return null;
			}
			List<Transform> list = outfitSystem.GetBones().Values.ToList();
			Transform rootBone = characterBody.rootBone;
			Transform parent = characterBody.transform.parent;
			Dictionary<string, boneData> dictionary = new Dictionary<string, boneData>();
			for (int i = 0; i < list.Count; i++)
			{
				boneData boneData2 = new boneData();
				boneData2.bone = list[i];
				boneData2.index = i;
				dictionary.Add(list[i].name, boneData2);
			}
			bool flag = false;
			List<Vector3> list2 = new List<Vector3>();
			List<Vector3> list3 = new List<Vector3>();
			List<Vector4> list4 = new List<Vector4>();
			List<Vector2> list5 = new List<Vector2>();
			List<Vector2> list6 = new List<Vector2>();
			List<Color> list7 = new List<Color>();
			List<BoneWeight> list8 = new List<BoneWeight>();
			new List<Material>();
			List<Matrix4x4> list9 = new List<Matrix4x4>();
			Dictionary<string, List<BlendshapeData>> dictionary2 = new Dictionary<string, List<BlendshapeData>>();
			int num = 0;
			List<List<int>> list10 = new List<List<int>>();
			int num2 = -999;
			new Texture2D(2, 2);
			Dictionary<string, Rect> dictionary3 = new Dictionary<string, Rect>();
			Material material = new Material(mergeMaterial);
			material.mainTexture = null;
			List<Renderer> list11 = new List<Renderer>();
			foreach (Outfit item4 in outfits)
			{
				if (!outfitSystem.hiddenTypes.ContainsKey(item4.Type))
				{
					list11.AddRange(item4.GetComponentsInChildren<Renderer>());
					Animator componentInChildren = item4.GetComponentInChildren<Animator>();
					if (componentInChildren != null)
					{
						componentInChildren.enabled = false;
					}
					if (item4.materialIndex > num2)
					{
						num2 = item4.materialIndex;
					}
					IOutfitExtension<Texture> component = item4.GetComponent<IOutfitExtension<Texture>>();
					if (component != null)
					{
						sourceSystem.customMaps[component.GetID()] = component.GetValue();
					}
				}
			}
			foreach (Renderer item5 in list11)
			{
				item5.enabled = false;
			}
			List<Material> list12 = PackTextures(outfitSystem);
			Dictionary<string, int> dictionary4 = new Dictionary<string, int>();
			for (int j = 0; j < list12.Count; j++)
			{
				dictionary4[list12[j].name] = j;
			}
			for (int k = 0; k < dictionary4.Count + 1; k++)
			{
				list10.Add(new List<int>());
			}
			if (list12.Count == 0)
			{
				MergedMaterialData[] array = mergedMaterialDatas;
				for (int l = 0; l < array.Length; l++)
				{
					_ = array[l];
				}
			}
			Dictionary<int, int> dictionary5 = new Dictionary<int, int>();
			for (int m = 0; m < list11.Count; m++)
			{
				SkinnedMeshRenderer skinnedMeshRenderer = list11[m].GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true);
				Outfit componentInParent = list11[m].GetComponentInParent<Outfit>(includeInactive: true);
				if (skinnedMeshRenderer == null)
				{
					Mesh sharedMesh = list11[m].GetComponentInChildren<MeshFilter>(includeInactive: true).sharedMesh;
					MeshRenderer componentInChildren2 = list11[m].GetComponentInChildren<MeshRenderer>(includeInactive: true);
					Material sharedMaterial = componentInChildren2.sharedMaterial;
					GameObject gameObject = componentInChildren2.gameObject;
					UnityEngine.Object.DestroyImmediate(componentInChildren2);
					UnityEngine.Object.DestroyImmediate(gameObject.GetComponent<MeshFilter>());
					skinnedMeshRenderer = gameObject.AddComponent<SkinnedMeshRenderer>();
					skinnedMeshRenderer.sharedMaterial = sharedMaterial;
					skinnedMeshRenderer.sharedMesh = sharedMesh;
				}
				Mesh sharedMesh2 = skinnedMeshRenderer.sharedMesh;
				bool flag2 = true;
				if (skinnedMeshRenderer.rootBone != null && !dictionary.ContainsKey(skinnedMeshRenderer.rootBone.name))
				{
					int count = list.Count;
					if (componentInParent.AttachPoint == "")
					{
						Debug.Log("What are you doing here? Stop the show...");
						return null;
					}
					skinnedMeshRenderer.rootBone = dictionary[componentInParent.AttachPoint].bone;
					list.AddRange(skinnedMeshRenderer.bones);
					skinnedMeshRenderer.bones[0].SetParent(dictionary[componentInParent.AttachPoint].bone);
					for (int n = 0; n < skinnedMeshRenderer.bones.Length; n++)
					{
						int num3 = 1;
						boneData boneData3 = new boneData();
						boneData3.bone = skinnedMeshRenderer.bones[n];
						boneData3.index = n + count;
						try
						{
							dictionary.Add(skinnedMeshRenderer.bones[n].name, boneData3);
						}
						catch
						{
							Debug.LogWarning("Duplicate bone naming in: <" + componentInParent.name + "> <" + skinnedMeshRenderer.bones[n].name + ">Please give bone a unique name");
							dictionary.Add(skinnedMeshRenderer.bones[n].name + num3, boneData3);
						}
					}
				}
				Transform[] bones = skinnedMeshRenderer.bones;
				if (skinnedMeshRenderer.rootBone != null)
				{
					for (int num4 = 0; num4 < bones.Length; num4++)
					{
						Transform transform = bones[num4];
						int num5 = -1;
						if (!(transform == null))
						{
							if (dictionary.ContainsKey(transform.name))
							{
								num5 = dictionary[transform.name].index;
							}
							else
							{
								Debug.LogWarning(transform.name + " Is not in BoneMap");
							}
							if (num5 != -1)
							{
								dictionary5[num4] = num5;
							}
						}
					}
				}
				else
				{
					flag2 = false;
					int index = dictionary[componentInParent.AttachPoint].index;
					dictionary5[0] = index;
					skinnedMeshRenderer.bones = list.ToArray();
					skinnedMeshRenderer.rootBone = rootBone;
				}
				Matrix4x4 localToWorldMatrix = skinnedMeshRenderer.transform.localToWorldMatrix;
				Vector3[] array2 = new Vector3[sharedMesh2.vertexCount];
				Vector3[] array3 = new Vector3[sharedMesh2.vertexCount];
				Vector4[] array4 = new Vector4[sharedMesh2.vertexCount];
				Vector3[] vertices = sharedMesh2.vertices;
				Vector3[] normals = sharedMesh2.normals;
				Vector4[] tangents = sharedMesh2.tangents;
				for (int num6 = 0; num6 < sharedMesh2.vertexCount; num6++)
				{
					array2[num6] = localToWorldMatrix.MultiplyPoint3x4(vertices[num6]);
					array3[num6] = localToWorldMatrix.MultiplyVector(normals[num6]).normalized;
					Vector3 normalized = localToWorldMatrix.MultiplyVector(new Vector3(tangents[num6].x, tangents[num6].y, tangents[num6].z)).normalized;
					array4[num6] = new Vector4(normalized.x, normalized.y, normalized.z, tangents[num6].w);
				}
				list2.AddRange(array2);
				list3.AddRange(array3);
				list4.AddRange(array4);
				if (flag)
				{
					Vector2[] uv = sharedMesh2.uv;
					Vector2[] array5 = new Vector2[uv.Length];
					Rect rect = dictionary3[componentInParent.name];
					for (int num7 = 0; num7 < uv.Length; num7++)
					{
						Vector2 vector = uv[num7];
						array5[num7] = new Vector2(rect.x + vector.x * rect.width, rect.y + vector.y * rect.height);
					}
					list5.AddRange(array5);
				}
				else
				{
					list5.AddRange(sharedMesh2.uv);
					if (sharedMesh2.uv2 != null && sharedMesh2.uv2.Length == sharedMesh2.vertexCount)
					{
						list6.AddRange(sharedMesh2.uv2);
					}
					else
					{
						Vector2[] collection = new Vector2[sharedMesh2.vertexCount];
						list6.AddRange(collection);
					}
				}
				Color[] colors = sharedMesh2.colors;
				if (colors != null && colors.Length == sharedMesh2.vertexCount)
				{
					list7.AddRange(colors);
				}
				else
				{
					for (int num8 = 0; num8 < sharedMesh2.vertexCount; num8++)
					{
						list7.Add(Color.red);
					}
				}
				if (flag2)
				{
					BoneWeight[] boneWeights = sharedMesh2.boneWeights;
					for (int l = 0; l < boneWeights.Length; l++)
					{
						BoneWeight boneWeight = boneWeights[l];
						if (!dictionary5.ContainsKey(boneWeight.boneIndex0))
						{
							BoneWeight item = new BoneWeight
							{
								boneIndex0 = dictionary5[dictionary[skinnedMeshRenderer.rootBone.name].index],
								weight0 = 1f
							};
							list8.Add(item);
							continue;
						}
						BoneWeight item2 = new BoneWeight
						{
							boneIndex0 = dictionary5[boneWeight.boneIndex0],
							boneIndex1 = dictionary5[boneWeight.boneIndex1],
							boneIndex2 = dictionary5[boneWeight.boneIndex2],
							boneIndex3 = dictionary5[boneWeight.boneIndex3],
							weight0 = boneWeight.weight0,
							weight1 = boneWeight.weight1,
							weight2 = boneWeight.weight2,
							weight3 = boneWeight.weight3
						};
						list8.Add(item2);
					}
				}
				else
				{
					BoneWeight[] boneWeights = new BoneWeight[sharedMesh2.vertexCount];
					foreach (BoneWeight boneWeight2 in boneWeights)
					{
						BoneWeight item3 = new BoneWeight
						{
							boneIndex0 = dictionary5[boneWeight2.boneIndex0],
							weight0 = 1f
						};
						list8.Add(item3);
					}
				}
				int index2 = componentInParent.materialIndex;
				if (componentInParent.materialIndex == -1)
				{
					index2 = dictionary4[componentInParent.Type.name];
				}
				for (int num9 = 0; num9 < sharedMesh2.subMeshCount; num9++)
				{
					List<int> list13 = sharedMesh2.GetTriangles(num9).ToList();
					for (int num10 = 0; num10 < list13.Count; num10++)
					{
						list13[num10] += num;
					}
					list10[index2].AddRange(list13);
				}
				int blendShapeCount = sharedMesh2.blendShapeCount;
				for (int num11 = 0; num11 < blendShapeCount; num11++)
				{
					string text = sharedMesh2.GetBlendShapeName(num11);
					string[] array6 = text.Split(".");
					if (array6.Length != 0)
					{
						text = text.Replace(array6[0] + ".", "");
					}
					int blendShapeFrameCount = sharedMesh2.GetBlendShapeFrameCount(num11);
					for (int num12 = 0; num12 < blendShapeFrameCount; num12++)
					{
						float blendShapeFrameWeight = sharedMesh2.GetBlendShapeFrameWeight(num11, num12);
						float blendShapeWeight = skinnedMeshRenderer.GetBlendShapeWeight(num11);
						Vector3[] deltaVertices = new Vector3[sharedMesh2.vertexCount];
						Vector3[] deltaNormals = new Vector3[sharedMesh2.vertexCount];
						Vector3[] deltaTangents = new Vector3[sharedMesh2.vertexCount];
						sharedMesh2.GetBlendShapeFrameVertices(num11, num12, deltaVertices, deltaNormals, deltaTangents);
						if (!dictionary2.ContainsKey(text))
						{
							dictionary2[text] = new List<BlendshapeData>();
						}
						dictionary2[text].Add(new BlendshapeData
						{
							name = text,
							weight = blendShapeFrameWeight,
							currentWeight = blendShapeWeight,
							deltaVertices = deltaVertices,
							deltaNormals = deltaNormals,
							deltaTangents = deltaTangents,
							vertexOffset = num
						});
					}
				}
				num += sharedMesh2.vertexCount;
			}
			foreach (Outfit item6 in outfits)
			{
				UnityEngine.Object.DestroyImmediate(item6.gameObject);
			}
			UnityEngine.Object.DestroyImmediate(characterBody.gameObject);
			for (int num13 = 0; num13 < list.Count; num13++)
			{
				list9.Add(list[num13].worldToLocalMatrix * rootBone.localToWorldMatrix);
			}
			Mesh mesh = new Mesh();
			mesh.name = "Combined Skinned Mesh";
			mesh.SetVertices(list2);
			mesh.SetNormals(list3);
			mesh.SetTangents(list4);
			mesh.SetUVs(0, list5);
			mesh.SetUVs(1, list6);
			mesh.SetColors(list7);
			mesh.boneWeights = list8.ToArray();
			mesh.bindposes = list9.ToArray();
			mesh.subMeshCount = list10.Count;
			for (int num14 = 0; num14 < list10.Count; num14++)
			{
				mesh.SetTriangles(list10[num14], num14);
			}
			foreach (KeyValuePair<string, List<BlendshapeData>> item7 in dictionary2)
			{
				string key = item7.Key;
				List<BlendshapeData> value = item7.Value;
				float weight = value[0].weight;
				int vertexCount = mesh.vertexCount;
				Vector3[] array7 = new Vector3[vertexCount];
				Vector3[] array8 = new Vector3[vertexCount];
				Vector3[] array9 = new Vector3[vertexCount];
				foreach (BlendshapeData item8 in value)
				{
					for (int num15 = 0; num15 < item8.deltaVertices.Length; num15++)
					{
						array7[item8.vertexOffset + num15] = item8.deltaVertices[num15];
						array8[item8.vertexOffset + num15] = item8.deltaNormals[num15];
						array9[item8.vertexOffset + num15] = item8.deltaTangents[num15];
					}
				}
				mesh.AddBlendShapeFrame(key, weight, array7, array8, array9);
			}
			SkinnedMeshRenderer skinnedMeshRenderer2 = new GameObject("CombinedSkinnedMesh").AddComponent<SkinnedMeshRenderer>();
			skinnedMeshRenderer2.sharedMesh = mesh;
			skinnedMeshRenderer2.bones = list.ToArray();
			skinnedMeshRenderer2.rootBone = rootBone;
			skinnedMeshRenderer2.materials = list12.ToArray();
			foreach (string key2 in dictionary2.Keys)
			{
				BlendshapeData blendshapeData = dictionary2[key2][0];
				int blendShapeIndex = skinnedMeshRenderer2.sharedMesh.GetBlendShapeIndex(blendshapeData.name);
				skinnedMeshRenderer2.SetBlendShapeWeight(blendShapeIndex, blendshapeData.currentWeight);
			}
			skinnedMeshRenderer2.transform.parent = parent;
			Debug.Log("Dynamic skinning merge complete with bone remapping!");
			return parent.gameObject;
		}

		public List<Material> PackTextures(OutfitSystem system)
		{
			return BoZo_TextureBuilder.GetMaterials(system);
		}

		public async Task<(Texture2D texture, Dictionary<string, Texture2D> additionalMaps, Dictionary<string, Rect> rect, Dictionary<string, Texture2D> customMaps)> CreateMergedTextures(List<Outfit> outfits)
		{
			List<Texture2D> diffuseMaps = new List<Texture2D>();
			new List<Texture2D>();
			Dictionary<string, Texture2D[]> additionalMaps = new Dictionary<string, Texture2D[]>();
			Dictionary<string, Texture2D[]> customMaps = new Dictionary<string, Texture2D[]>();
			Material bakeMaterial = new Material(Shader.Find("BoZo/BakeTexture"));
			int index = 0;
			foreach (Outfit outfit in outfits)
			{
				SkinnedMeshRenderer componentInChildren = outfit.GetComponentInChildren<SkinnedMeshRenderer>();
				Renderer renderer = outfit.GetComponentInChildren<Renderer>();
				List<Mesh> list = new List<Mesh>();
				SkinnedMeshRenderer[] skinnedRenderers = outfit.skinnedRenderers;
				foreach (SkinnedMeshRenderer skinnedMeshRenderer in skinnedRenderers)
				{
					list.Add(skinnedMeshRenderer.sharedMesh);
				}
				if (componentInChildren == null)
				{
					list.Add(outfit.GetComponentInChildren<MeshFilter>(includeInactive: true).sharedMesh);
				}
				Material originalMaterial = renderer.sharedMaterial;
				renderer.sharedMaterial = bakeMaterial;
				if (!outfit.customShader)
				{
					bakeMaterial.mainTexture = originalMaterial.mainTexture;
					bakeMaterial.SetTexture("_DecalMap", originalMaterial.GetTexture("_DecalMap"));
					bakeMaterial.SetFloat("_DecalUVSet", originalMaterial.GetFloat("_DecalUVSet"));
					bakeMaterial.SetFloat("_DecalBlend", originalMaterial.GetFloat("_DecalBlend"));
					bakeMaterial.SetVector("_DecalScale", originalMaterial.GetVector("_DecalScale"));
					bakeMaterial.SetTexture("_PatternMap", originalMaterial.GetTexture("_PatternMap"));
					bakeMaterial.SetFloat("_PatternUVSet", originalMaterial.GetFloat("_PatternUVSet"));
					bakeMaterial.SetFloat("_PatternBlend", originalMaterial.GetFloat("_PatternBlend"));
					bakeMaterial.SetVector("_PatternScale", originalMaterial.GetVector("_PatternScale"));
					for (int j = 0; j < 9; j++)
					{
						bakeMaterial.SetColor("_Color_" + (j + 1), originalMaterial.GetColor("_Color_" + (j + 1)));
						bakeMaterial.SetColor("_Color_" + (j + 1), originalMaterial.GetColor("_Color_" + (j + 1)));
						if (j + 1 <= 3)
						{
							bakeMaterial.SetColor("_DecalColor_" + (j + 1), originalMaterial.GetColor("_DecalColor_" + (j + 1)));
							bakeMaterial.SetColor("_PatternColor_" + (j + 1), originalMaterial.GetColor("_PatternColor_" + (j + 1)));
						}
					}
				}
				else
				{
					diffuseMaps.Add((Texture2D)originalMaterial.mainTexture);
				}
				MergedMaterialData[] array = mergedMaterialDatas;
				foreach (MergedMaterialData mergedMaterialData in array)
				{
					if (!additionalMaps.ContainsKey(mergedMaterialData.toMateiralProperty))
					{
						additionalMaps[mergedMaterialData.toMateiralProperty] = new Texture2D[outfits.Count];
					}
					additionalMaps[mergedMaterialData.toMateiralProperty][index] = (Texture2D)originalMaterial.GetTexture(mergedMaterialData.fromMateiralProperty);
				}
				IOutfitExtension[] componentsInChildren = outfit.GetComponentsInChildren<IOutfitExtension>();
				foreach (IOutfitExtension outfitExtension in componentsInChildren)
				{
					if (outfitExtension.GetValue() is Texture2D && !customMaps.ContainsKey(outfitExtension.GetID()))
					{
						customMaps[outfitExtension.GetID()] = new Texture2D[outfits.Count];
					}
					if (outfitExtension.GetValue() is Texture2D)
					{
						customMaps[outfitExtension.GetID()][index] = (Texture2D)outfitExtension.GetValue();
					}
				}
				diffuseMaps.Add(await BakeTextureAsyncTask(list, bakeMaterial));
				renderer.sharedMaterial = originalMaterial;
				RenderTexture.active = null;
				index++;
			}
			int atlasSize = 2048;
			Texture2D atlas = new Texture2D(atlasSize, atlasSize);
			new Texture2D(atlasSize, atlasSize);
			Dictionary<string, Texture2D> additionalMapsList = new Dictionary<string, Texture2D>();
			Dictionary<string, Texture2D> atlasCustomMapsList = new Dictionary<string, Texture2D>();
			atlas.wrapMode = TextureWrapMode.Repeat;
			Rect[] rects = atlas.PackTextures(diffuseMaps.ToArray(), 0, atlasSize);
			Dictionary<string, Rect> rectMap = new Dictionary<string, Rect>();
			for (int k = 0; k < outfits.Count; k++)
			{
				rectMap.Add(outfits[k].name, rects[k]);
			}
			Color32[] pixels = atlas.GetPixels32();
			atlas.SetPixels32(await DilateTextureAsync(atlas, pixels));
			atlas.Apply();
			int addIndex = 0;
			foreach (KeyValuePair<string, Texture2D[]> item in additionalMaps)
			{
				List<Texture2D> normalMaps = item.Value.ToList();
				Texture2D texture2D = new Texture2D(atlasSize, atlasSize);
				texture2D.wrapMode = TextureWrapMode.Repeat;
				Dictionary<string, Texture2D> dictionary = additionalMapsList;
				string key = item.Key;
				dictionary[key] = await RemapTextureAsync(normalMaps, rects, atlasSize, texture2D, mergedMaterialDatas[addIndex].backgroundColor);
				addIndex++;
			}
			foreach (KeyValuePair<string, Texture2D[]> item2 in customMaps)
			{
				List<Texture2D> list2 = item2.Value.ToList();
				if (list2.Count != 0)
				{
					Texture2D texture2D2 = new Texture2D(atlasSize, atlasSize);
					texture2D2.wrapMode = TextureWrapMode.Repeat;
					Dictionary<string, Texture2D> dictionary = atlasCustomMapsList;
					string key = item2.Key;
					dictionary[key] = await RemapTextureAsync(list2, rects, atlasSize, texture2D2, Color.black);
				}
			}
			return (texture: atlas, additionalMaps: additionalMapsList, rect: rectMap, customMaps: atlasCustomMapsList);
		}

		public async Task<Texture2D> BakeTextureAsyncTask(List<Mesh> mesh, Material bakeMaterial)
		{
			int textureSize = bakeMaterial.mainTexture.width;
			RenderTexture renderTexture = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.ARGB32)
			{
				useMipMap = false,
				autoGenerateMips = false
			};
			renderTexture.Create();
			CommandBuffer commandBuffer = new CommandBuffer();
			commandBuffer.SetRenderTarget(renderTexture);
			commandBuffer.ClearRenderTarget(clearDepth: true, clearColor: true, Color.clear);
			foreach (Mesh item in mesh)
			{
				commandBuffer.DrawMesh(item, Matrix4x4.identity, bakeMaterial);
			}
			Graphics.ExecuteCommandBuffer(commandBuffer);
			commandBuffer.Release();
			TaskCompletionSource<Texture2D> tcs = new TaskCompletionSource<Texture2D>();
			AsyncGPUReadback.Request(renderTexture, 0, TextureFormat.RGBA32, (AsyncGPUReadbackRequest request) =>
			{
				if (request.hasError)
				{
					tcs.SetException(new Exception("Async GPU readback failed."));
				}
				else
				{
					NativeArray<Color32> data = request.GetData<Color32>();
					Texture2D texture2D = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, mipChain: false);
					texture2D.LoadRawTextureData(data);
					texture2D.Apply();
					tcs.SetResult(texture2D);
				}
			});
			return await tcs.Task;
		}

		public async Task<Texture2D> RemapTextureAsync(List<Texture2D> normalMaps, Rect[] rects, int atlasSize, Texture2D atlasNormal, Color fillColor)
		{
			List<Task> list = new List<Task>();
			Color[] array = new Color[atlasSize * atlasSize];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = fillColor;
			}
			atlasNormal.SetPixels(array);
			for (int j = 0; j < normalMaps.Count; j++)
			{
				if (normalMaps[j] == null)
				{
					continue;
				}
				Rect rect = rects[j];
				int num = Mathf.RoundToInt(rect.x * (float)atlasSize);
				int num2 = Mathf.RoundToInt(rect.y * (float)atlasSize);
				int num3 = Mathf.RoundToInt(rect.width * (float)atlasSize);
				int num4 = Mathf.RoundToInt(rect.height * (float)atlasSize);
				Texture2D source = normalMaps[j];
				RenderTexture temporary = RenderTexture.GetTemporary(num3, num4, 0, RenderTextureFormat.ARGB32);
				Graphics.Blit(source, temporary);
				int copyX = num;
				int copyY = num2;
				int copyW = num3;
				int copyH = num4;
				RenderTexture copyRT = temporary;
				TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
				list.Add(tcs.Task);
				AsyncGPUReadback.Request(copyRT, 0, TextureFormat.RGBA32, (AsyncGPUReadbackRequest request) =>
				{
					if (request.hasError)
					{
						Debug.LogError("Normal map readback failed");
						tcs.SetResult(result: true);
						RenderTexture.ReleaseTemporary(copyRT);
					}
					else
					{
						Color32[] array2 = request.GetData<Color32>().ToArray();
						Color[] array3 = new Color[array2.Length];
						for (int k = 0; k < array2.Length; k++)
						{
							array3[k] = array2[k];
						}
						atlasNormal.SetPixels(copyX, copyY, copyW, copyH, array3);
						RenderTexture.ReleaseTemporary(copyRT);
						tcs.SetResult(result: true);
					}
				});
			}
			await Task.WhenAll(list);
			atlasNormal.Apply();
			return atlasNormal;
		}

		public async Task<Color32[]> DilateTextureAsync(Texture2D tex, Color32[] pixels)
		{
			int iterations = 2;
			int w = tex.width;
			int h = tex.height;
			return await Task.Run(() =>
			{
				Color32[] array = new Color32[pixels.Length];
				Color32[] array2 = new Color32[pixels.Length];
				bool[] array3 = new bool[pixels.Length];
				for (int i = 0; i < pixels.Length; i++)
				{
					array[i] = pixels[i];
					array3[i] = pixels[i].a >= byte.MaxValue;
				}
				for (int j = 0; j < iterations; j++)
				{
					array.CopyTo(array2, 0);
					for (int k = 0; k < h; k++)
					{
						for (int l = 0; l < w; l++)
						{
							int num = k * w + l;
							if (array[num].a < byte.MaxValue)
							{
								for (int m = -1; m <= 1; m++)
								{
									int num4;
									for (int n = -1; n <= 1; n++)
									{
										if (n == 0 && m == 0)
										{
											continue;
										}
										int num2 = l + n;
										int num3 = k + m;
										if (num2 < 0 || num2 >= w || num3 < 0 || num3 >= h)
										{
											continue;
										}
										num4 = num3 * w + num2;
										if (!array3[num4])
										{
											continue;
										}
										goto IL_0102;
									}
									continue;
									IL_0102:
									array2[num] = array[num4];
									break;
								}
							}
						}
					}
					Color32[] array4 = array;
					array = array2;
					array2 = array4;
					for (int num5 = 0; num5 < pixels.Length; num5++)
					{
						if (array[num5].a >= 1)
						{
							array3[num5] = true;
						}
					}
				}
				return array;
			});
		}
	}
}
