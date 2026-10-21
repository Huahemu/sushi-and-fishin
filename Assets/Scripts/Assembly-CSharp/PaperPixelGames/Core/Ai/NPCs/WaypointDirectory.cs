using System.Collections.Generic;
using PaperPixelGames.Framework.Enums;
using UnityEngine;

namespace PaperPixelGames.Core.Ai.NPCs
{
	public static class WaypointDirectory
	{
		private static readonly Dictionary<string, WaypointAnchor> Map = new Dictionary<string, WaypointAnchor>();

		public static void Register(WaypointAnchor anchor)
		{
			if (!(anchor == null))
			{
				Map[anchor.GlobalKey] = anchor;
			}
		}

		public static void Unregister(WaypointAnchor anchor)
		{
			if (!(anchor == null))
			{
				Map.Remove(anchor.GlobalKey);
			}
		}

		public static bool TryGet(ResidentActivityType category, WayPointIndex index, out Transform target)
		{
			target = null;
			if (!Map.TryGetValue(BuildKey(category, index), out var value) || value == null)
			{
				return false;
			}
			target = value.transform;
			return target != null;
		}

		public static bool TryGetAnchor(ResidentActivityType category, WayPointIndex index, out WaypointAnchor anchor)
		{
			if (Map.TryGetValue(BuildKey(category, index), out anchor))
			{
				return anchor != null;
			}
			return false;
		}

		private static string BuildKey(ResidentActivityType category, WayPointIndex index)
		{
			return category.ToString() + "_" + index;
		}
	}
}
