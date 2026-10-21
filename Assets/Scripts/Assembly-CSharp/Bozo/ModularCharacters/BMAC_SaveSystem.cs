using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Bozo.ModularCharacters
{
	public static class BMAC_SaveSystem
	{
		public static string filePath = Application.persistentDataPath + "/BoZo_StylizedModularCharacters/CustomCharacters";

		public static string iconFilePath = Application.persistentDataPath + "/BoZo_StylizedModularCharacters/CustomCharacters/Icons";

		public static void EnsureSaveDirectories()
		{
			Directory.CreateDirectory(filePath);
			Directory.CreateDirectory(iconFilePath);
		}

		public static void SaveCharacter(OutfitSystem outfitSystem, string saveName, Texture2D icon = null)
		{
			EnsureSaveDirectories();
			CharacterData characterData = GetCharacterData(outfitSystem);
			characterData.characterName = saveName;
			string contents = JsonUtility.ToJson(characterData);
			File.WriteAllText(filePath + "/" + saveName + ".json", contents);
			Debug.Log("Character Saved:" + filePath + "/" + saveName + ".json");
		}

		public static CharacterData GetCharacterData(OutfitSystem outfitSystem)
		{
			CharacterData characterData = new CharacterData();
			characterData.versionID = 2;
			characterData.material = ResolveMaterialPackageId(outfitSystem);
			Dictionary<string, float> bodyShapeValues = outfitSystem.GetBodyShapeValues();
			characterData.bodyIDs = bodyShapeValues.Keys.ToList();
			characterData.bodyShapes = bodyShapeValues.Values.ToList();
			Dictionary<string, float> faceShapeValues = outfitSystem.GetFaceShapeValues();
			characterData.faceIDs = faceShapeValues.Keys.ToList();
			characterData.faceShapes = faceShapeValues.Values.ToList();
			List<BodyModData> list = new List<BodyModData>();
			List<string> list2 = outfitSystem.bodyModifiers.Keys.ToList();
			for (int i = 0; i < list2.Count; i++)
			{
				BodyModData data = outfitSystem.bodyModifiers[list2[i]].GetData();
				list.Add(data);
			}
			characterData.bodyMods = list;
			characterData.bodyModsKeys = list2;
			List<Outfit> outfits = outfitSystem.GetOutfits();
			List<OutfitData> list3 = new List<OutfitData>();
			for (int j = 0; j < outfits.Count; j++)
			{
				if (!(outfits[j] == null))
				{
					list3.Add(outfits[j].GetOutfitData());
				}
			}
			characterData.stance = outfitSystem.stance;
			characterData.outfitDatas = list3;
			return characterData;
		}

		private static string ResolveMaterialPackageId(OutfitSystem outfitSystem)
		{
			string currentMaterialPackageId = outfitSystem.CurrentMaterialPackageId;
			if (!string.IsNullOrWhiteSpace(currentMaterialPackageId) && Resources.Load<MaterialPackage>(currentMaterialPackageId) != null)
			{
				return currentMaterialPackageId;
			}
			if (!outfitSystem.GetOutfits().Any((Outfit outfit) => outfit != null && ((outfit.Type != null && string.Equals(outfit.Type.name, "Body", StringComparison.Ordinal) && outfit.name.IndexOf("Anime", StringComparison.OrdinalIgnoreCase) >= 0) || (outfit.tags != null && outfit.tags.Any((string tag) => string.Equals(tag, "Anime", StringComparison.OrdinalIgnoreCase))))))
			{
				return "BSMC_StylizedShader";
			}
			return "BSMC_AnimeShader";
		}

		public static async Task LoadCharacter(OutfitSystem outfitSystem, CharacterData characterObject, bool manualShapeApply = false, bool async = false, CancellationToken cancellationToken = default(CancellationToken))
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (outfitSystem == null || characterObject == null || outfitSystem.isStatic)
			{
				return;
			}
			if (outfitSystem.isloading)
			{
				Debug.LogWarning(outfitSystem.name + " is already trying to load a save. Please wait until its finished before loading another one");
				return;
			}
			outfitSystem.isloading = true;
			bool loadAsync = outfitSystem.async || cancellationToken.CanBeCanceled;
			SkinnedMeshRenderer characterBody = outfitSystem.GetCharacterBody();
			try
			{
				if (characterBody != null)
				{
					characterBody.enabled = false;
				}
				CharacterData loadData = Bozo_SavePatcher.UpdateSave(CloneCharacterData(characterObject));
				if (loadData == null)
				{
					return;
				}
				MaterialPackage materialPackage;
				if (loadAsync)
				{
					ResourceRequest materialRequest = Resources.LoadAsync<MaterialPackage>(loadData.material);
					while (!materialRequest.isDone)
					{
						await Task.Yield();
						cancellationToken.ThrowIfCancellationRequested();
					}
					materialPackage = materialRequest.asset as MaterialPackage;
				}
				else
				{
					materialPackage = Resources.Load<MaterialPackage>(loadData.material);
				}
				cancellationToken.ThrowIfCancellationRequested();
				if (materialPackage != null)
				{
					outfitSystem.UpdateMaterial(materialPackage);
				}
				try
				{
					loadData.outfitDatas = loadData.outfitDatas.OrderByDescending((OutfitData item) => item.outfit.Contains("Head")).ToList();
				}
				catch
				{
				}
				List<Outfit> outfits = new List<Outfit>();
				foreach (OutfitData outfitData2 in loadData.outfitDatas)
				{
					if (loadAsync)
					{
						ResourceRequest materialRequest = Resources.LoadAsync<Outfit>(outfitData2.outfit);
						while (!materialRequest.isDone)
						{
							await Task.Yield();
							cancellationToken.ThrowIfCancellationRequested();
						}
						outfits.Add(materialRequest.asset as Outfit);
					}
					else
					{
						outfits.Add(Resources.Load<Outfit>(outfitData2.outfit));
					}
				}
				cancellationToken.ThrowIfCancellationRequested();
				outfitSystem.RemoveAllOutfits();
				for (int i = 0; i < loadData.outfitDatas.Count; i++)
				{
					try
					{
						OutfitData outfitData = loadData.outfitDatas[i];
						Outfit outfit = outfits[i];
						if (outfit == null)
						{
							Debug.LogWarning("Outfit Path: " + outfitData.outfit + " returns null make sure Prefab is named correctly");
							continue;
						}
						Outfit outfit2 = UnityEngine.Object.Instantiate(outfit, outfitSystem.transform);
						outfit2.Attach(outfitSystem);
						if (loadData.outfitDatas[i].decalDatas != null)
						{
							List<DecalData> list = new List<DecalData>(loadData.outfitDatas[i].decalDatas);
							for (int num = 0; num < list.Count; num++)
							{
								DecalData value = list[num];
								value.decalTexture = Resources.Load<Texture>("Decal/" + value.decal);
								list[num] = value;
							}
							outfit2.decalDatas = list;
						}
						try
						{
							for (int num2 = 0; num2 < 9; num2++)
							{
								if (outfitData.colors.Count > num2)
								{
									outfit2.SetColor(outfitData.colors[num2], num2 + 1);
								}
								if (num2 + 1 <= 3 && !string.IsNullOrEmpty(outfitData.pattern) && outfitData.patternColors != null && outfitData.patternColors.Count > num2)
								{
									outfit2.SetPatternColor(outfitData.patternColors[num2], num2 + 1);
								}
							}
						}
						catch
						{
							Debug.LogWarning(loadData.outfitDatas[i].outfit + " Colors has failed loading");
						}
						Texture pattern = Resources.Load<Texture>(outfitData.pattern);
						outfit2.SetPattern(pattern);
						outfit2.SetPatternSize(outfitData.patternScale);
						if (outfit2.customShader)
						{
							outfit2.SetSwatch(outfitData.swatch);
							outfit2.SetColor(outfitData.color);
						}
						try
						{
							if (loadData.outfitDatas[i].partVisibility != null)
							{
								int num3 = Mathf.Min(loadData.outfitDatas[i].partVisibility.Length, (outfit2.optionalPieces != null) ? outfit2.optionalPieces.Length : 0);
								for (int num4 = 0; num4 < num3; num4++)
								{
									if (!(outfit2.optionalPieces[num4] == null))
									{
										outfit2.optionalPieces[num4].SetActive(loadData.outfitDatas[i].partVisibility[num4]);
									}
								}
							}
						}
						catch
						{
							Debug.LogError($"{outfit2}Something Went Wrong");
						}
					}
					catch
					{
						Debug.LogWarning(loadData.outfitDatas[i].outfit + " Has failed loading");
					}
					if (loadAsync)
					{
						await Task.Yield();
					}
					cancellationToken.ThrowIfCancellationRequested();
				}
				outfitSystem.isloading = false;
				await outfitSystem.MergeCharacter(cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
				if (characterBody != null)
				{
					characterBody.enabled = true;
				}
				outfitSystem.ResetShapes();
				int num5 = Mathf.Min(loadData.bodyIDs.Count, loadData.bodyShapes.Count);
				for (int num6 = 0; num6 < num5; num6++)
				{
					outfitSystem.SetShape(loadData.bodyIDs[num6], loadData.bodyShapes[num6]);
				}
				int num7 = Mathf.Min(loadData.faceIDs.Count, loadData.faceShapes.Count);
				for (int num8 = 0; num8 < num7; num8++)
				{
					outfitSystem.SetShape(loadData.faceIDs[num8], loadData.faceShapes[num8]);
				}
				LoadBodyMods(outfitSystem, loadData);
				outfitSystem.SetStance(loadData.stance);
				await Task.Yield();
				cancellationToken.ThrowIfCancellationRequested();
				outfitSystem.RebuildClothAfterBodyChanges();
				outfitSystem.OnCharacterLoaded?.Invoke();
			}
			finally
			{
				if (outfitSystem != null)
				{
					outfitSystem.isloading = false;
				}
				if (characterBody != null)
				{
					characterBody.enabled = true;
				}
			}
		}

		public static void LoadBodyMods(OutfitSystem outfitSystem, CharacterData loadData)
		{
			if (outfitSystem == null || loadData?.bodyModsKeys == null || loadData.bodyMods == null)
			{
				return;
			}
			int num = Mathf.Min(loadData.bodyModsKeys.Count, loadData.bodyMods.Count);
			for (int i = 0; i < num; i++)
			{
				if (loadData.bodyMods[i] != null && outfitSystem.bodyModifiers.ContainsKey(loadData.bodyModsKeys[i]))
				{
					outfitSystem.bodyModifiers[loadData.bodyModsKeys[i]].SetData(loadData.bodyMods[i]);
				}
			}
		}

		private static CharacterData CloneCharacterData(CharacterData source)
		{
			if (source == null)
			{
				return null;
			}
			return JsonUtility.FromJson<CharacterData>(JsonUtility.ToJson(source));
		}

		public static CharacterData GetDataFromID(string saveName)
		{
			Debug.Log("Attempted Load at: " + filePath + "/" + saveName + ".json");
			if (!File.Exists(filePath + "/" + saveName + ".json"))
			{
				Debug.LogWarning("Save ID: " + saveName + " does not exist. Make sure input matches an existing Save");
				return null;
			}
			return JsonUtility.FromJson<CharacterData>(File.ReadAllText(filePath + "/" + saveName + ".json"));
		}

		public static async Task<List<Outfit>> LoadOutfits(List<OutfitData> outfitDatas)
		{
			return (await Task.WhenAll(outfitDatas.Select((OutfitData data) => LoadResourceAsync<Outfit>(data.outfit)))).ToList();
		}

		public static async Task<T> LoadResourceAsync<T>(string path) where T : UnityEngine.Object
		{
			ResourceRequest request = Resources.LoadAsync<T>(path);
			TaskCompletionSource<T> tcs = new TaskCompletionSource<T>();
			request.completed += (AsyncOperation operation) =>
			{
				if (request.asset == null)
				{
					tcs.SetResult(null);
				}
				else if (!(request.asset is T result))
				{
					tcs.SetResult(null);
				}
				else
				{
					tcs.SetResult(result);
				}
			};
			return await tcs.Task;
		}

		public static void DeleteCharacter(string characterName)
		{
			File.Delete(filePath + "/" + characterName + ".json");
			File.Delete(iconFilePath + "/" + characterName + ".png");
		}
	}
}
