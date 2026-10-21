using System.Collections.Generic;

namespace PaperPixelGames.Networking
{
	public static class AdmissionSlotCounter
	{
		public static int CountOccupiedSlots(ulong hostPlatformId, IEnumerable<ulong> activePlatformIds, IEnumerable<ulong> reservedPlatformIds)
		{
			HashSet<ulong> hashSet = ((activePlatformIds != null) ? new HashSet<ulong>(activePlatformIds) : new HashSet<ulong>());
			if (reservedPlatformIds != null)
			{
				hashSet.UnionWith(reservedPlatformIds);
			}
			hashSet.Remove(0uL);
			hashSet.Remove(hostPlatformId);
			return 1 + hashSet.Count;
		}
	}
}
