using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using Bozo.ModularCharacters;
using Cysharp.Threading.Tasks;
using PaperPixelGames.Core.Streaming;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using UnityEngine;

namespace PaperPixelGames.Core.Player.Visuals
{
	public static class PlayerCustomizationSession
	{
		private const double OutfitLoadWaitSeconds = 180.0;

		private const string StanceAnimatorParameter = "Stance";

		private const string SaveDirectoryName = "CharacterCustomization";

		private const string SaveFileName = "last-character.json";

		private static readonly string[] FirstPersonAllowedOutfitPrefixes = new string[2] { "Body/", "Gloves/" };

		private static readonly HashSet<string> FirstPersonVisibleBodyTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CoverHands", "CoverLowerArms", "CoverShoulders", "CoverUpperArms", "CoverWrist" };

		private static readonly HashSet<string> FirstPersonHiddenBodyTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CoverBack", "CoverLowerLegs", "CoverChest", "CoverFeet", "CoverHips", "CoverNeck", "CoverUpperLegs", "CoverStomach", "CoverAnkles" };

		private static readonly FieldInfo HideSettingsField = typeof(OutfitHideByTag).GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic);

		private static FieldInfo _hideSettingRendererField;

		private static FieldInfo _hideSettingTagField;

		private static CharacterData _currentCharacterData;

		private static bool _persistentLoadAttempted;

		private static string SaveDirectoryPath => Path.Combine(Application.persistentDataPath, "CharacterCustomization");

		private static string SaveFilePath => Path.Combine(SaveDirectoryPath, "last-character.json");

		public static bool CaptureFromMenuPuppets(IReadOnlyList<GameObject> puppetRoots)
		{
			if (puppetRoots == null)
			{
				return false;
			}
			for (int i = 0; i < puppetRoots.Count; i++)
			{
				GameObject gameObject = puppetRoots[i];
				if (!(gameObject == null) && gameObject.activeInHierarchy)
				{
					OutfitSystem componentInChildren = gameObject.GetComponentInChildren<OutfitSystem>(includeInactive: true);
					if (!(componentInChildren == null) && !componentInChildren.isStatic)
					{
						return CaptureFromOutfitSystem(componentInChildren);
					}
				}
			}
			return false;
		}

		public static bool SetCurrentCharacterData(CharacterData characterData, bool persist)
		{
			if (characterData == null)
			{
				return false;
			}
			CharacterData characterData2 = Bozo_SavePatcher.UpdateSave(CloneCharacterData(characterData));
			if (!IsFullBodyCharacterDataValid(characterData2))
			{
				UtilityMethods.Log("[PlayerCustomizationSession] Rejected character data because required Head or Body outfit data is missing.", LogType.Warning);
				return false;
			}
			_currentCharacterData = CloneCharacterData(characterData2);
			_persistentLoadAttempted = true;
			if (!persist)
			{
				return true;
			}
			return SaveCurrentCharacterData();
		}

		public static CharacterData GetCurrentCharacterData()
		{
			EnsurePersistentDataLoaded();
			return CloneCharacterData(_currentCharacterData);
		}

		public static CharacterData CloneCharacterDataForRuntime(CharacterData source)
		{
			return CloneCharacterData(source);
		}

		public static bool EnsureInitialCharacterData()
		{
			EnsurePersistentDataLoaded();
			if (_currentCharacterData != null)
			{
				return true;
			}
			CharacterObject[] array = Resources.LoadAll<CharacterObject>(string.Empty);
			List<CharacterObject> list = new List<CharacterObject>();
			foreach (CharacterObject characterObject in array)
			{
				if (characterObject != null && characterObject.ShowInCharacterList && characterObject.data?.outfitDatas != null)
				{
					list.Add(characterObject);
				}
			}
			if (list.Count == 0)
			{
				UtilityMethods.Log("[PlayerCustomizationSession] Initial character could not be created because no visible character preset was found.", LogType.Error);
				return false;
			}
			CharacterObject characterObject2 = list[UnityEngine.Random.Range(0, list.Count)];
			bool flag = SetCurrentCharacterData(characterObject2.data, persist: true);
			UtilityMethods.Log(flag ? ("[PlayerCustomizationSession] Initialized and saved the first character from preset '" + characterObject2.name + "'.") : ("[PlayerCustomizationSession] Initialized the first character from preset '" + characterObject2.name + "', but persistence failed."), flag ? LogType.Log : LogType.Warning);
			return _currentCharacterData != null;
		}

		public static bool TryGetCurrentCharacterDataPayload(out byte[] payload)
		{
			payload = null;
			EnsurePersistentDataLoaded();
			if (_currentCharacterData == null)
			{
				return false;
			}
			try
			{
				string s = JsonUtility.ToJson(_currentCharacterData, prettyPrint: false);
				payload = Encoding.UTF8.GetBytes(s);
				return payload.Length != 0;
			}
			catch (Exception ex)
			{
				UtilityMethods.Log("[PlayerCustomizationSession] Failed to serialize current character data: " + ex.Message, LogType.Warning);
				return false;
			}
		}

		public static bool TryReadCharacterDataPayload(byte[] payload, out CharacterData characterData)
		{
			characterData = null;
			if (payload == null || payload.Length == 0)
			{
				return false;
			}
			try
			{
				CharacterData characterData2 = Bozo_SavePatcher.UpdateSave(CloneCharacterData(JsonUtility.FromJson<CharacterData>(Encoding.UTF8.GetString(payload))));
				if (!IsFullBodyCharacterDataValid(characterData2))
				{
					return false;
				}
				characterData = CloneCharacterData(characterData2);
				return true;
			}
			catch (Exception ex)
			{
				UtilityMethods.Log("[PlayerCustomizationSession] Failed to deserialize character payload: " + ex.Message, LogType.Warning);
				return false;
			}
		}

		private static bool CaptureFromOutfitSystem(OutfitSystem outfitSystem)
		{
			if (!IsOutfitSystemReadyForCapture(outfitSystem))
			{
				UtilityMethods.Log("[PlayerCustomizationSession] Skipped menu puppet capture because its customization is not ready.", LogType.Warning);
				return false;
			}
			try
			{
				bool flag = SetCurrentCharacterData(BMAC_SaveSystem.GetCharacterData(outfitSystem), persist: true);
				UtilityMethods.Log(flag ? "[PlayerCustomizationSession] Captured and saved menu puppet customization." : "[PlayerCustomizationSession] Captured menu puppet customization, but persistence failed.", flag ? LogType.Log : LogType.Warning);
				return flag;
			}
			catch (Exception ex)
			{
				UtilityMethods.Log("[PlayerCustomizationSession] Failed to capture menu puppet customization: " + ex.Message, LogType.Warning);
				return false;
			}
		}

		public static async UniTask<bool> ApplyToLocalPlayerAsync(PPG_Player player, CancellationToken cancellationToken = default(CancellationToken))
		{
			cancellationToken.ThrowIfCancellationRequested();
			EnsurePersistentDataLoaded();
			if (_currentCharacterData == null || player == null || player.CharacterVisuals == null)
			{
				return false;
			}
			PlayerCharacterVisuals visuals = player.CharacterVisuals;
			CharacterData characterData = CloneCharacterData(_currentCharacterData);
			bool tpsApplied = await ApplyCharacterDataToOutfitSystemAsync(visuals.TpsOutfitSystem, characterData, "local player TPS", PlayerCustomizationViewMode.FullBody, cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			bool flag = await ApplyCharacterDataToOutfitSystemAsync(visuals.FpsOutfitSystem, characterData, "local player FPS", PlayerCustomizationViewMode.FirstPersonViewModel, cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			if (player == null || visuals == null)
			{
				return false;
			}
			visuals.RefreshForOwner(player.IsOwner);
			player.RefreshDissonanceHeadRenderer();
			return tpsApplied & flag;
		}

		public static async UniTask<bool> ApplyToRemotePlayerAsync(PPG_Player player, byte[] payload, CancellationToken cancellationToken = default(CancellationToken))
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (player == null || player.CharacterVisuals == null)
			{
				return false;
			}
			if (!TryReadCharacterDataPayload(payload, out var characterData))
			{
				return false;
			}
			OutfitSystem outfitSystem = player.CharacterVisuals.TpsOutfitSystem;
			bool flag = await ApplyCharacterDataToOutfitSystemAsync(outfitSystem, characterData, $"remote player {player.OwnerClientId} TPS", PlayerCustomizationViewMode.FullBody, cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			if (player == null)
			{
				return false;
			}
			if (!flag || !IsRemoteTpsRenderReady(outfitSystem))
			{
				UtilityMethods.Log($"[PlayerCustomizationSession] Remote TPS validation failed for player {player.OwnerClientId}.", LogType.Warning);
				return false;
			}
			player.CharacterVisuals.RefreshForOwner(player.IsOwner);
			player.RefreshDissonanceHeadRenderer();
			return true;
		}

		private static bool IsRemoteTpsRenderReady(OutfitSystem outfitSystem)
		{
			if (outfitSystem == null || outfitSystem.isloading || outfitSystem.isMerging || outfitSystem.isUpdatingTexture)
			{
				return false;
			}
			SkinnedMeshRenderer characterBody = outfitSystem.GetCharacterBody();
			if (characterBody == null || !characterBody.enabled || !characterBody.gameObject.activeInHierarchy || characterBody.sharedMesh == null || characterBody.sharedMesh.vertexCount <= 0)
			{
				return false;
			}
			if (outfitSystem.GetOutfit("Head") == null || outfitSystem.GetOutfit("Body") == null)
			{
				return false;
			}
			if (outfitSystem.combinedMesh == null || characterBody.sharedMesh != outfitSystem.combinedMesh)
			{
				return false;
			}
			Material[] sharedMaterials = characterBody.sharedMaterials;
			if (sharedMaterials == null || sharedMaterials.Length == 0)
			{
				return false;
			}
			Bounds localBounds = characterBody.localBounds;
			Vector3 center = localBounds.center;
			Vector3 extents = localBounds.extents;
			if (IsFinite(center.x) && IsFinite(center.y) && IsFinite(center.z) && IsFinite(extents.x) && IsFinite(extents.y) && IsFinite(extents.z))
			{
				return extents.sqrMagnitude > 0.0001f;
			}
			return false;
		}

		private static bool IsFinite(float value)
		{
			if (!float.IsNaN(value))
			{
				return !float.IsInfinity(value);
			}
			return false;
		}

		public static async UniTask<bool> ApplyCharacterDataToOutfitSystemAsync(OutfitSystem outfitSystem, CharacterData characterData, string targetLabel, PlayerCustomizationViewMode viewMode = PlayerCustomizationViewMode.FullBody, CancellationToken cancellationToken = default(CancellationToken))
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (outfitSystem == null || outfitSystem.isStatic || !IsFullBodyCharacterDataValid(characterData))
			{
				return false;
			}
			try
			{
				await WaitForDressingStartupStreamingAsync(cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
				if (outfitSystem == null)
				{
					return false;
				}
				if (!outfitSystem.initalized)
				{
					outfitSystem.Init();
				}
				if (!(await WaitForOutfitSystemLoadAsync(outfitSystem, targetLabel, cancellationToken)))
				{
					return false;
				}
				float stance = GetCurrentStance(outfitSystem);
				CharacterData source = Bozo_SavePatcher.UpdateSave(CloneCharacterData(characterData));
				CharacterData sourceData = CreateViewCharacterData(source, viewMode);
				CharacterData characterData2 = CloneCharacterData(sourceData);
				characterData2.stance = stance;
				PrepareForVendorLoad(characterData2);
				if (viewMode != PlayerCustomizationViewMode.FirstPersonViewModel)
				{
					await BMAC_SaveSystem.LoadCharacter(outfitSystem, characterData2, manualShapeApply: false, outfitSystem.async, cancellationToken);
				}
				else if (!(await LoadFirstPersonCharacterAsync(outfitSystem, characterData2, cancellationToken)))
				{
					return false;
				}
				cancellationToken.ThrowIfCancellationRequested();
				await ApplyVendorLoadGapsAsync(outfitSystem, sourceData, cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
				if (viewMode == PlayerCustomizationViewMode.FirstPersonViewModel)
				{
					outfitSystem.MuteBodyMods(mute: true);
				}
				outfitSystem.SetStance(stance);
				UtilityMethods.Log("[PlayerCustomizationSession] Applied customization to " + targetLabel + ".");
				return true;
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex2)
			{
				UtilityMethods.Log("[PlayerCustomizationSession] Failed to apply customization to " + targetLabel + ": " + ex2.Message, LogType.Warning);
				return false;
			}
		}

		private static async UniTask WaitForDressingStartupStreamingAsync(CancellationToken cancellationToken)
		{
			DressingSceneManager instance = Singleton<DressingSceneManager>.Instance;
			while (instance != null && instance.IsStartupStreamingBusy)
			{
				await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
				instance = Singleton<DressingSceneManager>.Instance;
			}
		}

		private static CharacterData CreateViewCharacterData(CharacterData source, PlayerCustomizationViewMode viewMode)
		{
			CharacterData characterData = CloneCharacterData(source);
			if (characterData == null || viewMode == PlayerCustomizationViewMode.FullBody)
			{
				return characterData;
			}
			if (characterData.outfitDatas == null)
			{
				characterData.outfitDatas = new List<OutfitData>();
			}
			for (int num = characterData.outfitDatas.Count - 1; num >= 0; num--)
			{
				if (!IsFirstPersonOutfit(characterData.outfitDatas[num].outfit))
				{
					characterData.outfitDatas.RemoveAt(num);
				}
			}
			ClearFirstPersonShapeData(characterData);
			return characterData;
		}

		private static void ClearFirstPersonShapeData(CharacterData data)
		{
			if (data != null)
			{
				data.bodyIDs?.Clear();
				data.bodyShapes?.Clear();
				data.faceIDs?.Clear();
				data.faceShapes?.Clear();
				data.bodyModsKeys?.Clear();
				data.bodyMods?.Clear();
			}
		}

		private static bool IsFirstPersonOutfit(string outfitPath)
		{
			if (string.IsNullOrWhiteSpace(outfitPath))
			{
				return false;
			}
			for (int i = 0; i < FirstPersonAllowedOutfitPrefixes.Length; i++)
			{
				if (outfitPath.StartsWith(FirstPersonAllowedOutfitPrefixes[i], StringComparison.Ordinal))
				{
					return true;
				}
			}
			return false;
		}

		private static async UniTask<bool> LoadFirstPersonCharacterAsync(OutfitSystem outfitSystem, CharacterData characterData, CancellationToken cancellationToken)
		{
			if (outfitSystem.isloading)
			{
				UtilityMethods.Log("[PlayerCustomizationSession] " + outfitSystem.name + " is already loading customization.", LogType.Warning);
				return false;
			}
			outfitSystem.isloading = true;
			SkinnedMeshRenderer characterBody = outfitSystem.GetCharacterBody();
			try
			{
				if (characterBody != null)
				{
					characterBody.enabled = false;
				}
				List<Outfit> loadedOutfits = await LoadOutfitPrefabsAsync(outfitSystem, characterData.outfitDatas, cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
				outfitSystem.RemoveAllOutfits();
				for (int i = 0; i < characterData.outfitDatas.Count; i++)
				{
					OutfitData outfitData = characterData.outfitDatas[i];
					Outfit outfit = ((i < loadedOutfits.Count) ? loadedOutfits[i] : null);
					if (outfit == null)
					{
						UtilityMethods.Log("[PlayerCustomizationSession] FPS outfit path failed to load: " + outfitData.outfit, LogType.Warning);
						continue;
					}
					Outfit outfit2 = UnityEngine.Object.Instantiate(outfit, outfitSystem.transform);
					outfit2.Attach(outfitSystem);
					ApplyOutfitData(outfit2, outfitData);
					if (outfitSystem.async || cancellationToken.CanBeCanceled)
					{
						await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
					}
				}
				ApplyFirstPersonBodyPartVisibility(outfitSystem);
				outfitSystem.isloading = false;
				await outfitSystem.MergeCharacter(cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
				if (characterBody != null)
				{
					characterBody.enabled = true;
				}
				outfitSystem.ResetShapes();
				ApplyShapes(outfitSystem, characterData.bodyIDs, characterData.bodyShapes);
				ApplyShapes(outfitSystem, characterData.faceIDs, characterData.faceShapes);
				outfitSystem.MuteBodyMods(mute: true);
				outfitSystem.SetStance(characterData.stance);
				outfitSystem.OnCharacterLoaded?.Invoke();
				return true;
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

		private static async UniTask<List<Outfit>> LoadOutfitPrefabsAsync(OutfitSystem outfitSystem, List<OutfitData> outfitDatas, CancellationToken cancellationToken)
		{
			List<Outfit> outfits = new List<Outfit>();
			if (outfitDatas == null)
			{
				return outfits;
			}
			for (int i = 0; i < outfitDatas.Count; i++)
			{
				string outfit = outfitDatas[i].outfit;
				if (outfitSystem.async || cancellationToken.CanBeCanceled)
				{
					ResourceRequest request = Resources.LoadAsync<Outfit>(outfit);
					while (!request.isDone)
					{
						await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
					}
					cancellationToken.ThrowIfCancellationRequested();
					outfits.Add(request.asset as Outfit);
				}
				else
				{
					outfits.Add(Resources.Load<Outfit>(outfit));
				}
			}
			return outfits;
		}

		private static void ApplyOutfitData(Outfit outfit, OutfitData outfitData)
		{
			if (outfit == null)
			{
				return;
			}
			if (outfitData.decalDatas != null)
			{
				outfit.decalDatas = new List<DecalData>(outfitData.decalDatas);
			}
			if (outfitData.colors != null)
			{
				int num = Mathf.Min(9, outfitData.colors.Count);
				for (int i = 0; i < num; i++)
				{
					outfit.SetColor(outfitData.colors[i], i + 1);
				}
			}
			if (!string.IsNullOrEmpty(outfitData.pattern) && outfitData.patternColors != null)
			{
				int num2 = Mathf.Min(3, outfitData.patternColors.Count);
				for (int j = 0; j < num2; j++)
				{
					outfit.SetPatternColor(outfitData.patternColors[j], j + 1);
				}
			}
			if (!string.IsNullOrEmpty(outfitData.pattern))
			{
				Texture pattern = Resources.Load<Texture>(outfitData.pattern);
				outfit.SetPattern(pattern);
			}
			outfit.SetPatternSize(outfitData.patternScale);
			if (outfitData.partVisibility != null && outfit.optionalPieces != null)
			{
				int num3 = Mathf.Min(outfitData.partVisibility.Length, outfit.optionalPieces.Length);
				for (int k = 0; k < num3; k++)
				{
					if (outfit.optionalPieces[k] != null)
					{
						outfit.optionalPieces[k].SetActive(outfitData.partVisibility[k]);
					}
				}
			}
			if (outfit.customShader)
			{
				outfit.SetSwatch(outfitData.swatch);
				outfit.SetColor(outfitData.color);
			}
		}

		private static void ApplyShapes(OutfitSystem outfitSystem, List<string> ids, List<float> values)
		{
			if (ids != null && values != null)
			{
				int num = Mathf.Min(ids.Count, values.Count);
				for (int i = 0; i < num; i++)
				{
					outfitSystem.SetShape(ids[i], values[i]);
				}
			}
		}

		private static void ApplyFirstPersonBodyPartVisibility(OutfitSystem outfitSystem)
		{
			if (!(outfitSystem == null) && !(HideSettingsField == null))
			{
				OutfitHideByTag[] componentsInChildren = outfitSystem.GetComponentsInChildren<OutfitHideByTag>(includeInactive: true);
				for (int i = 0; i < componentsInChildren.Length; i++)
				{
					ApplyFirstPersonBodyPartVisibility(componentsInChildren[i]);
				}
			}
		}

		private static void ApplyFirstPersonBodyPartVisibility(OutfitHideByTag hideByTag)
		{
			if (hideByTag == null)
			{
				return;
			}
			try
			{
				hideByTag.OnDestroy();
				hideByTag.enabled = false;
			}
			catch (Exception ex)
			{
				UtilityMethods.Log("[PlayerCustomizationSession] Failed to detach FPS body hide extension: " + ex.Message, LogType.Warning);
			}
			if (!(HideSettingsField.GetValue(hideByTag) is Array array))
			{
				return;
			}
			EnsureHideSettingFields(array);
			if (_hideSettingRendererField == null || _hideSettingTagField == null)
			{
				return;
			}
			for (int i = 0; i < array.Length; i++)
			{
				object value = array.GetValue(i);
				if (value == null)
				{
					continue;
				}
				SkinnedMeshRenderer skinnedMeshRenderer = _hideSettingRendererField.GetValue(value) as SkinnedMeshRenderer;
				string text = _hideSettingTagField.GetValue(value) as string;
				if (!(skinnedMeshRenderer == null) && !string.IsNullOrWhiteSpace(text))
				{
					if (FirstPersonVisibleBodyTags.Contains(text))
					{
						skinnedMeshRenderer.gameObject.SetActive(value: true);
					}
					else if (FirstPersonHiddenBodyTags.Contains(text))
					{
						skinnedMeshRenderer.gameObject.SetActive(value: false);
					}
				}
			}
		}

		private static void EnsureHideSettingFields(Array settings)
		{
			if (!(_hideSettingRendererField != null) || !(_hideSettingTagField != null))
			{
				Type elementType = settings.GetType().GetElementType();
				if (!(elementType == null))
				{
					_hideSettingRendererField = elementType.GetField("renderer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					_hideSettingTagField = elementType.GetField("tag", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				}
			}
		}

		private static async UniTask<bool> WaitForOutfitSystemLoadAsync(OutfitSystem outfitSystem, string targetLabel, CancellationToken cancellationToken)
		{
			double deadline = Time.realtimeSinceStartupAsDouble + 180.0;
			while (outfitSystem != null && (outfitSystem.isloading || outfitSystem.isMerging || outfitSystem.isUpdatingTexture) && Time.realtimeSinceStartupAsDouble < deadline)
			{
				await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
			}
			bool num = outfitSystem != null && !outfitSystem.isloading && !outfitSystem.isMerging && !outfitSystem.isUpdatingTexture;
			if (!num)
			{
				UtilityMethods.Log("[PlayerCustomizationSession] Customization load wait timed out for " + targetLabel + ".", LogType.Warning);
			}
			return num;
		}

		private static float GetCurrentStance(OutfitSystem outfitSystem)
		{
			if (outfitSystem == null)
			{
				return 0f;
			}
			Animator animator = outfitSystem.animator;
			if (animator != null)
			{
				AnimatorControllerParameter[] parameters = animator.parameters;
				for (int i = 0; i < parameters.Length; i++)
				{
					if (parameters[i].type == AnimatorControllerParameterType.Float && string.Equals(parameters[i].name, "Stance", StringComparison.Ordinal))
					{
						return animator.GetFloat("Stance");
					}
				}
			}
			return outfitSystem.stance;
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

		private static async UniTask ApplyVendorLoadGapsAsync(OutfitSystem outfitSystem, CharacterData sourceData, CancellationToken cancellationToken)
		{
			if (sourceData?.outfitDatas == null)
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
				if (outfitSystem != null)
				{
					outfitSystem.isloading = false;
				}
			}
			if (flag)
			{
				await outfitSystem.MergeCharacter(cancellationToken);
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

		private static void EnsurePersistentDataLoaded()
		{
			if (_persistentLoadAttempted)
			{
				return;
			}
			_persistentLoadAttempted = true;
			try
			{
				if (File.Exists(SaveFilePath))
				{
					_currentCharacterData = CloneCharacterData(Bozo_SavePatcher.UpdateSave(CloneCharacterData(JsonUtility.FromJson<CharacterData>(File.ReadAllText(SaveFilePath)))));
					if (!IsFullBodyCharacterDataValid(_currentCharacterData))
					{
						throw new InvalidDataException("Migrated character data is missing a required Head or Body outfit.");
					}
					UtilityMethods.Log("[PlayerCustomizationSession] Loaded last character from " + SaveFilePath + ".");
				}
			}
			catch (Exception ex)
			{
				_currentCharacterData = null;
				UtilityMethods.Log("[PlayerCustomizationSession] Failed to load last character: " + ex.Message, LogType.Warning);
			}
		}

		private static bool SaveCurrentCharacterData()
		{
			if (_currentCharacterData == null)
			{
				return false;
			}
			try
			{
				Directory.CreateDirectory(SaveDirectoryPath);
				string contents = JsonUtility.ToJson(_currentCharacterData, prettyPrint: true);
				File.WriteAllText(SaveFilePath, contents);
				return true;
			}
			catch (Exception ex)
			{
				UtilityMethods.Log("[PlayerCustomizationSession] Failed to save last character: " + ex.Message, LogType.Warning);
				return false;
			}
		}

		private static bool IsOutfitSystemReadyForCapture(OutfitSystem outfitSystem)
		{
			if (outfitSystem != null && !outfitSystem.isStatic && outfitSystem.initalized && !outfitSystem.isloading && !outfitSystem.isMerging)
			{
				return !outfitSystem.isUpdatingTexture;
			}
			return false;
		}

		private static bool IsFullBodyCharacterDataValid(CharacterData characterData)
		{
			if (characterData?.outfitDatas == null || characterData.outfitDatas.Count == 0)
			{
				return false;
			}
			bool flag = false;
			bool flag2 = false;
			for (int i = 0; i < characterData.outfitDatas.Count; i++)
			{
				string outfit = characterData.outfitDatas[i].outfit;
				if (!string.IsNullOrWhiteSpace(outfit))
				{
					flag |= outfit.StartsWith("Head/", StringComparison.Ordinal);
					flag2 |= outfit.StartsWith("Body/", StringComparison.Ordinal);
					if (flag & flag2)
					{
						return true;
					}
				}
			}
			return false;
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
	}
}
