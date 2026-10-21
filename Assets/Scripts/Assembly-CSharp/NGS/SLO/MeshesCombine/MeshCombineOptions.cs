using System;

namespace NGS.SLO.MeshesCombine
{
	[Serializable]
	public struct MeshCombineOptions
	{
		public bool limit65kVertices;

		public bool useLightweightBuffers;

		public bool rebuildUV2;

		public static MeshCombineOptions Default => new MeshCombineOptions
		{
			limit65kVertices = true,
			useLightweightBuffers = false
		};
	}
}
