using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.Player.Visuals;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Bozo.ModularCharacters
{
	public class CharacterCreator : MonoBehaviour
	{
		private enum CharacterFamily
		{
			Stylized = 0,
			Anime = 1
		}

		private enum WardrobeMood
		{
			Everyday = 0,
			Tonal = 1,
			Soft = 2,
			Bold = 3,
			Dark = 4
		}

		private readonly struct WardrobePalette
		{
			public readonly Color Main;

			public readonly Color Secondary;

			public readonly Color Accent;

			public readonly Color NeutralLight;

			public readonly Color NeutralDark;

			public readonly Color Metal;

			public WardrobePalette(Color main, Color secondary, Color accent, Color neutralLight, Color neutralDark, Color metal)
			{
				Main = main;
				Secondary = secondary;
				Accent = accent;
				NeutralLight = neutralLight;
				NeutralDark = neutralDark;
				Metal = metal;
			}
		}

		private struct AppearancePalette
		{
			public WardrobePalette Wardrobe;

			public int WardrobeScheme;

			public Color Iris;

			public Color Hair;

			public Color HairAccent;
		}

		private struct HumanBodyProfile
		{
			public bool FeminineFrame;

			public float Weight;

			public float Muscle;

			public float Curvy;

			public float Chest;
		}

		private const string StylizedFamilyTag = "Stylized";

		private const string AnimeFamilyTag = "Anime";

		private const string StylizedMaterial = "BSMC_StylizedShader";

		private const string AnimeMaterial = "BSMC_AnimeShader";

		private const string ExcludedRandomHead = "Head_Stern";

		private const string IncompatiblePartsResetKey = "customization.incompatible_parts_reset";

		[Header("Creator Dependencies")]
		public OutfitSystem character;

		public DecalController decalController;

		[SerializeField]
		private Camera iconCamera;

		[SerializeField]
		private RenderTexture iconTexture;

		public OutfitType[] outfitTypes;

		[Header("Outfit Dependencies")]
		private Dictionary<string, Outfit> OutfitDataBase = new Dictionary<string, Outfit>();

		[SerializeField]
		private OutfitSelector outfitSelectorObject;

		private List<OutfitSelector> outfitSelectors = new List<OutfitSelector>();

		[SerializeField]
		private Transform outfitContainer;

		[SerializeField]
		private OutfitVisibilityToggler visibilityToggler;

		[Header("Texture Dependencies")]
		[SerializeField]
		private TextureSelector textureSelectorObject;

		private List<TextureSelector> textureSelectors = new List<TextureSelector>();

		[SerializeField]
		private Transform decalContainer;

		[SerializeField]
		private Transform patternContainer;

		[Header("BodyShape Dependencies")]
		[SerializeField]
		private BlendSlider blendSliderObject;

		private Dictionary<string, BlendSlider> blendSliders = new Dictionary<string, BlendSlider>();

		private Dictionary<string, BlendSlider> faceBlendSliders = new Dictionary<string, BlendSlider>();

		[SerializeField]
		private BodyShapeSliders modSliderObject;

		private List<BodyShapeSliders> ModSliders = new List<BodyShapeSliders>();

		[SerializeField]
		private Transform bodyShapeContainer;

		[SerializeField]
		private Transform bodyModContainer;

		[SerializeField]
		private Transform faceShapeContainer;

		[SerializeField]
		private Transform faceModContainer;

		[SerializeField]
		private string catagory;

		[SerializeField]
		private GameObject currentPage;

		private GameObject previousPage;

		private Dictionary<string, List<GameObject>> outfits = new Dictionary<string, List<GameObject>>();

		private List<TexturePackage> textures = new List<TexturePackage>();

		[Header("UserSettings")]
		[SerializeField]
		private GameObject RemoveButton;

		[SerializeField]
		private string[] DisableRemoveByType;

		[Header("Save Dependencies")]
		[SerializeField]
		private SaveSelector saveSelector;

		[SerializeField]
		private Dictionary<string, SaveSelector> saveSlots = new Dictionary<string, SaveSelector>();

		[SerializeField]
		private Transform saveContainer;

		[SerializeField]
		private GameObject DeleteConfirmWindow;

		[SerializeField]
		private TMP_Text loadedCharacterNameText;

		[SerializeField]
		private TMP_Text DeleteCharacterNameText;

		public UnityAction<string> onCategoryChanged;

		public UnityAction<GameObject> onCharacterChanged;

		public OutfitType type;

		[Header("Save Options")]
		public TMP_InputField CharacterName;

		[Header("Debug Options")]
		public string ShowOnlyPack;

		[Header("Command Options")]
		[SerializeField]
		private CharacterObject defaultCharacterPreset;

		[SerializeField]
		private int maxHistoryStates = 5;

		[SerializeField]
		private float continuousEditCaptureInterval = 0.5f;

		[SerializeField]
		private BaseButton _randomButton;

		[SerializeField]
		private BaseButton _resetButton;

		[SerializeField]
		private BaseButton _undoButton;

		[SerializeField]
		private BaseButton _redoButton;

		[SerializeField]
		private BaseButton _backButton;

		private readonly List<CharacterData> undoStates = new List<CharacterData>();

		private readonly List<CharacterData> redoStates = new List<CharacterData>();

		private bool isApplyingCommand;

		private bool commandButtonsBound;

		private float lastContinuousEditRecordTime = -999f;

		private static readonly Color[] NaturalIrisColors = new Color[6]
		{
			new Color32(62, 39, 24, byte.MaxValue),
			new Color32(99, 69, 34, byte.MaxValue),
			new Color32(112, 91, 45, byte.MaxValue),
			new Color32(64, 91, 70, byte.MaxValue),
			new Color32(87, 105, 110, byte.MaxValue),
			new Color32(61, 91, 124, byte.MaxValue)
		};

		private static readonly Color[] NaturalHairColors = new Color[11]
		{
			new Color32(24, 21, 20, byte.MaxValue),
			new Color32(55, 36, 27, byte.MaxValue),
			new Color32(91, 61, 42, byte.MaxValue),
			new Color32(119, 70, 43, byte.MaxValue),
			new Color32(136, 63, 42, byte.MaxValue),
			new Color32(167, 52, 43, byte.MaxValue),
			new Color32(190, 81, 39, byte.MaxValue),
			new Color32(210, 119, 47, byte.MaxValue),
			new Color32(181, 147, 94, byte.MaxValue),
			new Color32(199, 181, 142, byte.MaxValue),
			new Color32(139, 135, 128, byte.MaxValue)
		};

		private static readonly Color[] HairAccentColors = new Color[8]
		{
			new Color32(230, 164, 61, byte.MaxValue),
			new Color32(198, 67, 60, byte.MaxValue),
			new Color32(205, 82, 132, byte.MaxValue),
			new Color32(126, 78, 168, byte.MaxValue),
			new Color32(64, 112, 183, byte.MaxValue),
			new Color32(42, 145, 137, byte.MaxValue),
			new Color32(226, 205, 154, byte.MaxValue),
			new Color32(67, 57, 53, byte.MaxValue)
		};

		private static readonly WardrobePalette[] WardrobePalettes = new WardrobePalette[12]
		{
			new WardrobePalette(new Color32(42, 73, 91, byte.MaxValue), new Color32(184, 199, 191, byte.MaxValue), new Color32(173, 82, 58, byte.MaxValue), new Color32(235, 230, 216, byte.MaxValue), new Color32(47, 51, 54, byte.MaxValue), new Color32(184, 149, 73, byte.MaxValue)),
			new WardrobePalette(new Color32(67, 91, 72, byte.MaxValue), new Color32(153, 166, 132, byte.MaxValue), new Color32(126, 55, 68, byte.MaxValue), new Color32(232, 228, 213, byte.MaxValue), new Color32(48, 55, 49, byte.MaxValue), new Color32(171, 143, 82, byte.MaxValue)),
			new WardrobePalette(new Color32(119, 50, 67, byte.MaxValue), new Color32(185, 112, 119, byte.MaxValue), new Color32(50, 79, 102, byte.MaxValue), new Color32(238, 231, 218, byte.MaxValue), new Color32(47, 48, 53, byte.MaxValue), new Color32(192, 156, 78, byte.MaxValue)),
			new WardrobePalette(new Color32(42, 111, 105, byte.MaxValue), new Color32(112, 151, 145, byte.MaxValue), new Color32(197, 151, 55, byte.MaxValue), new Color32(232, 232, 219, byte.MaxValue), new Color32(45, 56, 61, byte.MaxValue), new Color32(176, 181, 179, byte.MaxValue)),
			new WardrobePalette(new Color32(61, 94, 132, byte.MaxValue), new Color32(142, 165, 184, byte.MaxValue), new Color32(171, 55, 54, byte.MaxValue), new Color32(236, 230, 211, byte.MaxValue), new Color32(45, 51, 60, byte.MaxValue), new Color32(186, 149, 75, byte.MaxValue)),
			new WardrobePalette(new Color32(91, 71, 107, byte.MaxValue), new Color32(158, 139, 166, byte.MaxValue), new Color32(72, 123, 112, byte.MaxValue), new Color32(235, 229, 218, byte.MaxValue), new Color32(50, 48, 57, byte.MaxValue), new Color32(180, 181, 184, byte.MaxValue)),
			new WardrobePalette(new Color32(145, 91, 45, byte.MaxValue), new Color32(184, 144, 91, byte.MaxValue), new Color32(49, 93, 98, byte.MaxValue), new Color32(238, 230, 209, byte.MaxValue), new Color32(55, 49, 43, byte.MaxValue), new Color32(181, 145, 69, byte.MaxValue)),
			new WardrobePalette(new Color32(76, 83, 91, byte.MaxValue), new Color32(151, 159, 164, byte.MaxValue), new Color32(49, 128, 139, byte.MaxValue), new Color32(235, 233, 225, byte.MaxValue), new Color32(39, 43, 47, byte.MaxValue), new Color32(178, 181, 180, byte.MaxValue)),
			new WardrobePalette(new Color32(45, 78, 166, byte.MaxValue), new Color32(235, 115, 91, byte.MaxValue), new Color32(224, 178, 58, byte.MaxValue), new Color32(239, 234, 219, byte.MaxValue), new Color32(39, 46, 63, byte.MaxValue), new Color32(188, 153, 73, byte.MaxValue)),
			new WardrobePalette(new Color32(36, 118, 91, byte.MaxValue), new Color32(215, 121, 145, byte.MaxValue), new Color32(235, 188, 72, byte.MaxValue), new Color32(240, 233, 216, byte.MaxValue), new Color32(42, 56, 51, byte.MaxValue), new Color32(190, 156, 76, byte.MaxValue)),
			new WardrobePalette(new Color32(117, 42, 54, byte.MaxValue), new Color32(39, 123, 130, byte.MaxValue), new Color32(212, 153, 51, byte.MaxValue), new Color32(238, 231, 214, byte.MaxValue), new Color32(48, 43, 47, byte.MaxValue), new Color32(187, 148, 70, byte.MaxValue)),
			new WardrobePalette(new Color32(38, 42, 48, byte.MaxValue), new Color32(216, 216, 207, byte.MaxValue), new Color32(180, 48, 58, byte.MaxValue), new Color32(241, 238, 227, byte.MaxValue), new Color32(27, 30, 34, byte.MaxValue), new Color32(177, 181, 183, byte.MaxValue))
		};

		public event Action BackRequested;

		private void Awake()
		{
			outfits.Clear();
			OutfitDataBase.Clear();
			List<Outfit> list = Resources.LoadAll<Outfit>("").ToList();
			TexturePackage[] array = Resources.LoadAll<TexturePackage>("");
			list.Sort((Outfit a, Outfit b) => string.Compare(a.PackName, b.PackName, StringComparison.OrdinalIgnoreCase));
			foreach (Outfit item in list)
			{
				if (item.showCharacterCreator)
				{
					if (OutfitDataBase.ContainsKey(item.name))
					{
						Debug.LogWarning("Outfit: " + item.name + " has already been added, you may have a duplicate outfit in your project");
					}
					else
					{
						OutfitDataBase.Add(item.name, item.GetComponent<Outfit>());
					}
				}
			}
			TexturePackage[] array2 = array;
			foreach (TexturePackage texturePackage in array2)
			{
				textures.Add(texturePackage.GetComponent<TexturePackage>());
			}
			GenerateOutfitSelection();
			GenerateTextureSelection();
		}

		private void OnEnable()
		{
			LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
			LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
			if (character != null)
			{
				OutfitSystem outfitSystem = character;
				outfitSystem.OnOutfitChanged = (UnityAction<Outfit>)Delegate.Combine(outfitSystem.OnOutfitChanged, new UnityAction<Outfit>(OnOutfitUpdate));
				OutfitSystem outfitSystem2 = character;
				outfitSystem2.OnRigChanged = (UnityAction<SkinnedMeshRenderer>)Delegate.Combine(outfitSystem2.OnRigChanged, new UnityAction<SkinnedMeshRenderer>(OnRigUpdate));
			}
			BindCommandButtons();
		}

		private void OnDisable()
		{
			LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
			if (character != null)
			{
				OutfitSystem outfitSystem = character;
				outfitSystem.OnOutfitChanged = (UnityAction<Outfit>)Delegate.Remove(outfitSystem.OnOutfitChanged, new UnityAction<Outfit>(OnOutfitUpdate));
				OutfitSystem outfitSystem2 = character;
				outfitSystem2.OnRigChanged = (UnityAction<SkinnedMeshRenderer>)Delegate.Remove(outfitSystem2.OnRigChanged, new UnityAction<SkinnedMeshRenderer>(OnRigUpdate));
			}
			UnbindCommandButtons();
		}

		private void OnSelectedLocaleChanged(Locale _)
		{
			foreach (BlendSlider value in blendSliders.Values)
			{
				value?.RefreshLocalization();
			}
			foreach (BlendSlider value2 in faceBlendSliders.Values)
			{
				value2?.RefreshLocalization();
			}
			foreach (BodyShapeSliders modSlider in ModSliders)
			{
				modSlider?.RefreshLocalization();
			}
		}

		public async void Start()
		{
			await RepairMismatchedFamilyAsync();
			GetBodyBlends();
			GetBodyMods();
			if (character != null)
			{
				onCharacterChanged?.Invoke(character.gameObject);
			}
			SwitchCatagory("Top");
			UpdateCharacterSaves();
		}

		public void GenerateOutfitSelection()
		{
			if (!(outfitSelectorObject == null) && !(outfitContainer == null))
			{
				Outfit[] array = OutfitDataBase.Values.ToArray();
				foreach (Outfit outfit in array)
				{
					OutfitSelector outfitSelector = UnityEngine.Object.Instantiate(outfitSelectorObject, outfitContainer);
					outfitSelector.Init(outfit, this);
					outfitSelectors.Add(outfitSelector);
				}
			}
		}

		public void GenerateTextureSelection()
		{
			if (textureSelectorObject == null)
			{
				return;
			}
			foreach (TexturePackage texture in textures)
			{
				Transform transform = null;
				if (texture.type == TextureType.Decal)
				{
					transform = decalContainer;
				}
				if (texture.type == TextureType.Pattern)
				{
					transform = patternContainer;
				}
				if (!(transform == null))
				{
					TextureSelector textureSelector = UnityEngine.Object.Instantiate(textureSelectorObject, transform);
					textureSelector.Init(texture, this);
					textureSelectors.Add(textureSelector);
				}
			}
		}

		public void GetBodyBlends()
		{
			if (blendSliderObject == null || bodyModContainer == null)
			{
				return;
			}
			Dictionary<string, int> shapes = character.GetShapes();
			Dictionary<string, int> bodyShapes = character.GetBodyShapes();
			Dictionary<string, int> faceShapes = character.GetFaceShapes();
			foreach (BlendSlider value in faceBlendSliders.Values)
			{
				value.gameObject.SetActive(value: false);
			}
			foreach (BlendSlider value2 in blendSliders.Values)
			{
				value2.gameObject.SetActive(value: false);
			}
			foreach (KeyValuePair<string, int> item in shapes)
			{
				if (bodyShapes.ContainsKey(item.Key))
				{
					if (blendSliders.ContainsKey(item.Key))
					{
						blendSliders[item.Key].gameObject.SetActive(value: true);
						blendSliders[item.Key].RefreshValue();
					}
					else
					{
						BlendSlider blendSlider = UnityEngine.Object.Instantiate(blendSliderObject, bodyShapeContainer);
						blendSlider.Init(character, item.Key);
						blendSliders.Add(item.Key, blendSlider);
					}
				}
				else if (faceShapes.ContainsKey(item.Key))
				{
					if (faceBlendSliders.ContainsKey(item.Key))
					{
						faceBlendSliders[item.Key].gameObject.SetActive(value: true);
						faceBlendSliders[item.Key].RefreshValue();
					}
					else
					{
						BlendSlider blendSlider2 = UnityEngine.Object.Instantiate(blendSliderObject, faceShapeContainer);
						blendSlider2.Init(character, item.Key);
						faceBlendSliders.Add(item.Key, blendSlider2);
					}
				}
			}
		}

		public void GetBodyMods()
		{
			if (modSliderObject == null)
			{
				return;
			}
			List<BodyShapeModifier> list = character.GetMods().Values.ToList();
			for (int i = 0; i < ModSliders.Count; i++)
			{
				if (ModSliders[i] != null)
				{
					UnityEngine.Object.Destroy(ModSliders[i].gameObject);
				}
			}
			ModSliders.Clear();
			Transform transform = bodyShapeContainer;
			foreach (BodyShapeModifier item in list)
			{
				transform = ((!(item.sorting == "Head")) ? bodyModContainer : faceModContainer);
				if (!(transform == null))
				{
					BodyShapeSliders bodyShapeSliders = UnityEngine.Object.Instantiate(modSliderObject, transform);
					bodyShapeSliders.Init(character, item);
					ModSliders.Add(bodyShapeSliders);
				}
			}
		}

		public void OpenPage(GameObject page)
		{
			if (!(page == null))
			{
				if (currentPage != null)
				{
					currentPage.SetActive(value: false);
				}
				previousPage = currentPage;
				currentPage = page;
				currentPage.SetActive(value: true);
				if (IsDataPageActive() && TryGetComponent<ColorPickerControl>(out var component))
				{
					component.RemoveObject();
				}
				else
				{
					RefreshCustomizationControls();
				}
			}
		}

		public void BackPage()
		{
			currentPage.SetActive(value: false);
			currentPage = previousPage;
			currentPage.SetActive(value: true);
			SwitchCatagory("");
		}

		public async void SetOutfit(Outfit outfit)
		{
			if (outfit == null || outfit.Type == null)
			{
				return;
			}
			if (string.Equals(outfit.Type.name, "Head", StringComparison.Ordinal))
			{
				await SetHeadOutfitSafelyAsync(outfit);
				return;
			}
			if (TryGetCurrentCharacterFamily(out var family) && !IsOutfitCompatibleWithFamily(outfit, family))
			{
				ShowCustomizationInfo("This outfit doesn't match the face.");
				SwitchCatagory(outfit.Type.name);
				return;
			}
			RecordUndoState();
			bool flag = character.GetOutfit("Overall") != null;
			if (flag && outfit.Type.name == "Top")
			{
				character.RemoveOutfit("Bottom", merge: false);
				character.RemoveOutfit("Overall", merge: false);
			}
			else if (flag && outfit.Type.name == "Bottom")
			{
				character.RemoveOutfit("Top", merge: false);
				character.RemoveOutfit("Overall", merge: false);
			}
			UnityEngine.Object.Instantiate(outfit, character.transform);
			await Task.Yield();
			SwitchCatagory(outfit.Type.name);
			type = outfit.Type;
		}

		public void OnOutfitUpdate(Outfit outfit)
		{
			if (outfit != null && outfit.Type != null && (outfit.Type.name == "Head" || outfit.Type.name == "Body"))
			{
				GetBodyBlends();
				GetBodyMods();
			}
			RefreshCustomizationControls();
		}

		public void OnRigUpdate(SkinnedMeshRenderer rig)
		{
			GetBodyBlends();
			GetBodyMods();
			RefreshCustomizationControls();
		}

		public void RemoveOutfit()
		{
			if (!(type == null) && !DisableRemoveByType.Contains(type.name))
			{
				if (character == null || character.GetOutfit(type.name) == null)
				{
					type = null;
					return;
				}
				RecordUndoState();
				character.RemoveOutfit(type);
				type = null;
			}
		}

		public void SwitchCatagory(string catagory)
		{
			if (IsDataPageActive())
			{
				this.catagory = string.Empty;
				if (TryGetComponent<ColorPickerControl>(out var component))
				{
					component.RemoveObject();
				}
				return;
			}
			foreach (OutfitSelector outfitSelector in outfitSelectors)
			{
				bool flag = outfitSelector.outfit.requiredTags == null || outfitSelector.outfit.requiredTags.All(character.ContainsTag);
				bool flag2 = outfitSelector.outfit.Type != null && string.Equals(outfitSelector.outfit.Type.name, "Head", StringComparison.Ordinal);
				bool flag3 = flag2 || !TryGetCurrentCharacterFamily(out var family) || IsOutfitCompatibleWithFamily(outfitSelector.outfit, family);
				bool flag4 = string.IsNullOrEmpty(ShowOnlyPack) || ShowOnlyPack.Contains(outfitSelector.outfit.PackName);
				outfitSelector.SetVisable(((flag2 | flag) & flag3 & flag4) ? catagory : string.Empty);
			}
			Outfit outfit = character.GetOutfit(catagory);
			this.catagory = catagory;
			if (RemoveButton != null)
			{
				RemoveButton.SetActive(value: true);
			}
			if ((bool)visibilityToggler)
			{
				visibilityToggler.Set(outfit, this);
			}
			if (outfit == null)
			{
				type = null;
				SwitchTextureCatagory(string.Empty);
				onCategoryChanged?.Invoke(catagory);
				RefreshCustomizationControls();
			}
			else
			{
				SwitchTextureCatagory(outfit.TextureCatagory);
				type = outfit.Type;
				onCategoryChanged?.Invoke(catagory);
				RefreshCustomizationControls();
			}
		}

		private async Task SetHeadOutfitSafelyAsync(Outfit head)
		{
			if (isApplyingCommand || head == null || head.Type == null)
			{
				return;
			}
			CharacterData characterData = CaptureCharacterData();
			if (characterData == null)
			{
				UtilityMethods.Log("[CharacterCreator] Head selection failed because the current character could not be captured.", LogType.Warning);
				return;
			}
			CharacterData characterData2 = characterData;
			bool changedFamily = TryGetOutfitFamily(head, out var family) && TryGetCharacterFamily(characterData, out var family2) && family != family2;
			if (changedFamily)
			{
				characterData2 = CreateFamilyTransitionData(characterData, family, out var _);
				if (characterData2 == null)
				{
					ShowCustomizationInfo("This character style could not be loaded. Your current character was kept.");
					return;
				}
			}
			OutfitData selectedHead;
			if (TryGetOutfitData(characterData2, "Head", out var outfitData))
			{
				OutfitData outfitData2 = new OutfitData(outfitData);
				outfitData2.outfit = "Head/" + head.name;
				outfitData2.partVisibility = CreateVisiblePartState(head);
				selectedHead = outfitData2;
			}
			else
			{
				selectedHead = CreateRandomOutfitData(head);
				selectedHead.partVisibility = CreateVisiblePartState(head);
			}
			PreserveHeadSkinColors(characterData, characterData2, ref selectedHead);
			UpsertOutfitData(characterData2.outfitDatas, selectedHead);
			RecordUndoState();
			redoStates.Clear();
			await ApplyCharacterDataAsync(characterData2);
			type = head.Type;
			SwitchCatagory(head.Type.name);
			if (changedFamily)
			{
				ShowCustomizationInfo(LocalizationDatabase.GetString("GameStateTable", "customization.incompatible_parts_reset", "Face and outfit don't match. Incompatible parts reset. You can Undo."));
			}
		}

		private async Task RepairMismatchedFamilyAsync()
		{
			if (isApplyingCommand)
			{
				return;
			}
			CharacterData characterData = CaptureCharacterData();
			if (characterData != null && TryGetOutfitData(characterData, "Head", out var outfitData) && TryGetOutfitData(characterData, "Body", out var outfitData2) && TryGetOutfitDefinition(outfitData.outfit, out var outfit) && TryGetOutfitDefinition(outfitData2.outfit, out var outfit2) && TryGetOutfitFamily(outfit, out var family) && TryGetOutfitFamily(outfit2, out var family2) && family != family2)
			{
				CharacterData characterData2 = CreateFamilyTransitionData(characterData, family, out var adjustedPartCount);
				if (characterData2 != null)
				{
					OutfitData outfitData3 = new OutfitData(outfitData);
					outfitData3.outfit = "Head/" + outfit.name;
					outfitData3.partVisibility = CreateVisiblePartState(outfit);
					OutfitData selectedHead = outfitData3;
					PreserveHeadSkinColors(characterData, characterData2, ref selectedHead);
					UpsertOutfitData(characterData2.outfitDatas, selectedHead);
					await ApplyCharacterDataAsync(characterData2);
					ShowCustomizationInfo($"An incompatible character setup was repaired. {adjustedPartCount} part(s) were adjusted.");
				}
			}
		}

		private CharacterData CreateFamilyTransitionData(CharacterData source, CharacterFamily targetFamily, out int adjustedPartCount)
		{
			adjustedPartCount = 0;
			CharacterData defaultCharacterData = GetDefaultCharacterData(targetFamily);
			if (source == null || defaultCharacterData == null)
			{
				return null;
			}
			defaultCharacterData.characterName = source.characterName;
			defaultCharacterData.stance = source.stance;
			defaultCharacterData.material = ((targetFamily == CharacterFamily.Anime) ? "BSMC_AnimeShader" : "BSMC_StylizedShader");
			CopyMatchingValues(source.bodyIDs, source.bodyShapes, defaultCharacterData.bodyIDs, defaultCharacterData.bodyShapes);
			CopyMatchingValues(source.faceIDs, source.faceShapes, defaultCharacterData.faceIDs, defaultCharacterData.faceShapes);
			CopyMatchingBodyModifiers(source, defaultCharacterData);
			if (source.outfitDatas == null)
			{
				return defaultCharacterData;
			}
			foreach (OutfitData outfitData2 in source.outfitDatas)
			{
				string outfitTypePrefix = GetOutfitTypePrefix(outfitData2.outfit);
				if (!string.Equals(outfitTypePrefix, "Head", StringComparison.Ordinal))
				{
					Outfit outfit;
					Outfit equivalent;
					if (string.Equals(outfitTypePrefix, "Body", StringComparison.Ordinal))
					{
						adjustedPartCount++;
					}
					else if (TryGetOutfitDefinition(outfitData2.outfit, out outfit) && IsOutfitCompatibleWithFamily(outfit, targetFamily))
					{
						UpsertOutfitData(defaultCharacterData.outfitDatas, new OutfitData(outfitData2));
					}
					else if (outfit != null && TryFindEquivalentOutfit(outfit, targetFamily, out equivalent))
					{
						OutfitData outfitData = new OutfitData(outfitData2);
						outfitData.outfit = equivalent.Type.name + "/" + equivalent.name;
						outfitData.partVisibility = CreateVisiblePartState(equivalent);
						OutfitData newData = outfitData;
						UpsertOutfitData(defaultCharacterData.outfitDatas, newData);
						adjustedPartCount++;
					}
					else
					{
						adjustedPartCount++;
					}
				}
			}
			return defaultCharacterData;
		}

		private bool TryFindEquivalentOutfit(Outfit source, CharacterFamily targetFamily, out Outfit equivalent)
		{
			equivalent = null;
			if (source == null || source.Type == null)
			{
				return false;
			}
			HashSet<string> outfitSemanticKeys = GetOutfitSemanticKeys(source);
			if (outfitSemanticKeys.Count == 0)
			{
				return false;
			}
			foreach (Outfit value in OutfitDataBase.Values)
			{
				if (!(value == null) && !(value.Type == null) && string.Equals(value.Type.name, source.Type.name, StringComparison.Ordinal) && IsOutfitCompatibleWithFamily(value, targetFamily))
				{
					HashSet<string> outfitSemanticKeys2 = GetOutfitSemanticKeys(value);
					if (outfitSemanticKeys.Overlaps(outfitSemanticKeys2))
					{
						equivalent = value;
						return true;
					}
				}
			}
			return false;
		}

		private static HashSet<string> GetOutfitSemanticKeys(Outfit outfit)
		{
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			if (outfit == null)
			{
				return hashSet;
			}
			AddSemanticKey(hashSet, outfit.OutfitName, null);
			AddSemanticKey(hashSet, outfit.name, (outfit.Type != null) ? outfit.Type.name : null);
			return hashSet;
		}

		private static void AddSemanticKey(HashSet<string> keys, string value, string typeName)
		{
			if (!string.IsNullOrWhiteSpace(value))
			{
				string[] array = (from token in Regex.Split(Regex.Replace(value, "([a-z0-9])([A-Z])", "$1 $2").ToLowerInvariant(), "[^a-z0-9]+")
					where !string.IsNullOrWhiteSpace(token) && !string.Equals(token, "bsmc", StringComparison.Ordinal) && !string.Equals(token, "bmsc", StringComparison.Ordinal) && !string.Equals(token, "outfit", StringComparison.Ordinal) && !string.Equals(token, "anime", StringComparison.Ordinal) && !string.Equals(token, typeName, StringComparison.OrdinalIgnoreCase)
					select token).OrderBy((string token) => token, StringComparer.Ordinal).ToArray();
				if (array.Length != 0)
				{
					keys.Add(string.Join("|", array));
				}
			}
		}

		private static void CopyMatchingValues(List<string> sourceIds, List<float> sourceValues, List<string> targetIds, List<float> targetValues)
		{
			if (sourceIds == null || sourceValues == null || targetIds == null || targetValues == null)
			{
				return;
			}
			int num = Mathf.Min(sourceIds.Count, sourceValues.Count);
			for (int i = 0; i < num; i++)
			{
				int num2 = targetIds.IndexOf(sourceIds[i]);
				if (num2 >= 0 && num2 < targetValues.Count)
				{
					targetValues[num2] = sourceValues[i];
				}
			}
		}

		private static void CopyMatchingBodyModifiers(CharacterData source, CharacterData target)
		{
			if (source.bodyModsKeys == null || source.bodyMods == null || target.bodyModsKeys == null || target.bodyMods == null)
			{
				return;
			}
			int num = Mathf.Min(source.bodyModsKeys.Count, source.bodyMods.Count);
			for (int i = 0; i < num; i++)
			{
				int num2 = target.bodyModsKeys.IndexOf(source.bodyModsKeys[i]);
				if (num2 >= 0 && num2 < target.bodyMods.Count)
				{
					target.bodyMods[num2] = source.bodyMods[i];
				}
			}
		}

		private bool TryGetCurrentCharacterFamily(out CharacterFamily family)
		{
			family = CharacterFamily.Stylized;
			if (character == null)
			{
				return false;
			}
			if (TryGetOutfitFamily(character.GetOutfit("Body"), out family))
			{
				return true;
			}
			return TryGetOutfitFamily(character.GetOutfit("Head"), out family);
		}

		private bool TryGetCharacterFamily(CharacterData data, out CharacterFamily family)
		{
			family = CharacterFamily.Stylized;
			if (TryGetOutfitData(data, "Body", out var outfitData) && TryGetOutfitDefinition(outfitData.outfit, out var outfit) && TryGetOutfitFamily(outfit, out family))
			{
				return true;
			}
			if (TryGetOutfitData(data, "Head", out var outfitData2) && TryGetOutfitDefinition(outfitData2.outfit, out var outfit2) && TryGetOutfitFamily(outfit2, out family))
			{
				return true;
			}
			if (string.Equals(data?.material, "BSMC_AnimeShader", StringComparison.OrdinalIgnoreCase))
			{
				family = CharacterFamily.Anime;
				return true;
			}
			return data != null;
		}

		private static bool TryGetOutfitFamily(Outfit outfit, out CharacterFamily family)
		{
			family = CharacterFamily.Stylized;
			if (outfit == null)
			{
				return false;
			}
			if (HasFamilyMarker(outfit.tags, "Anime") || HasFamilyMarker(outfit.requiredTags, "Anime") || string.Equals(outfit.PackName, "Anime", StringComparison.OrdinalIgnoreCase) || (outfit.Type != null && string.Equals(outfit.Type.name, "Body", StringComparison.Ordinal) && outfit.name.IndexOf("Anime", StringComparison.OrdinalIgnoreCase) >= 0))
			{
				family = CharacterFamily.Anime;
				return true;
			}
			if (HasFamilyMarker(outfit.tags, "Stylized") || HasFamilyMarker(outfit.requiredTags, "Stylized") || !string.IsNullOrWhiteSpace(outfit.PackName) || (outfit.Type != null && string.Equals(outfit.Type.name, "Body", StringComparison.Ordinal)))
			{
				family = CharacterFamily.Stylized;
				return true;
			}
			return false;
		}

		private static bool HasFamilyMarker(string[] markers, string family)
		{
			return markers?.Any((string marker) => string.Equals(marker, family, StringComparison.OrdinalIgnoreCase)) ?? false;
		}

		private static bool IsOutfitCompatibleWithFamily(Outfit outfit, CharacterFamily family)
		{
			if (outfit == null)
			{
				return false;
			}
			if (!TryGetOutfitFamily(outfit, out var family2))
			{
				return true;
			}
			return family2 == family;
		}

		private static CharacterFamily GetOppositeFamily(CharacterFamily family)
		{
			if (family != CharacterFamily.Anime)
			{
				return CharacterFamily.Anime;
			}
			return CharacterFamily.Stylized;
		}

		private static string GetFamilyDisplayName(CharacterFamily family)
		{
			if (family != CharacterFamily.Anime)
			{
				return "Stylized";
			}
			return "Anime";
		}

		private static void ShowCustomizationInfo(string message)
		{
			if (Singleton<NotifyManager>.Instance != null)
			{
				Singleton<NotifyManager>.Instance.ShowToast(message, NotificationState.Info, 4f);
			}
			else
			{
				Debug.Log("[CharacterCreator] " + message);
			}
		}

		private static bool[] CreateVisiblePartState(Outfit outfit)
		{
			if (outfit == null || outfit.optionalPieces == null || outfit.optionalPieces.Length == 0)
			{
				return null;
			}
			bool[] array = new bool[outfit.optionalPieces.Length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = true;
			}
			return array;
		}

		private static void PreserveHeadSkinColors(CharacterData source, CharacterData target, ref OutfitData selectedHead)
		{
			if (!TryGetOutfitData(source, "Head", out var outfitData) || outfitData.colors == null || outfitData.colors.Count == 0)
			{
				return;
			}
			ref List<Color> colors = ref selectedHead.colors;
			if (colors == null)
			{
				colors = new List<Color>();
			}
			EnsureColorCount(selectedHead.colors, 2);
			selectedHead.colors[0] = outfitData.colors[0];
			selectedHead.colors[1] = ((outfitData.colors.Count > 1) ? outfitData.colors[1] : outfitData.colors[0]);
			if (!TryGetOutfitData(target, "Body", out var outfitData2))
			{
				return;
			}
			if (outfitData2.colors == null)
			{
				outfitData2.colors = new List<Color>();
			}
			EnsureColorCount(outfitData2.colors, 2);
			outfitData2.colors[0] = selectedHead.colors[0];
			outfitData2.colors[1] = selectedHead.colors[1];
			for (int i = 0; i < target.outfitDatas.Count; i++)
			{
				if (string.Equals(GetOutfitTypePrefix(target.outfitDatas[i].outfit), "Body", StringComparison.Ordinal))
				{
					target.outfitDatas[i] = outfitData2;
					break;
				}
			}
		}

		private bool IsDataPageActive()
		{
			if (currentPage != null)
			{
				return string.Equals(currentPage.name, "Page_SaveLoad", StringComparison.Ordinal);
			}
			return false;
		}

		public void SwitchTextureCatagory(string catagory)
		{
			foreach (TextureSelector textureSelector in textureSelectors)
			{
				textureSelector.SetVisable(catagory);
			}
		}

		public void RefreshCustomizationControls()
		{
			if (character == null)
			{
				return;
			}
			foreach (BlendSlider value in blendSliders.Values)
			{
				value?.RefreshValue();
			}
			foreach (BlendSlider value2 in faceBlendSliders.Values)
			{
				value2?.RefreshValue();
			}
			foreach (BodyShapeSliders modSlider in ModSliders)
			{
				modSlider?.RefreshValues();
			}
			Outfit outfit = (string.IsNullOrEmpty(catagory) ? null : character.GetOutfit(catagory));
			foreach (OutfitSelector outfitSelector in outfitSelectors)
			{
				outfitSelector?.SetSelected(IsSameOutfit(outfitSelector.outfit, outfit));
			}
			decalController?.RefreshFromCharacter();
			Texture currentDecal = ((decalController != null) ? decalController.decal : null);
			foreach (TextureSelector textureSelector in textureSelectors)
			{
				textureSelector?.RefreshSelection(outfit, currentDecal);
			}
			if (visibilityToggler != null)
			{
				visibilityToggler.Set(outfit, this);
			}
		}

		private static bool IsSameOutfit(Outfit selectorOutfit, Outfit currentOutfit)
		{
			if (selectorOutfit == null || currentOutfit == null)
			{
				return false;
			}
			if ((object)selectorOutfit == currentOutfit)
			{
				return true;
			}
			string a = selectorOutfit.name.Replace("(Clone)", string.Empty).Trim();
			string b = currentOutfit.name.Replace("(Clone)", string.Empty).Trim();
			if (!string.Equals(a, b, StringComparison.Ordinal))
			{
				if (!string.IsNullOrEmpty(selectorOutfit.OutfitName))
				{
					return string.Equals(selectorOutfit.OutfitName, currentOutfit.OutfitName, StringComparison.Ordinal);
				}
				return false;
			}
			return true;
		}

		public void ReplaceCharacter(OutfitSystem character)
		{
			this.character = character;
		}

		public string GetCurrentCatagory()
		{
			return catagory;
		}

		public void ToggleWalk(bool value)
		{
			character.animator.SetBool("isWalk", value);
		}

		public void SaveCharacter()
		{
			StartCoroutine(Save());
		}

		public void RecordUndoState()
		{
			if (!isApplyingCommand)
			{
				RecordUndoStateInternal(clearRedo: true);
			}
		}

		public void RecordContinuousUndoState()
		{
			if (!isApplyingCommand)
			{
				float unscaledTime = Time.unscaledTime;
				if (!(unscaledTime - lastContinuousEditRecordTime < continuousEditCaptureInterval))
				{
					lastContinuousEditRecordTime = unscaledTime;
					RecordUndoStateInternal(clearRedo: true);
				}
			}
		}

		public async void RandomizeCharacter()
		{
			await RandomizeCharacterAsync();
		}

		public async Task<CharacterData> RandomizeCharacterAsync()
		{
			if (isApplyingCommand)
			{
				return null;
			}
			CharacterData randomizedData = CreateRandomizedCharacterData();
			if (randomizedData == null)
			{
				UtilityMethods.Log("[CharacterCreator] Randomize failed because no source character data was available.", LogType.Warning);
				return null;
			}
			RecordUndoState();
			redoStates.Clear();
			int expression = ChooseRandomExpression();
			await ApplyCharacterDataAsync(randomizedData);
			ApplyExpression(expression);
			if (TryGetComponent<ColorPickerControl>(out var component))
			{
				component.RemoveObject();
			}
			return CaptureCharacterData() ?? randomizedData;
		}

		public async void ResetCharacter()
		{
			if (!isApplyingCommand)
			{
				CharacterData defaultCharacterData = GetDefaultCharacterData();
				if (defaultCharacterData == null)
				{
					UtilityMethods.Log("[CharacterCreator] Reset failed because defaultCharacterPreset is not assigned.", LogType.Warning);
					return;
				}
				RecordUndoState();
				redoStates.Clear();
				await ApplyCharacterDataAsync(defaultCharacterData);
			}
		}

		public async void Undo()
		{
			if (!isApplyingCommand && undoStates.Count != 0)
			{
				CharacterData characterData = CaptureCharacterData();
				if (characterData != null)
				{
					PushHistoryState(redoStates, characterData);
				}
				CharacterData data = PopHistoryState(undoStates);
				await ApplyCharacterDataAsync(data);
			}
		}

		public async void Redo()
		{
			if (!isApplyingCommand && redoStates.Count != 0)
			{
				CharacterData characterData = CaptureCharacterData();
				if (characterData != null)
				{
					PushHistoryState(undoStates, characterData);
				}
				CharacterData data = PopHistoryState(redoStates);
				await ApplyCharacterDataAsync(data);
			}
		}

		public void SetBackButtonVisible(bool visible)
		{
			if (_backButton != null)
			{
				_backButton.gameObject.SetActive(visible);
			}
		}

		public void RefreshAfterExternalCharacterLoad()
		{
			GetBodyBlends();
			GetBodyMods();
			if (character != null)
			{
				onCharacterChanged?.Invoke(character.gameObject);
			}
			if (!string.IsNullOrEmpty(catagory))
			{
				SwitchCatagory(catagory);
			}
			else
			{
				SwitchCatagory("Top");
			}
			RefreshCustomizationControls();
		}

		public Outfit GetOutfit(string outfitName)
		{
			return OutfitDataBase[outfitName];
		}

		private void BindCommandButtons()
		{
			if (!commandButtonsBound)
			{
				if (_randomButton != null)
				{
					_randomButton.Clicked += RandomizeCharacter;
				}
				if (_resetButton != null)
				{
					_resetButton.Clicked += ResetCharacter;
				}
				if (_undoButton != null)
				{
					_undoButton.Clicked += Undo;
				}
				if (_redoButton != null)
				{
					_redoButton.Clicked += Redo;
				}
				if (_backButton != null)
				{
					_backButton.Clicked += OnBackButtonClicked;
				}
				commandButtonsBound = true;
			}
		}

		private void UnbindCommandButtons()
		{
			if (commandButtonsBound)
			{
				if (_randomButton != null)
				{
					_randomButton.Clicked -= RandomizeCharacter;
				}
				if (_resetButton != null)
				{
					_resetButton.Clicked -= ResetCharacter;
				}
				if (_undoButton != null)
				{
					_undoButton.Clicked -= Undo;
				}
				if (_redoButton != null)
				{
					_redoButton.Clicked -= Redo;
				}
				if (_backButton != null)
				{
					_backButton.Clicked -= OnBackButtonClicked;
				}
				commandButtonsBound = false;
			}
		}

		private void OnBackButtonClicked()
		{
			BackRequested?.Invoke();
		}

		private void RecordUndoStateInternal(bool clearRedo)
		{
			CharacterData characterData = CaptureCharacterData();
			if (characterData != null && (undoStates.Count <= 0 || !AreCharacterDataEqual(undoStates[undoStates.Count - 1], characterData)))
			{
				PushHistoryState(undoStates, characterData);
				if (clearRedo)
				{
					redoStates.Clear();
				}
			}
		}

		private CharacterData CaptureCharacterData()
		{
			if (character == null || character.isStatic)
			{
				return null;
			}
			try
			{
				if (!character.initalized)
				{
					character.Init();
				}
				return PlayerCustomizationSession.CloneCharacterDataForRuntime(BMAC_SaveSystem.GetCharacterData(character));
			}
			catch (Exception ex)
			{
				UtilityMethods.Log("[CharacterCreator] Failed to capture character data: " + ex.Message, LogType.Warning);
				return null;
			}
		}

		private async Task ApplyCharacterDataAsync(CharacterData data)
		{
			if (character == null || data == null)
			{
				return;
			}
			isApplyingCommand = true;
			try
			{
				CharacterData characterObject = PlayerCustomizationSession.CloneCharacterDataForRuntime(data);
				if (!character.initalized)
				{
					character.Init();
				}
				await BMAC_SaveSystem.LoadCharacter(character, characterObject, manualShapeApply: false, character.async);
				RefreshAfterExternalCharacterLoad();
			}
			catch (Exception ex)
			{
				UtilityMethods.Log("[CharacterCreator] Failed to apply character command data: " + ex.Message, LogType.Warning);
			}
			finally
			{
				isApplyingCommand = false;
			}
		}

		private CharacterData GetDefaultCharacterData()
		{
			if (defaultCharacterPreset != null && defaultCharacterPreset.data != null)
			{
				return PlayerCustomizationSession.CloneCharacterDataForRuntime(defaultCharacterPreset.data);
			}
			CharacterObject characterObject = Resources.Load<CharacterObject>("Default");
			if (!(characterObject != null) || characterObject.data == null)
			{
				return null;
			}
			return PlayerCustomizationSession.CloneCharacterDataForRuntime(characterObject.data);
		}

		private CharacterData CreateRandomizedCharacterData()
		{
			CharacterFamily characterFamily = ((!(UnityEngine.Random.value < 0.5f)) ? CharacterFamily.Anime : CharacterFamily.Stylized);
			CharacterData characterData = GetDefaultCharacterData(characterFamily) ?? GetDefaultCharacterData() ?? CaptureCharacterData();
			if (characterData == null)
			{
				return null;
			}
			characterData.characterName = "Random";
			characterData.material = ((characterFamily == CharacterFamily.Anime) ? "BSMC_AnimeShader" : "BSMC_StylizedShader");
			HumanBodyProfile humanBodyProfile = RandomizeHumanBody(characterData);
			RandomizeSafeBodyModifiers(characterData, humanBodyProfile);
			RandomizeOutfits(characterData, humanBodyProfile, characterFamily);
			AppearancePalette appearance = CreateAppearancePalette();
			ApplyStyledOutfitColors(characterData.outfitDatas, appearance);
			ApplyHumanSkinPalette(characterData, characterFamily);
			ApplyNaturalEyeAndFaceColors(characterData, appearance);
			return characterData;
		}

		private CharacterData GetDefaultCharacterData(CharacterFamily family)
		{
			if (family == CharacterFamily.Stylized)
			{
				return GetDefaultCharacterData();
			}
			CharacterObject characterObject = Resources.Load<CharacterObject>("DefaultAnime") ?? Resources.Load<CharacterObject>("Anime/Default_Girl") ?? Resources.Load<CharacterObject>("Anime/Default_Boy");
			if (!(characterObject != null) || characterObject.data == null)
			{
				return null;
			}
			return PlayerCustomizationSession.CloneCharacterDataForRuntime(characterObject.data);
		}

		private static int ChooseRandomExpression()
		{
			if (UnityEngine.Random.value < 0.55f)
			{
				return 0;
			}
			float value = UnityEngine.Random.value;
			if (value < 0.3f)
			{
				return 1;
			}
			if (value < 0.48f)
			{
				return 6;
			}
			if (value < 0.63f)
			{
				return 4;
			}
			if (value < 0.77f)
			{
				return 3;
			}
			if (value < 0.89f)
			{
				return 2;
			}
			return 5;
		}

		private void ApplyExpression(int expression)
		{
			Animator animator = ((character != null) ? character.animator : null);
			if (animator != null)
			{
				animator.SetFloat("Expression", Mathf.Clamp(expression, 0, 6));
			}
			ExpressionSelect expressionSelect = GetComponentsInChildren<ExpressionSelect>(includeInactive: true).FirstOrDefault((ExpressionSelect item) => string.Equals(item.parameterID, "Expression", StringComparison.Ordinal));
			if (!(expressionSelect == null))
			{
				expressionSelect.outfitSystem = character;
				expressionSelect.animator = animator;
				Slider componentInChildren = expressionSelect.GetComponentInChildren<Slider>(includeInactive: true);
				if (componentInChildren != null)
				{
					componentInChildren.SetValueWithoutNotify(expression);
				}
			}
		}

		private HumanBodyProfile RandomizeHumanBody(CharacterData data)
		{
			bool flag = UnityEngine.Random.value < 0.5f;
			float value = (flag ? RandomCentered(0f, 35f) : RandomCentered(65f, 100f));
			bool num = UnityEngine.Random.value < 0.11f;
			bool flag2 = UnityEngine.Random.value < 0.1f;
			bool flag3 = num && UnityEngine.Random.value < 0.55f;
			bool flag4 = flag2 && UnityEngine.Random.value < 0.65f;
			float num2 = (num ? RandomCentered(42f, 67f) : RandomCentered(4f, 46f));
			float num3 = RandomCentered(flag ? 0f : 5f, flag ? 50f : 70f);
			float num4 = ((!flag) ? (flag2 ? RandomCentered(20f, 35f) : RandomCentered(0f, 22f)) : (flag2 ? RandomCentered(48f, 72f) : RandomCentered(8f, 45f)));
			float num5 = (flag3 ? Mathf.Clamp(18f + (num2 - 35f) * 0.7f - num3 * 0.08f + UnityEngine.Random.Range(0f, 12f), 18f, 62f) : Mathf.Clamp((num2 - 22f) * 0.35f - num3 * 0.1f + UnityEngine.Random.Range(-6f, 6f), 0f, 28f));
			float value2 = Mathf.Clamp(8f + num2 * 0.32f + num5 * 0.44f + num4 * 0.08f + UnityEngine.Random.Range(-4f, 4f), 5f, flag3 ? 72f : 58f);
			float value3 = (flag4 ? Mathf.Clamp(25f + num2 * 0.22f + num4 * 0.52f + UnityEngine.Random.Range(-3f, 8f), 30f, 78f) : Mathf.Clamp(6f + num2 * 0.16f + num4 * 0.28f + UnityEngine.Random.Range(-5f, 6f), 5f, 45f));
			float num6 = (flag ? Mathf.Clamp(18f + num4 * 0.35f + num2 * 0.2f + UnityEngine.Random.Range(-8f, 16f), 10f, 88f) : Mathf.Clamp(num3 * 0.35f + num2 * 0.12f + UnityEngine.Random.Range(0f, 16f), 0f, 55f));
			float value4 = Mathf.Clamp(8f + num3 * 0.55f + num2 * 0.18f + UnityEngine.Random.Range(-5f, 6f), 5f, 74f);
			SetShapeValue(data.bodyIDs, data.bodyShapes, "BodyType", value);
			SetShapeValue(data.bodyIDs, data.bodyShapes, "Chest", num6);
			SetShapeValue(data.bodyIDs, data.bodyShapes, "Weight", num2);
			SetShapeValue(data.bodyIDs, data.bodyShapes, "Belly", num5);
			SetShapeValue(data.bodyIDs, data.bodyShapes, "Muscle", num3);
			SetShapeValue(data.bodyIDs, data.bodyShapes, "Curvy", num4);
			SetShapeValue(data.bodyIDs, data.bodyShapes, "NeckThickness", value4);
			SetShapeValue(data.bodyIDs, data.bodyShapes, "WaistSize", value2);
			SetShapeValue(data.bodyIDs, data.bodyShapes, "ButtSize", value3);
			SetShapeValue(data.faceIDs, data.faceShapes, "BodyType", value);
			SetShapeValue(data.faceIDs, data.faceShapes, "Weight", num2);
			SetShapeValue(data.faceIDs, data.faceShapes, "Muscle", num3);
			SetShapeValue(data.faceIDs, data.faceShapes, "NeckThickness", value4);
			return new HumanBodyProfile
			{
				FeminineFrame = flag,
				Weight = num2,
				Muscle = num3,
				Curvy = num4,
				Chest = num6
			};
		}

		private static float RandomCentered(float minimum, float maximum)
		{
			float t = (UnityEngine.Random.value + UnityEngine.Random.value) * 0.5f;
			return Mathf.Lerp(minimum, maximum, t);
		}

		private static void SetShapeValue(List<string> ids, List<float> values, string id, float value)
		{
			if (ids != null && values != null)
			{
				int num = ids.IndexOf(id);
				if (num >= 0 && num < values.Count)
				{
					values[num] = Mathf.Clamp(value, 0f, 100f);
				}
			}
		}

		private void RandomizeSafeBodyModifiers(CharacterData data, HumanBodyProfile profile)
		{
			if (data.bodyModsKeys == null || data.bodyMods == null || character == null)
			{
				return;
			}
			Dictionary<string, BodyShapeModifier> mods = character.GetMods();
			int num = Mathf.Min(data.bodyModsKeys.Count, data.bodyMods.Count);
			for (int i = 0; i < num; i++)
			{
				if (!mods.TryGetValue(data.bodyModsKeys[i], out var value) || value == null)
				{
					continue;
				}
				float value2;
				switch (value.shapeName)
				{
				case "Height":
					value2 = RandomCentered(0.92f, 1.07f);
					break;
				case "Chest Size":
					value2 = Mathf.Clamp(0.95f + profile.Chest * 0.0014f, 0.95f, 1.08f);
					break;
				case "Shoulder Width":
					value2 = Mathf.Clamp(0.95f + profile.Muscle * 0.0018f + (profile.FeminineFrame ? 0f : 0.025f), 0.95f, 1.12f);
					break;
				case "Shoulder Size":
					value2 = Mathf.Clamp(0.92f + profile.Muscle * 0.0028f + profile.Weight * 0.0008f, 0.92f, 1.17f);
					break;
				case "UpperArm":
					value2 = Mathf.Clamp(0.9f + profile.Muscle * 0.0032f + profile.Weight * 0.0007f, 0.9f, 1.2f);
					break;
				case "Calf":
					value2 = Mathf.Clamp(0.92f + profile.Weight * 0.0014f + profile.Muscle * 0.0009f, 0.92f, 1.14f);
					break;
				case "Thigh":
					value2 = Mathf.Clamp(0.92f + profile.Weight * 0.0017f + profile.Curvy * 0.0013f, 0.92f, 1.18f);
					break;
				default:
					continue;
				}
				BodyModData bodyModData = data.bodyMods[i] ?? new BodyModData();
				value2 = Mathf.Clamp(value2, value.scaleRange.x, value.scaleRange.y);
				if (value.linkScaleAxis)
				{
					bodyModData.scaleValue = value2;
				}
				else
				{
					Vector3 scale = ((bodyModData.scale == Vector3.zero) ? Vector3.one : bodyModData.scale);
					if (value.useXScale)
					{
						scale.x = value2;
					}
					if (value.useYScale)
					{
						scale.y = value2;
					}
					if (value.useZScale)
					{
						scale.z = value2;
					}
					bodyModData.scale = scale;
				}
				data.bodyMods[i] = bodyModData;
			}
		}

		private void ApplyHumanSkinPalette(CharacterData data, CharacterFamily family)
		{
			if (data?.outfitDatas == null)
			{
				return;
			}
			CharacterObject[] array = Resources.LoadAll<CharacterObject>((family == CharacterFamily.Anime) ? "Anime" : "Base");
			List<CharacterData> list = new List<CharacterData>();
			for (int i = 0; i < array.Length; i++)
			{
				CharacterData characterData = ((array[i] != null) ? array[i].data : null);
				if (TryGetOutfitData(characterData, "Head", out var outfitData) && outfitData.colors != null && outfitData.colors.Count > 0 && IsHumanSkinColor(outfitData.colors[0]))
				{
					list.Add(characterData);
				}
			}
			if (list.Count == 0 || !TryGetOutfitData(list[UnityEngine.Random.Range(0, list.Count)], "Head", out var outfitData2))
			{
				return;
			}
			for (int j = 0; j < data.outfitDatas.Count; j++)
			{
				OutfitData value = data.outfitDatas[j];
				string outfitTypePrefix = GetOutfitTypePrefix(value.outfit);
				if (string.Equals(outfitTypePrefix, "Head", StringComparison.Ordinal))
				{
					value.colors = ((outfitData2.colors != null) ? new List<Color>(outfitData2.colors) : value.colors);
					data.outfitDatas[j] = value;
				}
				else if (string.Equals(outfitTypePrefix, "Body", StringComparison.Ordinal))
				{
					ref List<Color> colors = ref value.colors;
					if (colors == null)
					{
						colors = new List<Color>();
					}
					EnsureColorCount(value.colors, 2);
					value.colors[0] = outfitData2.colors[0];
					value.colors[1] = ((outfitData2.colors.Count > 1) ? outfitData2.colors[1] : outfitData2.colors[0]);
					data.outfitDatas[j] = value;
				}
			}
		}

		private static bool TryGetOutfitData(CharacterData data, string type, out OutfitData outfitData)
		{
			outfitData = default;
			if (data?.outfitDatas == null)
			{
				return false;
			}
			for (int i = 0; i < data.outfitDatas.Count; i++)
			{
				OutfitData outfitData2 = data.outfitDatas[i];
				if (string.Equals(GetOutfitTypePrefix(outfitData2.outfit), type, StringComparison.Ordinal))
				{
					outfitData = outfitData2;
					return true;
				}
			}
			return false;
		}

		private static bool IsHumanSkinColor(Color color)
		{
			Color.RGBToHSV(color, out var H, out var S, out var V);
			if ((H <= 0.12f || H >= 0.97f) && S <= 0.85f)
			{
				return V >= 0.12f;
			}
			return false;
		}

		private static void EnsureColorCount(List<Color> colors, int count)
		{
			while (colors.Count < count)
			{
				colors.Add(Color.white);
			}
		}

		private void RandomizeOutfits(CharacterData data, HumanBodyProfile bodyProfile, CharacterFamily family)
		{
			Dictionary<string, List<Outfit>> dictionary = BuildRandomizableOutfitsByType(family);
			if (dictionary.Count == 0)
			{
				return;
			}
			if (data.outfitDatas == null)
			{
				data.outfitDatas = new List<OutfitData>();
			}
			RemoveRandomizableOutfitData(data.outfitDatas, dictionary);
			AddRandomOutfit(data.outfitDatas, dictionary, "Head");
			AddRandomOutfit(data.outfitDatas, dictionary, "Body");
			AddRandomOutfit(data.outfitDatas, dictionary, "Eyes");
			AddRandomOutfit(data.outfitDatas, dictionary, "Pupil");
			AddRandomOutfit(data.outfitDatas, dictionary, "HairFront");
			AddRandomOutfit(data.outfitDatas, dictionary, "HairBack");
			AddBodyMatchedUnderwear(data.outfitDatas, dictionary, bodyProfile);
			string empty = string.Empty;
			if (dictionary.ContainsKey("Overall") && UnityEngine.Random.value < 0.22f)
			{
				empty = GetOutfitTheme(AddRandomOutfit(data.outfitDatas, dictionary, "Overall"));
			}
			else
			{
				empty = GetOutfitTheme(AddRandomOutfit(data.outfitDatas, dictionary, "Top"));
				Outfit outfit = AddRandomOutfit(data.outfitDatas, dictionary, "Bottom", empty, 0.65f);
				if (string.IsNullOrEmpty(empty))
				{
					empty = GetOutfitTheme(outfit);
				}
			}
			AddRandomOutfit(data.outfitDatas, dictionary, "Feet", empty, 0.55f);
			AddOptionalHeadFeature(data.outfitDatas, dictionary, empty);
			AddOptionalOutfit(data.outfitDatas, dictionary, "Neck", 0.22f, empty);
			AddOptionalOutfit(data.outfitDatas, dictionary, "Gloves", 0.22f, empty);
			AddOptionalOutfit(data.outfitDatas, dictionary, "Waist", 0.16f, empty);
			AddOptionalOutfit(data.outfitDatas, dictionary, "Shoulders", 0.12f, empty);
			AddOptionalOutfit(data.outfitDatas, dictionary, "FaceDetails", 0.18f);
			AddOptionalLegLayer(data.outfitDatas, dictionary);
			AddOptionalMakeup(data.outfitDatas, dictionary);
		}

		private static void RemoveRandomizableOutfitData(List<OutfitData> outfitDatas, Dictionary<string, List<Outfit>> outfitsByType)
		{
			for (int num = outfitDatas.Count - 1; num >= 0; num--)
			{
				if (outfitsByType.ContainsKey(GetOutfitTypePrefix(outfitDatas[num].outfit)))
				{
					outfitDatas.RemoveAt(num);
				}
			}
		}

		private Outfit AddRandomOutfit(List<OutfitData> outfitDatas, Dictionary<string, List<Outfit>> outfitsByType, string type, string preferredTheme = "", float themeMatchChance = 0f)
		{
			if (!outfitsByType.TryGetValue(type, out var value) || value.Count == 0)
			{
				return null;
			}
			Outfit outfit = SelectOutfit(value, preferredTheme, themeMatchChance);
			if (outfit != null)
			{
				UpsertOutfitData(outfitDatas, CreateRandomOutfitData(outfit));
			}
			return outfit;
		}

		private static Outfit SelectOutfit(List<Outfit> candidates, string preferredTheme, float themeMatchChance)
		{
			if (!string.IsNullOrEmpty(preferredTheme) && UnityEngine.Random.value < themeMatchChance)
			{
				List<Outfit> list = candidates.Where((Outfit candidate) => string.Equals(GetOutfitTheme(candidate), preferredTheme, StringComparison.Ordinal)).ToList();
				if (list.Count > 0)
				{
					return list[UnityEngine.Random.Range(0, list.Count)];
				}
			}
			return candidates[UnityEngine.Random.Range(0, candidates.Count)];
		}

		private void AddBodyMatchedUnderwear(List<OutfitData> outfitDatas, Dictionary<string, List<Outfit>> outfitsByType, HumanBodyProfile bodyProfile)
		{
			AddPreferredNamedOutfit(outfitDatas, outfitsByType, "UnderUpper", bodyProfile.FeminineFrame ? "Bra" : "UnderShirt", 0.82f);
			AddPreferredNamedOutfit(outfitDatas, outfitsByType, "UnderLower", bodyProfile.FeminineFrame ? "Panties" : "Boxers", 0.82f);
		}

		private void AddPreferredNamedOutfit(List<OutfitData> outfitDatas, Dictionary<string, List<Outfit>> outfitsByType, string type, string preferredNamePart, float preferenceChance)
		{
			if (!outfitsByType.TryGetValue(type, out var value) || value.Count == 0)
			{
				return;
			}
			Outfit outfit = null;
			if (UnityEngine.Random.value < preferenceChance)
			{
				outfit = value.FirstOrDefault((Outfit candidate) => candidate.name.IndexOf(preferredNamePart, StringComparison.OrdinalIgnoreCase) >= 0);
			}
			if ((object)outfit == null)
			{
				outfit = value[UnityEngine.Random.Range(0, value.Count)];
			}
			UpsertOutfitData(outfitDatas, CreateRandomOutfitData(outfit));
		}

		private void AddOptionalHeadFeature(List<OutfitData> outfitDatas, Dictionary<string, List<Outfit>> outfitsByType, string outfitTheme)
		{
			if (UnityEngine.Random.value >= 0.4f)
			{
				return;
			}
			string[] array = new string[5] { "Hat", "UpperFace", "LowerFace", "HeadAcc", "Ears" };
			float[] array2 = new float[5] { 0.3f, 0.27f, 0.23f, 0.12f, 0.08f };
			float value = UnityEngine.Random.value;
			float num = 0f;
			for (int i = 0; i < array.Length; i++)
			{
				num += array2[i];
				if (value <= num)
				{
					AddRandomOutfit(outfitDatas, outfitsByType, array[i], outfitTheme, 0.55f);
					break;
				}
			}
		}

		private void AddOptionalOutfit(List<OutfitData> outfitDatas, Dictionary<string, List<Outfit>> outfitsByType, string type, float probability, string preferredTheme = "")
		{
			if (UnityEngine.Random.value < probability)
			{
				AddRandomOutfit(outfitDatas, outfitsByType, type, preferredTheme, 0.5f);
			}
		}

		private void AddOptionalLegLayer(List<OutfitData> outfitDatas, Dictionary<string, List<Outfit>> outfitsByType)
		{
			if (!(UnityEngine.Random.value >= 0.3f))
			{
				string text = ((UnityEngine.Random.value < 0.7f) ? "Socks" : "Leggings");
				AddRandomOutfit(outfitDatas, outfitsByType, text);
			}
		}

		private void AddOptionalMakeup(List<OutfitData> outfitDatas, Dictionary<string, List<Outfit>> outfitsByType)
		{
			if (UnityEngine.Random.value >= 0.25f)
			{
				return;
			}
			string[] array = new string[3] { "MakeUpEyes", "MakeUpCheeks", "MakeUpLips" };
			int num = UnityEngine.Random.Range(0, array.Length);
			AddRandomOutfit(outfitDatas, outfitsByType, array[num]);
			for (int i = 0; i < array.Length; i++)
			{
				if (i != num && UnityEngine.Random.value < 0.45f)
				{
					AddRandomOutfit(outfitDatas, outfitsByType, array[i]);
				}
			}
		}

		private static string GetOutfitTheme(Outfit outfit)
		{
			if (outfit == null)
			{
				return string.Empty;
			}
			string[] array = new string[19]
			{
				"Police", "Fire", "HealthCare", "Chef", "Maid", "Office", "Camo", "Snow", "Sharp", "Steel",
				"Rustic", "Devout", "Meditative", "Fearsome", "Villager", "Ranger", "Witch", "Battle", "Vanguard"
			};
			for (int i = 0; i < array.Length; i++)
			{
				if (outfit.name.IndexOf(array[i], StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return array[i];
				}
			}
			return string.Empty;
		}

		private Dictionary<string, List<Outfit>> BuildRandomizableOutfitsByType(CharacterFamily family)
		{
			Dictionary<string, List<Outfit>> dictionary = new Dictionary<string, List<Outfit>>();
			string familyTag = ((family == CharacterFamily.Anime) ? "Anime" : "Stylized");
			foreach (Outfit value2 in OutfitDataBase.Values)
			{
				if (!(value2 == null) && !(value2.Type == null) && value2.showCharacterCreator && IsRandomOutfitCompatible(value2, family, familyTag))
				{
					if (!dictionary.TryGetValue(value2.Type.name, out var value))
					{
						value = new List<Outfit>();
						dictionary.Add(value2.Type.name, value);
					}
					value.Add(value2);
				}
			}
			return dictionary;
		}

		private static bool IsRandomOutfitCompatible(Outfit outfit, CharacterFamily family, string familyTag)
		{
			if (outfit == null || outfit.Type == null)
			{
				return false;
			}
			if (string.Equals(outfit.Type.name, "Head", StringComparison.Ordinal))
			{
				if (string.Equals(outfit.name, "Head_Stern", StringComparison.Ordinal))
				{
					return false;
				}
				if (outfit.tags != null)
				{
					return outfit.tags.Any((string tag) => string.Equals(tag, familyTag, StringComparison.OrdinalIgnoreCase));
				}
				return false;
			}
			bool flag = family == CharacterFamily.Anime;
			bool flag2 = string.Equals(outfit.PackName, "Anime", StringComparison.OrdinalIgnoreCase);
			if (!string.IsNullOrWhiteSpace(outfit.PackName) && flag2 != flag)
			{
				return false;
			}
			if (string.Equals(outfit.Type.name, "Body", StringComparison.Ordinal) && outfit.name.IndexOf("Anime", StringComparison.OrdinalIgnoreCase) >= 0 != flag)
			{
				return false;
			}
			if (outfit.requiredTags != null && outfit.requiredTags.Length != 0)
			{
				return outfit.requiredTags.All((string tag) => string.Equals(tag, familyTag, StringComparison.OrdinalIgnoreCase));
			}
			return true;
		}

		private OutfitData CreateRandomOutfitData(Outfit outfit)
		{
			return new OutfitData
			{
				outfit = outfit.Type.name + "/" + outfit.name,
				colors = CreateFilledColors(Color.white, 9),
				decalColors = CreateFilledColors(Color.white, 3),
				decalScale = Vector4.one,
				pattern = string.Empty,
				patternColors = CreateFilledColors(Color.white, 3),
				patternScale = Vector4.one,
				color = Color.white,
				swatch = 0,
				partVisibility = CreateRandomPartVisibility(outfit),
				decalDatas = ((outfit.decalDatas != null) ? new List<DecalData>(outfit.decalDatas) : null)
			};
		}

		private void ApplyStyledOutfitColors(List<OutfitData> outfitDatas, AppearancePalette appearance)
		{
			if (outfitDatas == null)
			{
				return;
			}
			for (int i = 0; i < outfitDatas.Count; i++)
			{
				OutfitData value = outfitDatas[i];
				string outfitTypePrefix = GetOutfitTypePrefix(value.outfit);
				switch (outfitTypePrefix)
				{
				case "HairFront":
				case "HairBack":
				{
					string[] channels2 = (TryGetOutfitDefinition(value.outfit, out var outfit2) ? outfit2.ColorChannels : null);
					value.colors = CreateHairChannelColors(channels2, appearance.Hair, appearance.HairAccent);
					value.color = appearance.Hair;
					break;
				}
				case "LowerFace":
					value.colors = CreateShadeSet(appearance.Hair);
					value.color = appearance.Hair;
					break;
				default:
					if (outfitTypePrefix.StartsWith("MakeUp", StringComparison.Ordinal) || outfitTypePrefix == "FaceDetails")
					{
						Color color = ((outfitTypePrefix == "MakeUpEyes") ? ShadeColor(appearance.Hair, 0.65f, 0.8f) : MutedColor(appearance.Wardrobe.Accent, 0.55f));
						value.colors = CreateShadeSet(color);
						value.color = color;
					}
					else
					{
						Color categoryBaseColor = GetCategoryBaseColor(outfitTypePrefix, appearance.Wardrobe, appearance.WardrobeScheme);
						string[] channels = (TryGetOutfitDefinition(value.outfit, out var outfit) ? outfit.ColorChannels : null);
						value.colors = CreateStyledChannelColors(channels, categoryBaseColor, appearance.Wardrobe);
						value.color = categoryBaseColor;
					}
					break;
				case "Head":
				case "Body":
				case "Eyes":
				case "Pupil":
					continue;
				}
				Color harmoniousChromaticColor = GetHarmoniousChromaticColor(value.color, appearance.Wardrobe);
				Color neutralContrast = GetNeutralContrast(value.color, appearance.Wardrobe);
				value.decalColors = new List<Color>
				{
					harmoniousChromaticColor,
					neutralContrast,
					appearance.Wardrobe.Metal
				};
				value.patternColors = new List<Color> { value.color, harmoniousChromaticColor, neutralContrast };
				outfitDatas[i] = value;
			}
		}

		private void ApplyNaturalEyeAndFaceColors(CharacterData data, AppearancePalette appearance)
		{
			if (data?.outfitDatas == null)
			{
				return;
			}
			Color color = new Color32(20, 17, 15, byte.MaxValue);
			Color value = new Color32(244, 241, 234, byte.MaxValue);
			Color value2 = new Color32(205, 198, 188, byte.MaxValue);
			Color value3 = new Color32(238, 231, 218, byte.MaxValue);
			for (int i = 0; i < data.outfitDatas.Count; i++)
			{
				OutfitData value4 = data.outfitDatas[i];
				string outfitTypePrefix = GetOutfitTypePrefix(value4.outfit);
				if (outfitTypePrefix == "Eyes")
				{
					value4.colors = CreateFilledColors(appearance.Iris, 9);
					value4.colors[0] = appearance.Iris;
					value4.colors[1] = ShadeColor(appearance.Iris, 0.7f, 1.05f);
					value4.colors[2] = ShadeColor(appearance.Iris, 1.22f, 0.9f);
					value4.color = appearance.Iris;
					data.outfitDatas[i] = value4;
				}
				else if (outfitTypePrefix == "Pupil")
				{
					value4.colors = CreateFilledColors(color, 9);
					value4.color = color;
					data.outfitDatas[i] = value4;
				}
				else
				{
					if (outfitTypePrefix != "Head" || !TryGetOutfitDefinition(value4.outfit, out var outfit))
					{
						continue;
					}
					ref List<Color> colors = ref value4.colors;
					if (colors == null)
					{
						colors = new List<Color>();
					}
					EnsureColorCount(value4.colors, 9);
					for (int j = 0; j < outfit.ColorChannels.Length && j < value4.colors.Count; j++)
					{
						string text = (outfit.ColorChannels[j] ?? string.Empty).ToLowerInvariant();
						if (text.Contains("sclera edge"))
						{
							value4.colors[j] = value2;
						}
						else if (text.Contains("sclera"))
						{
							value4.colors[j] = value;
						}
						else if (text.Contains("teeth"))
						{
							value4.colors[j] = value3;
						}
						else if (text.Contains("eye lash") || text.Contains("eye brow"))
						{
							value4.colors[j] = ShadeColor(appearance.Hair, 0.65f, 0.8f);
						}
					}
					data.outfitDatas[i] = value4;
				}
			}
		}

		private List<Color> CreateStyledChannelColors(string[] channels, Color baseColor, WardrobePalette palette)
		{
			List<Color> list = CreateFilledColors(baseColor, 9);
			if (channels == null)
			{
				list[1] = palette.Secondary;
				list[2] = palette.Accent;
				return list;
			}
			for (int i = 0; i < channels.Length && i < list.Count; i++)
			{
				list[i] = ResolveChannelColor(channels[i], i, baseColor, palette);
			}
			return list;
		}

		private static Color ResolveChannelColor(string channelName, int index, Color baseColor, WardrobePalette palette)
		{
			string text = (channelName ?? string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
			if (index != 0)
			{
				switch (text)
				{
				case "base":
				case "shirt":
				case "overalls":
				case "pants":
					break;
				default:
					if (text.Contains("button") || text.Contains("buckle") || text.Contains("metal") || text.Contains("ring"))
					{
						return palette.Metal;
					}
					if (text.Contains("gem"))
					{
						return palette.Accent;
					}
					if (text.Contains("undershirt") || text.Contains("lining"))
					{
						return palette.NeutralLight;
					}
					if (text.Contains("sole") || text.Contains("palm") || text.Contains("shade"))
					{
						return palette.NeutralDark;
					}
					if (text.Contains("lace"))
					{
						return palette.NeutralLight;
					}
					if (text.Contains("pattern") || text.Contains("stripe") || text.Contains("motif") || text.Contains("tie"))
					{
						return palette.Accent;
					}
					if (text.Contains("secondary") || text.Contains("base2") || text.Contains("gradient"))
					{
						return palette.Secondary;
					}
					if (index % 2 != 0)
					{
						return palette.Secondary;
					}
					return palette.Accent;
				}
			}
			return baseColor;
		}

		private static Color GetCategoryBaseColor(string type, WardrobePalette palette, int scheme)
		{
			Color result;
			Color result2;
			Color color;
			switch (scheme)
			{
			case 1:
				result = palette.Secondary;
				result2 = palette.Main;
				color = palette.Accent;
				break;
			case 2:
				result = palette.Main;
				result2 = palette.NeutralDark;
				color = palette.Secondary;
				break;
			case 3:
				result = palette.NeutralLight;
				result2 = palette.Main;
				color = palette.Accent;
				break;
			case 4:
				result = palette.Accent;
				result2 = palette.Main;
				color = palette.Secondary;
				break;
			case 5:
				result = palette.NeutralDark;
				result2 = palette.Main;
				color = palette.Accent;
				break;
			default:
				result = palette.Main;
				result2 = palette.Secondary;
				color = palette.Accent;
				break;
			}
			switch (type)
			{
			case "Bottom":
			case "Leggings":
				return result2;
			case "Feet":
				if (scheme != 0 && scheme != 1)
				{
					return ShadeColor(palette.NeutralDark, 0.8f, 0.9f);
				}
				return ShadeColor(color, 0.72f, 0.85f);
			case "Socks":
			case "UnderLower":
			case "UnderUpper":
				return palette.NeutralLight;
			case "Top":
				return result;
			case "Overall":
				return result;
			default:
				return color;
			}
		}

		private static Color GetHarmoniousChromaticColor(Color baseColor, WardrobePalette palette)
		{
			if (!(ColorDistanceSquared(baseColor, palette.Secondary) > 0.008f))
			{
				return palette.Accent;
			}
			return palette.Secondary;
		}

		private static Color GetNeutralContrast(Color baseColor, WardrobePalette palette)
		{
			if (!(ColorDistanceSquared(baseColor, palette.NeutralLight) >= ColorDistanceSquared(baseColor, palette.NeutralDark)))
			{
				return palette.NeutralDark;
			}
			return palette.NeutralLight;
		}

		private static float ColorDistanceSquared(Color left, Color right)
		{
			float num = left.r - right.r;
			float num2 = left.g - right.g;
			float num3 = left.b - right.b;
			return num * num * 0.3f + num2 * num2 * 0.59f + num3 * num3 * 0.11f;
		}

		private bool TryGetOutfitDefinition(string outfitPath, out Outfit outfit)
		{
			outfit = null;
			if (string.IsNullOrEmpty(outfitPath))
			{
				return false;
			}
			int num = outfitPath.IndexOf('/');
			string key = ((num >= 0) ? outfitPath.Substring(num + 1) : outfitPath);
			if (OutfitDataBase.TryGetValue(key, out outfit))
			{
				return outfit != null;
			}
			return false;
		}

		private static AppearancePalette CreateAppearancePalette()
		{
			Color hair = NaturalHairColors[UnityEngine.Random.Range(0, NaturalHairColors.Length)];
			WardrobeMood mood = SelectWardrobeMood();
			WardrobePalette wardrobe = CreateHarmoniousPalette(WardrobePalettes[UnityEngine.Random.Range(0, WardrobePalettes.Length)], mood);
			return new AppearancePalette
			{
				Wardrobe = wardrobe,
				WardrobeScheme = SelectWardrobeScheme(mood),
				Iris = NaturalIrisColors[UnityEngine.Random.Range(0, NaturalIrisColors.Length)],
				Hair = hair,
				HairAccent = SelectHairAccent(hair, wardrobe, mood)
			};
		}

		private static WardrobeMood SelectWardrobeMood()
		{
			float value = UnityEngine.Random.value;
			if (value < 0.36f)
			{
				return WardrobeMood.Everyday;
			}
			if (value < 0.58f)
			{
				return WardrobeMood.Tonal;
			}
			if (value < 0.76f)
			{
				return WardrobeMood.Soft;
			}
			if (value < 0.92f)
			{
				return WardrobeMood.Bold;
			}
			return WardrobeMood.Dark;
		}

		private static int SelectWardrobeScheme(WardrobeMood mood)
		{
			switch (mood)
			{
			case WardrobeMood.Tonal:
				return UnityEngine.Random.Range(0, 2);
			case WardrobeMood.Soft:
				if (!(UnityEngine.Random.value < 0.72f))
				{
					return 3;
				}
				return UnityEngine.Random.Range(0, 2);
			case WardrobeMood.Bold:
				if (!(UnityEngine.Random.value < 0.45f))
				{
					return UnityEngine.Random.Range(0, 2);
				}
				return 4;
			case WardrobeMood.Dark:
				if (!(UnityEngine.Random.value < 0.65f))
				{
					return 2;
				}
				return 5;
			default:
				return UnityEngine.Random.Range(0, 4);
			}
		}

		private static WardrobePalette CreateHarmoniousPalette(WardrobePalette source, WardrobeMood mood)
		{
			float num = ((UnityEngine.Random.value < 0.5f) ? (-1f) : 1f);
			float minInclusive = 0.035f;
			float maxInclusive = 0.085f;
			float minInclusive2 = 0.09f;
			float maxInclusive2 = 0.145f;
			float saturationMultiplier = 1f;
			float valueMultiplier = 1f;
			switch (mood)
			{
			case WardrobeMood.Tonal:
				minInclusive = 0.012f;
				maxInclusive = 0.04f;
				minInclusive2 = 0.045f;
				maxInclusive2 = 0.08f;
				saturationMultiplier = 0.88f;
				break;
			case WardrobeMood.Soft:
				saturationMultiplier = 0.58f;
				valueMultiplier = 1.22f;
				break;
			case WardrobeMood.Bold:
				minInclusive = 0.065f;
				maxInclusive = 0.12f;
				minInclusive2 = 0.13f;
				maxInclusive2 = 0.22f;
				saturationMultiplier = 1.18f;
				valueMultiplier = 1.08f;
				break;
			case WardrobeMood.Dark:
				saturationMultiplier = 0.92f;
				valueMultiplier = 0.68f;
				break;
			}
			Color color = ShiftColor(source.Main, 0f, saturationMultiplier, valueMultiplier);
			float hueOffset = num * UnityEngine.Random.Range(minInclusive, maxInclusive);
			float hueOffset2 = num * UnityEngine.Random.Range(minInclusive2, maxInclusive2);
			Color secondary = ShiftColor(color, hueOffset, UnityEngine.Random.Range(0.82f, 1.02f), UnityEngine.Random.Range(0.9f, 1.08f));
			Color accent = ShiftColor(color, hueOffset2, UnityEngine.Random.Range(0.9f, 1.08f), UnityEngine.Random.Range(0.96f, 1.12f));
			return new WardrobePalette(color, secondary, accent, source.NeutralLight, source.NeutralDark, source.Metal);
		}

		private static Color ShiftColor(Color color, float hueOffset, float saturationMultiplier, float valueMultiplier)
		{
			Color.RGBToHSV(color, out var H, out var S, out var V);
			return Color.HSVToRGB(Mathf.Repeat(H + hueOffset, 1f), Mathf.Clamp01(S * saturationMultiplier), Mathf.Clamp01(V * valueMultiplier));
		}

		private static Color CreateTonalHairAccent(Color hair)
		{
			if (!(UnityEngine.Random.value < 0.5f))
			{
				return ShadeColor(hair, UnityEngine.Random.Range(0.68f, 0.84f), 1.02f);
			}
			return ShadeColor(hair, UnityEngine.Random.Range(1.18f, 1.38f), 0.82f);
		}

		private static Color SelectHairAccent(Color hair, WardrobePalette wardrobe, WardrobeMood mood)
		{
			if (mood == WardrobeMood.Bold && UnityEngine.Random.value < 0.55f)
			{
				if (!(UnityEngine.Random.value < 0.65f))
				{
					return SelectDistinctHairAccent(hair);
				}
				return wardrobe.Accent;
			}
			if (mood == WardrobeMood.Soft && UnityEngine.Random.value < 0.22f)
			{
				return Color.Lerp(CreateTonalHairAccent(hair), wardrobe.Secondary, 0.28f);
			}
			return CreateTonalHairAccent(hair);
		}

		private static Color SelectDistinctHairAccent(Color hair)
		{
			List<Color> list = HairAccentColors.Where((Color color) => ColorDistanceSquared(hair, color) >= 0.055f).ToList();
			if (list.Count <= 0)
			{
				return HairAccentColors[UnityEngine.Random.Range(0, HairAccentColors.Length)];
			}
			return list[UnityEngine.Random.Range(0, list.Count)];
		}

		private static List<Color> CreateHairChannelColors(string[] channels, Color baseColor, Color accentColor)
		{
			List<Color> list = CreateFilledColors(baseColor, 9);
			list[1] = accentColor;
			if (channels == null)
			{
				return list;
			}
			for (int i = 0; i < channels.Length && i < list.Count; i++)
			{
				string text = (channels[i] ?? string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
				if (i == 0 || text == "base")
				{
					list[i] = baseColor;
				}
				else if (text.Contains("gradient") || text.Contains("clip") || text.Contains("tie"))
				{
					list[i] = accentColor;
				}
				else
				{
					list[i] = Color.Lerp(baseColor, accentColor, 0.35f);
				}
			}
			return list;
		}

		private static List<Color> CreateFilledColors(Color color, int count)
		{
			List<Color> list = new List<Color>(count);
			for (int i = 0; i < count; i++)
			{
				list.Add(color);
			}
			return list;
		}

		private static List<Color> CreateShadeSet(Color color)
		{
			List<Color> list = CreateFilledColors(color, 9);
			for (int i = 1; i < list.Count; i++)
			{
				list[i] = ((i % 2 == 0) ? ShadeColor(color, 1.12f, 0.9f) : ShadeColor(color, 0.78f, 1f));
			}
			return list;
		}

		private static Color ShadeColor(Color color, float valueMultiplier, float saturationMultiplier)
		{
			Color.RGBToHSV(color, out var H, out var S, out var V);
			return Color.HSVToRGB(H, Mathf.Clamp01(S * saturationMultiplier), Mathf.Clamp01(V * valueMultiplier));
		}

		private static Color MutedColor(Color color, float saturationMultiplier)
		{
			Color.RGBToHSV(color, out var H, out var S, out var V);
			return Color.HSVToRGB(H, S * saturationMultiplier, Mathf.Clamp(V, 0.35f, 0.78f));
		}

		private bool[] CreateRandomPartVisibility(Outfit outfit)
		{
			if (outfit.optionalPieces == null || outfit.optionalPieces.Length == 0)
			{
				return null;
			}
			bool[] array = new bool[outfit.optionalPieces.Length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = true;
			}
			if (array.Length != 0 && UnityEngine.Random.value < 0.15f)
			{
				array[UnityEngine.Random.Range(0, array.Length)] = false;
			}
			return array;
		}

		private void UpsertOutfitData(List<OutfitData> outfitDatas, OutfitData newData)
		{
			string outfitTypePrefix = GetOutfitTypePrefix(newData.outfit);
			for (int i = 0; i < outfitDatas.Count; i++)
			{
				if (string.Equals(GetOutfitTypePrefix(outfitDatas[i].outfit), outfitTypePrefix, StringComparison.Ordinal))
				{
					outfitDatas[i] = newData;
					return;
				}
			}
			outfitDatas.Add(newData);
		}

		private static string GetOutfitTypePrefix(string outfitPath)
		{
			if (string.IsNullOrEmpty(outfitPath))
			{
				return string.Empty;
			}
			int num = outfitPath.IndexOf('/');
			if (num <= 0)
			{
				return outfitPath;
			}
			return outfitPath.Substring(0, num);
		}

		private void PushHistoryState(List<CharacterData> states, CharacterData data)
		{
			states.Add(PlayerCustomizationSession.CloneCharacterDataForRuntime(data));
			int num = Mathf.Max(1, maxHistoryStates);
			while (states.Count > num)
			{
				states.RemoveAt(0);
			}
		}

		private CharacterData PopHistoryState(List<CharacterData> states)
		{
			int index = states.Count - 1;
			CharacterData result = states[index];
			states.RemoveAt(index);
			return result;
		}

		private bool AreCharacterDataEqual(CharacterData left, CharacterData right)
		{
			if (left == null || right == null)
			{
				return left == right;
			}
			return string.Equals(JsonUtility.ToJson(left), JsonUtility.ToJson(right), StringComparison.Ordinal);
		}

		[ContextMenu("Save")]
		private IEnumerator Save()
		{
			yield return new WaitForEndOfFrame();
			if (CharacterName == null)
			{
				Debug.LogWarning("Save Name InputField not Assigned Please assign one to save character", base.gameObject);
				yield break;
			}
			if (CharacterName.text.Length == 0)
			{
				Debug.LogWarning("Please enter in a name with at least one letter");
				yield break;
			}
			RenderTexture.active = iconTexture;
			Texture2D texture2D = new Texture2D(iconTexture.width, iconTexture.height, TextureFormat.RGBA32, mipChain: false);
			Rect source = new Rect(new Rect(0f, 0f, iconTexture.width, iconTexture.height));
			texture2D.ReadPixels(source, 0, 0);
			texture2D.Apply();
			byte[] bytes = texture2D.EncodeToPNG();
			Texture2D icon = null;
			BMAC_SaveSystem.EnsureSaveDirectories();
			File.WriteAllBytes(BMAC_SaveSystem.iconFilePath + "/" + CharacterName.text + ".png", bytes);
			BMAC_SaveSystem.SaveCharacter(character, CharacterName.text, icon);
			UpdateCharacterSaves();
		}

		public void UpdateCharacterSaves()
		{
			if (saveContainer == null)
			{
				Debug.LogWarning("Save Container not assigned. Assigned one to display saves");
			}
			SaveSelector[] array = saveSlots.Values.ToArray();
			for (int i = 0; i < array.Length; i++)
			{
				UnityEngine.Object.Destroy(array[i].gameObject);
			}
			saveSlots.Clear();
			BMAC_SaveSystem.EnsureSaveDirectories();
			List<CharacterObject> list = Resources.LoadAll<CharacterObject>("").ToList();
			for (int j = 0; j < list.Count; j++)
			{
				CharacterObject characterObject = list[j];
				if (!(characterObject == null) && characterObject.ShowInCharacterList && characterObject.data != null && !saveSlots.ContainsKey(characterObject.data.characterName))
				{
					SaveSelector saveSelector = UnityEngine.Object.Instantiate(this.saveSelector, saveContainer);
					Sprite icon = null;
					if (characterObject.icon != null)
					{
						icon = Sprite.Create(characterObject.icon, new Rect(0f, 0f, characterObject.icon.width, characterObject.icon.height), new Vector2(0.5f, 0.5f));
					}
					saveSelector.Init(characterObject.data, icon, this);
					saveSlots.Add(characterObject.data.characterName, saveSelector);
				}
			}
			AddCharacterSaveFiles(BMAC_SaveSystem.filePath, BMAC_SaveSystem.iconFilePath);
			string text = BMAC_SaveSystem.filePath.Replace("BoZo_StylizedModularCharacters/CustomCharacters", "BoZo_ModularAnimeCharacters/CharacterSaves");
			string iconDirectory = text + "/Icons";
			AddCharacterSaveFiles(text, iconDirectory);
			SaveSelector[] array2 = (from item in saveContainer.GetComponentsInChildren<SaveSelector>(includeInactive: true)
				where item.transform.parent == saveContainer
				select item).OrderBy((SaveSelector item) => item.data.characterName, StringComparer.OrdinalIgnoreCase).ToArray();
			for (int num = 0; num < array2.Length; num++)
			{
				array2[num].transform.SetSiblingIndex(num);
			}
		}

		private void AddCharacterSaveFiles(string saveDirectory, string iconDirectory)
		{
			if (!Directory.Exists(saveDirectory))
			{
				return;
			}
			string[] files = Directory.GetFiles(saveDirectory, "*.json");
			Array.Sort(files, StringComparer.OrdinalIgnoreCase);
			string[] array = files;
			foreach (string text in array)
			{
				try
				{
					CharacterData characterData = JsonUtility.FromJson<CharacterData>(File.ReadAllText(text));
					if (characterData == null || string.IsNullOrWhiteSpace(characterData.characterName))
					{
						Debug.LogWarning("[CharacterCreator] Ignored invalid character save: " + text, this);
					}
					else if (!saveSlots.ContainsKey(characterData.characterName))
					{
						Sprite icon = LoadCharacterSaveIcon(text, iconDirectory);
						SaveSelector saveSelector = UnityEngine.Object.Instantiate(this.saveSelector, saveContainer);
						saveSelector.Init(characterData, icon, this);
						saveSlots.Add(characterData.characterName, saveSelector);
					}
				}
				catch (Exception ex)
				{
					Debug.LogWarning("[CharacterCreator] Failed to load character save '" + text + "'. " + ex.Message, this);
				}
			}
		}

		private Sprite LoadCharacterSaveIcon(string jsonFilePath, string iconDirectory)
		{
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(jsonFilePath);
			string path = Path.Combine(iconDirectory, fileNameWithoutExtension + ".png");
			if (!File.Exists(path))
			{
				return null;
			}
			byte[] data = File.ReadAllBytes(path);
			Texture2D texture2D = new Texture2D(2, 2);
			if (texture2D.LoadImage(data))
			{
				return Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f));
			}
			UnityEngine.Object.Destroy(texture2D);
			return null;
		}

		public async void LoadCharacter(CharacterData data)
		{
			if (!(loadedCharacterNameText == null))
			{
				loadedCharacterNameText.text = data.characterName;
				RecordUndoState();
				await ApplyCharacterDataAsync(data);
			}
		}

		public void DeleteCharacter()
		{
			if (DeleteConfirmWindow == null)
			{
				Debug.LogWarning("Delete Character Confirmation Window is no assigned please assign one to delete character", base.gameObject);
			}
			if (!(loadedCharacterNameText == null) && !(loadedCharacterNameText.text == ""))
			{
				DeleteCharacterNameText.text = "Delete: " + loadedCharacterNameText.text;
				DeleteConfirmWindow.SetActive(value: true);
			}
		}

		public void ConfirmDelete()
		{
			if (!(loadedCharacterNameText == null))
			{
				BMAC_SaveSystem.DeleteCharacter(loadedCharacterNameText.text);
				loadedCharacterNameText.text = "";
				UpdateCharacterSaves();
			}
		}
	}
}
