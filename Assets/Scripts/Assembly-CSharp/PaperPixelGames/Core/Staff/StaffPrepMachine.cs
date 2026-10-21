using System;
using PaperPixelGames.Core.Kitchen;
using PaperPixelGames.Core.Kitchen.RiceCookerScript;
using UnityEngine;

namespace PaperPixelGames.Core.Staff
{
	[Serializable]
	public sealed class StaffPrepMachine
	{
		public RiceCooker RiceCooker;

		public FilletMachine FilletMachine;

		public Transform WorkPoint;

		public Transform OutputPoint;

		public bool IsReady
		{
			get
			{
				if ((RiceCooker != null || FilletMachine != null) && WorkPoint != null)
				{
					return OutputPoint != null;
				}
				return false;
			}
		}
	}
}
