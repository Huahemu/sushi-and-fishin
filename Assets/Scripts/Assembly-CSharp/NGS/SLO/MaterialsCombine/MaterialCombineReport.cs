using System;
using UnityEngine;

namespace NGS.SLO.MaterialsCombine
{
	[Serializable]
	public struct MaterialCombineReport
	{
		public Material material;

		public MaterialCombineStatus combineStatus;

		public string message;
	}
}
