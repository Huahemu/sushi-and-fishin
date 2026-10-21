using System;
using System.Collections.Generic;
using NGS.SLO.Shared;
using UnityEngine;

namespace NGS.SLO.MaterialsCombine
{
	[DisallowMultipleComponent]
	[RequireComponent(typeof(Renderer))]
	public class MaterialCombineOutput : MonoBehaviour
	{
		[SerializeField]
		[HideInInspector]
		private Renderer _renderer;

		[SerializeField]
		private MaterialCombineReport[] _combineReports;

		public IReadOnlyList<MaterialCombineReport> Reports => _combineReports;

		public void Reset()
		{
			_renderer = GetComponent<Renderer>();
			_combineReports = new MaterialCombineReport[_renderer.sharedMaterials.Length];
			for (int i = 0; i < _combineReports.Length; i++)
			{
				_combineReports[i] = new MaterialCombineReport
				{
					combineStatus = MaterialCombineStatus.NotCombined,
					message = ""
				};
			}
		}

		public void ApplyCombineResult(MaterialCombineInstance instance, MaterialCombineResult combineResult, int instanceIndex)
		{
			int materialIndex = instance.MaterialIndex;
			if (materialIndex < 0 || materialIndex >= _renderer.sharedMaterials.Length)
			{
				Debug.Log("MaterialCombinedOutput::trying to apply combine result with out-of-bounds materialIndex");
				return;
			}
			CreateCombineReport(combineResult, materialIndex);
			if (!combineResult.success)
			{
				return;
			}
			try
			{
				AdjustUV(instance, combineResult.rects[instanceIndex]);
				_renderer.SetSharedMaterial(combineResult.combinedMaterial, materialIndex);
			}
			catch (Exception ex)
			{
				Debug.LogError("MaterialCombinedOutput::" + base.gameObject.name + ": Error while apply combined material : " + ex.Message);
				_combineReports[materialIndex] = new MaterialCombineReport
				{
					material = _renderer.GetSharedMaterial(materialIndex),
					combineStatus = MaterialCombineStatus.CombineError,
					message = "MaterialCombinedOutput::Error while apply combined material : " + ex.Message
				};
			}
		}

		private void CreateCombineReport(MaterialCombineResult combineResult, int materialIndex)
		{
			MaterialCombineReport materialCombineReport = default;
			if (!combineResult.success)
			{
				materialCombineReport.material = _renderer.GetSharedMaterial(materialIndex);
				materialCombineReport.combineStatus = MaterialCombineStatus.CombineError;
				materialCombineReport.message = combineResult.errorMessage;
			}
			else
			{
				materialCombineReport.material = combineResult.combinedMaterial;
				materialCombineReport.combineStatus = MaterialCombineStatus.Combined;
			}
			_combineReports[materialIndex] = materialCombineReport;
		}

		private void AdjustUV(MaterialCombineInstance instance, Rect rect)
		{
			if (!_renderer.TryGetSharedMesh(out var mesh))
			{
				throw new Exception("MaterialCombinedOutput::mesh not found");
			}
			mesh = MeshUtil.GetUVAdjustedMesh(mesh, instance, rect);
			if (!_renderer.TrySetSharedMesh(mesh))
			{
				throw new Exception("MaterialCombinedOutput::unable to set adjusted mesh");
			}
		}
	}
}
