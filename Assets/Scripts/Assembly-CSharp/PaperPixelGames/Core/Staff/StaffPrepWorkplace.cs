using System;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Kitchen.PreparationStation.Station;
using UnityEngine;

namespace PaperPixelGames.Core.Staff
{
	[Serializable]
	public sealed class StaffPrepWorkplace
	{
		public PrepStation Station;

		public Transform WorkPoint;

		public Transform OutputPoint;

		public Transform PanStackPoint;

		[HideInInspector]
		public List<StaffStoragePoint> PreparedPanPoints = new List<StaffStoragePoint>();

		public List<Transform> PreparedPanSlots = new List<Transform>();

		public bool IsReady
		{
			get
			{
				if (Station != null && WorkPoint != null && OutputPoint != null)
				{
					return PanStackPoint != null;
				}
				return false;
			}
		}

		public Transform PreparedPanPoint(string ingredientId)
		{
			return PreparedPanPoints.FirstOrDefault((StaffStoragePoint p) => p.Item != null && p.Item.Id == ingredientId && p.Point != null)?.Point ?? PanStackPoint;
		}
	}
}
