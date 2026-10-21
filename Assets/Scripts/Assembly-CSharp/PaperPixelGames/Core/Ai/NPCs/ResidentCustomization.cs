using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bozo.ModularCharacters;
using PaperPixelGames.Core.Data.Ai.NPCs;
using PaperPixelGames.Core.Streaming;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using UnityEngine;

namespace PaperPixelGames.Core.Ai.NPCs
{
	[DefaultExecutionOrder(-10000)]
	public sealed class ResidentCustomization : MonoBehaviour
	{
		[SerializeField]
		private OutfitSystem _outfitSystem;

		private void Awake()
		{
			CacheOutfitSystem();
		}

		public void BindOutfitSystem(OutfitSystem outfitSystem)
		{
			_outfitSystem = outfitSystem;
		}

		public Task Apply(ResidentDataSO residentData)
		{
			CacheOutfitSystem();
			return ApplyToOutfitSystem(_outfitSystem, residentData, this);
		}

		public static Task ApplyToOutfitSystem(OutfitSystem outfitSystem, ResidentDataSO residentData, UnityEngine.Object context = null, bool ignoreBakedVisual = false)
		{
			if (!ignoreBakedVisual && residentData != null && residentData.HasUsableBakedVisual)
			{
				return Task.CompletedTask;
			}
			if (residentData == null || residentData.CharacterData == null)
			{
				return Task.CompletedTask;
			}
			if (outfitSystem == null)
			{
				UtilityMethods.Log("[ResidentCustomization] OutfitSystem missing.", LogType.Warning);
				return Task.CompletedTask;
			}
			outfitSystem.loadMode = OutfitSystem.LoadMode.Manual;
			CharacterData characterData = residentData.CharacterData.GetCharacterData();
			if (characterData == null)
			{
				return Task.CompletedTask;
			}
			outfitSystem.characterData = residentData.CharacterData as CharacterObject;
			outfitSystem.SaveID = residentData.CharacterData.name;
			return ApplyAsync(outfitSystem, characterData, context);
		}

		private static async Task ApplyAsync(OutfitSystem outfitSystem, CharacterData characterData, UnityEngine.Object context)
		{
			_ = 3;
			try
			{
				if (!outfitSystem.isStatic)
				{
					await WaitForDressingStartupStreamingAsync();
					if (!outfitSystem.initalized)
					{
						outfitSystem.Init();
					}
					if (await WaitForOutfitSystemLoadAsync(outfitSystem))
					{
						float stance = outfitSystem.stance;
						CharacterData migratedData = Bozo_SavePatcher.UpdateSave(CloneCharacterData(characterData));
						CharacterData characterData2 = CloneCharacterData(migratedData);
						characterData2.stance = stance;
						PrepareForVendorLoad(characterData2);
						await BMAC_SaveSystem.LoadCharacter(outfitSystem, characterData2, manualShapeApply: false, outfitSystem.async);
						await ApplyVendorLoadGapsAsync(outfitSystem, migratedData);
						outfitSystem.SetStance(stance);
					}
				}
			}
			catch (Exception ex)
			{
				UtilityMethods.Log("[ResidentCustomization] Failed to apply customization. Context=" + GetContextName(context) + ", Error=" + ex.Message, LogType.Warning);
			}
		}

		private static async Task<bool> WaitForOutfitSystemLoadAsync(OutfitSystem outfitSystem)
		{
			int frame = 0;
			while (outfitSystem != null && outfitSystem.isloading && frame < 600)
			{
				frame++;
				await Task.Yield();
			}
			if (outfitSystem != null && outfitSystem.isloading)
			{
				UtilityMethods.Log("[ResidentCustomization] OutfitSystem load wait timed out. Target=" + outfitSystem.name, LogType.Warning);
				return false;
			}
			return outfitSystem != null;
		}

		private static async Task WaitForDressingStartupStreamingAsync()
		{
			DressingSceneManager instance = Singleton<DressingSceneManager>.Instance;
			while (instance != null && instance.IsStartupStreamingBusy)
			{
				await Task.Yield();
				instance = Singleton<DressingSceneManager>.Instance;
			}
		}

		private static void PrepareForVendorLoad(CharacterData data)
		{
			if (data?.outfitDatas == null)
			{
				return;
			}
			for (int i = 0; i < data.outfitDatas.Count; i++)
			{
				OutfitData value = data.outfitDatas[i];
				ref string pattern = ref value.pattern;
				if (pattern == null)
				{
					pattern = string.Empty;
				}
				if (value.colors == null)
				{
					value.colors = new List<Color>();
				}
				if (RequiresDeferredColorApplication(value.outfit))
				{
					value.colors.Clear();
				}
				data.outfitDatas[i] = value;
			}
		}

		private static async Task ApplyVendorLoadGapsAsync(OutfitSystem outfitSystem, CharacterData sourceData)
		{
			if (outfitSystem == null || sourceData?.outfitDatas == null)
			{
				return;
			}
			bool flag = false;
			outfitSystem.isloading = true;
			try
			{
				for (int i = 0; i < sourceData.outfitDatas.Count; i++)
				{
					OutfitData outfitData = sourceData.outfitDatas[i];
					Outfit outfit = FindLoadedOutfit(outfitSystem, outfitData.outfit);
					if (outfit == null)
					{
						continue;
					}
					if (RequiresDeferredColorApplication(outfitData.outfit) && outfitData.colors != null)
					{
						for (int j = 0; j < outfitData.colors.Count; j++)
						{
							outfit.SetColor(outfitData.colors[j], j + 1);
						}
						flag = true;
					}
					if (!string.IsNullOrEmpty(outfitData.pattern) && outfitData.patternColors != null)
					{
						int num = Mathf.Min(3, outfitData.patternColors.Count);
						for (int k = 0; k < num; k++)
						{
							outfit.SetPatternColor(outfitData.patternColors[k], k + 1);
						}
						flag = true;
					}
					if (outfit.customShader)
					{
						outfit.SetSwatch(outfitData.swatch);
						outfit.SetColor(outfitData.color);
						flag = true;
					}
				}
			}
			finally
			{
				outfitSystem.isloading = false;
			}
			if (flag)
			{
				await outfitSystem.MergeCharacter();
			}
		}

		private static Outfit FindLoadedOutfit(OutfitSystem outfitSystem, string outfitPath)
		{
			if (outfitSystem == null || string.IsNullOrWhiteSpace(outfitPath))
			{
				return null;
			}
			int num = outfitPath.IndexOf('/');
			string outfitType = ((num > 0) ? outfitPath.Substring(0, num) : outfitPath);
			return outfitSystem.GetOutfit(outfitType);
		}

		private static bool RequiresDeferredColorApplication(string outfitPath)
		{
			if (string.IsNullOrEmpty(outfitPath))
			{
				return false;
			}
			if (!outfitPath.StartsWith("Pupil/", StringComparison.Ordinal) && !outfitPath.StartsWith("Eyes/", StringComparison.Ordinal) && !outfitPath.StartsWith("UnderUpper/", StringComparison.Ordinal))
			{
				return outfitPath.StartsWith("UnderLower/", StringComparison.Ordinal);
			}
			return true;
		}

		private static CharacterData CloneCharacterData(CharacterData source)
		{
			if (source == null)
			{
				return null;
			}
			CharacterData characterData = new CharacterData
			{
				versionID = source.versionID,
				characterName = source.characterName,
				bodyIDs = ((source.bodyIDs != null) ? new List<string>(source.bodyIDs) : new List<string>()),
				bodyShapes = ((source.bodyShapes != null) ? new List<float>(source.bodyShapes) : new List<float>()),
				faceIDs = ((source.faceIDs != null) ? new List<string>(source.faceIDs) : new List<string>()),
				faceShapes = ((source.faceShapes != null) ? new List<float>(source.faceShapes) : new List<float>()),
				bodyModsKeys = ((source.bodyModsKeys != null) ? new List<string>(source.bodyModsKeys) : new List<string>()),
				bodyMods = CloneBodyMods(source.bodyMods),
				outfitDatas = new List<OutfitData>(),
				bodyData = CloneOutfitData(source.bodyData),
				stance = source.stance,
				material = source.material
			};
			if (source.outfitDatas != null)
			{
				for (int i = 0; i < source.outfitDatas.Count; i++)
				{
					characterData.outfitDatas.Add(CloneOutfitData(source.outfitDatas[i]));
				}
			}
			return characterData;
		}

		private static List<BodyModData> CloneBodyMods(List<BodyModData> source)
		{
			List<BodyModData> list = new List<BodyModData>();
			if (source == null)
			{
				return list;
			}
			for (int i = 0; i < source.Count; i++)
			{
				BodyModData bodyModData = source[i];
				list.Add((bodyModData == null) ? null : new BodyModData
				{
					scaleValue = bodyModData.scaleValue,
					scale = bodyModData.scale,
					posValue = bodyModData.posValue,
					position = bodyModData.position,
					rotation = bodyModData.rotation
				});
			}
			return list;
		}

		private static OutfitData CloneOutfitData(OutfitData source)
		{
			return new OutfitData
			{
				outfit = source.outfit,
				colors = ((source.colors != null) ? new List<Color>(source.colors) : new List<Color>()),
				decal = source.decal,
				decalColors = ((source.decalColors != null) ? new List<Color>(source.decalColors) : null),
				decalScale = source.decalScale,
				pattern = (source.pattern ?? string.Empty),
				patternColors = ((source.patternColors != null) ? new List<Color>(source.patternColors) : null),
				patternScale = source.patternScale,
				partVisibility = ((source.partVisibility != null) ? ((bool[])source.partVisibility.Clone()) : null),
				color = source.color,
				swatch = source.swatch,
				decalDatas = ((source.decalDatas != null) ? new List<DecalData>(source.decalDatas) : null)
			};
		}

		private static string GetContextName(UnityEngine.Object context)
		{
			if (!(context != null))
			{
				return "null";
			}
			return context.name;
		}

		private void CacheOutfitSystem()
		{
			if (_outfitSystem == null)
			{
				_outfitSystem = GetComponentInChildren<OutfitSystem>(includeInactive: true);
			}
			if (_outfitSystem != null)
			{
				_outfitSystem.loadMode = OutfitSystem.LoadMode.Manual;
			}
		}
	}
}
