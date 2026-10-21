using NGS.SLO.Shared;
using UnityEngine;

namespace NGS.SLO.MeshesCombine
{
	public static class MeshCombineSourcesController
	{
		public static void SetEnable(bool combinedEnable, bool sourcesEnable)
		{
			CombinedObject[] array = UnityAPI.FindObjectsOfType<CombinedObject>();
			for (int i = 0; i < array.Length; i++)
			{
				array[i].SetEnable(combinedEnable);
			}
			MeshCombineSource[] array2 = UnityAPI.FindObjectsOfType<MeshCombineSource>();
			for (int i = 0; i < array2.Length; i++)
			{
				array2[i].SetEnable(sourcesEnable);
			}
		}

		public static void DestroyCombinedMeshes(string dataFolder = "")
		{
			CombinedObject[] array = UnityAPI.FindObjectsOfType<CombinedObject>();
			for (int i = 0; i < array.Length; i++)
			{
				array[i].Destroy(dataFolder);
			}
		}

		public static void DestroySourceComponents()
		{
			MeshCombineSource[] array = UnityAPI.FindObjectsOfType<MeshCombineSource>();
			foreach (MeshCombineSource obj in array)
			{
				obj.DestroySourceComponents();
				Object.DestroyImmediate(obj);
			}
		}

		public static void AddSourcesWithErrorToIgnore()
		{
			MeshCombineSource[] array = UnityAPI.FindObjectsOfType<MeshCombineSource>();
			foreach (MeshCombineSource meshCombineSource in array)
			{
				foreach (MeshCombineReport report in meshCombineSource.Reports)
				{
					if (report.status == MeshCombineStatus.CombineError)
					{
						meshCombineSource.AddToCombineIgnore();
						break;
					}
				}
			}
		}
	}
}
