using System;

namespace Kamgam.SettingsGenerator
{
	[Serializable]
	public struct GraphicsQualityPreset
	{
		public string QualityName;

		public float RenderDistance;

		public bool AmbientOcclusionEnabled;

		public int MsaaOptionIndex;

		public int AntiAliasingOptionIndex;

		public float ShadowDistance;
	}
}
