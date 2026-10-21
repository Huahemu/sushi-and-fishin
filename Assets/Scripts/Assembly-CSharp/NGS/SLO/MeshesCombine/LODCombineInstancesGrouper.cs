using System.Collections.Generic;

namespace NGS.SLO.MeshesCombine
{
	public static class LODCombineInstancesGrouper
	{
		public static void CreateCombineGroups(IList<LODCombineInstance> instances, List<LODCombineGroup> groups)
		{
			foreach (LODCombineInstance instance in instances)
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
					LODCombineGroup lODCombineGroup = new LODCombineGroup();
					lODCombineGroup.TryAddInstance(instance);
					groups.Add(lODCombineGroup);
				}
			}
		}
	}
}
