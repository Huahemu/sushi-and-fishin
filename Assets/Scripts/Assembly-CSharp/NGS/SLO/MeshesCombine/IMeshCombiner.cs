using System.Collections.Generic;

namespace NGS.SLO.MeshesCombine
{
	public interface IMeshCombiner
	{
		MeshCombineResult Combine(IList<MeshCombineInstance> instances);
	}
}
