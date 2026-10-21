using System;
using UnityEngine;

namespace PaperPixelGames.Core.Data
{
	[Serializable]
	public class ItemOffset
	{
		public Vector3 PositionOffset;

		public Vector3 RotationOffset;

		public Vector3 ScaleOffset = Vector3.one;

		public ItemOffset(Vector3 pos, Vector3 rot)
		{
			PositionOffset = pos;
			RotationOffset = rot;
			ScaleOffset = Vector3.one;
		}
	}
}
