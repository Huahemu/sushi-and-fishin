using PaperPixelGames.Core.Settings;
using UnityEngine;

namespace PaperPixelGames.Core.PlayerTools
{
	public static class FishingRodPreferences
	{
		public static bool ToggleReel => PlayerPrefs.GetInt("Fishing.ToggleReel", 0) != 0;

		public static bool HoldFullCharge => PlayerPrefs.GetInt("Fishing.HoldFullCharge", 0) != 0;

		public static bool ReducedMotion
		{
			get
			{
				if (PlayerPrefs.GetInt("Fishing.ReducedMotion", 0) == 0 && RuntimeSettings.CameraShakeEnabled)
				{
					return !RuntimeSettings.ViewmodelMotionEnabled;
				}
				return true;
			}
		}

		public static float Sensitivity => Mathf.Clamp(PlayerPrefs.GetFloat("Fishing.Sensitivity", 1f), 0.25f, 3f);
	}
}
