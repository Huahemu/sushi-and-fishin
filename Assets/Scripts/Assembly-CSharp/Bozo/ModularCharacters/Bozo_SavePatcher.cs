using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bozo.ModularCharacters
{
	public static class Bozo_SavePatcher
	{
		public static CharacterData UpdateSave(CharacterData data)
		{
			if (data == null)
			{
				return null;
			}
			EnsureCollections(data);
			if (data.versionID < 1)
			{
				if (HasLegacyAnimeHead(data))
				{
					UpdateANIME(data);
				}
				if (data.versionID < 1)
				{
					data = Update1(data);
				}
			}
			if (data.versionID < 2)
			{
				data = Update2(data);
			}
			EnsureCollections(data);
			NormalizeCoreRigAndMaterial(data);
			return data;
		}

		private static bool HasLegacyAnimeHead(CharacterData data)
		{
			return data.outfitDatas.Exists((OutfitData item) => item.outfit == "Head/Head_Young" || item.outfit == "Head/Head_Young_v2");
		}

		private static CharacterData UpdateANIME(CharacterData data)
		{
			new Dictionary<int, OutfitData>();
			new List<OutfitData>(data.outfitDatas);
			for (int i = 0; i < data.outfitDatas.Count; i++)
			{
				OutfitData value = data.outfitDatas[i];
				if (value.outfit == "Head/Head_Young" || value.outfit == "Head/Head_Young_v2")
				{
					value.outfit = "Head/Head_AnimeYoung";
				}
				if (value.outfit == "Eyes/Eyes_Eyes01")
				{
					value.outfit = "Eyes/Eyes_AnimeBasic";
				}
				if (value.outfit == "Eyes/Eyes_Eyes01")
				{
					value.outfit = "Eyes/Eyes_AnimeBasic";
				}
				if (value.outfit == "Leggings/Leggings_Stockings")
				{
					value.outfit = "Leggings/Leggings_Stocking";
				}
				if (value.outfit == "Socks/Socks_SimpleSocks")
				{
					value.outfit = "Socks/Socks_BasicSocks";
				}
				if (value.outfit == "UnderUpper/UnderUpper_SimpleUnderShirt")
				{
					value.outfit = "UnderUpper/UnderUpper_SimpleUnderShirt2";
				}
				if (value.outfit == "Body/Body_BasicBody")
				{
					value.outfit = "Body/Body_AnimeBasic";
				}
				if (value.outfit == "Overall/Overall_Overall")
				{
					value.outfit = "Top/Top_Overall";
				}
				if (value.outfit == "Overall/Overall_Sundress")
				{
					value.outfit = "Top/Top_Sundress";
				}
				if (value.outfit == "Overall/Overall_SmartDress")
				{
					value.outfit = "Top/Top_SmartDress";
				}
				if (value.outfit.Contains("Hands"))
				{
					value.outfit = value.outfit.Replace("Hands", "Gloves");
				}
				data.outfitDatas[i] = value;
			}
			for (int j = 0; j < data.bodyModsKeys.Count; j++)
			{
				if (data.bodyModsKeys[j] == "eye_l")
				{
					data.bodyModsKeys[j] = "IrisRoot_l";
				}
			}
			float num = 100f;
			for (int k = 0; k < data.bodyIDs.Count; k++)
			{
				if (data.bodyIDs[k] == "BodyType")
				{
					num -= data.bodyShapes[k];
				}
			}
			data.bodyIDs.Add("Curvy");
			data.bodyShapes.Add(num);
			data.material = "BSMC_AnimeShader";
			data.versionID = 2;
			return data;
		}

		private static CharacterData Update1(CharacterData data)
		{
			OutfitData item = default;
			OutfitData item2 = default;
			OutfitData item3 = default;
			OutfitData item4 = default;
			OutfitData value = default;
			int num = -1;
			OutfitData value2 = default;
			int num2 = -1;
			OutfitData value3 = default;
			int num3 = -1;
			for (int i = 0; i < data.outfitDatas.Count; i++)
			{
				if (data.outfitDatas[i].outfit.Contains("Eyes/"))
				{
					value = data.outfitDatas[i];
					num = i;
				}
				if (data.outfitDatas[i].outfit.Contains("Body/"))
				{
					value2 = data.outfitDatas[i];
					num2 = i;
				}
				if (data.outfitDatas[i].outfit.Contains("Head/"))
				{
					value3 = data.outfitDatas[i];
					num3 = i;
				}
			}
			if (num2 >= 0 && value2.pattern == "Pattern/Underwear_Boxers_D")
			{
				item3.outfit = "";
				item4.outfit = "UnderLower/UnderLower_SimpleBoxers";
			}
			if (num2 >= 0 && value2.pattern == "Pattern/Underwear_BraPanties_D")
			{
				item3.outfit = "UnderUpper/UnderUpper_SimpleBra";
				item4.outfit = "UnderLower/UnderLower_SimplePanties";
			}
			if (num2 >= 0 && value2.pattern == "Pattern/Underwear_Neutral_D")
			{
				item3.outfit = "UnderUpper/UnderUpper_SimpleUnderShirt";
				item4.outfit = "UnderLower/UnderLower_SimpleBoxers";
			}
			string text = ((num >= 0) ? value.decal : string.Empty);
			if (!string.IsNullOrEmpty(text))
			{
				text = text.Replace("Decal", "Pupil");
				item.outfit = text;
				item.colors = value.decalColors;
				value.decal = "";
			}
			string text2 = ((num >= 0) ? value.pattern : string.Empty);
			if (!string.IsNullOrEmpty(text2))
			{
				text2 = text2.Replace("Pattern", "Eyes");
				item2.outfit = text2;
				item2.colors = value.patternColors;
				value.pattern = "";
			}
			if (num3 >= 0 && num >= 0 && value.colors != null && value.colors.Count > 0)
			{
				ref List<Color> colors = ref value3.colors;
				if (colors == null)
				{
					colors = new List<Color>();
				}
				EnsureColorCount(value3.colors, 8);
				value3.colors[6] = value.colors[0];
				value3.colors[7] = ((value.colors.Count > 1) ? value.colors[1] : value.colors[0]);
			}
			item4.colors = ((value2.patternColors != null) ? new List<Color>(value2.patternColors) : new List<Color>());
			item3.colors = ((value2.patternColors != null) ? new List<Color>(value2.patternColors) : new List<Color>());
			value2.pattern = "";
			value2.decal = "";
			if (!string.IsNullOrEmpty(item.outfit))
			{
				data.outfitDatas.Add(item);
			}
			if (!string.IsNullOrEmpty(item2.outfit))
			{
				data.outfitDatas.Add(item2);
			}
			if (!string.IsNullOrEmpty(item3.outfit))
			{
				data.outfitDatas.Add(item3);
			}
			if (!string.IsNullOrEmpty(item4.outfit))
			{
				data.outfitDatas.Add(item4);
			}
			if (num >= 0)
			{
				data.outfitDatas[num] = value;
			}
			if (num3 >= 0)
			{
				data.outfitDatas[num3] = value3;
			}
			if (num2 >= 0)
			{
				data.outfitDatas[num2] = value2;
			}
			if (num >= 0)
			{
				data.outfitDatas.RemoveAt(num);
			}
			if (!data.bodyIDs.Contains("NeckThickness"))
			{
				data.bodyShapes.Add(100f);
				data.bodyIDs.Add("NeckThickness");
			}
			data.versionID = 1;
			return data;
		}

		private static CharacterData Update2(CharacterData data)
		{
			data.material = (IsAnimeCharacter(data) ? "BSMC_AnimeShader" : "BSMC_StylizedShader");
			data.versionID = 2;
			return data;
		}

		private static void EnsureCollections(CharacterData data)
		{
			CharacterData characterData = data;
			if (characterData.bodyIDs == null)
			{
				characterData.bodyIDs = new List<string>();
			}
			characterData = data;
			if (characterData.bodyShapes == null)
			{
				characterData.bodyShapes = new List<float>();
			}
			characterData = data;
			if (characterData.faceIDs == null)
			{
				characterData.faceIDs = new List<string>();
			}
			characterData = data;
			if (characterData.faceShapes == null)
			{
				characterData.faceShapes = new List<float>();
			}
			characterData = data;
			if (characterData.bodyModsKeys == null)
			{
				characterData.bodyModsKeys = new List<string>();
			}
			characterData = data;
			if (characterData.bodyMods == null)
			{
				characterData.bodyMods = new List<BodyModData>();
			}
			characterData = data;
			if (characterData.outfitDatas == null)
			{
				characterData.outfitDatas = new List<OutfitData>();
			}
			TrimParallelLists(data.bodyIDs, data.bodyShapes);
			TrimParallelLists(data.faceIDs, data.faceShapes);
			TrimParallelLists(data.bodyModsKeys, data.bodyMods);
			data.outfitDatas.RemoveAll((OutfitData item) => string.IsNullOrWhiteSpace(item.outfit));
		}

		private static void TrimParallelLists<TId, TValue>(List<TId> ids, List<TValue> values)
		{
			int num = Mathf.Min(ids.Count, values.Count);
			if (ids.Count > num)
			{
				ids.RemoveRange(num, ids.Count - num);
			}
			if (values.Count > num)
			{
				values.RemoveRange(num, values.Count - num);
			}
		}

		private static void NormalizeCoreRigAndMaterial(CharacterData data)
		{
			bool flag = IsAnimeCharacter(data);
			for (int i = 0; i < data.outfitDatas.Count; i++)
			{
				OutfitData value = data.outfitDatas[i];
				if (value.outfit.StartsWith("Body/", StringComparison.Ordinal))
				{
					if (value.outfit.IndexOf("Anime", StringComparison.OrdinalIgnoreCase) >= 0 != flag)
					{
						value.outfit = (flag ? "Body/Body_AnimeBasic" : "Body/Body_BasicBody");
					}
				}
				else if (value.outfit.StartsWith("Eyes/", StringComparison.Ordinal))
				{
					if (value.outfit.IndexOf("Anime", StringComparison.OrdinalIgnoreCase) >= 0 != flag)
					{
						value.outfit = (flag ? "Eyes/Eyes_AnimeBasic" : "Eyes/Eyes_BasicIris");
					}
				}
				else if ((value.outfit == "MakeUpLips/MakeUpLips_BasicLipstick") & flag)
				{
					value.outfit = "MakeUpLips/MakeUpLips_SimpleLipstick";
				}
				else if (value.outfit == "MakeUpLips/MakeUpLips_SimpleLipstick" && !flag)
				{
					value.outfit = "MakeUpLips/MakeUpLips_BasicLipstick";
				}
				else if ((value.outfit == "UnderUpper/UnderUpper_SimpleUnderShirt") & flag)
				{
					value.outfit = "UnderUpper/UnderUpper_SimpleUnderShirt2";
				}
				else if (value.outfit == "UnderUpper/UnderUpper_SimpleUnderShirt2" && !flag)
				{
					value.outfit = "UnderUpper/UnderUpper_SimpleUnderShirt";
				}
				data.outfitDatas[i] = value;
			}
			data.material = (flag ? "BSMC_AnimeShader" : "BSMC_StylizedShader");
		}

		private static bool IsAnimeCharacter(CharacterData data)
		{
			for (int i = 0; i < data.outfitDatas.Count; i++)
			{
				string outfit = data.outfitDatas[i].outfit;
				if (outfit.StartsWith("Head/", StringComparison.Ordinal))
				{
					if (outfit.IndexOf("Anime", StringComparison.OrdinalIgnoreCase) < 0 && !outfit.EndsWith("Head_Young", StringComparison.Ordinal))
					{
						return outfit.EndsWith("Head_Young_v2", StringComparison.Ordinal);
					}
					return true;
				}
			}
			if (data.material != null)
			{
				return data.material.IndexOf("Anime", StringComparison.OrdinalIgnoreCase) >= 0;
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
	}
}
