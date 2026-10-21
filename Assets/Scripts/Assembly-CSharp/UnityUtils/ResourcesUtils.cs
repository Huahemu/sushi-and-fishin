using UnityEngine;
using UnityEngine.Rendering;

namespace UnityUtils
{
	public static class ResourcesUtils
	{
		public static void LoadVolumeProfile(this Volume volume, string path)
		{
			VolumeProfile profile = Resources.Load<VolumeProfile>(path);
			volume.profile = profile;
		}
	}
}
