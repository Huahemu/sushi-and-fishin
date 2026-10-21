using System;

namespace NGS.SLO.MeshesCombine
{
	[Serializable]
	public struct MeshCombineReport
	{
		public int submeshIndex;

		public MeshCombineStatus status;

		public string combineError;
	}
}
