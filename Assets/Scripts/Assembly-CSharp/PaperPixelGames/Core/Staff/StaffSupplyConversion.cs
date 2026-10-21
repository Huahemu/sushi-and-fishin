using System;
using PaperPixelGames.Core.Data;
using UnityEngine;

namespace PaperPixelGames.Core.Staff
{
	[Serializable]
	public sealed class StaffSupplyConversion
	{
		public HoldableSO Input;

		public HoldableSO Output;

		[Min(0.001f)]
		public float Yield = 1f;
	}
}
