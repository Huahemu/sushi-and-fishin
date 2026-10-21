using System.Collections.Generic;

namespace NGS.SLO.MeshesCombine
{
	public static class MeshCombineInstancesGrouper
	{
		public static void CreateCombineGroups(IList<MeshCombineInstance> instances, MeshCombineOptions options, List<MeshCombineGroup> groups)
		{
			foreach (MeshCombineInstance instance in instances)
			{
				bool flag = false;
				for (int i = 0; i < groups.Count; i++)
				{
					if (groups[i].TryAddInstance(instance))
					{
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					MeshCombineGroup meshCombineGroup = new MeshCombineGroup(options);
					meshCombineGroup.TryAddInstance(instance);
					groups.Add(meshCombineGroup);
				}
			}
		}
	}
}
