using System.Collections.Generic;
using DG.Tweening;
using PaperPixelGames.Framework.Enums;
using UnityEngine;

namespace PaperPixelGames.Core.Restaurant
{
	public class DirtyPlateScanTarget : MonoBehaviour
	{
		private readonly struct MaterialColorState
		{
			public readonly Material Material;

			public readonly bool HasColor;

			public readonly Color Color;

			public readonly bool HasBaseColor;

			public readonly Color BaseColor;

			public MaterialColorState(Material material, bool hasColor, Color color, bool hasBaseColor, Color baseColor)
			{
				Material = material;
				HasColor = hasColor;
				Color = color;
				HasBaseColor = hasBaseColor;
				BaseColor = baseColor;
			}
		}

		[Header("References")]
		[SerializeField]
		private Collider _scanCollider;

		[SerializeField]
		private GameObject _graphicRoot;

		[SerializeField]
		private GameObject _checkVisual;

		[Header("Plate Visual")]
		[SerializeField]
		private Renderer[] _plateRenderers;

		[Header("Scan Highlight")]
		[SerializeField]
		private Color _scanHighlightColor = new Color(1f, 0.92f, 0.2f, 1f);

		[SerializeField]
		[Min(0.01f)]
		private float _scanHighlightDuration = 1f;

		private int _plateMaterialIndex;

		private readonly HashSet<Material> _ownedMaterials = new HashSet<Material>();

		private readonly List<MaterialColorState> _materialColorStates = new List<MaterialColorState>();

		public int PlateIndex { get; private set; }

		public bool IsScanned { get; private set; }

		public PlateColor PlateColor { get; private set; }

		public int DishPrice { get; private set; }

		public Collider ScanCollider => _scanCollider;

		public void Setup(int plateIndex, PlateColor plateColor, int dishPrice)
		{
			PlateIndex = plateIndex;
			PlateColor = plateColor;
			DishPrice = Mathf.Max(0, dishPrice);
			if (_graphicRoot != null)
			{
				_graphicRoot.SetActive(value: true);
			}
			if (_scanCollider != null)
			{
				_scanCollider.enabled = true;
			}
			SetScanned(scanned: false);
		}

		public void ApplyPlateMaterial(Material material, int materialIndex = 0)
		{
			if (material == null || _plateRenderers == null || _plateRenderers.Length == 0)
			{
				return;
			}
			int num = (_plateMaterialIndex = Mathf.Max(0, materialIndex));
			for (int i = 0; i < _plateRenderers.Length; i++)
			{
				Renderer renderer = _plateRenderers[i];
				if (!(renderer == null))
				{
					Material[] sharedMaterials = renderer.sharedMaterials;
					if (sharedMaterials != null && sharedMaterials.Length != 0 && num < sharedMaterials.Length)
					{
						Material material2 = sharedMaterials[num];
						sharedMaterials[num] = new Material(material);
						_ownedMaterials.Add(sharedMaterials[num]);
						renderer.sharedMaterials = sharedMaterials;
						ReleaseOwnedMaterial(material2);
					}
				}
			}
		}

		public void PlayScanHighlight()
		{
			if (base.isActiveAndEnabled)
			{
				List<MaterialColorState> list = CaptureMaterialColors();
				for (int i = 0; i < list.Count; i++)
				{
					PlayMaterialPulse(list[i]);
				}
			}
		}

		private void PlayMaterialPulse(MaterialColorState state)
		{
			if (!(state.Material == null))
			{
				DOTween.Kill(state.Material);
				ApplyMaterialColor(state.Material, _scanHighlightColor);
				Sequence sequence = DOTween.Sequence();
				sequence.SetTarget(state.Material);
				if (state.HasColor)
				{
					sequence.Join(state.Material.DOColor(state.Color, "_Color", _scanHighlightDuration));
				}
				if (state.HasBaseColor)
				{
					sequence.Join(state.Material.DOColor(state.BaseColor, "_BaseColor", _scanHighlightDuration));
				}
			}
		}

		private List<MaterialColorState> CaptureMaterialColors()
		{
			List<MaterialColorState> materialColorStates = _materialColorStates;
			materialColorStates.Clear();
			if (_plateRenderers == null || _plateRenderers.Length == 0)
			{
				return materialColorStates;
			}
			for (int i = 0; i < _plateRenderers.Length; i++)
			{
				Renderer renderer = _plateRenderers[i];
				if (renderer == null)
				{
					continue;
				}
				Material[] sharedMaterials = renderer.sharedMaterials;
				if (sharedMaterials == null || _plateMaterialIndex < 0 || _plateMaterialIndex >= sharedMaterials.Length)
				{
					continue;
				}
				Material material = sharedMaterials[_plateMaterialIndex];
				if (!(material == null))
				{
					if (!_ownedMaterials.Contains(material))
					{
						material = new Material(material);
						_ownedMaterials.Add(material);
						sharedMaterials[_plateMaterialIndex] = material;
						renderer.sharedMaterials = sharedMaterials;
					}
					bool flag = material.HasProperty("_Color");
					bool flag2 = material.HasProperty("_BaseColor");
					materialColorStates.Add(new MaterialColorState(material, flag, flag ? material.GetColor("_Color") : Color.white, flag2, flag2 ? material.GetColor("_BaseColor") : Color.white));
				}
			}
			return materialColorStates;
		}

		private void ReleaseOwnedMaterial(Material material)
		{
			if (!(material == null) && _ownedMaterials.Remove(material))
			{
				DestroyMaterial(material);
			}
		}

		private static void DestroyMaterial(Material material)
		{
			if (!(material == null))
			{
				DOTween.Kill(material);
				if (Application.isPlaying)
				{
					Object.Destroy(material);
				}
				else
				{
					Object.DestroyImmediate(material);
				}
			}
		}

		private void OnDestroy()
		{
			foreach (Material ownedMaterial in _ownedMaterials)
			{
				DestroyMaterial(ownedMaterial);
			}
			_ownedMaterials.Clear();
			_materialColorStates.Clear();
		}

		private static void ApplyMaterialColor(Material material, Color color)
		{
			if (!(material == null))
			{
				if (material.HasProperty("_Color"))
				{
					material.SetColor("_Color", color);
				}
				if (material.HasProperty("_BaseColor"))
				{
					material.SetColor("_BaseColor", color);
				}
			}
		}

		private void Reset()
		{
			if (_scanCollider == null)
			{
				_scanCollider = GetComponentInChildren<Collider>(includeInactive: true);
			}
			if (_graphicRoot == null)
			{
				_graphicRoot = base.gameObject;
			}
			if (_plateRenderers == null || _plateRenderers.Length == 0)
			{
				_plateRenderers = GetComponentsInChildren<Renderer>(includeInactive: true);
			}
		}

		public void SetScanned(bool scanned)
		{
			IsScanned = scanned;
			if (_scanCollider != null)
			{
				_scanCollider.enabled = true;
			}
			if (_checkVisual != null)
			{
				_checkVisual.SetActive(scanned);
			}
		}
	}
}
