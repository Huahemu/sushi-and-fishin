using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NGS.SLO.MeshesCombine
{
	public class LODGroupCombiner
	{
		private class LevelOfDetailCombiner
		{
			private LODGroupCombiner _parent;

			private int _levelIndex;

			public LevelOfDetailCombiner(LODGroupCombiner parent, int levelIndex)
			{
				_parent = parent;
				_levelIndex = levelIndex;
			}

			public void Combine(MeshCombineInstance[] instances, List<MeshCombineResult> outCombineResult, MeshCombineOptions options, out GameObject combinedLodGO)
			{
				List<MeshCombineGroup> list = new List<MeshCombineGroup>();
				MeshCombineInstancesGrouper.CreateCombineGroups(instances, options, list);
				combinedLodGO = new GameObject($"LOD{_levelIndex}");
				foreach (MeshCombineGroup item2 in list)
				{
					MeshCombineResult item = _parent._combiner.CreateCombinedObject(item2.Instances, options, out var combinedGO);
					outCombineResult.Add(item);
					if (item.success)
					{
						combinedGO.transform.parent = combinedLodGO.transform;
					}
				}
			}
		}

		private RendererCombiner _combiner;

		public LODGroupCombiner(IMeshCombiner meshCombiner, ISkinnedMeshCombiner skinnedMeshCombiner)
		{
			_combiner = new RendererCombiner(meshCombiner, skinnedMeshCombiner);
		}

		public MeshCombineResult[] CreateCombinedObject(IList<LODCombineInstance> instances, MeshCombineOptions options, out LODGroup combinedGroup)
		{
			if (instances == null || instances.Count == 0)
			{
				combinedGroup = null;
				return new MeshCombineResult[1]
				{
					new MeshCombineResult(instances.Select((LODCombineInstance i) => i.instance).ToArray(), "LODGroupCombiner::CreateCombinedObject 'instances' is empty")
				};
			}
			combinedGroup = CreateLODGroupObject();
			CopyLODGroupSettings(instances[0].lodGroup, combinedGroup);
			List<MeshCombineResult> list = new List<MeshCombineResult>();
			LOD[] lODs = CombineLODs(combinedGroup, instances, list, options);
			combinedGroup.SetLODs(lODs);
			return list.ToArray();
		}

		private LODGroup CreateLODGroupObject()
		{
			return new GameObject("Combined_LODGroup").AddComponent<LODGroup>();
		}

		private void CopyLODGroupSettings(LODGroup source, LODGroup destination)
		{
			destination.fadeMode = source.fadeMode;
			destination.animateCrossFading = source.animateCrossFading;
		}

		private LOD[] CombineLODs(LODGroup parent, IList<LODCombineInstance> instances, List<MeshCombineResult> outCombineResult, MeshCombineOptions options)
		{
			int num = instances.Max((LODCombineInstance lODCombineInstance) => lODCombineInstance.lodGroup.lodCount);
			LOD[] array = new LOD[num];
			float num2 = 1f / (float)(num + 1);
			int i;
			for (i = 0; i < num; i++)
			{
				LevelOfDetailCombiner levelOfDetailCombiner = new LevelOfDetailCombiner(this, i);
				MeshCombineInstance[] instances2 = (from l in instances
					where l.lodLevel == i
					select l.instance).ToArray();
				GameObject combinedLodGO = null;
				levelOfDetailCombiner.Combine(instances2, outCombineResult, options, out combinedLodGO);
				combinedLodGO.transform.parent = parent.transform;
				array[i] = new LOD(1f - num2 * (float)(i + 1), combinedLodGO.GetComponentsInChildren<Renderer>());
			}
			return array;
		}
	}
}
