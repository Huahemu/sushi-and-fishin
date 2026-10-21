using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace NGS.SLO.MeshesCombine
{
	public class UnityMeshCombiner : IMeshCombiner
	{
		public MeshCombineResult Combine(IList<MeshCombineInstance> instances)
		{
			try
			{
				if (instances == null || instances.Count == 0)
				{
					return new MeshCombineResult(instances.ToArray(), "UnityMeshCombiner::instances is empty");
				}
				foreach (MeshCombineInstance instance in instances)
				{
					if (!instance.ReadyForCombine)
					{
						return new MeshCombineResult(instances.ToArray(), "Not all instances ready for combine");
					}
				}
				CombineInstance[] array = new CombineInstance[instances.Count];
				for (int i = 0; i < instances.Count; i++)
				{
					MeshCombineInstance meshCombineInstance = instances[i];
					if (meshCombineInstance.mesh == null || meshCombineInstance.renderer == null)
					{
						return new MeshCombineResult(instances.ToArray(), $"UnityMeshCombiner::MeshCombineInstance at index {i} with null mesh or renderer");
					}
					array[i] = new CombineInstance
					{
						mesh = meshCombineInstance.mesh,
						subMeshIndex = meshCombineInstance.submeshIndex,
						transform = meshCombineInstance.transform,
						lightmapScaleOffset = meshCombineInstance.lightmapScaleOffset
					};
				}
				int num = instances.Sum((MeshCombineInstance meshCombineInstance2) => meshCombineInstance2.vertexCount);
				Mesh mesh = new Mesh
				{
					name = CreateCombinedMeshName(instances)
				};
				if (num >= 65535)
				{
					mesh.indexFormat = IndexFormat.UInt32;
				}
				mesh.CombineMeshes(array, mergeSubMeshes: true, useMatrices: true, hasLightmapData: true);
				return new MeshCombineResult(instances.ToArray(), mesh);
			}
			catch (Exception ex)
			{
				return new MeshCombineResult(instances.ToArray(), ex.Message + " \n " + ex.StackTrace);
			}
		}

		private string CreateCombinedMeshName(IList<MeshCombineInstance> instances)
		{
			string text = "";
			int num = Mathf.Min(3, instances.Count);
			for (int i = 0; i < num; i++)
			{
				string text2 = instances[i].renderer.name;
				if (text2.Length > 5)
				{
					text2 = text2.Remove(5);
				}
				text += $"{text2}_sub_{instances[i].submeshIndex}_";
			}
			return text.Remove(text.Length - 1);
		}
	}
}
