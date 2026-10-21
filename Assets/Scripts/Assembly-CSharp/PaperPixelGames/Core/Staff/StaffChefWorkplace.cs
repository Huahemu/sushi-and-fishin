using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Kitchen.ChefDesk;
using UnityEngine;

namespace PaperPixelGames.Core.Staff
{
	[Serializable]
	public sealed class StaffChefWorkplace
	{
		public ChefDesk Desk;

		public Transform WorkPoint;

		public Transform OutputPoint;

		public Transform PanStackPoint;

		[HideInInspector]
		public List<Transform> PanOutputPoints = new List<Transform>();

		public List<Transform> ReadySushiPoints = new List<Transform>();

		public IEnumerable<Transform> PanOutputs
		{
			get
			{
				if (PanOutputPoints.Count <= 0)
				{
					return new Transform[1] { PanStackPoint };
				}
				return PanOutputPoints;
			}
		}

		public bool IsReady
		{
			get
			{
				if (Desk != null && Desk.IsSpawned && WorkPoint != null && OutputPoint != null)
				{
					return PanStackPoint != null;
				}
				return false;
			}
		}
	}
}
