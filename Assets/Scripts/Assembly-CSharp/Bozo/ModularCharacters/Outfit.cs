using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;

namespace Bozo.ModularCharacters
{
	public class Outfit : OutfitBase
	{
		[SerializeField]
		private bool AttachInEditMode;

		private OutfitSystem system;

		public string OutfitName;

		public string PackName;

		public Sprite OutfitIcon;

		public string[] ColorChannels = new string[1] { "Base" };

		public string TextureCatagory;

		[Tooltip("Merges Texture into a atlas. Turn this off if your texture doesn't align with the atlas or you want a unique shader")]
		public bool mergeTexture = true;

		[Tooltip("Merges Mesh. Turn this off if you want this to be a seperate mesh")]
		public bool mergeMesh = true;

		public bool supportDecals;

		public bool supportPatterns;

		public bool showCharacterCreator = true;

		[SerializeField]
		public OutfitType Type;

		public string AttachPoint;

		[Range(-1f, 11f)]
		public int materialIndex = -1;

		[Range(0f, 100f)]
		public int materialPriority;

		public Color[] colors = new Color[9];

		public Color[] defaultColors;

		public Texture pattern;

		public Color[] patternColors = new Color[3];

		public Vector2 patternSize;

		private DecalBaker decalBaker;

		public List<DecalData> decalDatas;

		public string[] tags;

		[Tooltip("CharacterCreator Only: Prevents Outfit from showing up in creator unless the character has these tags")]
		public string[] requiredTags;

		public string[] categories;

		public GameObject[] optionalPieces;

		public Transform[] additionalBones;

		private Dictionary<string, int> tagShapes = new Dictionary<string, int>();

		private Dictionary<string, int> shapes = new Dictionary<string, int>();

		public LinkedColorSets[] LinkedColorSets;

		public OutfitType[] IncompatibleSets;

		public OutfitType[] HideTypes;

		public int currentSwatch;

		public List<OutfitSwatch> outfitSwatches = new List<OutfitSwatch>();

		public Transform[] originalBones;

		public Transform originalRootBone;

		public Transform editorAttachPoint;

		public bool customShader;

		public Material material;

		public Renderer controlMaterial;

		public int controlIndex;

		public Dictionary<string, Material> editMaterials = new Dictionary<string, Material>();

		private MaterialPropertyBlock block;

		public Texture2D MC2Map;

		public UnityAction<Outfit> OnColorChanged;

		public UnityAction<Outfit> OnOutfitChanged;

		public bool attached;

		public bool Initalized { get; set; }

		public SkinnedMeshRenderer skinnedRenderer
		{
			get
			{
				if (skinnedRenderers.Length != 0)
				{
					return skinnedRenderers[0];
				}
				return null;
			}
		}

		public SkinnedMeshRenderer[] skinnedRenderers { get; private set; }

		public Renderer outfitRenderer { get; private set; }

		private void OnValidate()
		{
			if (Application.isPlaying && base.gameObject.scene.isLoaded && base.gameObject.activeSelf)
			{
				if (system == null)
				{
					system = GetComponentInParent<OutfitSystem>();
				}
				SetColorInital();
			}
		}

		private void Awake()
		{
			outfitRenderer = GetComponentInChildren<Renderer>();
			skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true);
			if ((bool)outfitRenderer)
			{
				material = outfitRenderer.sharedMaterial;
			}
			block = new MaterialPropertyBlock();
			SkinnedMeshRenderer[] array = skinnedRenderers;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].sharedMaterial = material;
			}
			if ((bool)material)
			{
				for (int j = 1; j < 10; j++)
				{
					colors[j - 1] = material.GetColor("_Color_" + j);
				}
			}
			InitSetUpShapes();
		}

		private void OnDestroy()
		{
			if ((bool)system)
			{
				OutfitSystem outfitSystem = system;
				outfitSystem.OnOutfitChanged = (UnityAction<Outfit>)Delegate.Remove(outfitSystem.OnOutfitChanged, new UnityAction<Outfit>(OnOutfitUpdate));
				OutfitSystem outfitSystem2 = system;
				outfitSystem2.OnShapeChanged = (UnityAction<string, float>)Delegate.Remove(outfitSystem2.OnShapeChanged, new UnityAction<string, float>(SetShape));
			}
		}

		private void Start()
		{
			if (!Initalized)
			{
				Attach();
			}
			SetColorInital();
		}

		public void Attach(Transform parent)
		{
			base.transform.parent = parent;
			Attach();
		}

		public void Attach(OutfitSystem system)
		{
			base.transform.parent = system.transform;
			Attach();
		}

		public void Attach(bool force = false)
		{
			if (attached)
			{
				return;
			}
			system = GetComponentInParent<OutfitSystem>();
			outfitRenderer = GetComponentInChildren<Renderer>();
			if (!(system == null) && system.initalized)
			{
				InitDecals();
				CopySystemShapes();
				OutfitSystem outfitSystem = system;
				outfitSystem.OnOutfitChanged = (UnityAction<Outfit>)Delegate.Combine(outfitSystem.OnOutfitChanged, new UnityAction<Outfit>(OnOutfitUpdate));
				OutfitSystem outfitSystem2 = system;
				outfitSystem2.OnShapeChanged = (UnityAction<string, float>)Delegate.Combine(outfitSystem2.OnShapeChanged, new UnityAction<string, float>(SetShape));
				IOutfitExtension[] componentsInChildren = GetComponentsInChildren<IOutfitExtension>();
				IOutfitExtension[] array = componentsInChildren;
				for (int i = 0; i < array.Length; i++)
				{
					array[i].Initalize(system, this);
				}
				if (originalBones.Length != 0 && (bool)skinnedRenderer)
				{
					skinnedRenderer.bones = originalBones;
					skinnedRenderer.rootBone = originalRootBone;
				}
				system.AttachOutfit(this);
				CheckTags();
				array = componentsInChildren;
				for (int i = 0; i < array.Length; i++)
				{
					array[i].Execute(system, this);
				}
				attached = true;
			}
		}

		public void ReturnBones()
		{
			Transform[] array = additionalBones;
			foreach (Transform transform in array)
			{
				if (!(transform == null))
				{
					transform.parent = base.transform;
				}
			}
		}

		private void OnOutfitUpdate(Outfit newOutfit)
		{
			CheckTags();
		}

		private void CheckTags()
		{
			List<string> list = new List<string>(tagShapes.Keys);
			SkinnedMeshRenderer[] array = skinnedRenderers;
			foreach (SkinnedMeshRenderer skinnedMeshRenderer in array)
			{
				for (int j = 0; j < list.Count; j++)
				{
					if (system.ContainsTag(list[j]))
					{
						skinnedMeshRenderer.SetBlendShapeWeight(tagShapes[list[j]], 100f);
					}
					else
					{
						skinnedMeshRenderer.SetBlendShapeWeight(tagShapes[list[j]], 0f);
					}
				}
			}
		}

		private void CopySystemShapes()
		{
			if (!system)
			{
				return;
			}
			string[] array = shapes.Keys.ToArray();
			for (int i = 0; i < array.Length; i++)
			{
				float shape = system.GetShape(array[i]);
				if (shape != -10000f)
				{
					SetShape(array[i], shape);
				}
			}
		}

		private void InitSetUpShapes()
		{
			if (!skinnedRenderer)
			{
				return;
			}
			Mesh sharedMesh = skinnedRenderer.sharedMesh;
			int blendShapeCount = skinnedRenderer.sharedMesh.blendShapeCount;
			for (int i = 0; i < blendShapeCount; i++)
			{
				string text = sharedMesh.GetBlendShapeName(i);
				int num = text.IndexOf('.');
				if (num >= 0)
				{
					text = text.Substring(num + 1);
				}
				if (text.IndexOf('_') >= 0)
				{
					if (string.CompareOrdinal(text, 0, "Tag_", 0, 4) == 0)
					{
						tagShapes.Add(text.Substring(4), i);
					}
					else if (string.CompareOrdinal(text, 0, "Shape_", 0, 6) == 0)
					{
						shapes.Add(text.Substring(6), i);
					}
				}
			}
		}

		public void SetShape(string key, float value)
		{
			if (value <= -10000f || !skinnedRenderer)
			{
				return;
			}
			string[] array = key.Split(".");
			if (array.Length > 1)
			{
				key = array[1];
			}
			if (!shapes.TryGetValue(key, out var value2))
			{
				return;
			}
			SkinnedMeshRenderer[] array2 = skinnedRenderers;
			foreach (SkinnedMeshRenderer skinnedMeshRenderer in array2)
			{
				if (skinnedMeshRenderer.sharedMesh.blendShapeCount > value2)
				{
					skinnedMeshRenderer.SetBlendShapeWeight(value2, value);
				}
			}
		}

		public void SetPartActive(int index, bool active)
		{
			if (index >= 0 || index < optionalPieces.Length)
			{
				optionalPieces[index].SetActive(active);
				OnOutfitChanged?.Invoke(this);
			}
		}

		public void SetControlMaterial(Renderer mat, int index)
		{
			controlMaterial = mat;
			controlIndex = index;
		}

		public void SetMaterial(Material mat)
		{
			if ((bool)outfitRenderer)
			{
				outfitRenderer.sharedMaterial = mat;
			}
			SkinnedMeshRenderer[] array = skinnedRenderers;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].sharedMaterial = mat;
			}
		}

		public void InitDecals()
		{
			if (supportDecals)
			{
				base.gameObject.AddComponent<DecalBaker>();
				decalBaker = GetComponent<DecalBaker>();
			}
		}

		private void SetColorInital()
		{
			if ((bool)outfitRenderer)
			{
				for (int i = 0; i < defaultColors.Length; i++)
				{
					SetColor(defaultColors[i], i + 1);
				}
			}
		}

		public void SetColor(Color color, int channel, bool linkedChanged = false)
		{
			if (channel < 1 || channel > colors.Length)
			{
				return;
			}
			colors[channel - 1] = color;
			if (system == null)
			{
				system = GetComponentInParent<OutfitSystem>();
			}
			if (outfitRenderer == null)
			{
				outfitRenderer = GetComponentInChildren<Renderer>();
			}
			MaterialPropertyBlock materialPropertyBlock = SelectBlock();
			try
			{
				if (customShader)
				{
					SetColor(color);
				}
				else
				{
					materialPropertyBlock.SetColor("_Color_" + channel, color);
				}
			}
			catch
			{
				Debug.LogError(base.name + " Failed to load colors. Is it missing its material or renderer?");
			}
			LinkedColorSets[] linkedColorSets = LinkedColorSets;
			foreach (LinkedColorSets linkedColorSets2 in linkedColorSets)
			{
				if (!linkedChanged)
				{
					Outfit outfit = system.GetOutfit(linkedColorSets2.linkedType);
					if (!(outfit == null) && channel <= linkedColorSets2.linkedChannelRange)
					{
						outfit.SetColor(color, channel, linkedChanged: true);
					}
				}
			}
			UpdateMaterialBlock();
			OnColorChanged?.Invoke(this);
		}

		public virtual void SetColor(Color color)
		{
			colors[0] = color;
			MaterialPropertyBlock materialPropertyBlock = SelectBlock();
			if (materialPropertyBlock.HasProperty("_Color_1"))
			{
				materialPropertyBlock.SetColor("_Color_1", color);
			}
			else if (materialPropertyBlock.HasProperty("_Color"))
			{
				materialPropertyBlock.SetColor("_Color", color);
			}
			else if (materialPropertyBlock.HasProperty("_MainColor"))
			{
				materialPropertyBlock.SetColor("_MainColor", color);
			}
			UpdateMaterialBlock();
		}

		public virtual List<Color> GetColors()
		{
			return colors.ToList();
		}

		public virtual Color GetColor(int channel)
		{
			if (channel - 1 < 0 || channel - 1 > colors.Length)
			{
				return colors[0];
			}
			return colors[channel - 1];
		}

		public void ApplyDecals(RenderTexture rt, RenderTexture crt)
		{
			if (!decalBaker)
			{
				return;
			}
			foreach (DecalData decalData in decalDatas)
			{
				decalBaker.BakeDecal(rt, crt, decalData);
			}
		}

		public void AddDecal()
		{
			DecalData item = new DecalData("", "spine_04", new Vector3(0f, 0f, -0.05f), 0f, 0f, 0f, 0.15f, Color.white);
			decalDatas.Add(item);
			OnColorChanged?.Invoke(this);
		}

		public void RemoveDecal()
		{
			decalDatas.RemoveAt(decalDatas.Count - 1);
			OnColorChanged?.Invoke(this);
		}

		public virtual bool GetDecal(int index, out DecalData data)
		{
			if (index >= 0 && index < decalDatas.Count)
			{
				data = decalDatas[index];
				return true;
			}
			data = default;
			return false;
		}

		public virtual void SetDecal(DecalData data, int index)
		{
			if (index >= 0 && index < decalDatas.Count)
			{
				decalDatas[index] = data;
				OnColorChanged?.Invoke(this);
			}
		}

		public virtual void SetDecalSize(Vector4 size)
		{
			MaterialPropertyBlock materialPropertyBlock = SelectBlock();
			if (materialPropertyBlock.HasProperty("_DecalScale"))
			{
				materialPropertyBlock.SetVector("_DecalScale", size);
				UpdateMaterialBlock();
			}
		}

		public virtual Vector4 GetDecalSize()
		{
			Material material = SelectMaterial();
			if (!material.HasProperty("_DecalScale"))
			{
				return new Vector4(0f, 0f, 0f, 0f);
			}
			Vector4 vector = material.GetVector("_DecalScale");
			return new Vector4(vector.x, vector.y, 0f, 0f);
		}

		public virtual void SetDecalColor(Color color, int index)
		{
			SelectBlock().SetColor("_DecalColor_" + index, color);
			UpdateMaterialBlock();
		}

		public virtual Color GetDecalColor(int index)
		{
			return SelectMaterial().GetColor("_DecalColor_" + index);
		}

		public virtual List<Color> GetDecalColors()
		{
			Material material = SelectMaterial();
			List<Color> list = new List<Color>();
			for (int i = 1; i < 4; i++)
			{
				list.Add(material.GetColor("_DecalColor_" + i));
			}
			return list;
		}

		public virtual void SetPattern(Texture texture)
		{
			pattern = texture;
			OnColorChanged?.Invoke(this);
		}

		public virtual void SetPattern(Texture texture, Color[] colors)
		{
			pattern = texture;
			for (int i = 0; i < colors.Length && i < this.colors.Length; i++)
			{
				this.colors[i] = colors[i];
			}
			OnColorChanged?.Invoke(this);
		}

		public virtual Texture GetPattern()
		{
			return pattern;
		}

		public virtual void SetPatternColor(Color color, int index)
		{
			if (index < colors.Length)
			{
				colors[index] = color;
			}
		}

		public virtual Color GetPatternColor(int index)
		{
			if (index >= colors.Length)
			{
				return colors[colors.Length - 1];
			}
			return colors[index];
		}

		public virtual List<Color> GetPatternColors()
		{
			return colors.ToList();
		}

		public virtual void SetPatternSize(Vector2 size)
		{
			patternSize = size;
			OnColorChanged?.Invoke(this);
		}

		public virtual Vector4 GetPatternSize()
		{
			Material material = SelectMaterial();
			if (!material.HasProperty("_PatternScale"))
			{
				return new Vector4(0f, 0f, 0f, 0f);
			}
			Vector4 vector = material.GetVector("_PatternScale");
			return new Vector4(vector.x, vector.y, 0f, 0f);
		}

		public virtual void SetBaseTexture(Texture texture, Texture normalTexture = null)
		{
		}

		protected MaterialPropertyBlock SelectBlock()
		{
			if (block == null)
			{
				block = new MaterialPropertyBlock();
			}
			if ((bool)controlMaterial)
			{
				controlMaterial.GetPropertyBlock(block, controlIndex);
			}
			else if ((bool)outfitRenderer)
			{
				outfitRenderer.GetPropertyBlock(block);
			}
			return block;
		}

		protected Material SelectMaterial()
		{
			Material result = null;
			if ((bool)controlMaterial)
			{
				result = controlMaterial.sharedMaterials[controlIndex];
			}
			else if ((bool)outfitRenderer)
			{
				result = outfitRenderer.sharedMaterial;
			}
			else if ((bool)material)
			{
				result = material;
			}
			return result;
		}

		public void UpdateMaterialBlock()
		{
			if ((bool)controlMaterial)
			{
				controlMaterial.SetPropertyBlock(block, controlIndex);
			}
			else if ((bool)outfitRenderer)
			{
				outfitRenderer.SetPropertyBlock(block);
				SkinnedMeshRenderer[] array = skinnedRenderers;
				for (int i = 0; i < array.Length; i++)
				{
					array[i].SetPropertyBlock(block, controlIndex);
				}
			}
		}

		public OutfitData GetOutfitData()
		{
			OutfitData result = default;
			string text = Type.name + "/" + base.name;
			text = text.Replace("(Clone)", "");
			result.outfit = text;
			if (customShader)
			{
				result.color = GetColor(1);
				result.swatch = currentSwatch;
			}
			else
			{
				result.colors = GetColors();
				Texture texture = GetPattern();
				if (texture != null)
				{
					result.pattern = "Pattern/" + texture.name;
					result.patternColors = GetPatternColors();
					result.patternScale = GetPatternSize();
				}
				else
				{
					result.pattern = "";
				}
			}
			bool[] array = new bool[optionalPieces.Length];
			for (int i = 0; i < optionalPieces.Length; i++)
			{
				if (!(optionalPieces[i] == null))
				{
					array[i] = optionalPieces[i].activeSelf;
				}
			}
			result.partVisibility = array;
			result.decalDatas = decalDatas;
			return result;
		}

		[ContextMenu("QuickName")]
		private void QuickName()
		{
			int num = base.name.IndexOf('_');
			string outfitName = Regex.Replace((num >= 0) ? base.name.Substring(num + 1) : base.name, "(?<!^)([A-Z])", " $1");
			OutfitName = outfitName;
		}
	}
}
