using System;
using System.Collections.Generic;

namespace DynamicMeshCutter
{
	[Serializable]
	public class GroupBehaviours
	{
		public string Name;

		public Behaviour Behaviour;

		public GroupCondition Condition;

		public List<DynamicRagdollPart> Parts = new List<DynamicRagdollPart>();

		public List<int> Indices = new List<int>();

		public bool Passes(int[] indices)
		{
			switch (Condition)
			{
			case GroupCondition.Exact:
			{
				if (indices.Length != Parts.Count)
				{
					break;
				}
				for (int k = 0; k < indices.Length; k++)
				{
					if (!Indices.Contains(indices[k]))
					{
						return false;
					}
				}
				return true;
			}
			case GroupCondition.ContainsAll:
			{
				for (int l = 0; l < Indices.Count; l++)
				{
					bool flag = false;
					for (int m = 0; m < indices.Length; m++)
					{
						if (indices[m] == Indices[l])
						{
							flag = true;
							break;
						}
					}
					if (!flag)
					{
						return false;
					}
				}
				return true;
			}
			case GroupCondition.ContainsAny:
			{
				for (int i = 0; i < Indices.Count; i++)
				{
					for (int j = 0; j < indices.Length; j++)
					{
						if (indices[j] == Indices[i])
						{
							return true;
						}
					}
				}
				break;
			}
			}
			return false;
		}
	}
}
