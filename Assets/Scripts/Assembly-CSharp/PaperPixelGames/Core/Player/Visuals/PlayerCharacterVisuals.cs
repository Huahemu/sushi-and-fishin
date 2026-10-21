using System;
using System.Collections.Generic;
using System.Reflection;
using Bozo.ModularCharacters;
using PaperPixelGames.Framework.Utility;
using UnityEngine;

namespace PaperPixelGames.Core.Player.Visuals
{
	[DisallowMultipleComponent]
	public sealed class PlayerCharacterVisuals : MonoBehaviour
	{
		[Header("Outfit Systems")]
		[SerializeField]
		private OutfitSystem _tpsOutfitSystem;

		[SerializeField]
		private OutfitSystem _fpsOutfitSystem;

		[SerializeField]
		private GameObject _tpsRootObject;

		[SerializeField]
		private GameObject _fpsRootObject;

		[Header("TPS Action Visibility")]
		[SerializeField]
		private string[] _tpsActionHiddenOutfitTypeNames = new string[10] { "Head", "Eyes", "Ears", "HairFront", "HairBack", "HeadAcc", "LowerFace", "UpperFace", "Neck", "Hat" };

		[Header("Diving Equipment Visibility")]
		[SerializeField]
		private string[] _divingEquipmentHiddenOutfitTypeNames = new string[1] { "Feet" };

		[Header("FPS View Guard")]
		[SerializeField]
		private Vector3 _fpsLocalOffset = new Vector3(0f, -1.35f, 0f);

		[SerializeField]
		private bool _lockFpsOutfitSystemLocalY = true;

		[SerializeField]
		private float _fpsOutfitSystemLocalY = -1.35f;

		[SerializeField]
		private string[] _visibleBodyPartTags = new string[5] { "CoverHands", "CoverLowerArms", "CoverShoulders", "CoverUpperArms", "CoverWrist" };

		[SerializeField]
		private string[] _hiddenBodyPartTags = new string[8] { "CoverBack", "CoverLowerLegs", "CoverChest", "CoverFeet", "CoverHips", "CoverNeck", "CoverUpperLegs", "CoverStomach" };

		private static readonly string[] DefaultTpsActionHiddenOutfitTypeNames = new string[10] { "Head", "Eyes", "Ears", "HairFront", "HairBack", "HeadAcc", "LowerFace", "UpperFace", "Neck", "Hat" };

		private static readonly FieldInfo HideSettingsField = typeof(OutfitHideByTag).GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic);

		private static FieldInfo _hideSettingRendererField;

		private static FieldInfo _hideSettingTagField;

		private readonly List<Renderer> _tpsHeadLinkedRenderers = new List<Renderer>();

		private readonly Dictionary<Renderer, bool> _tpsHeadLinkedRendererStates = new Dictionary<Renderer, bool>();

		private readonly List<Renderer> _divingEquipmentFeetLinkedRenderers = new List<Renderer>();

		private readonly Dictionary<Renderer, bool> _divingEquipmentFeetLinkedRendererStates = new Dictionary<Renderer, bool>();

		private readonly HashSet<string> _visibleBodyPartTagSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		private readonly HashSet<string> _hiddenBodyPartTagSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		private bool _tpsHeadMeshesVisible = true;

		private bool _divingEquipmentFeetMeshesVisible = true;

		private bool _lastOwnerState;

		private PPG_Player _player;

		private GameObject _temporaryAppearance;

		private bool _customizationPending;

		public OutfitSystem TpsOutfitSystem => _tpsOutfitSystem;

		public OutfitSystem FpsOutfitSystem => _fpsOutfitSystem;

		public GameObject TpsRootObject => _tpsRootObject;

		public GameObject FpsRootObject => _fpsRootObject;

		public void SetCustomizationPending(bool pending)
		{
			_customizationPending = pending;
			if (pending && !_lastOwnerState && _temporaryAppearance == null)
			{
				_temporaryAppearance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
				_temporaryAppearance.name = "Pending player appearance";
				_temporaryAppearance.transform.SetParent((_player != null) ? _player.transform : base.transform, worldPositionStays: false);
				_temporaryAppearance.transform.localPosition = new Vector3(0f, 0.9f, 0f);
				_temporaryAppearance.transform.localScale = new Vector3(0.45f, 0.85f, 0.45f);
				int num = LayerMask.NameToLayer("ClientPlayer");
				if (num >= 0)
				{
					_temporaryAppearance.layer = num;
				}
				Collider component = _temporaryAppearance.GetComponent<Collider>();
				component.enabled = false;
				if (Application.isPlaying)
				{
					UnityEngine.Object.Destroy(component);
				}
				else
				{
					UnityEngine.Object.DestroyImmediate(component);
				}
				SkinnedMeshRenderer skinnedMeshRenderer = ((_tpsOutfitSystem != null) ? _tpsOutfitSystem.GetCharacterBody() : null);
				if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMaterial != null)
				{
					_temporaryAppearance.GetComponent<Renderer>().sharedMaterial = skinnedMeshRenderer.sharedMaterial;
				}
			}
			if (_temporaryAppearance != null)
			{
				_temporaryAppearance.SetActive(pending && !_lastOwnerState);
			}
		}

		private void OnDestroy()
		{
			if (!(_temporaryAppearance == null))
			{
				if (Application.isPlaying)
				{
					UnityEngine.Object.Destroy(_temporaryAppearance);
				}
				else
				{
					UnityEngine.Object.DestroyImmediate(_temporaryAppearance);
				}
			}
		}

		private void Awake()
		{
			CacheReferences();
			RebuildTagSets();
			RefreshForOwner(_player != null && _player.IsOwner);
		}

		private void OnValidate()
		{
			RebuildTagSets();
		}

		private void LateUpdate()
		{
			ApplyFpsViewGuard();
		}

		public void Configure(OutfitSystem tpsOutfitSystem, OutfitSystem fpsOutfitSystem, GameObject tpsRootObject, GameObject fpsRootObject, string[] tpsActionHiddenOutfitTypeNames, string[] divingEquipmentHiddenOutfitTypeNames, Vector3 fpsLocalOffset, string[] visibleBodyPartTags, string[] hiddenBodyPartTags)
		{
			_tpsOutfitSystem = tpsOutfitSystem;
			_fpsOutfitSystem = fpsOutfitSystem;
			_tpsRootObject = tpsRootObject;
			_fpsRootObject = fpsRootObject;
			_tpsActionHiddenOutfitTypeNames = CloneArrayOrKeepDefault(tpsActionHiddenOutfitTypeNames, _tpsActionHiddenOutfitTypeNames);
			_divingEquipmentHiddenOutfitTypeNames = CloneArrayOrKeepDefault(divingEquipmentHiddenOutfitTypeNames, _divingEquipmentHiddenOutfitTypeNames);
			_fpsLocalOffset = fpsLocalOffset;
			_visibleBodyPartTags = CloneArrayOrKeepDefault(visibleBodyPartTags, _visibleBodyPartTags);
			_hiddenBodyPartTags = CloneArrayOrKeepDefault(hiddenBodyPartTags, _hiddenBodyPartTags);
			RebuildTagSets();
			RefreshForOwner(_lastOwnerState);
		}

		public void RefreshForOwner(bool isOwner)
		{
			_lastOwnerState = isOwner;
			if (_temporaryAppearance != null)
			{
				_temporaryAppearance.SetActive(_customizationPending && !isOwner);
			}
			CacheReferences();
			if (_fpsRootObject != null)
			{
				UtilityMethods.SetLayerRecursive(_fpsRootObject, "ViewModel", includeChildren: true);
			}
			if (_tpsRootObject != null)
			{
				UtilityMethods.SetLayerRecursive(_tpsRootObject, isOwner ? "Player" : "ClientPlayer", includeChildren: true);
			}
			RebuildTpsHeadRendererCache();
			ApplyTpsHeadMeshVisibility();
			RebuildDivingEquipmentFeetRendererCache();
			ApplyDivingEquipmentFeetMeshVisibility();
			ApplyFpsViewGuard();
			RefreshDivingEquipmentHoseGuideLayers();
		}

		public void SetTpsHeadMeshesVisible(bool visible)
		{
			_tpsHeadMeshesVisible = visible;
			RebuildTpsHeadRendererCache();
			ApplyTpsHeadMeshVisibility();
		}

		public void SetDivingEquipmentFeetMeshesVisible(bool visible)
		{
			_divingEquipmentFeetMeshesVisible = visible;
			RebuildDivingEquipmentFeetRendererCache();
			ApplyDivingEquipmentFeetMeshVisibility();
		}

		private void CacheReferences()
		{
			if ((object)_player == null)
			{
				_player = GetComponentInParent<PPG_Player>();
			}
			if (_tpsOutfitSystem == null && _tpsRootObject != null)
			{
				_tpsOutfitSystem = _tpsRootObject.GetComponentInChildren<OutfitSystem>(includeInactive: true);
			}
			if (_fpsOutfitSystem == null && _fpsRootObject != null)
			{
				_fpsOutfitSystem = _fpsRootObject.GetComponentInChildren<OutfitSystem>(includeInactive: true);
			}
		}

		private void RefreshDivingEquipmentHoseGuideLayers()
		{
			if ((object)_player == null)
			{
				_player = GetComponentInParent<PPG_Player>();
			}
			_player?.DivingEquipment?.RefreshDivingHoseGuideLayers();
		}

		private void RebuildTpsHeadRendererCache()
		{
			_tpsHeadLinkedRenderers.Clear();
			if (_tpsOutfitSystem == null)
			{
				return;
			}
			Outfit[] componentsInChildren = _tpsOutfitSystem.GetComponentsInChildren<Outfit>(includeInactive: true);
			foreach (Outfit outfit in componentsInChildren)
			{
				if (!IsTpsActionHiddenOutfit(outfit))
				{
					continue;
				}
				AddUniqueRenderer(_tpsHeadLinkedRenderers, outfit.outfitRenderer);
				AddUniqueRenderer(_tpsHeadLinkedRenderers, outfit.skinnedRenderer);
				SkinnedMeshRenderer[] skinnedRenderers = outfit.skinnedRenderers;
				if (skinnedRenderers != null)
				{
					for (int j = 0; j < skinnedRenderers.Length; j++)
					{
						AddUniqueRenderer(_tpsHeadLinkedRenderers, skinnedRenderers[j]);
					}
				}
				Renderer[] componentsInChildren2 = outfit.GetComponentsInChildren<Renderer>(includeInactive: true);
				for (int k = 0; k < componentsInChildren2.Length; k++)
				{
					AddUniqueRenderer(_tpsHeadLinkedRenderers, componentsInChildren2[k]);
				}
			}
		}

		private bool IsTpsActionHiddenOutfit(Outfit outfit)
		{
			if (outfit == null || outfit.Type == null || string.IsNullOrWhiteSpace(outfit.Type.name))
			{
				return false;
			}
			if (ContainsOutfitTypeName(DefaultTpsActionHiddenOutfitTypeNames, outfit.Type.name))
			{
				return true;
			}
			return ContainsOutfitTypeName(_tpsActionHiddenOutfitTypeNames, outfit.Type.name);
		}

		private void RebuildDivingEquipmentFeetRendererCache()
		{
			_divingEquipmentFeetLinkedRenderers.Clear();
			if (_tpsOutfitSystem == null)
			{
				return;
			}
			Outfit[] componentsInChildren = _tpsOutfitSystem.GetComponentsInChildren<Outfit>(includeInactive: true);
			foreach (Outfit outfit in componentsInChildren)
			{
				if (!IsDivingEquipmentHiddenOutfit(outfit))
				{
					continue;
				}
				AddUniqueRenderer(_divingEquipmentFeetLinkedRenderers, outfit.outfitRenderer);
				AddUniqueRenderer(_divingEquipmentFeetLinkedRenderers, outfit.skinnedRenderer);
				SkinnedMeshRenderer[] skinnedRenderers = outfit.skinnedRenderers;
				if (skinnedRenderers != null)
				{
					for (int j = 0; j < skinnedRenderers.Length; j++)
					{
						AddUniqueRenderer(_divingEquipmentFeetLinkedRenderers, skinnedRenderers[j]);
					}
				}
				Renderer[] componentsInChildren2 = outfit.GetComponentsInChildren<Renderer>(includeInactive: true);
				for (int k = 0; k < componentsInChildren2.Length; k++)
				{
					AddUniqueRenderer(_divingEquipmentFeetLinkedRenderers, componentsInChildren2[k]);
				}
			}
		}

		private bool IsDivingEquipmentHiddenOutfit(Outfit outfit)
		{
			if (outfit == null || outfit.Type == null || string.IsNullOrWhiteSpace(outfit.Type.name))
			{
				return false;
			}
			return ContainsOutfitTypeName(_divingEquipmentHiddenOutfitTypeNames, outfit.Type.name);
		}

		private void ApplyTpsHeadMeshVisibility()
		{
			ApplyRendererVisibility(_tpsHeadLinkedRenderers, _tpsHeadLinkedRendererStates, _tpsHeadMeshesVisible);
		}

		private void ApplyDivingEquipmentFeetMeshVisibility()
		{
			ApplyRendererVisibility(_divingEquipmentFeetLinkedRenderers, _divingEquipmentFeetLinkedRendererStates, _divingEquipmentFeetMeshesVisible);
		}

		private static void ApplyRendererVisibility(List<Renderer> renderers, Dictionary<Renderer, bool> previousStates, bool visible)
		{
			for (int i = 0; i < renderers.Count; i++)
			{
				Renderer renderer = renderers[i];
				if (renderer == null)
				{
					continue;
				}
				if (visible)
				{
					if (previousStates.TryGetValue(renderer, out var value))
					{
						renderer.enabled = value;
					}
					continue;
				}
				if (!previousStates.ContainsKey(renderer))
				{
					previousStates.Add(renderer, renderer.enabled);
				}
				renderer.enabled = false;
			}
			if (visible)
			{
				previousStates.Clear();
			}
		}

		private void ApplyFpsViewGuard()
		{
			if (_fpsRootObject != null)
			{
				_fpsRootObject.transform.localPosition = _fpsLocalOffset;
			}
			if (_lockFpsOutfitSystemLocalY && _fpsOutfitSystem != null)
			{
				Transform transform = _fpsOutfitSystem.transform;
				Vector3 localPosition = transform.localPosition;
				if (!Mathf.Approximately(localPosition.y, _fpsOutfitSystemLocalY))
				{
					transform.localPosition = new Vector3(localPosition.x, _fpsOutfitSystemLocalY, localPosition.z);
				}
			}
			if (!(_fpsOutfitSystem == null) && !(HideSettingsField == null))
			{
				OutfitHideByTag[] componentsInChildren = _fpsOutfitSystem.GetComponentsInChildren<OutfitHideByTag>(includeInactive: true);
				for (int i = 0; i < componentsInChildren.Length; i++)
				{
					ApplyBodyPartVisibility(componentsInChildren[i]);
				}
			}
		}

		private void ApplyBodyPartVisibility(OutfitHideByTag hideByTag)
		{
			if (hideByTag == null || !(HideSettingsField.GetValue(hideByTag) is Array array))
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
					if (_visibleBodyPartTagSet.Contains(text))
					{
						skinnedMeshRenderer.gameObject.SetActive(value: true);
					}
					else if (_hiddenBodyPartTagSet.Contains(text))
					{
						skinnedMeshRenderer.gameObject.SetActive(value: false);
					}
				}
			}
		}

		private void RebuildTagSets()
		{
			RebuildTagSet(_visibleBodyPartTags, _visibleBodyPartTagSet);
			RebuildTagSet(_hiddenBodyPartTags, _hiddenBodyPartTagSet);
		}

		private static void RebuildTagSet(string[] source, HashSet<string> target)
		{
			target.Clear();
			if (source == null)
			{
				return;
			}
			foreach (string text in source)
			{
				if (!string.IsNullOrWhiteSpace(text))
				{
					target.Add(text.Trim());
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

		private static bool ContainsOutfitTypeName(string[] typeNames, string outfitTypeName)
		{
			if (typeNames == null)
			{
				return false;
			}
			foreach (string text in typeNames)
			{
				if (!string.IsNullOrWhiteSpace(text) && string.Equals(outfitTypeName, text, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			return false;
		}

		private static void AddUniqueRenderer(List<Renderer> renderers, Renderer renderer)
		{
			if (!(renderer == null) && !renderers.Contains(renderer))
			{
				renderers.Add(renderer);
			}
		}

		private static string[] CloneArrayOrKeepDefault(string[] value, string[] fallback)
		{
			if (value == null || value.Length == 0)
			{
				return fallback;
			}
			return (string[])value.Clone();
		}
	}
}
