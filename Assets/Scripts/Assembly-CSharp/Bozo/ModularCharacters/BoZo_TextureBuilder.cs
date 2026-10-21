using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Experimental.Rendering;

namespace Bozo.ModularCharacters
{
	public static class BoZo_TextureBuilder
	{
		public class BoZo_TextureBuilderManager : MonoBehaviour
		{
			[Serializable]
			public class TextureBuilder
			{
				public Transform parent;

				public OutfitSystem outfitSystem;

				public OutfitSystem proxySystem;

				public Dictionary<string, Material> masterMaterials = new Dictionary<string, Material>();

				private Dictionary<string, Material> unpackedMaterials = new Dictionary<string, Material>();

				private Dictionary<string, Outfit> outfits = new Dictionary<string, Outfit>();

				private Dictionary<string, Dictionary<string, Material>> materials = new Dictionary<string, Dictionary<string, Material>>();

				public Dictionary<string, DecalBaker> decalOutfit = new Dictionary<string, DecalBaker>();

				public Camera cam;

				public List<string> properties = new List<string>();

				public HashSet<string> allProperties = new HashSet<string>();

				public HashSet<string> SubIDs = new HashSet<string>();

				public Dictionary<string, Color> baseColors = new Dictionary<string, Color>();

				public Dictionary<string, Renderer> meshes = new Dictionary<string, Renderer>();

				public Dictionary<string, RenderTexture> rts = new Dictionary<string, RenderTexture>();

				private bool renderCooldown;

				public bool locked;

				public HashSet<string> renderQueue = new HashSet<string>();

				public TextureBuilder()
				{
					parent = new GameObject().transform;
					parent.parent = _manager.transform;
					GameObject gameObject = new GameObject("Builder Camera");
					gameObject.transform.parent = parent;
					gameObject.transform.position = new Vector3(0f, 0f, 0f);
					cam = gameObject.AddComponent<Camera>();
					cam.orthographic = true;
					cam.clearFlags = CameraClearFlags.Color;
					cam.backgroundColor = Color.clear;
					cam.orthographicSize = 0.5f;
					cam.farClipPlane = 3f;
					cam.allowHDR = false;
					cam.enabled = false;
					OutfitSystem original = Resources.Load<OutfitSystem>("BSMC_CharacterMergedBase");
					proxySystem = UnityEngine.Object.Instantiate(original);
					proxySystem.name = "Proxy System";
					proxySystem.transform.parent = parent;
					proxySystem.transform.position = new Vector3(0f, -0.5f, 0f);
					proxySystem.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
				}

				public void Link(OutfitSystem sys)
				{
					parent.name = sys.name;
					outfitSystem = sys;
					foreach (Outfit outfit in outfitSystem.GetOutfits())
					{
						AddOutfit(outfit);
					}
					sys.OnOutfitChanged = (UnityAction<Outfit>)Delegate.Combine(sys.OnOutfitChanged, new UnityAction<Outfit>(AddOutfit));
					sys.OnOutfitRemoved = (UnityAction<Outfit>)Delegate.Combine(sys.OnOutfitRemoved, new UnityAction<Outfit>(RemoveOutfit));
					sys.OnCharacterLoaded = (UnityAction)Delegate.Combine(sys.OnCharacterLoaded, new UnityAction(PrepareRender));
				}

				public void AddOutfit(Outfit outfit)
				{
					if (outfit == null || outfitSystem == null)
					{
						return;
					}
					outfits[outfit.Type.name] = outfit;
					string text = outfit.materialIndex + outfit.Type.name;
					string text2 = outfit.materialIndex.ToString();
					if (outfit.materialIndex == -1 && !meshes.ContainsKey(text))
					{
						InitalizeWorkSpace(outfit.materialIndex, outfit.Type.name);
					}
					else if (!meshes.ContainsKey(outfit.materialIndex + "MainTexture"))
					{
						InitalizeWorkSpace(outfit.materialIndex);
					}
					if (outfit.supportDecals)
					{
						Outfit outfit2 = UnityEngine.Object.Instantiate(Resources.Load<Outfit>(outfit.GetOutfitData().outfit), proxySystem.transform);
						outfit2.Attach(force: true);
						outfit2.gameObject.SetActive(value: true);
						outfit2.gameObject.AddComponent<DecalBaker>();
						DecalBaker component = outfit2.GetComponent<DecalBaker>();
						component.bones = proxySystem.GetBones();
						decalOutfit[outfit2.Type.name] = component;
						component.onDecalChanged = (UnityAction<Outfit>)Delegate.Combine(component.onDecalChanged, new UnityAction<Outfit>(PrepareRender));
						if (outfit.materialIndex == -1)
						{
							component.SetBoundsPosition(meshes[text + "MainTexture"].transform.localPosition);
						}
						else
						{
							component.SetBoundsPosition(meshes[text2 + "MainTexture"].transform.localPosition);
						}
					}
					if (!outfit.gameObject.activeSelf)
					{
						return;
					}
					Material material = null;
					foreach (string property in properties)
					{
						string text3 = outfit.materialIndex + property;
						if (outfit.materialIndex == -1)
						{
							text3 = outfit.materialIndex + outfit.Type.name + property;
						}
						if (outfit.editMaterials.ContainsKey(text3))
						{
							material = outfit.editMaterials[text3];
						}
						else
						{
							material = new Material(Shader.Find("BoZo/BakeTexture"));
							material.enableInstancing = true;
							if (outfit.material.GetTexture("_IDMap") != null)
							{
								material.SetTexture("_IDMap", outfit.material.GetTexture("_IDMap"));
								material.SetFloat("_UsingIDMap", 1f);
							}
							material.name = outfit.OutfitName;
							if (property == "MainTexture")
							{
								material.mainTexture = outfit.material.mainTexture;
								List<Color> colors = outfit.GetColors();
								if (outfit.material.HasProperty("_UseCustomTexture"))
								{
									if (outfit.material.GetFloat("_UseCustomTexture") == 1f)
									{
										for (int i = 1; i < 10; i++)
										{
											if (material.HasProperty("_Color_" + i))
											{
												material.SetColor("_Color_" + i, colors[i - 1]);
											}
										}
									}
									else
									{
										material.SetFloat("_UseCustomTexture", 0f);
										material.color = outfit.GetColor(1);
									}
								}
							}
							else if (!property.Contains("_"))
							{
								IOutfitExtension[] componentsInChildren = outfit.GetComponentsInChildren<IOutfitExtension>();
								foreach (IOutfitExtension outfitExtension in componentsInChildren)
								{
									if (outfitExtension.GetID() == property)
									{
										material.mainTexture = (Texture)outfitExtension.GetValue();
										if (meshes[text3].sharedMaterials.Length == 0)
										{
											outfitSystem.customMaps.Remove(property);
										}
										else
										{
											outfitSystem.customMaps[property] = rts[text3];
										}
										break;
									}
								}
							}
							else
							{
								if (outfit.material.HasProperty(property))
								{
									material.mainTexture = outfit.material.GetTexture(property);
									material.SetFloat("_UseCustomTexture", 0f);
								}
								string key = text3.Replace(property, "MainTexture");
								material.SetTexture("_IDMap", materials[key][outfit.Type.name].mainTexture);
								material.SetFloat("_UsingIDMap", 1f);
								if (property.Contains("Normal") || property.Contains("normal"))
								{
									material.SetFloat("_isNormalMap", 1f);
								}
							}
						}
						material.SetColor("_BaseColor", baseColors[property]);
						if (material.mainTexture == null)
						{
							material.SetFloat("_hasTextureMap", 0f);
						}
						materials[text3][outfit.Type.name] = material;
						ReassignMaterials();
						outfit.editMaterials[text3] = material;
						Render(text3, force: true);
					}
					Dictionary<string, Material> dictionary = masterMaterials;
					if (outfit.materialIndex == -1)
					{
						dictionary = unpackedMaterials;
					}
					Material material2 = ((!dictionary.ContainsKey(outfit.materialIndex + outfit.Type.name)) ? dictionary[outfit.materialIndex.ToString()] : dictionary[outfit.materialIndex + outfit.Type.name]);
					outfit.SetMaterial(material2);
					outfit.OnColorChanged = (UnityAction<Outfit>)Delegate.Combine(outfit.OnColorChanged, new UnityAction<Outfit>(PrepareRender));
				}

				public void InitalizeWorkSpace(int materialIndex = 0, string outfitID = "")
				{
					string text = materialIndex + outfitID;
					if (allProperties.Contains(text + "MainTexture"))
					{
						return;
					}
					OutfitSystem outfitSystem = this.outfitSystem;
					SubIDs.Add(text);
					allProperties.Add(text + "MainTexture");
					if (!properties.Contains("MainTexture"))
					{
						properties.Add("MainTexture");
					}
					AddToWorkSpace(materialIndex, "MainTexture", outfitID, Color.black);
					for (int i = 0; i < outfitSystem.materialData.Length; i++)
					{
						string toMateiralProperty = outfitSystem.materialData[i].toMateiralProperty;
						if (!allProperties.Contains(text + toMateiralProperty))
						{
							allProperties.Add(text + toMateiralProperty);
						}
						if (!properties.Contains(toMateiralProperty))
						{
							properties.Add(toMateiralProperty);
						}
						AddToWorkSpace(materialIndex, toMateiralProperty, outfitID, outfitSystem.materialData[i].backgroundColor);
					}
				}

				public void AddToWorkSpace(int materialIndex, string propertyID, string outfitID, Color color)
				{
					int num = materialIndex;
					if (materialIndex == -1)
					{
						num = 12 + unpackedMaterials.Count;
					}
					int num2 = (num + 1) * 3;
					int num3 = properties.IndexOf(propertyID) - 1;
					string text = materialIndex + outfitID + propertyID;
					Dictionary<string, Material> dictionary = masterMaterials;
					if (text.Contains("-1"))
					{
						dictionary = unpackedMaterials;
					}
					string text2 = materialIndex.ToString();
					if (materialIndex == -1)
					{
						text2 += outfitID;
					}
					Material material;
					if (dictionary.ContainsKey(text2))
					{
						material = dictionary[text2];
					}
					else
					{
						material = new Material(outfitSystem.mergeMaterial);
						material.name = text2;
						dictionary[text2] = material;
					}
					GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
					gameObject.transform.parent = parent;
					gameObject.transform.localPosition = new Vector3(0f, -2 * (num3 + 1), num2);
					gameObject.name = text + "_Quad";
					meshes[text] = gameObject.GetComponent<Renderer>();
					RenderTexture renderTexture = new RenderTexture(2048, 2048, 0, GraphicsFormat.R8G8B8A8_SRGB);
					renderTexture.wrapMode = TextureWrapMode.Repeat;
					renderTexture.depthStencilFormat = GraphicsFormat.D16_UNorm;
					renderTexture.enableRandomWrite = false;
					renderTexture.useMipMap = false;
					renderTexture.Create();
					rts[text] = renderTexture;
					materials[text] = new Dictionary<string, Material>();
					if (propertyID == "MainTexture")
					{
						material.mainTexture = renderTexture;
					}
					else if (material.HasProperty(propertyID))
					{
						material.SetTexture(propertyID, renderTexture);
					}
					baseColors[propertyID] = color;
				}

				public void RemoveOutfit(Outfit outfit)
				{
					if (!outfits.ContainsValue(outfit))
					{
						return;
					}
					Outfit outfit2 = outfits[outfit.Type.name];
					outfit2.OnColorChanged = (UnityAction<Outfit>)Delegate.Remove(outfit2.OnColorChanged, new UnityAction<Outfit>(PrepareRender));
					outfits.Remove(outfit.Type.name);
					ReassignMaterials();
					foreach (string key in meshes.Keys)
					{
						Render(key);
					}
				}

				public void ReassignMaterials()
				{
					List<Outfit> list = new List<Outfit>(outfits.Values.ToList());
					list.Sort((Outfit a, Outfit b) => b.materialPriority.CompareTo(a.materialPriority));
					foreach (string allProperty in allProperties)
					{
						List<Material> list2 = new List<Material>();
						foreach (Outfit item in list)
						{
							string key = allProperty;
							if (!meshes.ContainsKey(key) || !meshes[key])
							{
								return;
							}
							if (materials[key].ContainsKey(item.Type.name))
							{
								list2.Add(materials[key][item.Type.name]);
								meshes[key].sharedMaterials = list2.ToArray();
								if (allProperty.Contains("MainTexture"))
								{
									item.SetControlMaterial(meshes[key], list2.Count - 1);
								}
								item.UpdateMaterialBlock();
							}
							for (int num = 0; num < meshes[key].sharedMaterials.Length; num++)
							{
								meshes[key].sharedMaterials[num].renderQueue = 1000 + num;
							}
						}
					}
				}

				public void Remove()
				{
					locked = true;
					Clean();
					cam.targetTexture = null;
					UnityEngine.Object.Destroy(parent.gameObject);
					UnityEngine.Object.Destroy(proxySystem.gameObject);
					foreach (Renderer value in meshes.Values)
					{
						UnityEngine.Object.Destroy(value.gameObject);
					}
				}

				public void Clean()
				{
					masterMaterials.Clear();
					unpackedMaterials.Clear();
					foreach (string key in materials.Keys)
					{
						foreach (Material value in materials[key].Values)
						{
							UnityEngine.Object.Destroy(value);
						}
					}
					materials.Clear();
					properties.Clear();
					allProperties.Clear();
					SubIDs.Clear();
					rts.Clear();
					foreach (Renderer value2 in meshes.Values)
					{
						UnityEngine.Object.Destroy(value2);
					}
					meshes.Clear();
					outfitSystem = null;
				}

				public void Organize(int x)
				{
					parent.position = new Vector3(10 * x, -5000f, 0f);
				}

				public void PrepareRender(Outfit outfit)
				{
					string text = outfit.materialIndex + outfit.Type.name + "MainTexture";
					if (rts.ContainsKey(text))
					{
						Render(text);
						return;
					}
					text = text.Replace(outfit.Type.name, "");
					Render(text);
				}

				public void PrepareRender()
				{
					Render();
				}

				public async void Render(string id = "", bool force = false)
				{
					if (!cam)
					{
						return;
					}
					if (renderCooldown)
					{
						if (id != "")
						{
							renderQueue.Add(id);
						}
					}
					else if (!locked)
					{
						renderCooldown = true;
						if (rts.ContainsKey(id))
						{
							RenderTexture active = RenderTexture.active;
							RenderTexture.active = rts[id];
							GL.Clear(clearDepth: true, clearColor: true, Color.clear);
							RenderTexture.active = active;
							cam.targetTexture = rts[id];
							Vector3 position = meshes[id].transform.position;
							cam.transform.position = new Vector3(position.x, position.y, position.z - 1f);
							cam.Render();
						}
						renderCooldown = false;
						if (renderQueue.Count != 0)
						{
							id = renderQueue.First();
							renderQueue.Remove(id);
							Render(id);
						}
					}
				}

				public List<Material> GetMaterials()
				{
					List<Material> list = new List<Material>();
					list.AddRange(masterMaterials.Values.ToList());
					list.AddRange(unpackedMaterials.Values.ToList());
					foreach (Material item in list)
					{
						item.name = item.name.Replace("-1", "");
					}
					return list;
				}
			}

			private TextureBuilder commonBuilder;

			private Dictionary<OutfitSystem, TextureBuilder> builders = new Dictionary<OutfitSystem, TextureBuilder>();

			public List<TextureBuilder> visableBuilders = new List<TextureBuilder>();

			private static HashSet<OutfitSystem> buildQueue = new HashSet<OutfitSystem>();

			private bool isBuilding;

			public async void QuickBuild(OutfitSystem system, bool save = false)
			{
				if (system == null)
				{
					return;
				}
				if (isBuilding)
				{
					buildQueue.Add(system);
					return;
				}
				isBuilding = true;
				if (commonBuilder == null)
				{
					commonBuilder = new TextureBuilder();
					commonBuilder.parent.position = new Vector3(-12f, -100f, 0f);
				}
				commonBuilder.locked = false;
				if (commonBuilder.outfitSystem == null)
				{
					OutfitSystem outfitSystem = UnityEngine.Object.Instantiate(Resources.Load<OutfitSystem>("BSMC_CharacterMergedBase"), new Vector3(0f, -100f, 0f), Quaternion.identity);
					outfitSystem.name = "CommonBuilder_MergedBase";
					outfitSystem.MuteHeightChange(value: true);
					outfitSystem.animator.enabled = false;
					outfitSystem.mergeMaterial = system.mergeMaterial;
					commonBuilder.Link(outfitSystem);
				}
				CharacterData data = system.data;
				BMAC_SaveSystem.LoadCharacter(commonBuilder.outfitSystem, data, manualShapeApply: true, async: true);
				commonBuilder.outfitSystem.data = data;
				await Task.Yield();
				commonBuilder.locked = true;
				system.customMaps = commonBuilder.outfitSystem.customMaps;
				BoZo_CharacterOptimizer boZo_CharacterOptimizer = new BoZo_CharacterOptimizer();
				commonBuilder.outfitSystem.prefabName = system.prefabName;
				system.SetCharacterBody(await boZo_CharacterOptimizer.OptimizeCharacter(commonBuilder.outfitSystem, data, save));
				system.SetRenderTextures(commonBuilder.rts.Values.ToList());
				UnityEngine.Object.Destroy(commonBuilder.outfitSystem.gameObject);
				await Task.Yield();
				commonBuilder.Remove();
				commonBuilder = null;
				isBuilding = false;
				if (buildQueue.Count != 0)
				{
					OutfitSystem outfitSystem2 = buildQueue.First();
					buildQueue.Remove(outfitSystem2);
					QuickBuild(outfitSystem2);
				}
			}

			public void AddOutfit(OutfitSystem system)
			{
				if (!builders.ContainsKey(system))
				{
					TextureBuilder textureBuilder = new TextureBuilder();
					textureBuilder.Link(system);
					builders.Add(system, textureBuilder);
					visableBuilders.Add(textureBuilder);
					List<TextureBuilder> list = builders.Values.ToList();
					for (int i = 0; i < list.Count; i++)
					{
						list[i].Organize(i);
					}
				}
			}

			public void RemoveBuilder(OutfitSystem system)
			{
				if (builders.ContainsKey(system))
				{
					builders[system].Remove();
					builders.Remove(system);
				}
			}

			public List<Material> GetMaterials(OutfitSystem system)
			{
				if (builders.ContainsKey(system))
				{
					return builders[system].GetMaterials();
				}
				return commonBuilder.GetMaterials();
			}

			public DecalBaker GetDecalControl(OutfitSystem outfitSystem, Outfit outfit)
			{
				if (builders.ContainsKey(outfitSystem) && builders[outfitSystem].decalOutfit.ContainsKey(outfit.Type.name))
				{
					return builders[outfitSystem].decalOutfit[outfit.Type.name];
				}
				if (commonBuilder != null)
				{
					return commonBuilder.decalOutfit[outfit.Type.name];
				}
				return null;
			}
		}

		private struct DecalPositions
		{
			public Vector3 pos;

			public Vector3 rot;

			public Vector3 scale;
		}

		private static BoZo_TextureBuilderManager _manager;

		public static void Init()
		{
			GameObject gameObject = new GameObject("BoZo_TextureBuilderManager");
			gameObject.AddComponent(typeof(BoZo_TextureBuilderManager));
			_manager = gameObject.GetComponent<BoZo_TextureBuilderManager>();
		}

		public static void AddOutfit(OutfitSystem system)
		{
			if (_manager == null)
			{
				Init();
			}
			_manager.AddOutfit(system);
		}

		public static void RemoveBuilder(OutfitSystem system)
		{
			if (!(_manager == null))
			{
				_manager.RemoveBuilder(system);
			}
		}

		public static List<Material> GetMaterials(OutfitSystem system)
		{
			return _manager.GetMaterials(system);
		}

		public static void QuickBuild(OutfitSystem system, bool save = false)
		{
			if (_manager == null)
			{
				Init();
			}
			_manager.QuickBuild(system, save);
		}

		public static DecalBaker GetDecalControl(OutfitSystem outfitSystem, Outfit outfit)
		{
			if (_manager == null)
			{
				return null;
			}
			return _manager.GetDecalControl(outfitSystem, outfit);
		}
	}
}
