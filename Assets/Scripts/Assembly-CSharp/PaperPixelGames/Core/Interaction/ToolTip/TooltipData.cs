using System;
using UnityEngine;

namespace PaperPixelGames.Core.Interaction.ToolTip
{
	[Serializable]
	public struct TooltipData
	{
		public string Title;

		public string Body;

		public bool OnlyHeader;

		public bool SetSize;

		public Vector3 Size;
	}
}
