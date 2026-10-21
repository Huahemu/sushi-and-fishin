using System;
using System.Collections.Generic;
using PaperPixelGames.Framework.Enums;
using UnityEngine;

namespace PaperPixelGames.Core.Kitchen
{
	[DisallowMultipleComponent]
	public sealed class PlateGraphicHandler : MonoBehaviour
	{
		[SerializeField]
		private PlateColor _color;

		[Header("Target")]
		[SerializeField]
		private MeshFilter _plateMeshFilter;

		[SerializeField]
		private MeshRenderer _plateMeshRenderer;

		[Header("Original Plate")]
		[SerializeField]
		private Mesh _originalPlateMesh;

		[SerializeField]
		private List<Material> _originalPlateMaterials = new List<Material>();

		[Header("Conveyor Plate")]
		[SerializeField]
		private Mesh _conveyorPlateMesh;

		[SerializeField]
		private List<Material> _conveyorPlateMaterials = new List<Material>();

		[Header("Editor Fields")]
		[SerializeField]
		private Mesh _conveyorPlateMeshEditor;

		[SerializeField]
		private Material _conveyorPlateMaterialBase;

		[SerializeField]
		private Material _conveyorPlateMaterialRed;

		[SerializeField]
		private Material _conveyorPlateMaterialGreen;

		[SerializeField]
		private Material _conveyorPlateMaterialBlue;

		[SerializeField]
		private Material _conveyorPlateMaterialYellow;

		public PlateColor Color => _color;

		public MeshRenderer PlateRenderer => _plateMeshRenderer;

		private void Awake()
		{
			CaptureOriginalIfMissing();
		}

		public void ApplyOriginalPlate()
		{
			ApplyPlate(_originalPlateMesh, ToMaterialArray(_originalPlateMaterials));
		}

		public void ApplyConveyorPlate()
		{
			ApplyPlate(_conveyorPlateMesh, ToMaterialArray(_conveyorPlateMaterials));
		}

		public void ApplyMode(bool useConveyorPlate)
		{
			if (useConveyorPlate)
			{
				ApplyConveyorPlate();
			}
			else
			{
				ApplyOriginalPlate();
			}
		}

		private void CaptureOriginalIfMissing()
		{
			if (_plateMeshFilter != null && _originalPlateMesh == null)
			{
				_originalPlateMesh = _plateMeshFilter.sharedMesh;
			}
			if (_plateMeshRenderer != null && (_originalPlateMaterials == null || _originalPlateMaterials.Count == 0))
			{
				_originalPlateMaterials = CopyMaterials(_plateMeshRenderer.sharedMaterials);
			}
		}

		private void ApplyPlate(Mesh mesh, Material[] materials)
		{
			if (_plateMeshFilter != null && mesh != null)
			{
				_plateMeshFilter.sharedMesh = mesh;
			}
			if (_plateMeshRenderer != null && materials != null && materials.Length != 0)
			{
				_plateMeshRenderer.sharedMaterials = materials;
			}
		}

		private static List<Material> CopyMaterials(Material[] source)
		{
			if (source == null || source.Length == 0)
			{
				return new List<Material>();
			}
			return new List<Material>(source);
		}

		private static Material[] ToMaterialArray(List<Material> materials)
		{
			if (materials == null || materials.Count == 0)
			{
				return Array.Empty<Material>();
			}
			return materials.ToArray();
		}
	}
}
