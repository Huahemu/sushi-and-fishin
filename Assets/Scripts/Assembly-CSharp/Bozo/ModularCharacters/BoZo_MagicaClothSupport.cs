using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MagicaCloth2;
using UnityEngine;
using UnityEngine.Events;

namespace Bozo.ModularCharacters
{
	public class BoZo_MagicaClothSupport : MonoBehaviour, IOutfitExtension<Texture>, IOutfitExtension
	{
		public enum ClothType
		{
			Mesh = 0,
			Bone = 1,
			Spring = 2
		}

		public const string id = "MagicaClothExtension";

		private bool initalized;

		private int rebuildVersion;

		private OutfitSystem system;

		public ClothType type;

		public SkinnedMeshRenderer skinnedMeshRenderer;

		public string[] disableByTag;

		public bool InitalizeOnStart;

		[Header("Bones")]
		public bool boneReferenceByString;

		public List<Transform> rootBones;

		public List<string> rootBonesString;

		public float collisionSize = 0.025f;

		public AnimationCurve collisionCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 1f));

		[Header("Mesh")]
		public Texture2D influenceMap;

		private List<Texture2D> influenceMaps = new List<Texture2D>();

		[Range(0f, 0.2f)]
		public float reductionSetting = 0.065f;

		[Header("Preset")]
		public TextAsset clothPreset;

		public List<Transform> transforms = new List<Transform>();

		private MagicaCloth cloth;

		public Texture GetValue()
		{
			return influenceMap;
		}

		object IOutfitExtension.GetValue()
		{
			return influenceMap;
		}

		public Type GetValueType()
		{
			return typeof(Texture);
		}

		private void Awake()
		{
			MagicaCloth[] components = GetComponents<MagicaCloth>();
			foreach (MagicaCloth magicaCloth in components)
			{
				if (magicaCloth != cloth)
				{
					UnityEngine.Object.Destroy(magicaCloth);
				}
			}
		}

		private void OnEnable()
		{
			system = GetComponentInParent<OutfitSystem>();
			if ((bool)system)
			{
				OutfitSystem outfitSystem = system;
				outfitSystem.OnRigChanged = (UnityAction<SkinnedMeshRenderer>)Delegate.Combine(outfitSystem.OnRigChanged, new UnityAction<SkinnedMeshRenderer>(OnCharacterMerged));
			}
			if ((bool)system)
			{
				OutfitSystem outfitSystem2 = system;
				outfitSystem2.OnOutfitChanged = (UnityAction<Outfit>)Delegate.Combine(outfitSystem2.OnOutfitChanged, new UnityAction<Outfit>(DisableClothByTag));
			}
		}

		private void OnDisable()
		{
			rebuildVersion++;
			system = GetComponentInParent<OutfitSystem>();
			if ((bool)system)
			{
				OutfitSystem outfitSystem = system;
				outfitSystem.OnRigChanged = (UnityAction<SkinnedMeshRenderer>)Delegate.Remove(outfitSystem.OnRigChanged, new UnityAction<SkinnedMeshRenderer>(OnCharacterMerged));
			}
			if ((bool)system)
			{
				OutfitSystem outfitSystem2 = system;
				outfitSystem2.OnOutfitChanged = (UnityAction<Outfit>)Delegate.Remove(outfitSystem2.OnOutfitChanged, new UnityAction<Outfit>(DisableClothByTag));
			}
		}

		private void Start()
		{
			if (InitalizeOnStart)
			{
				system = GetComponentInParent<OutfitSystem>();
				Initalize(system, null);
				Execute(system, null);
			}
		}

		public void Initalize(OutfitSystem outfitSystem, Outfit outfit)
		{
			if (type == ClothType.Mesh && influenceMap == null)
			{
				return;
			}
			if (type == ClothType.Bone || type == ClothType.Spring)
			{
				if (boneReferenceByString)
				{
					if (rootBonesString == null || rootBonesString.Count == 0)
					{
						return;
					}
				}
				else if (rootBones == null || rootBones.Count == 0)
				{
					return;
				}
			}
			cloth = base.gameObject.AddComponent<MagicaCloth>();
			cloth.Initialize();
			cloth.DisableAutoBuild();
		}

		private async void OnCharacterMerged(SkinnedMeshRenderer rig)
		{
			int requestedVersion = ++rebuildVersion;
			try
			{
				influenceMaps.Clear();
				influenceMap = null;
				if ((bool)cloth)
				{
					UnityEngine.Object.Destroy(cloth);
					cloth = null;
				}
				await Task.Yield();
				if (this == null || requestedVersion != rebuildVersion || !base.isActiveAndEnabled)
				{
					return;
				}
				if (system == null)
				{
					system = GetComponentInParent<OutfitSystem>();
				}
				if (system == null)
				{
					return;
				}
				IEnumerable<Outfit> outfits = system.GetOutfits();
				if (outfits == null)
				{
					return;
				}
				foreach (Outfit item in outfits)
				{
					if (!(item == null) && (bool)item.MC2Map && (system.hiddenTypes == null || !system.hiddenTypes.ContainsKey(item.Type)))
					{
						influenceMap = item.MC2Map;
					}
				}
				initalized = false;
				Initalize(system, null);
				Execute(system, null);
			}
			catch (Exception ex)
			{
				string arg = ((system != null) ? system.name : "MissingOutfitSystem");
				string arg2 = ((rig != null) ? rig.name : "MissingRig");
				_ = ex.GetType().Name;
				Debug.LogException(new InvalidOperationException("Magica failed to initialize after character merge. " + $"OutfitSystem:{arg} Rig:{arg2} ClothType:{type}", ex), this);
			}
		}

		public void RebuildAfterBodyChanges()
		{
			if (system == null)
			{
				system = GetComponentInParent<OutfitSystem>();
			}
			if (!(system == null))
			{
				OnCharacterMerged(system.GetCharacterBody());
			}
		}

		public void DestroyCloth()
		{
			if ((bool)cloth)
			{
				UnityEngine.Object.Destroy(cloth);
			}
			influenceMap = null;
		}

		private void DisableClothByTag(Outfit outfit)
		{
			if (system == null || !initalized)
			{
				return;
			}
			bool flag = true;
			if (disableByTag != null)
			{
				string[] array = disableByTag;
				foreach (string text in array)
				{
					if (system.ContainsTag(text))
					{
						flag = false;
					}
				}
			}
			if ((bool)cloth)
			{
				cloth.enabled = flag;
			}
		}

		public void Execute(OutfitSystem outfitSystem, Outfit outfit)
		{
			_ = (bool)outfitSystem;
			if (!initalized && !(cloth == null))
			{
				system = outfitSystem;
				switch (type)
				{
				case ClothType.Mesh:
					SetMeshCloth(outfitSystem, outfit);
					break;
				case ClothType.Bone:
					SetBoneCloth(outfitSystem, outfit);
					break;
				case ClothType.Spring:
					SetBoneCloth(outfitSystem, outfit);
					break;
				}
				initalized = true;
				if ((bool)outfit)
				{
					DisableClothByTag(outfit);
				}
			}
		}

		private void SetMeshCloth(OutfitSystem outfitSystem, Outfit outfit)
		{
			if (!(influenceMap == null) && !(cloth == null))
			{
				ClothSerializeData serializeData = cloth.SerializeData;
				SkinnedMeshRenderer skinnedMeshRenderer = null;
				skinnedMeshRenderer = (this.skinnedMeshRenderer ? this.skinnedMeshRenderer : ((!outfit) ? outfitSystem.GetCharacterBody() : outfit.skinnedRenderer));
				if (skinnedMeshRenderer == null)
				{
					throw new InvalidOperationException("Magica mesh cloth has no source SkinnedMeshRenderer.");
				}
				serializeData.sourceRenderers.Add(skinnedMeshRenderer);
				serializeData.reductionSetting.shapeDistance = reductionSetting;
				serializeData.paintMode = ClothSerializeData.PaintMode.Texture_Fixed_Move;
				serializeData.paintMaps.Add(influenceMap);
				if ((bool)clothPreset)
				{
					cloth.SerializeData.ImportJson(clothPreset.ToString());
				}
				serializeData.radius = new CurveSerializeData(collisionSize, collisionCurve);
				ColliderComponent[] array = null;
				array = ((!outfitSystem) ? this.skinnedMeshRenderer.GetComponentsInChildren<ColliderComponent>() : outfitSystem.GetClothColliders());
				if (array != null)
				{
					serializeData.colliderCollisionConstraint.colliderList = array.ToList();
				}
				cloth.enabled = true;
				cloth.BuildAndRun();
			}
		}

		private void SetBoneCloth(OutfitSystem outfitSystem, Outfit outfit)
		{
			if (cloth == null)
			{
				return;
			}
			ClothSerializeData serializeData = cloth.SerializeData;
			if (type == ClothType.Bone)
			{
				serializeData.clothType = ClothProcess.ClothType.BoneCloth;
			}
			if (type == ClothType.Spring)
			{
				serializeData.clothType = ClothProcess.ClothType.BoneSpring;
			}
			SkinnedMeshRenderer skinnedMeshRenderer = null;
			skinnedMeshRenderer = (this.skinnedMeshRenderer ? this.skinnedMeshRenderer : ((!outfit) ? outfitSystem.GetCharacterBody() : outfit.skinnedRenderer));
			if (skinnedMeshRenderer == null)
			{
				throw new InvalidOperationException("Magica bone cloth has no source SkinnedMeshRenderer.");
			}
			if (rootBonesString != null && rootBonesString.Count != 0)
			{
				serializeData.rootBones = skinnedMeshRenderer.bones.Where((Transform bone) => rootBonesString.Contains(bone.name)).ToList();
			}
			else
			{
				serializeData.rootBones = rootBones ?? new List<Transform>();
			}
			if (serializeData.rootBones.Count == 0)
			{
				if ((bool)cloth)
				{
					UnityEngine.Object.Destroy(cloth);
				}
				return;
			}
			if ((bool)clothPreset)
			{
				cloth.SerializeData.ImportJson(clothPreset.ToString());
			}
			serializeData.radius = new CurveSerializeData(collisionSize, collisionCurve);
			ColliderComponent[] array = null;
			array = ((!outfitSystem) ? this.skinnedMeshRenderer.GetComponentsInChildren<ColliderComponent>() : outfitSystem.GetClothColliders());
			if (array != null)
			{
				serializeData.colliderCollisionConstraint.colliderList = array.ToList();
			}
			cloth.enabled = true;
			cloth.BuildAndRun();
		}

		public string GetID()
		{
			return "MagicaClothExtension";
		}
	}
}
