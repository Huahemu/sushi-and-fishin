using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NGS.SLO.MeshesCombine
{
	public class MeshCombineSource : ToggleableObject
	{
		[SerializeField]
		private List<MeshCombineReport> _reports;

		public IReadOnlyList<MeshCombineReport> Reports => _reports;

		public void CreateCombineReport(MeshCombineResult combineResult, int instanceIndex)
		{
			if (_reports == null)
			{
				_reports = new List<MeshCombineReport>();
			}
			int submeshIndex = combineResult.instances[instanceIndex].submeshIndex;
			_reports.RemoveAll((MeshCombineReport r) => r.submeshIndex == submeshIndex);
			_reports.Add(new MeshCombineReport
			{
				submeshIndex = submeshIndex,
				status = ((!combineResult.success) ? MeshCombineStatus.CombineError : MeshCombineStatus.Combined),
				combineError = combineResult.errorMessage
			});
			_reports = _reports.OrderBy((MeshCombineReport r) => r.submeshIndex).ToList();
		}

		public void DestroySourceComponents()
		{
			Renderer component = GetComponent<Renderer>();
			if (!(component == null))
			{
				MeshFilter component2 = GetComponent<MeshFilter>();
				if (component2 != null)
				{
					Object.DestroyImmediate(component2);
				}
				if (component != null)
				{
					Object.DestroyImmediate(component);
				}
			}
		}

		public void AddToCombineIgnore()
		{
			if (!(GetComponent<MeshCombineIgnore>() != null))
			{
				base.gameObject.AddComponent<MeshCombineIgnore>();
			}
		}
	}
}
