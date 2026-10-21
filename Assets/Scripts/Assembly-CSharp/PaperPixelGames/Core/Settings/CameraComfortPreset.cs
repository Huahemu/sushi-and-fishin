using System.Collections.Generic;
using System.Linq;
using Kamgam.SettingsGenerator;

namespace PaperPixelGames.Core.Settings
{
	public static class CameraComfortPreset
	{
		private static readonly string[] MotionSettingIds = new string[8] { "camShake", "cameraBob", "landingCameraMotion", "dynamicFovEffects", "underwaterDepthOfField", "underwaterLensDistortion", "viewmodelMotion", "damageCameraMotion" };

		public static IReadOnlyList<string> SettingIds => MotionSettingIds;

		public static bool TryApplyReduceMotion(SettingsProvider provider, bool reduceMotionEnabled, out string error)
		{
			error = null;
			if (provider == null)
			{
				error = "SettingsProvider is missing.";
				return false;
			}
			Kamgam.SettingsGenerator.Settings settings = provider.Settings;
			if (settings == null)
			{
				error = "Runtime settings could not be loaded.";
				return false;
			}
			List<string> list = MotionSettingIds.Where((string id2) => settings.GetBool(id2) == null).ToList();
			if (list.Count > 0)
			{
				error = "Missing camera comfort settings: " + string.Join(", ", list);
				return false;
			}
			bool value = !reduceMotionEnabled;
			string[] motionSettingIds = MotionSettingIds;
			foreach (string id in motionSettingIds)
			{
				settings.GetBool(id).SetValue(value);
				settings.RefreshRegisteredResolvers(id);
			}
			provider.Save();
			return true;
		}

		public static bool IsReduceMotionEnabled(IEnumerable<SettingBool> settings)
		{
			if (settings == null)
			{
				return false;
			}
			bool result = false;
			foreach (SettingBool setting in settings)
			{
				if (setting != null)
				{
					result = true;
					if (setting.GetValue())
					{
						return false;
					}
				}
			}
			return result;
		}
	}
}
