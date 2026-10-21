using System;
using PaperPixelGames.Core.Data.Ai.NPCs;
using UnityEngine;

namespace PaperPixelGames.Core.Staff
{
	[Serializable]
	public sealed class StaffCustomizationOption
	{
		public int CustomizationId = -1;

		public StaffGender Gender;

		public ResidentDataSO ResidentData;

		public Sprite Portrait;

		public int ResolveId()
		{
			if (CustomizationId < 0)
			{
				if (!(ResidentData != null))
				{
					return -1;
				}
				return ResidentData.NpcID;
			}
			return CustomizationId;
		}
	}
}
