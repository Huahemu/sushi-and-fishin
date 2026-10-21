using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MagicaCloth2;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Experimental.Rendering;

namespace Bozo.ModularCharacters
{
	public class OutfitSystem : MonoBehaviour
	{
		public enum LoadMode
		{
			OnStartAndOnValidate = 0,
			OnStart = 1,
			Manual = 2
		}

		private struct BonePose
		{
			public Vector3 localPosition;

			public Quaternion localRotation;

			public Vector3 localScale;
		}

		public CharacterObject characterData;

		private CharacterObject _characterData;

		public string SaveID;

		[SerializeField]
		private SkinnedMeshRenderer CharacterBody;

		public Dictionary<GameObject, float> heightSources = new Dictionary<GameObject, float>();

		private Animator _animator;

		public Dictionary<string, Transform> boneMap = new Dictionary<string, Transform>();

		public Dictionary<string, Transform> originalBoneMap = new Dictionary<string, Transform>();

		private Dictionary<Transform, BonePose> bindPose = new Dictionary<Transform, BonePose>();

		public List<Outfit> _Outfits = new List<Outfit>();

		public Dictionary<string, Outfit> Outfits = new Dictionary<string, Outfit>();

		public Dictionary<OutfitType, List<Outfit>> hiddenTypes = new Dictionary<OutfitType, List<Outfit>>();

		private static readonly HashSet<string> OverallHiddenTypeNames = new HashSet<string>(StringComparer.Ordinal) { "UnderLower", "UnderUpper" };

		private Dictionary<string, int> shapes = new Dictionary<string, int>();

		private Dictionary<string, float> shapesValues = new Dictionary<string, float>();

		private Dictionary<string, int> bodyShapes = new Dictionary<string, int>();

		private Dictionary<string, float> bodyShapesValues = new Dictionary<string, float>();

		private Dictionary<string, int> faceShapes = new Dictionary<string, int>();

		private Dictionary<string, float> faceShapesValues = new Dictionary<string, float>();

		private Dictionary<string, int> tagShapes = new Dictionary<string, int>();

		public Dictionary<string, BodyShapeModifier> bodyModifiers = new Dictionary<string, BodyShapeModifier>();

		private List<string> tags = new List<string>();

		public UnityAction<Outfit> OnOutfitChanged;

		public UnityAction<Outfit> OnOutfitRemoved;

		public UnityAction<SkinnedMeshRenderer> OnRigChanged;

		public UnityAction<string, float> OnShapeChanged;

		public UnityAction<List<string>> OnTagsChanged;

		public UnityAction OnCharacterLoaded;

		private Dictionary<string, RenderTexture> textures = new Dictionary<string, RenderTexture>();

		private RenderTexture copyTexture;

		private RenderTexture normalCopyTexture;

		private Material masterMaterial;

		public Material copyMaterial;

		public string prefabName;

		public Material mergeMaterial;

		public CharacterData data;

		private Dictionary<string, OutfitData> outfitData;

		public Dictionary<string, Texture> customMaps = new Dictionary<string, Texture>();

		public MergedMaterialData[] materialData;

		public List<RenderTexture> renderTextures = new List<RenderTexture>();

		public Mesh combinedMesh;

		public LoadMode loadMode;

		public bool async;

		[Tooltip("Allows for this character to have the fully customized. Great for Main Characters that will change often or for the Character Creator. NOT RECOMMENDED for characters that will never change")]
		public bool isloading;

		public bool isUpdatingTexture;

		public bool cleanAfterMerge;

		public bool isMerging;

		private bool mergeRequested;

		public bool isStatic;

		private ColliderComponent[] ClothColliders;

		public bool muteHeightChange { get; private set; }

		public bool mutebodyMods { get; private set; }

		public bool isbindPose { get; private set; }

		public float height { get; private set; }

		public Animator animator
		{
			get
			{
				if (_animator == null)
				{
					_animator = GetComponentInParent<Animator>();
					if (_animator == null)
					{
						_animator = GetComponentInChildren<Animator>();
					}
				}
				return _animator;
			}
			private set
			{
				_animator = value;
			}
		}

		public float stance { get; private set; }

		public string CurrentMaterialPackageId { get; private set; } = "BSMC_StylizedShader";

		public bool initalized { get; private set; }

		private void OnValidate()
		{
			if (Application.isPlaying && base.gameObject.scene.isLoaded && loadMode == LoadMode.OnStartAndOnValidate && base.gameObject.activeSelf)
			{
				Invoke("LoadFromObject", 0f);
			}
		}

		private void Awake()
		{
			Init();
			if (loadMode == LoadMode.OnStart || loadMode == LoadMode.OnStartAndOnValidate)
			{
				LoadFromObject();
			}
		}

		private void Start()
		{
			InitClothColliders();
		}

		private void OnDestroy()
		{
			ReleaseRenderTextures(renderTextures);
			renderTextures?.Clear();
			ReleaseDynamicTextures();
			if (!isStatic)
			{
				if (combinedMesh != null)
				{
					UnityEngine.Object.Destroy(combinedMesh);
				}
				if (masterMaterial != null)
				{
					UnityEngine.Object.Destroy(masterMaterial);
				}
			}
		}

		private void ReleaseDynamicTextures()
		{
			ReleaseRenderTextures(textures.Values);
			textures.Clear();
			ReleaseRenderTexture(copyTexture);
			ReleaseRenderTexture(normalCopyTexture);
			copyTexture = null;
			normalCopyTexture = null;
			DestroyOwnedObject(copyMaterial);
			copyMaterial = null;
		}

		private void ReleaseRenderTextures(IEnumerable<RenderTexture> texturesToRelease)
		{
			if (texturesToRelease == null)
			{
				return;
			}
			foreach (RenderTexture item in texturesToRelease)
			{
				ReleaseRenderTexture(item);
			}
		}

		private void ReleaseRenderTexture(RenderTexture texture)
		{
			if (!(texture == null))
			{
				if (texture.IsCreated())
				{
					texture.Release();
				}
				DestroyOwnedObject(texture);
			}
		}

		private static void DestroyOwnedObject(UnityEngine.Object resource)
		{
			if (!(resource == null))
			{
				if (Application.isPlaying)
				{
					UnityEngine.Object.Destroy(resource);
				}
				else
				{
					UnityEngine.Object.DestroyImmediate(resource);
				}
			}
		}

		public void Init()
		{
			if (initalized || isStatic)
			{
				return;
			}
			if (CharacterBody == null)
			{
				Debug.LogWarning("Outfit System does not have a Rig assigned please assign one to prevent this warning", base.gameObject);
				Debug.LogWarning("Attempting auto rig assignment...");
				SkinnedMeshRenderer[] componentsInChildren = GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true);
				foreach (SkinnedMeshRenderer skinnedMeshRenderer in componentsInChildren)
				{
					if (skinnedMeshRenderer.name == "BMAC_Body")
					{
						CharacterBody = skinnedMeshRenderer;
						Debug.Log("Rig Found Successfully!");
						break;
					}
				}
				Debug.LogError("Search Failed. Please Assign Mannually", base.gameObject);
			}
			else
			{
				InitBoneMap();
				InitBodyShapes();
				InitBodyMods();
				InitMaterial();
				InitTextures();
				InitBindPose();
				initalized = true;
			}
		}

		private void InitBoneMap()
		{
			boneMap.Clear();
			Transform[] bones = CharacterBody.bones;
			foreach (Transform transform in bones)
			{
				if (!boneMap.ContainsKey(transform.name))
				{
					boneMap.Add(transform.name, transform);
				}
			}
			originalBoneMap = new Dictionary<string, Transform>(boneMap);
		}

		public void InitTextures()
		{
			if (isStatic)
			{
				return;
			}
			ReleaseDynamicTextures();
			copyMaterial = new Material(Shader.Find("Hidden/TextureLayerer"));
			copyTexture = new RenderTexture(2024, 2024, 0);
			copyTexture.wrapMode = TextureWrapMode.Repeat;
			RenderTexture renderTexture = new RenderTexture(2024, 2024, 0, GraphicsFormat.R8G8B8A8_SRGB);
			renderTexture.name = "MainTexture";
			masterMaterial.SetTexture("mainTexture", renderTexture);
			masterMaterial.mainTexture = renderTexture;
			textures["mainTexture"] = renderTexture;
			renderTexture.wrapMode = TextureWrapMode.Repeat;
			MergedMaterialData[] array = materialData;
			foreach (MergedMaterialData mergedMaterialData in array)
			{
				if (!(mergedMaterialData.toMateiralProperty == "mainTexture"))
				{
					string toMateiralProperty = mergedMaterialData.toMateiralProperty;
					RenderTexture renderTexture2;
					if (toMateiralProperty.Contains("normal") || toMateiralProperty.Contains("Normal") || toMateiralProperty.Contains("bump") || toMateiralProperty.Contains("Bump"))
					{
						RenderTextureDescriptor desc = new RenderTextureDescriptor(2024, 2024);
						desc.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
						desc.depthBufferBits = 0;
						desc.msaaSamples = 1;
						desc.sRGB = false;
						desc.useMipMap = false;
						desc.autoGenerateMips = false;
						renderTexture2 = new RenderTexture(desc);
						renderTexture2.wrapMode = TextureWrapMode.Repeat;
					}
					else
					{
						renderTexture2 = new RenderTexture(2024, 2024, 0, GraphicsFormat.R8G8B8A8_SRGB);
						renderTexture2.wrapMode = TextureWrapMode.Repeat;
					}
					renderTexture2.name = mergedMaterialData.toMateiralProperty;
					renderTexture2.Create();
					textures[mergedMaterialData.toMateiralProperty] = renderTexture2;
					masterMaterial.SetTexture(mergedMaterialData.toMateiralProperty, renderTexture2);
				}
			}
		}

		private void InitMaterial()
		{
			if (mergeMaterial != null)
			{
				CurrentMaterialPackageId = "BSMC_StylizedShader";
				masterMaterial = new Material(mergeMaterial);
				CharacterBody.sharedMaterial = masterMaterial;
				OriginalNativeShaderRestoration.RestoreMaterial(masterMaterial);
			}
			else
			{
				string text = "";
				if (characterData == null)
				{
					text = "BSMC_StylizedShader";
				}
				if (text == "")
				{
					text = "BSMC_StylizedShader";
				}
				MaterialPackage materialPackage = Resources.Load<MaterialPackage>(text);
				CurrentMaterialPackageId = text;
				masterMaterial = new Material(materialPackage.material);
				materialData = materialPackage.materialData;
			}
			CharacterBody.sharedMaterial = masterMaterial;
			OriginalNativeShaderRestoration.RestoreMaterial(masterMaterial);
		}

		private void InitBodyShapes()
		{
			Outfit outfit = GetOutfit("Body");
			Outfit outfit2 = GetOutfit("Head");
			shapes.Clear();
			bodyShapes.Clear();
			faceShapes.Clear();
			tagShapes.Clear();
			new Dictionary<string, int>();
			if (outfit != null)
			{
				int blendShapeCount = outfit.skinnedRenderer.sharedMesh.blendShapeCount;
				for (int i = 0; i < blendShapeCount; i++)
				{
					string text = outfit.skinnedRenderer.sharedMesh.GetBlendShapeName(i);
					int num = text.IndexOf('.');
					if (num >= 0)
					{
						text = text.Substring(num + 1);
					}
					if (text.IndexOf('_') >= 0 && string.CompareOrdinal(text, 0, "Shape_", 0, 6) == 0)
					{
						bodyShapes.Add(text.Substring(6), i);
					}
				}
			}
			if (outfit2 != null)
			{
				int blendShapeCount2 = outfit2.skinnedRenderer.sharedMesh.blendShapeCount;
				for (int j = 0; j < blendShapeCount2; j++)
				{
					string text2 = outfit2.skinnedRenderer.sharedMesh.GetBlendShapeName(j);
					int num2 = text2.IndexOf('.');
					if (num2 >= 0)
					{
						text2 = text2.Substring(num2 + 1);
					}
					if (text2.IndexOf('_') >= 0 && string.CompareOrdinal(text2, 0, "Shape_", 0, 6) == 0)
					{
						faceShapes.Add(text2.Substring(6), j);
					}
				}
			}
			if (!(CharacterBody != null))
			{
				return;
			}
			int blendShapeCount3 = CharacterBody.sharedMesh.blendShapeCount;
			for (int k = 0; k < blendShapeCount3; k++)
			{
				string text3 = CharacterBody.sharedMesh.GetBlendShapeName(k);
				int num3 = text3.IndexOf('.');
				if (num3 >= 0)
				{
					text3 = text3.Substring(num3 + 1);
				}
				if (text3.IndexOf('_') >= 0 && string.CompareOrdinal(text3, 0, "Shape_", 0, 6) == 0)
				{
					string key = text3.Substring(6);
					shapes.Add(key, k);
					if (!shapesValues.ContainsKey(key))
					{
						shapesValues.Add(key, 0f);
					}
				}
			}
		}

		public void InitBodyMods()
		{
			List<BodyShapeModifier> list = new List<BodyShapeModifier>(GetComponentsInChildren<BodyShapeModifier>());
			bodyModifiers.Clear();
			for (int i = 0; i < list.Count; i++)
			{
				bodyModifiers[list[i].name] = list[i];
			}
		}

		public void InitBindPose()
		{
			bindPose.Clear();
			foreach (Transform value in boneMap.Values)
			{
				bindPose[value] = new BonePose
				{
					localPosition = value.localPosition,
					localRotation = value.localRotation,
					localScale = value.localScale
				};
			}
		}

		public void RestoreBindPose()
		{
			foreach (KeyValuePair<Transform, BonePose> item in bindPose)
			{
				if (!(item.Key == null))
				{
					item.Key.localPosition = item.Value.localPosition;
					item.Key.localRotation = item.Value.localRotation;
					item.Key.localScale = item.Value.localScale;
				}
			}
		}

		private void InitClothColliders()
		{
			ClothColliders = GetComponentsInChildren<ColliderComponent>();
		}

		public void RebuildClothAfterBodyChanges()
		{
			InitClothColliders();
			BoZo_MagicaClothSupport[] componentsInChildren = GetComponentsInChildren<BoZo_MagicaClothSupport>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].RebuildAfterBodyChanges();
			}
		}

		public void LoadFromObject(CharacterObject saveData)
		{
			if (!isloading)
			{
				characterData = saveData;
				LoadFromObject();
			}
		}

		[ContextMenu("Load")]
		public void LoadFromObject()
		{
			if (!isloading && (bool)characterData && _characterData != characterData)
			{
				_characterData = characterData;
				LoadCharacter(characterData.GetCharacterData());
			}
		}

		[ContextMenu("LoadByID")]
		public void LoadFromID()
		{
			LoadFromID(SaveID);
		}

		public void LoadFromID(string saveName)
		{
			if (!string.IsNullOrEmpty(saveName))
			{
				SaveID = saveName;
				CharacterData dataFromID = BMAC_SaveSystem.GetDataFromID(SaveID);
				if (dataFromID != null)
				{
					LoadCharacter(dataFromID);
				}
			}
		}

		public void LoadCharacter(CharacterData data)
		{
			if (!isStatic)
			{
				BMAC_SaveSystem.LoadCharacter(this, data, manualShapeApply: false, async);
			}
		}

		[ContextMenu("SaveToObject")]
		public void SaveToObject()
		{
			if (!characterData)
			{
				Debug.LogWarning("Character Data Field is empty. Please provide a BSMC_CharacterObject to " + base.transform.name);
			}
			else
			{
				BMAC_SaveSystem.SaveCharacter(this, characterData.GetCharacterData().characterName, characterData.GetCharacterIcon());
			}
		}

		[ContextMenu("SaveByID")]
		public void SaveByID()
		{
			SaveByID(SaveID);
		}

		public void SaveByID(string characterName)
		{
			if (string.IsNullOrEmpty(characterName))
			{
				Debug.LogWarning("No ID provided saving aborted");
				return;
			}
			BMAC_SaveSystem.EnsureSaveDirectories();
			if (!File.Exists(BMAC_SaveSystem.iconFilePath + "/" + characterName + ".png"))
			{
				byte[] bytes = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false).EncodeToPNG();
				File.WriteAllBytes(BMAC_SaveSystem.iconFilePath + "/" + characterName + ".png", bytes);
			}
			BMAC_SaveSystem.SaveCharacter(this, characterName);
		}

		public void RemoveOutfit(string type, bool merge = true)
		{
			Outfit outfit = GetOutfit(type);
			if (outfit != null)
			{
				RemoveOutfit(outfit.Type, merge);
			}
		}

		public async void RemoveOutfit(OutfitType type, bool merge = true)
		{
			if (Outfits.TryGetValue(type.name, out var value) && value != null)
			{
				Transform[] additionalBones = value.additionalBones;
				for (int i = 0; i < additionalBones.Length; i++)
				{
					Transform[] componentsInChildren = additionalBones[i].GetComponentsInChildren<Transform>(includeInactive: true);
					foreach (Transform transform in componentsInChildren)
					{
						boneMap.Remove(transform.name);
					}
				}
				RemoveHide(value);
				value.ReturnBones();
				value.transform.parent = null;
				UnityEngine.Object.DestroyImmediate(value.gameObject);
				Outfits[type.name] = null;
				_Outfits.Remove(value);
			}
			AddTags();
			OnOutfitChanged?.Invoke(null);
			if (merge)
			{
				MergeCharacter();
			}
		}

		public void RemoveTags(string[] outfitTags)
		{
			foreach (string item in outfitTags)
			{
				tags.Remove(item);
			}
			OnTagsChanged?.Invoke(tags);
		}

		public async void RemoveAllOutfits(bool returnBones = true)
		{
			List<Outfit> list = new List<Outfit>(Outfits.Values);
			Outfits.Clear();
			_Outfits.Clear();
			tags.Clear();
			hiddenTypes.Clear();
			boneMap = new Dictionary<string, Transform>(originalBoneMap);
			foreach (Outfit item in list)
			{
				if (!(item == null))
				{
					if (returnBones)
					{
						item.ReturnBones();
					}
					UnityEngine.Object.Destroy(item.gameObject);
				}
			}
		}

		public void AttachSkinnedOutfit(Outfit outfit)
		{
			AttachOutfit(outfit);
		}

		public async void AttachOutfit(Outfit outfit)
		{
			if (initalized && !isStatic)
			{
				outfit.transform.parent = base.transform;
				outfit.transform.SetPositionAndRotation(base.transform.position, base.transform.rotation);
				outfit.transform.localScale = Vector3.one;
				if (outfit.mergeMesh)
				{
					outfit.gameObject.SetActive(value: false);
				}
				RemoveOutfit(outfit.Type, merge: false);
				_Outfits.Add(outfit);
				Outfits[outfit.Type.name] = outfit;
				Outfit outfit2 = GetOutfit("Overall");
				if (outfit2 != null && OverallHiddenTypeNames.Contains(outfit.Type.name))
				{
					RegisterHiddenType(outfit.Type, outfit2);
				}
				if (hiddenTypes.ContainsKey(outfit.Type))
				{
					outfit.gameObject.SetActive(value: false);
				}
				MergeBones(outfit);
				outfit.OnColorChanged = (UnityAction<Outfit>)Delegate.Combine(outfit.OnColorChanged, new UnityAction<Outfit>(UpdateTexture));
				outfit.OnOutfitChanged = (UnityAction<Outfit>)Delegate.Combine(outfit.OnOutfitChanged, new UnityAction<Outfit>(MergeCharacter));
				ApplyShapesToOufit(outfit);
				if (outfit.Type.name == "Head" || outfit.Type.name == "Body")
				{
					InitBodyMods();
					InitBodyShapes();
				}
				AddTags();
				SetHide(outfit);
				OriginalNativeShaderRestoration.RestoreRendererMaterials(outfit.gameObject);
				OnOutfitChanged?.Invoke(outfit);
				if (isMerging || isloading)
				{
					mergeRequested = true;
				}
				else
				{
					MergeCharacter();
				}
			}
		}

		private async void UpdateTexture(Outfit outfit)
		{
			if (isUpdatingTexture || isMerging || isloading || hiddenTypes.ContainsKey(outfit.Type))
			{
				return;
			}
			isUpdatingTexture = true;
			await Task.Delay(50);
			if (this == null || outfit == null || animator == null)
			{
				isUpdatingTexture = false;
				return;
			}
			AnimatorStateInfo currentAnimatorStateInfo = animator.GetCurrentAnimatorStateInfo(0);
			float normalizedTime = currentAnimatorStateInfo.normalizedTime;
			int fullPathHash = currentAnimatorStateInfo.fullPathHash;
			animator.enabled = false;
			MuteBodyMods(mute: true);
			RestoreBindPose();
			if (outfit.materialPriority != 0)
			{
				MergeTextures();
				animator.enabled = true;
				animator.Play(fullPathHash, 0, normalizedTime % 1f);
				MuteBodyMods(mute: false);
				isUpdatingTexture = false;
				return;
			}
			RenderTexture renderTexture = textures["mainTexture"];
			Graphics.Blit(renderTexture, copyTexture);
			copyMaterial.mainTexture = renderTexture;
			copyMaterial.SetTexture("_BlendTex", outfit.material.mainTexture);
			copyMaterial.SetFloat("_UseCustomColors", 1f);
			copyMaterial.SetFloat("_isNormalMap", 0f);
			copyMaterial.SetTexture("_IDMap", outfit.material.GetTexture("_IDMap"));
			if (copyMaterial.GetTexture("_IDMap") == null)
			{
				copyMaterial.SetFloat("_UsingIDMap", 0f);
			}
			else
			{
				copyMaterial.SetFloat("_UsingIDMap", 1f);
			}
			for (int i = 0; i < outfit.colors.Length; i++)
			{
				copyMaterial.SetColor("_Color_" + (i + 1), outfit.colors[i]);
			}
			copyMaterial.SetTexture("_PatternMap", outfit.pattern);
			copyMaterial.SetVector("_PatternScale", outfit.patternSize);
			for (int j = 0; j < outfit.patternColors.Length; j++)
			{
				copyMaterial.SetColor("_PatternColor_" + (j + 1), outfit.colors[j]);
			}
			Graphics.Blit(renderTexture, copyTexture, copyMaterial);
			Graphics.Blit(copyTexture, renderTexture);
			outfit.ApplyDecals(renderTexture, copyTexture);
			animator.enabled = true;
			animator.Play(fullPathHash, 0, normalizedTime % 1f);
			MuteBodyMods(mute: false);
			isUpdatingTexture = false;
		}

		private void MergeTextures()
		{
			RenderTexture renderTexture = textures["mainTexture"];
			RenderTexture active = RenderTexture.active;
			RenderTexture.active = copyTexture;
			GL.Clear(clearDepth: true, clearColor: true, Color.clear);
			RenderTexture.active = renderTexture;
			GL.Clear(clearDepth: true, clearColor: true, Color.black);
			RenderTexture.active = active;
			_Outfits.Sort((Outfit a, Outfit b) => b.materialPriority.CompareTo(a.materialPriority));
			foreach (Outfit outfit in _Outfits)
			{
				if (outfit.mergeTexture && !hiddenTypes.ContainsKey(outfit.Type))
				{
					copyMaterial.mainTexture = renderTexture;
					copyMaterial.SetTexture("_BlendTex", outfit.material.mainTexture);
					copyMaterial.SetFloat("_UseCustomColors", 1f);
					copyMaterial.SetFloat("_isNormalMap", 0f);
					copyMaterial.SetTexture("_IDMap", outfit.material.GetTexture("_IDMap"));
					if (copyMaterial.GetTexture("_IDMap") == null)
					{
						copyMaterial.SetFloat("_UsingIDMap", 0f);
					}
					else
					{
						copyMaterial.SetFloat("_UsingIDMap", 1f);
					}
					for (int num = 0; num < outfit.colors.Length; num++)
					{
						copyMaterial.SetColor("_Color_" + (num + 1), outfit.colors[num]);
					}
					copyMaterial.SetTexture("_PatternMap", outfit.pattern);
					copyMaterial.SetVector("_PatternScale", outfit.patternSize);
					for (int num2 = 0; num2 < outfit.patternColors.Length; num2++)
					{
						copyMaterial.SetColor("_PatternColor_" + (num2 + 1), outfit.colors[num2]);
					}
					Graphics.Blit(renderTexture, copyTexture, copyMaterial);
					Graphics.Blit(copyTexture, renderTexture);
					outfit.ApplyDecals(renderTexture, copyTexture);
				}
			}
			MergedMaterialData[] array = materialData;
			foreach (MergedMaterialData mergedMaterialData in array)
			{
				string toMateiralProperty = mergedMaterialData.toMateiralProperty;
				if (toMateiralProperty == "mainTexture")
				{
					continue;
				}
				renderTexture = textures[toMateiralProperty];
				RenderTexture active2 = RenderTexture.active;
				RenderTexture.active = copyTexture;
				GL.Clear(clearDepth: true, clearColor: true, Color.clear);
				RenderTexture.active = renderTexture;
				GL.Clear(clearDepth: true, clearColor: true, mergedMaterialData.backgroundColor);
				RenderTexture.active = active2;
				foreach (Outfit outfit2 in _Outfits)
				{
					if (!outfit2.material.HasProperty(toMateiralProperty) || !outfit2.mergeTexture)
					{
						continue;
					}
					copyMaterial.mainTexture = renderTexture;
					copyMaterial.SetTexture("_BlendTex", outfit2.material.GetTexture(toMateiralProperty));
					copyMaterial.SetFloat("_UseCustomColors", 0f);
					copyMaterial.SetFloat("_UsingIDMap", 1f);
					copyMaterial.SetTexture("_IDMap", outfit2.material.mainTexture);
					if (toMateiralProperty.Contains("normal") || toMateiralProperty.Contains("Normal") || toMateiralProperty.Contains("bump") || toMateiralProperty.Contains("Bump"))
					{
						if (outfit2.material.GetTexture(toMateiralProperty) == null)
						{
							continue;
						}
						copyMaterial.SetFloat("_isNormalMap", 1f);
					}
					else
					{
						copyMaterial.SetFloat("_isNormalMap", 0f);
					}
					Graphics.Blit(renderTexture, copyTexture, copyMaterial);
					Graphics.Blit(copyTexture, renderTexture);
				}
			}
		}

		public void UpdateMaterial(MaterialPackage material)
		{
			if (material == null)
			{
				return;
			}
			CurrentMaterialPackageId = material.name;
			if (mergeMaterial != null)
			{
				return;
			}
			Material material2 = new Material(material.material);
			for (int i = 0; i < material.materialData.Length && i + 1 <= materialData.Length; i++)
			{
				if (material.materialData[i].toMateiralProperty == "mainTexture")
				{
					material2.mainTexture = masterMaterial.mainTexture;
					continue;
				}
				string toMateiralProperty = material.materialData[i].toMateiralProperty;
				string toMateiralProperty2 = materialData[i].toMateiralProperty;
				if (material2.HasProperty(toMateiralProperty2))
				{
					material2.SetTexture(toMateiralProperty, textures[toMateiralProperty2]);
				}
			}
			UnityEngine.Object.Destroy(masterMaterial);
			masterMaterial = material2;
			CharacterBody.sharedMaterial = material2;
			OriginalNativeShaderRestoration.RestoreMaterial(material2);
		}

		private void ApplyShapesToOufit(Outfit outfit)
		{
			List<string> list = new List<string>(bodyShapes.Keys);
			for (int i = 0; i < list.Count; i++)
			{
				GetBodyShapeValues();
				outfit.SetShape(list[i], GetShape(list[i]));
			}
		}

		private void ApplyAllShapes()
		{
			foreach (KeyValuePair<string, float> shapesValue in shapesValues)
			{
				if (shapes.TryGetValue(shapesValue.Key, out var value))
				{
					CharacterBody.SetBlendShapeWeight(value, shapesValue.Value);
				}
			}
		}

		public void ResetShapes()
		{
			foreach (KeyValuePair<string, int> shape in shapes)
			{
				SetShape(shape.Key, 0f);
			}
		}

		public void SetShape(string key, float value)
		{
			SkinnedMeshRenderer characterBody = CharacterBody;
			int num = -1;
			if (shapes.TryGetValue(key, out var value2) && (bool)characterBody)
			{
				num = value2;
			}
			if (num != -1)
			{
				characterBody.SetBlendShapeWeight(num, value);
				shapesValues[key] = value;
			}
			OnShapeChanged?.Invoke(key, value);
		}

		public void AddTags()
		{
			tags.Clear();
			foreach (Outfit outfit in _Outfits)
			{
				if (hiddenTypes.ContainsKey(outfit.Type))
				{
					continue;
				}
				tags.AddRange(outfit.tags);
				ApplyTags[] componentsInChildren = outfit.GetComponentsInChildren<ApplyTags>(includeInactive: true);
				foreach (ApplyTags applyTags in componentsInChildren)
				{
					if (applyTags.gameObject.activeSelf)
					{
						tags.AddRange(applyTags.tags);
					}
				}
			}
			OnTagsChanged?.Invoke(tags);
		}

		public void SetHide(Outfit outfit)
		{
			if (outfit == null)
			{
				return;
			}
			OutfitType[] hideTypes = outfit.HideTypes;
			foreach (OutfitType hiddenType in hideTypes)
			{
				RegisterHiddenType(hiddenType, outfit);
			}
			if (!string.Equals(outfit.Type.name, "Overall", StringComparison.Ordinal))
			{
				return;
			}
			foreach (Outfit outfit2 in _Outfits)
			{
				if (outfit2 != null && OverallHiddenTypeNames.Contains(outfit2.Type.name))
				{
					RegisterHiddenType(outfit2.Type, outfit);
				}
			}
		}

		private void RegisterHiddenType(OutfitType hiddenType, Outfit hidingOutfit)
		{
			if (!(hiddenType == null) && !(hidingOutfit == null))
			{
				if (!hiddenTypes.TryGetValue(hiddenType, out var value))
				{
					value = new List<Outfit>();
					hiddenTypes.Add(hiddenType, value);
				}
				if (!value.Contains(hidingOutfit))
				{
					value.Add(hidingOutfit);
				}
				Outfit outfit = GetOutfit(hiddenType);
				if (outfit != null)
				{
					outfit.gameObject.SetActive(value: false);
				}
			}
		}

		public void RemoveHide(Outfit outfit)
		{
			if (outfit == null)
			{
				return;
			}
			foreach (OutfitType item in hiddenTypes.Keys.ToList())
			{
				List<Outfit> list = hiddenTypes[item];
				list.Remove(outfit);
				if (list.Count == 0)
				{
					hiddenTypes.Remove(item);
					Outfit outfit2 = GetOutfit(item);
					if (outfit2 != null && !outfit2.mergeMesh)
					{
						outfit2.gameObject.SetActive(value: true);
					}
				}
			}
		}

		private void ApplyTags()
		{
		}

		public void SetStance(float value)
		{
			AnimatorControllerParameter[] parameters = animator.parameters;
			for (int i = 0; i < parameters.Length; i++)
			{
				if (parameters[i].name == "Stance")
				{
					animator.SetFloat("Stance", value);
				}
			}
			stance = value;
		}

		public void AddHeightSource(GameObject source, float value)
		{
			heightSources[source] = value;
			SetHeight();
		}

		public void RemoveHeightSource(GameObject source, float value)
		{
			heightSources.Remove(source);
			SetHeight();
		}

		public void SetHeight()
		{
			if (muteHeightChange)
			{
				return;
			}
			base.transform.localPosition = new Vector3(base.transform.localPosition.x, base.transform.localPosition.y - height, base.transform.localPosition.z);
			height = 0f;
			foreach (KeyValuePair<GameObject, float> heightSource in heightSources)
			{
				height += heightSource.Value;
			}
			base.transform.localPosition = new Vector3(base.transform.localPosition.x, base.transform.localPosition.y + height, base.transform.localPosition.z);
		}

		public void MuteHeightChange()
		{
			MuteHeightChange(!muteHeightChange);
		}

		public void MuteHeightChange(bool value)
		{
			if (value != muteHeightChange)
			{
				if (muteHeightChange)
				{
					base.transform.localPosition = new Vector3(base.transform.localPosition.x, base.transform.localPosition.y - height, base.transform.localPosition.z);
				}
				SetHeight();
			}
		}

		public void MuteBodyMods()
		{
			mutebodyMods = !mutebodyMods;
			MuteBodyMods(mutebodyMods);
		}

		public void MuteBodyMods(bool mute)
		{
			foreach (BodyShapeModifier value in bodyModifiers.Values)
			{
				if (value != null)
				{
					value.SetMute(mute);
				}
			}
			mutebodyMods = mute;
		}

		public void ForceBindPose()
		{
			ForceBindPose(!isbindPose);
		}

		public void ForceBindPose(bool bindPose)
		{
			if (isbindPose && (bool)animator)
			{
				animator.enabled = false;
				RestoreBindPose();
			}
			else
			{
				animator.enabled = true;
			}
			isbindPose = bindPose;
		}

		private void MergeBones(Outfit outfit)
		{
			if (outfit.additionalBones.Length != 0)
			{
				for (int i = 0; i < outfit.additionalBones.Length; i++)
				{
					Transform transform = outfit.additionalBones[i];
					if (transform == null || transform.parent == null)
					{
						continue;
					}
					if (!GetBones().TryGetValue(transform.parent.name, out var value))
					{
						Debug.LogWarning("Missing parent bone '" + transform.parent.name + "' for additional bone '" + transform.name + "'");
						continue;
					}
					if (boneMap.TryGetValue(transform.name, out var value2) && value2 != null && value2 != transform)
					{
						UnityEngine.Object.Destroy(value2.gameObject);
						boneMap.Remove(transform.name);
					}
					transform.SetParent(value, worldPositionStays: false);
					Transform[] componentsInChildren = transform.GetComponentsInChildren<Transform>(includeInactive: true);
					foreach (Transform transform2 in componentsInChildren)
					{
						if (boneMap.TryGetValue(transform2.name, out var value3) && value3 != null && value3 != transform2)
						{
							UnityEngine.Object.Destroy(value3.gameObject);
						}
						boneMap[transform2.name] = transform2;
					}
				}
			}
			SkinnedMeshRenderer[] skinnedRenderers = outfit.skinnedRenderers;
			foreach (SkinnedMeshRenderer skinnedMeshRenderer in skinnedRenderers)
			{
				if (outfit.AttachPoint == "" && (bool)skinnedMeshRenderer)
				{
					Transform[] array = skinnedMeshRenderer.bones.ToArray();
					Transform[] array2 = new Transform[skinnedMeshRenderer.bones.Length];
					for (int k = 0; k < array.Length; k++)
					{
						Transform transform3 = array[k];
						if (!(transform3 == null))
						{
							boneMap.TryGetValue(transform3.name, out var value4);
							array2[k] = value4;
						}
					}
					skinnedMeshRenderer.bones = array2;
					if (outfit.mergeMesh)
					{
						skinnedMeshRenderer.rootBone = CharacterBody.rootBone;
					}
					else
					{
						skinnedMeshRenderer.rootBone = GetBones()[skinnedMeshRenderer.rootBone.name];
					}
				}
				else
				{
					try
					{
						_ = boneMap[outfit.AttachPoint];
					}
					catch
					{
						Debug.LogError(base.name + " is missing " + outfit.AttachPoint + " that " + outfit.name + " requires");
						return;
					}
				}
			}
			outfit.Initalized = true;
			if ((bool)outfit.outfitRenderer && outfit.AttachPoint != "")
			{
				Transform transform4 = null;
				try
				{
					transform4 = boneMap[outfit.AttachPoint];
				}
				catch
				{
					Debug.LogError(base.name + " is missing " + outfit.AttachPoint + " that " + outfit.name + " requires");
					return;
				}
				outfit.transform.parent = transform4.transform;
				outfit.transform.position = transform4.position;
				outfit.transform.rotation = transform4.rotation;
				outfit.transform.localScale = Vector3.one;
			}
		}

		public bool ContainsTag(string tag)
		{
			return tags.Contains(tag);
		}

		public Outfit GetOutfit(OutfitType outfitType)
		{
			if (Outfits.TryGetValue(outfitType.name, out var value))
			{
				return value;
			}
			return null;
		}

		public Outfit GetOutfit(string outfitType)
		{
			if (Outfits.TryGetValue(outfitType, out var value))
			{
				return value;
			}
			return null;
		}

		public List<Outfit> GetOutfits()
		{
			return new List<Outfit>(Outfits.Values);
		}

		public Dictionary<string, int> GetShapes()
		{
			return shapes;
		}

		public Dictionary<string, int> GetBodyShapes()
		{
			return bodyShapes;
		}

		public Dictionary<string, int> GetFaceShapes()
		{
			return faceShapes;
		}

		public ColliderComponent[] GetClothColliders()
		{
			return ClothColliders;
		}

		public float GetShape(string key)
		{
			if (bodyShapes.TryGetValue(key, out var value))
			{
				Outfit outfit = GetOutfit("Body");
				if (outfit != null)
				{
					return outfit.skinnedRenderer.GetBlendShapeWeight(value);
				}
				return -10000f;
			}
			return -10000f;
		}

		public Dictionary<string, BodyShapeModifier> GetMods()
		{
			return bodyModifiers;
		}

		public Dictionary<string, Transform> GetBones()
		{
			return boneMap;
		}

		public float GetShapeValue(string key)
		{
			float result = -1f;
			Outfit outfit = GetOutfit("Body");
			if (outfit == null)
			{
				return -1f;
			}
			int value2;
			if (bodyShapes.TryGetValue(key, out var value))
			{
				result = outfit.skinnedRenderer.GetBlendShapeWeight(value);
			}
			else if (faceShapes.TryGetValue(key, out value2))
			{
				Outfit outfit2 = GetOutfit("Head");
				if (outfit2 == null)
				{
					return -1f;
				}
				result = outfit2.skinnedRenderer.GetBlendShapeWeight(value2);
			}
			return result;
		}

		public float GetShapeValue(int key)
		{
			Outfit outfit = GetOutfit("Body");
			SkinnedMeshRenderer skinnedMeshRenderer = ((!(outfit == null)) ? outfit.skinnedRenderer : CharacterBody);
			return skinnedMeshRenderer.GetBlendShapeWeight(key);
		}

		public Dictionary<string, float> GetBodyShapeValues()
		{
			Dictionary<string, float> dictionary = new Dictionary<string, float>();
			int[] array = bodyShapes.Values.ToArray();
			string[] array2 = bodyShapes.Keys.ToArray();
			Outfit outfit = GetOutfit("Body");
			SkinnedMeshRenderer skinnedMeshRenderer = ((!(outfit == null)) ? outfit.skinnedRenderer : CharacterBody);
			for (int i = 0; i < array.Length; i++)
			{
				float blendShapeWeight = skinnedMeshRenderer.GetBlendShapeWeight(array[i]);
				dictionary.Add(array2[i], blendShapeWeight);
			}
			return dictionary;
		}

		public Dictionary<string, float> GetFaceShapeValues()
		{
			Dictionary<string, float> dictionary = new Dictionary<string, float>();
			int[] array = faceShapes.Values.ToArray();
			string[] array2 = faceShapes.Keys.ToArray();
			Outfit outfit = GetOutfit("Head");
			SkinnedMeshRenderer skinnedMeshRenderer = ((!(outfit == null)) ? outfit.skinnedRenderer : CharacterBody);
			for (int i = 0; i < array.Length; i++)
			{
				float blendShapeWeight = skinnedMeshRenderer.GetBlendShapeWeight(array[i]);
				dictionary.Add(array2[i], blendShapeWeight);
			}
			return dictionary;
		}

		public SkinnedMeshRenderer GetCharacterBody()
		{
			return CharacterBody;
		}

		public void SetCharacterBody(GameObject newBody)
		{
		}

		public void SetRenderTextures(List<RenderTexture> rt)
		{
			List<RenderTexture> list = ((rt == null) ? new List<RenderTexture>() : new List<RenderTexture>(rt));
			foreach (RenderTexture renderTexture in renderTextures)
			{
				if (!list.Contains(renderTexture))
				{
					ReleaseRenderTexture(renderTexture);
				}
			}
			renderTextures = list;
		}

		public void MergeCharacter(Outfit outfit)
		{
			MergeCharacter();
		}

		[ContextMenu("Merge")]
		public Task MergeCharacter()
		{
			return MergeCharacter(CancellationToken.None);
		}

		public async Task MergeCharacter(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (isStatic)
			{
				return;
			}
			if (isMerging || isloading)
			{
				mergeRequested = true;
				return;
			}
			mergeRequested = false;
			isMerging = true;
			try
			{
				await Task.Yield();
				cancellationToken.ThrowIfCancellationRequested();
				if (this == null)
				{
					return;
				}
				if (this == null)
				{
					isMerging = false;
					return;
				}
				if (Application.isPlaying && base.gameObject.scene.isLoaded)
				{
					AnimatorStateInfo currentAnimatorStateInfo = animator.GetCurrentAnimatorStateInfo(0);
					RuntimeAnimatorController runtimeAnimatorController = animator.runtimeAnimatorController;
					Avatar avatar = animator.avatar;
					float normalizedTime = currentAnimatorStateInfo.normalizedTime;
					int fullPathHash = currentAnimatorStateInfo.fullPathHash;
					bool flag = animator.enabled;
					animator.enabled = false;
					animator.runtimeAnimatorController = null;
					_Outfits.RemoveAll((Outfit x) => x == null);
					customMaps.Remove("MagicaClothExtension");
					try
					{
						BoZo_MagicaClothSupport[] componentsInChildren = GetComponentsInChildren<BoZo_MagicaClothSupport>();
						for (int num = 0; num < componentsInChildren.Length; num++)
						{
							componentsInChildren[num].DestroyCloth();
						}
					}
					catch
					{
						Debug.LogWarning("Magica related things has failed");
					}
					MuteBodyMods(mute: true);
					MuteHeightChange(value: true);
					AddTags();
					try
					{
						MergeTextures();
					}
					catch
					{
						Debug.LogError("Texture Merge has failed");
					}
					BoZo_CharacterOptimizer boZo_CharacterOptimizer = new BoZo_CharacterOptimizer();
					try
					{
						boZo_CharacterOptimizer.MergeButTheBetterOne(this);
					}
					catch
					{
						Debug.LogError("Character Merge Failed");
					}
					try
					{
						MuteBodyMods(mute: false);
						MuteHeightChange(value: false);
						InitBodyShapes();
						InitBodyMods();
						ApplyAllShapes();
					}
					catch
					{
						Debug.LogError("Body Shapes has failed");
					}
					animator.avatar = avatar;
					animator.runtimeAnimatorController = runtimeAnimatorController;
					animator.enabled = flag;
					animator.Rebind();
					if (flag)
					{
						animator.Play(fullPathHash, 0, normalizedTime % 1f);
						animator.Update(0f);
					}
					if (cleanAfterMerge)
					{
						RemoveAllOutfits();
						isStatic = true;
					}
					OnRigChanged?.Invoke(CharacterBody);
				}
				else
				{
					Debug.LogWarning("For stability reasons Character Merging is only available in Play Mode");
				}
			}
			finally
			{
				isMerging = false;
				if (cancellationToken.IsCancellationRequested)
				{
					mergeRequested = false;
				}
			}
			if (mergeRequested && !isloading && !isStatic)
			{
				mergeRequested = false;
				await MergeCharacter(cancellationToken);
			}
		}

		[ContextMenu("SaveToPrefab")]
		public async void SaveCharacterToPrefab()
		{
		}
	}
}
