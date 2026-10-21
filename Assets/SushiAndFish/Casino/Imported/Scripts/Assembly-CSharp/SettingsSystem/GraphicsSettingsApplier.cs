namespace SushiAndFish.Casino.Original
{
using global::System;
using global::UnityEngine;
using global::UnityEngine.Rendering;
using global::UnityEngine.Rendering.Universal;

namespace SettingsSystem
{
	public class GraphicsSettingsApplier : ISettingsApplier
	{
		private readonly GraphicsSettings _graphicsSettings;

		public GraphicsSettingsApplier(GraphicsSettings graphicsSettings)
		{
			_graphicsSettings = graphicsSettings;
		}

		public void Apply(SettingItemBase entry)
		{
			if (entry == null || string.IsNullOrWhiteSpace(entry.key) || _graphicsSettings == null)
			{
				return;
			}
			switch (entry.key.Trim().ToLowerInvariant())
			{
			case "quality":
			case "qualitylevel":
			{
				if (entry is DropdownSettingItem dropdownSettingItem && Enum.TryParse<GraphicsSettings.QualityLevel>(dropdownSettingItem.CurrentOption, ignoreCase: true, out var result))
				{
					_graphicsSettings.qualityLevel = result;
					ApplyQualityLevel();
				}
				break;
			}
			case "renderscale":
			case "render scale":
				if (entry is SliderSettingItem sliderSettingItem3)
				{
					_graphicsSettings.renderScale = sliderSettingItem3.value;
					ApplyRenderScale();
				}
				break;
			case "hdr":
				if (entry is ToggleSettingItem toggleSettingItem)
				{
					_graphicsSettings.enableHDR = toggleSettingItem.value;
					ApplyHDR();
				}
				break;
			case "brightness":
				if (entry is SliderSettingItem sliderSettingItem2)
				{
					_graphicsSettings.brightness = sliderSettingItem2.value;
					ApplyBrightness();
				}
				break;
			case "filmgrain":
				if (entry is SliderSettingItem sliderSettingItem)
				{
					_graphicsSettings.filmGrain = sliderSettingItem.value;
					ApplyFilmGrain();
				}
				break;
			}
		}

		public void ApplyAll(SettingsLayout layout)
		{
			if (layout == null || _graphicsSettings == null)
			{
				return;
			}
			foreach (SettingsLayout.Tab tab in layout.tabs)
			{
				if (tab == null)
				{
					continue;
				}
				foreach (SettingItemBase entry in tab.entries)
				{
					Apply(entry);
				}
			}
			ApplyAllSettings();
		}

		public void ApplyAllSettings()
		{
			if (!(_graphicsSettings == null))
			{
				ApplyQualityLevel();
				ApplyRenderScale();
				ApplyHDR();
				ApplyBrightness();
				ApplyFilmGrain();
			}
		}

		private void ApplyQualityLevel()
		{
			UniversalRenderPipelineAsset qualityAsset = _graphicsSettings.GetQualityAsset();
			if (qualityAsset != null)
			{
				int vSyncCount = QualitySettings.vSyncCount;
				int qualityLevel = (int)_graphicsSettings.qualityLevel;
				if (qualityLevel >= 0 && qualityLevel < QualitySettings.names.Length)
				{
					QualitySettings.SetQualityLevel(qualityLevel, applyExpensiveChanges: true);
				}
				QualitySettings.vSyncCount = vSyncCount;
				QualitySettings.renderPipeline = qualityAsset;
				UniversalRenderPipelineAsset universalRenderPipelineAsset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
				if (universalRenderPipelineAsset != null)
				{
					universalRenderPipelineAsset.renderScale = _graphicsSettings.renderScale;
					universalRenderPipelineAsset.supportsHDR = _graphicsSettings.enableHDR;
				}
			}
		}

		private void ApplyRenderScale()
		{
			UniversalRenderPipelineAsset universalRenderPipelineAsset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
			if (universalRenderPipelineAsset != null)
			{
				universalRenderPipelineAsset.renderScale = _graphicsSettings.renderScale;
			}
		}

		private void ApplyHDR()
		{
			UniversalRenderPipelineAsset universalRenderPipelineAsset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
			if (universalRenderPipelineAsset != null)
			{
				universalRenderPipelineAsset.supportsHDR = _graphicsSettings.enableHDR;
			}
		}

		private void ApplyBrightness()
		{
			Volume[] array = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
			foreach (Volume volume in array)
			{
				if (volume != null && volume.profile != null && volume.profile.TryGet<ColorAdjustments>(out var component))
				{
					component.postExposure.overrideState = true;
					component.postExposure.value = (_graphicsSettings.brightness - 1f) * 2f;
				}
			}
		}

		private void ApplyFilmGrain()
		{
			Volume[] array = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
			float filmGrain = _graphicsSettings.filmGrain;
			Volume[] array2 = array;
			foreach (Volume volume in array2)
			{
				if (!(volume == null) && !(volume.profile == null) && volume.profile.TryGet<FilmGrain>(out var component))
				{
					component.intensity.overrideState = true;
					component.intensity.value = filmGrain;
					component.active = filmGrain > 0f;
				}
			}
		}
	}
}

}
